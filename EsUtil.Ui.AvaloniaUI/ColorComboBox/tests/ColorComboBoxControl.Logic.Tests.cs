using Avalonia.Headless.XUnit;
using Xunit;

namespace EsUtil.Ui.AvaloniaUI.ColorComboBox.Tests;

/// <summary><see cref="ColorComboBoxControl"/> の非公開実装（Logic）の単体テスト（バリデーション状態）。</summary>
/// <remarks>
/// 対象: <c>ColorComboBoxControl.axaml.cs</c> のバリデーション状態更新（<see cref="ColorComboBoxControl.UpdateValidationStatus"/>）。<br/>
/// コントロール生成を含むため、Avalonia ヘッドレス実行（<c>[AvaloniaFact]</c>）で直列検証する。<br/>
/// その他の内部実装（テーマ管理・テキスト選択解除・Flyout 構築）は各クラスのテストファイルで検証する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class ColorComboBoxControlLogicTests
{
    // ====================================================================
    // ColorComboBoxControl.UpdateValidationStatus
    // ====================================================================

    [AvaloniaFact]
    internal void UpdateValidationStatus_空文字はエラーにならない()
    {
        var control = new ColorComboBoxControl();

        control.UpdateValidationStatus("");

        Assert.False(control.HasValidationError);
        Assert.Null(control.ValidationErrorMessage);
    }

    [AvaloniaFact]
    internal void UpdateValidationStatus_無効な値はエラーになる()
    {
        var control = new ColorComboBoxControl();

        control.UpdateValidationStatus("NotAColor");

        Assert.True(control.HasValidationError);
        Assert.Equal("無効な色コードです。", control.ValidationErrorMessage);
    }

    [AvaloniaFact]
    internal void UpdateValidationStatus_有効な値はエラーにならない()
    {
        var control = new ColorComboBoxControl();

        control.UpdateValidationStatus("#123456");

        Assert.False(control.HasValidationError);
        Assert.Null(control.ValidationErrorMessage);
    }
}
