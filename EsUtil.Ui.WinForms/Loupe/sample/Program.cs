using EsUtil.Ui.WinForms.Loupe.Sample.Views;

namespace EsUtil.Ui.WinForms.Loupe.Sample;

/// <summary>サンプルアプリケーションのエントリポイント。</summary>
/// <remarks>
/// <b>高 DPI の指定</b>: ルーペは <c>Control.DeviceDpi</c> から実効倍率を解決するため、<br/>
/// アプリケーションが高 DPI に対応している（DPI 認識を持つ）ことが前提になる。<br/>
/// 指定はウィンドウを生成する前に 1 度だけ行う（WinForms の要件）。
/// </remarks>
internal static class Program
{
    /// <summary>アプリケーションを起動する。</summary>
    [STAThread]
    private static void Main()
    {
        // モニターごとの DPI へ追従する（DPI の変更通知を受けてコントロールが倍率を更新する）
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Application.Run(new MainForm());
    }
}
