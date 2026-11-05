namespace EsUtil.Ui.WinUI3.Interop.Bootstrapping;

/// <summary>Windows App Runtime の導入と初期化の設定。</summary>
/// <remarks>
/// <see cref="Default"/> は「無ければダウンロードして導入し、初期化する」である（利用者の操作を要さない）。<br/>
/// 設定は不変であり、<c>with</c> 式で差し替える（<see cref="WindowsAppRuntimeBootstrap.TryEnsureAvailable"/> は
/// 呼び出しごとに受け取るため、コントロールの状態へ保持しない）。<br/>
/// 利用者へ確認してから導入する場合は <see cref="WindowsAppRuntimeInstallMode.BootstrapUi"/> を使う。
/// </remarks>
public sealed record WindowsAppRuntimeBootstrapOptions
{
    /// <summary>既定の設定を取得する。</summary>
    /// <value><see cref="WindowsAppRuntimeInstallMode.Download"/> を使う設定。</value>
    public static WindowsAppRuntimeBootstrapOptions Default { get; } = new();

    /// <summary>ランタイムが無いときの導入方法を取得または設定する。</summary>
    /// <value>導入方法。 既定は <see cref="WindowsAppRuntimeInstallMode.Download"/>。</value>
    public WindowsAppRuntimeInstallMode InstallMode { get; init; } = WindowsAppRuntimeInstallMode.Download;

    /// <summary>インストーラーの取得先を取得または設定する。</summary>
    /// <value>取得先。 null（既定）の場合は Microsoft の配布 URL を使う。</value>
    /// <remarks>
    /// 社内ミラーやオフライン配布へ差し替えるための入口である。 指定した場合も実行するファイルは
    /// Windows App Runtime のインストーラー（<c>windowsappruntimeinstall.exe</c>）であること。
    /// </remarks>
    public Uri? InstallerSource { get; init; }

    /// <summary>インストーラーの取得と実行の制限時間を取得または設定する。</summary>
    /// <value>制限時間。 既定は 5 分。</value>
    /// <remarks>取得と実行でそれぞれこの時間を使う（合計は最長で 2 倍になる）。</remarks>
    public TimeSpan InstallerTimeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>案内 UI を表示する処理を取得または設定する。</summary>
    /// <value>
    /// 案内を行う処理。 null（既定）の場合は組み込みの案内 UI を出す
    /// （タスク ダイアログ。 使えない環境ではメッセージ ボックス）。
    /// </value>
    /// <remarks>
    /// <see cref="WindowsAppRuntimeInstallMode.BootstrapUi"/> のときだけ使う。 アプリが自前の UI
    /// （WinUI のダイアログ・専用の画面）で尋ねたい場合に設定する。
    /// </remarks>
    public WindowsAppRuntimeInstallPrompt? Prompt { get; init; }
}
