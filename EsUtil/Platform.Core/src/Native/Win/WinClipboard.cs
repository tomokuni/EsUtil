using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;
using Windows.Win32.System.Ole;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>クリップボードの内容を読み書きする（Windows 実装）。</summary>
/// <remarks>
/// <b>なぜコアが持つか</b>: クリップボードの開閉・ロック・解放の順序（<c>OpenClipboard</c> →
/// <c>GetClipboardData</c> → <c>GlobalLock</c> → <c>GlobalUnlock</c> → <c>CloseClipboard</c>）は Win32 の作法であり、
/// 1 つでも抜けるとクリップボードが他アプリから使えなくなる。 呼び出し側ごとに書かせず、
/// <b>寿命の管理を 1 箇所に閉じる</b>（処理をコールバックで受け取り、必ず解放する）。<br/>
/// <b>内容の解釈は呼び出し側が行う</b>（DIB の解析は画像処理の層）。 テキストだけは形式が 1 つ（<c>CF_UNICODETEXT</c>）のため本クラスが扱う。<br/>
/// <b>Win32 の宣言は CsWin32 が生成する</b>（<c>src/NativeMethods.txt</c>）。<br/>
/// <b>失敗を例外にしない</b>: クリップボードを開けない（他アプリが使用中）・内容が無い場合は <see langword="false"/>。
/// </remarks>
[SupportedOSPlatform("windows10.0.17763")]
public static class WinClipboard
{
    /// <summary>クリップボードの DIB（デバイス非依存ビットマップ）を読む。</summary>
    /// <param name="format">クリップボードの形式（<c>CF_DIB</c> は 8）。</param>
    /// <param name="read">DIB の先頭を指すポインターを受け取る処理。 戻るまでメモリはロックされたままである。</param>
    /// <returns>読めた場合は <see langword="true"/>。 開けない・内容が無い・ロックできない場合は <see langword="false"/>。</returns>
    /// <remarks>
    /// <b>ポインターを返さない理由</b>: 返すとロックの解放（<c>GlobalUnlock</c> と <c>CloseClipboard</c>）を
    /// 呼び出し側が忘れやすい。 処理の中で使い切らせる。<br/>
    /// <b>処理の中で例外が出ても解放する</b>（<c>finally</c> で解除する）。
    /// </remarks>
    public static unsafe bool TryReadDib(uint format, Action<IntPtr> read)
    {
        ArgumentNullException.ThrowIfNull(read);

        if (!PInvoke.OpenClipboard(default))
        {
            return false;
        }

        try
        {
            var data = PInvoke.GetClipboardData(format);
            if (data.IsNull)
            {
                return false;
            }

            var memory = PInvoke.GlobalLock((HGLOBAL)data.Value);
            if (memory is null)
            {
                return false;
            }

            try
            {
                read((IntPtr)memory);
                return true;
            }
            finally
            {
                _ = PInvoke.GlobalUnlock((HGLOBAL)data.Value);
            }
        }
        finally
        {
            _ = PInvoke.CloseClipboard();
        }
    }

    /// <summary>クリップボードのテキスト（<c>CF_UNICODETEXT</c>）を読む。</summary>
    /// <param name="text">読めたテキスト。 読めなかった場合は <see langword="null"/>。</param>
    /// <returns>読めた場合は <see langword="true"/>。 開けない・テキストが無い・ロックできない場合は <see langword="false"/>。</returns>
    /// <remarks>
    /// <b>ポインターを返さない理由</b>は <see cref="TryReadDib"/> と同じである（解放を呼び出し側へ渡さない）。<br/>
    /// <b>文字列はロックしたまま写す</b>（<c>Marshal.PtrToStringUni</c> がロック中に読み切る）。
    /// </remarks>
    public static unsafe bool TryReadText(out string? text)
    {
        text = null;

        if (!PInvoke.OpenClipboard(default))
        {
            return false;
        }

        try
        {
            var data = PInvoke.GetClipboardData((uint)CLIPBOARD_FORMAT.CF_UNICODETEXT);
            if (data.IsNull)
            {
                return false;
            }

            var memory = PInvoke.GlobalLock((HGLOBAL)data.Value);
            if (memory is null)
            {
                return false;
            }

            try
            {
                text = Marshal.PtrToStringUni((IntPtr)memory);
                return text is not null;
            }
            finally
            {
                _ = PInvoke.GlobalUnlock((HGLOBAL)data.Value);
            }
        }
        finally
        {
            _ = PInvoke.CloseClipboard();
        }
    }

    /// <summary>クリップボードへテキスト（<c>CF_UNICODETEXT</c>）を書く。</summary>
    /// <param name="text">書くテキスト。</param>
    /// <returns>書けた場合は <see langword="true"/>。 開けない・確保できない・設定できない場合は <see langword="false"/>。</returns>
    /// <remarks>
    /// <b>アプリ終了後も内容を保つ</b>ため、書き込みの前に <c>EmptyClipboard</c> で既存の内容を消す。<br/>
    /// <b>メモリは <c>GMEM_MOVEABLE</c> で確保する</b>（<c>SetClipboardData</c> が要求する。 固定メモリを渡すと
    /// クリップボードの所有者が決まらない）。 設定に成功したメモリの所有権はクリップボードへ移るため解放しない。<br/>
    /// <b>失敗を例外にしない</b>（利用側は書き込みの可否だけを見ればよい）。
    /// </remarks>
    public static unsafe bool TryWriteText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!PInvoke.OpenClipboard(default))
        {
            return false;
        }

        try
        {
            if (!PInvoke.EmptyClipboard())
            {
                return false;
            }

            // 終端の null を含めた UTF-16 のバイト数
            var bytes = (nuint)((text.Length + 1) * sizeof(char));
            var memory = PInvoke.GlobalAlloc(GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE, bytes);
            if (memory.IsNull)
            {
                return false;
            }

            var moved = false;
            try
            {
                var target = PInvoke.GlobalLock(memory);
                if (target is null)
                {
                    return false;
                }

                try
                {
                    text.AsSpan().CopyTo(new Span<char>(target, text.Length));
                    ((char*)target)[text.Length] = '\0';
                }
                finally
                {
                    _ = PInvoke.GlobalUnlock(memory);
                }

                if (PInvoke.SetClipboardData((uint)CLIPBOARD_FORMAT.CF_UNICODETEXT, new HANDLE(memory.Value)).IsNull)
                {
                    return false;
                }

                // 所有権はクリップボードへ移る（解放するとクリップボードの内容が壊れる）
                moved = true;
                return true;
            }
            finally
            {
                if (!moved)
                {
                    _ = PInvoke.GlobalFree(memory);
                }
            }
        }
        finally
        {
            _ = PInvoke.CloseClipboard();
        }
    }

    /// <summary>クリップボードの内容が変わるたびに増えるシーケンス番号を取得する。</summary>
    /// <value>クリップボードの変更を検出するための番号（OS が管理する）。</value>
    /// <remarks>
    /// <b>失敗を例外にしない</b>: 取得できない場合は 0 を返す（呼び出し側は前回の値と比べるだけでよい）。<br/>
    /// 値は<b>クリップボードを開かずに</b>取得できるため、他アプリが使用中でも監視を続けられる。
    /// </remarks>
    public static uint SequenceNumber => PInvoke.GetClipboardSequenceNumber();

}
