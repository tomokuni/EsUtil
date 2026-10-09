using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Feature.Loupe.MewUI.Internal;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Loupe.MewUI.Tests;

/// <summary><see cref="LoupeSkiaPainter"/> の描画テスト（画素を検証する）。</summary>
/// <remarks>
/// 描画計画（<see cref="LoupeDrawPlan"/>）は実際の提示層（<see cref="LoupePresenter"/>）が解決するため、<br/>
/// 本テストは「層の順序に従って描画 API へ写す」ことと「描画計画の値が画素へ現れる」ことを同時に検証する。<br/>
/// 画素の検証は Skia（<c>SKCanvas</c>）で行うため、UI 初期化とウィンドウなしで実行できる<br/>
/// （Avalonia ヘッドレスでは <c>RenderTargetBitmap</c> の画素を読めないため、描画の検証は本テストが担う）。<br/>
/// 色・表示可否は外観（<see cref="LoupeAppearance"/>）が単一所有する。
/// </remarks>
public class LoupeSkiaPainterTests
{
    /// <summary>ペーストボード（画像外）の色（赤・不透明）。</summary>
    private static readonly DrawingColor s_background = DrawingColor.FromArgb(255, 255, 0, 0);

    /// <summary>拡大画像に使う色（青・不透明）。</summary>
    private static readonly DrawingColor s_image = DrawingColor.FromArgb(255, 0, 0, 255);

    /// <summary>枠線の色（黄・不透明）。</summary>
    private static readonly DrawingColor s_border = DrawingColor.FromArgb(255, 255, 255, 0);

    /// <summary>十字線の色（白・不透明）。</summary>
    private static readonly DrawingColor s_cross = DrawingColor.FromArgb(255, 255, 255, 255);

    /// <summary>ペーストボードの赤（画素比較用）。</summary>
    private static readonly SKColor s_backgroundPixel = new(255, 0, 0);

    /// <summary>ルーペの一辺（物理ピクセル）。</summary>
    private const int LoupeSize = 32;

    /// <summary>拡大中心（描画先の中央）。</summary>
    private static readonly EsPoint s_center = new(16, 16);

    // ====================================================================
    // ペーストボード（画像外の色）
    // ====================================================================

    [Fact]
    internal void Paint_ペーストボードで描画面を置き換える()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);
        // 前フレームの残像を模して別の色で塗っておく
        target.Canvas.Clear(s_image.ToSKColor());

        Paint(target, CreateAppearance(pasteboard: s_background), s_center);

        Assert.Equal(s_backgroundPixel, target.GetPixel(0, 0));
        Assert.Equal(s_backgroundPixel, target.GetPixel(LoupeSize - 1, LoupeSize - 1));
    }

    [Fact]
    internal void Paint_拡大中心が未設定でもペーストボードを描く()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);

        Paint(target, CreateAppearance(pasteboard: s_background), center: null);

        Assert.Equal(s_backgroundPixel, target.GetPixel(1, 1));
    }

    /// <summary>ペーストボードが非表示なら前フレームを消して透明にすることを 検証する。</summary>
    /// <remarks>
    /// 描画面を保持する UI（MewUI のキャンバス）では層を省くと前フレームが残るため、<br/>
    /// 非表示は「描画面を透明で塗る」として実装する（<see cref="LoupeAppearance.ResolvePasteboardColor"/>）。
    /// </remarks>
    [Fact]
    internal void Paint_ペーストボードが非表示なら透明で塗る()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);
        // 前フレームの残像を模して別の色で塗っておく
        target.Canvas.Clear(s_image.ToSKColor());

        Paint(target, CreateAppearance(pasteboard: s_background, showPasteboard: false), s_center);

        // 透明（アルファ 0）になり、前フレームの色も残らない
        Assert.Equal(0, target.GetPixel(16, 16).Alpha);
        Assert.Equal(0, target.GetPixel(0, 0).Alpha);
    }

    // ====================================================================
    // 拡大画像
    // ====================================================================

    /// <summary>画像の範囲外（画像端の外）はペーストボードの色（無地）になることを検証する。</summary>
    /// <remarks>
    /// 画像外は「透明パディング」として切り出されるため、描画面全体を塗るペーストボードの色がそのまま見える<br/>
    /// （市松模様ではなく無地）。 中心を画像の左外へ寄せ、切り出し領域の左端が画像の外へ出る状態を作る。
    /// </remarks>
    [Fact]
    internal void Paint_画像の外はペーストボードの色になる()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);
        using var source = EsWritableBitmap.Create(LoupeSize, LoupeSize, s_image);

        // 中心 x=1・一辺 32・拡大率 4 → 切り出し 8 px の左上 x=-3（画像の外）→ 描画先の左側が画像外になる
        Paint(target, CreateAppearance(pasteboard: s_background), new EsPoint(1, LoupeSize / 2), source);

        // 画像の外（左端）はペーストボードの色、画像の中（右端）は拡大画像の色
        Assert.Equal(s_backgroundPixel, target.GetPixel(0, LoupeSize / 2));
        Assert.Equal(new SKColor(0, 0, 255), target.GetPixel(LoupeSize - 1, LoupeSize / 2));
    }

    [Fact]
    internal void Paint_拡大中心の周囲を切り出して拡大描画する()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);
        using var source = EsWritableBitmap.Create(LoupeSize, LoupeSize, s_image);

        Paint(target, CreateAppearance(pasteboard: s_background), s_center, source);

        // 切り出した領域（一様な色）がルーペ全体へ拡大される
        Assert.Equal(new SKColor(0, 0, 255), target.GetPixel(0, 0));
        Assert.Equal(new SKColor(0, 0, 255), target.GetPixel(16, 16));
        Assert.Equal(new SKColor(0, 0, 255), target.GetPixel(LoupeSize - 1, LoupeSize - 1));
    }

    [Fact]
    internal void Paint_ソースが無ければ拡大画像を描かない()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);

        Paint(target, CreateAppearance(pasteboard: s_background), s_center);

        // ペーストボードのみ（拡大画像は描かれない）
        Assert.Equal(s_backgroundPixel, target.GetPixel(16, 16));
    }

    [Fact]
    internal void Paint_拡大中心が未設定なら拡大画像を描かない()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);
        using var source = EsWritableBitmap.Create(LoupeSize, LoupeSize, s_image);

        Paint(target, CreateAppearance(pasteboard: s_background), center: null, source);

        Assert.Equal(s_backgroundPixel, target.GetPixel(16, 16));
    }

    // ====================================================================
    // 十字線・枠線
    // ====================================================================

    [Fact]
    internal void Paint_十字線を切り出し領域の中央へ描く()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);

        Paint(target, CreateAppearance(pasteboard: s_background, crosshair: s_cross), s_center);

        // 拡大率 4・一辺 32 → 切り出し一辺 8、交点は 8 / 2 × 4 = 16
        Assert.Equal(new SKColor(255, 255, 255), target.GetPixel(17, 1));
        Assert.Equal(new SKColor(255, 255, 255), target.GetPixel(1, 17));
        // 十字の外側はペーストボードのまま
        Assert.Equal(s_backgroundPixel, target.GetPixel(1, 1));
    }

    [Fact]
    internal void Paint_十字線が非表示なら描かない()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);

        Paint(target, CreateAppearance(pasteboard: s_background, crosshair: s_cross, showCrosshair: false), s_center);

        Assert.Equal(s_backgroundPixel, target.GetPixel(17, 1));
    }

    [Fact]
    internal void Paint_枠線を四辺へ太さ分だけ描く()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);

        Paint(target, CreateAppearance(pasteboard: s_background, border: s_border), s_center);

        // 上辺（太さ 2）
        Assert.Equal(new SKColor(255, 255, 0), target.GetPixel(16, 1));
        // 左辺
        Assert.Equal(new SKColor(255, 255, 0), target.GetPixel(1, 16));
        // 内側はペーストボードのまま
        Assert.Equal(s_backgroundPixel, target.GetPixel(16, 16));
    }

    [Fact]
    internal void Paint_枠線が非表示なら描かない()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);

        Paint(target, CreateAppearance(pasteboard: s_background, border: s_border, showBorder: false), s_center);

        Assert.Equal(s_backgroundPixel, target.GetPixel(16, 1));
    }

    // ====================================================================
    // テキスト（描画実装は描かない）
    // ====================================================================

    [Fact]
    internal void Paint_はテキストを描かない()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);

        // ラベルは UI 層が要素として置くか画像として描くかを決めるため、描画計画には背景色が含まれない
        // （座標ラベルを表示する設定でもペーストボードのみが描かれる）
        Paint(target, CreateAppearance(pasteboard: s_background), s_center);

        Assert.Equal(s_backgroundPixel, target.GetPixel(16, 2));
    }

    // ====================================================================
    // 引数の検証
    // ====================================================================

    [Fact]
    internal void Paint_描画先がnullなら例外()
    {
        using var target = new RenderTarget(4, 4);
        var plan = CreatePlan(CreateAppearance(), s_center);

        Assert.Throws<ArgumentNullException>(
            () => LoupeSkiaPainter.Paint(null!, null, in plan));
    }

    // ====================================================================
    // ヘルパー
    // ====================================================================

    /// <summary>指定の外観・中心で描画計画を解決し、描画先へ描く。</summary>
    /// <param name="target">描画先。</param>
    /// <param name="appearance">外観。</param>
    /// <param name="center">拡大中心。</param>
    /// <param name="source">拡大元の画像。</param>
    private static void Paint(RenderTarget target, LoupeAppearance appearance, EsPoint? center, EsWritableBitmap? source = null)
        => LoupeSkiaPainter.Paint(target.Canvas, source, CreatePlan(appearance, center));

    /// <summary>テスト用の描画計画を提示層（実際の解決経路）で解決する。</summary>
    /// <param name="appearance">外観。</param>
    /// <param name="center">拡大中心。</param>
    /// <returns>描画計画。</returns>
    /// <remarks>実寸測定はダミーを渡す（描画計画の画素検証には影響しない）。</remarks>
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

    /// <summary>画素を検証できる描画先（SKBitmap とそのキャンバス）。</summary>
    private sealed class RenderTarget : IDisposable
    {
        /// <summary>描画結果を保持するビットマップ。</summary>
        private readonly SKBitmap _bitmap;

        /// <summary>指定サイズの描画先を生成する。</summary>
        /// <param name="width">幅（ピクセル）。</param>
        /// <param name="height">高さ（ピクセル）。</param>
        public RenderTarget(int width, int height)
        {
            _bitmap = new SKBitmap(width, height);
            Canvas = new SKCanvas(_bitmap);
        }

        /// <summary>描画先のキャンバスを取得する。</summary>
        public SKCanvas Canvas { get; }

        /// <summary>指定位置の画素を取得する。</summary>
        /// <param name="x">X 座標。</param>
        /// <param name="y">Y 座標。</param>
        /// <returns>画素の色。</returns>
        public SKColor GetPixel(int x, int y) => _bitmap.GetPixel(x, y);

        /// <summary>キャンバスとビットマップを破棄する。</summary>
        public void Dispose()
        {
            Canvas.Dispose();
            _bitmap.Dispose();
        }
    }
}
