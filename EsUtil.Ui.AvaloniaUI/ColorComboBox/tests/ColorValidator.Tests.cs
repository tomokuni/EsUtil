using Xunit;

namespace EsUtil.Ui.AvaloniaUI.ColorComboBox.Tests;

/// <summary><see cref="ColorValidator"/> の単体テスト（純粋演算・並列実行）。</summary>
/// <remarks>
/// 色文字列の妥当性検証のみを検証するため、Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に依存せず
/// <c>[Fact]</c> で並列実行する（テスト全体の高速化）。<br/>
/// ヘッドレス必須のテスト（コントロール生成・イベント処理等）は <see cref="ColorComboBoxControlLogicTests"/> が
/// 直列コレクションで検証する。
/// </remarks>
public class ColorValidatorTests
{
    [Fact]
    internal void IsValid_nullはfalseを返す()
    {
        Assert.False(ColorValidator.IsValid(null));
    }

    [Fact]
    internal void IsValid_有効な色はtrueを返す()
    {
        Assert.True(ColorValidator.IsValid("Red"));
        Assert.True(ColorValidator.IsValid("#00FF00"));
    }

    [Fact]
    internal void IsValid_無効な色はfalseを返す()
    {
        Assert.False(ColorValidator.IsValid("NotAColor"));
    }
}
