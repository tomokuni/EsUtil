using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using EsUtil.Feature.Marquee.AvaloniaUI.Internal;
using Xunit;

namespace EsUtil.Feature.Marquee.AvaloniaUI.Tests.Internal;

/// <summary><see cref="ScreenConverter"/> の単体テスト（ローカル DIP ↔ 画面物理）。</summary>
/// <remarks>
/// <see cref="Window"/>（<c>TopLevel</c> 接続）を使うテストは <c>[AvaloniaFact]</c> で直列実行する。<br/>
/// <see cref="Panel"/> の未接続（<c>TopLevel</c> 非接続）のテストは <c>[Fact]</c> で並列実行する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class ScreenConverterTests
{
    [AvaloniaFact]
    internal void ScreenToLocalDip_ウィンドウ内のコントロールで逆変換できる()
    {
        var window = new Window { Width = 300, Height = 300 };
        var panel = new Panel { Width = 200, Height = 200 };
        window.Content = panel;
        window.Show();

        // ローカル (30, 40) → 画面物理 → ローカル DIP の往復（ヘッドレスはスケーリング 1.0・原点 (0,0)）
        var screen = ScreenConverter.LocalDipToScreen(panel, new Point(30, 40));
        var local = ScreenConverter.ScreenToLocalDip(panel, screen);

        Assert.Equal(30, local.X, 6);
        Assert.Equal(40, local.Y, 6);
        window.Close();
    }
}

/// <summary><see cref="ScreenConverter"/> のビジュアルツリー未接続テスト（<see cref="Control"/> 未接続・並列実行）。</summary>
/// <remarks>
/// <see cref="Panel"/> を生成するが <see cref="TopLevel"/> に接続しないため、<c>TopLevel.GetTopLevel</c> は null を返し等価変換される。<br/>
/// <c>Controls</c> を <c>Window</c> に表示しないことから UI スレッド（Dispatcher）を必要とせず <c>[Fact]</c> で並列実行できる。
/// </remarks>
public class ScreenConverterUnconnectedTests
{
    [Fact]
    internal void ScreenToLocalDip_ビジュアルツリー未接続時は等価変換する()
    {
        var control = new Panel();

        var p = ScreenConverter.ScreenToLocalDip(control, new PixelPoint(10, 20));

        Assert.Equal(new Point(10, 20), p);
    }
}
