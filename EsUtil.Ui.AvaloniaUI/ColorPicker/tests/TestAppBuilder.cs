using Avalonia;
using Avalonia.Headless;
using EsUtil.Ui.AvaloniaUI.ColorPicker.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace EsUtil.Ui.AvaloniaUI.ColorPicker.Tests;

/// <summary>Avalonia ヘッドレステストのアプリケーションビルダー（アセンブリ属性方式）。</summary>
/// <remarks>
/// <c>[AvaloniaFact]</c> / <c>[AvaloniaTheory]</c> のテストは本クラスが提供する AppBuilder で
/// Avalonia を初期化する（<see cref="Avalonia.Headless.AvaloniaTestApplicationAttribute"/> アセンブリ属性）。<br/>
/// Skia を有効化することで WriteableBitmap のピクセル操作（書き込み・読み取り）が利用可能になる。<br/>
/// テストは全て UI スレッドで実行されるため、コントロール生成やレイアウト操作が可能になる。<br/>
/// 注: FluentTheme 等のテーマを Application に追加するとヘッドレステストランナーの
/// ApplicationLifetime 管理と競合するため、テーマは追加しない（ComboBox テンプレートは適用されない）。<br/>
/// </remarks>
public static class TestAppBuilder
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

