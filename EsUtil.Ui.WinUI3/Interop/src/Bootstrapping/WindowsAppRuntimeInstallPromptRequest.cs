namespace EsUtil.Ui.WinUI3.Interop.Bootstrapping;

/// <summary>案内 UI へ渡す情報（必要とする版・インストール済みの最新版・ダウンロード ページ）。</summary>
/// <remarks>
/// <see cref="WindowsAppRuntimeInstallPrompt"/> の引数であり、<see cref="WindowsAppRuntimeInstallMode.BootstrapUi"/> のときに
/// <see cref="WindowsAppRuntimeBootstrap"/> が組み立てる。 案内 UI をアプリが自前で作る場合はこの値だけを使う。
/// </remarks>
public sealed record WindowsAppRuntimeInstallPromptRequest
{
    /// <summary>必要とする版を取得する。</summary>
    /// <value>EsUtil の WinUI 3 版が必要とする Windows App Runtime の版（例: <c>2.6.4.0</c>）。</value>
    public required Version RequiredVersion { get; init; }

    /// <summary>インストール済みの最新の版を取得する。</summary>
    /// <value>最新の版。 1 件も入っていない（または調べられない）場合は null。</value>
    public Version? InstalledVersion { get; init; }

    /// <summary>ダウンロード ページの URL を取得する。</summary>
    /// <value>Microsoft の公式ダウンロード ページ。</value>
    public required Uri DownloadPageUri { get; init; }
}
