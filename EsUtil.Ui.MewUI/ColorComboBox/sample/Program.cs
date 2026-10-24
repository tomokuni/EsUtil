using Aprillz.MewUI;
using EsUtil.Ui.MewUI.ColorComboBox.Sample.Views;

namespace EsUtil.Ui.MewUI.ColorComboBox.Sample;

/// <summary>サンプルアプリケーションのエントリポイント。</summary>
/// <remarks>
/// <b>必須の初期化</b>: <c>DpiHelper.EnablePerMonitorDpiAwareness()</c>、
/// <c>Direct2DBackend.Register()</c>、<c>Win32Platform.Register()</c>（他の MewUI サンプルと同じ）。
/// </remarks>
internal static class Program
{
    /// <summary>サンプルアプリケーションを起動する。</summary>
    [STAThread]
    private static void Main()
    {
        DpiHelper.EnablePerMonitorDpiAwareness();
        Direct2DBackend.Register();
        Win32Platform.Register();

        Application.Run(MainWindow.Create());
    }
}