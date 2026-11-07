using EsUtil.Platform.Core.Native.Win;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Native;

/// <summary><see cref="WinClipboard"/> のテキスト読み書きとシーケンス番号の実機テスト。</summary>
/// <remarks>
/// <b>検証の目的</b>: クリップボードの開閉・ロック・解放の順序が正しいことを確かめる
/// （解放を 1 つ落としてもコンパイルは通り、他アプリがクリップボードを使えなくなる）。<br/>
/// <b>実 OS の状態を操作する</b>: 書き込みのテストは<b>元の内容がテキストのときだけ</b>実行し、終了時に元へ戻す
/// （画像などテキスト以外の内容を壊さない）。 並列実行すると他のテストと競合するため <c>Win32</c> コレクションで直列化する。
/// </remarks>
[Collection("Win32")]
public class WinClipboardTests
{
    /// <summary>テキストの読み取りが例外を投げないことを検証する。</summary>
    /// <remarks>他のアプリがクリップボードを使用中でも例外にしない（呼び出し側は戻り値だけを見る）。</remarks>
    [Fact]
    internal void TryReadText_例外を投げない()
    {
        var clipboard = new WinClipboard();
        _ = clipboard.TryReadText(out _);
    }

    /// <summary>書き込んだテキストをそのまま読み戻せることを検証する。</summary>
    /// <remarks>元の内容がテキストでない場合は読み飛ばす（画像などを上書きしない）。</remarks>
    [Fact]
    internal void TryWriteText_書いたテキストを読み戻せる()
    {
        var clipboard = new WinClipboard();
        if (!clipboard.TryReadText(out var original) || original is null)
        {
            // テキスト以外の内容（画像など）は復元できないため、書き込みを行わない
            return;
        }

        var written = $"EsUtil.Platform.Core.Tests {Guid.NewGuid():N}";
        try
        {
            Assert.True(clipboard.TryWriteText(written));
            Assert.True(clipboard.TryReadText(out var read));
            Assert.Equal(written, read);
        }
        finally
        {
            Assert.True(clipboard.TryWriteText(original));
        }
    }

    /// <summary>シーケンス番号が取得でき、書き込みで変化することを検証する。</summary>
    /// <remarks>元の内容がテキストでない場合は読み飛ばす（画像などを上書きしない）。</remarks>
    [Fact]
    internal void SequenceNumber_書き込みで変化する()
    {
        var clipboard = new WinClipboard();
        if (!clipboard.TryReadText(out var original) || original is null)
        {
            return;
        }

        var before = clipboard.SequenceNumber;
        try
        {
            Assert.True(clipboard.TryWriteText($"SequenceNumber {Guid.NewGuid():N}"));
            Assert.NotEqual(before, clipboard.SequenceNumber);
        }
        finally
        {
            Assert.True(clipboard.TryWriteText(original));
        }
    }
}
