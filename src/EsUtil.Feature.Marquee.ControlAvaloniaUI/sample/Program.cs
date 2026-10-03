using Avalonia;

namespace EsUtil.Feature.Marquee.AvaloniaUI.Sample;

/// <summary>アプリケーションのエントリポイント。</summary>
internal static class Program
{
    /// <summary>Avalonia アプリケーションをデスクトップモードで起動する。</summary>
    [STAThread]
    public static void Main(string[] args)
        => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>Avalonia アプリケーションビルダーを構成する。</summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
