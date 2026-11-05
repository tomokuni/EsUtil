using System.Diagnostics;
using System.Runtime.InteropServices;
using EsUtil.Platform.Core;
using SdkRelease = Microsoft.WindowsAppSDK.Release;
using SdkRuntimeVersion = Microsoft.WindowsAppSDK.Runtime.Version;

namespace EsUtil.Ui.WinUI3.Interop.Bootstrapping;

/// <summary>Windows App Runtime（Windows App SDK のランタイム）を用意してから WinUI 3 を使えるようにする入口。</summary>
/// <remarks>
/// <b>何のためか</b>: アンパッケージ（<c>WindowsPackageType=None</c>）の WinUI 3 アプリは、実行する PC に
/// Windows App Runtime が入っていなければ XAML の初期化に失敗して終了する。 Windows App SDK の
/// <b>Bootstrapper</b>（<c>MddBootstrapInitialize2</c>）は一致するランタイムを探して読み込むが、
/// <b>見つからないときに導入まではしない</b>（案内 UI を出すか、失敗する）。<br/>
/// 本クラスはその前後を埋める: 初期化を試し、無ければ設定（<see cref="WindowsAppRuntimeBootstrapOptions.InstallMode"/>）に従って
/// <b>公式のインストーラーで導入してから初期化し直す</b>。 案内を出して利用者に選ばせることもできる（`BootstrapUi`）。<br/>
/// <br/>
/// <b>呼ぶ時点</b>: <b>WinUI の型へ触れる前</b>（<c>Application</c> の生成より前）に 1 回呼ぶ。
/// プロセスの起動時に呼ぶには、エントリ ポイント（<c>Program.Main</c>）の先頭で
/// <see cref="TryEnsureAvailable"/> を呼ぶ。 終了時は <see cref="Shutdown"/> を呼ぶ。<br/>
/// <br/>
/// <b>自動初期化との関係</b>: プロジェクトが <c>WindowsPackageType=None</c> の実行ファイルである場合、
/// Windows App SDK は<b>モジュールの読み込み時</b>に Bootstrapper を自動で呼ぶ
/// （<c>WindowsAppSdkBootstrapInitialize</c>）。 **自動初期化はこのクラスが呼ばれるより先に走る**ため、
/// ランタイムが無い PC では自動初期化の時点で失敗し、本クラスの導入へ到達しない。
/// 導入の判断を本クラスへ寄せる場合は、プロジェクトで
/// <c>&lt;WindowsAppSdkBootstrapInitialize&gt;false&lt;/WindowsAppSdkBootstrapInitialize&gt;</c> にして
/// <b>本クラスだけ</b>が初期化する（止めない場合は本クラスを呼ぶ必要はない）。<br/>
/// <br/>
/// <b>版</b>: 初期化に使う版（<c>majorMinor</c>・版タグ・最小版）とインストーラーの版は、
/// 参照している <c>Microsoft.WindowsAppSDK</c> パッケージの値（<c>Microsoft.WindowsAppSDK.Release</c> /
/// <c>Microsoft.WindowsAppSDK.Runtime.Version</c>）から取る（ライブラリ側に版を書かない）。<br/>
/// <br/>
/// <b>Native AOT</b>: Bootstrapper は <c>LibraryImport</c>（ソース生成の P/Invoke）、案内 UI は CsWin32 が生成する
/// <c>TaskDialogIndirect</c> / <c>MessageBox</c> で呼び、インストーラーの取得は <see cref="HttpClient"/> と
/// <see cref="Process"/> だけで行う（リフレクションを使わないため、AOT で発行しても動作する。 実測 2026-11-05）。<br/>
/// <br/>
/// <b>UI スレッド</b>: 同期版は処理が終わるまで呼び出し元を止める。 UI を表示する前の起動処理で呼ぶか、
/// 非同期版（<see cref="TryEnsureAvailableAsync"/>）を使う。
/// </remarks>
public static partial class WindowsAppRuntimeBootstrap
{
    /// <summary>Bootstrapper のライブラリ名。</summary>
    /// <remarks>Windows App SDK のパッケージが出力へ複製する（自己完結の配置では複製されない）。</remarks>
    private const string BootstrapLibrary = "Microsoft.WindowsAppRuntime.Bootstrap.dll";

    /// <summary>ライブラリが見つからないことを表す HRESULT（<c>HRESULT_FROM_WIN32(ERROR_MOD_NOT_FOUND)</c>）。</summary>
    private const int ModuleNotFoundResult = unchecked((int)0x8007007E);

    /// <summary>Bootstrapper へ渡す版（パッケージ参照が決める）。</summary>
    private const uint MajorMinorVersion = SdkRelease.MajorMinor;

    /// <summary>Bootstrapper へ渡す版タグ（安定版では空）。</summary>
    private static readonly string s_versionTag = SdkRelease.VersionTag;

    /// <summary>Bootstrapper が要求するランタイムの最小版（パッケージ参照が決める）。</summary>
    private static readonly ulong s_minimumVersion = SdkRuntimeVersion.UInt64;

    /// <summary>インストーラーの配布 URL に含める版（例: <c>2.6.4</c>）。</summary>
    private static readonly string s_installerVersion =
        $"{SdkRuntimeVersion.Major}.{SdkRuntimeVersion.Minor}.{SdkRuntimeVersion.Build}";

    /// <summary>インストーラーの取得に使う接続（ライブラリで 1 つだけ持つ）。</summary>
    /// <remarks>制限時間は要求ごとに <see cref="CancellationTokenSource"/> で与える（接続の既定値は無期限にする）。</remarks>
    private static readonly HttpClient s_httpClient = new() { Timeout = Timeout.InfiniteTimeSpan };

    /// <summary>Bootstrapper で初期化したかどうか（<see cref="Shutdown"/> で解除する対象）。</summary>
    private static bool s_initialized;

    /// <summary>Bootstrapper を使わない配置かどうか（自己完結。 ランタイムは同封されている）。</summary>
    private static bool s_notApplicable;

    /// <summary>Windows App Runtime のダウンロード ページの URL を取得する。</summary>
    /// <value>Microsoft の公式ダウンロード ページ（インストーラーの入手先）。</value>
    /// <remarks>案内 UI に表示し、「ダウンロードページを表示する」を選んだときにブラウザーで開く URL である。</remarks>
    public static Uri DownloadPageUri => WindowsAppRuntimeDownloadPage.Uri;

    /// <summary>Bootstrapper で初期化済みかどうかを取得する。</summary>
    /// <value>初期化している場合、または初期化が不要な配置（自己完結・MSIX）の場合は true。</value>
    public static bool IsInitialized => s_initialized || s_notApplicable;

    /// <summary>Bootstrapper でランタイムを初期化する（導入も案内もしない）。</summary>
    /// <param name="errorCode">失敗した場合の HRESULT。 成功した場合は 0。</param>
    /// <returns>初期化できた（または初期化が不要な配置である）場合は true。</returns>
    /// <remarks>
    /// <b>案内 UI は出さない</b>（案内と導入は <see cref="TryEnsureAvailable"/> が行う）。 無ければ false を返す。
    /// </remarks>
    public static bool TryInitialize(out int errorCode) => TryInitialize(null, out errorCode);

    /// <summary>Bootstrapper でランタイムを初期化する（導入も案内もしない）。</summary>
    /// <param name="options">設定。 null の場合は既定。</param>
    /// <param name="errorCode">失敗した場合の HRESULT。 成功した場合は 0。</param>
    /// <returns>初期化できた（または初期化が不要な配置である）場合は true。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="options"/> の制限時間が正でない場合。</exception>
    /// <remarks>案内 UI と導入は行わない（<see cref="WindowsAppRuntimeInstallMode"/> は参照しない）。</remarks>
    public static bool TryInitialize(WindowsAppRuntimeBootstrapOptions? options, out int errorCode)
    {
        var effective = options ?? WindowsAppRuntimeBootstrapOptions.Default;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(effective.InstallerTimeout, TimeSpan.Zero);

        if (IsInitialized)
        {
            errorCode = 0;
            return true;
        }

        // MSIX で配布しているアプリはランタイムを同封しているため、初期化は何もしない（OnPackageIdentity_NOOP）。
        // **案内 UI は本ライブラリのダイアログが持つ**ため、Bootstrapper の案内 UI（OnNoMatch_ShowUI）は使わない
        var flags = BootstrapOptions.OnPackageIdentityNoOp;

        errorCode = InitializeCore(flags);
        if (errorCode < 0)
        {
            return false;
        }

        // **実際に Bootstrapper を呼んだときだけ**初期化済みにする。 自己完結の配置は同封した
        // ランタイムを使うため初期化しておらず、<see cref="Shutdown"/> が存在しない
        // Bootstrap ライブラリ（Microsoft.WindowsAppRuntime.Bootstrap.dll）を呼んでしまう。
        if (!s_notApplicable)
        {
            s_initialized = true;
        }

        return true;
    }

    /// <summary>ランタイムを用意する（無ければ設定に従って導入し、初期化する）。</summary>
    /// <param name="options">設定。 null の場合は既定（無ければダウンロードして導入する）。</param>
    /// <returns>用意できた場合は true。 導入しなかった・できなかった場合は false。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="options"/> の制限時間が正でない場合。</exception>
    /// <remarks>
    /// 初期化 → （失敗したら）設定に従って用意 → 初期化し直す、の順に試す。<br/>
    /// <b><see cref="WindowsAppRuntimeInstallMode.Download"/></b>: 案内を出さずに、公式のインストーラーを取得して実行する。<br/>
    /// <b><see cref="WindowsAppRuntimeInstallMode.BootstrapUi"/></b>: 案内 UI を出し、選択に従う
    /// （自動インストール / ダウンロード ページの表示 / 終了）。 ページを表示した場合は、利用者が導入してから
    /// 選び直せるよう<b>もう一度案内を出す</b>。<br/>
    /// <b><see cref="WindowsAppRuntimeInstallMode.None"/></b>: 初期化だけを試す。<br/>
    /// 導入したのに初期化できない場合は false を返す（同じ案内を繰り返さない）。
    /// </remarks>
    public static bool TryEnsureAvailable(WindowsAppRuntimeBootstrapOptions? options = null)
    {
        var effective = options ?? WindowsAppRuntimeBootstrapOptions.Default;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(effective.InstallerTimeout, TimeSpan.Zero);

        var installed = false;
        while (true)
        {
            if (TryInitialize(effective, out _))
            {
                return true;
            }

            if (installed)
            {
                // 導入したのに初期化できない（アーキテクチャの不一致など）。 案内を繰り返さない
                return false;
            }

            switch (effective.InstallMode)
            {
                case WindowsAppRuntimeInstallMode.Download:
                    installed = true;
                    if (!TryInstall(effective))
                    {
                        return false;
                    }

                    break;

                case WindowsAppRuntimeInstallMode.BootstrapUi:
                    switch (Ask(effective))
                    {
                        case WindowsAppRuntimeInstallChoice.Install:
                            installed = true;
                            if (!TryInstall(effective))
                            {
                                return false;
                            }

                            break;

                        case WindowsAppRuntimeInstallChoice.OpenDownloadPage:
                            OpenDownloadPage();
                            break;

                        default:
                            return false;
                    }

                    break;

                default:
                    return false;
            }
        }
    }

    /// <summary>ランタイムを用意する（無ければ設定に従って導入し、初期化する）非同期版。</summary>
    /// <param name="options">設定。 null の場合は既定（無ければダウンロードして導入する）。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>用意できた場合は true。 導入しなかった・できなかった場合は false。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="options"/> の制限時間が正でない場合。</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> がキャンセルされた場合。</exception>
    /// <remarks>
    /// 規則は <see cref="TryEnsureAvailable"/> と同じ。 取得とインストーラーの実行は待機を <c>await</c> で行う
    /// （案内 UI 自体は同期で出るため、その間だけ UI スレッドが止まる）。
    /// </remarks>
    public static async Task<bool> TryEnsureAvailableAsync(
        WindowsAppRuntimeBootstrapOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var effective = options ?? WindowsAppRuntimeBootstrapOptions.Default;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(effective.InstallerTimeout, TimeSpan.Zero);

        var installed = false;
        while (true)
        {
            if (TryInitialize(effective, out _))
            {
                return true;
            }

            if (installed)
            {
                return false;
            }

            switch (effective.InstallMode)
            {
                case WindowsAppRuntimeInstallMode.Download:
                    installed = true;
                    if (!await TryInstallAsync(effective, cancellationToken))
                    {
                        return false;
                    }

                    break;

                case WindowsAppRuntimeInstallMode.BootstrapUi:
                    switch (Ask(effective))
                    {
                        case WindowsAppRuntimeInstallChoice.Install:
                            installed = true;
                            if (!await TryInstallAsync(effective, cancellationToken))
                            {
                                return false;
                            }

                            break;

                        case WindowsAppRuntimeInstallChoice.OpenDownloadPage:
                            OpenDownloadPage();
                            break;

                        default:
                            return false;
                    }

                    break;

                default:
                    return false;
            }
        }
    }

    /// <summary>案内 UI を出して、利用者の選択を返す。</summary>
    /// <param name="options">設定（案内の実装と制限時間を持つ）。</param>
    /// <returns>利用者が選んだ結果。</returns>
    private static WindowsAppRuntimeInstallChoice Ask(WindowsAppRuntimeBootstrapOptions options)
    {
        var request = new WindowsAppRuntimeInstallPromptRequest
        {
            RequiredVersion = WindowsAppRuntimeInventory.RequiredVersion,
            InstalledVersion = WindowsAppRuntimeInventory.TryGetInstalledVersion(out var installed) ? installed : null,
            DownloadPageUri = DownloadPageUri,
        };

        return options.Prompt?.Invoke(request) ?? WindowsAppRuntimeInstallDialog.Ask(request);
    }

    /// <summary>ダウンロード ページを既定のブラウザーで開く。</summary>
    /// <remarks>開けなくても処理を続ける（利用者は案内に表示した URL を手で開ける）。</remarks>
    private static void OpenDownloadPage() => WindowsAppRuntimeDownloadPage.Open();

    /// <summary>Bootstrapper の初期化を解除する。</summary>
    /// <remarks>本クラスが初期化した場合だけ解除する（他が初期化した参照を減らさない）。</remarks>
    public static void Shutdown()
    {
        if (!s_initialized)
        {
            return;
        }

        MddBootstrapShutdown();
        s_initialized = false;
    }

    /// <summary>インストーラーの配布 URL を組み立てる。</summary>
    /// <returns>取得先。 プロセスのアーキテクチャに対応する配布物が無い場合は null。</returns>
    /// <remarks>
    /// 形式は Microsoft のダウンロード ページが示す
    /// <c>https://aka.ms/windowsappsdk/&lt;major.minor&gt;/&lt;version&gt;/windowsappruntimeinstall-&lt;arch&gt;.exe</c> である。
    /// </remarks>
    internal static Uri? CreateInstallerUri()
    {
        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            _ => null,
        };

        return architecture is null
            ? null
            : new Uri($"https://aka.ms/windowsappsdk/{SdkRelease.Major}.{SdkRelease.Minor}/{s_installerVersion}/windowsappruntimeinstall-{architecture}.exe");
    }

    /// <summary>ランタイムを導入する（初期化はしない）。</summary>
    /// <param name="options">設定。 null の場合は既定（公式インストーラーを取得して黙って実行する）。</param>
    /// <returns>導入できた場合は true。 取得・実行・時間切れで失敗した場合は false。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="options"/> の制限時間が正でない場合。</exception>
    /// <remarks>
    /// <b>利用者の確認を挟むための入口である</b>: 先に <see cref="WindowsAppRuntimeInventory.IsRequiredVersionInstalled"/> で
    /// 状態を調べ、必要だと分かってから本メソッドを呼ぶ（<see cref="TryEnsureAvailable"/> は確認なしに導入する）。<br/>
    /// 導入しただけでは初期化されないため、続けて <see cref="TryInitialize(out int)"/> を呼ぶか、
    /// <see cref="TryEnsureAvailable"/> を使う。<br/>
    /// <b>インストーラーは利用者単位で導入する</b>（管理者権限を要求しない）。
    /// </remarks>
    public static bool TryInstall(WindowsAppRuntimeBootstrapOptions? options = null)
    {
        var effective = options ?? WindowsAppRuntimeBootstrapOptions.Default;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(effective.InstallerTimeout, TimeSpan.Zero);

        var path = CreateInstallerPath();
        try
        {
            return TryDownload(effective, path)
                && RunInstaller(path, effective.InstallerTimeout) == 0;
        }
        finally
        {
            TryDelete(path);
        }
    }

    /// <summary>ランタイムを導入する（初期化はしない）非同期版。</summary>
    /// <param name="options">設定。 null の場合は既定（公式インストーラーを取得して黙って実行する）。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>導入できた場合は true。 取得・実行・時間切れで失敗した場合は false。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="options"/> の制限時間が正でない場合。</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> がキャンセルされた場合。</exception>
    /// <remarks>取得と実行は待機を <c>await</c> で行う（UI スレッドを止めない。 インストーラー自体は画面を出さない）。</remarks>
    public static async Task<bool> TryInstallAsync(
        WindowsAppRuntimeBootstrapOptions? options = null, CancellationToken cancellationToken = default)
    {
        var effective = options ?? WindowsAppRuntimeBootstrapOptions.Default;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(effective.InstallerTimeout, TimeSpan.Zero);

        var path = CreateInstallerPath();
        try
        {
            return await TryDownloadAsync(effective, path, cancellationToken)
                && await RunInstallerAsync(path, effective.InstallerTimeout, cancellationToken) == 0;
        }
        finally
        {
            TryDelete(path);
        }
    }

    /// <summary>インストーラーを一時フォルダへ取得する。</summary>
    /// <param name="options">設定。</param>
    /// <param name="path">書き込み先。</param>
    /// <returns>取得できた場合は true。</returns>
    private static bool TryDownload(WindowsAppRuntimeBootstrapOptions options, string path)
    {
        if (ResolveInstallerUri(options) is not { } uri)
        {
            return false;
        }

        try
        {
            using var timeout = new CancellationTokenSource(options.InstallerTimeout);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = s_httpClient.Send(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            using var source = response.Content.ReadAsStream(timeout.Token);
            using var target = File.Create(path);

            // **本文のコピーにも制限時間を効かせる**: `ResponseHeadersRead` はヘッダーを受け取った時点で
            // 戻るため、本文を止める接続では `CopyTo` が終わらず起動処理が止まり続ける。
            source.CopyToAsync(target, timeout.Token).GetAwaiter().GetResult();
            return true;
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // 取得できない（通信の遮断・配布物の移動・一時フォルダの拒否）ときは導入しない。
            // 呼び出し側は false を受け取り、利用者へ案内する手段（InstallMode.BootstrapUi）へ切り替えられる
            return false;
        }
    }

    /// <summary>インストーラーを一時フォルダへ取得する非同期版。</summary>
    /// <param name="options">設定。</param>
    /// <param name="path">書き込み先。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>取得できた場合は true。</returns>
    private static async Task<bool> TryDownloadAsync(
        WindowsAppRuntimeBootstrapOptions options, string path, CancellationToken cancellationToken)
    {
        if (ResolveInstallerUri(options) is not { } uri)
        {
            return false;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.InstallerTimeout);
            using var response = await s_httpClient.GetAsync(
                uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            using var source = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var target = File.Create(path);
            await source.CopyToAsync(target, timeout.Token);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 呼び出し側のキャンセルはそのまま伝える（時間切れだけを「導入できなかった」として扱う）
            throw;
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // 取得できない（通信の遮断・配布物の移動・一時フォルダの拒否）ときは導入しない。
            // 呼び出し側は false を受け取り、利用者へ案内する手段（InstallMode.BootstrapUi）へ切り替えられる
            return false;
        }
    }

    /// <summary>インストーラーを黙って実行する。</summary>
    /// <param name="path">インストーラーのパス。</param>
    /// <param name="timeout">実行の制限時間。</param>
    /// <returns>インストーラーの終了コード。 起動できない・時間切れの場合は -1。</returns>
    private static int RunInstaller(string path, TimeSpan timeout)
    {
        using var process = StartInstaller(path);
        if (process is null)
        {
            return -1;
        }

        return process.WaitForExit(ToMilliseconds(timeout))
            ? process.ExitCode
            : Terminate(process);
    }

    /// <summary>インストーラーを黙って実行する非同期版。</summary>
    /// <param name="path">インストーラーのパス。</param>
    /// <param name="timeout">実行の制限時間。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>インストーラーの終了コード。 起動できない・時間切れの場合は -1。</returns>
    private static async Task<int> RunInstallerAsync(
        string path, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var process = StartInstaller(path);
        if (process is null)
        {
            return -1;
        }

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(limit.Token);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            // 時間切れでも利用者のキャンセルでも、インストーラーを残さない
            var exitCode = Terminate(process);
            cancellationToken.ThrowIfCancellationRequested();
            return exitCode;
        }
    }

    /// <summary>インストーラーを（窓を出さずに）起動する。</summary>
    /// <param name="path">インストーラーのパス。</param>
    /// <returns>起動したプロセス。 起動できない場合は null。</returns>
    /// <remarks><c>--quiet</c> は Windows App Runtime のインストーラーが解釈する「画面を出さない」指定である。</remarks>
    private static Process? StartInstaller(string path)
    {
        try
        {
            var startInfo = new ProcessStartInfo(path)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("--quiet");
            return Process.Start(startInfo);
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // 起動できない（ファイルが消えた・実行が拒否された）ときは導入しなかったものとして扱う
            return null;
        }
    }

    /// <summary>時間切れのインストーラーを終了させる。</summary>
    /// <param name="process">対象のプロセス。</param>
    /// <returns>常に -1（失敗として扱う）。</returns>
    private static int Terminate(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // 既に終了している場合は終了できない（結果は失敗として扱えば足りる）
        }

        return -1;
    }

    /// <summary>インストーラーの取得先を決める（設定が優先）。</summary>
    /// <param name="options">設定。</param>
    /// <returns>取得先。 決められない場合は null。</returns>
    private static Uri? ResolveInstallerUri(WindowsAppRuntimeBootstrapOptions options)
        => options.InstallerSource ?? CreateInstallerUri();

    /// <summary>インストーラーの一時ファイルのパスを作る。</summary>
    /// <returns>他の実行と衝突しないパス。</returns>
    private static string CreateInstallerPath()
        => Path.Combine(
            Path.GetTempPath(),
            $"windowsappruntimeinstall-{Environment.ProcessId}-{Guid.NewGuid():N}.exe");

    /// <summary>一時ファイルを消す（消せなくても処理を続ける）。</summary>
    /// <param name="path">対象のパス。</param>
    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // 一時フォルダの掃除は OS に任せる（導入の成否へ影響させない）
        }
    }

    /// <summary>制限時間をミリ秒へ直す（<see cref="int"/> の範囲へ収める）。</summary>
    /// <param name="timeout">制限時間。</param>
    /// <returns>ミリ秒。</returns>
    private static int ToMilliseconds(TimeSpan timeout)
        => (int)Math.Clamp(timeout.TotalMilliseconds, 1, int.MaxValue);

    /// <summary>Bootstrapper を呼ぶ。</summary>
    /// <param name="options">Bootstrapper の動作。</param>
    /// <returns>HRESULT。</returns>
    /// <remarks>
    /// <b>自己完結の配置</b>（<c>WindowsAppSDKSelfContained=true</c>）ではランタイムを同封しているため
    /// 初期化は不要であり、成功として扱う（同封物は <c>Microsoft.WindowsAppRuntime.dll</c> の有無で判定する）。<br/>
    /// <b>ライブラリが無い</b>場合は配置の誤りであるため、成功にしない（後で XAML の初期化に失敗するだけになる）。
    /// </remarks>
    private static int InitializeCore(BootstrapOptions options)
    {
        if (IsSelfContained())
        {
            s_notApplicable = true;
            return 0;
        }

        try
        {
            return MddBootstrapInitialize2(MajorMinorVersion, s_versionTag, s_minimumVersion, options);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Bootstrapper のライブラリが読み込めない（出力への複製漏れなど）。 用意できなかったものとして失敗を返す
            return ModuleNotFoundResult;
        }
    }

    /// <summary>自己完結の配置かどうかを判定する。</summary>
    /// <returns>ランタイムを同封している場合は true。</returns>
    /// <remarks>
    /// 自己完結の配置はランタイムの本体（<c>Microsoft.WindowsAppRuntime.dll</c>）をアプリのフォルダーへ
    /// 複製する（フレームワーク依存の配置は複製しない。 実測 2026-11-05）。
    /// </remarks>
    private static bool IsSelfContained()
        => File.Exists(Path.Combine(AppContext.BaseDirectory, "Microsoft.WindowsAppRuntime.dll"));

    /// <summary>Bootstrapper で初期化する（<c>MddBootstrapInitialize2</c>）。</summary>
    /// <param name="majorMinorVersion">版（<c>0xMMMMNNNN</c>）。</param>
    /// <param name="versionTag">版タグ（安定版は空）。</param>
    /// <param name="minVersion">最小版（<c>0xMMMMNNNNBBBBRRRR</c>）。</param>
    /// <param name="options">動作。</param>
    /// <returns>HRESULT。</returns>
    /// <remarks>
    /// 探索先はアプリのフォルダー（と <c>runtimes/&lt;RID&gt;/native</c>）に限る。 他の層は
    /// <c>System32</c> だけに固定しているが、Bootstrapper のライブラリは Windows App SDK のパッケージが
    /// アプリの出力へ複製するため、<c>System32</c> だけでは見つからない（[`コーディング規約.md`] の CA5392 の方針）。
    /// </remarks>
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport(BootstrapLibrary, EntryPoint = "MddBootstrapInitialize2", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MddBootstrapInitialize2(
        uint majorMinorVersion, string versionTag, ulong minVersion, BootstrapOptions options);

    /// <summary>Bootstrapper の初期化を解除する（<c>MddBootstrapShutdown</c>）。</summary>
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport(BootstrapLibrary, EntryPoint = "MddBootstrapShutdown")]
    private static partial void MddBootstrapShutdown();

    /// <summary>Bootstrapper の動作（<c>MddBootstrapInitializeOptions</c>）。</summary>
    [Flags]
    private enum BootstrapOptions : uint
    {
        /// <summary>パッケージ ID を持つアプリでは何もしない（MSIX で配布している場合）。</summary>
        OnPackageIdentityNoOp = 0x0010,
    }
}
