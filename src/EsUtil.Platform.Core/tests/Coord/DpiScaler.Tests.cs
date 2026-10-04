using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Coord;

/// <summary><see cref="DpiScaler"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// DIP 値 → 物理ピクセル変換のみを検証するため、<br/>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// <c>表示サイズ・市松模様のサイズ・外枠の太さ</c>の物理ピクセル換算と、ウィンドウの位置・サイズの換算で共用する。
/// </remarks>
public class DpiScalerTests
{
    [Fact]
    internal void ScaleToPixel_スケーリングで端数は切り上げる()
    {
        // 100 DIP × 1.5 = 150（切り上げ不要）→ 150
        Assert.Equal(150, DpiScaler.ScaleToPixel(100, 1.5));
    }

    [Fact]
    internal void ScaleToPixel_浮動小数誤差で誤切り上げしない()
    {
        // 100 × 1.1 = 110.00000000000001（浮動小数誤差）→ 誤らず 110
        Assert.Equal(110, DpiScaler.ScaleToPixel(100, 1.1));
    }

    [Fact]
    internal void ScaleToPixel_端数を切り上げて物理ピクセルへ変換する()
    {
        // 33 × 1.1 = 36.3 → 37（切り上げ）
        Assert.Equal(37, DpiScaler.ScaleToPixel(33, 1.1));
    }

    [Fact]
    internal void ScaleToPixel_0以下の値は1に正規化する()
    {
        // 0 × 1.0 = 0 → 最低 1
        Assert.Equal(1, DpiScaler.ScaleToPixel(0, 1.0));
        Assert.Equal(1, DpiScaler.ScaleToPixel(0.0, 1.0));
    }

    [Fact]
    internal void ScaleToPixel_浮動小数のDIP値も正しく変換する()
    {
        // 160.4 × 1.0 = 160.4 → 161（切り上げ）
        Assert.Equal(161, DpiScaler.ScaleToPixel(160.4, 1.0));
    }

    [Fact]
    internal void NormalizeScaling_無効な値は基準値へ正規化する()
    {
        // 描画先のスケーリングが未確定（無効な値）のまま使うと描画が消えるため、基準値（1.0）へ寄せる
        Assert.Equal(DpiScaler.StandardScaling, DpiScaler.NormalizeScaling(0));
        Assert.Equal(DpiScaler.StandardScaling, DpiScaler.NormalizeScaling(-1.5));
        Assert.Equal(DpiScaler.StandardScaling, DpiScaler.NormalizeScaling(double.NaN));
        Assert.Equal(DpiScaler.StandardScaling, DpiScaler.NormalizeScaling(double.PositiveInfinity));
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(2.0)]
    internal void NormalizeScaling_正の値はそのまま返す(double scaling)
    {
        Assert.Equal(scaling, DpiScaler.NormalizeScaling(scaling));
    }

    [Fact]
    internal void StandardScaling_はDIPと物理ピクセルが1対1になる値である()
    {
        Assert.Equal(1.0, DpiScaler.StandardScaling);
        Assert.Equal(DpiScaler.StandardScaling, DpiScaler.ToScaling(DpiScaler.StandardDpi));
    }

    [Theory]
    [InlineData(0.5, true)]
    [InlineData(1.0, true)]
    [InlineData(2.0, true)]
    [InlineData(0.0, false)]
    [InlineData(-1.0, false)]
    [InlineData(double.NaN, false)]
    [InlineData(double.PositiveInfinity, false)]
    internal void IsValidScaling_有限の正の値のみ有効と判定する(double scaling, bool expected)
    {
        // 非数（NaN・無限大）を有効とすると、乗算で座標が整数の上限へ溢れてウィンドウが壊れる
        Assert.Equal(expected, DpiScaler.IsValidScaling(scaling));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-2.0)]
    [InlineData(1.25)]
    [InlineData(2.0)]
    internal void IsValidScaling_正規化のしきい値と一致する(double scaling)
    {
        // 「0 以下は未確定」の判定が正規化と同一であることを保証する（規則の二重所有を防ぐ）
        // 有効な係数は正規化しても値が変わらない（= そのまま使われる）
        Assert.Equal(DpiScaler.IsValidScaling(scaling), DpiScaler.NormalizeScaling(scaling) == scaling);
    }

    [Fact]
    internal void ToDip_物理ピクセルの長さをDIPへ換算する()
    {
        Assert.Equal(100.0, DpiScaler.ToDip(125, 1.25));
        Assert.Equal(960.0, DpiScaler.ToDip(1200, 1.25));
    }

    [Fact]
    internal void ToDip_切り上げと最低1の規則を適用しない()
    {
        // 逆方向（物理ピクセルへ変換する方）の規則を持ち込むと、0 が 1 に化けてレイアウトが崩れる
        Assert.Equal(0.0, DpiScaler.ToDip(0, 1.25));
        Assert.Equal(0.0, DpiScaler.ToDip(0.0, 1.25));
        Assert.Equal(0.8, DpiScaler.ToDip(1, 1.25));
    }

    [Fact]
    internal void ToDip_係数が0以下または非数なら基準値を使う()
    {
        // 係数の正規化は NormalizeScaling と単一所有（逆方向の変換と対称）
        Assert.Equal(100.0, DpiScaler.ToDip(100, 0));
        Assert.Equal(100.0, DpiScaler.ToDip(100, -1));
        Assert.Equal(100.0, DpiScaler.ToDip(100, double.NaN));
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    internal void ToDip_ScaleToPixelと往復一致する(double scaling)
    {
        // 双方向の変換は対称性を保つ（片方向だけ正規化・丸めが異なると、ウィンドウの位置・サイズがずれる）
        Assert.Equal(200.0, DpiScaler.ToDip(DpiScaler.ScaleToPixel(200, scaling), scaling));
    }

    [Fact]
    internal void ToDip_サイズは成分ごとに換算する()
    {
        var result = DpiScaler.ToDip(new EsSize(125, 250), 1.25);

        Assert.Equal(new EsSize(100, 200), result);
    }

    [Fact]
    internal void ToDip_サイズでも切り上げと最低1の規則を適用しない()
    {
        var result = DpiScaler.ToDip(new EsSize(0, 1), 1.25);

        Assert.Equal(new EsSize(0, 0.8), result);
    }
}
