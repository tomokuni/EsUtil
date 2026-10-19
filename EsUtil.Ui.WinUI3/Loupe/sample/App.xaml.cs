using Microsoft.UI.Xaml;

namespace EsUtil.Ui.WinUI3.Loupe.Sample;

/// <summary>サンプル アプリケーションのエントリ（既定のリソースとメイン ウィンドウを用意する）。</summary>
/// <remarks>
/// 実行ファイルはアンパッケージ（MSIX なし）で起動する（<c>WindowsPackageType=None</c>）ため、<br/>
/// Windows App SDK のブートストラップは SDK が生成する自動初期化へ任せる。
/// </remarks>
public partial class App : Application
{
    /// <summary>表示中のメイン ウィンドウ。</summary>
    private Window? _window;

    /// <summary>アプリケーションを初期化する。</summary>
    public App() => InitializeComponent();

    /// <summary>メイン ウィンドウを生成して表示する。</summary>
    /// <param name="args">起動引数。</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new Views.MainWindow();
        _window.Activate();
    }
}
