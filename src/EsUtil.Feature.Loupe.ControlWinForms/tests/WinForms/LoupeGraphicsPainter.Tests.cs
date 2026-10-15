using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;
using DrawingColor = System.Drawing.Color;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingRectangle = System.Drawing.Rectangle;

namespace EsUtil.Feature.Loupe.WinForms.Tests;

/// <summary><see cref="LoupeGraphicsPainter"/> の描画テスト（画素を検証する）。</summary>
/// <remarks>
/// 描画計画（<see cref="LoupeDrawPlan"/>）は実際の提示層（<see cref="LoupePresenter"/>）が解決するため、<br/>
/// 本テストは「層の順序に従って描画 API へ写す」ことと「描画計画の値が画素へ現れる」ことを同時に検証する。<br/>
/// 画素の検証は GDI+（<see cref="Graphics"/>）で行うため、ウィンドウもメッセージ ループも要らない<br/>
/// （描画面は <see cref="Bitmap"/> から作り、<c>GetPixel</c> で読む）。<br/>
/// 色・表示可否は外観（<see cref="LoupeAppearance"/>）が単一所有する。
/// </remarks>
public class LoupeGraphicsPainterTests
{
    /// <summary>ペーストボード（画像外）の色（赤・不透明）。</summary>
    private static readonly DrawingColor s_background = DrawingColor.FromArgb(255, 255, 0, 0);

    /// <summary>拡大画像に使う色（青・不透明）。</summary>
    private static readonly DrawingColor s_image = DrawingColor.FromArgb(255, 0, 0, 255);

    /// <summary>枠線の色（黄・不透明）。</summary>
    private static readonly DrawingColor s_border = DrawingColor.FromArgb(255, 255, 255, 0);

    /// <summary>十字線の色（白・不透明）。</summary>
    private static readonly DrawingColor s_cross = DrawingColor.FromArgb(255, 255, 255, 255);

    /// <summary>下地（コントロールの背景色）を模した色（緑・不透明）。</summary>
    private static readonly DrawingColor s_underlay = DrawingColor.FromArgb(255, 0, 128, 0);

    /// <summary>ルーペの一辺（物理ピクセル。 表示 DIP と等しい）。</summary>
    private const int LoupeSize = 32;

    /// <summary>拡大中心（描画先の中央）。</summary>
    private static readonly EsPoint s_center = new(16, 16);

    // ====================================================================
    // ペーストボード（画像外の色）
    // ====================================================================

    /// <summary>ペーストボードが描画面全体を置き換えることを検証する。</summary>
    [Fact]
    internal void Paint_ペーストボードで描画面を置き換える()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize, s_underlay);

        Paint(target, CreateAppearance(pasteboard: s_background), s_center);

        Assert.Equal(s_background.ToArgb(), target.GetPixel(0, 0).ToArgb());
        Assert.Equal(s_background.ToArgb(), target.GetPixel(LoupeSize - 1, LoupeSize - 1).ToArgb());
    }

    /// <summary>拡大中心が未設定でもペーストボードを描くことを検証する。</summary>
    [Fact]
    internal void Paint_拡大中心が未設定でもペーストボードを描く()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize, s_underlay);

        Paint(target, CreateAppearance(pasteboard: s_background), center: null);

        Assert.Equal(s_background.ToArgb(), target.GetPixel(1, 1).ToArgb());
    }

    /// <summary>ペーストボードが非表示なら描画面を置き換えない（下地を保つ）ことを検証する。</summary>
    /// <remarks>
    /// 透明は画面へ伝えられないため、本 UI では「下地（コントロールの背景色）を見せる」ことを意味する。<br/>
    /// 透明で塗ると未定義の下地（多くの場合 黒）が残るため、置き換えない<br/>
    /// （他の UI は描画面を透明で塗って前のフレームを消す。 下地はフレームワークが毎フレーム塗る）。
    /// </remarks>
    [Fact]
    internal void Paint_ペーストボードが非表示なら描画面を置き換えない()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize, s_underlay);

        Paint(target, CreateAppearance(pasteboard: s_background, showPasteboard: false), s_center);

        Assert.Equal(s_underlay.ToArgb(), target.GetPixel(0, 0).ToArgb());
        Assert.Equal(s_underlay.ToArgb(), target.GetPixel(16, 16).ToArgb());
    }

    // ====================================================================
    // 拡大画像
    // ====================================================================

    /// <summary>画像の範囲外（画像端の外）はペーストボードの色（無地）になることを検証する。</summary>
    /// <remarks>
    /// 画像の外は描かないため、描画面全体を塗るペーストボードの色がそのまま見える<br/>
    /// （無地）。 中心を画像の左外へ寄せ、切り出し領域の左端が画像の外へ出る状態を作る。
    /// </remarks>
    [Fact]
    internal void Paint_画像の外はペーストボードの色になる()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize, s_underlay);
        using var source = TestHelper.CreateBitmap(LoupeSize, LoupeSize, s_image);

        // 中心 x=1・一辺 32・拡大率 4 → 切り出し 9 px の左上 x=-3（画像の外）→ 描画先の左側が画像外になる
        Paint(target, CreateAppearance(pasteboard: s_background), new EsPoint(1, LoupeSize / 2), source);

        // 画像の外（左端）はペーストボードの色、画像の中（右端）は拡大画像の色
        Assert.Equal(s_background.ToArgb(), target.GetPixel(0, LoupeSize / 2).ToArgb());
        Assert.Equal(s_image.ToArgb(), target.GetPixel(LoupeSize - 1, LoupeSize / 2).ToArgb());
    }

    /// <summary>拡大中心の周囲を切り出して拡大描画することを検証する。</summary>
    [Fact]
    internal void Paint_拡大中心の周囲を切り出して拡大描画する()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize, s_underlay);
        using var source = TestHelper.CreateBitmap(LoupeSize, LoupeSize, s_image);

        Paint(target, CreateAppearance(pasteboard: s_background), s_center, source);

        // 切り出した領域（一様な色）がルーペ全体へ拡大される
        Assert.Equal(s_image.ToArgb(), target.GetPixel(0, 0).ToArgb());
        Assert.Equal(s_image.ToArgb(), target.GetPixel(16, 16).ToArgb());
        Assert.Equal(s_image.ToArgb(), target.GetPixel(LoupeSize - 1, LoupeSize - 1).ToArgb());
    }

    /// <summary>画像の内側の切り出しは元の位置の画素を描くことを検証する（行と列のずれの検出）。</summary>
    /// <remarks>
    /// 切り出しは行幅（元画像のストライド）を持つため、行幅や原点の渡し方が誤っていると 2 行目以降がずれる。<br/>
    /// そのため列と行で値が変わる画像を使い、1 行目と 2 行目の両方を検証する。
    /// </remarks>
    [Fact]
    internal void Paint_画像の内側の切り出しは元の位置の画素を描く()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize, s_underlay);

        // 列と行で値が変わる画像（B = X、G = Y。 位置と行のずれを検出する）
        using var source = TestHelper.CreatePositionMap(LoupeSize * 2, LoupeSize * 2);

        // 中心 (32, 32)・一辺 32・拡大率 4 → 切り出し 9 px の左上 (28, 28)（画像の内側）
        Paint(target, CreateAppearance(pasteboard: s_background), new EsPoint(32, 32), source);

        // 描画先の (0, 0) は切り出し領域の左上（元画像の (28, 28)）
        Assert.Equal(DrawingColor.FromArgb(255, 0, 28, 28).ToArgb(), target.GetPixel(0, 0).ToArgb());

        // 描画先の (0, 4) は切り出し領域の 2 行目（元画像の (28, 29)）。 行幅が誤っているとここがずれる
        Assert.Equal(DrawingColor.FromArgb(255, 0, 29, 28).ToArgb(), target.GetPixel(0, 4).ToArgb());
    }

    /// <summary>ソースが無ければ拡大画像を描かないことを検証する。</summary>
    [Fact]
    internal void Paint_ソースが無ければ拡大画像を描かない()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize, s_underlay);

        Paint(target, CreateAppearance(pasteboard: s_background), s_center);

        // ペーストボードのみ（拡大画像は描かれない）
        Assert.Equal(s_background.ToArgb(), target.GetPixel(16, 16).ToArgb());
    }

    /// <summary>拡大中心が未設定なら拡大画像を描かないことを検証する。</summary>
    [Fact]
    internal void Paint_拡大中心が未設定なら拡大画像を描かない()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize, s_underlay);
        using var source = TestHelper.CreateBitmap(LoupeSize, LoupeSize, s_image);

        Paint(target, CreateAppearance(pasteboard: s_background), center: null, source);

        Assert.Equal(s_background.ToArgb(), target.GetPixel(16, 16).ToArgb());
    }

    // ====================================================================
    // 十字線・枠線
    // ====================================================================

    /// <summary>十字線を切り出し領域の中央へ描くことを検証する。</summary>
    [Fact]
    internal void Paint_十字線を切り出し領域の中央へ描く()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize, s_underlay);

        Paint(target, CreateAppearance(pasteboard: s_background, crosshair: s_cross), s_center);

        // 拡大率 4・一辺 32 → 交点は 16、線の太さは 4
        Assert.Equal(s_cross.ToArgb(), target.GetPixel(17, 1).ToArgb());
        Assert.Equal(s_cross.ToArgb(), target.GetPixel(1, 17).ToArgb());

        // 十字の外側はペーストボードのまま
        Assert.Equal(s_background.ToArgb(), target.GetPixel(1, 1).ToArgb());
    }

    /// <summary>十字線が非表示なら描かないことを検証する。</summary>
    [Fact]
    internal void Paint_十字線が非表示なら描かない()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize, s_underlay);

        Paint(target, CreateAppearance(pasteboard: s_background, crosshair: s_cross, showCrosshair: false), s_center);

        Assert.Equal(s_background.ToArgb(), target.GetPixel(17, 1).ToArgb());
    }

    /// <summary>枠線を四辺へ太さ分だけ描くことを検証する。</summary>
    [Fact]
    internal void Paint_枠線を四辺へ太さ分だけ描く()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize, s_underlay);

        Paint(target, CreateAppearance(pasteboard: s_background, border: s_border), s_center);

        // 上辺（太さ 2）と左辺
        Assert.Equal(s_border.ToArgb(), target.GetPixel(16, 1).ToArgb());
        Assert.Equal(s_border.ToArgb(), target.GetPixel(1, 16).ToArgb());

        // 内側はペーストボードのまま
        Assert.Equal(s_background.ToArgb(), target.GetPixel(16, 16).ToArgb());
    }

    /// <summary>枠線が非表示なら描かないことを検証する。</summary>
    [Fact]
    internal void Paint_枠線が非表示なら描かない()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize, s_underlay);

        Paint(target, CreateAppearance(pasteboard: s_background, border: s_border, showBorder: false), s_center);

        Assert.Equal(s_background.ToArgb(), target.GetPixel(16, 1).ToArgb());
    }

    // ====================================================================
    // 中心座標ラベル（背景と文字を描画層が描く）
    // ====================================================================

    /// <summary>ラベルの背景と文字を描くことを検証する。</summary>
    /// <remarks>
    /// 配置指示の矩形・余白・フォントサイズは DIP であるため、物理ピクセルへ換算して描かれる<br/>
    /// （本テストはスケーリング係数 1.0 のため DIP と物理ピクセルが一致する）。
    /// </remarks>
    [Fact]
    internal void Paint_ラベルの背景と文字を描く()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize, s_underlay);
        var label = new LoupeLabelPlacement(
            new EsRect(2, 2, 24, 14), 2, 1, "AB", 8, DrawingColor.White, DrawingColor.Black, 0, IsVisible: true);

        PaintPlan(target, CreatePlan(CreateAppearance(pasteboard: s_background), s_center) with { Labels = [label] });

        // 文字の無い位置は背景色（黒）
        Assert.Equal(DrawingColor.Black.ToArgb(), target.GetPixel(3, 13).ToArgb());

        // 文字の色（白）の画素が背景の中に現れる
        Assert.True(CountPixels(target, new DrawingRectangle(2, 2, 24, 14), static pixel => pixel is { R: > 200, G: > 200, B: > 200 }) > 0);
    }

    /// <summary>ラベルが非表示の指示なら描かないことを検証する。</summary>
    /// <remarks>計画は表示を止めても指示を消さないため、描くかどうかは描画側が判断する。</remarks>
    [Fact]
    internal void Paint_ラベルが非表示なら描かない()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize, s_underlay);
        var label = new LoupeLabelPlacement(
            new EsRect(2, 2, 24, 14), 2, 1, "AB", 8, DrawingColor.White, DrawingColor.Black, 0, IsVisible: false);

        PaintPlan(target, CreatePlan(CreateAppearance(pasteboard: s_background), s_center) with { Labels = [label] });

        Assert.Equal(s_background.ToArgb(), target.GetPixel(3, 13).ToArgb());
    }

    // ====================================================================
    // 層の差し替え
    // ====================================================================

    /// <summary>レイヤーを差し替えると装飾だけを止められることを検証する。</summary>
    /// <remarks>
    /// ペーストボード（描画面の消去）と拡大画像は層から外すと前のフレームが残る・画像が欠けるため残す<br/>
    /// （外観を変えずに止められるので、止めた後も矩形の計算値を自前の描画へ使える）。
    /// </remarks>
    [Fact]
    internal void Paint_レイヤーを差し替えると装飾だけを止められる()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize, s_underlay);
        var plan = CreatePlan(
            CreateAppearance(pasteboard: s_background, border: s_border), s_center);
        PaintPlan(target, plan);
        var withBorder = target.GetPixel(16, 1);

        // ホストが層を差し替える（ペーストボードと拡大画像だけを残す）
        var host = plan with { Layers = [LoupeDrawLayer.Pasteboard, LoupeDrawLayer.Image] };
        PaintPlan(target, host);

        Assert.NotEqual(withBorder.ToArgb(), target.GetPixel(16, 1).ToArgb());
        Assert.Equal(s_background.ToArgb(), target.GetPixel(16, 1).ToArgb());

        // 値は残る（ホストは矩形を使って自前で描ける）
        Assert.Equal(2, host.CrosshairPixelRects.Count);
        Assert.Equal(4, host.BorderPixelRects.Count);
    }

    // ====================================================================
    // 引数の検証と描画面の状態
    // ====================================================================

    /// <summary>描画先が null なら例外になることを検証する。</summary>
    [Fact]
    internal void Paint_描画先がnullなら例外()
    {
        var plan = CreatePlan(CreateAppearance(), s_center);

        Assert.Throws<ArgumentNullException>(() => LoupeGraphicsPainter.Paint(null!, null, in plan));
    }

    /// <summary>描画の前後で描画面の状態（補間方法・合成方法）が変わらないことを検証する。</summary>
    /// <remarks>
    /// 拡大画像の補間方法と塗りの合成方法を変えるため、復帰しないと呼び出し側（描画完了通知のオーバーレイ等）の<br/>
    /// 描画へ影響が残る。
    /// </remarks>
    [Fact]
    internal void Paint_描画の前後で描画面の状態を復帰する()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize, s_underlay);
        using var source = TestHelper.CreateBitmap(LoupeSize, LoupeSize, s_image);
        var plan = CreatePlan(CreateAppearance(pasteboard: s_background, border: s_border), s_center);

        var interpolation = target.Graphics.InterpolationMode;
        var compositing = target.Graphics.CompositingMode;
        var smoothing = target.Graphics.SmoothingMode;

        PaintPlan(target, plan, source);

        Assert.Equal(interpolation, target.Graphics.InterpolationMode);
        Assert.Equal(compositing, target.Graphics.CompositingMode);
        Assert.Equal(smoothing, target.Graphics.SmoothingMode);
    }

    // ====================================================================
    // ヘルパー
    // ====================================================================

    /// <summary>指定の外観・中心で描画計画を解決し、描画先へ描く。</summary>
    /// <param name="target">描画先。</param>
    /// <param name="appearance">外観。</param>
    /// <param name="center">拡大中心。</param>
    /// <param name="source">拡大元の画像。</param>
    private static void Paint(RenderTarget target, LoupeAppearance appearance, EsPoint? center, EsReadOnlyBitmap? source = null)
        => PaintPlan(target, CreatePlan(appearance, center), source);

    /// <summary>描画計画を描画先へ描く。</summary>
    /// <param name="target">描画先。</param>
    /// <param name="plan">描画計画。</param>
    /// <param name="source">拡大元の画像。</param>
    private static void PaintPlan(RenderTarget target, in LoupeDrawPlan plan, EsReadOnlyBitmap? source = null)
        => LoupeGraphicsPainter.Paint(target.Graphics, source, in plan);

    /// <summary>テスト用の描画計画を提示層（実際の解決経路）で解決する。</summary>
    /// <param name="appearance">外観。</param>
    /// <param name="center">拡大中心。</param>
    /// <returns>描画計画。</returns>
    /// <remarks>実寸測定はダミーを渡す（描画計画の画素検証には影響しない）。 ラベルを検証する場合は配置指示を差し替える。</remarks>
    private static LoupeDrawPlan CreatePlan(LoupeAppearance appearance, EsPoint? center)
    {
        var presenter = new LoupePresenter
        {
            Options = LoupeOptions.Default with { RequestedSize = LoupeSize, ZoomLevel = 4 },
            Appearance = appearance,
            CenterPoint = center,
        };

        return presenter.ResolveDrawPlan(LoupeSize, static (_, _) => default);
    }

    /// <summary>テスト用の外観を生成する（指定しない色は透明＝描かない）。</summary>
    /// <param name="pasteboard">ペーストボード（画像外）の色。</param>
    /// <param name="border">枠線の色。</param>
    /// <param name="crosshair">十字線の色。</param>
    /// <param name="showCrosshair">十字線を表示するかどうか。</param>
    /// <param name="showBorder">枠線を表示するかどうか。</param>
    /// <param name="showPasteboard">ペーストボードを表示するかどうか。</param>
    /// <returns>描画に使う外観。</returns>
    private static LoupeAppearance CreateAppearance(
        DrawingColor? pasteboard = null,
        DrawingColor? border = null,
        DrawingColor? crosshair = null,
        bool showCrosshair = true,
        bool showBorder = true,
        bool showPasteboard = true)
        => LoupeAppearance.Default with
        {
            PasteboardColor = pasteboard ?? DrawingColor.Transparent,
            BorderColor = border ?? DrawingColor.Transparent,
            CrossColor = crosshair ?? DrawingColor.Transparent,
            BorderThickness = 2,
            ShowCrosshair = showCrosshair,
            ShowBorder = showBorder,
            ShowPasteboard = showPasteboard,
            ShowCoordLabel = false,
        };

    /// <summary>指定領域の中で条件を満たす画素の数を数える。</summary>
    /// <param name="target">描画先。</param>
    /// <param name="area">数える領域。</param>
    /// <param name="predicate">数える条件。</param>
    /// <returns>条件を満たす画素の数。</returns>
    private static int CountPixels(RenderTarget target, DrawingRectangle area, Func<DrawingColor, bool> predicate)
    {
        var count = 0;
        for (var y = area.Top; y < area.Bottom; y++)
        {
            for (var x = area.Left; x < area.Right; x++)
            {
                if (predicate(target.GetPixel(x, y)))
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>画素を検証できる描画先（ビットマップとその描画面）。</summary>
    /// <remarks>ウィンドウを持たないため、テストはビットマップの画素を直接読める。</remarks>
    private sealed class RenderTarget : IDisposable
    {
        /// <summary>描画結果を保持するビットマップ。</summary>
        private readonly Bitmap _bitmap;

        /// <summary>指定サイズの描画先を生成し、下地の色で塗る。</summary>
        /// <param name="width">幅（ピクセル）。</param>
        /// <param name="height">高さ（ピクセル）。</param>
        /// <param name="underlay">下地（コントロールの背景色を模した色）。</param>
        public RenderTarget(int width, int height, DrawingColor underlay)
        {
            _bitmap = new Bitmap(width, height, DrawingPixelFormat.Format32bppPArgb);
            Graphics = Graphics.FromImage(_bitmap);

            // 前のフレームを模して下地を塗っておく（ペーストボードが置き換えるかどうかを検証できる）
            Graphics.Clear(underlay);
        }

        /// <summary>描画先の描画面を取得する。</summary>
        public Graphics Graphics { get; }

        /// <summary>指定座標の画素を取得する。</summary>
        /// <param name="x">X 座標。</param>
        /// <param name="y">Y 座標。</param>
        /// <returns>画素の色（アルファ乗算前の値）。</returns>
        public DrawingColor GetPixel(int x, int y) => _bitmap.GetPixel(x, y);

        /// <inheritdoc />
        public void Dispose()
        {
            Graphics.Dispose();
            _bitmap.Dispose();
        }
    }
}
