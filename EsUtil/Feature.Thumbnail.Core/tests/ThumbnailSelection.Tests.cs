using EsUtil.Feature.Thumbnail.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EsUtil.Feature.Thumbnail.Tests;

/// <summary>単一選択の規則（範囲・通知・変更の検出）と選択のログの単体テスト。</summary>
[Collection(ThumbnailLogPrefix.Name)]
public class ThumbnailSelectionTests
{
    /// <summary>範囲内の添字を選択できることを検証する。</summary>
    [Fact]
    internal void Select_範囲内の添字を選択する()
    {
        var selection = new ThumbnailSelection();

        Assert.True(selection.Select(2, 5));
        Assert.Equal(2, selection.Index);
        Assert.True(selection.HasSelection);
    }

    /// <summary>範囲外の添字は選択の解除として扱うことを検証する。</summary>
    /// <param name="index">範囲外の添字。</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData(int.MaxValue)]
    internal void Select_範囲外の添字は選択を解除する(int index)
    {
        var selection = new ThumbnailSelection();
        selection.Select(2, 5);

        Assert.True(selection.Select(index, 5));
        Assert.Equal(-1, selection.Index);
        Assert.False(selection.HasSelection);
    }

    /// <summary>同じ添字の再設定では通知も変更も発生しないことを検証する。</summary>
    [Fact]
    internal void Select_同じ添字では通知しない()
    {
        var selection = new ThumbnailSelection();
        var raised = 0;
        selection.Changed += (_, _) => raised++;
        selection.Select(1, 3);

        Assert.False(selection.Select(1, 3));
        Assert.Equal(1, raised);
    }

    /// <summary>選択の解除が変更として扱われることを検証する。</summary>
    [Fact]
    internal void Clear_選択を解除する()
    {
        var selection = new ThumbnailSelection();
        selection.Select(1, 3);

        Assert.True(selection.Clear());
        Assert.Equal(-1, selection.Index);
    }

    /// <summary>項目が無い場合は選択できないことを検証する。</summary>
    [Fact]
    internal void Select_項目が無い場合は選択しない()
    {
        var selection = new ThumbnailSelection();

        Assert.False(selection.Select(0, 0));
        Assert.Equal(-1, selection.Index);
    }

    /// <summary>null のロガーを設定しても <see cref="NullLogger"/> へ吸収されることを検証する。</summary>
    [Fact]
    internal void Log_nullを設定しても例外にしない()
    {
        var selection = new ThumbnailSelection { Log = null! };

        Assert.True(selection.Select(0, 1));
        Assert.Equal(0, selection.Index);
    }

    /// <summary>選択の変更が定義どおりの書式で記録されることを検証する。</summary>
    [Fact]
    internal void Select_選択の変更を記録する()
    {
        var original = ThumbnailLogMessage.Prefix;
        var logger = new RecordingLogger();
        var selection = new ThumbnailSelection { Log = logger };

        try
        {
            selection.LogPrefix = "[Test]";
            selection.Select(3, 5);
            selection.Clear();
        }
        finally
        {
            selection.LogPrefix = original;
        }

        Assert.Equal(
            ["[Test] Selection: -1 → 3", "[Test] Selection: 3 → -1"],
            logger.Messages);
        Assert.All(logger.Levels, level => Assert.Equal(LogLevel.Debug, level));
    }

    /// <summary>同値の再設定では記録しないことを検証する。</summary>
    [Fact]
    internal void Select_同値の再設定では記録しない()
    {
        var logger = new RecordingLogger();
        var selection = new ThumbnailSelection { Log = logger };
        selection.Select(1, 3);
        logger.Messages.Clear();

        selection.Select(1, 3);

        Assert.Empty(logger.Messages);
    }

    /// <summary>接頭辞を空白にすると既定値へ戻ることを検証する。</summary>
    [Fact]
    internal void LogPrefix_空白は既定値へ戻す()
    {
        var selection = new ThumbnailSelection { LogPrefix = " " };

        Assert.Equal(" ", selection.LogPrefix);
        Assert.Equal(ThumbnailLogMessage.DefaultPrefix, ThumbnailLogMessage.Prefix);
    }
}
