using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>クリップボードの内容を読む（Windows 実装）。</summary>
/// <remarks>
/// <b>なぜコアが持つか</b>: クリップボードの開閉・ロック・解放の順序（<c>OpenClipboard</c> →
/// <c>GetClipboardData</c> → <c>GlobalLock</c> → <c>GlobalUnlock</c> → <c>CloseClipboard</c>）は Win32 の作法であり、
/// 1 つでも抜けるとクリップボードが他アプリから使えなくなる。 呼び出し側ごとに書かせず、
/// <b>寿命の管理を 1 箇所に閉じる</b>（処理をコールバックで受け取り、必ず解放する）。<br/>
/// <b>内容の解釈は呼び出し側が行う</b>（本クラスはビットマップの形式を知らない。 例: CF_DIB の解析は画像処理の層）。<br/>
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
}
