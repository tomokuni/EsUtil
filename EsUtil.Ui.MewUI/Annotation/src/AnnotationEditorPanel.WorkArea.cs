using Aprillz.MewUI;
using EsUtil.Feature.Annotation.Presenter.Panel;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native.Win;

namespace EsUtil.Ui.MewUI.Annotation;

/// <summary>注釈のパネルを画面の作業領域の内側へ収める処理。</summary>
/// <remarks>
/// <b>画面の外へ出さない</b>: パネルは別ウィンドウなので、画面の外へ出ると見えなくなります。
/// モニターの作業領域（タスク バーを除いた範囲）で位置を挟みます。<br/>
/// <b>照会と規則はほかの層が単一所有する</b>: 作業領域の照会は <see cref="WinMonitorWorkArea"/>、
/// 収める規則は <see cref="AnnotationPanelPlacement.Clamp"/> を使います。
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
        if (!OperatingSystem.IsWindows()
            || WinMonitorWorkArea.GetWorkArea(reference.Handle) is not { } workArea)
        {
            return (x, y);
        }

        // 作業領域（ピクセル）を DIP へ写してから挟む
        var scale = reference.DpiScale > 0 ? reference.DpiScale : 1;
        var available = new EsRect(
            workArea.X / scale,
            workArea.Y / scale,
            workArea.Width / scale,
            workArea.Height / scale);
        var clamped = AnnotationPanelPlacement.Clamp(new EsPoint(x, y), width, height, available);
        return ((int)Math.Round(clamped.X), (int)Math.Round(clamped.Y));
    }
}
