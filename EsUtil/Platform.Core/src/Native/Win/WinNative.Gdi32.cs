using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace EsUtil.Platform.Core.Native.Win;

// 本ファイルは gdi32.dll の宣言（画面キャプチャの DIB と BitBlt）を集約する。 型の説明は WinNative.cs を参照する。
// 宣言は CsWin32 が生成する（src/NativeMethods.txt）。 本ファイルは IntPtr を保つ薄い包みであり、
// 呼び出し側（WinCaptureService）の引数の型を変えない。
internal static partial class WinNative
{

    // ============================================================
    // 定数
    // ============================================================
    // --- 画面キャプチャ（DIB / BitBlt） ---
    /// <summary>無圧縮（BI_RGB）。 DIB の既定の圧縮方式。</summary>
    internal const BI_COMPRESSION BI_RGB = BI_COMPRESSION.BI_RGB;

    /// <summary>パレットを RGB の 3 成分で指定する（DIB_RGB_COLORS）。</summary>
    internal const DIB_USAGE DIB_RGB_COLORS = DIB_USAGE.DIB_RGB_COLORS;

    /// <summary>コピー元をそのままコピーするラスタ オペレーション（SRCCOPY）。</summary>
    internal const ROP_CODE SRCCOPY = ROP_CODE.SRCCOPY;

    /// <summary>重ね合わせウィンドウ（レイヤード ウィンドウ）も含めてコピーする BitBlt の追加フラグ。</summary>
    internal const ROP_CODE CAPTUREBLT = ROP_CODE.CAPTUREBLT;

    // ============================================================
    // gdi32.dll（画面キャプチャ）
    // ============================================================
    /// <summary>指定されたデバイス コンテキストと互換のメモリ デバイス コンテキストを作成する（gdi32.CreateCompatibleDC）。</summary>
    /// <param name="hdc">互換の基準にするデバイス コンテキスト。</param>
    /// <returns>作成されたメモリ デバイス コンテキスト。 失敗した場合は <see cref="IntPtr.Zero"/>。</returns>
    /// <remarks>不要になった時点で <see cref="DeleteDC"/> で削除する。</remarks>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static IntPtr CreateCompatibleDC(IntPtr hdc) => PInvoke.CreateCompatibleDC((HDC)hdc);
#pragma warning restore CA1416

    /// <summary>メモリ デバイス コンテキストを削除する（gdi32.DeleteDC）。</summary>
    /// <param name="hdc">削除するデバイス コンテキスト。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static bool DeleteDC(IntPtr hdc) => PInvoke.DeleteDC((HDC)hdc);
#pragma warning restore CA1416

    /// <summary>DIB セクションを作成し、ピクセルを書き込めるメモリを確保する（gdi32.CreateDIBSection）。</summary>
    /// <param name="hdc">互換の基準にするデバイス コンテキスト。</param>
    /// <param name="pbmi">作成する DIB の形式。</param>
    /// <param name="usage">パレットの指定（<see cref="DIB_RGB_COLORS"/>）。</param>
    /// <param name="ppvBits">ピクセル データへのポインターの出力先。</param>
    /// <param name="hSection">ファイル マッピング オブジェクト（0 でよい）。</param>
    /// <param name="offset">セクション内のオフセット（0 でよい）。</param>
    /// <returns>作成されたビットマップ。 失敗した場合は <see cref="IntPtr.Zero"/>。</returns>
    /// <remarks>
    /// ピクセルは 0 で初期化される。 32bpp（<see cref="BI_RGB"/>）では 1 行のバイト数が <c>幅 × 4</c> になり、
    /// コアの画像と同じ並び（BGRA・上から下・パディングなし）になる。<br/>
    /// <c>biHeight</c> を負値にするとトップダウン DIB になる。
    /// </remarks>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static unsafe IntPtr CreateDIBSection(
        IntPtr hdc, ref BITMAPINFO pbmi, DIB_USAGE usage, out IntPtr ppvBits, IntPtr hSection, uint offset)
    {
        fixed (BITMAPINFO* info = &pbmi)
        {
            void* bits;
            var handle = PInvoke.CreateDIBSection((HDC)hdc, info, usage, &bits, (HANDLE)hSection, offset);
            ppvBits = (IntPtr)bits;
            return handle;
        }
    }
#pragma warning restore CA1416

    /// <summary>デバイス コンテキストへオブジェクトを選択する（gdi32.SelectObject）。</summary>
    /// <param name="hdc">対象のデバイス コンテキスト。</param>
    /// <param name="h">選択するオブジェクト（ビットマップ等）。</param>
    /// <returns>置き換えられた以前のオブジェクト。 失敗した場合は <see cref="IntPtr.Zero"/>。</returns>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static IntPtr SelectObject(IntPtr hdc, IntPtr h) => PInvoke.SelectObject((HDC)hdc, (HGDIOBJ)h);
#pragma warning restore CA1416

    /// <summary>GDI オブジェクトを削除する（gdi32.DeleteObject）。</summary>
    /// <param name="ho">削除するオブジェクト。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static bool DeleteObject(IntPtr ho) => PInvoke.DeleteObject((HGDIOBJ)ho);
#pragma warning restore CA1416

    /// <summary>ビット ブロック転送でピクセルをコピーする（gdi32.BitBlt）。</summary>
    /// <param name="hdcDest">コピー先のデバイス コンテキスト。</param>
    /// <param name="xDest">コピー先の X 座標。</param>
    /// <param name="yDest">コピー先の Y 座標。</param>
    /// <param name="width">コピーする幅。</param>
    /// <param name="height">コピーする高さ。</param>
    /// <param name="hdcSrc">コピー元のデバイス コンテキスト。</param>
    /// <param name="xSrc">コピー元の X 座標（画面物理座標。 負値は仮想スクリーンの左端）。</param>
    /// <param name="ySrc">コピー元の Y 座標（画面物理座標。 負値は仮想スクリーンの上端）。</param>
    /// <param name="rop">ラスタ オペレーション（<see cref="SRCCOPY"/> 等）。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// 同期コールであり、DWM の応答が止まるとブロックする可能性がある（呼び出し側がタイムアウトで保護する）。<br/>
    /// 画面のピクセルは GDI の仕様でアルファ値を持たない（書き込まれない）ため、呼び出し側が不透明へ揃える。<br/>
    /// <b>注意</b>: GDI+ の <c>CopyFromScreen</c> と違い、<c>CAPTUREBLT</c> を付けない限りレイヤード ウィンドウは写らない。
    /// </remarks>
#pragma warning disable CA1416 // 生成された宣言の「Windows 5.1 以降」は本ライブラリの前提（Windows 10 以降）に含まれる
    internal static bool BitBlt(
        IntPtr hdcDest, int xDest, int yDest, int width, int height, IntPtr hdcSrc, int xSrc, int ySrc, ROP_CODE rop)
        => PInvoke.BitBlt((HDC)hdcDest, xDest, yDest, width, height, (HDC)hdcSrc, xSrc, ySrc, rop);
#pragma warning restore CA1416
}