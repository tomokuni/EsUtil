using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EsUtil.Feature.Loupe.AvaloniaUI.Sample.Views;

namespace EsUtil.Feature.Loupe.AvaloniaUI.Sample;

/// <summary>Avalonia アプリケーション本体。</summary>
public sealed partial class App : Application
{
    /// <summary>XAML を読み込んでリソース・スタイルを適用する。</summary>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>フレームワーク初期化完了後にメインウィンドウを表示する。</summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
