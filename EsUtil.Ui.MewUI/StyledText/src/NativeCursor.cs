using System.Runtime.InteropServices;

namespace EsUtil.Ui.MewUI.StyledText;

/// <summary>マウス カーソルの画面位置を取得する（ポップアップのドラッグ用）。</summary>
/// <remarks>
/// <b>イベントの座標を使わない</b>: ポップアップを動かすと、イベントの座標の基準（要素・ウィンドウ）も
/// 一緒に動くため、その差を取ると移動量が増幅される（実測）。 画面の位置は動かしても変わらないため、
/// ドラッグの差を正しく取れる。<br/>
/// <b>本 UI 版だけの処理</b>: MewUI は画面のカーソル位置を公開していないため、OS の照会だけをここへ置く
/// （他の UI 版はフレームワークの座標で足りる）。
/// </remarks>
internal static class NativeCursor
{
    /// <summary>カーソルの画面位置（物理ピクセル）を取得する。</summary>
    /// <param name="x">取得した X 座標。</param>
    /// <param name="y">取得した Y 座標。</param>
    /// <returns>取得できた場合は <see langword="true"/>。</returns>
    internal static bool TryGetPosition(out int x, out int y)
    {
        if (GetCursorPos(out var point))
        {
            x = point.X;
            y = point.Y;
            return true;
        }

        x = 0;
        y = 0;
        return false;
    }

    /// <summary>画面の点。</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        /// <summary>X 座標。</summary>
        public int X;

        /// <summary>Y 座標。</summary>
        public int Y;
    }

    /// <summary>カーソルの画面位置を取得する。</summary>
    /// <param name="point">取得した位置。</param>
    /// <returns>取得できた場合は <see langword="true"/>。</returns>
    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point point);
}
