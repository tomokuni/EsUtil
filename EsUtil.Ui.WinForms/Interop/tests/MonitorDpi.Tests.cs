using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Ui.WinForms.Interop.Tests;

/// <summary><see cref="MonitorDpi"/> のテスト（モニター単位の DPI 照会）。</summary>
/// <remarks>
/// 照会は Win32（<c>MonitorFromPoint</c> / <c>GetDpiForMonitor</c>）へ委ねるため、<br/>
/// 本テストは「照会できない環境では 0（無効値）を返す」という契約を検証する<br/>
/// （0 を返す契約により、呼び出し側は代替値で解決できる）。<br/>
/// ウィンドウ（ハンドル）を必要としない。
/// </remarks>
public sealed class MonitorDpiTests
{
    /// <summary>プライマリ モニター上の点では有効な倍率を返すことを検証する。</summary>
    /// <remarks>
    /// プロセスが DPI 非対応の場合はシステム DPI（96）が返るため、倍率は 1.0 になる。<br/>
    /// 照会自体に失敗する環境（対応 API を持たない OS 等）では 0（無効値）を返す。
    /// </remarks>
    [Fact]
    internal void GetScaleForPoint_有効な倍率を返す()
    {
        var scale = MonitorDpi.GetScaleForPoint(new EsPointInt(0, 0));

        Assert.True(scale == 0 || DpiScaler.IsValidScaling(scale));
    }

    /// <summary>仮想スクリーンの外の点でも例外を出さないことを検証する。</summary>
    /// <remarks>画面外の座標では最も近いモニターが選ばれる（照会の可否は OS の判断に従う）。</remarks>
    [Fact]
    internal void GetScaleForPoint_画面外の座標でも例外を出さない()
    {
        var scale = MonitorDpi.GetScaleForPoint(new EsPointInt(-32000, -32000));

        Assert.True(scale == 0 || DpiScaler.IsValidScaling(scale));
    }

    /// <summary>照会先を差し替えると、その DPI を倍率へ換算することを検証する（150% の環境を再現する）。</summary>
    [Fact]
    internal void GetScaleForPoint_差し替えた照会先の倍率を返す()
    {
        var spy = new MonitorDpiSpy { Dpi = 144 };

        var scale = MonitorDpi.GetScaleForPoint(new EsPointInt(10, 20), spy);

        Assert.Equal(1.5, scale, precision: 6);
        Assert.Equal([new EsPointInt(10, 20)], spy.Points);
    }

    /// <summary>照会できない場合は 0（無効値）を返すことを検証する（96 DPI と区別する）。</summary>
    [Fact]
    internal void GetScaleForPoint_照会できない場合は0を返す()
    {
        var spy = new MonitorDpiSpy { Dpi = null };

        var scale = MonitorDpi.GetScaleForPoint(new EsPointInt(0, 0), spy);

        Assert.Equal(0, scale);
        Assert.False(DpiScaler.IsValidScaling(scale));
    }
}
