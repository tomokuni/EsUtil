using EsUtil.Platform.Core.Imaging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EsUtil.Ui.WinUI3.Thumbnail.Tests;

/// <summary><see cref="ThumbnailLogMessage"/>（WinUI 3 版）の単体テスト。</summary>
/// <remarks>
/// 接頭辞は定義クラスの <c>static</c> プロパティ（プロセス全体の状態）であるため、<br/>
/// 設定するテストはコレクションで直列化し、<c>try</c> / <c>finally</c> で既定値へ戻す（ログ規約 §7）。<br/>
/// 本 UI 版のコントロールは XAML の初期化を必要とするため、自動テストはログ定義と描画手順を対象にする。
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
            using var before = EsReadOnlyBitmap.Create(4, 3);
            using var after = EsReadOnlyBitmap.Create(8, 6);
            logger.LogImageChanged("Image[2]", before, after);
        }
        finally
        {
            ThumbnailLogMessage.Prefix = ThumbnailLogMessage.DefaultPrefix;
        }

        Assert.Equal("[Test] Setting: Image[2] 4x3 → 8x6", Assert.Single(logger.Messages));
        Assert.Equal(LogLevel.Debug, Assert.Single(logger.Levels));
    }

    /// <summary>同じインスタンスの再設定では記録しないことを検証する。</summary>
    [Fact]
    internal void LogImageChanged_同じインスタンスでは記録しない()
    {
        var logger = new RecordingLogger();
        using var image = EsReadOnlyBitmap.Create(8, 6);

        logger.LogImageChanged("Image[0]", image, image);

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
}
