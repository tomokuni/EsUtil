using EsUtil.Ui.WinForms.Marquee.Internal;
using Xunit;

namespace EsUtil.Ui.WinForms.Marquee.Tests;

/// <summary><see cref="MarqueeGraphicsPainter"/> の描画テスト（実寸の画素を検査する）。</summary>
/// <remarks>
/// 描画実装は <see cref="Graphics"/> だけを要求するため、ウィンドウを持たないヘッドレス環境でも<br/>
/// <see cref="Bitmap"/> 上の画素を検査できる。 層の順序は提示層が単一所有するため、<br/>
/// 本テストは「与えられた層を描くかどうか（外観の表示可否）」と「層の順序どおりに上書きされること」を固定する。
/// </remarks>
public sealed class MarqueeGraphicsPainterTests
{
    /// <summary>検証に使う外枠の色。</summary>
    private static readonly Color s_borderColor = Color.FromArgb(255, 0, 128, 255);

    /// <summary>検証に使う内側の塗りの色。</summary>
    private static readonly Color s_innerFillColor = Color.FromArgb(255, 0, 255, 0);

    /// <summary>検証に使うハンドルの塗りの色。</summary>
    private static readonly Color s_handleColor = Color.Lime;

    /// <summary>検証に使うハンドルの枠線の色。</summary>
    private static readonly Color s_handleBorderColor = Color.Red;

    /// <summary>検証に使う十字マーカーの色。</summary>
    private static readonly Color s_markerColor = Color.Red;

    /// <summary>検証に使うラベルの背景色。</summary>
    private static readonly Color s_labelBackgroundColor = Color.FromArgb(255, 255, 255, 0);

    // ====================================================================
    // 引数
    // ====================================================================

    /// <summary>描画面が null の場合に引数例外になることを検証する。</summary>
    [Fact]
    internal void Paint_描画面がnullならArgumentNullExceptionになる()
    {
        var plan = TestHelper.CreatePlan(MarqueeAppearance.Default, new EsSize(10, 10), []);

        Assert.Throws<ArgumentNullException>(() => MarqueeGraphicsPainter.Paint(null!, in plan));
    }

    /// <summary>描く層が無い場合は何も描かないことを検証する。</summary>
    [Fact]
    internal void Paint_層が無ければ何も描かない()
    {
        var plan = TestHelper.CreatePlan(MarqueeAppearance.Default, new EsSize(40, 40), []);

        using var bitmap = TestHelper.Paint(plan, 40, 40);

        Assert.False(TestHelper.HasInk(bitmap));
    }

    /// <summary>表示可否が false の場合は値を渡しても描かないことを検証する。</summary>
    /// <remarks>計画は表示可否で値を消さないため、描くかどうかの判断は描画側が行う（その判断を固定する）。</remarks>
    [Fact]
    internal void Paint_表示可否がfalseなら描かない()
    {
        var plan = TestHelper.CreatePlan(
            CreateAppearance(handles: false, marker: false, overlay: false, border: false, innerFill: false),
            new EsSize(60, 60),
            [MarqueeDrawLayer.Decoration, MarqueeDrawLayer.Handles, MarqueeDrawLayer.Marker],
            focus: new EsRect(10, 10, 40, 40),
            overlay: [new EsRect(0, 0, 60, 10)],
            outerBorder: [new EsRect(10, 10, 40, 2)],
            innerFill: new EsRect(10, 10, 40, 40),
            handles: new MarqueeHandles([new EsRect(10, 10, 10, 10)], 2.0),
            marker: new MarqueeMarker(new EsPoint(30, 30), 8));

        using var bitmap = TestHelper.Paint(plan, 60, 60);

        Assert.False(TestHelper.HasInk(bitmap));
    }

    // ====================================================================
    // 装飾（暗転マスク・外枠・内側の塗り）
    // ====================================================================

    /// <summary>暗転マスクが半透明で塗られ、注目矩形の内側は塗られないことを検証する。</summary>
    [Fact]
    internal void Paint_暗転マスクを描く()
    {
        var focus = new EsRect(20, 20, 40, 40);
        var plan = TestHelper.CreatePlan(
            CreateAppearance(overlay: true),
            new EsSize(100, 100),
            [MarqueeDrawLayer.Decoration],
            focus: focus,
            overlay: [new EsRect(0, 0, 100, 20), new EsRect(0, 20, 20, 40)]);

        using var bitmap = TestHelper.Paint(plan, 100, 100);

        // 白（255）に黒の 50% を重ねると中間の灰色になる
        var masked = TestHelper.Pixel(bitmap, 5, 5);
        Assert.InRange(masked.R, 127, 128);

        // 注目矩形の内側は暗転しない
        Assert.Equal(Color.White.ToArgb(), TestHelper.Pixel(bitmap, 40, 40).ToArgb());
    }

    /// <summary>注目矩形が無く全面暗転が有効な場合は全体が暗転することを検証する。</summary>
    [Fact]
    internal void Paint_全面暗転を描く()
    {
        var plan = TestHelper.CreatePlan(
            CreateAppearance(overlay: true) with { ShowFullOverlay = true },
            new EsSize(50, 50),
            [MarqueeDrawLayer.Decoration],
            overlay: [new EsRect(0, 0, 50, 50)]);

        using var bitmap = TestHelper.Paint(plan, 50, 50);

        Assert.InRange(TestHelper.Pixel(bitmap, 25, 25).R, 127, 128);
    }

    /// <summary>外枠（4 辺の矩形）が指定の色で塗られることを検証する。</summary>
    [Fact]
    internal void Paint_外枠を描く()
    {
        var plan = TestHelper.CreatePlan(
            CreateAppearance(border: true),
            new EsSize(100, 100),
            [MarqueeDrawLayer.Decoration],
            focus: new EsRect(20, 20, 60, 60),
            outerBorder: [new EsRect(20, 20, 60, 2), new EsRect(20, 78, 60, 2)]);

        using var bitmap = TestHelper.Paint(plan, 100, 100);

        Assert.Equal(s_borderColor.ToArgb(), TestHelper.Pixel(bitmap, 40, 20).ToArgb());
        Assert.Equal(s_borderColor.ToArgb(), TestHelper.Pixel(bitmap, 40, 79).ToArgb());
        Assert.Equal(Color.White.ToArgb(), TestHelper.Pixel(bitmap, 40, 40).ToArgb());
    }

    /// <summary>内側の塗りが指定の色で塗られることを検証する。</summary>
    [Fact]
    internal void Paint_内側の塗りを描く()
    {
        var plan = TestHelper.CreatePlan(
            CreateAppearance(innerFill: true),
            new EsSize(100, 100),
            [MarqueeDrawLayer.Decoration],
            focus: new EsRect(10, 10, 30, 30),
            innerFill: new EsRect(10, 10, 30, 30));

        using var bitmap = TestHelper.Paint(plan, 100, 100);

        Assert.Equal(s_innerFillColor.ToArgb(), TestHelper.Pixel(bitmap, 25, 25).ToArgb());
        Assert.Equal(Color.White.ToArgb(), TestHelper.Pixel(bitmap, 5, 25).ToArgb());
    }

    // ====================================================================
    // ハンドル
    // ====================================================================

    /// <summary>ドラッグハンドルが塗りと枠線の 2 色で描かれることを検証する。</summary>
    [Fact]
    internal void Paint_ハンドルを描く()
    {
        var plan = TestHelper.CreatePlan(
            CreateAppearance(handles: true),
            new EsSize(60, 60),
            [MarqueeDrawLayer.Handles],
            handles: new MarqueeHandles([new EsRect(20, 20, 10, 10)], 2.0));

        using var bitmap = TestHelper.Paint(plan, 60, 60);

        // 塗りはハンドルの中心に残り、枠線は辺を覆う
        Assert.Equal(s_handleColor.ToArgb(), TestHelper.Pixel(bitmap, 24, 24).ToArgb());
        Assert.True(TestHelper.ContainsColor(bitmap, s_handleBorderColor));
        Assert.Equal(Color.White.ToArgb(), TestHelper.Pixel(bitmap, 5, 5).ToArgb());
    }

    /// <summary>ハンドルが無い場合はハンドルの層があっても何も描かないことを検証する。</summary>
    [Fact]
    internal void Paint_ハンドルが無ければ描かない()
    {
        var plan = TestHelper.CreatePlan(
            CreateAppearance(handles: true), new EsSize(40, 40), [MarqueeDrawLayer.Handles]);

        using var bitmap = TestHelper.Paint(plan, 40, 40);

        Assert.False(TestHelper.HasInk(bitmap));
    }

    // ====================================================================
    // 始点マーカー
    // ====================================================================

    /// <summary>十字マーカーが中心の縦横へ描かれることを検証する。</summary>
    [Fact]
    internal void Paint_始点マーカーを描く()
    {
        var plan = TestHelper.CreatePlan(
            CreateAppearance(marker: true),
            new EsSize(100, 100),
            [MarqueeDrawLayer.Marker],
            marker: new MarqueeMarker(new EsPoint(50, 50), 10));

        using var bitmap = TestHelper.Paint(plan, 100, 100);

        Assert.Equal(s_markerColor.ToArgb(), TestHelper.Pixel(bitmap, 50, 50).ToArgb());
        Assert.Equal(s_markerColor.ToArgb(), TestHelper.Pixel(bitmap, 45, 50).ToArgb());
        Assert.Equal(s_markerColor.ToArgb(), TestHelper.Pixel(bitmap, 50, 45).ToArgb());
        Assert.Equal(Color.White.ToArgb(), TestHelper.Pixel(bitmap, 30, 50).ToArgb());
    }

    /// <summary>マーカーが無い場合はマーカーの層があっても何も描かないことを検証する。</summary>
    [Fact]
    internal void Paint_マーカーが無ければ描かない()
    {
        var plan = TestHelper.CreatePlan(
            CreateAppearance(marker: true), new EsSize(40, 40), [MarqueeDrawLayer.Marker]);

        using var bitmap = TestHelper.Paint(plan, 40, 40);

        Assert.False(TestHelper.HasInk(bitmap));
    }

    // ====================================================================
    // ラベル（文字）
    // ====================================================================

    /// <summary>ラベルの背景と文字が描かれることを検証する。</summary>
    [Fact]
    internal void Paint_ラベルを描く()
    {
        var placement = new MarqueeLabelPlacement(
            new EsRect(10, 10, 90, 26),
            3.0,
            3.0,
            "800 x 600",
            14.0,
            Color.Black,
            s_labelBackgroundColor,
            true);
        var plan = TestHelper.CreatePlan(
            CreateAppearance(labels: true),
            new EsSize(120, 60),
            [MarqueeDrawLayer.Label],
            labels: [placement]);

        using var bitmap = TestHelper.Paint(plan, 120, 60);

        // 内側余白の部分は背景色のまま
        Assert.Equal(s_labelBackgroundColor.ToArgb(), TestHelper.Pixel(bitmap, 10, 10).ToArgb());
        // 文字の領域には背景色以外の画素がある
        Assert.False(AllPixelsAre(bitmap, 13, 13, 80, 20, s_labelBackgroundColor));
    }

    /// <summary>表示可否が false のラベルは描かないことを検証する。</summary>
    [Fact]
    internal void Paint_表示しないラベルは描かない()
    {
        var placement = new MarqueeLabelPlacement(
            new EsRect(10, 10, 90, 26), 3.0, 3.0, "800 x 600", 14.0, Color.Black, s_labelBackgroundColor, false);
        var plan = TestHelper.CreatePlan(
            CreateAppearance(labels: true),
            new EsSize(120, 60),
            [MarqueeDrawLayer.Label],
            labels: [placement]);

        using var bitmap = TestHelper.Paint(plan, 120, 60);

        Assert.False(TestHelper.HasInk(bitmap));
    }

    /// <summary>複数のラベルが一覧の順に描かれることを検証する。</summary>
    [Fact]
    internal void Paint_複数のラベルを描く()
    {
        var first = new MarqueeLabelPlacement(
            new EsRect(0, 0, 40, 20), 2.0, 2.0, "A", 12.0, Color.Black, s_labelBackgroundColor, true);
        var second = new MarqueeLabelPlacement(
            new EsRect(0, 30, 40, 20), 2.0, 2.0, "B", 12.0, Color.Black, s_labelBackgroundColor, true);
        var plan = TestHelper.CreatePlan(
            CreateAppearance(labels: true),
            new EsSize(60, 60),
            [MarqueeDrawLayer.Label],
            labels: [first, second]);

        using var bitmap = TestHelper.Paint(plan, 60, 60);

        Assert.Equal(s_labelBackgroundColor.ToArgb(), TestHelper.Pixel(bitmap, 2, 2).ToArgb());
        Assert.Equal(s_labelBackgroundColor.ToArgb(), TestHelper.Pixel(bitmap, 2, 32).ToArgb());
    }

    // ====================================================================
    // 層の順序
    // ====================================================================

    /// <summary>層の順序どおりに描かれ、後の層が前の層を上書きすることを検証する。</summary>
    /// <remarks>
    /// 順序は提示層（<see cref="MarqueeDrawPlan.Layers"/>）が単一所有するため、<br/>
    /// 描画実装は与えられた順に写すだけである（そのことを固定する）。
    /// </remarks>
    [Fact]
    internal void Paint_層の順序どおりに上書きする()
    {
        var plan = TestHelper.CreatePlan(
            CreateAppearance(overlay: true, handles: true) with { ShowFullOverlay = true },
            new EsSize(60, 60),
            [MarqueeDrawLayer.Decoration, MarqueeDrawLayer.Handles],
            overlay: [new EsRect(0, 0, 60, 60)],
            handles: new MarqueeHandles([new EsRect(20, 20, 10, 10)], 1.0));

        using var bitmap = TestHelper.Paint(plan, 60, 60);

        // 後から描いたハンドルは暗転の上に置かれる（暗転と混ざらない）
        Assert.Equal(s_handleColor.ToArgb(), TestHelper.Pixel(bitmap, 24, 24).ToArgb());
        // 暗転はハンドルの外側に残る
        Assert.InRange(TestHelper.Pixel(bitmap, 50, 50).R, 127, 128);
    }

    /// <summary>ハンドルを先に、暗転を後にした場合は暗転が上書きすることを検証する。</summary>
    [Fact]
    internal void Paint_逆順の層では後の層が優先される()
    {
        var plan = TestHelper.CreatePlan(
            CreateAppearance(overlay: true, handles: true) with { ShowFullOverlay = true },
            new EsSize(60, 60),
            [MarqueeDrawLayer.Handles, MarqueeDrawLayer.Decoration],
            overlay: [new EsRect(0, 0, 60, 60)],
            handles: new MarqueeHandles([new EsRect(20, 20, 10, 10)], 1.0));

        using var bitmap = TestHelper.Paint(plan, 60, 60);

        // 半透明の暗転がハンドルの上に重なる（ハンドルの色そのものにはならない）
        Assert.NotEqual(s_handleColor.ToArgb(), TestHelper.Pixel(bitmap, 24, 24).ToArgb());
    }

    // ====================================================================
    // 提示層との結合（実際に解決された計画を描く）
    // ====================================================================

    /// <summary>提示層が解決した計画をそのまま描けることを検証する。</summary>
    /// <remarks>
    /// 提示層（<see cref="MarqueePresenter"/>）と描画実装の結合を、UI 初期化なしで検証する。
    /// </remarks>
    [Fact]
    internal void Paint_提示層が解決した計画を描く()
    {
        var effects = new EffectsSpy();
        var presenter = new MarqueePresenter(effects)
        {
            Appearance = MarqueeAppearance.Default with
            {
                ShowSizeLabel = false,
                ShowCoordLabel = false,
                OuterBorderColor = s_borderColor,
            },
        };
        presenter.SetSelection(new EsRect(40, 30, 60, 50));

        var plan = presenter.ResolveDrawPlan(new EsSize(200, 200), GraphicsText.Measure);
        using var bitmap = TestHelper.Paint(plan, 200, 200);

        Assert.Contains(MarqueeDrawLayer.Decoration, plan.Layers);
        Assert.True(TestHelper.ContainsColor(bitmap, s_borderColor));
    }

    /// <summary>指定の領域のすべての画素が指定の色であるかどうかを調べる。</summary>
    /// <param name="bitmap">描画面。</param>
    /// <param name="x">領域の左端。</param>
    /// <param name="y">領域の上端。</param>
    /// <param name="width">領域の幅。</param>
    /// <param name="height">領域の高さ。</param>
    /// <param name="color">比較する色。</param>
    /// <returns>すべての画素が一致する場合は true。</returns>
    private static bool AllPixelsAre(Bitmap bitmap, int x, int y, int width, int height, Color color)
    {
        for (var offsetY = 0; offsetY < height; offsetY++)
        {
            for (var offsetX = 0; offsetX < width; offsetX++)
            {
                if (TestHelper.Pixel(bitmap, x + offsetX, y + offsetY).ToArgb() != color.ToArgb())
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>検証に使う外観を組み立てる（指定した項目だけを表示する）。</summary>
    /// <param name="handles">ドラッグハンドルを表示するかどうか。</param>
    /// <param name="marker">始点マーカーを表示するかどうか。</param>
    /// <param name="overlay">暗転マスクを表示するかどうか。</param>
    /// <param name="border">外枠を表示するかどうか。</param>
    /// <param name="innerFill">内側の塗りを表示するかどうか。</param>
    /// <param name="labels">テキストを表示するかどうか。</param>
    /// <returns>検証用の外観。</returns>
    private static MarqueeAppearance CreateAppearance(
        bool handles = false, bool marker = false, bool overlay = false, bool border = false, bool innerFill = false, bool labels = false)
        => MarqueeAppearance.Default with
        {
            ShowHandles = handles,
            ShowMarker = marker,
            ShowOverlay = overlay,
            ShowFullOverlay = false,
            ShowOuterBorder = border,
            ShowInnerFill = innerFill,
            ShowInnerBorder = false,
            ShowSizeLabel = labels,
            ShowCoordLabel = false,
            OverlayColor = Color.FromArgb(128, 0, 0, 0),
            OuterBorderColor = s_borderColor,
            InnerFillColor = s_innerFillColor,
            HandleColor = s_handleColor,
            HandleBorderColor = s_handleBorderColor,
            MarkerColor = s_markerColor,
            MarkerThickness = 2.0,
            SizeLabelColor = Color.Black,
            SizeLabelBackgroundColor = s_labelBackgroundColor,
            SizeLabelFontSize = 14.0,
            LabelPadding = 3.0,
        };
}
