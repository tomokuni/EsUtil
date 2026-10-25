using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using EsUtil.Platform.Core.Palette;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EsUtil.Ui.AvaloniaUI.ColorComboBox.Tests;

/// <summary><see cref="ColorComboBoxControl"/> の単体テスト（公開プロパティ・バリデーション・入力の反映）。</summary>
/// <remarks>
/// コントロール生成と入力欄の操作を含むため、Avalonia ヘッドレス実行（<c>[AvaloniaFact]</c>）で直列検証する。<br/>
/// Flyout の構築（<see cref="ColorComboBoxFlyoutFactory"/>）は専用のテストファイルで検証する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class ColorComboBoxControlTests
{
    /// <summary>テスト用の <see cref="ColorComboBoxControl"/> を生成する。</summary>
    /// <returns>生成したコントロール（入力欄の名前解決が済んでいること）。</returns>
    private static ColorComboBoxControl CreateControl()
    {
        var control = new ColorComboBoxControl();
        control.ApplyTemplate();
        return control;
    }

    /// <summary>入力欄（<c>PART_Text</c>）を取り出す。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <returns>入力欄。</returns>
    private static TextBox TextOf(ColorComboBoxControl control)
        => control.FindControl<TextBox>("PART_Text")!;

    /// <summary>外周の枠（<c>PART_Frame</c>）を取り出す。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <returns>外周の枠。</returns>
    private static Border FrameOf(ColorComboBoxControl control)
        => control.FindControl<Border>("PART_Frame")!;

    // ====================================================================
    // ログ（Log プロパティ）
    // ====================================================================

    [AvaloniaFact]
    internal void Log_デフォルトはNullLoggerインスタンス()
    {
        var control = CreateControl();

        Assert.Same(NullLogger.Instance, control.Log);
    }

    [AvaloniaFact]
    internal void Log_設定したロガーを返す()
    {
        var control = CreateControl();
        var logger = NullLogger.Instance;

        control.Log = logger;

        Assert.Same(logger, control.Log);
    }

    [AvaloniaFact]
    internal void Log_nullを設定するとNullLoggerにフォールバックする()
    {
        var control = CreateControl();

        control.Log = null!;

        Assert.Same(NullLogger.Instance, control.Log);
    }

    [AvaloniaFact]
    internal void LogPrefix_nullは既定の接頭辞へ戻る()
    {
        var control = CreateControl();

        control.LogPrefix = "[Test]";
        control.LogPrefix = null;

        Assert.Null(control.LogPrefix);
    }

    // ====================================================================
    // 依存プロパティのデフォルト値
    // ====================================================================

    [AvaloniaFact]
    internal void 依存プロパティのデフォルト値が正しい()
    {
        var control = CreateControl();

        Assert.Equal(ColorSortMode.ByHue, control.ColorSortMode);
        Assert.Equal(ColorTextFormat.NameWithAlpha, control.TextFormat);
        Assert.False(control.HasValidationError);
        Assert.Null(control.ValidationErrorMessage);
        Assert.False(control.HighlightBorderOnError);
        Assert.Equal(120d, control.TextMinWidth);
        Assert.Equal(double.PositiveInfinity, control.TextMaxWidth);
        Assert.Equal(double.NaN, control.TextWidth);
        Assert.Null(control.TextPadding);
        Assert.Null(control.TextMargin);
    }

    // ====================================================================
    // 1 つの入力欄としての構造
    // ====================================================================

    [AvaloniaFact]
    internal void 構造_外周の枠と入力欄と区画がある()
    {
        var control = CreateControl();

        Assert.NotNull(control.FindControl<Border>("PART_Frame"));
        Assert.NotNull(control.FindControl<Border>("PART_Swatch"));
        Assert.NotNull(control.FindControl<Button>("PART_ListButton"));
        Assert.NotNull(control.FindControl<Button>("PART_PickerButton"));
    }

    [AvaloniaFact]
    internal void 構造_入力欄は枠を持たない()
    {
        var control = CreateControl();

        // 枠は外周の 1 つだけが持つ（入力欄自身の枠は消す。 1 つの入力欄に見せるため）
        Assert.Equal(new Thickness(0), TextOf(control).BorderThickness);
        Assert.Equal(1d, FrameOf(control).BorderThickness.Left);
    }

    [AvaloniaFact]
    internal void 構造_入力欄の左余白はスウォッチ分を確保する()
    {
        var control = CreateControl();

        // スウォッチ（6 + 16）と文字の間隔（6）の分を左へ確保する
        Assert.Equal(28d, TextOf(control).Padding.Left);
    }

    [AvaloniaFact]
    internal void 構造_区画のボタンは枠と背景を持たない()
    {
        var control = CreateControl();

        var list = control.FindControl<Button>("PART_ListButton")!;
        var picker = control.FindControl<Button>("PART_PickerButton")!;

        // 区画のボタン自身が枠を持つと、外周の枠と二重になる
        foreach (var button in new[] { list, picker })
        {
            Assert.Contains("segment", button.Classes);
        }
    }

    // ====================================================================
    // SelectedColor / バリデーション
    // ====================================================================

    [AvaloniaFact]
    internal void SelectedColor_有効な色を設定するとエラーにならない()
    {
        var control = CreateControl();

        control.SelectedColor = "Red";

        // 入力欄は表記（色名+不透明度）で表示する
        Assert.Equal("Red", TextOf(control).Text);
        Assert.False(control.HasValidationError);
        Assert.Null(control.ValidationErrorMessage);
    }

    [AvaloniaFact]
    internal void SelectedColor_16進コードを設定するとエラーにならない()
    {
        var control = CreateControl();

        control.SelectedColor = "#FF0000";

        Assert.False(control.HasValidationError);
    }

    [AvaloniaFact]
    internal void SelectedColor_無効な色を設定するとエラーになる()
    {
        var control = CreateControl();

        control.SelectedColor = "NotAColor";

        Assert.True(control.HasValidationError);
        Assert.Equal("無効な色コードです。", control.ValidationErrorMessage);
    }

    [AvaloniaFact]
    internal void SelectedColor_空文字を設定するとエラーにならない()
    {
        var control = CreateControl();

        control.SelectedColor = "";

        Assert.False(control.HasValidationError);
        Assert.Null(control.ValidationErrorMessage);
    }

    [AvaloniaFact]
    internal void SelectedColor_無効後_有効な値を設定するとエラーが解除される()
    {
        var control = CreateControl();
        control.SelectedColor = "NotAColor";

        control.SelectedColor = "Blue";

        Assert.False(control.HasValidationError);
        Assert.Null(control.ValidationErrorMessage);
    }

    [AvaloniaFact]
    internal void SelectedColor_nullを設定すると例外にならない()
    {
        var control = CreateControl();

        var exception = Record.Exception(() => control.SelectedColor = null!);

        Assert.Null(exception);
    }

    // ====================================================================
    // 表記（TextFormat）
    // ====================================================================

    [AvaloniaFact]
    internal void TextFormat_色名と不透明度で表示する()
    {
        var control = CreateControl();
        control.SelectedColor = "#80FF0000";

        control.TextFormat = ColorTextFormat.NameWithAlpha;

        Assert.Equal("Red+50%", TextOf(control).Text);
    }

    [AvaloniaFact]
    internal void TextFormat_カラーコードで表示する()
    {
        var control = CreateControl();
        control.SelectedColor = "#80FF0000";

        control.TextFormat = ColorTextFormat.HexCode;

        Assert.Equal("#80FF0000", TextOf(control).Text);
    }

    // ====================================================================
    // ツールチップ
    // ====================================================================

    [AvaloniaFact]
    internal void ツールチップ_色名とカラーコードの2行を出す()
    {
        var control = CreateControl();

        control.SelectedColor = "#80FF0000";

        var text = ToolTip.GetTip(TextOf(control)) as string;
        Assert.NotNull(text);
        var lines = text.Split(Environment.NewLine);
        Assert.Equal("Red+50%", lines[0]);
        Assert.Equal("#80FF0000", lines[1]);
    }

    [AvaloniaFact]
    internal void ツールチップ_スウォッチにも同じ内容を出す()
    {
        var control = CreateControl();

        control.SelectedColor = "#80FF0000";

        var swatch = control.FindControl<Border>("PART_Swatch")!;
        Assert.Equal(ToolTip.GetTip(TextOf(control)), ToolTip.GetTip(swatch));
    }

    // ====================================================================
    // 入力欄の編集
    // ====================================================================

    [AvaloniaFact]
    internal void 入力_有効な色を入力すると保存値へ反映する()
    {
        var control = CreateControl();
        var text = TextOf(control);

        text.Text = "Lime";

        Assert.Equal("#FF00FF00", control.SelectedColor);
    }

    [AvaloniaFact]
    internal void 入力_無効な色を入力するとエラーになる()
    {
        var control = CreateControl();

        TextOf(control).Text = "NotAColor";

        Assert.True(control.HasValidationError);
    }

    [AvaloniaFact]
    internal void LostFocus_無効な色が最終有効値に戻る()
    {
        var control = CreateControl();
        control.SelectedColor = "Red";
        var text = TextOf(control);
        text.Text = "NotAColor";

        text.RaiseEvent(new FocusChangedEventArgs(InputElement.LostFocusEvent));

        Assert.Equal("#FFFF0000", control.SelectedColor);
        Assert.Equal("Red", text.Text);
        Assert.False(control.HasValidationError);
    }

    [AvaloniaFact]
    internal void LostFocus_有効な色の場合はそのまま維持される()
    {
        var control = CreateControl();
        control.SelectedColor = "Red";
        var text = TextOf(control);
        text.Text = "Blue";

        text.RaiseEvent(new FocusChangedEventArgs(InputElement.LostFocusEvent));

        Assert.Equal("#FF0000FF", control.SelectedColor);
    }

    // ====================================================================
    // バリデーションの強調
    // ====================================================================

    [AvaloniaFact]
    internal void HighlightBorderOnError_エラー時にvalidationErrorクラスが設定される()
    {
        var control = CreateControl();
        control.HighlightBorderOnError = true;

        control.SelectedColor = "NotAColor";

        Assert.Contains("validation-error", FrameOf(control).Classes);
    }

    [AvaloniaFact]
    internal void HighlightBorderOnError_エラーでなければvalidationErrorクラスは設定されない()
    {
        var control = CreateControl();
        control.HighlightBorderOnError = true;

        control.SelectedColor = "Red";

        Assert.DoesNotContain("validation-error", FrameOf(control).Classes);
    }

    [AvaloniaFact]
    internal void HighlightBorderOnError_falseではエラーでもvalidationErrorクラスは設定されない()
    {
        var control = CreateControl();
        control.HighlightBorderOnError = false;

        control.SelectedColor = "NotAColor";

        Assert.DoesNotContain("validation-error", FrameOf(control).Classes);
    }

    // ====================================================================
    // 外観（入力欄のサイズ）
    // ====================================================================

    [AvaloniaFact]
    internal void 入力欄サイズプロパティを設定すると反映される()
    {
        var control = CreateControl();

        control.TextMinWidth = 100;
        control.TextMaxWidth = 300;
        control.TextWidth = 200;

        var text = TextOf(control);
        Assert.Equal(100d, text.MinWidth);
        Assert.Equal(300d, text.MaxWidth);
        Assert.Equal(200d, text.Width);
    }

    [AvaloniaFact]
    internal void TextPadding変更で入力欄の左余白にスウォッチ分が加算される()
    {
        var control = CreateControl();

        control.TextPadding = new Thickness(4, 2, 8, 2);

        var padding = TextOf(control).Padding;
        Assert.Equal(32d, padding.Left);
        Assert.Equal(2d, padding.Top);
        Assert.Equal(8d, padding.Right);
    }

    [AvaloniaFact]
    internal void TextMargin変更で入力欄のMarginが設定される()
    {
        var control = CreateControl();
        var margin = new Thickness(1, 2, 3, 4);

        control.TextMargin = margin;

        Assert.Equal(margin, TextOf(control).Margin);
    }

    [AvaloniaFact]
    internal void TextMargin_nullでMarginがクリアされる()
    {
        var control = CreateControl();
        control.TextMargin = new Thickness(1);

        control.TextMargin = null;

        Assert.Equal(default, TextOf(control).Margin);
    }

    // ====================================================================
    // カラーピッカー ボタン
    // ====================================================================

    [AvaloniaFact]
    internal void カラーピッカーボタンクリック_senderがButton以外なら何もしない()
    {
        var control = CreateControl();

        var exception = Record.Exception(() =>
            control.GetType()
                .GetMethod("OnColorPickerButtonClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(control, [null, new RoutedEventArgs()]));

        Assert.Null(exception);
    }
}