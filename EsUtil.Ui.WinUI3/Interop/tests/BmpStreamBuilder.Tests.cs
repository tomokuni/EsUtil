using EsUtil.Ui.WinUI3.Interop.Drawing;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinUI3.Interop.Tests;

/// <summary><see cref="BmpStreamBuilder"/> の単体テスト（BMP のヘッダーと画素の並び）。</summary>
/// <remarks>
/// Composition の <c>LoadedImageSurface</c> へ渡す形式を固定する。 ヘッダーの値がずれると画像が表示されないため、<br/>
/// 各フィールドの値を実測ではなく仕様（BITMAPFILEHEADER / BITMAPINFOHEADER）に照らして検証する。
/// </remarks>
public class BmpStreamBuilderTests
{
    [Fact]
    internal void Build_ヘッダーがBMPの仕様に一致する()
    {
        var pixels = new byte[4 * 4 * 3];
        var data = BmpStreamBuilder.Build(4, 3, pixels);

        Assert.Equal(0x4D42, BitConverter.ToUInt16(data, 0));      // "BM"
        Assert.Equal(data.Length, BitConverter.ToInt32(data, 2));
        Assert.Equal(54, BitConverter.ToInt32(data, 10));          // 画素の開始位置
        Assert.Equal(40, BitConverter.ToInt32(data, 14));          // BITMAPINFOHEADER
        Assert.Equal(4, BitConverter.ToInt32(data, 18));           // 幅
        Assert.Equal(-3, BitConverter.ToInt32(data, 22));          // 高さ（負 = 上から下）
        Assert.Equal(1, BitConverter.ToUInt16(data, 26));          // プレーン数
        Assert.Equal(32, BitConverter.ToUInt16(data, 28));         // ビット数
        Assert.Equal(0, BitConverter.ToInt32(data, 30));           // 圧縮なし
        Assert.Equal(pixels.Length, BitConverter.ToInt32(data, 34));
    }

    [Fact]
    internal void Build_画素が上から下の順で並ぶ()
    {
        // 2x2 の画素（BGRA）。 左上と右下で区別できる値にする
        byte[] pixels =
        [
            1, 2, 3, 255, 4, 5, 6, 255,
            7, 8, 9, 255, 10, 11, 12, 255,
        ];

        var data = BmpStreamBuilder.Build(2, 2, pixels);

        // ヘッダーの直後が最初の行（左上）になる
        Assert.Equal(1, data[54]);
        Assert.Equal(10, data[54 + 12]);
    }

    [Fact]
    internal void Build_画素の長さが寸法と合わない場合は例外()
    {
        Assert.Throws<ArgumentException>(() => BmpStreamBuilder.Build(2, 2, new byte[4]));
    }

    [Fact]
    internal void Build_寸法が0以下の場合は例外()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BmpStreamBuilder.Build(0, 1, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => BmpStreamBuilder.Build(1, 0, []));
    }

    [Fact]
    internal void Build_画像から組み立てられる()
    {
        using var image = TestHelper.CreateBitmap(4, 2, DrawingColor.Red);

        var data = BmpStreamBuilder.Build(image);

        // 4x2 の BGRA = 32 バイト + 54 バイトのヘッダー
        Assert.Equal(54 + 32, data.Length);
        Assert.Equal(4, BitConverter.ToInt32(data, 18));
        Assert.Equal(-2, BitConverter.ToInt32(data, 22));
    }
}
