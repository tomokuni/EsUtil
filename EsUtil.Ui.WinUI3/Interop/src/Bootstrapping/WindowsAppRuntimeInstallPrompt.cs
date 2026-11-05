namespace EsUtil.Ui.WinUI3.Interop.Bootstrapping;

/// <summary>「Windows App Runtime が必要」という案内を表示し、利用者の選択を返す処理。</summary>
/// <param name="request">案内に出す情報（必要とする版・インストール済みの版・ダウンロード ページ）。</param>
/// <returns>利用者が選んだ結果。</returns>
/// <remarks>
/// <see cref="WindowsAppRuntimeInstallMode.BootstrapUi"/> のときに <see cref="WindowsAppRuntimeBootstrap"/> が呼ぶ。<br/>
/// <see cref="WindowsAppRuntimeBootstrapOptions.Prompt"/> を設定すると、アプリが自前の UI（WinUI のダイアログ等）で
/// 尋ねられる（既定は Win32 のタスク ダイアログ。 使えない環境ではメッセージ ボックスへ落ちる）。
/// </remarks>
public delegate WindowsAppRuntimeInstallChoice WindowsAppRuntimeInstallPrompt(WindowsAppRuntimeInstallPromptRequest request);
