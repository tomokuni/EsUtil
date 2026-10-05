using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Skia.Controls;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Platform.MewUI;
using EsUtil.SaveEvidence.Platform.MewUI.Imaging;
using EsUtil.SaveEvidence.Platform.Skia;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using static EsUtil.Feature.Loupe.LoupeCore.Limits;



namespace EsUtil.Feature.Loupe.MewUI;

/// <summary>拡大鏡（ルーペ）コントロール（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版 <c>LoupeControl</c> と同一の UI 非依存コア（<c>EsUtil.Feature.Loupe.Core</c>）を使い、<br/>
/// 描画層のみを <see cref="SkiaCanvasView"/> で実装する（同じ設定・同じ見た目になる）。<br/>
/// <br/>
/// <b>入力</b>: <see cref="Source"/>（拡大元の 100% 画像）と <see cref="CenterPoint"/>（元画像ピクセル座標）。<br/>
/// <b>設定</b>: <see cref="Options"/>（コアの <see cref="LoupeOptions"/> が単一所有する表示サイズ・拡大率）。<br/>
/// <b>環境</b>: <see cref="ClientToPixelScaling"/>（DPI 追従）。<br/>
/// <b>描画</b>: 中間ビットマップを生成せず、子の図形キャンバス（<see cref="ShapeCanvas"/>）が画面のキャンバスへ直接描画する（Avalonia 版との差）。<br/>
/// 数値の安全域への補正は <see cref="LoupeOptions"/>（コア）が、描画サイズの決定は<br/>
/// <see cref="LoupePresenter.ResolveDrawPlan"/>（描画層）が、寸法値（枠線の太さ）と<br/>
/// ラベル表示の可否は <see cref="LoupeAppearance"/>（描画層）が単一所有する。<br/>
/// <br/>
/// <b>基底クラス</b>: 図形キャンバスとラベル要素を子として重ねるため <see cref="Panel"/> を採用する。<br/>
/// 図形キャンバスを先に、ラベル要素を後に子へ加えるため、<b>ラベルは常に最上位</b>の契約が構造で成立する。<br/>
/// <br/>
/// <b>ホスト側の操作</b>: <see cref="Source"/> を設定して <see cref="CenterPoint"/> を現在位置へ更新すると、<br/>
/// ルーペがその位置を拡大表示する（描画はプロパティ変更で自動的に無効化される）。<br/>
/// <b>座標ラベル</b>: 表示可否は <see cref="LoupeAppearance.ShouldShowCoordLabel"/>（描画層）が判断し、<br/>
/// 文言・基準点・配置は描画層（<see cref="LoupePresenter"/>）が実寸（<see cref="MewUITextMeasure"/>）から<br/>
/// 単一所有する（本コントロールは要素を置くだけである）。<br/>
/// <br/>
/// <b>見た目のカスタマイズ</b>: 色・太さ・表示可否は <see cref="Appearance"/> で、<br/>
/// 矩形の並びで表せる装飾とラベルの文言・位置は <see cref="DrawPlanResolver"/> で差し替えられる。<br/>
/// 矩形で表せない描画（円・斜線等）は <see cref="DrawPlan"/> を呼んで自前で描く（装飾は外観の <c>Show*</c> で止める）<br/>
/// （描画の直後に呼ばれる <see cref="Rendered"/> のキャンバスへ描き、必要なら <see cref="DrawPlan"/> で既定の描画を行う）。
/// </remarks>
public sealed class LoupeControl : Panel
{
    /// <summary><see cref="Source"/> プロパティ（拡大元の 100% 画像）。</summary>
    /// <remarks>変更ログはコアのログ定義が単一所有する（本コントロールは変更通知で呼ぶだけ）。</remarks>
    public static readonly MewProperty<EsBitmap?> SourceProperty =
        MewProperty<EsBitmap?>.Register<LoupeControl>(
            nameof(Source), null, MewPropertyOptions.AffectsRender,
            changed: static (o, oldValue, newValue) => o.Log.LogSourceChanged(oldValue, newValue));

    /// <summary><see cref="CenterPoint"/> プロパティ（拡大中心の元画像ピクセル座標）。</summary>
    /// <remarks>変更ログはコアのログ定義が単一所有する（本コントロールは変更通知で呼ぶだけ）。</remarks>
    public static readonly MewProperty<EsPoint?> CenterPointProperty =
        MewProperty<EsPoint?>.Register<LoupeControl>(
            nameof(CenterPoint), null, MewPropertyOptions.AffectsRender,
            changed: static (o, oldValue, newValue) =>
            {
                // 拡大中心はコア（ファサード）が単一所有する（表示位置・描画入力の計算元）
                o._presenter.CenterPoint = newValue;
                // 中心座標ラベルの文言は中心に依存するため、ラベル要素を配置し直す
                o.InvalidatePlan();
                o.InvalidateArrange();
            });

    /// <summary><see cref="Options"/> プロパティ（表示サイズ・拡大率。 コアの <see cref="LoupeOptions"/> が単一所有）。</summary>
    /// <remarks>
    /// 個別に公開していた設定（表示サイズ・拡大率）を 1 つの不変オブジェクトへ統合し、公開プロパティを 1 つに縮小する。<br/>
    /// <see cref="MeasureContent"/> が要求サイズを返すため、変更はレイアウトにも影響する（<c>AffectsLayout</c>）。<br/>
    /// 変更ログはコアのログ定義が単一所有する（変化した項目の判定（差分検出）もコアが行う）。<br/>
    /// 設定値の既定値と数値項目の安全域への補正はコアの <see cref="LoupeOptions"/> が単一所有するため、<br/>
    /// 本コントロールは既定値を持たず、補正の登録も行わない（どの設定経路でも範囲内の値だけが保持される）。
    /// </remarks>
    public static readonly MewProperty<LoupeOptions> OptionsProperty =
        MewProperty<LoupeOptions>.Register<LoupeControl>(
            nameof(Options), LoupeOptions.Default,
            MewPropertyOptions.AffectsRender | MewPropertyOptions.AffectsLayout,
            changed: static (o, oldValue, newValue) =>
            {
                // 表示サイズ（要求サイズ）と拡大率・外観はコア（ファサード）が単一所有する（表示レイアウトと描画入力の計算元）
                o._presenter.Options = newValue;
                // ラベルの大きさ・外観・表示可否が変わるため、ラベル要素を配置し直す（表示サイズが同じでも必要）
                o.InvalidatePlan();
                o.InvalidateArrange();
            });

    /// <summary><see cref="ClientToPixelScaling"/> プロパティ（表示 DIP → 物理ピクセルのスケーリング係数）。</summary>
    /// <remarks>
    /// 環境値（DPI）のため設定（<see cref="LoupeOptions"/>）には含めず、補正と変更ログは本プロパティへ登録する<br/>
    /// （setter を経由しない設定でも安全域が保たれ、変更が記録される）。
    /// </remarks>
    public static readonly MewProperty<double> ClientToPixelScalingProperty =
        MewProperty<double>.Register<LoupeControl>(
            nameof(ClientToPixelScaling), ClientToPixelScalingDefault, MewPropertyOptions.AffectsRender,
            changed: static (o, oldValue, newValue) =>
            {
                // 描画入力（物理ピクセル）の計算元はコア（ファサード）が単一所有する
                o._presenter.ClientToPixelScaling = newValue;
                // ラベルの配置の基準（表示サイズ）が物理ピクセル換算で変わるため、配置し直す
                o.InvalidateArrange();
            },
            coerce: static (_, value) => CoerceClientToPixelScaling(value));

    /// <summary><see cref="Appearance"/> プロパティ（外観。 描画にだけ必要な設定）。</summary>
    /// <remarks>
    /// 外観はコアの計算に入らない（描画にだけ必要）ため、コアの設定（<see cref="LoupeOptions"/>）には含めず、<br/>
    /// 描画の設定（<see cref="LoupeAppearance"/>）として本コントロールが公開する（描画層が単一所有する）。
    /// </remarks>
    public static readonly MewProperty<LoupeAppearance> AppearanceProperty =
        MewProperty<LoupeAppearance>.Register<LoupeControl>(
            nameof(Appearance), LoupeAppearance.Default,
            MewPropertyOptions.AffectsRender | MewPropertyOptions.AffectsLayout,
            changed: static (o, oldValue, newValue) =>
            {
                o._presenter.Appearance = newValue;
                // 中心座標ラベルの色・体裁も外観が与えるため、ラベル要素を配置し直す
                o.InvalidatePlan();
                o.InvalidateArrange();
            });

    /// <summary>提示層のファサード（設定・表示レイアウト・描画計画の窓口）。</summary>
    /// <remarks>
    /// 表示サイズ・伸長入力・座標ラベル・表示位置の計算はすべて <see cref="LoupePresenter"/> が単一所有する<br/>
    /// （本コントロールは公開プロパティで受け取った値を転送し、返された描画計画を描画・配置へ使うだけ）。
    /// </remarks>
    private readonly LoupePresenter _presenter;

    // ====================================================================
    // 見た目のカスタマイズ（描画計画の差し替えと既定描画の可否）
    // ====================================================================

    /// <summary>描画計画を差し替える関数を取得または設定する。</summary>
    /// <value>計画（幾何・外観・層の順序・ラベル配置）を受け取り、差し替えた計画を返す関数。 null の場合は既定の計画をそのまま使う。</value>
    /// <remarks>
    /// <b>矩形の並びで表せる装飾と、ラベルの文言・位置を差し替える入口</b>である（色・太さ・表示可否は <see cref="Appearance"/>）。<br/>
    /// 受け取る計画は提示層が解決済みであるため、本関数は値の置き換えだけで完結する<br/>
    /// （<c>plan with { CrosshairPixelRects = …, Labels = … }</c>）。<br/>
    /// <b>配置と描画の両方に適用される</b>ため、<see cref="LoupeDrawPlan.Labels"/> を差し替えるとラベル要素も追随する。<br/>
    /// <b>純粋関数（同じ入力に同じ出力）にすること</b>（同じ入力では 1 フレーム 1 回だけ解決されるため、<br/>
    /// 呼び出しの時点に依存する関数を渡すと差し替えが反映されない）。 設定すると保持中の計画を破棄して配置と描画をやり直す。
    /// </remarks>
    public Func<LoupeDrawPlan, LoupeDrawPlan>? DrawPlanResolver
    {
        get;
        set
        {
            field = value;
            InvalidatePlan();
            InvalidateArrange();
            InvalidateVisual();
        }
    }

    /// <summary>描画計画を指定の描画面へ描く（計画どおりに描く）。</summary>
    /// <param name="canvas">描画先のキャンバス（物理ピクセル・拡大画像と同じ座標系）。 null 不可。</param>
    /// <param name="source">拡大元の 100% 画像。 null の場合は拡大画像を描かない。</param>
    /// <param name="plan">描く描画計画。 null 不可。</param>
    /// <exception cref="ArgumentNullException"><paramref name="canvas"/> が null の場合。</exception>
    /// <remarks>
    /// <b>既定の描画をホストへ提供する入口</b>である（ホストが任意のタイミングと順序で既定の描画を行える）。<br/>
    /// 描画実装（<c>LoupeSkiaPainter</c>）は本プロジェクトの内部であるため、外部へは本メソッドが窓口になる。<br/>
    /// 計画を必要とする場合は <see cref="Rendered"/> の <c>Content</c> を使う。
    /// </remarks>
    public static void DrawPlan(SKCanvas canvas, EsBitmap? source, in LoupeDrawPlan plan)
        => LoupeSkiaPainter.Paint(canvas, source, in plan);

    /// <summary>図形（背景・市松模様・拡大画像・十字線・枠線）を描画する内部キャンバス。</summary>
    /// <remarks>
    /// <b>描画順を構造で保証する</b>: 本パネルが図形のキャンバスを先に、ラベル要素を後に子として持つため、<br/>
    /// ラベルは常に図形より後に描かれる（コアの「ラベルは最上位」契約が構造で成立する）。<br/>
    /// 入力は本パネルが受ける（本キャンバスはヒットテスト対象外）。
    /// </remarks>
    private readonly ShapeCanvas _shapes;

    /// <summary>配置中のラベル要素（背景 + テキスト）。 子の末尾側へ置くため常に最上位になる。</summary>
    private readonly List<Border> _labels = [];

    /// <summary>拡大元の 100% 画像を取得または設定する。</summary>
    /// <value>拡大元の画像。 null の場合は市松模様のみを表示する。</value>
    public EsBitmap? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>拡大表示する中心座標（元画像ピクセル座標）を取得または設定する。</summary>
    /// <value>拡大中心。 null の場合は市松模様のみを表示する。</value>
    public EsPoint? CenterPoint
    {
        get => GetValue(CenterPointProperty);
        set => SetValue(CenterPointProperty, value);
    }

    /// <summary>ルーペの設定（表示サイズ・拡大率）を取得または設定する。</summary>
    /// <value>設定の単一値。 既定はコアの既定値（<see cref="LoupeOptions.Default"/>）。</value>
    /// <remarks>
    /// 一部の項目だけを変える場合は <c>LoupeOptions.Default with { ZoomLevel = 8 }</c> の形で指定する（指定しない項目は既定値を保つ）。<br/>
    /// 設定の値・既定値・数値項目の安全域への補正はコアの <see cref="LoupeOptions"/> が単一所有する。<br/>
    /// 変更すると再描画される（本プロパティは <c>AffectsRender</c> / <c>AffectsLayout</c> で登録されている）。
    /// </remarks>
    public LoupeOptions Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    /// <summary>外観（色・フォント・角丸・枠線の太さ・表示可否）を取得または設定する。</summary>
    /// <value>外観の単一値。 既定は <see cref="LoupeAppearance.Default"/>。</value>
    /// <remarks>
    /// 外観はコアの計算に入らない（描画にだけ必要）ため、コアの設定（<see cref="LoupeOptions"/>）ではなく本プロパティが単一所有する。<br/>
    /// 変更すると再描画され、ラベル要素の再配置が行われる。
    /// </remarks>
    public LoupeAppearance Appearance
    {
        get => GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    /// <summary>表示 DIP を物理ピクセルへ変換するスケーリング係数を取得または設定する。</summary>
    /// <value>スケーリング係数。 範囲外・非有限は 0.1〜8.0 または既定値へ補正される。</value>
    public double ClientToPixelScaling
    {
        get => GetValue(ClientToPixelScalingProperty);
        set => SetValue(ClientToPixelScalingProperty, value);
    }

    /// <summary>ルーペ中心からの右下オフセット（DIP）を取得または設定する。</summary>
    /// <value>カーソル位置（拡大中心）からの右下方向へのオフセット量（DIP）。 既定は 0。</value>
    /// <remarks>
    /// 保持と位置の補正はコア（ファサード）が単一所有する。<br/>
    /// 本コントロールは位置を持たない（ホストが <see cref="ResolveLayout"/> の結果で配置する）ため、設定時は再配置を要求する。
    /// </remarks>
    public double PositionOffset
    {
        get => _presenter.PositionOffset;
        set
        {
            _presenter.PositionOffset = value;
            InvalidateArrange();
        }
    }

    /// <summary>対象 ↔ 親領域 DIP の座標変換ポートを設定する。</summary>
    /// <param name="port">座標変換ポート（ToDip = 対象 → 親領域 DIP）。 null の場合は等価変換。</param>
    /// <remarks>
    /// 保持と、未設定（null）を等価変換として扱う規則はコア（ファサード）が単一所有する。<br/>
    /// 変換関数は呼び出し毎に評価されるため、ズーム・スクロール等の動的変化に追従する（設定時は再配置を要求する）。
    /// </remarks>
    public void SetCoordTransform(ICoordTransform? port)
    {
        try
        {
            _presenter.SetCoordTransform(port);
            InvalidateArrange();
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(SetCoordTransform));
            throw;
        }
    }

    /// <summary>親領域のサイズから表示レイアウト（表示サイズ・表示位置）を解決する。</summary>
    /// <param name="parentSize">親領域のサイズ（DIP）。 未確定（幅または高さが 0 以下）の場合は位置を補正しない。</param>
    /// <returns>表示レイアウト（表示サイズと表示位置。 拡大中心未設定時と、親領域が表示サイズを収められない場合は位置が null）。</returns>
    /// <remarks>
    /// 本コントロールは位置を持たないため、ホストが本メソッドの結果で配置する（オフセット・位置の補正の規則はコアが単一所有する）。<br/>
    /// 描画サイズも同じ値から決まる（描画ビットマップと配置サイズを一致させる）。
    /// </remarks>
    public LoupeLayout ResolveLayout(EsSize parentSize) => _presenter.ResolveLayout(parentSize);

    /// <summary>ルーペコントロールを初期化する。</summary>
    /// <remarks>
    /// 図形キャンバス（<see cref="ShapeCanvas"/>）を子として加える（ラベル要素より先に加えるため常に最下になる）。<br/>
    /// キャンバスは連続再描画を無効化し、親からの要求（入力・設定の変更）があるときだけ描き直す。
    /// </remarks>
    public LoupeControl()
    {
        // 提示層（描画計画の窓口）を先に生成する（以降のプロパティ設定で参照されるため）
        // ロガーは Log プロパティの setter が提示層へ伝搬する（生成時点では未設定のため引き継がない）
        _presenter = new LoupePresenter();

        // 図形は専用の子キャンバスが描く（ラベル要素より先に子として加えるため、常にラベルの下になる）
        _shapes = new ShapeCanvas(this) { IsHitTestVisible = false };
        Add(_shapes);
    }

    /// <summary>図形（背景・市松模様・拡大画像・十字線・枠線）だけを描画する内部 Skia キャンバス。</summary>
    /// <remarks>
    /// 親（<see cref="LoupeControl"/>）の描画入力を組み立てて描画層へ渡すだけであり、判断を持たない。<br/>
    /// 中心座標ラベルの文字は本キャンバスが描かない（親パネルのラベル要素が描く）。
    /// </remarks>
    private sealed class ShapeCanvas : SkiaCanvasView
    {
        /// <summary>描画入力の取得元（親コントロール）。</summary>
        private readonly LoupeControl _owner;

        /// <summary>描画先の親を指定して初期化する。</summary>
        /// <param name="owner">描画入力の取得元（親コントロール）。</param>
        public ShapeCanvas(LoupeControl owner)
        {
            _owner = owner;
            // 静止画として扱う（入力・設定の変更時のみ再描画する）
            ContinuousAnimation = false;
            PaintSurface += _owner.OnPaintSurface;
        }
    }

    /// <summary>ルーペの描画完了を通知するイベント（描画先キャンバスと描画計画を渡す）。</summary>
    /// <remarks>
    /// 拡大画像の描画結果（<see cref="LoupeRenderedEventArgs.Canvas"/>）と描画計画を通知する。<br/>
    /// イベントは本コントロールの描画直後（同一フレーム内）に発火するため、ホスト側は<br/>
    /// イベントハンドラ内でキャンバスへオーバーレイ（選択矩形の破線等）を追加描画できる。<br/>
    /// 描画位置の計算は <see cref="LoupeDrawPlan.CaptureTopLeft"/> と <see cref="LoupeDrawPlan.ZoomLevel"/> から求める。
    /// </remarks>
    public event EventHandler<LoupeRenderedEventArgs>? Rendered;

    /// <summary>設定変更ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。 null を設定した場合は <see cref="NullLogger"/> へフォールバックする（出力なし）。</value>
    /// <remarks>
    /// C#14 の <c>field</c> キーワードでバッキングフィールドを暗黙定義する。<br/>
    /// getter / setter とも null を <see cref="NullLogger"/> へ安全に変換する。<br/>
    /// 設定・環境値・拡大中心の変更ログはコアが出力するため、同じロガーをコアへも渡す。
    /// </remarks>
    public ILogger Log
    {
        get => field.OrNullLogger();
        set
        {
            field = value.OrNullLogger();
            _presenter.Log = value;
        }
    }

    /// <summary>ログの接頭辞を取得または設定する（ロガーと一緒に設定する）。</summary>
    /// <value>接頭辞（例: <c>[Loupe]</c>）。 null または空白を設定すると既定値へ戻す。</value>
    /// <remarks>
    /// 本コントロールのログ定義（<see cref="LoupeLogMessage"/>）の接頭辞を設定し、<br/>
    /// 提示層・コアへ伝搬する（集約した接頭辞で全層のログを揃える）。
    /// </remarks>
    public string? LogPrefix
    {
        get => field;
        set
        {
            field = value;
            LoupeLogMessage.Prefix = string.IsNullOrWhiteSpace(value) ? LoupeLogMessage.DefaultPrefix : value;
            _presenter.LogPrefix = value;
        }
    }

    /// <summary>ルーペの要求サイズ（DIP）と子要素の計測を行う。</summary>
    /// <param name="availableSize">親から提示された利用可能サイズ。</param>
    /// <returns>一辺が <see cref="LoupeOptions.RequestedSize"/> の正方形。</returns>
    /// <remarks>
    /// ルーペは一辺が <see cref="LoupeOptions.RequestedSize"/> の正方形であるため、自身の要求サイズを返す。<br/>
    /// 表示位置はホストが <see cref="ResolveLayout"/>（コアの計算）の結果で配置する（本コントロールは位置を持たない）。<br/>
    /// 子（図形キャンバス・ラベル要素）へは利用可能サイズを伝搬する。
    /// </remarks>
    protected override Size MeasureContent(Size availableSize)
    {
        var size = new Size(Options.RequestedSize, Options.RequestedSize);

        _shapes.Measure(size);

        foreach (var label in _labels)
        {
            label.Measure(size);
        }

        return size;
    }

    /// <summary>子（図形キャンバス・ラベル要素）を配置する。</summary>
    /// <param name="bounds">本コントロールへ割り当てられた矩形（ウィンドウ絶対座標・DIP）。</param>
    /// <remarks>
    /// <b>位置はコアが決める</b>: ラベルの配置は <c>LoupePresenter</c>（描画層）が<br/>
    /// 実寸から求めた矩形をそのまま使い、本クラスは要素を <c>Arrange</c> するだけである。<br/>
    /// 子の座標はウィンドウ絶対座標のため、親の絶対座標（<paramref name="bounds"/> の左上）を加算する。
    /// </remarks>
    protected override void ArrangeContent(Rect bounds)
    {
        // 図形のキャンバスは親領域全体を埋める
        _shapes.Arrange(bounds);

        // テキストは描画層が文言と基準点を渡し、要素として置くか画像として描くかは本層が決める
        // （表示サイズは要素の大きさ（ホストが描画層のレイアウト結果で配置したサイズ）である）
        // 実寸は配置（本ルート）と描画（OnPaintSurface）の両方で同じ値を使う必要があるため、同じ測定関数を渡す
        var plan = ResolvePlan(bounds.Size.Width);
        SyncLabels(plan.Labels, bounds);
    }

    // ====================================================================
    // 1 フレーム分の描画計画（1 フレーム = 1 回だけ解決する）
    // ====================================================================

    /// <summary>直近に解決した描画計画（null = 未解決）。</summary>
    private LoupeDrawPlan? _plan;

    /// <summary>直近に解決した描画計画の基準（表示サイズ（DIP））。</summary>
    private double _planSize;

    /// <summary>直近に解決した描画計画の基準（スケーリング係数）。</summary>
    private double _planScaling;

    /// <summary>描画計画を解決し、<see cref="DrawPlanResolver"/> を適用する（同じ入力では再解決しない）。</summary>
    /// <param name="displaySize">ルーペの一辺の表示サイズ（DIP）。</param>
    /// <returns>差し替え後の描画計画。</returns>
    /// <remarks>
    /// <b>配置と描画の両経路が本メソッドを通る</b>（配置は <see cref="ArrangeContent"/>、描画は <see cref="OnPaintSurface"/>）。<br/>
    /// どちらの経路も同じ表示サイズを渡すため（描画は <see cref="ApplyClientPixelScaling"/> が描画面から求めた倍率で<br/>
    /// 割り当て領域（DIP）へ戻した値）、<b>同じ入力ではキャッシュを返して 1 フレーム 1 回の解決に留める</b><br/>
    /// （解決のたびに十字線・枠線の矩形とラベルの実寸測定をやり直さない）。<br/>
    /// キャッシュのキーは「表示サイズ」と「スケーリング係数」である（後者は描画の直前に書き戻されるため、<br/>
    /// 書き戻し後の解決を取りこぼさないようキーに含める）。<br/>
    /// それ以外の入力（拡大中心・設定・外観・<see cref="DrawPlanResolver"/>）の変更は <see cref="InvalidatePlan"/> で破棄する。
    /// </remarks>
    private LoupeDrawPlan ResolvePlan(double displaySize)
    {
        var scaling = ClientToPixelScaling;
        if (_plan is not { } cached || _planSize != displaySize || _planScaling != scaling)
        {
            cached = _presenter.ResolveDrawPlan(displaySize, MewUITextMeasure.Measure);
            cached = DrawPlanResolver?.Invoke(cached) ?? cached;
            _plan = cached;
            _planSize = displaySize;
            _planScaling = scaling;
        }

        return cached;
    }

    /// <summary>保持している描画計画を破棄する（次の解決で最新の状態を反映する）。</summary>
    private void InvalidatePlan() => _plan = null;

    /// <summary>ラベル要素（背景 + テキスト）を配置指示へ同期し、親の絶対座標を基準に配置する。</summary>
    /// <param name="placements">配置するラベル（描画順に並ぶ）。</param>
    /// <param name="bounds">本コントロールへ割り当てられた矩形（ウィンドウ絶対座標・DIP）。</param>
    /// <remarks>
    /// 余分な要素は親から外し、不足分は生成して子の末尾へ追加する（配置のたびに呼ばれる）。<br/>
    /// 子の座標はウィンドウ絶対座標のため、親の絶対座標（<paramref name="bounds"/> の左上）を加算する。
    /// </remarks>
    private void SyncLabels(IReadOnlyList<LoupeLabelPlacement> placements, Rect bounds)
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
    /// 本体は配置でサイズを確定するため、要素には内側余白と角丸だけを与える。<br/>
    /// <b>表示可否は本層が反映する</b>: 計画は表示可否（外観の <c>ShowCoordLabel</c>）でも指示を消さないため、<br/>
    /// 要素として置くかどうかを要素の <c>IsVisible</c> で表す。
    /// </remarks>
    private static void Apply(Border border, in LoupeLabelPlacement placement)
    {
        border.IsVisible = placement.IsVisible;
        border.Padding(placement.PaddingX, placement.PaddingY);
        border.CornerRadius(placement.CornerRadius);
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
    /// <remarks>
    /// モニター間の移動で DPI が変わっても物理ピクセル基準の見た目を保つため、<see cref="ClientToPixelScaling"/> を<br/>
    /// DPI から換算した値へ更新する（ホストが明示設定した値も DPI 変更時は上書きされる）。<br/>
    /// 換算規則は <see cref="DpiScaler.ToScaling"/>（コア）が単一所有する。<br/>
    /// ホストがスケーリングを明示する必要はない（DPI はコントロールが自動取得する）。
    /// </remarks>
    protected override void OnDpiChanged(uint oldDpi, uint newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        ClientToPixelScaling = DpiScaler.ToScaling(newDpi);
    }

    /// <summary>描画先の表面サイズから描画に使う実効倍率を解決し、保持値へも反映する。</summary>
    /// <param name="info">描画先の情報（物理ピクセルの大きさ）。</param>
    /// <returns>描画に使うスケーリング係数（保持値へ反映済みの値）。</returns>
    /// <remarks>
    /// <b>なぜ描画面から求めるか</b>: DPI の変更通知（<see cref="OnDpiChanged"/>）は<b>追加直後のコントロールへ届かない</b><br/>
    /// （実測: ウィンドウ表示後に追加したコントロールは登録時の既定値 1.0 のままになる）。<br/>
    /// その状態で保持値だけを使うと、拡大画像がルーペ領域（描画面）より小さい倍率で描かれる<br/>
    /// （実測: 125% の環境で 1/1.25 の大きさになり、右と下にペーストボードの帯が残る）。<br/>
    /// 描画面（物理ピクセル）と割り当て領域（DIP）の比は実際に描くときの倍率そのものであるため、こちらを保持値とする<br/>
    /// （保持値の変更はファサードへ伝搬されるため、拡大画像の物理ピクセルサイズの算出元は 1 つに保たれる）。
    /// </remarks>
    internal double ApplyClientPixelScaling(SKImageInfo info)
    {
        var resolved = ClientPixelScale.Resolve(info.Width, Bounds.Size.Width, ClientToPixelScaling);

        if (resolved == ClientToPixelScaling)
        {
            return ClientToPixelScaling;
        }

        ClientToPixelScaling = resolved;

        // 保持値の補正（範囲外は安全域へ）を通した値を返す（計算に使う値と保持値を一致させる）
        return ClientToPixelScaling;
    }

    /// <summary>ルーペを描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（物理ピクセル）。</param>
    /// <param name="info">描画先の情報（幅・高さ）。</param>
    /// <remarks>
    /// 描画計画（描画層が解決した幾何・外観・層の順序）を自 UI の公開プロパティから組み立てて描画層へ渡す<br/>
    /// （DIP → 物理ピクセルの換算は描画層が単一所有する）。<br/>
    /// <b>描く層とその順序は描画層が単一所有する</b>（<see cref="LoupeDrawPlan.Layers"/> の順に処理する）。<br/>
    /// <b>キャンバスを拡大せず 1:1 で描く</b>: 描画計画の矩形（描画面・拡大画像の描画先・枠線の太さ）は<b>物理ピクセル</b>で<br/>
    /// 解決されるため、拡大すると設定した拡大率より大きい画像をルーペ領域へ描くことになり、切り取られてずれる。<br/>
    /// 中心座標ラベルはテキストの実寸測定が必要なため本キャンバスでは描かず、<br/>
    /// 親パネルのラベル要素（<c>Border</c> + <c>TextBlock</c>）が表示する（要素は DIP で配置する）。
    /// </remarks>
    private void OnPaintSurface(SKCanvas canvas, SKImageInfo info)
    {
        // 描画先の表面サイズ（物理ピクセル）から表示サイズ（DIP）を導いて描画計画を組み立てる
        // （表面は要素の大きさそのものであるため、要素の大きさと描画サイズが一致する）。
        // 倍率は描画面と割り当て領域（DIP）の比から求め、保持値とファサードへも反映する
        // （反映しないと、拡大画像がルーペ領域より小さい倍率で描かれる）。
        var scaling = ApplyClientPixelScaling(info);
        var plan = ResolvePlan(DpiScaler.ToDip(info.Size.ToEsSize(), scaling).Width);

        try
        {
            // 描画の手順（層の順序と描画 API への写し）は LoupeSkiaPainter が単一所有する
            LoupeSkiaPainter.Paint(canvas, Source, in plan);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 描画の例外はアプリを落とさずログへ記録する（キャンバスは中途半端な状態になりうるが、次の描画で復帰する）
            Log.LogUpdateError(ex);
            return;
        }

        Rendered?.Invoke(this, new LoupeRenderedEventArgs(canvas, plan));
    }
}
