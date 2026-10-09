using Avalonia;
using Avalonia.Headless;
using EsUtil.Feature.Loupe.AvaloniaUI.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace EsUtil.Feature.Loupe.AvaloniaUI.Tests;

/// <summary>Avalonia ヘッドレステストのアプリケーションビルダー（アセンブリ属性方式）。</summary>
/// <remarks>
/// <c>[AvaloniaFact]</c> / <c>[AvaloniaTheory]</c> のテストは本クラスが提供する AppBuilder で
/// Avalonia を初期化する（<see cref="Avalonia.Headless.AvaloniaTestApplicationAttribute"/> アセンブリ属性）。<br/>
/// Skia を有効化し、<b>ヘッドレス描画を無効化</b>（<c>UseHeadlessDrawing = false</c>）することで
/// WriteableBitmap のピクセル操作（書き込み・読み取り）と実 Skia の描画（変換・エンコード）が動作する。<br/>
/// 既定（無効化しない場合）は描画がスタブになり、<c>new Bitmap(PixelFormat, AlphaFormat, IntPtr, ...)</c> が
/// 1×1 を返すため、寸法に依存するテストや変換の計測が成立しない。
/// </remarks>
public static class TestAppBuilder
{
    /// <summary>Avalonia ヘッドレステスト用の AppBuilder を構成する。</summary>
    /// <returns>構成済みの AppBuilder。</returns>
    /// <remarks>
    /// ヘッドレスプラットフォーム（AvaloniaHeadlessPlatformOptions）と Skia レンダラーを有効化し、
    /// <c>UseHeadlessDrawing = false</c> で実 Skia の描画を使う。<br/>
    /// アプリケーションはテスト用の最小構成（<see cref="Application"/>）を使用する。<br/>
    /// </remarks>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<Application>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

