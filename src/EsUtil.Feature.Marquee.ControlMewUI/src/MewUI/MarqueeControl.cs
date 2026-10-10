using Aprillz.MewUI;
using EsUtil.Feature.Marquee.MewUI.Internal;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Skia.Controls;
using EsUtil.Feature.Marquee.Core.Contract;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native;
using EsUtil.Platform.Core.Native.Win;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using static EsUtil.Feature.Marquee.MarqueeCore.Limits;



namespace EsUtil.Feature.Marquee.MewUI;

/// <summary>リサイズ可能なドラッグ矩形のコントロール（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版と同じ UI 非依存のコアのファサード（<c>EsUtil.Feature.Marquee.Core</c> の <see cref="MarqueePresenter"/>）を使い、<br/>
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
/// <b>Avalonia 版との違い</b>:<br/>
/// - 描画は中間ビットマップを経由せず <see cref="SkiaCanvasView"/> のキャンバスへ直接行う。<br/>
/// - イベント引数の座標はコア型（<see cref="EsPoint"/> / <see cref="EsRect"/>）で公開する（境界変換が不要）。<br/>
/// - <c>ScrollViewer</c> の自動連携は行わない。 ビューポートの変化はホストが検知して再描画を要求する。<br/>
/// - 矢印キーの nudge はフォーカスを必要とする（Avalonia 版の TopLevel 購読によるフォーカス非依存とは異なる）。<br/>
/// - ドラッグ中はマウスキャプチャを行う（コントロール外への移動でもイベントを受け取る）。<br/>
/// - ログ出力は <see cref="Log"/> プロパティで注入する（デフォルトは <see cref="NullLogger"/>。 Avalonia 版と同一の書式）。<br/>
/// <br/>
/// <b>見た目のカスタマイズ</b>: 色・太さ・表示可否は <see cref="Appearance"/> で、<br/>
/// 矩形の並びで表せる装飾とラベルの文言・位置は <see cref="DrawPlanResolver"/> で差し替えられる。<br/>
/// 矩形で表せない描画（円・斜線等）は外観（<see cref="MarqueeAppearance"/>）の <c>Show*</c> を false にして自前で描く<br/>
/// （描画の直後に呼ばれる <see cref="Rendered"/> で描画先へ描き、必要なら <see cref="DrawPlan"/> で既定の描画を行う）。
/// </remarks>
public sealed class MarqueeControl : Panel
{
    /// <summary><see cref="Options"/> プロパティ（寸法・対話パラメータをまとめた設定）。</summary>
    /// <remarks>
    /// 個別に公開していた設定（外観 10 項目・対話パラメータ 3 項目）を 1 つの不変オブジェクトへ統合し、公開プロパティを 1 つに縮小する。<br/>
    /// 設定値は不変（<see cref="MarqueeOptions"/>）であるため、同値の再設定では変更通知が発生しない（無駄な再描画を防ぐ）。<br/>
    /// 対話パラメータ（最小サイズ・ヒットテスト半径・境界外選択の禁止）はファサードへ伝搬し、判断と変更ログはコアが単一所有する。
    /// </remarks>
    public static readonly MewProperty<MarqueeOptions> OptionsProperty =
        MewProperty<MarqueeOptions>.Register<MarqueeControl>(
            nameof(Options), MarqueeOptions.Default, MewPropertyOptions.AffectsRender,
            changed: static (owner, _, newValue) =>
            {
                owner._marquee.Options = newValue;
                owner.InvalidateRender();
            });

    /// <summary><see cref="Appearance"/> プロパティ（外観。 描画にだけ必要な設定）。</summary>
    /// <remarks>
    /// 外観（色・ラベル体裁・表示可否）はコアの計算に入らない（描画にだけ必要）ため、コアの設定（<see cref="Options"/>）には含めず、<br/>
    /// 提示層（<c>EsUtil.Feature.Marquee.Presenter</c>）の <see cref="MarqueeAppearance"/> が単一所有する。
    /// </remarks>
    public static readonly MewProperty<MarqueeAppearance> AppearanceProperty =
        MewProperty<MarqueeAppearance>.Register<MarqueeControl>(
            nameof(Appearance), MarqueeAppearance.Default, MewPropertyOptions.AffectsRender,
            changed: static (owner, _, newValue) =>
            {
                owner._marquee.Appearance = newValue;
                owner.InvalidateRender();
            });

    /// <summary><see cref="HighlightSourceRect"/> プロパティ（ハイライトする矩形）。</summary>
    /// <remarks>
    /// <b>選択矩形ではなくホストが与える実行時状態</b>であるため、設定（<see cref="Options"/>）には含めない。
    /// </remarks>
    public static readonly MewProperty<EsRect?> HighlightSourceRectProperty =
        MewProperty<EsRect?>.Register<MarqueeControl>(
            nameof(HighlightSourceRect), null, MewPropertyOptions.AffectsRender,
            changed: static (owner, _, newValue) =>
            {
                owner._marquee.HighlightSourceRect = newValue;
                owner.InvalidateRender();
            });

    /// <summary><see cref="ClientToPixelScaling"/> プロパティ（DIP → 物理ピクセルのスケーリング係数）。</summary>
    public static readonly MewProperty<double> ClientToPixelScalingProperty =
        MewProperty<double>.Register<MarqueeControl>(nameof(ClientToPixelScaling), DpiScaler.StandardScaling, MewPropertyOptions.AffectsRender);

    /// <summary><see cref="ClientToSourceScaling"/> プロパティ（座標変換ポートへ適用する倍率）。</summary>
    /// <remarks>
    /// <see cref="SetCoordTransform"/> でポートを設定した場合は使用しない（等倍スケール専用の簡易指定）。<br/>
    /// 既定値はコアの <see cref="ClientToSourceScalingDefault"/> を参照する（既定値の二重所有を防ぐ）。
    /// </remarks>
    public static readonly MewProperty<double> ClientToSourceScalingProperty =
        MewProperty<double>.Register<MarqueeControl>(
            nameof(ClientToSourceScaling), ClientToSourceScalingDefault, MewPropertyOptions.AffectsRender,
            changed: static (owner, _, newValue) =>
            {
                owner._marquee.ClientToSourceScaling = newValue;
                owner.InvalidateRender();
            });

    /// <summary>コントロール化の提示層（設定・入力の解釈・描画計画の窓口）。</summary>
    /// <remarks>
    /// 入力の解釈・状態遷移の判断はコア（<c>EsUtil.Feature.Marquee.Core</c>）が、描画計画（幾何 + 外観 + 層の順序 + ラベル配置）は<br/>
    /// 本層が単一所有する（本コントロールは自 UI の公開プロパティを転送し、返された描画計画を描くだけである）。
    /// </remarks>
    private readonly MarqueePresenter _marquee;

    /// <summary>図形（装飾・ハンドル・始点マーカー）を描画する内部キャンバス。</summary>
    /// <remarks>
    /// <b>描画順を構造で保証する</b>: 本パネルが図形用キャンバスを先に、ラベル要素を後に子として持つため、<br/>
    /// ラベルは常に図形より後に描かれる（コアの「ラベルは最上位」契約が構造で成り立つ）。<br/>
    /// 入力は本パネルが受ける（本キャンバスはヒットテスト対象外）。
    /// </remarks>
    private readonly ShapeCanvas _shapes;

    /// <summary>配置中のラベル要素（背景 + テキスト）。 子の末尾側へ置くため常に最上位になる。</summary>
    private readonly List<Border> _labels = [];

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
    /// 静止画として扱い連続再描画を無効化する（選択の変更時のみ再描画する）。<br/>
    /// フォーカスを失った時点で nudge を終了する（カーソルの非表示残留を防ぐ）。<br/>
    /// 矢印キーの nudge はフォーカスを必要とする（Avalonia 版の TopLevel 購読によるフォーカス非依存とは異なる）。<br/>
    /// コアからの通知は公開イベントへ転送し、設定（<see cref="Options"/>）をファサードへ伝搬する。
    /// </remarks>
    public MarqueeControl(ICursorService? cursorService)
    {
        Focusable = true;
        _cursor = cursorService ?? new WinCursorService();
        _effects = new Effects(this);
        _marquee = new MarqueePresenter(_effects, _cursor, Options);
        _marquee.Subscribe(OnNotified);
        // 環境値とホストが与える実行時状態は入力であるため、本コントロールの現在値を提示層へ引き継ぐ
        // （外観は提示層が単一所有するため引き継がない: 外観は公開プロパティの設定で伝搬する）
        _marquee.ClientToSourceScaling = GetValue(ClientToSourceScalingProperty);
        _marquee.HighlightSourceRect = GetValue(HighlightSourceRectProperty);

        // 図形は専用の子キャンバスが描く（ラベル要素より先に子として加えるため、常にラベルの下になる）
        _shapes = new ShapeCanvas(this) { IsHitTestVisible = false };
        Add(_shapes);

        // フォーカスを失ったら nudge を終了する（カーソル非表示の残留防止）
        // `this.` は必須である（外すと MewUI の引数なしの OnLostFocus へ解決されて CS1501 になる）
        this.OnLostFocus(EndNudge);
    }

    /// <summary>図形（装飾・ハンドル・始点マーカー）だけを描画する内部 Skia キャンバス。</summary>
    /// <remarks>
    /// 親（<see cref="MarqueeControl"/>）の描画内容を読んで描くだけであり、判断を持たない。<br/>
    /// 連続再描画を無効化し、親からの要求（選択・設定の変更）があるときだけ描き直す。
    /// </remarks>
    private sealed class ShapeCanvas : SkiaCanvasView
    {
        /// <summary>描画内容の取得元（親コントロール）。</summary>
        private readonly MarqueeControl _owner;

        /// <summary>描画先の親を指定して初期化する。</summary>
        /// <param name="owner">描画内容の取得元（親コントロール）。</param>
        public ShapeCanvas(MarqueeControl owner)
        {
            _owner = owner;
            // 静止画として扱う（選択・設定の変更時のみ再描画する）
            ContinuousAnimation = false;
            PaintSurface += _owner.OnPaintSurface;
        }
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

    /// <summary>描画完了（コントロールが図形を描き終えたこと）を通知するイベント。</summary>
    /// <remarks>
    /// 本コントロールは文字（ラベル・座標ラベル）を描かない。 テキストの実寸測定は描画 API に依存するため、<br/>
    /// 「どの文言をどこへ描くか」はコアが決め、実際の配置は本コントロールが行う（<c>MarqueePresenter</c>）。<br/>
    /// 契約は文字に限定せず**描画内容全体**（<see cref="MarqueeRenderedEventArgs.Content"/>）を渡すため、ホストは文字以外の上書きもできる。<br/>
    /// <b>描画中に発火する</b>ため、ハンドラ内で描画を完了させること（キャンバスを保持しない）。<br/>
    /// 描画のたびに発火する（文字の有無は <c>Content.HasText</c> で判定できる）。
    /// </remarks>
    public event EventHandler<MarqueeRenderedEventArgs>? Rendered;

    /// <summary>寸法・対話パラメータをまとめた設定を取得または設定する。</summary>
    /// <value>設定（枠線・ハンドル・十字マーカー・座標ラベルの外観、最小選択サイズ・ヒットテスト半径・境界外選択の禁止）。 既定は <see cref="MarqueeOptions.Default"/>。</value>
    /// <remarks>
    /// 設定を変更すると再描画され、対話パラメータ（最小サイズ・ヒットテスト半径・境界外選択の禁止）はファサードへ伝搬する。<br/>
    /// 一部の項目だけを変える場合は <c>MarqueeOptions.Default with { MinSelectionSize = 8 }</c> の形で指定する（指定しない項目は既定値を保つ）。<br/>
    /// 外観（色・ラベル体裁・表示可否）はコアの設定に含めない（描画にだけ必要な設定のため <see cref="Appearance"/> が単一所有する）。
    /// </remarks>
    public MarqueeOptions Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    /// <summary>描画にだけ必要な外観（色・ラベル体裁・表示可否）を取得または設定する。</summary>
    /// <value>外観の単一値。 既定は <see cref="MarqueeAppearance.Default"/>。</value>
    /// <remarks>
    /// 外観はコアの計算に入らない（描画にだけ必要）ため、コアの設定（<see cref="Options"/>）ではなく本プロパティが与える。<br/>
    /// 変更すると次の描画で提示層が新しい外観で描画計画を解決する。
    /// </remarks>
    public MarqueeAppearance Appearance
    {
        get => GetValue(AppearanceProperty);
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
        get => GetValue(HighlightSourceRectProperty);
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
        get => GetValue(ClientToSourceScalingProperty);
        set => SetValue(ClientToSourceScalingProperty, value);
    }

    /// <summary>サイズラベル表示用のスケーリング係数を取得または設定する（マルチスクリーン補正）。</summary>
    /// <value>DisplayScale の百分率に使う係数。 未設定（0 以下）の場合は <see cref="ClientToSourceScaling"/> と同じ値が使用される。</value>
    /// <remarks>0 以下を <see cref="ClientToSourceScaling"/> と同値として扱う規則はコア（<see cref="MarqueePresenter.DisplayScaleScaling"/>）が単一所有する。</remarks>
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
    /// <value>スケーリング係数。 DPI 変更時に自動更新される。</value>
    public double ClientToPixelScaling
    {
        get => GetValue(ClientToPixelScalingProperty);
        set => SetValue(ClientToPixelScalingProperty, value);
    }

    /// <summary>現在の選択状態のスナップショットを取得する（ポーリング不要の完全状態）。</summary>
    /// <value>State / SelectionSourceRect / ActiveTarget / MousePosition / IsMouseDown / IsDragging / NudgeActive / HasSelection を含むスナップショット。</value>
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
    /// <value>ロガー。 null を設定した場合は <see cref="NullLogger"/> へフォールバックする（出力なし）。</value>
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

    /// <summary>対象 ↔ ローカル DIP の座標変換ポートを設定する。</summary>
    /// <param name="port">座標変換ポート。 null の場合は等価変換（1 対象ピクセル = 1 DIP・無制限）。</param>
    /// <remarks>
    /// ホスト（上位 AP）がモードに応じた変換ポートを設定する（範囲選択: 仮想スクリーンピクセル / 切出し: 元画像ピクセル）。<br/>
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

    /// <summary>要求サイズを返す（親の残余を埋める）。</summary>
    /// <param name="availableSize">親から提示された利用可能サイズ。</param>
    /// <returns>常に <c>Size.Empty</c>。</returns>
    /// <remarks>選択オーバーレイは下の画像と同じ領域を占めるため、自身の要求サイズを持たない。</remarks>
    protected override Size MeasureContent(Size availableSize)
    {
        // 子（図形のキャンバスとラベル要素）へ利用可能サイズを伝搬する
        _shapes.Measure(availableSize);

        foreach (var label in _labels)
        {
            label.Measure(availableSize);
        }

        return Size.Empty;
    }

    // ====================================================================
    // 1 フレーム分の描画計画（1 フレーム = 1 回だけ解決する）
    // ====================================================================

    /// <summary>直近に解決した描画計画（null = 未解決）。</summary>
    private MarqueeDrawPlan? _frame;

    /// <summary>直近に解決した描画計画の基準サイズ（DIP）。</summary>
    private EsSize _frameSize;

    /// <summary>1 フレーム分の描画計画を解決して保持する（同じ基準サイズでは再解決しない）。</summary>
    /// <param name="clientSize">描画領域のサイズ（DIP。 DIP 換算済みの描画先サイズ）。</param>
    /// <returns>そのフレームの描画計画。</returns>
    /// <remarks>
    /// 図形の描画（<c>OnPaintSurface</c>）とラベルの配置（<c>ArrangeContent</c>）が同じ描画計画を使う。<br/>
    /// 解決を 2 回行うと、選択矩形・装飾・ラベル文言の解決と確保が 1 フレームで重複する。<br/>
    /// 実寸の測定は描画 API に依存するため、本層が <see cref="MewUITextMeasure"/>（MewUI のテキスト エンジン）を渡す<br/>
    /// （配置の計算（クランプ）は提示層が単一所有する）。<br/>
    /// ラベルはテキスト要素として置くため、要素を描くエンジンで測らないと背景から文字がはみ出して切れる。<br/>
    /// 状態が変わると <see cref="InvalidateFrame"/> が破棄し、次の解決で最新の状態を反映する。
    /// </remarks>
    private MarqueeDrawPlan ResolveFrame(EsSize clientSize)
    {
        if (_frame is not { } cached || _frameSize != clientSize)
        {
            cached = _marquee.ResolveDrawPlan(clientSize, MewUITextMeasure.Measure);
            cached = DrawPlanResolver?.Invoke(cached) ?? cached;
            _frame = cached;
            _frameSize = clientSize;
        }

        return cached;
    }

    /// <summary>保持している描画計画を取得する（未解決の場合のみ解決する）。</summary>
    /// <param name="clientSize">描画領域のサイズ（DIP。 未解決時の解決にだけ使う）。</param>
    /// <returns>そのフレームの描画計画。</returns>
    /// <remarks>
    /// 描画フェーズ（<c>OnPaintSurface</c>）は配置フェーズが解決した値をそのまま使う<br/>
    /// （配置と描画で基準サイズが丸めによりわずかに違っても、1 フレームの解決を 2 回にしない）。
    /// </remarks>
    private MarqueeDrawPlan CurrentFrame(EsSize clientSize) => _frame ?? ResolveFrame(clientSize);

    /// <summary>保持している描画計画を破棄する（次の解決で最新の状態を反映する）。</summary>
    private void InvalidateFrame() => _frame = null;

    // ====================================================================
    // 見た目のカスタマイズ（描画計画の差し替えと既定描画の可否）
    // ====================================================================

    /// <summary>描画計画を差し替える関数を取得または設定する。</summary>
    /// <value>計画（幾何・外観・層の順序・ラベル配置）を受け取り、差し替えた計画を返す関数。 null の場合は既定の計画をそのまま使う。</value>
    /// <remarks>
    /// <b>矩形の並びで表せる装飾と、ラベルの文言・位置を差し替える入口</b>である（色・太さ・表示可否は <see cref="Appearance"/>）。<br/>
    /// 受け取る計画は提示層が解決済みであるため、本関数は値の置き換えだけで完結する<br/>
    /// （<c>plan with { OuterBorderDipRects = …, Labels = … }</c>）。<br/>
    /// <b>配置と描画の両方に適用される</b>ため、<see cref="MarqueeDrawPlan.Labels"/> を差し替えるとラベル要素も追随する。<br/>
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

    /// <summary>描画計画を自 UI の描画面へ描く（計画どおりに描く）。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標で描画できる状態であること）。 null 不可。</param>
    /// <param name="plan">描く描画計画。 null 不可。</param>
    /// <exception cref="ArgumentNullException"><paramref name="canvas"/> が null の場合。</exception>
    /// <remarks>
    /// <b>既定の描画をホストへ提供する入口</b>である（ホストが自前の描画と組み合わせて使う。 例: 自前の下地 → 本メソッド → 自前の上描き）。<br/>
    /// <b>計画どおり（外観の <c>Show*</c> を適用済みの層）に描く</b>ため、装飾を止めている場合は描かれない。<br/>
    /// 描画実装（<c>MarqueeSkiaPainter</c>）は本プロジェクトの内部であるため、外部へは本メソッドが窓口になる。<br/>
    /// 計画を必要とする場合は <see cref="Rendered"/> の <c>Content</c> を使う。
    /// </remarks>
    public static void DrawPlan(SKCanvas canvas, in MarqueeDrawPlan plan)
        => MarqueeSkiaPainter.Paint(canvas, in plan);

    /// <summary>描画内容の再解決と、図形・ラベルの再描画を要求する（描画に関わる変更の唯一の入口）。</summary>
    /// <remarks>
    /// 図形は子（<see cref="ShapeCanvas"/> = <c>SkiaCanvasView</c>）が描くため、親の <c>InvalidateVisual</c> だけでは再描画されない<br/>
    /// （子のサーフェスは自ら再描画を要求されたときだけ描き直し、初期描画の内容が残る）。<br/>
    /// ラベルは子要素の配置フェーズで内容と位置が変わるため、配置の無効化も併せて要求する。<br/>
    /// 保持している描画計画を破棄し、次の解決で最新の状態を反映する。
    /// </remarks>
    public void InvalidateRender()
    {
        InvalidateFrame();
        InvalidateVisual();
        _shapes.InvalidateVisual();
        InvalidateArrange();
    }

    /// <summary>子（図形のキャンバス・ラベル要素）を配置する。</summary>
    /// <param name="bounds">本コントロールへ割り当てられた矩形（ウィンドウ絶対座標・DIP）。</param>
    /// <remarks>
    /// <b>位置はコアが決める</b>: ラベルの配置は <c>MarqueePresenter</c>（描画層）が<br/>
    /// 実寸から求めた矩形をそのまま使い、本クラスは要素を <c>Arrange</c> するだけである。<br/>
    /// MewUI は <c>Margin</c> による絶対配置が反映されないため、配置フェーズで明示的に位置を確定させる。<br/>
    /// 子の座標はウィンドウ絶対座標のため、親の絶対座標（<paramref name="bounds"/> の左上）を加算する。<br/>
    /// <b>図形の描画もここで要求する</b>: 図形を描くキャンバスは自分の描画内容を保持し、<br/>
    /// 親の無効化や再配置では描き直されない（追加直後の初回描画とレイアウト変更の反映を確実にする）。
    /// </remarks>
    protected override void ArrangeContent(Rect bounds)
    {
        // 図形のキャンバスは親領域全体を埋める
        _shapes.Arrange(bounds);
        _shapes.InvalidateVisual();

        // 描画計画は本フェーズで 1 回だけ解決し、図形の描画（OnPaintSurface）と共有する
        // ラベルは描画計画が持つ配置指示（実寸を反映済み）をそのまま反映する
        SyncLabels(ResolveFrame(bounds.Size.ToEsSize()).Labels, bounds);
    }

    /// <summary>ラベル要素（背景 + テキスト）を配置指示へ同期し、親の絶対座標を基準に配置する。</summary>
    /// <param name="placements">配置するラベル（描画順に並ぶ）。</param>
    /// <param name="bounds">本コントロールへ割り当てられた矩形（ウィンドウ絶対座標・DIP）。</param>
    /// <remarks>
    /// 余分な要素は親から外し、不足分は生成して子の末尾へ追加する（配置のたびに呼ばれる）。<br/>
    /// 子の座標はウィンドウ絶対座標のため、親の絶対座標（<paramref name="bounds"/> の左上）を加算する。
    /// </remarks>
    private void SyncLabels(IReadOnlyList<MarqueeLabelPlacement> placements, Rect bounds)
    {
        while (_labels.Count > placements.Count)
        {
            var last = _labels[^1];
            _labels.RemoveAt(_labels.Count - 1);
            Remove(last);
        }

        while (_labels.Count < placements.Count)
        {
            var border = new Border().Child(new TextBlock());
            _labels.Add(border);
            Add(border);
        }

        for (var i = 0; i < placements.Count; i++)
        {
            var placement = placements[i];
            var border = _labels[i];

            Apply(border, placement);
            border.Arrange(new Rect(
                bounds.X + placement.DipRect.X, bounds.Y + placement.DipRect.Y,
                placement.DipRect.Width, placement.DipRect.Height));
        }
    }

    /// <summary>1 つのラベル要素の内容と体裁を配置指示に合わせる。</summary>
    /// <param name="border">更新する要素（背景）。</param>
    /// <param name="placement">配置指示（寸法は実寸を反映済み）。</param>
    /// <remarks>
    /// 本体は配置でサイズを確定するため、要素には内側余白だけを与える。<br/>
    /// <b>表示可否は本層が反映する</b>: 計画は表示可否（外観の <c>Show*</c>）でも指示を消さないため、<br/>
    /// 要素として置くかどうかを要素の <c>IsVisible</c> で表す（止めたラベルの文言・位置も指示としては残る）。
    /// </remarks>
    private static void Apply(Border border, in MarqueeLabelPlacement placement)
    {
        border.IsVisible = placement.IsVisible;
        border.Padding(placement.PaddingX, placement.PaddingY);
        border.Background = placement.BackgroundColor.ToMewUIColor();

        if (border.Child is TextBlock text)
        {
            text.Text = placement.Text;
            text.FontSize = placement.FontSize;
            text.Foreground = placement.TextColor.ToMewUIColor();
        }
    }

    /// <summary>DPI の変更時に表示スケーリング係数を追従させる。</summary>
    /// <param name="oldDpi">変更前の DPI。</param>
    /// <param name="newDpi">変更後の DPI。</param>
    /// <remarks>モニター間の移動で DPI が変わっても、枠線・ハンドル・マーカーの画面上の大きさを保つ。</remarks>
    protected override void OnDpiChanged(uint oldDpi, uint newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        ClientToPixelScaling = DpiScaler.ToScaling(newDpi);
    }

    /// <summary>左ボタン押下時にドラッグを開始する。</summary>
    /// <param name="e">マウスの情報。</param>
    /// <remarks>
    /// 押下位置を対象座標へ変換してコア（ファサード）へ通知する（ダブルクリックの確定/キャンセル・nudge の終了・<br/>
    /// ハンドルのスナップはコアの判断に従う）。<br/>
    /// ドラッグ中はポインターがコントロール外へ出ても移動イベントを受け取れるようマウスキャプチャを取得する。
    /// </remarks>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (!e.LeftButton)
        {
            return;
        }

        e.Handled = true;
        _marquee.Press(ToSource(e.GetPosition(this)), e.ClickCount);
    }

    /// <summary>ドラッグ中のマウス移動を処理する。</summary>
    /// <param name="e">マウスの情報。</param>
    /// <remarks>
    /// 押下位置を対象座標へ変換してコア（ファサード）へ通知する。<br/>
    /// ドラッグの適用・nudge 中のエコー移動の抑制・カーソル形状の決定はコアの判断に従う。
    /// </remarks>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _marquee.Move(ToSource(e.GetPosition(this)));
    }

    /// <summary>左ボタン解放時にドラッグを終了する。</summary>
    /// <param name="e">マウスの情報。</param>
    /// <remarks>
    /// 押下位置を対象座標へ変換してコア（ファサード）へ通知し、マウスキャプチャを解放する。<br/>
    /// 最小サイズによるキャンセル・確定・クリック検出・nudge の終了はコアの判断に従う。
    /// </remarks>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        e.Handled = true;
        _marquee.Release(ToSource(e.GetPosition(this)));
    }

    /// <summary>キーダウン処理（矢印キーの nudge・Escape のキャンセル・Enter の確定）。</summary>
    /// <param name="e">キーイベント。</param>
    /// <remarks>
    /// キーをコア型（<see cref="EsKey"/>）へ変換して通知し、返された効果（再描画・カーソル操作）を適用するだけである。<br/>
    /// 矢印キー nudge・Escape・Enter の判断（nudge の許可条件を含む）はコアが行う。<br/>
    /// <c>|| e.Handled</c> は、先に誰かが処理済みにしたフラグを false へ戻さないため。<br/>
    /// 本コントロールにフォーカスがある場合のみ呼ばれる（Avalonia 版のフォーカス非依存 nudge とは異なる）。
    /// </remarks>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        e.Handled = _marquee.KeyDown(e.Key.ToEsKey(), CurrentCursorSourcePosition()) || e.Handled;
    }

    /// <summary>現在のカーソル位置（対象座標）を取得する。</summary>
    /// <returns>カーソル位置（対象座標）。 ウィンドウを特定できない場合は null。</returns>
    /// <remarks>
    /// nudge の基点精度を上げるために記録する（マウス移動が未発生でも現在位置から微調整を開始でき、原点へ飛ばない）。<br/>
    /// 画面物理座標 → ウィンドウのクライアント座標 → 対象座標の順に変換する（境界の 1 行変換）。
    /// </remarks>
    private EsPoint? CurrentCursorSourcePosition()
    {
        if (FindVisualRoot() is not Window window)
        {
            return null;
        }

        // 位置を取得できない場合は基点を記録しない（失敗を原点で表さないため Try 系を使う）
        if (!_cursor.TryGetCursorScreenPoint(out var screenPoint))
        {
            return null;
        }

        var local = window.ScreenToClient(screenPoint.ToMewUIPoint());
        return _marquee.ToSource(local.ToEsPoint());
    }

    /// <summary>nudge を終了し、マウスポインターを再表示する。</summary>
    /// <remarks>フォーカス喪失時に呼び出し、nudge 中の非表示ポインターを復元する。</remarks>
    private void EndNudge() => _marquee.EndNudge();

    /// <summary>ローカル DIP 座標を対象座標へ変換する。</summary>
    /// <param name="localDip">ローカル DIP 座標。</param>
    /// <returns>対象座標。</returns>
    private EsPoint ToSource(Point localDip) => _marquee.ToSource(localDip.ToEsPoint());

    /// <summary>コア（ファサード）が要求する効果を本コントロールへ適用する実装。</summary>
    /// <remarks>
    /// 効果の適用順序（フォーカス → カーソル形状 → 再描画）はコアが単一所有するため、<br/>
    /// 本実装は呼ばれた順に自 UI の操作へ接続するだけでよい（判断を持たない）。<br/>
    /// <see cref="MarqueeEffectsBase"/> を継承し、本 UI が必要とする効果だけを override する。<br/>
    /// コアは <see cref= "IMarqueeEffects"/> のみに依存し、UI フレームワークを知らない。
    /// </remarks>
    private sealed class Effects(MarqueeControl owner) : MarqueeEffectsBase
    {
        /// <summary>フォーカスを取得する（矢印キー nudge の受け付け開始）。</summary>
        public override void RequestFocus() => owner.Focus();

        /// <summary>再描画を要求する。</summary>
        /// <remarks>
        /// 図形は子（<c>ShapeCanvas</c>）が描くため子も無効化する（要求の集約は <see cref="MarqueeControl.InvalidateRender"/> が単一所有する）。
        /// </remarks>
        public override void RequestRedraw() => owner.InvalidateRender();

        /// <summary>カーソル形状をコアが示す種別に応じて更新する。</summary>
        /// <param name="kind">カーソル形状の種別（コア提供）。</param>
        public override void SetCursorShape(MarqueeCursorKind kind) => owner.ApplyCursorShape(kind);

        /// <summary>ポインターキャプチャを取得または解放する。</summary>
        /// <param name="capture">true = 取得 / false = 解放。</param>
        /// <remarks>ドラッグ中にポインターがコントロール外へ出ても移動イベントを受け取れるようにする。</remarks>
        public override void SetPointerCapture(bool capture) => owner.CaptureMouse(capture);

        /// <summary>対象座標を画面物理座標へ変換する。</summary>
        /// <param name="sourcePoint">対象座標。</param>
        /// <returns>画面物理座標。 ウィンドウを特定できない場合は既定値。</returns>
        public override EsPointInt ToScreenPoint(EsPoint sourcePoint)
            => owner.ToScreen(sourcePoint);
    }

    /// <summary>対象座標を画面物理座標へ変換する。</summary>
    /// <param name="sourcePos">対象座標。</param>
    /// <returns>画面物理座標。 ウィンドウを特定できない場合は default。</returns>
    /// <remarks>カーソルの移動（nudge）に使用する。 変換できない場合は原点へカーソルが飛ぶため、呼び出し側は nudge 中のウィンドウ表示を前提とする。</remarks>
    private EsPointInt ToScreen(EsPoint sourcePos)
    {
        if (FindVisualRoot() is not Window window)
        {
            return default;
        }

        var localDip = _marquee.ToDip(sourcePos);
        var screen = window.ClientToScreen(localDip.ToMewUIPoint());
        return screen.ToEsPointInt();
    }

    /// <summary>マウスキャプチャを取得または解放する。</summary>
    /// <param name="capture">true = 取得 / false = 解放。</param>
    /// <remarks>ドラッグ中にポインタがコントロール外へ出ても移動イベントを受け取れるようにする。</remarks>
    private void CaptureMouse(bool capture)
    {
        if (FindVisualRoot() is not Window window)
        {
            return;
        }

        if (capture)
        {
            window.CaptureMouse(this);
        }
        else
        {
            window.ReleaseMouseCapture();
        }
    }

    /// <summary>コアからの通知を公開イベントへ転送する（判断はしない）。</summary>
    /// <param name="notice">通知の種別・座標・スナップショット。</param>
    /// <remarks>
    /// 転送のみを担う（確定/キャンセルの判断・状態の更新はコアが済ませている）。<br/>
    /// 本 UI は押下・解放の公開イベントを持たない（基底 <c>UIElement</c> の同名メンバーがサービスを提供済みのため）。
    /// </remarks>
    private void OnNotified(MarqueeNotice notice)
    {
        switch (notice.Kind)
        {
            // 選択（始点・変更・確定・キャンセル）
            case MarqueeNoticeKind.SelectionStarted: SelectionStarted?.Invoke(this, ToPointArgs(notice)); break;
            case MarqueeNoticeKind.SelectionChanged: SelectionChanged?.Invoke(this, ToRectArgs(notice)); break;
            case MarqueeNoticeKind.SelectionCompleted: SelectionCompleted?.Invoke(this, ToRectArgs(notice)); break;
            case MarqueeNoticeKind.SelectionCancelled: SelectionCancelled?.Invoke(this, EventArgs.Empty); break;

            // ルーペ（追従位置）・確定要求・キャンセル要求
            // コアは追従位置を汎用名（MarkerPositionMoved）で通知するため、ホスト向けの名前（ルーペ中心）へ写像する
            case MarqueeNoticeKind.MarkerPositionMoved: LoupePositionMoved?.Invoke(this, ToPointArgs(notice)); break;
            case MarqueeNoticeKind.ConfirmRequested: ConfirmRequested?.Invoke(this, ToRectArgs(notice)); break;
            case MarqueeNoticeKind.CancelRequested: CancelRequested?.Invoke(this, EventArgs.Empty); break;
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

    /// <summary>スナップショットを選択矩形のイベント引数へ変換する（コアの通知を経由しない通知用）。</summary>
    /// <param name="snapshot">通知時点の状態スナップショット。</param>
    /// <returns>選択矩形とスナップショットを含むイベント引数。</returns>
    private static SelectionRectEventArgs ToRectArgs(MarqueeSnapshot snapshot)
        => new(snapshot.SelectionSourceRect, snapshot);

    /// <summary>カーソル形状をコアが示す種別に応じて更新する。</summary>
    /// <param name="kind">カーソル形状の種別（コア提供）。</param>
    /// <remarks>種別と自 UI のカーソルの対応のみを持つ（どのハンドルが同じカーソルになるかはコアが判断済み）。</remarks>
    private void ApplyCursorShape(MarqueeCursorKind kind)
    {
        Cursor = kind switch
        {
            MarqueeCursorKind.DiagonalNWSE => CursorType.SizeNWSE,
            MarqueeCursorKind.DiagonalNESW => CursorType.SizeNESW,
            MarqueeCursorKind.Vertical => CursorType.SizeNS,
            MarqueeCursorKind.Horizontal => CursorType.SizeWE,
            MarqueeCursorKind.Move => CursorType.SizeAll,
            _ => null,
        };
    }

    /// <summary>選択矩形の外側に描く装飾・ハンドル・十字マーカーを描画し、付加情報の描画機会をホストへ通知する。</summary>
    /// <param name="canvas">描画先のキャンバス（物理ピクセル）。</param>
    /// <param name="info">描画先の情報（幅・高さ）。</param>
    /// <remarks>
    /// <b>描画の手順は提示層が単一所有する</b>: 層の順序と描画 API への写し方は描画計画（<see cref="MarqueeDrawPlan"/>）が持ち、<br/>
    /// 本メソッドは自 UI の描画面へ写すだけである（<see cref="MarqueeSkiaPainter.Paint"/>）。<br/>
    /// <b>ラベルは本パネルの子要素が描く</b>ため図形の層のみを描く。<br/>
    /// 選択枠線は装飾の外枠（同じ矩形の内側）と重なる（別々に描くと線幅が二重になる）。<br/>
    /// キャンバスを DPI スケーリングしてから DIP 座標で描画する（コアの計算と座標系を一致させる）。<br/>
    /// 背景は塗らない（透明のままにして下の画像を透かす）。
    /// </remarks>
    private void OnPaintSurface(SKCanvas canvas, SKImageInfo info)
    {
        // 倍率は描画面（物理ピクセル）と割り当て領域（DIP）の比から求める
        // （DPI の通知は追加直後のコントロールへ届かないため、保持値だけに頼ると 100% で描いてしまう）
        var scaling = ClientPixelScale.Resolve(info.Width, Bounds.Size.Width, ClientToPixelScaling);

        canvas.Save();
        canvas.Scale((float)scaling);
        try
        {
            // 前フレームの描画（選択矩形・暗転マスク）を残さないため、自分の描画面を消してから描く
            canvas.Clear(SKColors.Transparent);

            // 描画計画は配置フェーズ（ArrangeContent）が解決した値を共有する（未解決の場合のみ本メソッドが解決する）
            var plan = CurrentFrame(DpiScaler.ToDip(info.Size.ToEsSize(), scaling));

            MarqueeSkiaPainter.Paint(canvas, in plan);

            // 描画完了をホストへ通知する（操作・情報ラベルなどホスト固有の付加情報を描くための入口）。
            Rendered?.Invoke(this, new MarqueeRenderedEventArgs(canvas, plan));
        }
        finally
        {
            canvas.Restore();
        }
    }
}
