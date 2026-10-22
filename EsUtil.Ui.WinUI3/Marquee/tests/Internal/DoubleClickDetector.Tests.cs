using EsUtil.Ui.WinUI3.Marquee.Internal;
using Xunit;

namespace EsUtil.Ui.WinUI3.Marquee.Tests.Internal;

/// <summary><see cref="DoubleClickDetector"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// WinUI 3 のポインター イベントはクリック数を報告しないため、本判定がダブルクリックの唯一の根拠になる。<br/>
/// 時間（OS の設定値）と距離（本クラスの上限）の両方について、境界の内側と外側を検証する。
/// </remarks>
public class DoubleClickDetectorTests
{
    /// <summary>時間と距離の両方が上限内ならダブルクリックと判定することを検証する。</summary>
    [Fact]
    internal void IsDoubleClick_時間と距離が上限内ならtrue()
        => Assert.True(DoubleClickDetector.IsDoubleClick(new EsPoint(100, 100), new EsPoint(100, 100), 100, 500));

    /// <summary>時間が上限ちょうどならダブルクリックと判定することを検証する（上限は「以内」）。</summary>
    [Fact]
    internal void IsDoubleClick_時間が上限ちょうどならtrue()
        => Assert.True(DoubleClickDetector.IsDoubleClick(new EsPoint(0, 0), new EsPoint(0, 0), 500, 500));

    /// <summary>時間が上限を超える場合はダブルクリックと判定しないことを検証する。</summary>
    [Fact]
    internal void IsDoubleClick_時間が上限を超えるならfalse()
        => Assert.False(DoubleClickDetector.IsDoubleClick(new EsPoint(0, 0), new EsPoint(0, 0), 501, 500));

    /// <summary>負の経過時間（時計の巻き戻り・未初期化）はダブルクリックと判定しないことを検証する。</summary>
    [Fact]
    internal void IsDoubleClick_負の経過時間はfalse()
        => Assert.False(DoubleClickDetector.IsDoubleClick(new EsPoint(0, 0), new EsPoint(0, 0), -1, 500));

    /// <summary>距離が上限ちょうどならダブルクリックと判定することを検証する。</summary>
    [Fact]
    internal void IsDoubleClick_距離が上限ちょうどならtrue()
    {
        var previous = new EsPoint(100, 100);
        var current = new EsPoint(100 + DoubleClickDetector.MaxMoveDip, 100 + DoubleClickDetector.MaxMoveDip);

        Assert.True(DoubleClickDetector.IsDoubleClick(previous, current, 100, 500));
    }

    /// <summary>距離が上限を超える場合はダブルクリックと判定しないことを検証する。</summary>
    [Fact]
    internal void IsDoubleClick_距離が上限を超えるならfalse()
    {
        var previous = new EsPoint(100, 100);
        var current = new EsPoint(100 + DoubleClickDetector.MaxMoveDip + 0.5, 100);

        Assert.False(DoubleClickDetector.IsDoubleClick(previous, current, 100, 500));
    }

    /// <summary>片方の軸だけが大きく離れている場合もダブルクリックと判定しないことを検証する。</summary>
    [Fact]
    internal void IsDoubleClick_片方の軸だけ離れているならfalse()
        => Assert.False(DoubleClickDetector.IsDoubleClick(
            new EsPoint(100, 100), new EsPoint(100, 200), 100, 500));

    /// <summary>押下の記録がある場合でも時間の上限が 0 で経過していればダブルクリックと判定しないことを検証する。</summary>
    [Fact]
    internal void IsDoubleClick_上限0で経過していればfalse()
        => Assert.False(DoubleClickDetector.IsDoubleClick(new EsPoint(0, 0), new EsPoint(0, 0), 1, 0));
}
