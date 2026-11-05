namespace EsUtil.Ui.WinUI3.Interop.Bootstrapping;

/// <summary>利用者が「Windows App Runtime が必要」という案内で選んだ結果。</summary>
/// <remarks>
/// <see cref="WindowsAppRuntimeInstallMode.BootstrapUi"/> のときに案内 UI が返す値であり、
/// <see cref="WindowsAppRuntimeBootstrap"/> はこの値に従って導入・ページの表示・終了を行う。
/// </remarks>
public enum WindowsAppRuntimeInstallChoice
{
    /// <summary>自動でインストールする（公式のインストーラーを取得して実行する）。</summary>
    Install = 0,

    /// <summary>ダウンロード ページを表示する（利用者が手動で導入できるようにする）。</summary>
    /// <remarks>表示した後は、もう一度案内を出す（利用者が導入してから「自動インストールする」を選べる）。</remarks>
    OpenDownloadPage = 1,

    /// <summary>インストールせずにアプリを終了する。</summary>
    Exit = 2,
}
