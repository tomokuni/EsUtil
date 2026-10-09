using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Marquee.MewUI.Internal;
using SkiaSharp;
using Xunit;

namespace EsUtil.Feature.Marquee.MewUI.Tests.Internal;

/// <summary><see cref="SkiaText"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// フォント解決・実寸測定・描画は MewUI / Avalonia の描画層とホストが共用するため、<br/>
/// 「非 ASCII は字形を持つフォントへフォールバックすること」「実寸が正の値になること」<br/>
/// 「描画が指定の位置へ届くこと」を検証する。
/// </remarks>
public class SkiaTextTests
{
    /// <summary>テスト用の描画先（透明で初期化したビットマップとキャンバス）。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成した描画先。 破棄は呼び出し側。</returns>
    private static (SKBitmap Bitmap, SKCanvas Canvas) CreateTarget(int width, int height)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        bitmap.Erase(SKColors.Transparent);
        return (bitmap, new SKCanvas(bitmap));
    }

    // ====================================================================
    // ResolveTypeface
    // ====================================================================

    /// <summary>空文字は ASCII のみとみなし、既定のフォントを使うことを検証する。</summary>
    [Fact]
    internal void ResolveTypeface_空文字なら既定のフォントを使う()
    {
        Assert.Same(SKTypeface.Default, SkiaText.ResolveTypeface(""));
    }

    /// <summary>ASCII のみの文字列は既定のフォントを使うことを検証する。</summary>
    /// <param name="text">判定する文字列。</param>
    [Theory]
    [InlineData("ABC 123 (100%)")]
    [InlineData("ABC\u007F")]
    internal void ResolveTypeface_ASCIIのみなら既定のフォントを使う(string text)
    {
        Assert.Same(SKTypeface.Default, SkiaText.ResolveTypeface(text));
    }

    /// <summary>非 ASCII を含む文字列は字形を持つフォントへフォールバックすることを検証する。</summary>
    [Fact]
    internal void ResolveTypeface_日本語なら字形を持つフォントへフォールバックする()
    {
        // CJK のフォントが無い環境では既定のフォントへ戻る（描画結果は豆腐になる）
        Assert.SkipUnless(
            SKFontManager.Default.MatchCharacter('あ') is not null, "日本語のフォントがインストールされていない");

        var typeface = SkiaText.ResolveTypeface("確定");

        Assert.NotSame(SKTypeface.Default, typeface);
        Assert.NotEqual(0, typeface.GetGlyph('確'));
        Assert.NotEqual(0, typeface.GetGlyph('定'));
    }

    /// <summary>非 ASCII を含む文字列（全角記号）もフォールバックの対象になることを検証する。</summary>
    [Fact]
    internal void ResolveTypeface_全角記号でもフォールバックする()
    {
        // U+FF08（全角括弧）は 0x7F 超のため ASCII のみの判定から外れる
        Assert.SkipUnless(
            SKFontManager.Default.MatchCharacter('あ') is not null, "日本語のフォントがインストールされていない");

        Assert.NotSame(SKTypeface.Default, SkiaText.ResolveTypeface("（10, 20）"));
    }

    // ====================================================================
    // Measure
    // ====================================================================

    /// <summary>実寸が正の幅・行高になることを検証する（行高はフォントの Descent - Ascent）。</summary>
    /// <param name="text">測定する文字列。</param>
    [Theory]
    [InlineData("ABC")]
    [InlineData("(123, 456)")]
    [InlineData("確定: Enter")]
    internal void Measure_正の実寸を返す(string text)
    {
        var size = SkiaText.Measure(text, fontSize: 14);

        Assert.True(size.Width > 0, "幅が正の値になっていない");
        Assert.True(size.Height > 0, "行高が正の値になっていない");
    }

    /// <summary>長い文字列ほど幅が大きくなることを検証する。</summary>
    [Fact]
    internal void Measure_長い文字列ほど幅が大きい()
    {
        var shortSize = SkiaText.Measure("ABC", fontSize: 14);
        var longSize = SkiaText.Measure("ABCABCABC", fontSize: 14);

        Assert.True(longSize.Width > shortSize.Width);
    }

    /// <summary>フォントサイズに比例して行高が大きくなることを検証する。</summary>
    [Fact]
    internal void Measure_フォントサイズが大きいほど行高が大きい()
    {
        var small = SkiaText.Measure("ABC", fontSize: 12);
        var large = SkiaText.Measure("ABC", fontSize: 24);

        Assert.True(large.Height > small.Height);
        Assert.True(large.Width > small.Width);
    }

    // ====================================================================
    // Draw
    // ====================================================================

    /// <summary>指定した左上原点の位置へ文字が描画されることを検証する。</summary>
    [Fact]
    internal void Draw_左上原点へ文字を描画する()
    {
        var (bitmap, canvas) = CreateTarget(80, 40);
        using (bitmap)
        using (canvas)
        {
            SkiaText.Draw(canvas, "ABC", left: 4, top: 4, fontSize: 14, DrawingColor.White);

            // 領域内に文字が現れる（アンチエイリアスを含むため完全一致ではなく存在のみを確認する）
            Assert.True(CountNonTransparent(bitmap, 0, 0, 80, 40) > 0, "文字が描画されていない");
            // 原点より左・上の余白には描画されない
            Assert.Equal(0, CountNonTransparent(bitmap, 0, 0, 4, 4));
        }
    }

    /// <summary>日本語の文字列も例外なく描画することを検証する。</summary>
    /// <remarks>フォントにグリフが無い環境では豆腐になる（描画自体は完了する）。</remarks>
    [Fact]
    internal void Draw_日本語を描画する()
    {
        var (bitmap, canvas) = CreateTarget(120, 40);
        using (bitmap)
        using (canvas)
        {
            SkiaText.Draw(canvas, "確定", left: 0, top: 0, fontSize: 14, DrawingColor.White);

            Assert.True(CountNonTransparent(bitmap, 0, 0, 120, 40) > 0, "文字が描画されていない");
        }
    }

    /// <summary>キャンバスが null の場合は例外になることを検証する。</summary>
    [Fact]
    internal void Draw_キャンバスがnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(() => SkiaText.Draw(null!, "ABC", 0, 0, 14, DrawingColor.White));
    }

    /// <summary>指定領域で透明でない画素の数を数える。</summary>
    /// <param name="bitmap">対象のビットマップ。</param>
    /// <param name="x0">左上 X 座標。</param>
    /// <param name="y0">左上 Y 座標。</param>
    /// <param name="x1">右下 X 座標（含まない）。</param>
    /// <param name="y1">右下 Y 座標（含まない）。</param>
    /// <returns>透明でない画素数。</returns>
    private static int CountNonTransparent(SKBitmap bitmap, int x0, int y0, int x1, int y1)
    {
        var count = 0;

        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha != 0)
                {
                    count++;
                }
            }
        }

        return count;
    }
}
