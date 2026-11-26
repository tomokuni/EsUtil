using System.Runtime.InteropServices;

namespace EsUtil.Ui.WinForms.Interop.Native;

/// <summary>IME（imm32.dll）の呼び出しと、その引数の構造体を単一所有する。</summary>
/// <remarks>
/// <b>プラットフォーム固有の宣言を機能のソースへ置かない</b>: P/Invoke と構造体のレイアウトは本フォルダーが
/// 単一所有し、機能側は意味のある操作（<see cref="GetContext"/> 等）だけを呼ぶ。<br/>
/// <b>文字列は UTF-16</b>である（<c>ImmGetCompositionStringW</c> を使う）。
/// </remarks>
internal static class ImeInterop
{
    /// <summary>IME の変換開始（<c>WM_IME_STARTCOMPOSITION</c>）。</summary>
    internal const int WmImeStartComposition = 0x0105;

    /// <summary>IME の変換終了（<c>WM_IME_ENDCOMPOSITION</c>）。</summary>
    internal const int WmImeEndComposition = 0x0106;

    /// <summary>IME の変換内容（<c>WM_IME_COMPOSITION</c>）。</summary>
    internal const int WmImeComposition = 0x010F;

    /// <summary>確定した文字列（<c>GCS_RESULTSTR</c>）。</summary>
    internal const int GcsResultStr = 0x0800;

    /// <summary>変換中の文字列（<c>GCS_COMPSTR</c>）。</summary>
    internal const int GcsCompStr = 0x0008;

    /// <summary>変換候補の位置を指定する（<c>CFS_POINT</c>）。</summary>
    internal const int CfsPoint = 0x0002;

    /// <summary>IME のコンテキストを取得する。</summary>
    /// <param name="window">対象のウィンドウ。</param>
    /// <returns>コンテキスト（取得できない場合は <see cref="IntPtr.Zero"/>）。</returns>
    internal static IntPtr GetContext(IntPtr window) => ImmGetContext(window);

    /// <summary>IME のコンテキストを解放する。</summary>
    /// <param name="window">対象のウィンドウ。</param>
    /// <param name="context">解放するコンテキスト。</param>
    /// <returns>解放できた場合は <see langword="true"/>。</returns>
    internal static bool ReleaseContext(IntPtr window, IntPtr context) => ImmReleaseContext(window, context);

    /// <summary>IME の変換文字列を読み取る。</summary>
    /// <param name="context">IME のコンテキスト。</param>
    /// <param name="index">読み取る種類（<c>GCS_COMPSTR</c> / <c>GCS_RESULTSTR</c>）。</param>
    /// <param name="buffer">読み取り先（長さの取得だけなら <see langword="null"/>）。</param>
    /// <param name="length">読み取り先の長さ（バイト）。</param>
    /// <returns>読み取ったバイト数（失敗した場合は負の値）。</returns>
    internal static int GetCompositionString(IntPtr context, int index, byte[]? buffer, int length)
        => ImmGetCompositionStringW(context, index, buffer, length);

    /// <summary>変換候補のウィンドウの位置を設定する。</summary>
    /// <param name="context">IME のコンテキスト。</param>
    /// <param name="form">位置の指定。</param>
    /// <returns>設定できた場合は <see langword="true"/>。</returns>
    internal static bool SetCompositionWindow(IntPtr context, ref CompositionForm form)
        => ImmSetCompositionWindow(context, ref form);

    /// <summary>変換候補のウィンドウの指定（<c>COMPOSITIONFORM</c>）。</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct CompositionForm
    {
        /// <summary>指定の種類（<c>CFS_POINT</c> など）。</summary>
        public int Style;

        /// <summary>候補の位置。</summary>
        public NativePoint CurrentPosition;

        /// <summary>除外する矩形（使わない）。</summary>
        public NativeRectangle Area;
    }

    /// <summary>整数の点（<c>POINT</c>）。</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativePoint
    {
        /// <summary>X 座標。</summary>
        public int X;

        /// <summary>Y 座標。</summary>
        public int Y;
    }

    /// <summary>整数の矩形（<c>RECT</c>）。</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRectangle
    {
        /// <summary>左端。</summary>
        public int Left;

        /// <summary>上端。</summary>
        public int Top;

        /// <summary>右端。</summary>
        public int Right;

        /// <summary>下端。</summary>
        public int Bottom;
    }

    [DllImport("imm32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr ImmGetContext(IntPtr hWnd);

    [DllImport("imm32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);

    [DllImport("imm32.dll", CharSet = CharSet.Unicode, EntryPoint = "ImmGetCompositionStringW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int ImmGetCompositionStringW(IntPtr hIMC, int dwIndex, byte[]? lpBuf, int dwBufLen);

    [DllImport("imm32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool ImmSetCompositionWindow(IntPtr hIMC, ref CompositionForm lpCompositionForm);
}
