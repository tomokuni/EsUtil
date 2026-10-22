using EsUtil.Feature.Marquee.Core.Contract;
using EsUtil.Ui.WinForms.Marquee.Internal;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native;
using EsUtil.Platform.Core.Native.Win;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.ComponentModel;

namespace EsUtil.Ui.WinForms.Marquee;

/// <summary>リサイズ可能なドラッグ矩形のコントロール（WinForms 版）。</summary>
/// <remarks>
/// 他の UI 版と同じ UI 非依存のコアのファサード（<c>EsUtil.Feature.Marquee.Core</c> の <see cref="MarqueePresenter"/>）を使い、<br/>
/// 本コントロールは「入力の受付」「効果の適用」「通知の転送」「描画」のみを行う薄い層である。<br/>
/// <br/>
/// <b>座標系</b>:<br/>
/// - 対象座標 = 選択対象（画像ピクセル・仮想スクリーンピクセル等）。 コアはこの座標で動作する。<br/>
/// - ローカル DIP = 本コントロールのローカル座標。 入力と描画はこの座標で行い、境界でポート（<see cref="ICoordTransform"/>）により変換する。<br/>
/// <br/>
/// <b>操作</b>:<br/>
/// - 左ドラッグ: 新規選択、またはハンドルのリサイズ・移動<br/>
/// - 矢印キー: nudge（1 対象ピクセル単位の微調整。 カーソルを非表示にして十字マーカーを表示する）<br/>
/// - Escape: キャンセル要求 / Enter: 確定要求<br/>
/// <br/>
/// <b>他の UI 版との違い</b>:<br/>
/// - 描画は GDI+（<see cref="Graphics"/>）へ直接行う。 中間ビットマップを経由しない。<br/>
/// - ラベル（文字）は描画計画に従って本コントロールが描く（文字を子要素として置く UI 版とは異なる）。<br/>
/// - 背景を透明にして下の画像を透かす（<see cref="ControlStyles.SupportsTransparentBackColor"/>）。<br/>
/// - <c>ScrollViewer</c> に相当する自動連携は行わない。 ビューポートの変化はホストが検知して再描画を要求する。<br/>
/// - 矢印キーの nudge はフォーカスを必要とする（押下時にフォーカスを取得する）。<br/>
/// - ドラッグ中はマウス キャプチャを行う（コントロール外への移動でもイベントを受け取る）。<br/>
/// - ログ出力は <see cref="Log"/> プロパティで注入する（既定は <see cref="NullLogger"/>。 他の UI 版と同一の書式）。<br/>
/// <br/>
/// <b>見た目のカスタマイズ</b>: 色・太さ・表示可否は <see cref="Appearance"/> で、<br/>
/// 矩形の並びで表せる装飾とラベルの文言・位置は <see cref="DrawPlanResolver"/> で差し替えられる。<br/>
/// 矩形で表せない描画（円・斜線等）は外観（<see cref="MarqueeAppearance"/>）の <c>Show*</c> を false にして自前で描く<br/>
/// （描画の直後に呼ばれる <see cref="Rendered"/> で描画先へ描き、必要なら <see cref="DrawPlan"/> で既定の描画を行う）。<br/>
/// <br/>
/// <b>デザイナー連携</b>: 公開プロパティは実行時にコードで与える値（コアの設定・外観・座標変換ポート・ログ）であるため、<br/>
/// デザイナーのコード生成（<c>InitializeComponent</c>）の対象にしない（<see cref="DesignerSerializationVisibility.Hidden"/>）。
/// </remarks>
public sealed class MarqueeControl : Control
{
    /// <summary>コントロール化の提示層（設定・入力の解釈・描画計画の窓口）。</summary>
    /// <remarks>
    /// 入力の解釈・状態遷移の判断はコア（<c>EsUtil.Feature.Marquee.Core</c>）が、描画計画（幾何 + 外観 + 層の順序 + ラベル配置）は<br/>
    /// 提示層が単一所有する（本コントロールは自 UI の公開プロパティを転送し、返された描画計画を描くだけである）。
    /// </remarks>
    private readonly MarqueePresenter _marquee;

    /// <summary>コアが要求する効果（フォーカス・再描画・カーソル形状・キャプチャ・座標変換）の適用先。</summary>
    private readonly IMarqueeEffects _effects;

    /// <summary>カーソルサービス（nudge のカーソル非表示・移動）。</summary>
    /// <remarks>既定実装（Win32）の解決は本 UI 層が行う（コアは抽象と <see langword="null"/> のみを扱う）。</remarks>
    private readonly ICursorService _cursor;

    /// <summary>寸法・対話パラメータをまとめた設定（<see cref="Options"/> の保持先）。</summary>
    private MarqueeOptions _options = MarqueeOptions.Default;

    /// <summary>直近に解決した描画計画（null = 未解決）。</summary>
    private MarqueeDrawPlan? _frame;

    /// <summary>直近に解決した描画計画の基準サイズ（DIP）。</summary>
    private EsSize _frameSize;

    /// <summary>ログの接頭辞（未設定は null。 既定値は定義クラスが持つ）。</summary>
    private string? _logPrefix;

    /// <summary>ポインターが本コントロールの上にあるかどうか。</summary>
    private bool _isPointerOver;

    /// <summary>表示 DIP を物理ピクセルへ変換するスケーリング係数（<see cref="ClientToPixelScaling"/> の保持先）。</summary>
    /// <remarks>DPI の通知（<see cref="Control.RescaleConstantsForDpi"/>）が更新する。</remarks>
    private double _clientToPixelScaling = DpiScaler.StandardScaling;

    /// <summary>描画計画を差し替える関数（<see cref="DrawPlanResolver"/> の保持先）。</summary>
    private Func<MarqueeDrawPlan, MarqueeDrawPlan>? _drawPlanResolver;

    /// <summary>ドラッグ矩形コントロールを既定構成で初期化する。</summary>
    public MarqueeControl()
        : this(new WinCursorService())
    {
    }

    /// <summary>カーソルサービスを注入してドラッグ矩形コントロールを初期化する。</summary>
    /// <param name="cursorService">カーソルサービス（Windows の既定実装を使う場合は <see cref="WinCursorService"/> を渡す）。</param>
    /// <remarks>
    /// 描画は自前で行い（<see cref="ControlStyles.UserPaint"/>）、ちらつきを抑えるため二重バッファリングする。<br/>
    /// 背景を透明にして下の画像を透かす（オーバーレイのための必須の設定）。<br/>
    /// 矢印キーの nudge とダブルクリック（確定/キャンセル）を受け取るため、選択可能・標準クリックのスタイルを有効にする。<br/>
    /// フォーカスを失った時点で nudge を終了する（カーソルの非表示残留を防ぐ）。<br/>
    /// コアからの通知は公開イベントへ転送し、設定（<see cref="Options"/>）をファサードへ伝搬する。
    /// </remarks>
    public MarqueeControl(ICursorService cursorService)
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
            true);

        // 下の画像を透かすため、親の描画（画像を含む）を自分の背景として描く（オーバーレイのための必須の設定）。
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;

        // 矢印キー nudge のフォーカスと、ダブルクリック（確定/キャンセル）の検出に必要な設定。
        SetStyle(ControlStyles.Selectable | ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);

        _cursor = cursorService;
        _effects = new Effects(this);
        _marquee = new MarqueePresenter(_effects, _cursor, _options);
        _marquee.Subscribe(OnNotified);
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
    /// <remarks>通常追従ではポインター位置、ドラッグ・nudge 中は動いている側のハンドル位置を通知する。</remarks>
    public event EventHandler<MarqueePointEventArgs>? LoupePositionMoved;

    /// <summary>Enter キーによる確定要求時に発火する（選択確定後のみ。 ドラッグ中は発火しない）。</summary>
    public event EventHandler<SelectionRectEventArgs>? ConfirmRequested;

    /// <summary>Escape キーによるキャンセル要求時に発火する（選択の破棄は本コントロールが実施済み）。</summary>
    public event EventHandler? CancelRequested;

    /// <summary>描画完了（コントロールが図形とラベルを描き終えたこと）を通知するイベント。</summary>
    /// <remarks>
    /// 契約は文字に限定せず<b>描画内容全体</b>（<see cref="MarqueeRenderedEventArgs.Content"/>）を渡すため、ホストは文字以外の上書きもできる。<br/>
    /// <b>描画中に発火する</b>ため、ハンドラー内で描画を完了させること（描画面を保持しない）。<br/>
    /// 描画のたびに発火する（文字の有無は <c>Content.HasText</c> で判定できる）。
    /// </remarks>
    public event EventHandler<MarqueeRenderedEventArgs>? Rendered;

    /// <summary>寸法・対話パラメータをまとめた設定を取得または設定する。</summary>
    /// <value>設定（ハンドルの寸法・十字マーカーの長さ・最小選択サイズ・ヒットテスト半径・境界外選択の禁止）。 既定は <see cref="MarqueeOptions.Default"/>。</value>
    /// <remarks>
    /// 設定を変更すると再描画され、対話パラメータ（最小サイズ・ヒットテスト半径・境界外選択の禁止）はファサードへ伝搬する。<br/>
    /// 一部の項目だけを変える場合は <c>MarqueeOptions.Default with { MinSelectionSize = 8 }</c> の形で指定する（指定しない項目は既定値を保つ）。<br/>
    /// 外観（色・ラベル体裁・表示可否）はコアの設定に含めない（描画にだけ必要な設定のため <see cref="Appearance"/> が単一所有する）。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public MarqueeOptions Options
    {
        get => _options;
        set
        {
            _options = value;
            _marquee.Options = value;
            InvalidateRender();
        }
    }

    /// <summary>描画にだけ必要な外観（色・ラベル体裁・表示可否）を取得または設定する。</summary>
    /// <value>外観の単一値。 既定は <see cref="MarqueeAppearance.Default"/>。</value>
    /// <remarks>
    /// 外観はコアの計算に入らない（描画にだけ必要）ため、コアの設定（<see cref="Options"/>）ではなく本プロパティが与える。<br/>
    /// 変更すると次の描画で提示層が新しい外観で描画計画を解決する。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public MarqueeAppearance Appearance
    {
        get => _marquee.Appearance;
        set
        {
            _marquee.Appearance = value;
            InvalidateRender();
        }
    }

    /// <summary>ハイライトする矩形（対象座標）を取得または設定する。</summary>
    /// <value>ハイライト矩形。 null（既定）の場合はハイライトしない。</value>
    /// <remarks>
    /// 選択矩形と異なり選択コントロールの操作では変化しない（ホストが与える）。<br/>
    /// 値がある場合は選択矩形より優先して注目矩形になる（優先順はコアが単一所有する）。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EsRect? HighlightSourceRect
    {
        get => _marquee.HighlightSourceRect;
        set
        {
            _marquee.HighlightSourceRect = value;
            InvalidateRender();
        }
    }

    /// <summary>座標変換ポートへ適用する倍率を取得または設定する。</summary>
    /// <value>対象座標 / ローカル DIP の倍率。 既定は 1.0。</value>
    /// <remarks>
    /// 環境値（DPI・マルチスクリーン）のため <see cref="Options"/> には含めない。<br/>
    /// <see cref="SetCoordTransform"/> が未設定の場合のみ使われる（ポート優先）。<br/>
    /// 選択矩形と十字マーカーの描画はファサードの座標変換ポートのみを使うため本値の影響を受けない<br/>
    /// （本値が影響するのは外側の装飾のサイズラベル・ラベル位置）。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double ClientToSourceScaling
    {
        get => _marquee.ClientToSourceScaling;
        set
        {
            _marquee.ClientToSourceScaling = value;
            InvalidateRender();
        }
    }

    /// <summary>サイズラベル表示用のスケーリング係数を取得または設定する（マルチスクリーン補正）。</summary>
    /// <value>DisplayScale の百分率に使う係数。 未設定（0 以下）の場合は <see cref="ClientToSourceScaling"/> と同じ値が使用される。</value>
    /// <remarks>0 以下を <see cref="ClientToSourceScaling"/> と同値として扱う規則はコア（<see cref="MarqueePresenter.DisplayScaleScaling"/>）が単一所有する。</remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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
    /// <value>スケーリング係数。 DPI 変更時に自動更新される（描画面の DPI を解決できない場合の代替値）。</value>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double ClientToPixelScaling
    {
        get => _clientToPixelScaling;
        set
        {
            _clientToPixelScaling = value;
            InvalidateRender();
        }
    }

    /// <summary>描画計画を差し替える関数を取得または設定する。</summary>
    /// <value>計画（幾何・外観・層の順序・ラベル配置）を受け取り、差し替えた計画を返す関数。 null の場合は既定の計画をそのまま使う。</value>
    /// <remarks>
    /// <b>矩形の並びで表せる装飾と、ラベルの文言・位置を差し替える入口</b>である（色・太さ・表示可否は <see cref="Appearance"/>）。<br/>
    /// 受け取る計画は提示層が解決済みであるため、本関数は値の置き換えだけで完結する<br/>
    /// （<c>plan with { OuterBorderDipRects = …, Labels = … }</c>）。<br/>
    /// <b>純粋関数（同じ入力に同じ出力）にすること</b>（同じ入力では 1 フレーム 1 回だけ解決されるため、<br/>
    /// 呼び出しの時点に依存する関数を渡すと差し替えが反映されない）。 設定すると保持中の計画を破棄して再解決する。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<MarqueeDrawPlan, MarqueeDrawPlan>? DrawPlanResolver
    {
        get => _drawPlanResolver;
        set
        {
            _drawPlanResolver = value;
            InvalidateRender();
        }
    }

    /// <summary>操作ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。 null を設定した場合は <see cref="NullLogger"/> へフォールバックする（出力なし）。</value>
    /// <remarks>
    /// 保持とフォールバック、ファサードへの伝搬は提示層（<see cref="MarqueePresenter.Log"/>）が単一所有する（本層は委譲するだけ）。<br/>
    /// 入力の判断に伴うログはコアが同じロガーで出力する。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ILogger Log
    {
        get => _marquee.Log;
        set => _marquee.Log = value;
    }

    /// <summary>ログの接頭辞を取得または設定する（ロガーと一緒に設定する）。</summary>
    /// <value>接頭辞（例: <c>[Marquee]</c>）。 null または空白を設定すると既定値へ戻す。</value>
    /// <remarks>本コントロールのログ定義（<see cref="MarqueeLogMessage"/>）の接頭辞を設定し、提示層・コアへ伝搬する。</remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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

    /// <summary>現在の選択状態のスナップショットを取得する（ポーリング不要の完全状態）。</summary>
    /// <value>State / SelectionSourceRect / ActiveTarget / MousePosition / IsMouseDown / IsNudgeActive を含むスナップショット。</value>
    public MarqueeSnapshot CurrentSnapshot => _marquee.CreateSnapshot();

    /// <summary>選択矩形があるかどうかを取得する。</summary>
    /// <value>選択中の場合は true。</value>
    /// <remarks>描画のたびに参照されるため、スナップショットの生成を伴わない軽量なプロパティとする。</remarks>
    public bool HasSelection => _marquee.HasSelection;

    /// <summary>現在の選択矩形（対象座標）を取得する。</summary>
    /// <value>選択矩形（対象座標）。 未選択時は 0x0 の空矩形。</value>
    /// <remarks>描画のたびに参照されるため、スナップショットの生成を伴わない軽量なプロパティとする。</remarks>
    public EsRect SelectionSourceRect => _marquee.SelectionSourceRect;

    /// <summary>描画計画を GDI+ の描画面へ描く（計画どおりに描く）。</summary>
    /// <param name="graphics">描画先（計画と同じ座標系であること）。 null 不可。</param>
    /// <param name="plan">描く描画計画。</param>
    /// <exception cref="ArgumentNullException"><paramref name="graphics"/> が null の場合。</exception>
    /// <remarks>
    /// <b>既定の描画をホストへ提供する入口</b>である（ホストが自前の描画と組み合わせて使う。 例: 自前の下地 → 本メソッド → 自前の上描き）。<br/>
    /// <b>計画どおり（外観の <c>Show*</c> を適用済みの層）に描く</b>ため、装飾を止めている場合は描かれない。<br/>
    /// 描画実装（<see cref="MarqueeGraphicsPainter"/>）は本アセンブリの内部であるため、外部へは本メソッドが窓口になる。<br/>
    /// 計画を必要とする場合は <see cref="Rendered"/> の <c>Content</c> を使う。
    /// </remarks>
    public static void DrawPlan(Graphics graphics, in MarqueeDrawPlan plan)
        => MarqueeGraphicsPainter.Paint(graphics, in plan);

    /// <summary>追従位置（ルーペ中心。 対象座標）を実カーソルから取り直す。</summary>
    /// <param name="position">取り直した追従位置（対象座標）。 取り直さなかった場合は既定値。</param>
    /// <returns>取り直した場合は true。 追従していない場合（ドラッグ中・nudge 中・ポインターが本コントロールの外）と<br/>
    /// カーソル位置を取得できない場合（ウィンドウ未接続・OS の取得失敗）は false。</returns>
    /// <remarks>
    /// <b>ポインター移動イベントの座標は入力へ積まれた分だけ古い</b>（最新の移動がまだ届いていない）ため、<br/>
    /// イベント処理の直後に呼ぶと、追従位置を実カーソルへ合わせられる（カーソルからの遅れを減らす）。<br/>
    /// 追従位置が実カーソルを表すかどうかは <see cref="MarqueeSnapshot.IsMarkerFollowingPointer"/> が単一所有する。<br/>
    /// ポインターが本コントロールの上にあるかどうかはマウスの進入と離脱（<c>OnMouseEnter</c> / <c>OnMouseLeave</c>）に従う<br/>
    /// （コントロールの外では追従位置を進めない）。<br/>
    /// 追従位置の採用は呼び出し側が決める（本メソッドは状態を変えない）。
    /// </remarks>
    public bool TryGetCurrentLoupePosition(out EsPoint position)
    {
        position = default;

        // 追従位置が実カーソルを表さない間（ドラッグ中・nudge 中）と、ポインターがコントロールの外にある間は取り直さない
        if (!CurrentSnapshot.IsMarkerFollowingPointer || !_isPointerOver)
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
    /// ホスト（上位のアプリ）がモードに応じた変換ポートを設定する（範囲選択: 仮想スクリーンピクセル / 切出し: 元画像ピクセル）。<br/>
    /// 変換関数は呼び出し毎に評価されるため、ズーム・スクロール等の動的変化を反映できる。<br/>
    /// 保持と、ポート未指定時に倍率（<see cref="ClientToSourceScaling"/>）を使う規則はコアが単一所有する（本層は委譲するだけ）。
    /// </remarks>
    public void SetCoordTransform(ICoordTransform? port)
    {
        try
        {
            _marquee.SetCoordTransform(port);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
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
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
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
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
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
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
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
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            Log.LogOperationError(ex, nameof(RequestConfirm));
            throw;
        }
    }

    /// <summary>描画内容の再解決と再描画を要求する（描画に関わる変更の唯一の入口）。</summary>
    /// <remarks>
    /// 保持している描画計画を破棄し、次の解決で最新の状態を反映する。<br/>
    /// 設定・実行時状態の変更、コアからの再描画要求（<see cref="IMarqueeEffects.RequestRedraw"/>）は本メソッドへ集約する。
    /// </remarks>
    public void InvalidateRender()
    {
        InvalidateFrame();
        Invalidate();
    }

    /// <summary>矢印キー（nudge）・Escape（キャンセル）・Enter（確定）を入力キーとして扱う。</summary>
    /// <param name="keyData">判定するキー（修飾キーを含む）。</param>
    /// <returns>本コントロールが入力として扱う場合は true。</returns>
    /// <remarks>
    /// 既定では矢印キーはダイアログ キー（フォーカス移動）として扱われ、<see cref="OnKeyDown"/> が呼ばれない。<br/>
    /// 基底の判定は上書きせず、本コントロールが扱うキーだけを追加する。
    /// </remarks>
    protected override bool IsInputKey(Keys keyData)
        => (keyData & Keys.KeyCode)
            is Keys.Left or Keys.Up or Keys.Right or Keys.Down or Keys.Escape or Keys.Return
        || base.IsInputKey(keyData);

    /// <summary>描画計画を解決して描画面へ描き、付加情報の描画機会をホストへ通知する。</summary>
    /// <param name="e">描画の情報（描画面を含む）。</param>
    /// <remarks>
    /// <b>描画の手順は提示層が単一所有する</b>: 層の順序と描画 API への写し方は描画計画（<see cref="MarqueeDrawPlan"/>）が持ち、<br/>
    /// 本メソッドは自 UI の描画面へ写すだけである（<see cref="MarqueeGraphicsPainter.Paint"/>）。<br/>
    /// 描画面へ DPI の拡大を適用してから DIP 座標で描画する（コアの計算と座標系を一致させる）。<br/>
    /// 背景は塗らない（透明のままにして下の画像を透かす）。
    /// </remarks>
    protected override void OnPaint(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPaint(e);

        // 倍率は描画面（物理ピクセル）と割り当て領域（DIP）の比から求める
        // （DPI の通知はハンドル生成より前に届かないため、保持値だけに頼ると 100% で描いてしまう）
        var scaling = ResolveClientScaling(e.Graphics);
        var clientSize = DpiScaler.ToDip(new EsSize(ClientSize.Width, ClientSize.Height), scaling);

        var state = e.Graphics.Save();
        e.Graphics.ScaleTransform((float)scaling, (float)scaling);
        try
        {
            var plan = ResolveFrame(clientSize);

            MarqueeGraphicsPainter.Paint(e.Graphics, in plan);

            // 描画完了をホストへ通知する（操作・情報ラベルなどホスト固有の付加情報を描くための入口）
            RaiseRendered(new MarqueeRenderedEventArgs(e.Graphics, plan));
        }
        finally
        {
            e.Graphics.Restore(state);
        }
    }

    /// <summary>左ボタン押下時にドラッグを開始する。</summary>
    /// <param name="e">マウスの情報。</param>
    /// <remarks>
    /// 押下位置を対象座標へ変換してコア（ファサード）へ通知する（ダブルクリックの確定/キャンセル・nudge の<br/>
    /// ハンドルのスナップはコアの判断に従う）。<br/>
    /// ドラッグ中はポインターがコントロール外へ出ても移動イベントを受け取れるよう、マウス キャプチャを取得する。
    /// </remarks>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _marquee.Press(ToSource(e.Location), e.Clicks);
    }

    /// <summary>ドラッグ中のマウス移動を処理する。</summary>
    /// <param name="e">マウスの情報。</param>
    /// <remarks>
    /// 移動位置を対象座標へ変換してコア（ファサード）へ通知する。<br/>
    /// ドラッグの適用・nudge 中のエコー移動の抑制・カーソル形状の決定はコアの判断に従う。
    /// </remarks>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseMove(e);

        _marquee.Move(ToSource(e.Location));
    }

    /// <summary>左ボタン解放時にドラッグを終了する。</summary>
    /// <param name="e">マウスの情報。</param>
    /// <remarks>
    /// 解放位置を対象座標へ変換してコア（ファサード）へ通知し、マウス キャプチャを解放する。<br/>
    /// 最小サイズによるキャンセル・確定・クリック検出・nudge の終了はコアの判断に従う。
    /// </remarks>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseUp(e);

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _marquee.Release(ToSource(e.Location));
    }

    /// <summary>ポインターが本コントロールへ入ったことを記録する。</summary>
    /// <param name="e">イベントの情報。</param>
    /// <remarks>追従位置（<see cref="TryGetCurrentLoupePosition"/>）を実カーソルから取り直してよいかの判定に使う。</remarks>
    protected override void OnMouseEnter(EventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseEnter(e);

        _isPointerOver = true;
    }

    /// <summary>ポインターが本コントロールから出たことを記録する。</summary>
    /// <param name="e">イベントの情報。</param>
    /// <remarks>コントロールの外では追従位置を進めない（<see cref="TryGetCurrentLoupePosition"/> が false を返す）。</remarks>
    protected override void OnMouseLeave(EventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseLeave(e);

        _isPointerOver = false;
    }

    /// <summary>キーダウン処理（矢印キーの nudge・Escape のキャンセル・Enter の確定）。</summary>
    /// <param name="e">キーイベントの情報。</param>
    /// <remarks>
    /// キーをコア型（<see cref="EsKey"/>）へ変換して通知し、返された効果（再描画・カーソル操作）を適用するだけである。<br/>
    /// 矢印キー nudge・Escape・Enter の判断（nudge の許可条件を含む）はコアが行う。<br/>
    /// <c>|| e.Handled</c> は、先に誰かが処理済みにしたフラグを false へ戻さないため。<br/>
    /// 処理したキーは押下音と既定の処理を抑止する（<c>SuppressKeyPress</c>）。<br/>
    /// 本コントロールにフォーカスがある場合のみ呼ばれる（マウス押下時に自動でフォーカスを取得する）。
    /// </remarks>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnKeyDown(e);

        e.Handled = _marquee.KeyDown(e.KeyCode.ToEsKey(), CurrentCursorSourcePosition()) || e.Handled;
        e.SuppressKeyPress = e.Handled;
    }

    /// <summary>フォーカスを失った時点で nudge を終了する。</summary>
    /// <param name="e">イベントの情報。</param>
    /// <remarks>nudge 中はカーソルを非表示にしているため、フォーカス喪失時に再表示する（非表示残留の防止）。</remarks>
    protected override void OnLostFocus(EventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnLostFocus(e);

        _marquee.EndNudge();
    }

    /// <summary>ハンドルの生成時にスケーリング係数を DPI へ追随させる。</summary>
    /// <param name="e">イベントの情報。</param>
    /// <remarks>
    /// ハンドルの生成前は DPI を取得できないため、生成時に解決して保持する。<br/>
    /// 保持しないと、DPI の変更が届くまで倍率が 1.0 のままになり、高 DPI の環境で入力の座標がずれる<br/>
    /// （描画は描画面の DPI から倍率を解決するため、描画だけが正しくなる）。
    /// </remarks>
    protected override void OnHandleCreated(EventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnHandleCreated(e);

        ClientToPixelScaling = DpiScaler.ToScaling(DeviceDpi);
        InvalidateRender();
    }

    /// <summary>DPI の変更時に表示スケーリング係数を追従させる。</summary>
    /// <param name="deviceDpiOld">変更前の DPI。</param>
    /// <param name="deviceDpiNew">変更後の DPI。</param>
    /// <remarks>モニター間の移動で DPI が変わっても、枠線・ハンドル・マーカーの画面上の大きさを保つ。</remarks>
    protected override void RescaleConstantsForDpi(int deviceDpiOld, int deviceDpiNew)
    {
        base.RescaleConstantsForDpi(deviceDpiOld, deviceDpiNew);

        ClientToPixelScaling = DpiScaler.ToScaling(deviceDpiNew);
        InvalidateRender();
    }

    /// <summary>描画に使うスケーリング係数（DIP → 物理ピクセル）を解決する。</summary>
    /// <param name="graphics">描画面。</param>
    /// <returns>スケーリング係数（0.1〜8.0 へ補正済み）。</returns>
    /// <remarks>
    /// 描画面の実サイズ（物理ピクセル）と、同じ辺の割り当て領域（DIP。 描画面の DPI から求める）の比を採る。<br/>
    /// 割り当て領域が確定しない場合（クライアント領域が 0 の間）は、DPI の通知から保持している係数を使う。<br/>
    /// 画面の DPI が描画面と食い違っても（DPI の通知が届いていない）、描画面の実測を使うため描画は一致する。
    /// </remarks>
    private double ResolveClientScaling(Graphics graphics)
    {
        var paintScaling = DpiScaler.ToScaling(graphics.DpiX);
        return ClientPixelScale.Resolve(
            ClientSize.Width,
            DpiScaler.ToDip(ClientSize.Width, paintScaling),
            ClientToPixelScaling);
    }

    /// <summary>1 フレーム分の描画計画を解決して保持する（同じ基準サイズでは再解決しない）。</summary>
    /// <param name="clientSize">描画領域のサイズ（DIP。 DIP 換算済みの描画先サイズ）。</param>
    /// <returns>そのフレームの描画計画。</returns>
    /// <remarks>
    /// 描画は 1 フレーム 1 回だけ解決する（解決を 2 回行うと選択矩形・装飾・ラベル文言の解決と確保が重複する）。<br/>
    /// 実寸の測定は描画 API に依存するため、本層が <see cref="GraphicsText.Measure"/> を渡す<br/>
    /// （配置の計算（クランプ）は提示層が単一所有する）。<br/>
    /// 状態が変わると <see cref="InvalidateFrame"/> が破棄し、次の解決で最新の状態を反映する。
    /// </remarks>
    private MarqueeDrawPlan ResolveFrame(EsSize clientSize)
    {
        if (_frame is not { } cached || _frameSize != clientSize)
        {
            cached = _marquee.ResolveDrawPlan(clientSize, GraphicsText.Measure);
            cached = DrawPlanResolver?.Invoke(cached) ?? cached;
            _frame = cached;
            _frameSize = clientSize;
        }

        return cached;
    }

    /// <summary>保持している描画計画を破棄する（次の解決で最新の状態を反映する）。</summary>
    private void InvalidateFrame() => _frame = null;

    /// <summary>ローカル DIP 座標を対象座標へ変換する。</summary>
    /// <param name="clientPixels">クライアント座標（物理ピクセル。 <see cref="Control"/> のマウス イベントが渡す座標）。</param>
    /// <returns>対象座標。</returns>
    /// <remarks>
    /// <b>物理ピクセルを DIP へ換算してから変換する</b>: WinForms のマウス イベントは物理ピクセルで届くが、<br/>
    /// 描画計画と座標変換ポートは DIP を前提とする（描画は DPI の拡大を適用してから DIP で描く）。<br/>
    /// 換算しないと、高 DPI の環境でポインター位置と選択枠が倍率の分だけずれる。
    /// </remarks>
    private EsPoint ToSource(Point clientPixels) => _marquee.ToSource(ToLocalDip(clientPixels));

    /// <summary>クライアント座標（物理ピクセル）をローカル DIP 座標へ変換する。</summary>
    /// <param name="clientPixels">クライアント座標（物理ピクセル）。</param>
    /// <returns>ローカル DIP 座標。</returns>
    /// <remarks>倍率は DPI の通知（<see cref="Control.OnHandleCreated"/> / <see cref="Control.RescaleConstantsForDpi"/>）が保つ値を使う。</remarks>
    private EsPoint ToLocalDip(Point clientPixels)
    {
        var scaling = DpiScaler.NormalizeScaling(ClientToPixelScaling);
        return new EsPoint(
            DpiScaler.ToDip(clientPixels.X, scaling),
            DpiScaler.ToDip(clientPixels.Y, scaling));
    }

    /// <summary>対象座標を画面物理座標へ変換する。</summary>
    /// <param name="sourcePos">対象座標。</param>
    /// <returns>画面物理座標。 ハンドル未生成（ウィンドウ未接続）の場合は既定値。</returns>
    /// <remarks>
    /// カーソルの移動（nudge）に使用する。 変換できない場合は原点へカーソルが飛ぶため、<br/>
    /// 呼び出し側は nudge 中のウィンドウ表示を前提とする。<br/>
    /// <c>PointToScreen</c> は物理ピクセルを要求するため、DIP から物理ピクセルへ換算してから渡す。
    /// </remarks>
    private EsPointInt ToScreen(EsPoint sourcePos)
    {
        if (!IsHandleCreated)
        {
            return default;
        }

        var scaling = DpiScaler.NormalizeScaling(ClientToPixelScaling);
        var localDip = _marquee.ToDip(sourcePos);
        var localPixels = new Point(
            DpiScaler.ScaleToPixel(localDip.X, scaling),
            DpiScaler.ScaleToPixel(localDip.Y, scaling));
        return PointToScreen(localPixels).ToEsPointInt();
    }

    /// <summary>現在のカーソル位置（対象座標）を取得する。</summary>
    /// <returns>カーソル位置（対象座標）。 ハンドル未生成・カーソル位置を取得できない場合は null。</returns>
    /// <remarks>
    /// <b>nudge の基点</b>（マウス移動が未発生でも現在位置から微調整を開始でき、原点へ飛ばない）と、<br/>
    /// <b>追従位置の取り直し</b>（<see cref="TryGetCurrentLoupePosition"/>。 入力へ積まれた分だけ古いイベント座標を実カーソルで置き換える）に使う。<br/>
    /// 画面物理座標 → クライアント座標 → 対象座標の順に変換する（境界の 1 行変換）。
    /// </remarks>
    private EsPoint? CurrentCursorSourcePosition()
    {
        if (!IsHandleCreated)
        {
            return null;
        }

        // 位置を取得できない場合は基点を記録しない（失敗を原点で表さないため Try 系を使う）
        if (!_cursor.TryGetCursorScreenPoint(out var screenPoint))
        {
            return null;
        }

        return _marquee.ToSource(ToLocalDip(PointToClient(screenPoint.ToPoint())));
    }

    /// <summary>カーソル形状をコアが示す種別に応じて更新する。</summary>
    /// <param name="kind">カーソル形状の種別（コア提供）。</param>
    /// <remarks>種別と自 UI のカーソルの対応のみを持つ（どのハンドルが同じカーソルになるかはコアが判断済み）。</remarks>
    private void ApplyCursorShape(MarqueeCursorKind kind)
        => Cursor = kind switch
        {
            MarqueeCursorKind.DiagonalNWSE => Cursors.SizeNWSE,
            MarqueeCursorKind.DiagonalNESW => Cursors.SizeNESW,
            MarqueeCursorKind.Vertical => Cursors.SizeNS,
            MarqueeCursorKind.Horizontal => Cursors.SizeWE,
            MarqueeCursorKind.Move => Cursors.SizeAll,
            _ => Cursors.Default,
        };

    /// <summary>コアからの通知を公開イベントへ転送する（判断はしない）。</summary>
    /// <param name="notice">通知の種別・座標・スナップショット。</param>
    /// <remarks>
    /// 転送のみを担う（確定/キャンセルの判断・状態の更新はコアが済ませている）。<br/>
    /// 本 UI は押下・解放の公開イベントを持たない（生のポインター入力はホストが自 UI のイベントで観測する）。
    /// </remarks>
    private void OnNotified(MarqueeNotice notice)
    {
        switch (notice.Kind)
        {
            // 選択（始点・変更・確定・キャンセル）
            case MarqueeNoticeKind.SelectionStarted:
                RaiseSelectionStarted(ToPointArgs(notice));
                break;

            case MarqueeNoticeKind.SelectionChanged:
                RaiseSelectionChanged(ToRectArgs(notice));
                break;

            case MarqueeNoticeKind.SelectionCompleted:
                RaiseSelectionCompleted(ToRectArgs(notice));
                break;

            case MarqueeNoticeKind.SelectionCancelled:
                RaiseSelectionCancelled();
                break;

            // ルーペ（追従位置）・確定要求・キャンセル要求
            // コアは追従位置を汎用名（MarkerPositionMoved）で通知するため、ホスト向けの名前（ルーペ中心）へ写像する
            case MarqueeNoticeKind.MarkerPositionMoved:
                RaiseLoupePositionMoved(ToPointArgs(notice));
                break;

            case MarqueeNoticeKind.ConfirmRequested:
                RaiseConfirmRequested(ToRectArgs(notice));
                break;

            case MarqueeNoticeKind.CancelRequested:
                RaiseCancelRequested();
                break;
        }
    }

    /// <summary>通知を点座標のイベント引数へ変換する。</summary>
    /// <param name="notice">通知。</param>
    /// <returns>通知座標（対象座標）とスナップショットを含むイベント引数。</returns>
    private static MarqueePointEventArgs ToPointArgs(MarqueeNotice notice) => new(notice.Position, notice.Snapshot);

    /// <summary>通知を選択矩形のイベント引数へ変換する。</summary>
    /// <param name="notice">通知。</param>
    /// <returns>選択矩形（対象座標）とスナップショットを含むイベント引数。</returns>
    private static SelectionRectEventArgs ToRectArgs(MarqueeNotice notice) => ToRectArgs(notice.Snapshot);

    /// <summary>スナップショットを選択矩形のイベント引数へ変換する。</summary>
    /// <param name="snapshot">通知時点の状態スナップショット。</param>
    /// <returns>選択矩形とスナップショットを含むイベント引数。</returns>
    private static SelectionRectEventArgs ToRectArgs(MarqueeSnapshot snapshot)
        => new(snapshot.SelectionSourceRect, snapshot);

    /// <summary>ドラッグの始点の通知を発火する。</summary>
    /// <param name="args">通知するイベント引数。</param>
    private void RaiseSelectionStarted(MarqueePointEventArgs args)
    {
        var handler = SelectionStarted;
        handler?.Invoke(this, args);
    }

    /// <summary>選択矩形の変更の通知を発火する。</summary>
    /// <param name="args">通知するイベント引数。</param>
    private void RaiseSelectionChanged(SelectionRectEventArgs args)
    {
        var handler = SelectionChanged;
        handler?.Invoke(this, args);
    }

    /// <summary>選択矩形の確定の通知を発火する。</summary>
    /// <param name="args">通知するイベント引数。</param>
    private void RaiseSelectionCompleted(SelectionRectEventArgs args)
    {
        var handler = SelectionCompleted;
        handler?.Invoke(this, args);
    }

    /// <summary>領域のキャンセルの通知を発火する。</summary>
    private void RaiseSelectionCancelled()
    {
        var handler = SelectionCancelled;
        handler?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>ルーペ中心の移動の通知を発火する。</summary>
    /// <param name="args">通知するイベント引数。</param>
    private void RaiseLoupePositionMoved(MarqueePointEventArgs args)
    {
        var handler = LoupePositionMoved;
        handler?.Invoke(this, args);
    }

    /// <summary>確定要求の通知を発火する。</summary>
    /// <param name="args">通知するイベント引数。</param>
    private void RaiseConfirmRequested(SelectionRectEventArgs args)
    {
        var handler = ConfirmRequested;
        handler?.Invoke(this, args);
    }

    /// <summary>キャンセル要求の通知を発火する。</summary>
    private void RaiseCancelRequested()
    {
        var handler = CancelRequested;
        handler?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>描画完了の通知を発火する。</summary>
    /// <param name="args">通知するイベント引数。</param>
    private void RaiseRendered(MarqueeRenderedEventArgs args)
    {
        var handler = Rendered;
        handler?.Invoke(this, args);
    }

    /// <summary>コア（ファサード）が要求する効果を本コントロールへ適用する実装。</summary>
    /// <remarks>
    /// 効果の適用順序（フォーカス → カーソル形状 → 再描画）はコアが単一所有するため、<br/>
    /// 本実装は呼ばれた順に自 UI の操作へ接続するだけでよい（判断を持たない）。<br/>
    /// <see cref="MarqueeEffectsBase"/> を継承し、本 UI が必要とする効果だけを override する。<br/>
    /// コアは <see cref="IMarqueeEffects"/> のみに依存し、UI フレームワークを知らない。
    /// </remarks>
    /// <param name="owner">効果の適用先（本コントロール）。</param>
    private sealed class Effects(MarqueeControl owner) : MarqueeEffectsBase
    {
        /// <summary>フォーカスを取得する（矢印キー nudge の受け付け開始）。</summary>
        /// <remarks>ハンドル未生成（ウィンドウ未接続）の間はフォーカスできないため何もしない。</remarks>
        public override void RequestFocus()
        {
            if (owner.IsHandleCreated)
            {
                owner.Focus();
            }
        }

        /// <summary>再描画を要求する。</summary>
        /// <remarks>要求の集約は <see cref="MarqueeControl.InvalidateRender"/> が単一所有する。</remarks>
        public override void RequestRedraw() => owner.InvalidateRender();

        /// <summary>カーソル形状をコアが示す種別に応じて更新する。</summary>
        /// <param name="kind">カーソル形状の種別（コア提供）。</param>
        public override void SetCursorShape(MarqueeCursorKind kind) => owner.ApplyCursorShape(kind);

        /// <summary>マウス キャプチャを取得または解放する。</summary>
        /// <param name="capture">true = 取得 / false = 解放。</param>
        /// <remarks>ドラッグ中にポインターがコントロール外へ出ても移動イベントを受け取れるようにする。</remarks>
        public override void SetPointerCapture(bool capture) => owner.Capture = capture;

        /// <summary>対象座標を画面物理座標へ変換する。</summary>
        /// <param name="sourcePoint">対象座標。</param>
        /// <returns>画面物理座標。 ハンドル未生成（ウィンドウ未接続）の場合は既定値。</returns>
        public override EsPointInt ToScreenPoint(EsPoint sourcePoint) => owner.ToScreen(sourcePoint);
    }
}
