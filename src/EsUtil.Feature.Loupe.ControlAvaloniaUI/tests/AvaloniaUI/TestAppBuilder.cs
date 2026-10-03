using Avalonia;
using Avalonia.Headless;
using EsUtil.Feature.Loupe.AvaloniaUI.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace EsUtil.Feature.Loupe.AvaloniaUI.Tests;

/// <summary>Avalonia ヘッドレステストのアプリケーションビルダー（アセンブリ属性方式）。</summary>
/// <remarks>
/// <c>[AvaloniaFact]</c> / <c>[AvaloniaTheory]</c> のテストは本クラスが提供する AppBuilder で
/// Avalonia を初期化する（<see cref="Avalonia.Headless.AvaloniaTestApplicationAttribute"/> アセンブリ属性）。<br/>
/// Skia を有効化することで WriteableBitmap のピクセル操作（書き込み・読み取り）が利用可能になる。<br/>
/// テストは全て UI スレッドで実行されるため、コントロール生成や描画操作が可能になる。<br/>
/// </remarks>
public class TestAppBuilder
{
    /// <summary>Avalonia ヘッドレステスト用の AppBuilder を構成する。</summary>
    /// <returns>構成済みの AppBuilder。</returns>
    /// <remarks>
    /// ヘッドレスプラットフォーム（AvaloniaHeadlessPlatformOptions）と Skia レンダラーを有効化する。<br/>
    /// アプリケーションはテスト用の最小構成（<see cref="Application"/>）を使用する。<br/>
    /// </remarks>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<Application>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

