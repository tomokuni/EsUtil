using EsUtil.Platform.Core.Input;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Input;

/// <summary><see cref="HotKeyParser"/> の単体テスト（ホットキー文字列の解析・UI 非依存・並列実行）。</summary>
/// <remarks>
/// 文字列解析のみを検証するため、Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に依存せず
/// <c>[Fact]</c> で並列実行する（テスト全体の高速化）。
/// </remarks>
public class HotKeyParserTests
{
    // ====================================================================
    // 正常系: 標準形式
    // ====================================================================

    [Theory]
    [InlineData("Ctrl + Shift + Q", HotKeyModifiers.Control | HotKeyModifiers.Shift, EsKey.Q)]
    [InlineData("Ctrl+Shift+Q", HotKeyModifiers.Control | HotKeyModifiers.Shift, EsKey.Q)]
    [InlineData("ctrl + shift + q", HotKeyModifiers.Control | HotKeyModifiers.Shift, EsKey.Q)]
    [InlineData("Control + Alt + Delete", HotKeyModifiers.Control | HotKeyModifiers.Alt, EsKey.Delete)]
    [InlineData("Win + S", HotKeyModifiers.Windows, EsKey.S)]
    [InlineData("Windows + Space", HotKeyModifiers.Windows, EsKey.Space)]
    [InlineData("Alt + F4", HotKeyModifiers.Alt, EsKey.F4)]
    internal void TryParse_標準形式を解釈する(string text, HotKeyModifiers expectedModifiers, EsKey expectedKey)
    {
        var ok = HotKeyParser.TryParse(text, out var setting);

        Assert.True(ok);
        Assert.Equal(expectedModifiers, setting.Modifiers);
        Assert.Equal(expectedKey, setting.Key);
    }

    // ====================================================================
    // 異常系: 解釈できない入力
    // ====================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    internal void TryParse_空文字はfalse(string? text)
    {
        Assert.False(HotKeyParser.TryParse(text!, out _));
    }

    [Theory]
    [InlineData("Q")]          // 修飾キーなし
    [InlineData("Ctrl")]       // キーなし
    [InlineData("Ctrl +")]     // キーなし
    internal void TryParse_不十分な形式はfalse(string text)
    {
        Assert.False(HotKeyParser.TryParse(text, out _));
    }

    [Fact]
    internal void TryParse_未知のキーはfalse()
    {
        Assert.False(HotKeyParser.TryParse("Ctrl + UnknownKey", out _));
    }

    [Fact]
    internal void TryParse_未知の修飾キーは無視される()
    {
        // "Meta" は修飾キーとして解釈されない（None 扱い）→ 修飾キーなしとして失敗する
        // ここでは既知の修飾キー + 未知の修飾キーの組み合わせではなく、未知の修飾キーのみを検証する
        Assert.False(HotKeyParser.TryParse("Meta + Q", out _));
    }

    [Fact]
    internal void TryParse_成功時にKeyへ正しく設定される()
    {
        var ok = HotKeyParser.TryParse("Ctrl + P", out var setting);

        Assert.True(ok);
        Assert.Equal(HotKeyModifiers.Control, setting.Modifiers);
        Assert.Equal(EsKey.P, setting.Key);
    }

    [Theory]
    [InlineData("Ctrl + 999")]      // 列挙に無い数値（`Enum.TryParse` は数値文字列を受け付ける）
    [InlineData("Ctrl + -1")]
    [InlineData("Ctrl + 0")]        // None（キーとして無効）
    internal void TryParse_未定義の数値キーはfalse(string text)
    {
        // `Enum.TryParse<T>` は数値文字列を受理してしまうため、列挙に無い値が
        // そのままキーとして通る。 キーの正規化（`Canonical()`）で未定義値を拒否する。
        Assert.False(HotKeyParser.TryParse(text, out _));
    }
}
