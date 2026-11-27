using EsUtil.Platform.Core.Logging;
using EsUtil.Feature.StyledText.Core.Editing;
using EsUtil.Feature.StyledText.Logging;
using EsUtil.Feature.StyledText.Presenter.Formatting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EsUtil.Feature.StyledText.Core.Tests;

/// <summary>編集操作のログが操作内容と値を記録することを検証する。</summary>
public sealed class TextLogMessageTests
{
    /// <summary>本文は先頭 100 文字へ切り詰め、末尾に省略記号を付ける。</summary>
    [Fact]
    internal void ToPreview_100文字を超えたら切り詰める()
    {
        var preview = LogText.ToPreview(new string('あ', 150));

        Assert.Equal(101, preview.Length);
        Assert.EndsWith("…", preview, StringComparison.Ordinal);
    }

    /// <summary>改行は 1 行へ畳む（ログの 1 行を壊さない）。</summary>
    [Fact]
    internal void ToPreview_改行を1行にする()
        => Assert.Equal("a\\nb", LogText.ToPreview("a\r\nb"));

    /// <summary>本文の変更は文字数と本文の先頭を記録する。</summary>
    [Fact]
    internal void LogTextChanged_文字数と本文を記録する()
    {
        var logger = new RecordingLogger();
        var editor = new TextEditor
        {
            Log = logger,
            Text = RichText.Create("abc", TextDefaults.DefaultStyle),
        };

        editor.BeginEdit();
        editor.ReplaceSelection("xy");

        Assert.Contains(logger.Messages, m => m.Contains("本文を変更: 文字数=5 本文=abcxy", StringComparison.Ordinal));
    }

    /// <summary>選択の変更は Trace でキャレットと選択範囲を記録する。</summary>
    [Fact]
    internal void LogTextSelectionChanged_キャレットと選択を記録する()
    {
        var logger = new RecordingLogger();
        var editor = new TextEditor
        {
            Log = logger,
            Text = RichText.Create("abcd", TextDefaults.DefaultStyle),
        };

        editor.Select(0, 2);

        var index = logger.Messages.FindIndex(m => m.Contains("本文の選択: キャレット=", StringComparison.Ordinal));
        Assert.True(index >= 0);
        Assert.Contains("選択=(0, 2)", logger.Messages[index], StringComparison.Ordinal);
        Assert.Equal(LogLevel.Trace, logger.Levels[index]);
    }

    /// <summary>書式バーの項目の変更は項目名を並べて記録する。</summary>
    [Fact]
    internal void LogSettingChanged_書式バーの項目名を記録する()
    {
        var logger = new RecordingLogger();

        logger.LogSettingChanged(
            "FormatBarItems",
            TextFormatBarItemVisibility.Default,
            TextFormatBarItemVisibility.Default with { ShowColor = false });

        Assert.Contains(
            logger.Messages,
            m => m.Contains("設定: FormatBarItems", StringComparison.Ordinal) && m.Contains("Color", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Information, logger.Levels[^1]);
    }
}
