using Xunit;
using EsUtil.Feature.StyledText.Core.Editing;

namespace EsUtil.Feature.StyledText.Core.Tests;

/// <summary>キーの割り当て（どのキーがどの操作になるか）の仕様テスト。</summary>
/// <remarks>
/// 修飾キーとキーの組み合わせは利用者の慣習で決まるため、UI を介さずに固定する。
/// </remarks>
public class TextEditKeyMapTests
{
    /// <summary>装飾は Control と B / I / U の組み合わせで切り替わることを検証する。</summary>
    [Theory]
    [InlineData(TextEditKey.B, TextEditCommand.ToggleBold)]
    [InlineData(TextEditKey.I, TextEditCommand.ToggleItalic)]
    [InlineData(TextEditKey.U, TextEditCommand.ToggleUnderline)]
    internal void 装飾はControlと文字キーで切り替わる(TextEditKey key, TextEditCommand expected)
    {
        var request = TextEditKeyMap.Resolve(key, TextEditModifiers.Control);

        Assert.Equal(expected, request.Command);
        Assert.False(request.Extend);
    }

    /// <summary>Shift を伴う Control + 文字キーは装飾の切替にしないことを検証する。</summary>
    [Fact]
    internal void 装飾はShiftを伴うと切り替わらない()
    {
        var request = TextEditKeyMap.Resolve(TextEditKey.B, TextEditModifiers.Control | TextEditModifiers.Shift);

        Assert.Equal(TextEditCommand.None, request.Command);
        Assert.False(request.IsHandled);
    }

    /// <summary>取り消し線は Alt + Shift + 5 で切り替わることを検証する。</summary>
    [Fact]
    internal void 取り消し線はAltとShiftと5で切り替わる()
    {
        var request = TextEditKeyMap.Resolve(
            TextEditKey.Digit5, TextEditModifiers.Alt | TextEditModifiers.Shift);

        Assert.Equal(TextEditCommand.ToggleStrike, request.Command);
    }

    /// <summary>移動のキーは Shift を押している間だけ選択を広げることを検証する。</summary>
    [Theory]
    [InlineData(TextEditKey.Left, TextEditCommand.CaretLeft)]
    [InlineData(TextEditKey.Right, TextEditCommand.CaretRight)]
    [InlineData(TextEditKey.Up, TextEditCommand.CaretUp)]
    [InlineData(TextEditKey.Down, TextEditCommand.CaretDown)]
    [InlineData(TextEditKey.Home, TextEditCommand.CaretLineStart)]
    [InlineData(TextEditKey.End, TextEditCommand.CaretLineEnd)]
    internal void 移動のキーはShiftで選択を広げる(TextEditKey key, TextEditCommand expected)
    {
        Assert.Equal(expected, TextEditKeyMap.Resolve(key, TextEditModifiers.None).Command);
        Assert.False(TextEditKeyMap.Resolve(key, TextEditModifiers.None).Extend);

        var extending = TextEditKeyMap.Resolve(key, TextEditModifiers.Shift);
        Assert.Equal(expected, extending.Command);
        Assert.True(extending.Extend);
    }

    /// <summary>削除と改行と終了は選択を広げないことを検証する。</summary>
    [Theory]
    [InlineData(TextEditKey.Back, TextEditCommand.DeleteBackward)]
    [InlineData(TextEditKey.Delete, TextEditCommand.DeleteForward)]
    [InlineData(TextEditKey.Enter, TextEditCommand.InsertLineBreak)]
    [InlineData(TextEditKey.Escape, TextEditCommand.EndEdit)]
    internal void 削除と改行と終了は選択を広げない(TextEditKey key, TextEditCommand expected)
    {
        var request = TextEditKeyMap.Resolve(key, TextEditModifiers.Shift);

        Assert.Equal(expected, request.Command);
        Assert.False(request.Extend);
    }

    /// <summary>Control + Enter で編集を終えることを検証する。</summary>
    [Fact]
    internal void ControlとEnterで編集を終える()
    {
        var request = TextEditKeyMap.Resolve(TextEditKey.Enter, TextEditModifiers.Control);

        Assert.Equal(TextEditCommand.EndEdit, request.Command);
    }

    /// <summary>選択とクリップボードは Control と文字キーの組み合わせで解決することを検証する。</summary>
    [Theory]
    [InlineData(TextEditKey.A, TextEditCommand.SelectAll)]
    [InlineData(TextEditKey.C, TextEditCommand.Copy)]
    [InlineData(TextEditKey.X, TextEditCommand.Cut)]
    [InlineData(TextEditKey.V, TextEditCommand.Paste)]
    internal void 選択とクリップボードはControlと文字キーで解決する(TextEditKey key, TextEditCommand expected)
    {
        Assert.Equal(expected, TextEditKeyMap.Resolve(key, TextEditModifiers.Control).Command);
    }

    /// <summary>修飾キーの無い文字キーには割り当てが無いことを検証する（文字入力へ回す）。</summary>
    [Theory]
    [InlineData(TextEditKey.A)]
    [InlineData(TextEditKey.B)]
    [InlineData(TextEditKey.C)]
    [InlineData(TextEditKey.X)]
    internal void 修飾キーの無い文字キーは割り当てが無い(TextEditKey key)
    {
        var request = TextEditKeyMap.Resolve(key, TextEditModifiers.None);

        Assert.Equal(TextEditCommand.None, request.Command);
        Assert.False(request.IsHandled);
    }
}
