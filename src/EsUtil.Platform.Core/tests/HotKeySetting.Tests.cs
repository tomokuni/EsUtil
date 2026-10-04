using Xunit;

namespace EsUtil.Platform.Core.Tests;

/// <summary><see cref="HotKeySetting"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 値オブジェクトの文字列表現・キー変換のみを検証するため、UI フレームワークに依存せず<br/>
/// <c>[Fact]</c> で並列実行する。
/// </remarks>
public class HotKeySettingTests
{
    [Fact]
    internal void ToString_修飾キーとキーの組合せを表示する()
    {
        var setting = new HotKeySetting(HotKeyModifiers.Control | HotKeyModifiers.Shift, EsKey.Q);

        Assert.Equal("Ctrl + Shift + Q", setting.ToString());
    }

    [Fact]
    internal void ToString_修飾キーなしはキーのみ表示する()
    {
        var setting = new HotKeySetting(HotKeyModifiers.None, EsKey.A);

        Assert.Equal("A", setting.ToString());
    }

    [Fact]
    internal void ToString_数字キーは数字で表示する()
    {
        var setting = new HotKeySetting(HotKeyModifiers.Alt, EsKey.D1);

        Assert.Equal("Alt + 1", setting.ToString());
    }

    [Theory]
    [InlineData(EsKey.D0, "0")]
    [InlineData(EsKey.D2, "2")]
    [InlineData(EsKey.D3, "3")]
    [InlineData(EsKey.D4, "4")]
    [InlineData(EsKey.D5, "5")]
    [InlineData(EsKey.D6, "6")]
    [InlineData(EsKey.D7, "7")]
    [InlineData(EsKey.D8, "8")]
    [InlineData(EsKey.D9, "9")]
    [InlineData(EsKey.OemMinus, "-")]
    [InlineData(EsKey.OemPlus, "=")]
    [InlineData(EsKey.OemOpenBrackets, "[")]
    [InlineData(EsKey.OemCloseBrackets, "]")]
    [InlineData(EsKey.OemPipe, "\\")]
    [InlineData(EsKey.OemSemicolon, ";")]
    [InlineData(EsKey.OemComma, ",")]
    [InlineData(EsKey.OemPeriod, ".")]
    [InlineData(EsKey.OemQuestion, "/")]
    [InlineData(EsKey.Q, "Q")]
    internal void 変換_記号キーは記号で表示する(EsKey key, string symbol)
    {
        var setting = new HotKeySetting(HotKeyModifiers.None, key);

        Assert.Equal(symbol, setting.ToString());
    }

    [Fact]
    internal void ToString_記号キーは記号で表示する()
    {
        var setting = new HotKeySetting(HotKeyModifiers.Windows, EsKey.OemTilde);

        Assert.Equal("Win + `", setting.ToString());
    }

    [Fact]
    internal void レコード等価性で同一の組合せは一致する()
    {
        var a = new HotKeySetting(HotKeyModifiers.Control, EsKey.S);
        var b = new HotKeySetting(HotKeyModifiers.Control, EsKey.S);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
