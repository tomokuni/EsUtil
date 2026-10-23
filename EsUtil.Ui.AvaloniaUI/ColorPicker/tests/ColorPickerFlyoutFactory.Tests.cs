using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using EsUtil.Ui.AvaloniaUI.Interop;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EsUtil.Ui.AvaloniaUI.ColorPicker.Tests;

/// <summary><see cref="ColorPickerFlyoutFactory"/> の単体テスト（OK / キャンセル動作・初期色）。</summary>
/// <remarks>
/// ColorView の生成を含むため、<br/>
/// Avalonia ヘッドレス実行（<c>[AvaloniaFact]</c>）で直列検証する。<br/>
/// OK 確定時の色反映はファクトリへ渡したコールバック（applyColor）の呼び出しで検証する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class ColorPickerFlyoutFactoryTests
{
    [AvaloniaFact]
    internal void Create_OKボタンで色が16進に変換されて反映される()
    {
        string? applied = null;
        var flyout = ColorPickerFlyoutFactory.Create("#FF8800", NullLogger.Instance, c => applied = c);
        var okButton = FindButtonByContent(flyout.Content, "OK");
        Assert.NotNull(okButton);

        // ColorView の色を変更してから OK を押す → 保存値（#AARRGGBB）がコールバックへ渡される
        var colorView = FindColorView(flyout.Content);
        Assert.NotNull(colorView);
        colorView!.Color = Color.FromRgb(0x12, 0x34, 0x56);
        Dispatcher.UIThread.RunJobs();
        var expected = colorView.Color;

        okButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        // 保存値はアルファを含む #AARRGGBB（アルファ先頭8桁）形式で、ColorView.Color と一致する
        Assert.NotNull(applied);
        Assert.Matches("^#[0-9A-F]{8}$", applied);
        Assert.True(Color.TryParse(applied, out var parsed));
        Assert.Equal(expected, parsed);
    }

    [AvaloniaFact]
    internal void Create_アルファ付き色は8桁の16進に変換される()
    {
        string? applied = null;
        var flyout = ColorPickerFlyoutFactory.Create("White", NullLogger.Instance, c => applied = c);
        var okButton = FindButtonByContent(flyout.Content, "OK");
        var colorView = FindColorView(flyout.Content);
        Assert.NotNull(colorView);

        // 半透明（A=128）。 ColorView は HSV(float) 経由で色を保持するため
        // アルファが僅かに量子化される場合がある（128 → 127 など）。
        colorView!.Color = Color.FromAColor(128, Colors.Red);
        Dispatcher.UIThread.RunJobs();
        var expected = colorView.Color;

        okButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        // OK ボタンは ColorView.Color を #AARRGGBB（アルファ先頭8桁）へ変換して反映する
        Assert.NotNull(applied);
        Assert.Matches("^#[0-9A-F]{8}$", applied);
        Assert.True(Color.TryParse(applied, out var parsed));
        Assert.Equal(expected, parsed);
    }

    [AvaloniaFact]
    internal void Create_キャンセルボタンでは色が変わらない()
    {
        string? applied = null;
        var flyout = ColorPickerFlyoutFactory.Create("#FFFF0000", NullLogger.Instance, c => applied = c);
        var cancelButton = FindButtonByContent(flyout.Content, "キャンセル");
        Assert.NotNull(cancelButton);

        cancelButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        // コールバックは呼ばれない
        Assert.Null(applied);
    }

    [AvaloniaFact]
    internal void Create_SelectedColorが空でも初期色が白になる()
    {
        var flyout = ColorPickerFlyoutFactory.Create("", NullLogger.Instance, _ => { });
        var colorView = FindColorView(flyout.Content);

        Assert.NotNull(colorView);
        Assert.Equal(Colors.White, colorView!.Color);
    }

    [AvaloniaFact]
    internal void Create_SelectedColorが無効な場合は初期色が白になる()
    {
        var flyout = ColorPickerFlyoutFactory.Create("NotAColor", NullLogger.Instance, _ => { });
        var colorView = FindColorView(flyout.Content);

        Assert.NotNull(colorView);
        Assert.Equal(Colors.White, colorView!.Color);
    }

    [AvaloniaFact]
    internal void Create_SelectedColorが非空で解析不能な場合も白になる()
    {
        // 非空だが Color.TryParse に失敗する値（!IsNullOrWhiteSpace=true && TryParse=false の分岐）
        var flyout = ColorPickerFlyoutFactory.Create("#GGGGGG", NullLogger.Instance, _ => { });
        var colorView = FindColorView(flyout.Content);

        Assert.NotNull(colorView);
        Assert.Equal(Colors.White, colorView!.Color);
    }

    /// <summary>Flyout の Content ツリーから指定テキストの Button を再帰検索する。</summary>
    private static Button? FindButtonByContent(object? content, string text)
    {
        if (content is Button button && button.Content is string s && s == text) return button;
        if (content is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                var result = FindButtonByContent(child, text);
                if (result is not null) return result;
            }
        }
        return null;
    }

    /// <summary>Flyout の Content ツリーから ColorView を再帰検索する。</summary>
    private static ColorView? FindColorView(object? content)
    {
        if (content is ColorView colorView) return colorView;
        if (content is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                var result = FindColorView(child);
                if (result is not null) return result;
            }
        }
        return null;
    }
}
