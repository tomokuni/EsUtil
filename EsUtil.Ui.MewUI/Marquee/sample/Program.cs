using Aprillz.MewUI;
using EsUtil.Ui.MewUI.Marquee.Sample.Views;

namespace EsUtil.Ui.MewUI.Marquee.Sample;

/// <summary>サンプルアプリケーションのエントリポイント。</summary>
/// <remarks>
/// <b>必須の初期化</b>:<br/>
/// 1. <c>DpiHelper.EnablePerMonitorDpiAwareness()</c>（DPI 対応。 呼ばないと OS の DPI クエリが 96 に仮想化される）<br/>
/// 2. <c>Direct2DBackend.Register()</c> / <c>Win32Platform.Register()</c>（描画バックエンドとプラットフォームホストの明示登録）<br/>
/// コントロールの描画はフレームワーク標準（Direct2D）が担うため、1〜2 で足りる。
/// </remarks>
internal static class Program
{
    /// <summary>サンプルアプリケーションを起動する。</summary>
    [STAThread]
    private static void Main()
    {
        // Per-Monitor V2 の DPI 対応を有効化する（アプリ起動時に呼ぶ必要がある）
        DpiHelper.EnablePerMonitorDpiAwareness();

        // 描画バックエンドとプラットフォームホストを明示登録する（メタパッケージでは自動登録されない）
        Direct2DBackend.Register();
        Win32Platform.Register();


        Application.Run(MainWindow.Create());
    }
}
