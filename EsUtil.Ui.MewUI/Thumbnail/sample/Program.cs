using Aprillz.MewUI;
using EsUtil.Ui.MewUI.Thumbnail.Sample.Views;

namespace EsUtil.Ui.MewUI.Thumbnail.Sample;

/// <summary>サンプルアプリケーションのエントリポイント。</summary>
/// <remarks>
/// <b>必須の初期化</b>:<br/>
/// 1. <c>DpiHelper.EnablePerMonitorDpiAwareness()</c>（DPI 対応）<br/>
/// 2. <c>Direct2DBackend.Register()</c> / <c>Win32Platform.Register()</c>（描画バックエンドとプラットフォームホストの明示登録）
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