using EsUtil.Platform.Core.Native.Win;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Native;

/// <summary><see cref="WinUserPreferenceService"/> の実機テスト。</summary>
/// <remarks>
/// <b>検証の目的</b>: OS の設定値（ダブルクリックと見なす時間）を実際に取得できることを確かめる
/// （<c>GetDoubleClickTime</c> の宣言の誤りは「常に 0 を返す」形で現れ、値の範囲では検出できない）。<br/>
/// <b>値そのものは環境で変わる</b>: 既定は 500 ミリ秒だが、利用者が変更できるため範囲だけを検証する。
/// </remarks>
public class WinUserPreferenceServiceTests
{
    /// <summary>ダブルクリックと見なす時間が OS の有効な範囲で返ることを検証する。</summary>
    [Fact]
    internal void DoubleClickIntervalMilliseconds_OSの設定値を返す()
    {
        var value = new WinUserPreferenceService().DoubleClickIntervalMilliseconds;

        // GetDoubleClickTime は 0〜5000 ミリ秒を返す（0 は取得失敗）。
        Assert.InRange(value, 1, 5000);
    }
}
