using Microsoft.UI.Xaml;

namespace EsUtil.Ui.WinUI3.ColumnPacking.Sample;

/// <summary>サンプル アプリ（アンパッケージ）のアプリケーション エントリ。</summary>
/// <remarks>ウィンドウの生成と表示だけを行う（サンプルの内容は <see cref="MainWindow"/> が持つ）。</remarks>
public partial class App : Application
{
    /// <summary>メイン ウィンドウ（アプリの寿命の間だけ保持する）。</summary>
    private Window? _window;

    /// <summary>アプリケーションを初期化する。</summary>
    public App() => InitializeComponent();

    /// <summary>メイン ウィンドウを生成して表示する。</summary>
    /// <param name="args">起動引数。</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}