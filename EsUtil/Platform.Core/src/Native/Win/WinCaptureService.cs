using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>画面のキャプチャ機能を提供するクラス。</summary>
/// <remarks>
/// GDI の <c>BitBlt</c>（Win32 API）で画面のピクセルをメモリへコピーする。<br/>
/// <b>Win32 の宣言は CsWin32 が生成する</b>（<c>src/NativeMethods.txt</c>）。 本クラスは <c>PInvoke</c> を直接呼ぶ。<br/>
/// <see cref="ICaptureService"/> の実装であり、テスト時のモックに備える。<br/>
/// 公開 API（<see cref="ICaptureService"/>）は UI 非依存の型（<see cref="EsBitmap"/> / <see cref="EsRectInt"/>）のみを使用する。<br/>
/// ホスト（アプリ）が生成して注入する（Windows のみ。 テストではモックを注入できる）。<br/>
/// GDI は Windows 専用のため、本クラスは <see cref="SupportedOSPlatformAttribute"/> で Windows に限定する。<br/>
/// 継承不可。<br/>
/// </remarks>
[SupportedOSPlatform("windows10.0.17763")]
public sealed class WinCaptureService : ICaptureService
{
    // ====================================================================
    // 公開メソッド（プラットフォーム非依存の型のみ）
    // ====================================================================

    /// <summary>指定された画面領域をキャプチャし、<see cref="EsBitmap"/> として返す。</summary>
    /// <param name="bounds">キャプチャする画面領域（画面物理座標）。</param>
    /// <returns>キャプチャした画像。 破棄は呼び出し側。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bounds"/> の幅または高さが 0 以下の場合。</exception>
    /// <exception cref="Win32Exception">画面の取得（デバイス コンテキスト・DIB・<c>BitBlt</c>）に失敗した場合。</exception>
    /// <remarks>
    /// 内部で GDI の <c>BitBlt</c> を使用し、<see cref="EsBitmap"/> のバッファへ直接コピーする
    /// （中間の画像を経由しない）。<br/>
    /// 32bpp のトップダウン DIB はコアの画像と同じ並び（BGRA・上から下・パディングなし）になるため、
    /// 行単位ではなく全領域を 1 回でコピーできる。<br/>
    /// 画面のピクセルは GDI の仕様でアルファ値を持たない（書き込まれない）ため、<b>コピー後に不透明（255）へ揃える</b>。<br/>
    /// <b>同期コールである</b>: DWM の応答が止まるとブロックする可能性がある（呼び出し側がタイムアウトで保護する）。<br/>
    /// 公開 API は UI 非依存の型（<see cref="EsBitmap"/> / <see cref="EsRectInt"/>）のみを使用する。<br/>
    /// </remarks>
    public EsBitmap CaptureArea(EsRectInt bounds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bounds.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bounds.Height);

        return CaptureScreen(bounds);
    }

    /// <summary>仮想スクリーン（全モニタを包含する領域）全体を 1 回のキャプチャで取得する。</summary>
    /// <param name="origin">仮想スクリーンの原点（全モニタのバウンディングボックス左上、画面物理座標）。</param>
    /// <returns>仮想スクリーン全体の画像。 キャプチャ失敗時は null。 破棄は呼び出し側。</returns>
    /// <remarks>
    /// 仮想スクリーン境界は Win32 の <c>GetSystemMetrics</c>（SM_XVIRTUALSCREEN 等）で取得し、<br/>
    /// 全モニタを包含する矩形を 1 回の <c>BitBlt</c> でキャプチャする。<br/>
    /// 境界取得とキャプチャはスレッド非依存のため、バックグラウンドスレッドから安全に実行できる。<br/>
    /// </remarks>
    public EsBitmap? CaptureVirtualScreen(out EsPointInt origin)
    {
        // Win32 の仮想スクリーン境界（全モニタのバウンディングボックス）を取得する
        // （境界の計算は EsWindowPlacementMath が単一所有する）
        var bounds = EsWindowPlacementMath.GetVirtualScreenBounds();
        origin = new EsPointInt(bounds.X, bounds.Y);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            // 境界が取得できない場合はキャプチャできない（null はこの 1 つの意味だけを表す）
            return null;
        }

        return CaptureArea(bounds);
    }

    // ====================================================================
    // 内部実装（Win32 の資源を閉じる）
    // ====================================================================

    /// <summary>画面の領域をキャプチャして書き換え可能な画像を生成する（内部ヘルパー）。</summary>
    /// <param name="bounds">キャプチャする画面領域（画面物理座標）。</param>
    /// <returns>キャプチャした画像。 所有権は呼び出し側へ移る。</returns>
    /// <exception cref="Win32Exception">GDI の操作に失敗した場合。</exception>
    /// <remarks>
    /// <b>失敗の扱い</b>: GDI の失敗は <see cref="Win32Exception"/> として呼び出し側へ伝搬する
    /// （Windows の操作が失敗した原因と<b>対象領域</b>をメッセージへ残す）。<br/>
    /// 中途で失敗した場合は確保済みの画像を破棄してから伝搬する（資源の後始末は本クラスが所有する）。
    /// </remarks>
    private static EsBitmap CaptureScreen(EsRectInt bounds)
    {
        var bitmap = EsBitmap.CreateUninitialized(bounds.Width, bounds.Height);
        try
        {
            CopyScreenInto(bitmap, bounds);
        }
        catch
        {
            // 確保済みの画像を破棄して伝搬する
            bitmap.Dispose();
            throw;
        }

        return bitmap;
    }

    /// <summary>画面の領域を 32bpp の DIB へコピーし、コアの画像のバッファへ写す（内部ヘルパー）。</summary>
    /// <param name="target">コピー先の画像。</param>
    /// <param name="bounds">キャプチャする画面領域（画面物理座標）。</param>
    /// <exception cref="Win32Exception">GDI の操作に失敗した場合。</exception>
    /// <remarks>
    /// <b>座標</b>: 画面の座標はプライマリ モニタの左上を原点とするため、仮想スクリーンの左端・上端は負値になる。<br/>
    /// <c>BitBlt</c> のコピー元座標は負値を許容する（クリップされる）が、コピー先は常に (0, 0) から始める。<br/>
    /// <c>biHeight</c> を負値にしてトップダウン DIB にする（行の並びを画像と一致させる）。
    /// </remarks>
    private static unsafe void CopyScreenInto(EsBitmap target, EsRectInt bounds)
    {
        HDC screenDc = default;
        HDC memoryDc = default;
        HBITMAP dib = default;
        HGDIOBJ previous = default;
        try
        {
            screenDc = PInvoke.GetDC(default);
            if (screenDc.IsNull)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "画面のデバイス コンテキストを取得できませんでした。");
            }

            memoryDc = PInvoke.CreateCompatibleDC(screenDc);
            if (memoryDc.IsNull)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "メモリ デバイス コンテキストを作成できませんでした。");
            }

            var info = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = bounds.Width,
                    biHeight = -bounds.Height,
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = (uint)BI_COMPRESSION.BI_RGB,
                },
            };

            unsafe
            {
                void* bits;
                dib = PInvoke.CreateDIBSection(memoryDc, &info, DIB_USAGE.DIB_RGB_COLORS, &bits, default, 0);
                if (dib.IsNull || bits is null)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "キャプチャ用の DIB セクションを作成できませんでした。");
                }

                previous = PInvoke.SelectObject(memoryDc, (HGDIOBJ)dib.Value);
                if (previous.IsNull)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "キャプチャ用のビットマップを選択できませんでした。");
                }

                // 重ね合わせウィンドウ（レイヤード ウィンドウ）も写して、GDI+ の CopyFromScreen と同じ見え方にする
                if (!PInvoke.BitBlt(
                    memoryDc, 0, 0, bounds.Width, bounds.Height, screenDc, bounds.X, bounds.Y,
                    ROP_CODE.SRCCOPY | ROP_CODE.CAPTUREBLT))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "画面のコピーに失敗しました。");
                }

                CopyPixels(target, (IntPtr)bits);
            }
        }
        finally
        {
            // GDI オブジェクトは選択を戻してから削除する（選択中のビットマップは削除できない）
            if (!memoryDc.IsNull && !previous.IsNull)
            {
                _ = PInvoke.SelectObject(memoryDc, previous);
            }

            if (!dib.IsNull)
            {
                _ = PInvoke.DeleteObject((HGDIOBJ)dib.Value);
            }

            if (!memoryDc.IsNull)
            {
                _ = PInvoke.DeleteDC(memoryDc);
            }

            if (!screenDc.IsNull)
            {
                _ = PInvoke.ReleaseDC(default, screenDc);
            }
        }
    }

    /// <summary>DIB のピクセルをコアの画像のバッファへコピーし、アルファを不透明へ揃える。</summary>
    /// <param name="target">コピー先の画像。</param>
    /// <param name="source">DIB のピクセル データへのポインター。</param>
    /// <remarks>
    /// 32bpp の DIB の 1 行は <c>幅 × 4</c> バイトで、コアの画像のストライドと一致する（パディングなし）ため、
    /// 行ごとではなく全領域を 1 回でコピーする。<br/>
    /// 画面のピクセルにはアルファが無い（GDI は書き込まないため 0 のまま）ので、不透明へ揃える。
    /// </remarks>
    private static unsafe void CopyPixels(EsBitmap target, IntPtr source)
    {
        var destination = target.Pixels;
        new ReadOnlySpan<byte>((byte*)source, destination.Length).CopyTo(destination);

        for (var i = EsReadOnlyBitmap.BytesPerPixel - 1; i < destination.Length; i += EsReadOnlyBitmap.BytesPerPixel)
        {
            destination[i] = byte.MaxValue;
        }
    }
}
