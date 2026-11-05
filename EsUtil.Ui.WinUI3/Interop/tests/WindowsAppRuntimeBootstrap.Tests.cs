using EsUtil.Ui.WinUI3.Interop.Bootstrapping;
using Xunit;

namespace EsUtil.Ui.WinUI3.Interop.Tests;

/// <summary><see cref="WindowsAppRuntimeBootstrap"/> の単体テスト（配布 URL・設定・初期化の契約）。</summary>
/// <remarks>
/// ランタイムを実際に導入する経路はネットワークと管理者以外の操作を伴うため、テストでは触れない。<br/>
/// ここでは<b>導入を使わなくても確かめられること</b>（URL の組み立て・既定の設定・P/Invoke の解決）だけを検証する。
/// </remarks>
public class WindowsAppRuntimeBootstrapTests
{
    [Fact]
    internal void CreateInstallerUri_はMicrosoftの配布URLを指す()
    {
        var uri = WindowsAppRuntimeBootstrap.CreateInstallerUri();

        Assert.NotNull(uri);
        var installUri = uri!;
        Assert.Equal("https", installUri.Scheme);
        Assert.Equal("aka.ms", installUri.Host);
        Assert.StartsWith("/windowsappsdk/", installUri.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains("/windowsappruntimeinstall-", installUri.AbsolutePath, StringComparison.Ordinal);
        Assert.EndsWith(".exe", installUri.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    internal void Default_はダウンロードで導入する()
    {
        var options = WindowsAppRuntimeBootstrapOptions.Default;

        Assert.Equal(WindowsAppRuntimeInstallMode.Download, options.InstallMode);
        Assert.Null(options.InstallerSource);
        Assert.Null(options.Prompt);
        Assert.True(options.InstallerTimeout > TimeSpan.Zero);
    }

    [Fact]
    internal void DownloadPageUri_はMicrosoftのダウンロードページを指す()
    {
        var uri = WindowsAppRuntimeBootstrap.DownloadPageUri;

        Assert.Equal("https", uri.Scheme);
        Assert.Equal("learn.microsoft.com", uri.Host);
        Assert.Contains("windows-app-sdk", uri.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    internal void TryInitialize_は結果とHRESULTが一致する()
    {
        // ランタイムが入っていない環境では HRESULT で失敗する（自己完結の配置は成功として扱われる）。
        // どちらの環境でも「成功なら初期化済み・失敗なら負の HRESULT」という契約を保つ
        var succeeded = WindowsAppRuntimeBootstrap.TryInitialize(out var errorCode);
        try
        {
            if (succeeded)
            {
                Assert.Equal(0, errorCode);
                Assert.True(WindowsAppRuntimeBootstrap.IsInitialized);
            }
            else
            {
                Assert.True(errorCode < 0);
                Assert.False(WindowsAppRuntimeBootstrap.IsInitialized);
            }
        }
        finally
        {
            WindowsAppRuntimeBootstrap.Shutdown();
        }
    }

    [Fact]
    internal void TryInitialize_は制限時間が正でない設定を拒否する()
    {
        var options = new WindowsAppRuntimeBootstrapOptions { InstallerTimeout = TimeSpan.Zero };

        Assert.Throws<ArgumentOutOfRangeException>(() => WindowsAppRuntimeBootstrap.TryInitialize(options, out _));
    }
}
