using Xunit;

namespace EsUtil.Ui.AvaloniaUI.ColorComboBox.Tests;

/// <summary><see cref="ColorValidation.Determine"/> の純粋ロジックテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 色のバリデーション状態（エラーの有無・メッセージ）の判定のみを検証するため、<br/>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に依存せず <c>[Fact]</c> で並列実行する（テスト全体の高速化）。<br/>
/// <c>StyledProperty</c> への反映は <see cref="ColorComboBoxControl.UpdateValidationStatus"/>（ヘッドレス直列）が担う。
/// </remarks>
public class ColorValidationTests
{
    // ====================================================================
    // エラーなし（空文字・有効な色）
    // ====================================================================

    [Fact]
    internal void Determine_空文字はエラーなしになる()
    {
        var state = ColorValidation.Determine("", isValid: false);

        Assert.False(state.hasError);
        Assert.Null(state.message);
    }

    [Fact]
    internal void Determine_nullはエラーなしになる()
    {
        var state = ColorValidation.Determine(null!, isValid: false);

        Assert.False(state.hasError);
        Assert.Null(state.message);
    }

    [Fact]
    internal void Determine_有効な色はエラーなしになる()
    {
        var state = ColorValidation.Determine("#FF0000", isValid: true);

        Assert.False(state.hasError);
        Assert.Null(state.message);
    }

    // ====================================================================
    // エラーあり（無効な色）
    // ====================================================================

    [Fact]
    internal void Determine_無効な色はエラーありになる()
    {
        var state = ColorValidation.Determine("NotAColor", isValid: false);

        Assert.True(state.hasError);
        Assert.Equal(ColorValidation.InvalidColorMessage, state.message);
    }

    [Fact]
    internal void Determine_エラーメッセージは無効な色コードを示す()
    {
        var state = ColorValidation.Determine("NotAColor", isValid: false);

        Assert.Equal("無効な色コードです。", state.message);
    }

    // ====================================================================
    // ShouldRecover（無効値の最終有効値への復帰判定）
    // ====================================================================

    [Fact]
    internal void ShouldRecover_無効な色で最終有効値があればtrueになる()
    {
        Assert.True(ColorValidation.ShouldRecover(isValid: false, hasLastValid: true));
    }

    [Fact]
    internal void ShouldRecover_有効な色ではfalseになる()
    {
        Assert.False(ColorValidation.ShouldRecover(isValid: true, hasLastValid: true));
    }

    [Fact]
    internal void ShouldRecover_最終有効値がなければfalseになる()
    {
        Assert.False(ColorValidation.ShouldRecover(isValid: false, hasLastValid: false));
    }

    [Fact]
    internal void ShouldRecover_有効かつ最終有効値がなければfalseになる()
    {
        Assert.False(ColorValidation.ShouldRecover(isValid: true, hasLastValid: false));
    }
}
