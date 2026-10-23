using Avalonia;

namespace EsUtil.Ui.AvaloniaUI.ColorComboBox.Sample;

/// <summary>ColorComboBoxControl サンプルアプリケーションのエントリポイント。</summary>
internal static class Program
{
    /// <summary>アプリケーションのエントリポイント。</summary>
    /// <param name="args">コマンドライン引数。</param>
    [STAThread]
    private static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    /// <summary>Avalonia AppBuilder を構築する。</summary>
    /// <returns>構成済みの AppBuilder。</returns>
    /// <remarks>
    /// Fluent テーマ・Inter フォントを有効化し、デバッグ構成では開発者ツールを添付する。<br/>
    /// ReactiveUI は使用しないが、AppBuilder の標準構成として含める。<br/>
    /// </remarks>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}
