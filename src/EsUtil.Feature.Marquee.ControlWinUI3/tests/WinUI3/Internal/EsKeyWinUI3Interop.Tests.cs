using EsUtil.Feature.Marquee.WinUI3.Internal;
using EsUtil.Platform.Core;
using Windows.System;
using Xunit;

namespace EsUtil.Feature.Marquee.WinUI3.Tests.Internal;

/// <summary><see cref="EsKeyWinUI3Interop"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// コアが入力として解釈するキー（矢印 4 方向・Escape・Enter）だけを写し、それ以外は <see cref="EsKey.None"/> を返す。<br/>
/// <see cref="VirtualKey"/> はキー以外の入力（マウス ボタン・ゲームパッド）を含むため、<br/>
/// 「キー以外の入力がキーとして解釈されないこと」も検証する。
/// </remarks>
public class EsKeyWinUI3InteropTests
{
    /// <summary>本コントロールが解釈するキーが対応するコアのキーへ写ることを検証する。</summary>
    /// <param name="key">WinUI 3 のキー。</param>
    /// <param name="expected">期待するコアのキー。</param>
    [Theory]
    [InlineData(VirtualKey.Left, EsKey.Left)]
    [InlineData(VirtualKey.Up, EsKey.Up)]
    [InlineData(VirtualKey.Right, EsKey.Right)]
    [InlineData(VirtualKey.Down, EsKey.Down)]
    [InlineData(VirtualKey.Escape, EsKey.Escape)]
    [InlineData(VirtualKey.Enter, EsKey.Enter)]
    internal void ToEsKey_解釈するキーを写す(VirtualKey key, EsKey expected) => Assert.Equal(expected, key.ToEsKey());

    /// <summary>本コントロールが解釈しないキーは <see cref="EsKey.None"/> を返すことを検証する。</summary>
    /// <param name="key">WinUI 3 のキー。</param>
    [Theory]
    [InlineData(VirtualKey.None)]
    [InlineData(VirtualKey.A)]
    [InlineData(VirtualKey.Space)]
    [InlineData(VirtualKey.Tab)]
    [InlineData(VirtualKey.Number0)]
    [InlineData(VirtualKey.GamepadA)]
    [InlineData(VirtualKey.LeftButton)]
    [InlineData(VirtualKey.RightButton)]
    internal void ToEsKey_解釈しないキーはNoneを返す(VirtualKey key) => Assert.Equal(EsKey.None, key.ToEsKey());

    /// <summary>解釈するキーは表現可能と判定することを検証する。</summary>
    /// <param name="key">WinUI 3 のキー。</param>
    [Theory]
    [InlineData(VirtualKey.Left)]
    [InlineData(VirtualKey.Up)]
    [InlineData(VirtualKey.Right)]
    [InlineData(VirtualKey.Down)]
    [InlineData(VirtualKey.Escape)]
    [InlineData(VirtualKey.Enter)]
    internal void IsRepresentable_解釈するキーはtrue(VirtualKey key) => Assert.True(key.IsRepresentable());

    /// <summary>解釈しないキーは表現可能と判定しないことを検証する。</summary>
    /// <param name="key">WinUI 3 のキー。</param>
    [Theory]
    [InlineData(VirtualKey.A)]
    [InlineData(VirtualKey.None)]
    [InlineData(VirtualKey.GamepadA)]
    internal void IsRepresentable_解釈しないキーはfalse(VirtualKey key) => Assert.False(key.IsRepresentable());
}
