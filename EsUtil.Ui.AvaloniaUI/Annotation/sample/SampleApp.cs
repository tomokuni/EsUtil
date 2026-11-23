using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;

namespace EsUtil.Ui.AvaloniaUI.Annotation.Sample;

/// <summary>サンプルのアプリケーション（テーマとメイン ウィンドウを用意する）。</summary>
internal sealed class SampleApp : Application
{
    /// <summary>フレームワークの初期化が完了したときにメイン ウィンドウを表示する。</summary>
    public override void OnFrameworkInitializationCompleted()
    {
        Styles.Add(new FluentTheme());

        // ColorSelectorControl は ColorPicker のテーマを要求する（同梱しないのでホストが読み込む）
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
