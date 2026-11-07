using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native;

namespace EsUtil.Ui.WinForms.Interop.Tests;

/// <summary>テストで使用する DPI 照会のスタブ（モニター構成に依存せず倍率を固定する）。</summary>
/// <remarks>
/// <see cref="IMonitorDpi"/> を実装し、返す DPI をテストが決める（<see langword="null"/> は照会できない状態）。<br/>
/// 実機のモニター構成に依存すると「150% のモニター」を再現できないため、倍率の換算をこれで検証する。
/// </remarks>
internal sealed class MonitorDpiSpy : IMonitorDpi
{
    /// <summary>返す DPI。 <see langword="null"/> の場合は照会できない状態を表す。</summary>
    public uint? Dpi { get; set; }

    /// <summary><see cref="GetEffectiveDpi"/> へ渡された位置の履歴（呼び出し順）。</summary>
    public List<EsPointInt> Points { get; } = [];

    /// <inheritdoc/>
    public uint? GetEffectiveDpi(EsPointInt screenPoint)
    {
        Points.Add(screenPoint);
        return Dpi;
    }
}
