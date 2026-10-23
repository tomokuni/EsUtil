using EsUtil.Platform.Core.Input;
using Xunit;

namespace EsUtil.Platform.Core.Tests;

/// <summary>入力の窓口（<see cref="EsInput"/>）の単体テスト。</summary>
/// <remarks>
/// 窓口は実装を持たず機能の型へ委譲するため、ここでは<b>委譲の結果</b>（解釈の可否・未設定値の同一性）だけを検証する。<br/>
/// 解釈の規則そのものは各型のテスト（`Common.EnumCanonical` / `Input.HotKeyParser` / `Input.EsKeyExtensions`）が検証する。
/// </remarks>
public class EsInputTests
{
    [Fact]
    internal void TryParseKey_名前を解釈する()
    {
        Assert.True(EsInput.TryParseKey("Q", out var key));
        Assert.Equal(EsKey.Q, key);
    }

    [Fact]
    internal void TryParseKey_数値文字列は受理しない()
    {
        Assert.False(EsInput.TryParseKey("999", out var key));
        Assert.Equal(EsKey.None, key);
    }

    [Fact]
    internal void NormalizeKey_未定義値をNoneへ寄せる()
    {
        Assert.Equal(EsKey.None, EsInput.NormalizeKey((EsKey)999));
        Assert.Equal(EsKey.A, EsInput.NormalizeKey(EsKey.A));
    }

    [Fact]
    internal void TryParseHotKey_修飾キーとキーを解釈する()
    {
        Assert.True(EsInput.TryParseHotKey("Ctrl + Shift + Q", out var setting));
        Assert.Equal(HotKeyModifiers.Control | HotKeyModifiers.Shift, setting.Modifiers);
        Assert.Equal(EsKey.Q, setting.Key);
    }

    [Fact]
    internal void TryParseHotKey_修飾キーが無ければ失敗する()
        => Assert.False(EsInput.TryParseHotKey("Q", out _));

    [Fact]
    internal void CreateHotKey_指定した組合せを生成する()
    {
        var setting = EsInput.CreateHotKey(HotKeyModifiers.Alt, EsKey.F4);

        Assert.Equal(HotKeyModifiers.Alt, setting.Modifiers);
        Assert.Equal(EsKey.F4, setting.Key);
    }

    [Fact]
    internal void NoHotKey_修飾キーなしのNoneである()
    {
        Assert.Equal(HotKeyModifiers.None, EsInput.NoHotKey.Modifiers);
        Assert.Equal(EsKey.None, EsInput.NoHotKey.Key);
        Assert.Same(EsInput.NoHotKey, EsInput.NoHotKey);
        Assert.Equal(EsInput.CreateHotKey(HotKeyModifiers.None, EsKey.None), EsInput.NoHotKey);
    }
}