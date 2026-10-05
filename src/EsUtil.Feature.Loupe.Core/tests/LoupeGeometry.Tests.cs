using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Feature.Loupe.Tests;

/// <summary><see cref="LoupeGeometry"/> の解決（UI 非依存・並列実行）のテスト。</summary>
/// <remarks>
/// 表示サイズ（DIP）からの物理ピクセルサイズの解決と切り出し領域の導出のみを検証するため、<br/>
/// UI 初期化に依存せず <c>[Fact]</c> で並列実行する（両 UI が同じ手順で描画の幾何を組み立てることを保証する）。
/// </remarks>
public class LoupeGeometryTests
{
    // ====================================================================
    // 表示サイズ（DIP）→ 物理ピクセル
    // ====================================================================

    /// <summary>表示サイズが物理ピクセルへ換算されることを検証する。</summary>
    /// <param name="displaySize">表示サイズ（DIP）。</param>
    /// <param name="dipToDeviceScaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <param name="expectedPixelSize">期待する物理ピクセルサイズ。</param>
    [Theory]
    [InlineData(160, 1.0, 160)]
    [InlineData(160, 1.25, 200)]
    [InlineData(160, 2.0, 320)]
    [InlineData(0.4, 1.0, 1)]
    internal void ResolveGeometry_表示サイズを物理ピクセルへ換算する(
        double displaySize, double dipToDeviceScaling, int expectedPixelSize)
        => Assert.Equal(expectedPixelSize, Resolve(displaySize, dipToDeviceScaling).ClientPixelSize);

    /// <summary>描画先の大きさに関わらず表示サイズを縮小しないことを検証する。</summary>
    [Fact]
    internal void ResolveGeometry_描画先の大きさで縮小しない()
        => Assert.Equal(640, Resolve(640, 1.0).ClientPixelSize);

    /// <summary>換算結果の下限が 1 になることを検証する。</summary>
    /// <param name="size">表示サイズ（DIP）。</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    internal void ResolveGeometry_最低1になる(double size)
        => Assert.Equal(1, Resolve(size, 1.0).ClientPixelSize);

    /// <summary>表示サイズとスケーリングから物理ピクセルを求めることを検証する。</summary>
    /// <param name="size">表示サイズ（DIP）。</param>
    /// <param name="scaling">スケーリング係数。</param>
    /// <param name="expected">期待する物理ピクセルサイズ。</param>
    [Theory]
    [InlineData(100, 1.0, 100)]
    [InlineData(100, 1.5, 150)]
    [InlineData(33, 1.1, 37)]
    [InlineData(160.4, 1.0, 161)]
    internal void ResolveGeometry_表示サイズとスケーリングから物理ピクセルを求める(double size, double scaling, int expected)
        => Assert.Equal(expected, Resolve(size, scaling).ClientPixelSize);

    /// <summary>描画先の全領域が原点 0 の正方形になることを検証する。</summary>
    [Fact]
    internal void ClientPixelRect_物理ピクセルサイズの正方形を原点0で返す()
    {
        var geometry = Resolve(100, 1.25);

        // 描画層が下地を全面へ塗るための矩形である（UI 側で組み立てない）
        Assert.Equal(125, geometry.ClientPixelSize);
        Assert.Equal(new EsRect(0, 0, 125, 125), geometry.ClientPixelRect);
    }

    /// <summary>中心・拡大率・スケーリングがそのまま保持されることを検証する。</summary>
    [Fact]
    internal void ResolveGeometry_入力値をそのまま保持する()
    {
        var loupe = new LoupeCore(LoupeOptions.Default with { ZoomLevel = 8 })
        {
            CenterPoint = new EsPoint(3, 4),
            ClientToPixelScaling = 1.5,
        };

        var geometry = loupe.ResolveGeometry(100);

        Assert.Equal(new EsPoint(3, 4), geometry.Center);
        Assert.Equal(8, geometry.ZoomLevel);
        Assert.Equal(1.5, geometry.ClientToPixelScaling);
    }

    /// <summary>中心が未設定（null）でも解決できることを検証する。</summary>
    [Fact]
    internal void ResolveGeometry_中心未設定でも解決できる()
        => Assert.Null(Resolve(100, 1.0).Center);

    // ====================================================================
    // 切り出し領域の導出（CaptureSize / CaptureTopLeft / MagnifiedImage）
    // ====================================================================

    /// <summary>切り出し領域の一辺が対象ピクセルの切り上げで求まることを検証する。</summary>
    /// <param name="clientPixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="zoomLevel">拡大率。</param>
    /// <param name="expected">期待する切り出し領域の一辺（対象ピクセル）。</param>
    /// <remarks>
    /// 切り出しの左上是効原点を切り捨てた整数であるため、可視域の右端・下端まで覆うには切り上げに加えて 1 画素必要になる<br/>
    /// （可視域に 1 画素足りないと端に下地の色が見える）。
    /// </remarks>
    [Theory]
    [InlineData(100, 4, 26)]
    [InlineData(100, 3, 35)]
    [InlineData(64, 4, 17)]
    [InlineData(10, 64, 2)]
    [InlineData(16, 4, 5)]
    [InlineData(25, 4, 8)]
    [InlineData(1, 1, 2)]
    internal void CaptureSize_可視域を覆う一辺を返す(int clientPixelSize, int zoomLevel, int expected)
        => Assert.Equal(expected, Geometry(s_center, clientPixelSize, zoomLevel).CaptureSize);

    /// <summary>可視域の左上（実効原点）が「中心 − 可視域の半径」になることを検証する。</summary>
    /// <param name="center">拡大中心。</param>
    /// <param name="clientPixelSize">ルーペの一辺（物理ピクセル）。</param>
    /// <param name="zoomLevel">拡大率。</param>
    /// <param name="expectedLeft">期待する左上の X 座標。</param>
    /// <remarks>
    /// 可視域の半径は「物理ピクセルサイズ ÷ 拡大率 ÷ 2」であり、小数を含む<br/>
    /// （拡大中心が描画面の中心へ写るように決めるため。 切り出しの原点は <c>CaptureCropTopLeft</c> が別途切り捨てる）。
    /// </remarks>
    [Theory]
    [InlineData(50.5, 16, 4, 48.5)]     // 半径 2 → 48.5
    [InlineData(50, 16, 4, 48)]         // 半径 2（整数）
    [InlineData(-0.5, 16, 4, -2.5)]     // 負の座標でも同じ式
    [InlineData(101, 625, 25, 88.5)]    // 半径 12.5
    internal void CaptureTopLeft_中心から可視域の半径だけ左上へずらす(
        double center, int clientPixelSize, int zoomLevel, double expectedLeft)
    {
        var geometry = Geometry(new EsPoint(center, center), clientPixelSize, zoomLevel);

        Assert.Equal(expectedLeft, geometry.CaptureTopLeft!.Value.X, 6);
    }

    /// <summary>切り出しの原点が実効原点の切り捨てになることを検証する。</summary>
    /// <param name="center">拡大中心。</param>
    /// <param name="expectedLeft">期待する切り出しの左上。</param>
    /// <remarks>切り上げると可視域の左端・上端が 1 画素欠ける（差は <c>CaptureDrawOffset</c> が埋める）。</remarks>
    [Theory]
    [InlineData(50.5, 48)]      // 実効原点 48.5 → 48
    [InlineData(50, 48)]        // 実効原点 48（整数）
    [InlineData(-0.5, -3)]      // 実効原点 −2.5 → −3
    internal void CaptureCropTopLeft_実効原点を切り捨てる(double center, int expectedLeft)
    {
        var geometry = Geometry(new EsPoint(center, center), 16, 4);

        Assert.Equal(expectedLeft, geometry.CaptureCropTopLeft!.Value.X);
    }

    /// <summary>切り出しの配置位置が実効原点との差を物理ピクセルで埋めることを検証する。</summary>
    /// <param name="center">拡大中心。</param>
    /// <param name="expectedOffset">期待する配置位置（物理ピクセル）。</param>
    /// <remarks>描画の位置は整数のまま保たれる（小数へ置くと拡大した画素がぼやける）。 丸めの残りは最大 0.5 px。</remarks>
    [Theory]
    [InlineData(50.5, -2)]      // (48 − 48.5) × 4 = −2
    [InlineData(50.4, -2)]      // (48 − 48.4) × 4 = −1.6 → 0 から遠い方へ丸めて −2
    [InlineData(50, 0)]         // 差なし
    internal void CaptureDrawOffset_実効原点との差を物理ピクセルで持つ(double center, int expectedOffset)
    {
        var geometry = Geometry(new EsPoint(center, center), 16, 4);

        Assert.Equal(expectedOffset, geometry.CaptureDrawOffset!.Value.X);
    }

    /// <summary>可視域内の中心位置が可視域の半径そのものになることを検証する。</summary>
    /// <remarks>十字線の位置の根拠であり、拡大中心が小数でも値が変わらない（十字線が描画面の中心から動かない）。</remarks>
    [Theory]
    [InlineData(50.5, 2.0)]
    [InlineData(50, 2.0)]
    [InlineData(-0.5, 2.0)]
    internal void CaptureCenterOffset_可視域の半径を返す(double center, double expected)
    {
        var geometry = Geometry(new EsPoint(center, center), 16, 4);

        Assert.Equal(expected, geometry.CaptureCenterOffset.X, 6);
    }

    /// <summary>拡大中心が未設定の場合は切り出し領域の導出値が null（描画しない）になることを検証する。</summary>
    [Fact]
    internal void CaptureTopLeft_中心未設定ならnullになる()
    {
        var geometry = Geometry(center: null, 32, 4);

        Assert.Null(geometry.CaptureTopLeft);
        Assert.Null(geometry.CaptureCropTopLeft);
        Assert.Null(geometry.CaptureDrawOffset);
        Assert.Null(geometry.MagnifiedImage);
        // 可視域内の中心位置は中心に依存しない（幾何だけから決まる）
        Assert.Equal(4.0, geometry.CaptureCenterOffset.X, 6);
    }

    /// <summary>拡大画像の切り出し・描画元・描画先の矩形が求まることを検証する。</summary>
    /// <remarks>
    /// 中心 (10, 10)・clientPixelSize=16・zoom=4 → 可視域の半径 2、実効原点 (8, 8)（整数）、切り出し一辺 5（= ceil(4) + 1）、
    /// 切り出し原点 (8, 8)、配置位置 0、描画先 20 px（表面 16 px を越えた分はクリップされる）。
    /// </remarks>
    [Fact]
    internal void MagnifiedImage_切り出し領域と描画元と描画先を解決する()
    {
        var image = Assert.NotNull(Geometry(new EsPoint(10, 10), 16, 4).MagnifiedImage);

        Assert.Equal(new EsRectInt(8, 8, 5, 5), image.CropSourceRect);
        Assert.Equal(new EsRect(0, 0, 5, 5), image.CroppedRect);
        Assert.Equal(new EsRect(0, 0, 20, 20), image.DrawClientPixelRect);
    }

    /// <summary>中心が小数の場合は切り出しと描画の位置で実効原点の差を埋めることを検証する。</summary>
    /// <remarks>切り出しは切り捨てた整数から始め、描画の位置を負へずらして見た目を連続にする（画素はぼやけない）。</remarks>
    [Fact]
    internal void MagnifiedImage_中心が小数の場合は切り出しと描画で差を埋める()
    {
        // 中心 10.5・半径 2 → 実効原点 8.5、切り出し原点 8、配置位置 (8 − 8.5) × 4 = −2
        var image = Assert.NotNull(Geometry(new EsPoint(10.5, 10.5), 16, 4).MagnifiedImage);

        Assert.Equal(new EsRectInt(8, 8, 5, 5), image.CropSourceRect);
        Assert.Equal(new EsRect(-2, -2, 20, 20), image.DrawClientPixelRect);
    }

    /// <summary>画像端では切り出し領域が負の左上になることを検証する（透明パディングは切り出し側が担う）。</summary>
    [Fact]
    internal void MagnifiedImage_画像端では切り出し左上が負になる()
    {
        var image = Assert.NotNull(Geometry(new EsPoint(0, 0), 16, 4).MagnifiedImage);

        // 実効原点 (2 ÷ 2 = 2) → 切り出し原点 −2、切り出し一辺 5（= ceil(4) + 1）
        Assert.Equal(new EsRectInt(-2, -2, 5, 5), image.CropSourceRect);
    }

    /// <summary>描画先が切り出しサイズ × 拡大率になることを検証する（可視域より大きくなった分はクリップされる）。</summary>
    /// <param name="clientPixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="zoomLevel">拡大率。</param>
    /// <param name="expectedDestination">期待する描画先の一辺。</param>
    /// <remarks>切り出しの一辺は <c>ceil(物理ピクセル ÷ 拡大率) + 1</c> のため、描画先もその拡大率倍になる。</remarks>
    [Theory]
    [InlineData(16, 4, 20)]
    [InlineData(25, 4, 32)]
    [InlineData(1, 1, 2)]
    internal void MagnifiedImage_描画先は切り出しサイズの拡大率倍になる(
        int clientPixelSize, int zoomLevel, int expectedDestination)
    {
        var image = Assert.NotNull(Geometry(new EsPoint(100, 100), clientPixelSize, zoomLevel).MagnifiedImage);

        Assert.Equal(expectedDestination, image.DrawClientPixelRect.Width);
    }

    // ====================================================================
    // ヘルパー
    // ====================================================================

    /// <summary>拡大中心（描画先の中央）。</summary>
    private static readonly EsPoint s_center = new(50, 50);

    /// <summary>表示サイズ（DIP）とスケーリングから描画の幾何を解決する。</summary>
    /// <param name="displaySize">表示サイズ（DIP）。</param>
    /// <param name="dipToDeviceScaling">スケーリング係数。</param>
    /// <returns>描画の幾何。</returns>
    private static LoupeGeometry Resolve(double displaySize, double dipToDeviceScaling)
    {
        var loupe = new LoupeCore(LoupeOptions.Default) { ClientToPixelScaling = dipToDeviceScaling };

        return loupe.ResolveGeometry(displaySize);
    }

    /// <summary>テスト用の描画の幾何を生成する（スケーリング 1.0・表示サイズ＝物理ピクセル）。</summary>
    /// <param name="center">拡大中心（対象座標）。 未設定は null。</param>
    /// <param name="clientPixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="zoomLevel">拡大率。</param>
    /// <returns>描画の幾何。</returns>
    private static LoupeGeometry Geometry(EsPoint? center, int clientPixelSize, int zoomLevel)
        // 切り出し領域の一辺は LoupeGeometry が導出する（ClientPixelSize >= 1 のため 1 以上になる）
        => new(center, clientPixelSize, clientPixelSize, 1.0, zoomLevel);
}
