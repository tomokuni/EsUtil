using EsUtil.Feature.Marquee.Presenter.Drawing;
using EsUtil.Feature.Marquee.Presenter.Logging;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native;
using Microsoft.Extensions.Logging;

namespace EsUtil.Feature.Marquee;

/// <summary>選択矩形の提示（外観と描画計画）を UI 層へ提供するファサード（本層の既定実装）。</summary>
/// <remarks>
/// <b>適用パターン</b>: Facade（コアの設定・入力・描画計画の解決を 1 つの窓口へ集約する）と<br/>
/// Presenter（受動的な UI 層へ「何をどの順に描くか」を解決した計画を渡す）。<br/>
/// <br/>
/// <b>責務</b>: コア（<see cref="MarqueeCore"/>）を内部に保持し、UI 層が設定する値と入力の転送、<br/>
/// 外観（<see cref="Appearance"/>）の保持、描画 1 フレーム分の計画（<see cref="MarqueeDrawPlan"/>）の解決を行う。<br/>
/// UI 層は本クラスだけを参照する（コアの計算クラスも描画 API も個別に呼び出さない）。<br/>
/// <br/>
/// <b>本クラスが単一所有するもの</b>:<br/>
/// - 外観（色・ラベルの体裁・枠線の太さ・表示可否）<br/>
/// - 装飾（暗転マスク・外枠・内側の塗り）の矩形の導出（コアが返す幾何から）<br/>
/// - ラベルの文言（サイズの書式・表示スケール）と配置の写像<br/>
/// - 描く層とその順序（<see cref="MarqueeDrawLayer"/>）<br/>
/// <br/>
/// <b>コアが単一所有するもの</b>: 選択の状態機械・入力の解釈・注目矩形の決定・ハンドル / マーカーの位置・設定の既定値と補正・ログ。<br/>
/// <br/>
/// <b>本クラスは UI フレームワークに依存しない</b>（色は UI 非依存の <c>System.Drawing.Color</c>、描画は行わない）。<br/>
/// 派生クラスで <see cref="Appearance"/> を差し替えるか <see cref="ResolveDrawPlan"/> を上書きすることで、独自の見た目を提供できる<br/>
/// （このため本クラスは <c>sealed</c> にしない。 拡張点を持つことが役割である）。
/// </remarks>
public partial class MarqueePresenter
{
    /// <summary>描画層の組合せのうち、装飾の有無を表すビット。</summary>
    private const int DecorationBit = 1;

    /// <summary>描画層の組合せのうち、ドラッグハンドルの有無を表すビット。</summary>
    private const int HandlesBit = 2;

    /// <summary>描画層の組合せのうち、始点マーカーの有無を表すビット。</summary>
    private const int MarkerBit = 4;

    /// <summary>描画層の組合せのうち、テキストの有無を表すビット。</summary>
    private const int TextBit = 8;

    /// <summary>層の並び（4 つの有無 = 16 通り）。</summary>
    /// <remarks>
    /// 描画のたびに一覧を組み立てないよう、全通りを事前生成して共有する<br/>
    /// （<see cref="MarqueeDrawPlan.Layers"/> は読取専用の参照として渡す）。 層を追加した場合は本表も更新する。
    /// </remarks>
    private static readonly IReadOnlyList<MarqueeDrawLayer>[] s_layers = CreateLayers();

    /// <summary>内部に保持するコアのファサード（状態機械・入力の解釈・幾何の解決）。</summary>
    private readonly MarqueeCore _core;

    /// <summary>外観（描画にだけ必要な設定の単一値）の保持先。</summary>
    /// <remarks>
    /// <c>field</c> キーワードは <c>ref</c> 引数へ渡せないため明示的なフィールドを持つ<br/>
    /// （<see cref="SetIfChanged"/> が変更検出を行う）。
    /// </remarks>
    private MarqueeAppearance _appearance = MarqueeAppearance.Default;

    /// <summary>サイズラベルの表示スケーリング係数の保持先（未設定は 0）。</summary>
    /// <remarks>0 以下を倍率と同値として扱う解決は <c>MarqueeCore.Limits.ResolveDisplayScale</c> が単一所有する。</remarks>
    private double _displayScaleScaling;

    /// <summary>本層の生成の記録（<c>MarqueeLogMessage.LogPresenterCreated</c>）を済ませたかどうか。</summary>
    /// <remarks>出力先の設定は複数回起こりうるため、生成の記録は 1 度だけにする。</remarks>
    private bool _createdLogged;

    /// <summary>選択矩形コントロールの提示層を初期化する。</summary>
    /// <param name="effects">効果（フォーカス・再描画・カーソル形状・キャプチャ・座標変換）の適用先。 null 不可。</param>
    /// <param name="cursor">カーソルサービス。 null 可（null の場合はカーソル操作を行わない）。</param>
    /// <param name="options">初期設定。 null 可（null の場合は <see cref="MarqueeOptions.Default"/>）。</param>
    /// <param name="logger">ロガー。 null 可（null の場合は出力なし。 後から <see cref="Log"/> でも設定できる）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="effects"/> が null の場合。</exception>
    /// <remarks>
    /// <b>ロガーを注入した場合は、その場で生成の記録を出力する</b>（コンストラクター呼び出しの記録と、<br/>
    /// 本層とコアの生成を 1 度だけ出力する規則を兼ねる）。 注入しない場合は出力先が未設定のため、<br/>
    /// <see cref="Log"/> が初めて設定された時点で生成の記録を出力する。
    /// </remarks>
    public MarqueePresenter(
        IMarqueeEffects effects,
        ICursorService? cursor = null,
        MarqueeOptions? options = null,
        ILogger? logger = null)
    {
        _core = new MarqueeCore(effects, cursor, options);

        if (logger is not null)
        {
            // コンストラクタ由来の注入（本層とコアの生成の記録を今すぐ出力する）
            Log = logger;
        }
    }

    /// <summary>外観（色・ラベルの体裁・枠線の太さ・表示可否）を取得または設定する。</summary>
    /// <value>外観の単一値。 既定は <see cref="MarqueeAppearance.Default"/>。</value>
    /// <remarks>
    /// 外観はコアの計算・判断に入らないため本クラスが保持する。<br/>
    /// 一部の項目だけを変える場合は <c>MarqueeAppearance.Default with { ... }</c> の形で指定する。<br/>
    /// 変更の検出（変化した項目だけの出力）は <c>MarqueeLogMessage.LogAppearanceChanged</c> が単一所有する<br/>
    /// （ホストは値を設定するだけで、同値判定・差分検出を持たない）。
    /// </remarks>
    public MarqueeAppearance Appearance
    {
        get => _appearance;
        set
        {
            if (SetIfChanged(ref _appearance, value, out var previous))
            {
                Log.LogAppearanceChanged(previous, value);
            }
        }
    }

    /// <summary>サイズラベルの自動生成に使うスケーリング係数（マルチスクリーン補正）を取得または設定する。</summary>
    /// <value>DisplayScale の百分率に使う係数。 未設定（0 以下）の場合は <see cref="ClientToSourceScaling"/> と同じ値。</value>
    /// <remarks>
    /// 0 以下を倍率と同値として扱う規則は <c>MarqueeCore.Limits.ResolveDisplayScale</c> が単一所有する。<br/>
    /// 変更は本クラスが検出して出力する（UI 層に同値判定を置かない）。
    /// </remarks>
    public double DisplayScaleScaling
    {
        get => MarqueeCore.Limits.ResolveDisplayScale(_displayScaleScaling, _core.ClientToSourceScaling);
        set
        {
            if (SetIfChanged(ref _displayScaleScaling, value, out var previous))
            {
                Log.LogSettingChanged(nameof(DisplayScaleScaling), previous, value);
            }
        }
    }

    /// <summary>サイズラベルの文言を描画時に評価するプロバイダを取得または設定する。</summary>
    /// <value>描画のたびに呼び出される関数。 null または空文字を返す場合は描画層が自動生成する。</value>
    /// <remarks>デリゲート（ホスト結線）のため値の等価比較の対象外とする。</remarks>
    public Func<string?>? SizeLabelOverrideProvider { get; set; }

    /// <summary>サイズラベルの表示スケーリングを矩形ごとに解決するプロバイダを取得または設定する。</summary>
    /// <value>描画のたびに呼び出される関数（引数は対象座標の矩形）。 null の場合は <see cref="DisplayScaleScaling"/> を使う。</value>
    /// <remarks>デリゲート（ホスト結線）のため値の等価比較の対象外とする。</remarks>
    public Func<EsRect, double>? SizeLabelDisplayScalingProvider { get; set; }

    /// <summary>設定の変更ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。 null を設定した場合は出力なし（コアが <c>LoggerFallback</c> で吸収する）。</value>
    /// <remarks>
    /// 保持はコアが単一所有する（本層は委譲するだけである）。<br/>
    /// <b>生成の記録</b>: コンストラクターでロガーが注入されていない場合、本プロパティの <b>初回の設定時</b>に<br/>
    /// 本層とコアの生成を 1 度だけ記録する（生成時は出力先が未設定のため、その場では記録できない。 2 回目以降の設定では繰り返さない）。
    /// </remarks>
    public ILogger Log
    {
        get => _core.Log;
        set
        {
            _core.Log = value;
            LogPresenterCreatedOnce();
            _core.Log.LogLoggerAttached();
        }
    }

    /// <summary>ログの接頭辞を取得または設定する（ロガーと一緒に設定する）。</summary>
    /// <value>接頭辞（例: <c>[Marquee]</c>）。 null または空白を設定すると既定値へ戻す。</value>
    /// <remarks>
    /// 本層のログ定義（<see cref="MarqueeLogMessage"/>）の接頭辞を設定し、コア（<see cref="MarqueeCore.LogPrefix"/>）へ伝搬する。<br/>
    /// 集約した接頭辞で全層のログを揃えられる（未設定時は <see cref="MarqueeLogMessage.DefaultPrefix"/>）。
    /// </remarks>
    public string? LogPrefix
    {
        get => field;
        set
        {
            field = value;
            MarqueeLogMessage.Prefix = string.IsNullOrWhiteSpace(value) ? MarqueeLogMessage.DefaultPrefix : value;
            _core.LogPrefix = value;
        }
    }

    /// <summary>上位（UI 層）へ伝える通知を購読する。 UI 層は自 UI の公開イベントへ転送する。</summary>
    /// <param name="notify">通知を受け取る処理。</param>
    /// <returns>購読を解除する <see cref="IDisposable"/>。</returns>
    /// <remarks>
    /// 通知の種別と公開イベントの対応は UI 層が持ち、判断（確定・キャンセル等）はコアが済ませている（コアへ委譲する）。<br/>
    /// <see cref="MarqueeNotice"/> は値型のため <c>event</c> を使わない（<see cref="MarqueeCore.Subscribe"/> と同じ理由）。
    /// </remarks>
    public IDisposable Subscribe(Action<MarqueeNotice> notify) => _core.Subscribe(notify);

    // ====================================================================
    // コアへの委譲（設定・状態・環境値）
    // ====================================================================

    /// <summary>選択矩形の設定（計算に必要な値）を取得または設定する。</summary>
    /// <value>反映済みの設定。 既定は <see cref="MarqueeOptions.Default"/>。</value>
    /// <remarks>
    /// 保持・ログ・補正はコアが単一所有する（本プロパティはコアへ委譲するだけである）。<br/>
    /// 本層を通過したことは Trace で記録する（ホストの設定が本層を通ったかを確認できるようにする）。
    /// </remarks>
    public MarqueeOptions Options
    {
        get => _core.Options;
        set
        {
            _core.Options = value;
            Log.LogForwarded(nameof(Options), value);
        }
    }

    /// <summary>ポート未指定時に適用する倍率を取得または設定する。</summary>
    /// <value>対象座標 / ローカル DIP の倍率。 既定は 1.0。 設定時は 0.1〜8.0 へ補正される。</value>
    /// <remarks>保持とログはコアが単一所有する（本層は委譲し、通過したことを Trace で記録する）。</remarks>
    public double ClientToSourceScaling
    {
        get => _core.ClientToSourceScaling;
        set
        {
            _core.ClientToSourceScaling = value;
            Log.LogForwarded(nameof(ClientToSourceScaling), value);
        }
    }

    /// <summary>ハイライトする矩形（対象座標）を取得または設定する。</summary>
    /// <value>ハイライト矩形。 null（既定）の場合はハイライトしない。</value>
    /// <remarks>
    /// 保持とログ（Trace）はコアが単一所有する（本層は委譲し、通過したことを Trace で記録する）。<br/>
    /// 最も頻度が高い設定（ホバーのたびに変わりうる）のため、本層の記録も Trace とする。
    /// </remarks>
    public EsRect? HighlightSourceRect
    {
        get => _core.HighlightSourceRect;
        set
        {
            _core.HighlightSourceRect = value;
            Log.LogForwarded(nameof(HighlightSourceRect), value);
        }
    }

    /// <summary>選択矩形があるかどうかを取得する。</summary>
    /// <value>選択中の場合は true。</value>
    public bool HasSelection => _core.HasSelection;

    /// <summary>現在の選択矩形（対象座標）を取得する。</summary>
    /// <value>選択矩形。 未選択時は 0x0 の空矩形。</value>
    public EsRect SelectionSourceRect => _core.SelectionSourceRect;

    /// <summary>現在の選択状態のスナップショットを生成する。</summary>
    /// <param name="mousePosition">スナップショットに含めるマウス位置（対象座標）。 null の場合は直近の位置。</param>
    /// <returns>状態・選択矩形・アクティブハンドル・マウス位置・操作フラグを含むスナップショット。</returns>
    public MarqueeSnapshot CreateSnapshot(EsPoint? mousePosition = null) => _core.CreateSnapshot(mousePosition);

    // ====================================================================
    // コアへの委譲（環境値・入力・外部操作）
    // ====================================================================

    /// <summary>対象 ↔ ローカル DIP の座標変換ポートを設定する。</summary>
    /// <param name="port">座標変換ポート。 null の場合は等価変換。</param>
    /// <remarks>保持とログはコアが単一所有する（本層は委譲し、低頻度のメソッド呼び出しとして通過を Trace で記録する）。</remarks>
    public void SetCoordTransform(ICoordTransform? port)
    {
        try
        {
            _core.SetCoordTransform(port);
            Log.LogForwarded(nameof(SetCoordTransform), port);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(SetCoordTransform));
            throw;
        }
    }

    /// <summary>ローカル DIP 座標を対象座標へ変換する。</summary>
    /// <param name="localDip">ローカル DIP 座標。</param>
    /// <returns>対象座標。</returns>
    public EsPoint ToSource(EsPoint localDip) => _core.ToSource(localDip);

    /// <summary>対象座標をローカル DIP 座標へ変換する。</summary>
    /// <param name="source">対象座標。</param>
    /// <returns>ローカル DIP 座標。</returns>
    public EsPoint ToDip(EsPoint source) => _core.ToDip(source);

    /// <summary>ポインター押下（左ボタン）を通知する。</summary>
    /// <param name="position">押下位置（対象座標）。</param>
    /// <param name="clickCount">クリック数（2 以上 = ダブルクリック）。</param>
    public void Press(EsPoint position, int clickCount) => _core.Press(position, clickCount);

    /// <summary>ポインター移動を通知する。</summary>
    /// <param name="position">マウス位置（対象座標）。</param>
    public void Move(EsPoint position) => _core.Move(position);

    /// <summary>ポインター解放（左ボタン）を通知する。</summary>
    /// <param name="position">解放位置（対象座標）。</param>
    public void Release(EsPoint position) => _core.Release(position);

    /// <summary>キー入力を通知する。</summary>
    /// <param name="key">押下されたキー。</param>
    /// <param name="cursorPosition">押下時点のカーソル位置（対象座標）。 取得できない場合は null。</param>
    /// <returns>入力が処理された場合は true。</returns>
    public bool KeyDown(EsKey key, EsPoint? cursorPosition) => _core.KeyDown(key, cursorPosition);

    /// <summary>nudge を終了する（カーソルを再表示する）。</summary>
    public void EndNudge() => _core.EndNudge();

    /// <summary>選択矩形を外部状態から復元する。</summary>
    /// <param name="rect">選択矩形（対象座標）。</param>
    public void SetSelection(EsRect rect) => _core.SetSelection(rect);

    /// <summary>選択状態と nudge 状態を初期化する。</summary>
    public void ResetSelection() => _core.ResetSelection();

    /// <summary>確定要求を処理する。</summary>
    public void RequestConfirm() => _core.RequestConfirm();

    /// <summary>キャンセル要求を処理する。</summary>
    public void RequestCancel() => _core.RequestCancel();

    // ====================================================================
    // 描画計画（1 フレーム分）
    // ====================================================================

    /// <summary>現在の状態から描画 1 フレーム分の計画を解決する。</summary>
    /// <param name="clientSize">描画領域のサイズ（DIP。 原点は常に 0）。</param>
    /// <param name="measureText">テキストの実寸を測る関数。 null 不可。</param>
    /// <returns>描画計画（幾何・外観・層の順序・ラベル配置を解決済み）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measureText"/> が null の場合。</exception>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. コアから幾何（注目矩形・ハンドル・マーカー）を解決する。<br/>
    /// 2. 装飾（暗転領域・外枠・内側の塗り・内枠）の矩形を、外観（太さ）と幾何から導出する。<br/>
    /// 3. ラベルの文言と基準点を解決し、実寸から配置指示へ写す。<br/>
    /// 4. 描く値のある層を順序どおりに並べる（テキストは最後 = 最上位）。<br/>
    /// <br/>
    /// <b>表示可否（外観の <c>Show*</c>）で値を消さない</b>: 描く値は幾何だけで決まるため、<br/>
    /// 表示を止めても計画の値（矩形・ハンドル・マーカー）と層はそのまま残り、<br/>
    /// 描くかどうかは<b>描画側（painter / ホスト）が外観のフラグを見て決める</b><br/>
    /// （ホストは止めた項目も計算値を使って自前で描ける。 ラベルの表示可否は配置指示が値として持つ）。<br/>
    /// <br/>
    /// 実寸の測定は描画 API に依存するため、UI 層が関数として渡す<br/>
    /// （配置の計算（クランプ）は <see cref="ResolveLabelPlacements"/> が単一所有する）。
    /// </remarks>
    public virtual MarqueeDrawPlan ResolveDrawPlan(EsSize clientSize, Func<string, double, EsSize> measureText)
    {
        ArgumentNullException.ThrowIfNull(measureText);

        var appearance = Appearance;
        var request = _core.CreateRequest(clientSize);
        var geometry = MarqueeCore.ResolveGeometry(in request);
        var surface = geometry.ClientRect;
        var focus = geometry.FocusDipRect;

        // 描く値は表示可否（Appearance の Show*）に依存せず解決する（ホストが計算値を自前の描画へ使えるようにするため）。
        // 描くかどうかの判断は描画側（painter / ホスト）が行う（値の有無は幾何だけで決まる）。
        var overlayRegions = ComputeOverlayDipRects(surface, focus);
        var outerBorders = focus is { } outerFocus
            ? ComputeBorderDipRects(outerFocus, appearance.OuterBorderThickness)
            : [];
        var innerFill = focus;
        var innerBorders = focus is { } innerFocus
            ? ComputeBorderDipRects(innerFocus, appearance.InnerBorderThickness)
            : [];

        var sizeLabel = ResolveSizeLabelText(
            in geometry, appearance, DisplayScaleScaling, SizeLabelOverrideProvider, SizeLabelDisplayScalingProvider);
        var coordLabel = ResolveCoordLabelText(in geometry, appearance);
        var handles = geometry.Handles;
        var marker = geometry.Marker;

        return new MarqueeDrawPlan(
            appearance,
            surface,
            focus,
            focus is { } anchorFocus ? ResolveLabelAnchor(anchorFocus, appearance) : null,
            overlayRegions,
            outerBorders,
            innerFill,
            innerBorders,
            handles,
            marker,
            ResolveLayers(overlayRegions, outerBorders, innerFill, innerBorders, handles, marker, sizeLabel, coordLabel),
            ResolveLabelPlacements(sizeLabel, coordLabel, appearance, measureText));
    }

    /// <summary>描く層だけを描画順に並べる。</summary>
    /// <param name="overlayRegions">暗転領域。</param>
    /// <param name="outerBorderDipRects">外枠の矩形。</param>
    /// <param name="innerFillRect">内側の塗り。</param>
    /// <param name="innerBorderDipRects">内側の枠線。</param>
    /// <param name="handles">ドラッグハンドル。 null の場合は層を含めない。</param>
    /// <param name="marker">始点マーカー。 null の場合は層を含めない。</param>
    /// <param name="sizeLabel">サイズラベル。 null の場合は数えない。</param>
    /// <param name="coordLabel">カーソル座標ラベル。 null の場合は数えない。</param>
    /// <returns>描く層の一覧（先頭から順に処理する）。</returns>
    /// <remarks>
    /// <b>層にある ⇔ 描く値がある</b>（ルーペの層と同じ規則。 描く値の無い層は含めない）。<br/>
    /// <b>表示可否（外観の <c>Show*</c>）は層へ反映しない</b>（描く値があれば層を含める。 描くかどうかは描画側が判断する）。<br/>
    /// テキストはどちらかの文言がある場合だけ末尾へ置き、常に最上位にする。
    /// </remarks>
    private static IReadOnlyList<MarqueeDrawLayer> ResolveLayers(
        IReadOnlyList<EsRect> overlayRegions,
        IReadOnlyList<EsRect> outerBorderDipRects,
        EsRect? innerFillRect,
        IReadOnlyList<EsRect> innerBorderDipRects,
        MarqueeHandles? handles,
        MarqueeMarker? marker,
        MarqueeTextPlacement? sizeLabel,
        MarqueeTextPlacement? coordLabel)
        => s_layers[
            (overlayRegions.Count > 0 || outerBorderDipRects.Count > 0 || innerFillRect is not null || innerBorderDipRects.Count > 0 ? DecorationBit : 0)
            | (handles is not null ? HandlesBit : 0)
            | (marker is not null ? MarkerBit : 0)
            | (sizeLabel is not null || coordLabel is not null ? TextBit : 0)];

    /// <summary>層の全 16 通りを生成する。</summary>
    /// <returns>ビットの組合せを添字とする層の一覧。</returns>
    private static IReadOnlyList<MarqueeDrawLayer>[] CreateLayers()
    {
        var table = new IReadOnlyList<MarqueeDrawLayer>[1 << 4];

        for (var index = 0; index < table.Length; index++)
        {
            var layers = new List<MarqueeDrawLayer>(4);

            if ((index & DecorationBit) != 0)
            {
                layers.Add(MarqueeDrawLayer.Decoration);
            }

            if ((index & HandlesBit) != 0)
            {
                layers.Add(MarqueeDrawLayer.Handles);
            }

            if ((index & MarkerBit) != 0)
            {
                layers.Add(MarqueeDrawLayer.Marker);
            }

            // テキストは常に最上位（装飾・ハンドル・マーカーより後に描く）
            if ((index & TextBit) != 0)
            {
                layers.Add(MarqueeDrawLayer.Label);
            }

            table[index] = layers;
        }

        return table;
    }

    /// <summary>本層の生成を 1 度だけ記録する（既に記録済みの場合は何もしない）。</summary>
    /// <remarks>
    /// 生成時にロガーが注入されていない場合、出力先（<see cref="Log"/>）が未設定のためその場では記録できない。<br/>
    /// ロガーが設定された時点で本層の生成を出力する（記録は 1 回だけとする）。<br/>
    /// コンストラクターから注入された場合はその時点で既に記録済みであるため、本メソッドは何もしない。
    /// </remarks>
    private void LogPresenterCreatedOnce()
    {
        if (_createdLogged)
        {
            return;
        }

        _createdLogged = true;
        Log.LogPresenterCreated();
    }

    /// <summary>新しい値を保持する（同値なら何もしない）。</summary>
    /// <typeparam name="T">値の型。</typeparam>
    /// <param name="field">保持先のフィールド。</param>
    /// <param name="value">設定する値。</param>
    /// <param name="previous">変更前の値（同値の場合は現在値）。</param>
    /// <returns>値を変更した場合は true（同値だった場合は false）。</returns>
    /// <remarks>
    /// 「同値なら何もしない」という規則を単一所有する<br/>
    /// （各プロパティが同じ数行を書くと、片方だけ同値判定が抜けて無駄なログ・再評価が溢れる）。<br/>
    /// 変更前の値も返すため、変更を検出した側（外観の変更ログ）が前後を比較できる。
    /// </remarks>
    private static bool SetIfChanged<T>(ref T field, T value, out T previous)
    {
        previous = field;
        if (EqualityComparer<T>.Default.Equals(previous, value))
        {
            return false;
        }

        field = value;
        return true;
    }
}
