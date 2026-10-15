using Microsoft.UI.Xaml;

namespace EsUtil.Feature.Marquee.WinUI3.Sample;

/// <summary>サンプル アプリ（アンパッケージ）のアプリケーション エントリ。</summary>
/// <remarks>
/// ウィンドウの生成と活性化のみを行う（アプリ固有のロジックは <see cref="MainWindow"/> が持つ）。<br/>
/// アンパッケージで実行するため、起動時の Windows App SDK の初期化は自動初期化（ブートストラップ）に委ねる。
/// </remarks>
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
