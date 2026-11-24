using Xunit;

namespace EsUtil.Feature.StyledText.Core.Tests;

/// <summary><see cref="TextCursor"/> の仕様テスト（書記素クラスタの境界）。</summary>
/// <remarks>
/// サロゲート ペア（絵文字）と結合文字（基底文字 + 結合記号）を半分に割ると、孤立したサロゲートや
/// 結合記号だけの文字列ができ、描画 API が例外を投げる。 位置の計算はすべて本クラスの境界に従う。
/// </remarks>
public sealed class TextCursorTests
{
    /// <summary>サロゲート ペア（絵文字）を 1 つのクラスタとして扱う。</summary>
    private const string Emoji = "😀";

    /// <summary>結合記号付きの文字（e + 結合アキュート）。 1 つのクラスタになる。</summary>
    private const string Combined = "e\u0301";

    /// <summary>クラスタの内側の位置は、後方へ寄せるとクラスタの先頭になる。</summary>
    [Fact]
    internal void SnapBackward_ペアの内側は先頭へ寄せる()
    {
        var text = Emoji + "あ";

        Assert.Equal(0, TextCursor.SnapBackward(text, 1));
        Assert.Equal(2, TextCursor.SnapBackward(text, 2));
        Assert.Equal(text.Length, TextCursor.SnapBackward(text, text.Length));
    }

    /// <summary>クラスタの内側の位置は、前方へ寄せるとクラスタの末尾になる。</summary>
    [Fact]
    internal void SnapForward_ペアの内側は末尾へ寄せる()
    {
        var text = Emoji + "あ";

        Assert.Equal(2, TextCursor.SnapForward(text, 1));
        Assert.Equal(text.Length, TextCursor.SnapForward(text, text.Length));
        Assert.Equal(0, TextCursor.SnapForward(text, 0));
    }

    /// <summary>結合文字も 1 つのクラスタとして扱う。</summary>
    [Fact]
    internal void Snap_結合文字を分割しない()
    {
        var text = Combined + "b";

        Assert.Equal(2, TextCursor.SnapForward(text, 1));
        Assert.Equal(0, TextCursor.SnapBackward(text, 1));
    }

    /// <summary>前後の移動はクラスタ単位で進む。</summary>
    [Fact]
    internal void NextとPrevious_クラスタ単位で進む()
    {
        var text = "a" + Emoji + "b";

        Assert.Equal(1, TextCursor.Next(text, 0));
        Assert.Equal(3, TextCursor.Next(text, 1));
        Assert.Equal(text.Length, TextCursor.Next(text, text.Length));

        Assert.Equal(1, TextCursor.Previous(text, 3));
        Assert.Equal(0, TextCursor.Previous(text, 1));
        Assert.Equal(0, TextCursor.Previous(text, 0));
    }

    /// <summary>前方向の削除は、キャレット以降の 1 クラスタを丸ごと消す。</summary>
    [Fact]
    internal void ResolveDelete_前方向はペアを丸ごと消す()
    {
        var text = Emoji + "あ";

        var (start, length) = TextCursor.ResolveDelete(text, 0, forward: true);

        Assert.Equal(0, start);
        Assert.Equal(2, length);
    }

    /// <summary>後方向の削除は、キャレット以前の 1 クラスタを丸ごと消す。</summary>
    [Fact]
    internal void ResolveDelete_後方向は結合文字を丸ごと消す()
    {
        var text = "a" + Combined;

        var (start, length) = TextCursor.ResolveDelete(text, text.Length, forward: false);

        Assert.Equal(1, start);
        Assert.Equal(2, length);
    }

    /// <summary>端では削除する文字が無い。</summary>
    [Fact]
    internal void ResolveDelete_端では何も消さない()
    {
        Assert.Equal((0, 0), TextCursor.ResolveDelete(string.Empty, 0, forward: true));
        Assert.Equal((0, 0), TextCursor.ResolveDelete("abc", 0, forward: false));
        Assert.Equal((0, 0), TextCursor.ResolveDelete("abc", 3, forward: true));
    }
}
