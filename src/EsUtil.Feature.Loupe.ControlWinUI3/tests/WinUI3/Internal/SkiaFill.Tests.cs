using EsUtil.Feature.Loupe.WinUI3.Internal;
using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Loupe.WinUI3.Tests.Internal;

/// <summary><see cref="SkiaFill"/>（Skia の矩形塗り）のテスト。</summary>
/// <remarks>
/// 塗りは共有のペイント（色だけを書き換える 1 つ）を使うため、<b>呼び出しをまたいで色が入れ替わらない</b>ことも<br/>
/// あわせて検証する（実測: ペイントを使い回す実装では、色の取り違えが「前の色のまま描く」形で現れる）。
/// </remarks>
public class SkiaFillTests
{
    /// <summary>白（不透明）。</summary>
    private static readonly DrawingColor s_white = DrawingColor.FromArgb(255, 255, 255, 255);

    /// <summary>赤（不透明）。</summary>
    private static readonly DrawingColor s_red = DrawingColor.FromArgb(255, 255, 0, 0);

    /// <summary>矩形を指定の色で塗ることを検証する。</summary>
    [Fact]
    internal void Rect_矩形を塗る()
    {
        using var target = new RenderTarget(8, 8);

        SkiaFill.Rect(target.Canvas, new EsRect(2, 3, 4, 2), s_white);

        Assert.Equal(new SKColor(255, 255, 255), target.GetPixel(2, 3));
        Assert.Equal(new SKColor(255, 255, 255), target.GetPixel(5, 4));
        // 矩形の外は塗られない
        Assert.Equal(0, target.GetPixel(1, 3).Alpha);
        Assert.Equal(0, target.GetPixel(6, 5).Alpha);
    }

    /// <summary>複数の矩形が同じ色で塗られることを検証する。</summary>
    [Fact]
    internal void Rects_同じ色で複数の矩形を塗る()
    {
        using var target = new RenderTarget(8, 8);

        SkiaFill.Rects(target.Canvas, [new EsRect(0, 0, 2, 2), new EsRect(4, 4, 2, 2)], s_red);

        Assert.Equal(new SKColor(255, 0, 0), target.GetPixel(0, 0));
        Assert.Equal(new SKColor(255, 0, 0), target.GetPixel(5, 5));
        Assert.Equal(0, target.GetPixel(3, 3).Alpha);
    }

    /// <summary>空の一覧では何も描かないことを検証する。</summary>
    [Fact]
    internal void Rects_空の一覧では何もしない()
    {
        using var target = new RenderTarget(4, 4);

        SkiaFill.Rects(target.Canvas, [], s_red);

        Assert.Equal(0, target.GetPixel(0, 0).Alpha);
    }

    /// <summary>色は呼び出しごとに切り替わることを検証する（共有のペイントを使い回すため）。</summary>
    [Fact]
    internal void 連続する描画で色が入れ替わる()
    {
        using var target = new RenderTarget(8, 8);

        SkiaFill.Rect(target.Canvas, new EsRect(0, 0, 2, 2), s_red);
        SkiaFill.Rect(target.Canvas, new EsRect(4, 4, 2, 2), s_white);

        Assert.Equal(new SKColor(255, 0, 0), target.GetPixel(0, 0));
        Assert.Equal(new SKColor(255, 255, 255), target.GetPixel(5, 5));
    }

    /// <summary>角丸矩形の半径 0 が四角形として塗られることを検証する。</summary>
    [Fact]
    internal void RoundRect_半径0は四角形として塗る()
    {
        using var target = new RenderTarget(8, 8);

        SkiaFill.RoundRect(target.Canvas, new EsRect(1, 1, 6, 6), cornerRadius: 0, s_white);

        // 角（半径 0 では塗られる）
        Assert.Equal(new SKColor(255, 255, 255), target.GetPixel(1, 1));
        Assert.Equal(new SKColor(255, 255, 255), target.GetPixel(6, 6));
    }

    /// <summary>角丸矩形の角が落ちることを検証する。</summary>
    [Fact]
    internal void RoundRect_角を落とす()
    {
        using var target = new RenderTarget(16, 16);

        SkiaFill.RoundRect(target.Canvas, new EsRect(1, 1, 13, 13), cornerRadius: 4, s_white);

        // 角（半径 4 では塗られない）
        Assert.Equal(0, target.GetPixel(1, 1).Alpha);
        // 中央は塗られる
        Assert.Equal(new SKColor(255, 255, 255), target.GetPixel(7, 7));
    }

    /// <summary>描画先が null なら例外になることを検証する。</summary>
    [Fact]
    internal void 描画先がnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(() => SkiaFill.Rect(null!, new EsRect(0, 0, 1, 1), s_white));
        Assert.Throws<ArgumentNullException>(() => SkiaFill.Rects(null!, [new EsRect(0, 0, 1, 1)], s_white));
        Assert.Throws<ArgumentNullException>(() => SkiaFill.RoundRect(null!, new EsRect(0, 0, 1, 1), 0, s_white));
    }

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
