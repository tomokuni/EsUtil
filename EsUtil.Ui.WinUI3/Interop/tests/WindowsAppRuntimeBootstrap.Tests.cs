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

    [Fact]
    internal void GetRequiredVersion_は設定が無ければこのアセンブリの下限を返す()
    {
        var floor = WindowsAppRuntimeInventory.RequiredVersion;

        Assert.Equal(floor, WindowsAppRuntimeBootstrap.GetRequiredVersion());
        Assert.Equal(floor, WindowsAppRuntimeBootstrap.GetRequiredVersion(WindowsAppRuntimeBootstrapOptions.Default));
    }

    [Fact]
    internal void GetRequiredVersion_はアプリが指定した高い版を採る()
    {
        // アプリが本アセンブリより新しい Windows App SDK でビルドするのは正常である。
        // 低い方（本アセンブリの下限）で初期化すると、アプリのマネージド アセンブリより古い
        // ネイティブ ランタイムが読み込まれてしまう（実測 2026-11-10）
        var floor = WindowsAppRuntimeInventory.RequiredVersion;
        var newer = new Version(floor.Major, floor.Minor, floor.Build + 1, 0);
        var options = new WindowsAppRuntimeBootstrapOptions { RequiredVersion = newer };

        Assert.Equal(newer, WindowsAppRuntimeBootstrap.GetRequiredVersion(options));
    }

    [Fact]
    internal void GetRequiredVersion_は下限より低い指定を下限へ切り上げる()
    {
        // 下限を下回るランタイムでは本アセンブリが動かないため、指定が低くても下限を使う
        var floor = WindowsAppRuntimeInventory.RequiredVersion;
        var older = new Version(floor.Major, floor.Minor, Math.Max(floor.Build - 1, 0), 0);
        var options = new WindowsAppRuntimeBootstrapOptions { RequiredVersion = older };

        Assert.Equal(floor, WindowsAppRuntimeBootstrap.GetRequiredVersion(options));
    }

    [Fact]
    internal void GetRequiredVersion_は下限と同じ版をそのまま返す()
    {
        var floor = WindowsAppRuntimeInventory.RequiredVersion;
        var options = new WindowsAppRuntimeBootstrapOptions { RequiredVersion = floor };

        Assert.Equal(floor, WindowsAppRuntimeBootstrap.GetRequiredVersion(options));
    }

    [Fact]
    internal void CreateInstallerUri_は指定した版をURLへ入れる()
    {
        var uri = WindowsAppRuntimeBootstrap.CreateInstallerUri(new Version(2, 6, 5, 0));

        Assert.NotNull(uri);
        Assert.Contains("/windowsappsdk/2.6/2.6.5/", uri!.AbsolutePath, StringComparison.Ordinal);
    }
}
