using EsUtil.Platform.Core.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EsUtil.Feature.Loupe.MewUI.Tests;

/// <summary><see cref="LoupeLogMessage"/> の LoggerMessage 生成コード（IsEnabled 両パス）を検証するテスト。</summary>
/// <remarks>
/// 拡大元画像（<see cref="LoupeControl.Source"/>）はコントロール固有の概念であり、その変更と描画の例外は<br/>
/// 画像を所有する本 UI が記録する（<see cref="RecordingLogger"/>（IsEnabled=true）と<br/>
/// <see cref="NullLogger"/>（IsEnabled=false）の両方で全メソッドを呼び、生成コードの両パスを網羅する）。
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
        using var bitmap = EsBitmap.Create(4, 4);
        bitmap.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bitmap);
        var logger = new RecordingLogger();

        logger.LogSourceChanged(read, read);
        logger.LogSourceChanged(null, null);

        Assert.Empty(logger.Messages);
    }

    /// <summary>拡大元画像のログが寸法の等しい別インスタンスへの差し替えを出力することを検証する。</summary>
    [Fact]
    internal void 拡大元画像のログは別インスタンスなら出力する()
    {
        using var oldBitmap = EsBitmap.Create(8, 6);
        oldBitmap.SetReadOnly();
        using var oldRead = EsReadOnlyBitmap.FromBitmap(oldBitmap);
        using var newBitmap = EsBitmap.Create(8, 6);
        newBitmap.SetReadOnly();
        using var newRead = EsReadOnlyBitmap.FromBitmap(newBitmap);
        var logger = new RecordingLogger();

        logger.LogSourceChanged(oldRead, newRead);
        logger.LogSourceChanged(oldRead, null);

        Assert.Equal(2, logger.Messages.Count);
        Assert.Equal("[Loupe] Setting: Source 8x6 → 8x6", logger.Messages[0]);
        Assert.Equal("[Loupe] Setting: Source 8x6 → null", logger.Messages[1]);
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

    /// <summary>全ログメソッドを指定ロガーで呼び出す。</summary>
    /// <param name="logger">ロガー。</param>
    private static void InvokeAll(ILogger logger)
    {
        using var bitmap = EsBitmap.Create(8, 6);
        bitmap.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bitmap);

        logger.LogSourceChanged(read, null);
        logger.LogUpdateError(new InvalidOperationException("test"));
    }
}
