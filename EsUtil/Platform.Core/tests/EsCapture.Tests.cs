using EsUtil.Platform.Core.Capture;
using Xunit;

namespace EsUtil.Platform.Core.Tests;

/// <summary>キャプチャの窓口（<see cref="EsCapture"/>）の単体テスト。</summary>
/// <remarks>
/// 窓口は実装を持たず機能の型へ委譲するため、ここでは<b>委譲の結果</b>だけを検証する。<br/>
/// 解釈の規則そのものは `Common.EnumCanonical` のテストが検証する。
/// </remarks>
public class EsCaptureTests
{
    [Theory]
    [InlineData("ScreenSelect", CaptureType.ScreenSelect)]
    [InlineData("windowselect", CaptureType.WindowSelect)]
    [InlineData("AreaSelect", CaptureType.AreaSelect)]
    internal void TryParseType_名前を解釈する(string name, CaptureType expected)
    {
        Assert.True(EsCapture.TryParseType(name, out var captureType));
        Assert.Equal(expected, captureType);
    }

    [Fact]
    internal void TryParseType_数値文字列は受理しない()
    {
        Assert.False(EsCapture.TryParseType("999", out var captureType));
        Assert.Equal(CaptureType.ScreenSelect, captureType);
    }

    [Fact]
    internal void TryParseType_未知の名前は失敗する()
        => Assert.False(EsCapture.TryParseType("WholeScreen", out _));
}