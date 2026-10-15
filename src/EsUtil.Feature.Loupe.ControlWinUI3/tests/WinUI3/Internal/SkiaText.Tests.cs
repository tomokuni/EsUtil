using EsUtil.Feature.Loupe.WinUI3.Internal;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Loupe.WinUI3.Tests.Internal;

/// <summary><see cref="SkiaText"/>（Skia のテキスト描画）のテスト。</summary>
/// <remarks>
/// 中心座標ラベルは本クラスで実寸を測り、同じ関数で文字を描く（背景と文字の大きさが一致する前提）ため、<br/>
/// 測定（幅・行高）と描画（左上原点・フォント フォールバック）の契約を検証する。
/// </remarks>
public class SkiaTextTests
{
    /// <summary>文字色（白・不透明）。</summary>
    private static readonly DrawingColor s_white = DrawingColor.FromArgb(255, 255, 255, 255);

    /// <summary>空文字は幅 0 として測られることを検証する。</summary>
    /// <remarks>行高はフォントの高さ（Descent − Ascent）として正の値になる（背景の高さの算出元）。</remarks>
    [Fact]
    internal void Measure_空文字は幅0で行高は正()
    {
        var result = SkiaText.Measure(string.Empty, 12);

        Assert.Equal(0, result.Width);
        Assert.True(result.Height > 0);
    }

    /// <summary>長い文字列ほど幅が広く測られることを検証する。</summary>
    [Fact]
    internal void Measure_文字数に応じて幅が広がる()
    {
        var shortText = SkiaText.Measure("(1, 2)", 12);
        var longText = SkiaText.Measure("(1234, 5678)", 12);

        Assert.True(longText.Width > shortText.Width);
    }

    /// <summary>フォントサイズに応じて幅と行高が広がることを検証する。</summary>
    [Fact]
    internal void Measure_フォントサイズに比例する()
    {
        var small = SkiaText.Measure("(12, 34)", 12);
        var large = SkiaText.Measure("(12, 34)", 24);

        Assert.True(large.Width > small.Width);
        Assert.True(large.Height > small.Height);
    }

    /// <summary>ASCII のみのテキストは既定のフォントで描かれることを検証する。</summary>
    [Fact]
    internal void ResolveTypeface_ASCIIのみは既定のフォント()
    {
        var result = SkiaText.ResolveTypeface("(12, 34)");

        Assert.Same(SKTypeface.Default, result);
    }

    /// <summary>非 ASCII を含むテキストでも字形を持つフォントが解決されることを検証する。</summary>
    /// <remarks>
    /// 既定のフォントが日本語のグリフを持たない環境では豆腐（□）になるため、<br/>
    /// 本メソッドは字形を持つフォントを探す（見つからない場合も null ではなく既定のフォントを返す）。
    /// </remarks>
    [Fact]
    internal void ResolveTypeface_非ASCIIは字形を持つフォントを返す()
    {
        var result = SkiaText.ResolveTypeface("あ");

        Assert.NotNull(result);
    }

    /// <summary>null のテキストは例外になることを検証する。</summary>
    [Fact]
    internal void ResolveTypeface_nullは例外()
    {
        Assert.Throws<ArgumentNullException>(() => SkiaText.ResolveTypeface(null!));
    }

    /// <summary>左上を原点として文字が描かれることを検証する。</summary>
    /// <remarks>
    /// Skia の描画はベースライン指定のため、上端から文字が現れ、上端より上には描かれないことを確かめる。
    /// </remarks>
    [Fact]
    internal void Draw_左上原点で描く()
    {
        using var target = new RenderTarget(64, 64);

        SkiaText.Draw(target.Canvas, "W", left: 4, top: 4, fontSize: 24, s_white);

        // 文字が現れる（位置や字形に依存しない検証として画素数で確かめる）
        Assert.True(target.CountPixels(new SKColor(255, 255, 255)) > 0);
        // 上端より上には描かれない
        Assert.Equal(0, target.GetPixel(4, 3).Alpha);
    }

    /// <summary>描画先が null なら例外になることを検証する。</summary>
    [Fact]
    internal void Draw_描画先がnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(
            () => SkiaText.Draw(null!, "A", 0, 0, 12, s_white));
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

        /// <summary>指定の色の画素の数を数える。</summary>
        /// <param name="color">数える色。</param>
        /// <returns>一致した画素の数。</returns>
        public int CountPixels(SKColor color)
        {
            var count = 0;
            for (var y = 0; y < _bitmap.Height; y++)
            {
                for (var x = 0; x < _bitmap.Width; x++)
                {
                    if (_bitmap.GetPixel(x, y) == color)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        /// <summary>キャンバスとビットマップを破棄する。</summary>
        public void Dispose()
        {
            Canvas.Dispose();
            _bitmap.Dispose();
        }
    }
}
