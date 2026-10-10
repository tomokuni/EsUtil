using EsUtil.Feature.Loupe.Presenter.Drawing;
using EsUtil.Feature.Loupe.Presenter.Logging;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;

namespace EsUtil.Feature.Loupe;

/// <summary>ルーペの提示（外観と描画計画）を UI 層へ提供するファサード（本層の既定実装）。</summary>
/// <remarks>
/// <b>適用パターン</b>: Facade（コアの設定・表示レイアウト・描画計画の解決を 1 つの窓口へ集約する）と<br/>
/// Presenter（受動的な UI 層へ「何をどの順に描くか」を解決した計画を渡す）。<br/>
/// <br/>
/// <b>責務</b>: コア（<see cref="LoupeCore"/>）を内部に保持し、UI 層が設定する値の転送と、<br/>
/// 外観（<see cref="Appearance"/>）の保持、描画 1 フレーム分の計画（<see cref="LoupeDrawPlan"/>）の解決を行う。<br/>
/// UI 層は本クラスだけを参照する（コアの計算クラスも描画 API も個別に呼び出さない）。<br/>
/// <br/>
/// <b>本クラスが単一所有するもの</b>:<br/>
/// - 外観（色・フォント・角丸・枠線の太さ・表示可否）<br/>
/// - 装飾（十字線・枠線）の矩形の導出（コアが返す幾何から）<br/>
/// - ラベルの文言と基準点、および実寸 → 配置の写像<br/>
/// - 描く層とその順序（<see cref="LoupeDrawLayer"/>）<br/>
/// <br/>
/// <b>コアが単一所有するもの</b>: 表示サイズ・位置のクランプ、切り出し領域と拡大画像の幾何、設定の既定値と補正、ログ。<br/>
/// <br/>
/// <b>本クラスは UI フレームワークに依存しない</b>（色は UI 非依存の <c>System.Drawing.Color</c>、描画は行わない）。<br/>
/// 派生クラスで <see cref="Appearance"/> を差し替えるか <see cref="ResolveDrawPlan"/> を上書きすることで、独自の見た目を提供できる<br/>
/// （このため本クラスは <c>sealed</c> にしない。 拡張点を持つことが役割である）。
/// </remarks>
public partial class LoupePresenter
{
    /// <summary>内部に保持するコアのファサード（設定・表示レイアウト・幾何の解決）。</summary>
    private readonly LoupeCore _core = new();

    /// <summary>外観（描画にだけ必要な設定の単一値）の保持先。</summary>
    /// <remarks>
    /// <c>field</c> キーワードは <c>ref</c> 引数へ渡せないため明示的なフィールドを持つ<br/>
    /// （<see cref="SetIfChanged"/> が変更検出を行う）。
    /// </remarks>
    private LoupeAppearance _appearance = LoupeAppearance.Default;

    /// <summary>描く層の組合せのうち、拡大画像の有無を表すビット。</summary>
    private const int ImageBit = 1;

    /// <summary>描く層の組合せのうち、十字線の有無を表すビット。</summary>
    private const int CrosshairBit = 2;

    /// <summary>描く層の組合せのうち、枠線の有無を表すビット。</summary>
    private const int BorderBit = 4;

    /// <summary>描く層の組合せのうち、テキストの有無を表すビット。</summary>
    private const int TextBit = 8;

    /// <summary>本層の生成の記録を済ませたかどうか。</summary>
    /// <remarks>出力先の設定は複数回起こりうるため、生成の記録は 1 度だけにする。</remarks>
    private bool _createdLogged;

    /// <summary>層の並び（ペーストボードを除く 4 つの有無 = 16 通り）。</summary>
    /// <remarks>
    /// 描画のたびに一覧を組み立てないよう、全通りを事前生成して共有する<br/>
    /// （<see cref="LoupeDrawPlan.Layers"/> は読取専用の参照として渡す）。 層を追加した場合は本表も更新する。
    /// </remarks>
    private static readonly IReadOnlyList<LoupeDrawLayer>[] s_layers = CreateLayers();

    /// <summary><see cref="LoupePresenter"/> の新しいインスタンスを初期化します。</summary>
    /// <param name="logger">ロガー。 null 可（null の場合は出力なし。 後から <see cref="Log"/> でも設定できる）。</param>
    /// <remarks>
    /// <b>ロガーを注入した場合は、その場で生成の記録を出力する</b>（コンストラクター呼び出しの記録と、
    /// 生成を 1 度だけ出力する規則を兼ねる）。 注入しない場合は出力先が未設定のため、
    /// <see cref="Log"/> が初めて設定された時点で生成の記録を出力する。
    /// </remarks>
    public LoupePresenter(ILogger? logger = null)
    {
        if (logger is not null)
        {
            // コンストラクタ由来の注入（本層とコアの生成の記録を今すぐ出力する）
            Log = logger;
        }
    }

    /// <summary>ルーペの設定（計算に必要な値。 表示サイズ・拡大率）を取得または設定する。</summary>
    /// <value>設定の単一値。 既定は <see cref="LoupeOptions.Default"/>。</value>
    /// <remarks>
    /// 保持・ログ・補正はコアが単一所有する（本プロパティはコアへ委譲するだけである）。<br/>
    /// 本層を通過したことは Trace で記録する（ホストの設定が本層を通ったかを確認できるようにする）。
    /// </remarks>
    public LoupeOptions Options
    {
        get => _core.Options;
        set
        {
            _core.Options = value;
            Log.LogForwarded(nameof(Options), value);
        }
    }

    /// <summary>表示 DIP を物理ピクセルへ変換するスケーリング係数を取得または設定する。</summary>
    /// <value>スケーリング係数（0.1〜8.0）。 範囲外・非有限は補正済みの値を設定する。 既定は 1.0。</value>
    /// <remarks>保持とログはコアが単一所有する（本層は委譲し、通過したことを Trace で記録する）。</remarks>
    public double ClientToPixelScaling
    {
        get => _core.ClientToPixelScaling;
        set
        {
            _core.ClientToPixelScaling = value;
            Log.LogForwarded(nameof(ClientToPixelScaling), value);
        }
    }

    /// <summary>拡大表示する中心座標（対象座標）を取得または設定する。</summary>
    /// <value>拡大中心（元画像ピクセル座標）。 未設定は null。</value>
    /// <remarks>保持とログ（Trace）はコアが単一所有する（本層は委譲し、通過したことを Trace で記録する）。</remarks>
    public EsPoint? CenterPoint
    {
        get => _core.CenterPoint;
        set
        {
            _core.CenterPoint = value;
            Log.LogForwarded(nameof(CenterPoint), value);
        }
    }

    /// <summary>拡大中心からの右下オフセット（DIP）を取得または設定する。</summary>
    /// <value>カーソル位置（拡大中心）からの右下方向へのオフセット量（DIP）。 既定は 0。</value>
    /// <remarks>保持とログはコアが単一所有する（本層は委譲し、通過したことを Trace で記録する）。</remarks>
    public double PositionOffset
    {
        get => _core.PositionOffset;
        set
        {
            _core.PositionOffset = value;
            Log.LogForwarded(nameof(PositionOffset), value);
        }
    }

    /// <summary>設定の変更ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。 null を設定した場合は出力なし（<c>?? NullLogger.Instance</c> で吸収する）。</value>
    /// <remarks>
    /// 保持はコアが単一所有する（本層は委譲するだけである）。<br/>
    /// <b>生成の記録</b>: コンストラクターでロガーが注入されていない場合、本プロパティの <b>初回の設定時</b>に
    /// 本層とコアの生成を 1 度だけ記録する（生成時は出力先が未設定のため、その場では記録できない）。
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
    /// <value>接頭辞（例: <c>[Loupe]</c>）。 null または空白を設定すると既定値へ戻す。</value>
    /// <remarks>
    /// 本層のログ定義（<see cref="LoupeLogMessage"/>）の接頭辞を設定し、コア（<see cref="LoupeCore.LogPrefix"/>）へ伝搬する。<br/>
    /// 集約した接頭辞で全層のログを揃えられる（未設定時は <see cref="LoupeLogMessage.DefaultPrefix"/>）。
    /// </remarks>
    public string? LogPrefix
    {
        get;
        set
        {
            field = value;
            LoupeLogMessage.Prefix = string.IsNullOrWhiteSpace(value) ? LoupeLogMessage.DefaultPrefix : value;
            _core.LogPrefix = value;
        }
    }

    /// <summary>外観（色・フォント・角丸・枠線の太さ・表示可否）を取得または設定する。</summary>
    /// <value>外観の単一値。 既定は <see cref="LoupeAppearance.Default"/>。</value>
    /// <remarks>
    /// 外観はコアの計算・判断に入らないため本クラスが保持する。<br/>
    /// 一部の項目だけを変える場合は <c>LoupeAppearance.Default with { ... }</c> の形で指定する。<br/>
    /// 変更の検出（変化した項目だけの出力）は <c>LoupeLogMessage.LogAppearanceChanged</c> が単一所有する<br/>
    /// （ホストは値を設定するだけで、同値判定・差分検出を持たない）。
    /// </remarks>
    public LoupeAppearance Appearance
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

    /// <summary>対象 ↔ 親領域 DIP の座標変換ポートを設定する。</summary>
    /// <param name="port">座標変換ポート（ToDip = 対象 → 親領域 DIP）。 null の場合は等価変換。</param>
    /// <remarks>
    /// 保持とログはコアが単一所有する（本層は委譲するだけである）。<br/>
    /// 低頻度のメソッド呼び出しのため、本層を通過したことを Trace で記録する。
    /// </remarks>
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

    /// <summary>親領域のサイズを通知して表示レイアウト（表示サイズ・表示位置）を解決する。</summary>
    /// <param name="parentSize">親領域のサイズ（DIP）。 未確定（幅または高さが 0 以下）の場合は位置を補正しない。</param>
    /// <returns>表示レイアウト（表示サイズと表示位置。 拡大中心が未設定の場合と、親領域が表示サイズを収められない場合は位置が null）。</returns>
    /// <remarks>位置の補正・オフセット・中心の座標変換の規則はコアが単一所有する（本メソッドは委譲するだけである）。</remarks>
    public LoupeLayout ResolveLayout(EsSize parentSize) => _core.ResolveLayout(parentSize);

    /// <summary>現在の状態から描画 1 フレーム分の計画を解決する。</summary>
    /// <param name="clientSize">ルーペの一辺の表示サイズ（DIP。 通常は <see cref="ResolveLayout"/> の結果）。</param>
    /// <param name="measureText">テキストの実寸を測る関数。 null 不可。</param>
    /// <returns>描画計画（幾何・外観・層の順序・ラベル配置を解決済み）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measureText"/> が null の場合。</exception>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. コアから幾何（描画面・拡大画像・切り出し領域）を解決する。<br/>
    /// 2. 十字線と枠線の矩形を、外観（枠線の太さ）と幾何から導出する（太さは物理ピクセルへ換算する）。<br/>
    /// 3. ラベルの文言と基準点を解決し、実寸から配置指示へ写す。<br/>
    /// 4. 描く値のある層を順序どおりに並べる（テキストは最後 = 最上位）。<br/>
    /// <br/>
    /// <b>表示可否（外観の <c>Show*</c>）で値を消さない</b>: 描く値は幾何だけで決まるため、<br/>
    /// 表示を止めても計画の値（矩形）と層はそのまま残り、<br/>
    /// 描くかどうかは<b>描画側（painter / ホスト）が外観のフラグを見て決める</b><br/>
    /// （ホストは止めた項目も計算値を使って自前で描ける。 ラベルの表示可否は配置指示が値として持つ）。<br/>
    /// <br/>
    /// 実寸の測定は描画 API に依存するため、UI 層が関数として渡す<br/>
    /// （配置の計算（補正）は <see cref="ResolveLabelPlacements"/> が単一所有する）。<br/>
    /// 描画先の表面は、本メソッドが返す計画の <see cref="LoupeDrawPlan.ClientPixelSize"/> と同じ大きさで用意する（呼び出し側の責務）。
    /// </remarks>
    public virtual LoupeDrawPlan ResolveDrawPlan(
        double clientSize, Func<string, double, EsSize> measureText)
    {
        ArgumentNullException.ThrowIfNull(measureText);

        var appearance = Appearance;
        var geometry = _core.ResolveGeometry(clientSize);
        var surface = geometry.ClientPixelRect;

        // 描く値は表示可否（Appearance の Show*）に依存せず解決する（ホストが計算値を自前の描画へ使えるようにするため）。
        // 描くかどうかの判断は描画側（painter / ホスト）が行う（値の有無は幾何だけで決まる）。
        // 十字線は可視域の中心を指す（＝描画面の中心。 拡大中心が小数でも位置が動かない）
        var crosshair = ComputeCrosshairPixelRects(surface, geometry.ZoomLevel, geometry.CaptureCenterOffset);
        var border = ComputeBorderPixelRects(
            surface, DpiScaler.ScaleToPixel(appearance.BorderThickness, geometry.ClientToPixelScaling));

        var labelText = ResolveCoordLabelText(in geometry, appearance);

        return new LoupeDrawPlan(
            appearance,
            surface,
            geometry.MagnifiedImage,
            crosshair,
            border,
            ResolveLayers(geometry.MagnifiedImage, crosshair, border, labelText),
            ResolveLabelPlacements(labelText, appearance, measureText),
            geometry.CaptureTopLeft,
            geometry.ZoomLevel,
            geometry.ClientPixelSize,
            geometry.ClientToPixelScaling);
    }

    /// <summary>本層の生成を 1 度だけ記録する（既に記録済みの場合は何もしない）。</summary>
    /// <remarks>
    /// 生成時点では出力先（<see cref="Log"/>）が未設定のため、ロガーが設定された時点で記録する<br/>
    /// （コアの生成の記録と合わせて、どの設定で生成されたファサードからのログかを追えるようにする）。
    /// </remarks>
    private void LogPresenterCreatedOnce()
    {
        if (_createdLogged)
        {
            return;
        }

        _createdLogged = true;
        _core.Log.LogPresenterCreated();
    }

    /// <summary>描く層だけを描画順に並べる。</summary>
    /// <param name="magnifiedImage">拡大画像の切り出しと描画の矩形。 null の場合は層を含めない。</param>
    /// <param name="crosshairRects">十字線の矩形。 空の場合は層を含めない。</param>
    /// <param name="borderRects">枠線の矩形。 空の場合は層を含めない。</param>
    /// <param name="coordLabelText">中心座標ラベルのテキスト配置。 null の場合は層を含めない。</param>
    /// <returns>描く層の一覧（先頭から順に処理する）。</returns>
    /// <remarks>
    /// ペーストボード（画像外の色）は常に含める（描画面を置き換えて前フレームの残像を残さないため。 透明色を指定した場合も描画面の消去が必要）。<br/>
    /// テキストは末尾へ置き、常に最上位にする（UI 要素として置く UI は本層を無視してよい）。<br/>
    /// 層の有無は<b>描く値の有無</b>から決まる（層にある ⇔ 描く値がある）。<br/>
    /// <b>表示可否（外観の <c>Show*</c>）は層へ反映しない</b>（描く値があれば層を含める。 描くかどうかは描画側が判断する）。<br/>
    /// 一覧はビットの組合せで事前生成したものを返す（描画のたびの確保を避ける）。
    /// </remarks>
    private static IReadOnlyList<LoupeDrawLayer> ResolveLayers(
        LoupeMagnifiedImage? magnifiedImage,
        IReadOnlyList<EsRect> crosshairRects,
        IReadOnlyList<EsRect> borderRects,
        LoupeTextPlacement? coordLabelText)
        => s_layers[
            (magnifiedImage is not null ? ImageBit : 0)
            | (crosshairRects.Count > 0 ? CrosshairBit : 0)
            | (borderRects.Count > 0 ? BorderBit : 0)
            | (coordLabelText is not null ? TextBit : 0)];

    /// <summary>層の全 16 通りを生成する。</summary>
    /// <returns>ビットの組合せを添字とする層の一覧。</returns>
    /// <remarks>ビットの意味は <see cref="ImageBit"/> / <see cref="CrosshairBit"/> / <see cref="BorderBit"/> / <see cref="TextBit"/> が単一所有する。</remarks>
    private static IReadOnlyList<LoupeDrawLayer>[] CreateLayers()
    {
        var table = new IReadOnlyList<LoupeDrawLayer>[1 << 4];

        for (var index = 0; index < table.Length; index++)
        {
            var layers = new List<LoupeDrawLayer>(5) { LoupeDrawLayer.Pasteboard };

            if ((index & ImageBit) != 0)
            {
                layers.Add(LoupeDrawLayer.Image);
            }

            if ((index & CrosshairBit) != 0)
            {
                layers.Add(LoupeDrawLayer.Crosshair);
            }

            if ((index & BorderBit) != 0)
            {
                layers.Add(LoupeDrawLayer.Border);
            }

            // テキストは常に最上位（ペーストボード・拡大画像・十字線・枠線より後に描く）
            if ((index & TextBit) != 0)
            {
                layers.Add(LoupeDrawLayer.Label);
            }

            table[index] = layers;
        }

        return table;
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
