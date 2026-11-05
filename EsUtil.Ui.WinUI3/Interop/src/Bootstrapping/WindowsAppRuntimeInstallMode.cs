namespace EsUtil.Ui.WinUI3.Interop.Bootstrapping;

/// <summary>Windows App Runtime が無いときに、どの方法で導入するかを表す。</summary>
/// <remarks>
/// <see cref="WindowsAppRuntimeBootstrap"/> が Bootstrapper（<c>MddBootstrapInitialize2</c>）で
/// 一致するランタイムを見つけられなかったときの動作である。
/// </remarks>
public enum WindowsAppRuntimeInstallMode
{
    /// <summary>導入しない（初期化だけを試し、失敗したら false を返す）。</summary>
    /// <remarks>利用者へ確認する仕組みをアプリが自前で持つ場合に使う。</remarks>
    None = 0,

    /// <summary>案内 UI を出し、利用者の選択に従う（自動インストール / ページの表示 / 終了）。</summary>
    /// <remarks>
    /// 案内には<b>ダウンロード ページの URL</b> と、次の 3 つの選択肢を出す。<br/>
    /// ・「自動インストールする」… 公式のインストーラーを取得して黙って実行する。<br/>
    /// ・「ダウンロードページを表示する」… ダウンロード ページをブラウザーで開き、もう一度案内を出す（手動で導入したあと選び直せる）。<br/>
    /// ・「インストールせずにアプリを終了する」… 何もせずに false を返す（アプリは終了する）。<br/>
    /// 案内の実装は <see cref="WindowsAppRuntimeBootstrapOptions.Prompt"/> で差し替えられる
    /// （既定は Win32 のタスク ダイアログ。 使えない環境ではメッセージ ボックス）。
    /// </remarks>
    BootstrapUi = 1,

    /// <summary>公式のインストーラーを取得して黙って実行する（利用者の操作を要さない）。</summary>
    /// <remarks>
    /// 取得先は Microsoft の配布 URL（<c>https://aka.ms/windowsappsdk/...</c>）であり、
    /// <see cref="WindowsAppRuntimeBootstrapOptions.InstallerSource"/> で差し替えられる。<br/>
    /// インストーラーは利用者単位で導入するため、管理者権限を要求しない。
    /// </remarks>
    Download = 2,
}
