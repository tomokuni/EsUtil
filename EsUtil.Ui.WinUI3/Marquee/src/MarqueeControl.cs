using EsUtil.Feature.Marquee.Core.Contract;
using static EsUtil.Feature.Marquee.MarqueeCore.Limits;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native;
using EsUtil.Platform.Core.Native.Win;
using EsUtil.Platform.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace EsUtil.Ui.WinUI3.Marquee;

/// <summary>リサイズ可能なドラッグ矩形のコントロール（WinUI 3 版）。</summary>
/// <remarks>
/// 他の UI 版と同じ UI 非依存のコアのファサード（<c>EsUtil.Feature.Marquee.Presenter</c> の <see cref="MarqueePresenter"/>）を使い、<br/>
/// 本コントロールは「入力の受付」「効果の適用」「通知の転送」「描画」のみを行う薄い層である。<br/>
/// <br/>
/// <b>基底クラス</b>: 子要素を重ねる <see cref="Canvas"/> を継承し、図形とラベルを要素として置く<br/>
/// （描画ライブラリを持たない）。<br/>
/// <br/>
/// <b>座標系</b>:<br/>
/// - 対象座標 = 選択対象（画像ピクセル・仮想スクリーンピクセル等）。 コアはこの座標で動作する。<br/>
/// - ローカル DIP = 本コントロールのローカル座標。 入力と描画はこの座標で行い、境界でポート（<see cref="ICoordTransform"/>）により変換する。<br/>
/// <br/>
/// <b>操作</b>:<br/>
/// - 左ドラッグ: 新規選択、またはハンドルのリサイズ・移動<br/>
/// - 矢印キー: nudge（1 対象ピクセル単位の微調整。 カーソルを非表示にして十字マーカーを表示する）<br/>
/// - Escape: キャンセル要求 / Enter: 確定要求 / 左ダブルクリック: 選択矩形の内外に応じて確定・キャンセル<br/>
/// <br/>
/// <b>他の UI 版との違い</b>:<br/>
/// - ラベル（サイズ・カーソル座標）も描画面へ描く（子要素のレイアウトを走らせない。 実寸の測定と描画が同じエンジンになる）。<br/>
/// - イベント引数の座標はコア型（<see cref="EsPoint"/> / <see cref="EsRect"/>）で公開する（境界変換が不要）。<br/>
/// - ビューポート（スクロール・ズーム）の変化はホストが検知して再描画を要求する。<br/>
/// - 矢印キーの nudge はフォーカスを必要とする（コントロールにフォーカスが無い間はキーが届かない）。<br/>
/// - ドラッグ中はポインター キャプチャを行う（コントロール外への移動でもイベントを受け取る）。<br/>
/// - ダブルクリックは OS の設定値（時間の上限）と押下位置の距離から本 UI が判定する。<br/>
/// - ログ出力は <see cref="Log"/> プロパティで注入する（デフォルトは <see cref="NullLogger"/>。 他の UI 版と同一の書式）。<br/>
/// <br/>
/// <b>見た目のカスタマイズ</b>: 色・太さ・表示可否は <see cref="Appearance"/> で、<br/>
/// 矩形の並びで表せる装飾とラベルの文言・位置は <see cref="DrawPlanResolver"/> で差し替えられる。<br/>
/// 矩形で表せない描画（円・斜線等）は外観（<see cref="MarqueeAppearance"/>）の <c>Show*</c> を false にして自前で描く<br/>
/// （表示の直後に呼ばれる <see cref="Rendered"/> で矩形を追加する）。
/// </remarks>
public sealed partial class MarqueeControl : Canvas
{
    /// <summary><see cref="Options"/> プロパティ（寸法・対話パラメータをまとめた設定）。</summary>
    /// <remarks>
    /// 個別に公開していた設定（外観の寸法・対話パラメータ）を 1 つの不変オブジェクトへ統合し、公開プロパティを 1 つに縮小する。<br/>
    /// 対話パラメータ（最小サイズ・ヒットテスト半径・境界外選択の禁止）はファサードへ伝搬し、判断と変更ログはコアが単一所有する。
    /// </remarks>
    public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(
        nameof(Options),
        typeof(MarqueeOptions),
        typeof(MarqueeControl),
        new PropertyMetadata(MarqueeOptions.Default, OnOptionsChanged));

    /// <summary><see cref="Appearance"/> プロパティ（外観。 描画にだけ必要な設定）。</summary>
    /// <remarks>
    /// 外観（色・ラベル体裁・表示可否）はコアの計算に入らない（描画にだけ必要）ため、コアの設定（<see cref="Options"/>）には含めず、<br/>
    /// 提示層（<c>EsUtil.Feature.Marquee.Presenter</c>）の <see cref="MarqueeAppearance"/> が単一所有する。
    /// </remarks>
    public static readonly DependencyProperty AppearanceProperty = DependencyProperty.Register(
        nameof(Appearance),
        typeof(MarqueeAppearance),
        typeof(MarqueeControl),
        new PropertyMetadata(MarqueeAppearance.Default, OnAppearanceChanged));

    /// <summary><see cref="HighlightSourceRect"/> プロパティ（ハイライトする矩形）。</summary>
    /// <remarks>
    /// <b>選択矩形ではなくホストが与える実行時状態</b>であるため、設定（<see cref="Options"/>）には含めない。
    /// </remarks>
    public static readonly DependencyProperty HighlightSourceRectProperty = DependencyProperty.Register(
        nameof(HighlightSourceRect),
        typeof(EsRect?),
        typeof(MarqueeControl),
        new PropertyMetadata(null, OnHighlightSourceRectChanged));

    /// <summary><see cref="ClientToPixelScaling"/> プロパティ（DIP → 物理ピクセルのスケーリング係数）。</summary>
    /// <remarks>
    /// 描画の倍率は描画面の実寸と割り当て領域の比から求めるため、本値が使われるのは割り当て領域が未確定の場合だけである<br/>
    /// （値はホストが現在の表示倍率を読むためにも公開する。 表示倍率の変化へは XAML のルートの変更通知で追従する）。
    /// </remarks>
    public static readonly DependencyProperty ClientToPixelScalingProperty = DependencyProperty.Register(
        nameof(ClientToPixelScaling),
        typeof(double),
        typeof(MarqueeControl),
        new PropertyMetadata(DpiScaler.StandardScaling, OnAffectsRenderChanged));

    /// <summary><see cref="ClientToSourceScaling"/> プロパティ（座標変換ポートへ適用する倍率）。</summary>
    /// <remarks>
    /// <see cref="SetCoordTransform"/> でポートを設定した場合は使用しない（等倍スケール専用の簡易指定）。<br/>
    /// 既定値はコアの <see cref="ClientToSourceScalingDefault"/> を参照する（既定値の二重所有を防ぐ）。
    /// </remarks>
    public static readonly DependencyProperty ClientToSourceScalingProperty = DependencyProperty.Register(
        nameof(ClientToSourceScaling),
        typeof(double),
        typeof(MarqueeControl),
        new PropertyMetadata(ClientToSourceScalingDefault, OnClientToSourceScalingChanged));

    /// <summary>ヒットテストを有効にするための透明な背景（全インスタンスで共有する）。</summary>
    /// <remarks>
    /// 背景を持たない要素はポインター ヒットテストの対象にならず、入力を受け取れない。<br/>
    /// 透明な背景を与えて、表示直後から入力を受け取れるようにする。
    /// </remarks>
    private static readonly SolidColorBrush s_hitTestBackground = new(Microsoft.UI.Colors.Transparent);

    /// <summary>コントロール化の提示層（設定・入力の解釈・描画計画の窓口）。</summary>
    /// <remarks>
    /// 入力の解釈・状態遷移の判断はコア（<c>EsUtil.Feature.Marquee.Core</c>）が、描画計画（幾何 + 外観 + 層の順序 + ラベル配置）は<br/>
    /// 本層が単一所有する（本コントロールは自 UI の公開プロパティを転送し、返された描画計画を描くだけである）。
    /// </remarks>
    private readonly MarqueePresenter _marquee;

    /// <summary>コアが要求する効果（フォーカス・再描画・カーソル形状・キャプチャ・座標変換）の適用先。</summary>
    private readonly IMarqueeEffects _effects;

    /// <summary>カーソルサービス（nudge のカーソル非表示・移動）。</summary>
    /// <remarks>既定実装（Win32）の解決は本 UI 層が行う（コアは抽象と <see langword="null"/> のみを扱う）。</remarks>
    private readonly ICursorService _cursor;

    /// <summary>ドラッグ矩形コントロールを既定構成で初期化する。</summary>
    public MarqueeControl()
        : this(null)
    {
    }

    /// <summary>カーソルサービスを注入してドラッグ矩形コントロールを初期化する。</summary>
    /// <param name="cursorService">カーソルサービス。 null の場合は Windows 既定実装を使用する。</param>
    /// <remarks>
    /// フォーカスを受けられるようにする（矢印キーの nudge がキー入力を受け取る前提条件）。<br/>
    /// 入力（ポインター・キー・フォーカス）の購読と、読み込み・破棄の購読を行う。<br/>
    /// コアからの通知は公開イベントへ転送し、設定（<see cref="Options"/>）をファサードへ伝搬する。
    /// </remarks>
    public MarqueeControl(ICursorService? cursorService)
    {
        IsTabStop = true;
        Background = s_hitTestBackground;
        _cursor = cursorService ?? new WinCursorService();
        _effects = new Effects(this);
        _marquee = new MarqueePresenter(_effects, _cursor, Options);
        _marquee.Subscribe(OnNotified);
        // 環境値とホストが与える実行時状態は入力であるため、本コントロールの現在値を提示層へ引き継ぐ
        // （外観は提示層が単一所有するため引き継がない: 外観は公開プロパティの設定で伝搬する）
        _marquee.ClientToSourceScaling = (double)GetValue(ClientToSourceScalingProperty);
        _marquee.HighlightSourceRect = (EsRect?)GetValue(HighlightSourceRectProperty);

        SubscribeInputEvents();
        Loaded += OnControlLoaded;
        Unloaded += OnControlUnloaded;
    }

    /// <summary>ドラッグの始点が決定されたときに発火する（新規選択のドラッグ開始。 座標は対象座標）。</summary>
    public event EventHandler<MarqueePointEventArgs>? SelectionStarted;

    /// <summary>選択矩形が変更されたときに発火する。</summary>
    public event EventHandler<SelectionRectEventArgs>? SelectionChanged;

    /// <summary>ドラッグ後の終点が決定され、選択矩形が確定したときに発火する。</summary>
    public event EventHandler<SelectionRectEventArgs>? SelectionCompleted;

    /// <summary>領域がキャンセルされたときに発火する（枠外ダブルクリック・小さすぎる矩形等）。</summary>
    public event EventHandler? SelectionCancelled;

    /// <summary>ルーペが表示する中心座標を通知する（対象座標）。</summary>
    /// <remarks>通常追従ではポインタ位置、ドラッグ・nudge 中は動いている側のハンドル位置を通知する。</remarks>
    public event EventHandler<MarqueePointEventArgs>? LoupePositionMoved;

    /// <summary>Enter キーによる確定要求時に発火する（選択確定後のみ。 ドラッグ中は発火しない）。</summary>
    public event EventHandler<SelectionRectEventArgs>? ConfirmRequested;

    /// <summary>Escape キーによるキャンセル要求時に発火する（選択の破棄は本コントロールが実施済み）。</summary>
    public event EventHandler? CancelRequested;

    /// <summary>描画完了（コントロールが図形とラベルを描き終えたこと）を通知するイベント。</summary>
    /// <remarks>
    /// サイズラベル・カーソル座標ラベルはコントロールが描画面へ描く。 操作ラベル・情報ラベルなどアプリ固有の情報は<br/>
    /// 本コントロールの概念ではないため、ホストが本イベントで自前で描く。<br/>
    /// 契約は文字に限定せず<b>描画内容全体</b>（<see cref="MarqueeRenderedEventArgs.Content"/>）を渡すため、ホストは文字以外の上書きもできる。<br/>
    /// <b>描画中に発火する</b>ため、ハンドラ内で描画を完了させること（キャンバスを保持しない）。<br/>
    /// 描画のたびに発火する（文字の有無は <c>Content.HasText</c> で判定できる）。
    /// </remarks>
    public event EventHandler<MarqueeRenderedEventArgs>? Rendered;

    /// <summary>寸法・対話パラメータをまとめた設定を取得または設定する。</summary>
    /// <value>設定（最小選択サイズ・ハンドルの寸法・ヒットテスト半径・境界外選択の禁止）。 既定は <see cref="MarqueeOptions.Default"/>。</value>
    /// <remarks>
    /// 設定を変更すると再描画され、対話パラメータ（最小サイズ・ヒットテスト半径・境界外選択の禁止）はファサードへ伝搬する。<br/>
    /// 一部の項目だけを変える場合は <c>MarqueeOptions.Default with { MinSelectionSize = 8 }</c> の形で指定する（指定しない項目は既定値を保つ）。<br/>
    /// 外観（色・ラベル体裁・表示可否）はコアの設定に含めない（描画にだけ必要な設定のため <see cref="Appearance"/> が単一所有する）。
    /// </remarks>
    public MarqueeOptions Options
    {
        get => (MarqueeOptions)GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    /// <summary>描画にだけ必要な外観（色・ラベル体裁・表示可否）を取得または設定する。</summary>
    /// <value>外観の単一値。 既定は <see cref="MarqueeAppearance.Default"/>。</value>
    /// <remarks>
    /// 外観はコアの計算に入らない（描画にだけ必要）ため、設定（<see cref="Options"/>）には含めない。<br/>
    /// 変更すると次の描画で提示層が新しい外観で描画計画を解決する。
    /// </remarks>
    public MarqueeAppearance Appearance
    {
        get => (MarqueeAppearance)GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    /// <summary>ハイライトする矩形（対象座標）を取得または設定する。</summary>
    /// <value>ハイライト矩形。 null（既定）の場合はハイライトしない。</value>
    /// <remarks>
    /// 選択矩形と異なり選択コントロールの操作では変化しない（ホストが与える）。<br/>
    /// 値がある場合は選択矩形より優先して注目矩形になる（優先順はコアが単一所有する）。
    /// </remarks>
    public EsRect? HighlightSourceRect
    {
        get => (EsRect?)GetValue(HighlightSourceRectProperty);
        set => SetValue(HighlightSourceRectProperty, value);
    }

    /// <summary>座標変換ポートへ適用する倍率を取得または設定する。</summary>
    /// <value>対象座標 / ローカル DIP の倍率。 既定は 1.0。</value>
    /// <remarks>
    /// 環境値（DPI・マルチスクリーン）のため <see cref="Options"/> には含めない。<br/>
    /// <see cref="SetCoordTransform"/> が未設定の場合のみ使われる（ポート優先）。<br/>
    /// 選択矩形と十字マーカーの描画はファサードの座標変換ポートのみを使うため本値の影響を受けない<br/>
    /// （本値が影響するのは外側の装飾のサイズラベル・ラベル位置）。
    /// </remarks>
    public double ClientToSourceScaling
    {
        get => (double)GetValue(ClientToSourceScalingProperty);
        set => SetValue(ClientToSourceScalingProperty, value);
    }

    /// <summary>サイズラベル表示用のスケーリング係数を取得または設定する（マルチスクリーン補正）。</summary>
    /// <value>DisplayScale の百分率に使う係数。 未設定（0 以下）の場合は <see cref="ClientToSourceScaling"/> と同じ値が使用される。</value>
    /// <remarks>0 以下を <see cref="ClientToSourceScaling"/> と同値として扱う規則はコア（<c>MarqueeCore.Limits.ResolveDisplayScale</c>）が単一所有する。</remarks>
    public double DisplayScaleScaling
    {
        get => _marquee.DisplayScaleScaling;
        set
        {
            _marquee.DisplayScaleScaling = value;
            InvalidateRender();
        }
    }

    /// <summary>サイズラベルのテキストを提供する関数を取得または設定する。</summary>
    /// <value>サイズラベルのテキストを返す関数。 null または空文字の場合は自動生成する。</value>
    /// <remarks>
    /// デリゲート（ホスト結線）のため外観（<see cref="Appearance"/>）には含めない（値の等価比較の対象外にするため）。<br/>
    /// 保持はコア（<see cref="MarqueePresenter.SizeLabelOverrideProvider"/>）が単一所有し、本 setter が再描画を要求する。<br/>
    /// <b>操作ラベル・情報ラベルは本コントロールの概念ではない</b>（アプリ固有の付加情報）ため、ホストが自前で描く。
    /// </remarks>
    public Func<string?>? SizeLabelOverrideProvider
    {
        get => _marquee.SizeLabelOverrideProvider;
        set
        {
            _marquee.SizeLabelOverrideProvider = value;
            InvalidateRender();
        }
    }

    /// <summary>サイズラベルの表示スケーリングを矩形ごとに解決するプロバイダを取得または設定する。</summary>
    /// <value>描画のたびに呼び出される関数（引数は対象座標の矩形）。 null の場合は <see cref="DisplayScaleScaling"/> を使う。</value>
    /// <remarks>
    /// マルチモニタでモニタごとに表示スケールが異なる場合、ホストは矩形からモニタを判定してそのスケールを返す。<br/>
    /// 文言（書式）はコアが単一所有するため、ホストは数値だけを与える。<br/>
    /// 保持はコア（<see cref="MarqueePresenter.SizeLabelDisplayScalingProvider"/>）が単一所有し、本 setter が再描画を要求する。
    /// </remarks>
    public Func<EsRect, double>? SizeLabelDisplayScalingProvider
    {
        get => _marquee.SizeLabelDisplayScalingProvider;
        set
        {
            _marquee.SizeLabelDisplayScalingProvider = value;
            InvalidateRender();
        }
    }

    /// <summary>表示 DIP を物理ピクセルへ変換するスケーリング係数を取得または設定する。</summary>
    /// <value>スケーリング係数。 表示倍率の変化（XAML のルートの変更通知）で自動更新される。</value>
    public double ClientToPixelScaling
    {
        get => (double)GetValue(ClientToPixelScalingProperty);
        set => SetValue(ClientToPixelScalingProperty, value);
    }

    /// <summary>現在の選択状態のスナップショットを取得する（ポーリング不要の完全状態）。</summary>
    /// <value>State / SelectionSourceRect / ActiveTarget / MousePosition / IsMouseDown / IsNudgeActive / HasSelection を含むスナップショット。</value>
    public MarqueeSnapshot CurrentSnapshot => _marquee.CreateSnapshot();

    /// <summary>選択矩形があるかどうかを取得する。</summary>
    /// <value>選択中の場合は true。</value>
    /// <remarks>描画のたびに参照されるため、スナップショットの生成を伴わない軽量なプロパティとする。</remarks>
    public bool HasSelection => _marquee.HasSelection;

    /// <summary>現在の選択矩形（対象座標）を取得する。</summary>
    /// <value>選択矩形（対象座標）。 未選択時は 0x0 の空矩形。</value>
    /// <remarks>描画のたびに参照されるため、スナップショットの生成を伴わない軽量なプロパティとする。</remarks>
    public EsRect SelectionSourceRect => _marquee.SelectionSourceRect;

    /// <summary>操作ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。 null を設定した場合は出力なしへフォールバックする。</value>
    /// <remarks>
    /// 保持とフォールバック、ファサードへの伝搬はコア（<see cref="MarqueePresenter.Log"/>）が単一所有する（本層は委譲するだけ）。<br/>
    /// 入力の判断に伴うログはコアが同じロガーで出力する。
    /// </remarks>
    public ILogger Log
    {
        get => _marquee.Log;
        set => _marquee.Log = value;
    }

    /// <summary>ログの接頭辞を取得または設定する（ロガーと一緒に設定する）。</summary>
    /// <value>接頭辞（例: <c>[Marquee]</c>）。 null または空白を設定すると既定値へ戻す。</value>
    /// <remarks>本コントロールのログ定義（<see cref="MarqueeLogMessage"/>）の接頭辞を設定し、提示層・コアへ伝搬する。</remarks>
    public string? LogPrefix
    {
        get => _logPrefix;
        set
        {
            _logPrefix = value;
            MarqueeLogMessage.Prefix = string.IsNullOrWhiteSpace(value) ? MarqueeLogMessage.DefaultPrefix : value;
            _marquee.LogPrefix = value;
        }
    }

    /// <summary>ログの接頭辞（未設定は null。 既定値は定義クラスが持つ）。</summary>
    private string? _logPrefix;

    /// <summary>描画計画を差し替える関数を取得または設定する。</summary>
    /// <value>計画（幾何・外観・層の順序・ラベル配置）を受け取り、差し替えた計画を返す関数。 null の場合は既定の計画をそのまま使う。</value>
    /// <remarks>
    /// <b>矩形の並びで表せる装飾と、ラベルの文言・位置を差し替える入口</b>である（色・太さ・表示可否は <see cref="Appearance"/>）。<br/>
    /// 受け取る計画は提示層が解決済みであるため、本関数は値の置き換えだけで完結する<br/>
    /// （<c>plan with { OuterBorderDipRects = …, Labels = … }</c>）。<br/>
    /// <b>純粋関数（同じ入力に同じ出力）にすること</b>（同じ入力では 1 フレーム 1 回だけ解決されるため、<br/>
    /// 呼び出しの時点に依存する関数を渡すと差し替えが反映されない）。 設定すると保持中の計画を破棄して再解決する。
    /// </remarks>
    public Func<MarqueeDrawPlan, MarqueeDrawPlan>? DrawPlanResolver
    {
        get;
        set
        {
            field = value;
            InvalidateRender();
        }
    }

    /// <summary>ポインターが本コントロールの上にあるかどうか（追従位置の取り直しの条件）。</summary>
    public bool IsPointerOver => _isPointerOver;

    /// <summary>追従位置（ルーペ中心。 対象座標）を実カーソルから取り直す。</summary>
    /// <param name="position">取り直した追従位置（対象座標）。 取り直さなかった場合は既定値。</param>
    /// <returns>取り直した場合は true。 追従していない場合（ドラッグ中・nudge 中・ポインターが本コントロールの外）と<br/>
    /// カーソル位置を取得できない場合（ウィンドウ未接続・OS の取得失敗・未ロード）は false。</returns>
    /// <remarks>
    /// <b>ポインター移動イベントの座標は入力へ積まれた分だけ古い</b>（最新の移動がまだ届いていない）ため、<br/>
    /// イベント処理の直後に呼ぶと、追従位置を実カーソルへ合わせられる（カーソルからの遅れを減らす）。<br/>
    /// 追従位置が実カーソルを表すかどうかは <see cref="MarqueeSnapshot.IsMarkerFollowingPointer"/> が単一所有する。<br/>
    /// ポインターが本コントロールの上にあるかどうかは <see cref="IsPointerOver"/>（ポインターの進入・退出から保持する状態）に従う<br/>
    /// （コントロールの外では追従位置を進めない）。<br/>
    /// 追従位置の採用は呼び出し側が決める（本メソッドは状態を変えない）。
    /// </remarks>
    public bool TryGetCurrentLoupePosition(out EsPoint position)
    {
        position = default;

        // 追従位置が実カーソルを表さない間（ドラッグ中・nudge 中）と、ポインターがコントロールの外にある間は取り直さない
        if (!CurrentSnapshot.IsMarkerFollowingPointer || !IsPointerOver)
        {
            return false;
        }

        if (CurrentCursorSourcePosition() is not { } sourcePosition)
        {
            return false;
        }

        position = sourcePosition;
        return true;
    }

    /// <summary>対象 ↔ ローカル DIP の座標変換ポートを設定する。</summary>
    /// <param name="port">座標変換ポート。 null の場合は等価変換（1 対象ピクセル = 1 DIP・無制限）。</param>
    /// <remarks>
    /// ホスト（上位のアプリ・コントロール）がモードに応じた変換ポートを設定する（範囲選択: 仮想スクリーンピクセル / 切出し: 元画像ピクセル）。<br/>
    /// 変換関数は呼び出し毎に評価されるため、ズーム・スクロール等の動的変化を反映できる。<br/>
    /// 保持と、ポート未指定時に倍率（<see cref="ClientToSourceScaling"/>）を使う規則はコアが単一所有する（本層は委譲するだけ）。
    /// </remarks>
    public void SetCoordTransform(ICoordTransform? port)
    {
        try
        {
            _marquee.SetCoordTransform(port);
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(SetCoordTransform));
            throw;
        }
    }

    /// <summary>選択矩形を外部状態から復元する。</summary>
    /// <param name="rect">選択矩形（対象座標）。</param>
    /// <remarks>
    /// 状態の更新と通知はコア（ファサード）が担う（<see cref="MarqueeOptions.ClampToSourceBounds"/> が有効な場合は境界内へクリップしてから設定する）。<br/>
    /// 設定後は <see cref="SelectionChanged"/> が発火し、再描画される。
    /// </remarks>
    public void SetSelection(EsRect rect)
    {
        try
        {
            _marquee.SetSelection(rect);
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            Log.LogOperationError(ex, nameof(SetSelection));
            throw;
        }
    }

    /// <summary>選択状態と nudge 状態を初期化する。</summary>
    /// <remarks>nudge 中だった場合はカーソルを再表示する（非表示残留の防止。 状態の初期化はコアが担う）。</remarks>
    public void ResetSelection()
    {
        try
        {
            _marquee.ResetSelection();
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            Log.LogOperationError(ex, nameof(ResetSelection));
            throw;
        }
    }

    /// <summary>キャンセル要求を処理する（Escape キーと同じ条件）。</summary>
    /// <remarks>
    /// ドラッグ中 / 選択ありの場合は選択を破棄した後に <see cref="CancelRequested"/> を発火する。<br/>
    /// モード差分（キャンセル後の後始末）は上位が本イベントを購読して行う。
    /// </remarks>
    public void RequestCancel()
    {
        try
        {
            _marquee.RequestCancel();
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            Log.LogOperationError(ex, nameof(RequestCancel));
            throw;
        }
    }

    /// <summary>確定要求を処理する（Enter キーと同じ条件）。</summary>
    /// <remarks>
    /// ドラッグ中は無視し、選択確定後のみ <see cref="ConfirmRequested"/> を発火する。<br/>
    /// 確定後に選択状態をクリアする（確定後の再ドラッグ・再確定を防ぐ）。
    /// </remarks>
    public void RequestConfirm()
    {
        try
        {
            _marquee.RequestConfirm();
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            Log.LogOperationError(ex, nameof(RequestConfirm));
            throw;
        }
    }

    /// <summary>コアからの通知を公開イベントへ転送する（判断はしない）。</summary>
    /// <param name="notice">通知の種別・座標・スナップショット。</param>
    /// <remarks>
    /// 転送のみを担う（確定/キャンセルの判断・状態の更新はコアが済ませている）。<br/>
    /// 本 UI は押下・解放の公開イベントを持たない（基底クラスのポインター イベントがサービスを提供済みのため）。
    /// </remarks>
    private void OnNotified(MarqueeNotice notice)
    {
        switch (notice.Kind)
        {
            // 選択（始点・変更・確定・キャンセル）
            case MarqueeNoticeKind.SelectionStarted: Raise(SelectionStarted, ToPointArgs(notice)); break;
            case MarqueeNoticeKind.SelectionChanged: Raise(SelectionChanged, ToRectArgs(notice.Snapshot)); break;
            case MarqueeNoticeKind.SelectionCompleted: Raise(SelectionCompleted, ToRectArgs(notice.Snapshot)); break;
            case MarqueeNoticeKind.SelectionCancelled: Raise(SelectionCancelled, EventArgs.Empty); break;

            // ルーペ（追従位置）・確定要求・キャンセル要求
            // コアは追従位置を汎用名（MarkerPositionMoved）で通知するため、ホスト向けの名前（ルーペ中心）へ写像する
            case MarqueeNoticeKind.MarkerPositionMoved: Raise(LoupePositionMoved, ToPointArgs(notice)); break;
            case MarqueeNoticeKind.ConfirmRequested: Raise(ConfirmRequested, ToRectArgs(notice.Snapshot)); break;
            case MarqueeNoticeKind.CancelRequested: Raise(CancelRequested, EventArgs.Empty); break;
        }
    }

    /// <summary>通知を点座標のイベント引数へ変換する。</summary>
    /// <param name="notice">通知。</param>
    /// <returns>通知座標（対象座標）とスナップショットを含むイベント引数。</returns>
    private static MarqueePointEventArgs ToPointArgs(MarqueeNotice notice) => new(notice.Position, notice.Snapshot);

    /// <summary>スナップショットを選択矩形のイベント引数へ変換する。</summary>
    /// <param name="snapshot">通知時点の状態スナップショット。</param>
    /// <returns>選択矩形とスナップショットを含むイベント引数。</returns>
    private static SelectionRectEventArgs ToRectArgs(MarqueeSnapshot snapshot)
        => new(snapshot.SelectionSourceRect, snapshot);

    /// <summary>イベントを発火する（発火の唯一の入口）。</summary>
    /// <typeparam name="T">イベント引数の型。</typeparam>
    /// <param name="handler">発火するイベント（購読者がいない場合は null）。</param>
    /// <param name="args">イベント引数。</param>
    /// <remarks>イベントの発火を 1 箇所へ集約する（[`コーディング規約.md`] §7。 各転送は本メソッドを呼ぶだけになる）。</remarks>
    private void Raise<T>(EventHandler<T>? handler, T args)
        where T : EventArgs
        => handler?.Invoke(this, args);

    /// <summary>イベント引数を持たないイベントを発火する（発火の唯一の入口）。</summary>
    /// <param name="handler">発火するイベント（購読者がいない場合は null）。</param>
    /// <param name="args">イベント引数（<see cref="EventArgs.Empty"/> を使う）。</param>
    private void Raise(EventHandler? handler, EventArgs args) => handler?.Invoke(this, args);

    /// <summary><see cref="Options"/> の変更をファサードへ伝搬し、再描画を要求する。</summary>
    /// <param name="sender">変更されたコントロール。</param>
    /// <param name="args">変更前後の値。</param>
    private static void OnOptionsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var control = (MarqueeControl)sender;
        control._marquee.Options = (MarqueeOptions)args.NewValue;
        control.InvalidateRender();
    }

    /// <summary><see cref="Appearance"/> の変更をファサードへ伝搬し、再描画を要求する。</summary>
    /// <param name="sender">変更されたコントロール。</param>
    /// <param name="args">変更前後の値。</param>
    private static void OnAppearanceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var control = (MarqueeControl)sender;
        control._marquee.Appearance = (MarqueeAppearance)args.NewValue;
        control.InvalidateRender();
    }

    /// <summary><see cref="HighlightSourceRect"/> の変更をファサードへ伝搬し、再描画を要求する。</summary>
    /// <param name="sender">変更されたコントロール。</param>
    /// <param name="args">変更前後の値。</param>
    private static void OnHighlightSourceRectChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var control = (MarqueeControl)sender;
        control._marquee.HighlightSourceRect = (EsRect?)args.NewValue;
        control.InvalidateRender();
    }

    /// <summary><see cref="ClientToSourceScaling"/> の変更をファサードへ伝搬し、再描画を要求する。</summary>
    /// <param name="sender">変更されたコントロール。</param>
    /// <param name="args">変更前後の値。</param>
    private static void OnClientToSourceScalingChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var control = (MarqueeControl)sender;
        control._marquee.ClientToSourceScaling = (double)args.NewValue;
        control.InvalidateRender();
    }

    /// <summary>適用先を持たない値（表示倍率）の変更を処理する（再描画だけを要求する）。</summary>
    /// <param name="sender">変更されたコントロール。</param>
    /// <param name="args">変更前後の値。</param>
    private static void OnAffectsRenderChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        => ((MarqueeControl)sender).InvalidateRender();

    /// <summary>コア（ファサード）が要求する効果を本コントロールへ適用する実装。</summary>
    /// <remarks>
    /// 効果の適用順序（フォーカス → カーソル形状 → 再描画）はコアが単一所有するため、<br/>
    /// 本実装は呼ばれた順に自 UI の操作へ接続するだけでよい（判断を持たない）。<br/>
    /// <see cref="MarqueeEffectsBase"/> を継承し、本 UI が必要とする効果だけを override する。<br/>
    /// コアは <see cref="IMarqueeEffects"/> のみに依存し、UI フレームワークを知らない。
    /// </remarks>
    private sealed class Effects(MarqueeControl owner) : MarqueeEffectsBase
    {
        /// <summary>フォーカスを取得する（矢印キー nudge の受け付け開始）。</summary>
        public override void RequestFocus() => owner.Focus(FocusState.Programmatic);

        /// <summary>再描画を要求する。</summary>
        /// <remarks>要求の集約は <see cref="MarqueeControl.InvalidateRender"/> が単一所有する。</remarks>
        public override void RequestRedraw() => owner.InvalidateRender();

        /// <summary>カーソル形状をコアが示す種別に応じて更新する。</summary>
        /// <param name="kind">カーソル形状の種別（コア提供）。</param>
        public override void SetCursorShape(MarqueeCursorKind kind) => owner.ApplyCursorShape(kind);

        /// <summary>ポインター キャプチャを取得または解放する。</summary>
        /// <param name="capture">true = 取得 / false = 解放。</param>
        /// <remarks>ドラッグ中にポインターがコントロール外へ出ても移動イベントを受け取れるようにする。</remarks>
        public override void SetPointerCapture(bool capture) => owner.ApplyPointerCapture(capture);

        /// <summary>対象座標を画面物理座標へ変換する。</summary>
        /// <param name="sourcePoint">対象座標。</param>
        /// <returns>画面物理座標。 ウィンドウを特定できない場合は既定値。</returns>
        public override EsPointInt ToScreenPoint(EsPoint sourcePoint) => owner.ToScreen(sourcePoint);
    }
}
