using Microsoft.Extensions.Logging;
using Xunit;

namespace EsUtil.Ui.WinUI3.ColorComboBox.Tests;

/// <summary><see cref="LogMessage"/> の定義（接頭辞・レベル・書式）のテスト。</summary>
[Collection(ColorComboBoxLogPrefix.Name)]
public class LogMessageTests
{
    [Fact]
    internal void 既定の接頭辞はColorComboBoxである()
        => Assert.Equal("[ColorComboBox]", LogMessage.DefaultPrefix);

    [Fact]
    internal void 同じ値では出力しない()
    {
        var logger = new RecordingLogger();

        logger.LogSettingChanged("SelectedColor", "#FFFFFFFF", "#FFFFFFFF");

        Assert.Empty(logger.Messages);
    }

    [Fact]
    internal void 変更はDebugレベルで出力する()
    {
        var logger = new RecordingLogger();

        logger.LogSettingChanged("SelectedColor", "#FFFFFFFF", "#FF008000");

        Assert.Single(logger.Messages);
        Assert.Equal(LogLevel.Debug, logger.Levels[0]);
        Assert.Contains("#FFFFFFFF", logger.Messages[0], StringComparison.Ordinal);
        Assert.Contains("#FF008000", logger.Messages[0], StringComparison.Ordinal);
    }
}