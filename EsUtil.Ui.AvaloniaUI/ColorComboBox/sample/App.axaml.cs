using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EsUtil.Ui.AvaloniaUI.ColorComboBox.Sample.ViewModels;
using EsUtil.Ui.AvaloniaUI.ColorComboBox.Sample.Views;

namespace EsUtil.Ui.AvaloniaUI.ColorComboBox.Sample;

/// <summary>サンプルアプリケーションの Application 定義。</summary>
/// <remarks>
/// メインウィンドウを <see cref="MainWindow"/> として表示する。<br/>
/// DataContext は <see cref="MainWindowViewModel"/> を使用する。<br/>
/// </remarks>
public partial class App : Application
{
    /// <summary>アプリケーション初期化。 AXAML をロードする。</summary>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>フレームワーク初期化後の処理。 メインウィンドウを表示する。</summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
