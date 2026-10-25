using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Ui.WinUI3.Loupe.Internal;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinUI3.Loupe.Tests;

/// <summary><see cref="LoupeImageSampler"/> の画素テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// ルーペの拡大画像は「切り出し領域を最近傍で面へ写す」規則だけを持つ。 本テストは
/// <b>実際の画素</b>を比較して、写す位置・倍率・外側の扱いを固定する（ウィンドウ不要）。
/// </remarks>
public class LoupeImageSamplerTests
{
    /// <summary>拡大元の色（赤）。</summary>
    private static readonly DrawingColor s_red = DrawingColor.FromArgb(255, 255, 0, 0);

    /// <summary>面の一辺（画素）。</summary>
    private const int SurfaceSize = 8;

    /// <summary>切り出した 1 画素を面全体へ引き伸ばす。</summary>
    [Fact]
    internal void Sample_切り出した1画素を面全体へ引き伸ばす()
    {
        using var source = TestHelper.CreateBitmap(4, s_red);
        var destination = new byte[SurfaceSize * SurfaceSize * LoupeImageSampler.BytesPerPixel];

        LoupeImageSampler.Sample(
            source, new EsRectInt(1, 1, 1, 1), new EsRect(0, 0, SurfaceSize, SurfaceSize), SurfaceSize, destination);

        for (var i = 0; i < destination.Length; i += LoupeImageSampler.BytesPerPixel)
        {
            Assert.Equal(0, destination[i]);       // B
            Assert.Equal(0, destination[i + 1]);   // G
            Assert.Equal(255, destination[i + 2]); // R
            Assert.Equal(255, destination[i + 3]); // A
        }
    }

    /// <summary>描画先の矩形の外は透明のまま（下地が見える）。</summary>
    [Fact]
    internal void Sample_描画先の外は透明のまま()
    {
        using var source = TestHelper.CreateBitmap(4, s_red);
        var destination = new byte[SurfaceSize * SurfaceSize * LoupeImageSampler.BytesPerPixel];

        // 面の左上 4×4 にだけ描く
        LoupeImageSampler.Sample(
            source, new EsRectInt(0, 0, 2, 2), new EsRect(0, 0, 4, 4), SurfaceSize, destination);

        Assert.Equal(255, destination[((SurfaceSize * 1) + 1) * 4 + 3]);   // 内側（不透明）
        Assert.Equal(0, destination[((SurfaceSize * 1) + 6) * 4 + 3]);     // 外側（透明）
        Assert.Equal(0, destination[((SurfaceSize * 6) + 1) * 4 + 3]);     // 外側（透明）
    }

    /// <summary>拡大元の外へ出る部分は書かない（端の外は下地が見える）。</summary>
    [Fact]
    internal void Sample_拡大元の外は書かない()
    {
        using var source = TestHelper.CreateBitmap(4, s_red);
        var destination = new byte[SurfaceSize * SurfaceSize * LoupeImageSampler.BytesPerPixel];

        // 画像の右下 2×2 を面上の 4×4 へ写す（画像の外も含む）
        LoupeImageSampler.Sample(
            source, new EsRectInt(2, 2, 4, 4), new EsRect(0, 0, 4, 4), SurfaceSize, destination);

        Assert.Equal(255, destination[3]);                                  // (0,0) は画像の内側
        Assert.Equal(0, destination[((SurfaceSize * 3) + 3) * 4 + 3]);      // (3,3) は画像の外（透明）
    }

    /// <summary>面の寸法と書き込み先の長さが一致しない場合は例外。</summary>
    [Fact]
    internal void Sample_長さが合わない場合は例外()
    {
        using var source = TestHelper.CreateBitmap(4, s_red);

        Assert.Throws<ArgumentException>(() => LoupeImageSampler.Sample(
            source, new EsRectInt(0, 0, 4, 4), new EsRect(0, 0, SurfaceSize, SurfaceSize), SurfaceSize, new byte[4]));
    }

    /// <summary>破棄済みの画像は例外。</summary>
    [Fact]
    internal void Sample_破棄済みの画像は例外()
    {
        var source = TestHelper.CreateBitmap(4, s_red);
        source.Dispose();

        Assert.Throws<ObjectDisposedException>(() => LoupeImageSampler.Sample(
            source,
            new EsRectInt(0, 0, 4, 4),
            new EsRect(0, 0, SurfaceSize, SurfaceSize),
            SurfaceSize,
            new byte[SurfaceSize * SurfaceSize * LoupeImageSampler.BytesPerPixel]));
    }

    /// <summary>最近傍で写す（拡大しても色を混ぜない）。</summary>
    [Fact]
    internal void Sample_最近傍で写す()
    {
        // 2×1 の画像（左が赤・右が青）を作る
        using var created = EsBitmap.Create(2, 1, s_red);
        created.SetPixel(1, 0, 255, 0, 0, 255);
        created.SetReadOnly();
        using var source = EsReadOnlyBitmap.FromBitmap(created);

        var destination = new byte[SurfaceSize * SurfaceSize * LoupeImageSampler.BytesPerPixel];

        // 2×1 を面の上半分へ引き伸ばす（1 画素 = 4 面画素）
        LoupeImageSampler.Sample(
            source, new EsRectInt(0, 0, 2, 1), new EsRect(0, 0, SurfaceSize, 4), SurfaceSize, destination);

        Assert.Equal(255, destination[2]);                                   // 左端は赤
        Assert.Equal(255, destination[3]);                                   // 左端は赤
        Assert.Equal(255, destination[(6 * 4) + 0]);                         // 右端は青（B）
        Assert.Equal(0, destination[(6 * 4) + 2]);                           // 右端は青（R は 0）
    }
}
