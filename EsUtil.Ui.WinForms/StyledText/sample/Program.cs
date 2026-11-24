using EsUtil.Ui.WinForms.StyledText.Sample.Views;

namespace EsUtil.Ui.WinForms.StyledText.Sample;

/// <summary>サンプルアプリケーションのエントリポイント。</summary>
/// <remarks>
/// <b>高 DPI の指定</b>: コントロールは <c>Control.DeviceDpi</c> から実効倍率を解決するため、
/// アプリケーションが高 DPI に対応している（DPI 認識を持つ）ことが前提になる。
/// </remarks>
internal static class Program
{
    /// <summary>アプリケーションを起動する。</summary>
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Application.Run(MainForm.Create());
    }
}
