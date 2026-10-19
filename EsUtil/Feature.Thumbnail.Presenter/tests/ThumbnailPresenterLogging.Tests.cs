using EsUtil.Feature.Thumbnail.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EsUtil.Feature.Thumbnail.Tests;

/// <summary><see cref="ThumbnailPresenter"/> のログ（生成・ロガーの設定・設定変更）の単体テスト。</summary>
/// <remarks>接頭辞は定義クラスの <c>static</c> プロパティ（プロセス全体の状態）のため、<c>try</c> / <c>finally</c> で既定値へ戻す。</remarks>
[Collection(ThumbnailLogPrefix.Name)]
public class ThumbnailPresenterLoggingTests
{
    /// <summary>ロガーの初回設定時に生成を 1 度だけ記録することを検証する。</summary>
    [Fact]
    internal void Log_生成は初回の設定時に1度だけ記録する()
    {
        var logger = new RecordingLogger();
        var presenter = new ThumbnailPresenter { Log = logger };

        presenter.Log = logger;

        Assert.Equal(LogLevel.Information, logger.Levels[0]);
        Assert.Equal("[Thumbnail] Presenter created", logger.Messages[0]);
        Assert.Equal(1, logger.Messages.Count(message => message.EndsWith("Presenter created", StringComparison.Ordinal)));
        Assert.Equal(2, logger.Messages.Count(message => message.EndsWith("Logger attached", StringComparison.Ordinal)));
    }

    /// <summary>null のロガーを設定しても <see cref="NullLogger"/> へ吸収されることを検証する。</summary>
    [Fact]
    internal void Log_nullを設定しても例外にしない()
    {
        var presenter = new ThumbnailPresenter
        {
            Log = null!,
            Options = ThumbnailOptions.Default with { ItemMargin = 1 },
        };

        Assert.NotNull(presenter.Log);
        Assert.Equal(1, presenter.Options.ItemMargin);
    }

    /// <summary>設定の変更が変化した項目だけ記録されることを検証する。</summary>
    [Fact]
    internal void Options_変更を記録する()
    {
        var original = ThumbnailLogMessage.Prefix;
        var logger = new RecordingLogger();
        var presenter = new ThumbnailPresenter { Log = logger };

        try
        {
            presenter.LogPrefix = "[Test]";
            logger.Messages.Clear();
            presenter.Options = presenter.Options with { ItemSpacing = 6 };
        }
        finally
        {
            presenter.LogPrefix = original;
        }

        Assert.Equal(["[Test] Setting: ItemSpacing 0 → 6"], logger.Messages);
        Assert.Equal(6, presenter.Options.ItemSpacing);
    }

    /// <summary>同じインスタンスの再設定では記録しないことを検証する。</summary>
    [Fact]
    internal void Options_同じインスタンスでは記録しない()
    {
        var logger = new RecordingLogger();
        var presenter = new ThumbnailPresenter { Log = logger };
        var options = presenter.Options;
        logger.Messages.Clear();

        presenter.Options = options;

        Assert.Empty(logger.Messages);
    }

    /// <summary>外観の変更が変化した項目だけ記録されることを検証する。</summary>
    [Fact]
    internal void Appearance_変更を記録する()
    {
        var logger = new RecordingLogger();
        var presenter = new ThumbnailPresenter { Log = logger };
        logger.Messages.Clear();

        presenter.Appearance = presenter.Appearance with { ShowHoverBackground = false };

        Assert.Equal(["[Thumbnail] Setting: ShowHoverBackground True → False"], logger.Messages);
    }

    /// <summary>接頭辞を空白にすると既定値へ戻ることを検証する。</summary>
    [Fact]
    internal void LogPrefix_空白は既定値へ戻す()
    {
        var presenter = new ThumbnailPresenter { LogPrefix = string.Empty };

        Assert.Equal(string.Empty, presenter.LogPrefix);
        Assert.Equal(ThumbnailLogMessage.DefaultPrefix, ThumbnailLogMessage.Prefix);
    }
}
