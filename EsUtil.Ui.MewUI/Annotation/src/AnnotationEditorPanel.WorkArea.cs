using System.Runtime.InteropServices;
using Aprillz.MewUI;

namespace EsUtil.Ui.MewUI.Annotation;

/// <summary>注釈のパネルを画面の作業領域の内側へ収める処理。</summary>
/// <remarks>
/// <b>画面の外へ出さない</b>: パネルは別ウィンドウなので、画面の外へ出ると見えなくなります。
/// モニターの作業領域（タスク バーを除いた範囲）で位置を挟みます。
/// </remarks>
public sealed partial class AnnotationEditorPanel
{
    /// <summary>指定した位置をモニターの作業領域の内側へ収める。</summary>
    /// <param name="reference">基準にするウィンドウ（モニターと表示倍率を決める）。</param>
    /// <param name="x">左上の X（画面座標。 DIP）。</param>
    /// <param name="y">左上の Y（画面座標。 DIP）。</param>
    /// <param name="width">置くウィンドウの幅（DIP）。</param>
    /// <param name="height">置くウィンドウの高さ（DIP）。</param>
    /// <returns>収めた位置（DIP）。</returns>
    /// <remarks>
    /// <b>作業領域はピクセル、位置は DIP</b>です。 表示倍率（125% など）を掛けずに挟むと、
    /// 画面の外へはみ出します。
    /// </remarks>
    private static (int X, int Y) ClampToWorkArea(Window reference, int x, int y, double width, double height)
    {
        if (!OperatingSystem.IsWindows() || reference.Handle == IntPtr.Zero)
        {
            return (x, y);
        }

        var monitor = Win32Monitor.MonitorFromWindow(reference.Handle, 2);
        if (monitor == IntPtr.Zero)
        {
            return (x, y);
        }

        var info = default(Win32Monitor.MonitorInfo);
        info.Size = Marshal.SizeOf<Win32Monitor.MonitorInfo>();
        if (!Win32Monitor.GetMonitorInfo(monitor, ref info))
        {
            return (x, y);
        }

        // 作業領域（ピクセル）を DIP へ写してから挟む
        var scale = reference.DpiScale > 0 ? reference.DpiScale : 1;
        var left = (int)Math.Round(info.Work.Left / scale);
        var top = (int)Math.Round(info.Work.Top / scale);
        var right = (int)Math.Round(info.Work.Right / scale);
        var bottom = (int)Math.Round(info.Work.Bottom / scale);
        var maxX = Math.Max(left, right - (int)Math.Round(width));
        var maxY = Math.Max(top, bottom - (int)Math.Round(height));
        return (Math.Clamp(x, left, maxX), Math.Clamp(y, top, maxY));
    }

    /// <summary>モニターの作業領域を問い合わせる Win32 の窓口。</summary>
    private static class Win32Monitor
    {
        /// <summary>矩形（画面座標）。</summary>
        internal struct Rect
        {
            /// <summary>左端。</summary>
            internal int Left;

            /// <summary>上端。</summary>
            internal int Top;

            /// <summary>右端。</summary>
            internal int Right;

            /// <summary>下端。</summary>
            internal int Bottom;
        }

        /// <summary>モニターの情報。</summary>
        internal struct MonitorInfo
        {
            /// <summary>構造体の大きさ（呼ぶ前に設定する）。</summary>
            internal int Size;

            /// <summary>モニター全体の矩形。</summary>
            internal Rect Monitor;

            /// <summary>作業領域の矩形（タスク バーを除く）。</summary>
            internal Rect Work;

            /// <summary>主モニターかどうかの印。</summary>
            internal int Flags;
        }

        /// <summary>ウィンドウが載っているモニターを求める。</summary>
        /// <param name="handle">ウィンドウのハンドル。</param>
        /// <param name="flags">既定（2 = 最も近いモニター）。</param>
        /// <returns>モニターのハンドル。</returns>
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll")]
        internal static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

        /// <summary>モニターの情報を求める。</summary>
        /// <param name="monitor">モニターのハンドル。</param>
        /// <param name="info">受け取る情報。</param>
        /// <returns>成功した場合は <see langword="true"/>。</returns>
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll")]
        internal static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    }
}
