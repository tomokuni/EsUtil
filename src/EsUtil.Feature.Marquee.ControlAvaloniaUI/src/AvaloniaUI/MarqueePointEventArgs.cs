using Avalonia;

namespace EsUtil.Feature.Marquee.AvaloniaUI;

/// <summary>座標に関する通知のイベント引数。</summary>
/// <remarks>
/// 座標（<see cref="Point"/>）に加え、通知時点のスナップショット（<see cref="Snapshot"/>）を含む。<br/>
/// スクロール補正はイベント引数では行わず、座標変換ポート（<see cref="EsUtil.Platform.Core.Coord.ICoordTransform"/>）方式が担う
/// （<c>CoordTransform.ClientToSource</c> に倍率と境界を与えて変換に組み込む）。
/// </remarks>
/// <param name="point">通知対象の座標（対象座標）。</param>
/// <param name="snapshot">通知時点のスナップショット。</param>
public sealed class MarqueePointEventArgs(Point point, MarqueeSnapshot snapshot) : EventArgs
{
    /// <summary>通知対象の座標（対象座標）。</summary>
    public Point Point { get; } = point;

    /// <summary>通知時点のスナップショット（状態・選択矩形・アクティブハンドル・マウス位置・操作フラグ）。</summary>
    public MarqueeSnapshot Snapshot { get; } = snapshot;
}
