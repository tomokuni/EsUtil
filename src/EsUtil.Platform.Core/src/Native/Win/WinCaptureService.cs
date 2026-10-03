using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Native.Imaging;
using Microsoft.Extensions.Logging;
using DrawingBitmap = System.Drawing.Bitmap;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingRect = System.Drawing.Rectangle;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>画面/ウィンドウのキャプチャ機能を提供するクラス。</summary>
/// <remarks>
/// CopyFromScreen（<c>System.Drawing.Graphics</c>）および PrintWindow（Win32 API）を<br/>
/// 使用して画面/ウィンドウのキャプチャを行う。<br/>
/// P/Invoke 呼び出しは <c>WinNative</c> に委譲する。<br/>
/// <see cref="ICaptureService"/> の実装であり、テスト時のモックに備える。<br/>
/// Windows 固有の型（System.Drawing.Bitmap / Rectangle / IntPtr）は本クラス（Win 実装）の内部に閉じ、<br/>
/// 公開 API（<see cref="ICaptureService"/>）は UI 非依存の型（<see cref="EsBitmap"/> / <see cref="EsRectInt"/>）のみを使用する。<br/>
/// ホスト（アプリ）が生成して注入する（Windows のみ。 テストではモックを注入できる）。<br/>
/// System.Drawing は Windows 専用のため、本クラスは <see cref="SupportedOSPlatformAttribute"/> で Windows に限定する。<br/>
/// 継承不可。<br/>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed partial class WinCaptureService(ILogger? logger = null) : ICaptureService
{
    /// <summary>内部ロガー（null の場合は出力なしに正規化）。</summary>
    private readonly ILogger _log = logger.OrNullLogger();

    // ====================================================================
    // 公開メソッド（プラットフォーム非依存の型のみ）
    // ====================================================================

    /// <summary>指定された画面領域をキャプチャし、EsBitmap として返す。</summary>
    /// <param name="bounds">キャプチャする画面領域（画面物理座標）。</param>
    /// <returns>キャプチャした EsBitmap。 失敗時は null。 破棄は呼び出し側。</returns>
    /// <remarks>
    /// 内部で System.Drawing（CopyFromScreen）を使用してキャプチャし、<see cref="EsBitmap"/> へ変換する。<br/>
    /// <see cref="EsBitmap"/>（SKBitmap 内部）の生成はスレッド非依存のため、<br/>
    /// CopyFromScreen（GDI 同期コール）と同一のバックグラウンドスレッドで実行可能である。<br/>
    /// 公開 API は UI 非依存の型（<see cref="EsBitmap"/> / <see cref="EsRectInt"/>）のみを使用する。<br/>
    /// </remarks>
    public EsBitmap? CaptureArea(EsRectInt bounds)
    {
        var drawingRect = new DrawingRect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        using var bitmap = CaptureBitmap(drawingRect);
        return bitmap.ToEsBitmap();
    }

    /// <summary>仮想スクリーン（全モニタを包含する領域）全体を 1 回のキャプチャで取得する。</summary>
    /// <param name="origin">仮想スクリーンの原点（全モニタのバウンディングボックス左上、画面物理座標）。</param>
    /// <returns>仮想スクリーン全体の EsBitmap。 キャプチャ失敗時は null。 破棄は呼び出し側。</returns>
    /// <remarks>
    /// 仮想スクリーン境界は Win32 の <c>GetSystemMetrics</c>（SM_XVIRTUALSCREEN 等）で取得し、<br/>
    /// 全モニタを包含する矩形を 1 回の CopyFromScreen でキャプチャする。<br/>
    /// 境界取得とキャプチャはスレッド非依存のため、バックグラウンドスレッドから安全に実行できる。<br/>
    /// </remarks>
    public EsBitmap? CaptureVirtualScreen(out EsPointInt origin)
    {
        // Win32 の仮想スクリーン境界（全モニタのバウンディングボックス）を取得
        var (vx, vy, vw, vh) = WinNative.GetVirtualScreenBounds();
        origin = new EsPointInt(vx, vy);
        if (vw <= 0 || vh <= 0)
        {
            // 境界が取得できない場合はキャプチャできない（OS 操作の失敗として記録する）
            _log.LogVirtualScreenBoundsInvalid(vx, vy, vw, vh);
            return null;
        }

        _log.LogVirtualScreenBounds(vx, vy, vw, vh);
        return CaptureArea(new EsRectInt(vx, vy, vw, vh));
    }

    /// <summary>指定された画面領域を System.Drawing.Bitmap としてキャプチャする（内部ヘルパー）。</summary>
    /// <param name="bounds">キャプチャする画面領域（画面物理座標）。</param>
    /// <returns>キャプチャした System.Drawing.Bitmap。</returns>
    /// <exception cref="ExternalException">CopyFromScreen（GDI+）が失敗した場合。</exception>
    /// <remarks>
    /// CopyFromScreen は GDI の同期コールであり、DWM 応答不能時にはブロックする可能性がある。<br/>
    /// 呼び出し側（SelectionSession）はバックグラウンド実行とタイムアウトで保護する。<br/>
    /// <c>Format32bppPArgb</c>（アルファ乗算済み・不透明）で生成し、<see cref="EsBitmapDrawingExtensions.ToEsBitmap(System.Drawing.Bitmap)"/>
    /// が PArgb を直接読み取れるようにする（アルファ強制不要）。<br/>
    /// <b>GDI+ の失敗は記録して呼び出し側へ伝搬する</b>（Windows の操作が失敗した原因を残す）。
    /// </remarks>
    private DrawingBitmap CaptureBitmap(DrawingRect bounds)
    {
        _log.LogCaptureArea(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        var bitmap = new DrawingBitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            try
            {
                g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
            }
            catch (ExternalException ex)
            {
                // 記録してから伝搬する（呼び出し側の振る舞いを変えない）
                _log.LogCaptureFailed(bounds.X, bounds.Y, bounds.Width, bounds.Height, ex);
                bitmap.Dispose();
                throw;
            }
        }

        _log.LogCaptureAreaDone(bitmap.Width, bitmap.Height);
        return bitmap;
    }
}
