using System.Globalization;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EsUtil.Ui.WinForms.Thumbnail.Tests;

/// <summary><see cref="ThumbnailLogMessage"/>（WinForms 版）の単体テスト。</summary>
/// <remarks>
/// 接頭辞は定義クラスの <c>static</c> プロパティ（プロセス全体の状態）であるため、<br/>
/// 設定するテストはコレクションで直列化し、<c>try</c> / <c>finally</c> で既定値へ戻す（ログ規約 §7）。
/// </remarks>
[Collection(ThumbnailLogPrefix.Name)]
public class ThumbnailLogMessageTests
{
    /// <summary>既定の接頭辞が規約の値であることを検証する。</summary>
    [Fact]
    internal void DefaultPrefix_規約の接頭辞を持つ()
        => Assert.Equal("[Thumbnail]", ThumbnailLogMessage.DefaultPrefix);

    /// <summary>画像の差し替えが書式どおりに記録されることを検証する。</summary>
    [Fact]
    internal void LogImageChanged_接頭辞と寸法を含むメッセージを出力する()
    {
        var logger = new RecordingLogger();

        try
        {
            ThumbnailLogMessage.Prefix = "[Test]";
            using var before = new Bitmap(4, 3);
            using var after = new Bitmap(8, 6);
            logger.LogImageChanged("Image[2]", ThumbnailLogMessage.Describe(before), ThumbnailLogMessage.Describe(after));
        }
        finally
        {
            ThumbnailLogMessage.Prefix = ThumbnailLogMessage.DefaultPrefix;
        }

        Assert.Equal("[Test] Setting: Image[2] 4x3 → 8x6", Assert.Single(logger.Messages));
        Assert.Equal(LogLevel.Debug, Assert.Single(logger.Levels));
    }

    /// <summary>同じ画像（同じ記述）では記録しないことを検証する。</summary>
    [Fact]
    internal void LogImageChanged_同じ記述では記録しない()
    {
        var logger = new RecordingLogger();

        logger.LogImageChanged("Image[0]", "8x6", "8x6");

        Assert.Empty(logger.Messages);
    }

    /// <summary>項目数の変更が書式どおりに記録されることを検証する。</summary>
    [Fact]
    internal void LogItemCountChanged_接頭辞と件数を含むメッセージを出力する()
    {
        var logger = new RecordingLogger();

        try
        {
            ThumbnailLogMessage.Prefix = "[Test]";
            logger.LogItemCountChanged(3, 4);
        }
        finally
        {
            ThumbnailLogMessage.Prefix = ThumbnailLogMessage.DefaultPrefix;
        }

        Assert.Equal("[Test] Setting: ItemCount 3 → 4", Assert.Single(logger.Messages));
    }

    /// <summary>同数の再設定では記録しないことを検証する。</summary>
    [Fact]
    internal void LogItemCountChanged_同数では記録しない()
    {
        var logger = new RecordingLogger();

        logger.LogItemCountChanged(2, 2);

        Assert.Empty(logger.Messages);
    }

    /// <summary>画像の記述がカルチャに依存しないことを検証する。</summary>
    [Fact]
    internal void Describe_カルチャに依存しない寸法を返す()
    {
        using var image = new Bitmap(12, 34);

        Assert.Equal(
            string.Format(CultureInfo.InvariantCulture, "{0}x{1}", 12, 34),
            ThumbnailLogMessage.Describe(image));
        Assert.Null(ThumbnailLogMessage.Describe(null));
    }
}
