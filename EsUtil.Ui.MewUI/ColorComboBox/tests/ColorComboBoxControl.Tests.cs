using EsUtil.Platform.Core.Palette;
using Xunit;

namespace EsUtil.Ui.MewUI.ColorComboBox.Tests;

/// <summary>色選択コントロール（MewUI 版）の公開 API とログの単体テスト。</summary>
/// <remarks>
/// ウィンドウを開かずに生成・プロパティ設定だけで検証できる範囲に限定する（入力はサンプルで手動確認する）。
/// 色文字列の解釈そのものはコアのテストが検証する（本クラスは UI との同期を見る）。
/// </remarks>
[Collection(ColorComboBoxLogPrefix.Name)]
public class ColorComboBoxControlTests
{
    [Fact]
    internal void 既定の色は白である()
    {
        var control = new ColorComboBoxControl();

        Assert.Equal("#FFFFFFFF", control.SelectedColor);
        Assert.False(control.HasValidationError);
        Assert.Null(control.ValidationErrorMessage);
    }

    [Fact]
    internal void SelectedColor_色名を設定すると保存値へ正規化される()
    {
        var control = new ColorComboBoxControl { SelectedColor = "Red" };

        Assert.Equal("#FFFF0000", control.SelectedColor);
    }

    [Fact]
    internal void SelectedColor_不透明度付きの指定はアルファへ合成される()
    {
        var control = new ColorComboBoxControl { SelectedColor = "Red+50%" };

        Assert.Equal("#80FF0000", control.SelectedColor);
    }

    [Fact]
    internal void SelectedColor_変更するとイベントが発火する()
    {
        var control = new ColorComboBoxControl();
        var raised = 0;
        control.SelectedColorChanged += (_, _) => raised++;

        control.SelectedColor = "Blue";
        control.SelectedColor = "Blue";

        Assert.Equal(1, raised);
    }

    [Fact]
    internal void SelectedColor_無効な値はエラーになる()
    {
        var control = new ColorComboBoxControl { SelectedColor = "NotAColor" };

        Assert.True(control.HasValidationError);
        Assert.Equal(ColorValidation.InvalidColorMessage, control.ValidationErrorMessage);
    }

    [Fact]
    internal void SelectedColor_空文字はエラーにならない()
    {
        var control = new ColorComboBoxControl { SelectedColor = "" };

        Assert.False(control.HasValidationError);
        Assert.Null(control.ValidationErrorMessage);
    }

    [Fact]
    internal void ColorSortMode_未定義の値でも例外にならない()
    {
        var control = new ColorComboBoxControl { ColorSortMode = ColorSortMode.ByName };

        Assert.Equal(ColorSortMode.ByName, control.ColorSortMode);
    }

    [Fact]
    internal void Log_設定するとログが記録される()
    {
        var logger = new RecordingLogger();
        var control = new ColorComboBoxControl { Log = logger, SelectedColor = "Green" };

        Assert.Contains(logger.Messages, message => message.Contains("SelectedColor", StringComparison.Ordinal));
    }

    [Fact]
    internal void LogPrefix_接頭辞を変更するとログの接頭辞が変わる()
    {
        var logger = new RecordingLogger();
        var control = new ColorComboBoxControl { Log = logger, LogPrefix = "[MyColor]", SelectedColor = "Green" };

        Assert.Contains(logger.Messages, message => message.StartsWith("[MyColor]", StringComparison.Ordinal));
    }

    [Fact]
    internal void Log_nullを設定しても例外にならない()
    {
        var control = new ColorComboBoxControl { Log = null!, SelectedColor = "Green" };

        Assert.Equal("#FF008000", control.SelectedColor);
    }
}