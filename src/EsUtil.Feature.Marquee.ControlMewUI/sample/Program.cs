using Aprillz.MewUI;
using Aprillz.MewUI.Skia.Interop;
using EsUtil.Feature.Marquee.MewUI.Sample.Views;

namespace EsUtil.Feature.Marquee.MewUI.Sample;

/// <summary>サンプルアプリケーションのエントリポイント。</summary>
/// <remarks>
/// <b>必須の初期化</b>:<br/>
/// 1. <c>DpiHelper.EnablePerMonitorDpiAwareness()</c>（DPI 対応。 呼ばないと OS の DPI クエリが 96 に仮想化される）<br/>
/// 2. <c>Direct2DBackend.Register()</c> / <c>Win32Platform.Register()</c>（描画バックエンドとプラットフォームホストの明示登録）<br/>
/// 3. <c>SkiaDirect2DInterop.Register()</c>（Direct2D ⇔ Skia の GPU ブリッジ。 未登録でも動作するが描画が遅い）<br/>
/// コントロールの描画は Skia（<c>SkiaCanvasView</c>）が担うため、1〜3 のすべてが必要になる。
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

        // Direct2D ⇔ Skia のブリッジを有効化する（未登録だと Skia 描画が毎フレーム CPU 転送になる）
        SkiaDirect2DInterop.Register();

        Application.Run(MainWindow.Create());
    }
}
