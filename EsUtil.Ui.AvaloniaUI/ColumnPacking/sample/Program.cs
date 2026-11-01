using Avalonia;

namespace EsUtil.Ui.AvaloniaUI.ColumnPacking.Sample;

/// <summary>アプリケーションのエントリポイント。</summary>
internal static class Program
{
    /// <summary>Avalonia アプリケーションをデスクトップモードで起動する。</summary>
    /// <param name="args">コマンドライン引数。</param>
    [STAThread]
    public static void Main(string[] args)
        => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>Avalonia アプリケーションビルダーを構成する。</summary>
    /// <returns>構成したビルダー。</returns>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
