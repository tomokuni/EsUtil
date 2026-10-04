using EsUtil.Platform.Core.Native.Win;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Native;

/// <summary><see cref="WinNative.KeyToWin32VirtualKey"/>（仮想キーコードの変換表）の単体テスト。</summary>
/// <remarks>
/// <b>検証の目的</b>: <c>RegisterHotKey</c> の vk へ渡す値が Win32 の仮想キーコードとして正しいこと。<br/>
/// 序数（<c>(uint)key</c>）を返すと<b>無言で別のキーが登録される</b>（実測: 以前は <c>PrintScreen</c> の序数 89 = <c>0x59</c> = <c>Y</c> が<br/>
/// 登録されていた）ため、表に無いキーは <c>0</c>（登録できない値）であることを固定する。<br/>
/// 期待値は Win32 の <c>VK_*</c> 定義（WinUI 3 の <c>Windows.System.VirtualKey</c> は同じ値であることを実測で確認済み）に基づく。
/// </remarks>
public class WinNativeKeyTests
{
    /// <summary>代表的なキーが正しい仮想キーコードへ変換されることを検証する。</summary>
    /// <remarks>族ごとに 1 件以上を含める（修飾キー・文字・数字・テンキー・F キー・記号・メディア・ブラウザー・IME）。</remarks>
    /// <param name="key">変換対象のキー。</param>
    /// <param name="expected">期待する仮想キーコード。</param>
    [Theory]
    // 制御・編集
    [InlineData(EsKey.None, 0x00)]
    [InlineData(EsKey.Cancel, 0x03)]
    [InlineData(EsKey.Back, 0x08)]
    [InlineData(EsKey.Tab, 0x09)]
    [InlineData(EsKey.Return, 0x0D)]
    [InlineData(EsKey.Enter, 0x0D)]
    [InlineData(EsKey.Escape, 0x1B)]
    [InlineData(EsKey.Space, 0x20)]
    [InlineData(EsKey.Delete, 0x2E)]
    // ロック系（以前は表に無く序数が返っていた）
    [InlineData(EsKey.CapsLock, 0x14)]
    [InlineData(EsKey.NumLock, 0x90)]
    [InlineData(EsKey.Scroll, 0x91)]
    [InlineData(EsKey.PrintScreen, 0x2C)]
    [InlineData(EsKey.Sleep, 0x5F)]
    [InlineData(EsKey.Apps, 0x5D)]
    // 修飾キー（左右）
    [InlineData(EsKey.LeftShift, 0xA0)]
    [InlineData(EsKey.RightShift, 0xA1)]
    [InlineData(EsKey.LeftCtrl, 0xA2)]
    [InlineData(EsKey.RightCtrl, 0xA3)]
    [InlineData(EsKey.LeftAlt, 0xA4)]
    [InlineData(EsKey.RightAlt, 0xA5)]
    [InlineData(EsKey.LWin, 0x5B)]
    [InlineData(EsKey.RWin, 0x5C)]
    // 文字・数字
    [InlineData(EsKey.A, 0x41)]
    [InlineData(EsKey.Z, 0x5A)]
    [InlineData(EsKey.D0, 0x30)]
    [InlineData(EsKey.D9, 0x39)]
    // テンキー・演算
    [InlineData(EsKey.NumPad0, 0x60)]
    [InlineData(EsKey.NumPad9, 0x69)]
    [InlineData(EsKey.Multiply, 0x6A)]
    [InlineData(EsKey.Divide, 0x6F)]
    // ナビゲーション
    [InlineData(EsKey.Left, 0x25)]
    [InlineData(EsKey.PageUp, 0x21)]
    [InlineData(EsKey.PageDown, 0x22)]
    [InlineData(EsKey.Prior, 0x21)]
    [InlineData(EsKey.Next, 0x22)]
    // F キー
    [InlineData(EsKey.F1, 0x70)]
    [InlineData(EsKey.F12, 0x7B)]
    [InlineData(EsKey.F24, 0x87)]
    // メディア（WinUI 3 の VirtualKey には無い定義）
    [InlineData(EsKey.VolumeMute, 0xAD)]
    [InlineData(EsKey.VolumeUp, 0xAF)]
    [InlineData(EsKey.MediaPlayPause, 0xB3)]
    [InlineData(EsKey.LaunchMail, 0xB4)]
    // ブラウザー
    [InlineData(EsKey.BrowserBack, 0xA6)]
    [InlineData(EsKey.BrowserHome, 0xAC)]
    // 記号（OEM）
    [InlineData(EsKey.OemSemicolon, 0xBA)]
    [InlineData(EsKey.OemTilde, 0xC0)]
    [InlineData(EsKey.OemQuotes, 0xDE)]
    [InlineData(EsKey.OemBackslash, 0xE2)]
    [InlineData(EsKey.OemClear, 0xFE)]
    // IME
    [InlineData(EsKey.ImeConvert, 0x1C)]
    [InlineData(EsKey.KanaMode, 0x15)]
    internal void 変換_代表的なキーは仮想キーコードへ変換される(EsKey key, int expected)
        => Assert.Equal((uint)expected, WinNative.KeyToWin32VirtualKey(key));

    /// <summary>対応する仮想キーコードが無いキーは 0（登録できない値）を返すことを検証する。</summary>
    /// <remarks>
    /// 序数を返すと無言で別のキーが登録されるため、未対応は 0 にする（<c>RegisterHotKey</c> が失敗し、<br/>
    /// 呼び出し側の「登録に失敗した」ログで気付ける）。
    /// </remarks>
    /// <param name="key">変換対象のキー。</param>
    [Theory]
    [InlineData(EsKey.LineFeed)]
    [InlineData(EsKey.System)]
    [InlineData(EsKey.DeadCharProcessed)]
    [InlineData(EsKey.FnLeftArrow)]
    [InlineData(EsKey.FnDownArrow)]
    [InlineData(EsKey.MediaHome)]
    [InlineData(EsKey.MediaTvGuide)]
    [InlineData(EsKey.MediaPreviousChannel)]
    internal void 変換_対応表に無いキーは0を返す(EsKey key)
        => Assert.Equal(0u, WinNative.KeyToWin32VirtualKey(key));

    /// <summary>未定義値（列挙に無い数値）でも例外にならず 0 を返すことを検証する。</summary>
    [Fact]
    internal void 変換_未定義値は0を返す()
        => Assert.Equal(0u, WinNative.KeyToWin32VirtualKey((EsKey)9999));

    /// <summary>同じキーを表す別名が同じ仮想キーコードへ変換されることを検証する。</summary>
    /// <remarks>Win32 では <c>VK_CAPITAL</c> に <c>CapsLock</c> / <c>Capital</c> の 2 名がある（対称性の確認）。</remarks>
    [Fact]
    internal void 変換_別名は同じ値になる()
    {
        Assert.Equal(WinNative.KeyToWin32VirtualKey(EsKey.CapsLock), WinNative.KeyToWin32VirtualKey(EsKey.Capital));
        Assert.Equal(WinNative.KeyToWin32VirtualKey(EsKey.Return), WinNative.KeyToWin32VirtualKey(EsKey.Enter));
        Assert.Equal(WinNative.KeyToWin32VirtualKey(EsKey.PageUp), WinNative.KeyToWin32VirtualKey(EsKey.Prior));
        Assert.Equal(WinNative.KeyToWin32VirtualKey(EsKey.OemSemicolon), WinNative.KeyToWin32VirtualKey(EsKey.Oem1));
        Assert.Equal(WinNative.KeyToWin32VirtualKey(EsKey.OemBackslash), WinNative.KeyToWin32VirtualKey(EsKey.Oem102));
    }

    /// <summary>表に載せたキーが重複した仮想キーコードへ無秩序に潰れていないことを検証する。</summary>
    /// <remarks>
    /// Win32 では別名（<c>VK_OEM_ATTN</c> と <c>VK_DBE_ALPHANUMERIC</c> 等）が同じ値を持つため完全一致は要求しないが、<br/>
    /// <b>英字 26 種と数字 10 種は互いに異なる</b>こと（取り違えの検出）を確認する。
    /// </remarks>
    [Fact]
    internal void 変換_文字と数字は互いに異なる値になる()
    {
        var letters = new List<uint>();
        foreach (var c in "ABCDEFGHIJKLMNOPQRSTUVWXYZ")
        {
            letters.Add(WinNative.KeyToWin32VirtualKey(Enum.Parse<EsKey>(c.ToString())));
        }

        var digits = new List<uint>();
        for (var i = 0; i <= 9; i++)
        {
            digits.Add(WinNative.KeyToWin32VirtualKey(Enum.Parse<EsKey>("D" + i)));
        }

        Assert.Equal(26, letters.Distinct().Count());
        Assert.Equal(10, digits.Distinct().Count());
        Assert.Empty(letters.Intersect(digits));
    }
}
