using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using EsUtil.Ui.AvaloniaUI.StyledText.Sample.Views;

namespace EsUtil.Ui.AvaloniaUI.StyledText.Sample;

/// <summary>Avalonia アプリケーション本体。</summary>
public sealed partial class App : Application
{
    /// <summary>XAML を読み込んでリソース・スタイルを適用する。</summary>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>フレームワーク初期化完了後にメインウィンドウを表示する。</summary>
    public override void OnFrameworkInitializationCompleted()
    {
        // **カラーピッカーのテーマを読み込む**: 書式バーの色の選択は ColorView を使うため、
        // ColorPicker のテーマが無いと中身が描かれない（枠とタブだけが出る）。
        Styles.Add(new StyleInclude(new Uri("avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml"))
        {
            Source = new Uri("avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml"),
        });

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}