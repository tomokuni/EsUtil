using System.Runtime.InteropServices;

namespace EsUtil.Ui.MewUI.RichTextEditor;

/// <summary>本文の編集で使うテキストのクリップボード操作（Win32）。</summary>
/// <remarks>本コントロールだけが使う複製（MewUI のクリップボードはデータ オブジェクト経由で手順が多い）。</remarks>
/// <remarks>
/// <b>MewUI のクリップボードはデータ オブジェクト経由</b>であり、テキストだけを扱う用途には手順が多い。
/// 本クラスは Win32 のクリップボード API を直接使い、注釈の本文（Unicode テキスト）だけを読み書きする。<br/>
/// <b>失敗は例外にしない</b>: 他のプロセスがクリップボードを掴んでいる場合があるため、失敗したら何もしない。
/// </remarks>
internal static class RichTextClipboard
{
    /// <summary>Unicode テキストの形式。</summary>
    private const uint CfUnicodeText = 13;

    /// <summary>メモリの移動を許可する割り当て。</summary>
    private const uint GmemMoveable = 0x0002;

    /// <summary>指定した文字列をクリップボードへ書き込む。</summary>
    /// <param name="text">書き込む文字列。</param>
    /// <returns>書き込めた場合は <see langword="true"/>。</returns>
    internal static bool TrySetText(string text)
    {
        if (text.Length == 0 || !OpenClipboard(IntPtr.Zero))
        {
            return false;
        }

        try
        {
            _ = EmptyClipboard();
            var bytes = (text.Length + 1) * sizeof(char);
            var handle = GlobalAlloc(GmemMoveable, (nuint)bytes);
            if (handle == IntPtr.Zero)
            {
                return false;
            }

            var target = GlobalLock(handle);
            if (target == IntPtr.Zero)
            {
                _ = GlobalFree(handle);
                return false;
            }

            try
            {
                Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
                Marshal.WriteInt16(target, text.Length * sizeof(char), 0);
            }
            finally
            {
                _ = GlobalUnlock(handle);
            }

            if (SetClipboardData(CfUnicodeText, handle) == IntPtr.Zero)
            {
                _ = GlobalFree(handle);
                return false;
            }

            return true;
        }
        finally
        {
            _ = CloseClipboard();
        }
    }

    /// <summary>クリップボードの文字列を読み取る。</summary>
    /// <returns>読み取った文字列（無い場合は空）。</returns>
    internal static string GetText()
    {
        if (!IsClipboardFormatAvailable(CfUnicodeText) || !OpenClipboard(IntPtr.Zero))
        {
            return string.Empty;
        }

        try
        {
            var handle = GetClipboardData(CfUnicodeText);
            if (handle == IntPtr.Zero)
            {
                return string.Empty;
            }

            var source = GlobalLock(handle);
            if (source == IntPtr.Zero)
            {
                return string.Empty;
            }

            try
            {
                return Marshal.PtrToStringUni(source) ?? string.Empty;
            }
            finally
            {
                _ = GlobalUnlock(handle);
            }
        }
        finally
        {
            _ = CloseClipboard();
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GlobalAlloc(uint uFlags, nuint dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GlobalFree(IntPtr hMem);
}
