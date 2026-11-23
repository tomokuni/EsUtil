using Avalonia;
using Avalonia.Headless;
using EsUtil.Ui.AvaloniaUI.Annotation.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace EsUtil.Ui.AvaloniaUI.Annotation.Tests;

/// <summary>Avalonia ヘッドレステストのアプリケーションビルダー（アセンブリ属性方式）。</summary>
/// <remarks>
/// <c>[AvaloniaFact]</c> のテストは本クラスが提供する AppBuilder で Avalonia を初期化する。<br/>
/// Skia を有効化することで <c>RenderTargetBitmap</c> の描画と <c>WriteableBitmap</c> の画素操作が利用可能になる。
/// </remarks>
public static class TestAppBuilder
{
    /// <summary>Avalonia アプリケーションビルダーを構成する。</summary>
    /// <returns>構成済みの AppBuilder。</returns>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<Application>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
