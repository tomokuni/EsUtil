using Avalonia.Media;
using EsUtil.Feature.Loupe.AvaloniaUI.Logging;
using EsUtil.Feature.Loupe.AvaloniaUI.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EsUtil.Feature.Loupe.AvaloniaUI.Tests.Logging;

/// <summary><see cref="LoupeLogMessage"/> の LoggerMessage 生成コード（IsEnabled 両パス）を検証するテスト。</summary>
/// <remarks>
/// 拡大元画像（<see cref="LoupeControl.Source"/>）はコントロール固有の概念であり、その変更と描画の例外は<br/>
/// 画像を所有する本 UI が記録する（<see cref="RecordingLogger"/>（IsEnabled=true）と<br/>
/// <see cref="NullLogger"/>（IsEnabled=false）の両方で全メソッドを呼び、生成コードの両パスを網羅する）。<br/>
/// <see cref="IImage"/> の寸法取得は破棄済みで例外になるため、その防御経路（型名での代用）も検証する。
/// </remarks>
public class LoupeLogMessageTests
{
    /// <summary>実ロガー設定時に全ログメソッドが実行されることを検証する。</summary>
    [Fact]
    internal void 実ロガー設定時に全ログメソッドが実行される()
    {
        var logger = new RecordingLogger();
        InvokeAll(logger);

        Assert.True(logger.IsEnabled(LogLevel.Trace));
        Assert.Equal(2, logger.Messages.Count);
    }

    /// <summary>無効ロガーでも全ログメソッドが安全に実行されることを検証する。</summary>
    [Fact]
    internal void 無効ロガーでも全ログメソッドが安全に実行される()
    {
        // IsEnabled=false（NullLogger）でも生成コードの IsEnabled 分岐（false パス）が実行される
        InvokeAll(NullLogger.Instance);
    }

    /// <summary>出力書式が期待どおりであることを検証する。</summary>
    [Fact]
    internal void 出力書式が期待どおりである()
    {
        var logger = new RecordingLogger();
        InvokeAll(logger);

        Assert.Equal("[Loupe] Setting: Source 8x6 → null", logger.Messages[0]);
        Assert.Equal("[Loupe] Update exception", logger.Messages[1]);
    }

    /// <summary>拡大元画像のログが同一インスタンスの再設定では出力されないことを検証する。</summary>
    [Fact]
    internal void 拡大元画像のログは同一インスタンスなら出力しない()
    {
        using var image = CreateImage(4, 4);
        var logger = new RecordingLogger();

        logger.LogSourceChanged(image, image);
        logger.LogSourceChanged(null, null);

        Assert.Empty(logger.Messages);
    }

    /// <summary>拡大元画像のログが寸法の等しい別インスタンスへの差し替えを出力することを検証する。</summary>
    [Fact]
    internal void 拡大元画像のログは別インスタンスなら出力する()
    {
        using var oldImage = CreateImage(8, 6);
        using var newImage = CreateImage(8, 6);
        var logger = new RecordingLogger();

        logger.LogSourceChanged(oldImage, newImage);
        logger.LogSourceChanged(oldImage, null);

        Assert.Equal(2, logger.Messages.Count);
        Assert.Equal("[Loupe] Setting: Source 8x6 → 8x6", logger.Messages[0]);
        Assert.Equal("[Loupe] Setting: Source 8x6 → null", logger.Messages[1]);
    }

    /// <summary>破棄済みの画像を渡しても例外にならず、型名で代用されることを検証する。</summary>
    [Fact]
    internal void 破棄済みの画像は型名で代用する()
    {
        var image = CreateImage(8, 6);
        image.Dispose();
        var logger = new RecordingLogger();

        logger.LogSourceChanged(image, null);

        // ログ出力で例外を発生させない（寸法が取れない場合は型名を出す）
        Assert.Equal(
            "[Loupe] Setting: Source EsBitmapAvaloniaAdapter → null",
            Assert.Single(logger.Messages));
    }

    /// <summary>例外ログに例外が渡されることを検証する。</summary>
    [Fact]
    internal void 例外ログは例外を受け取る()
    {
        var logger = new RecordingLogger();

        logger.LogUpdateError(new InvalidOperationException("失敗"));

        // メッセージ本体は例外本文を含まない（例外は構造化ログのフィールドとして渡す）
        Assert.Equal("[Loupe] Update exception", Assert.Single(logger.Messages));
    }

    /// <summary>テスト用の画像を生成する。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成した画像。</returns>
    private static EsBitmapAvaloniaAdapter CreateImage(int width, int height)
        => EsBitmapAvaloniaExtensions.CreateAvaloniaImage(width, height, Colors.Blue);

    /// <summary>全ログメソッドを指定ロガーで呼び出す。</summary>
    /// <param name="logger">ロガー。</param>
    private static void InvokeAll(ILogger logger)
    {
        using var image = CreateImage(8, 6);

        logger.LogSourceChanged(image, null);
        logger.LogUpdateError(new InvalidOperationException("test"));
    }
}
