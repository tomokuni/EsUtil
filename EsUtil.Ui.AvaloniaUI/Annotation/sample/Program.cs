using Avalonia;

namespace EsUtil.Ui.AvaloniaUI.Annotation.Sample;

/// <summary>サンプル アプリケーションのエントリポイント。</summary>
internal static class Program
{
    /// <summary>Avalonia アプリケーションをデスクトップ モードで起動する。</summary>
    /// <param name="args">起動引数。</param>
    [STAThread]
    public static void Main(string[] args)
        => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>Avalonia のアプリケーション ビルダーを構成する。</summary>
    /// <returns>構成したビルダー。</returns>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<SampleApp>()
            .UsePlatformDetect()
            .LogToTrace();
}
