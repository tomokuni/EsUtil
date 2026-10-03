using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Marquee.MewUI;

/// <summary>座標に関する通知のイベント引数（MewUI 版）。</summary>
/// <remarks>
/// 座標（<see cref="EsPoint"/>）に加え、通知時点のスナップショット（<see cref="MarqueeSnapshot"/>）を含む。<br/>
/// 座標は UI 非依存のコア型で公開する（コアの状態機械と同一の型のため、境界変換が不要になる）。<br/>
/// 座標変換はイベント引数では行わず、座標変換ポート（<see cref="EsUtil.Platform.Core.Coord.ICoordTransform"/>）方式が担う。
/// </remarks>
/// <param name="point">通知対象の座標（対象座標）。</param>
/// <param name="snapshot">通知時点のスナップショット。</param>
public sealed class MarqueePointEventArgs(EsPoint point, MarqueeSnapshot snapshot) : EventArgs
{
    /// <summary>通知対象の座標（対象座標）を取得する。</summary>
    /// <value>通知対象の座標（対象座標）。</value>
    public EsPoint Point { get; } = point;

    /// <summary>通知時点のスナップショット（状態・選択矩形・アクティブハンドル・マウス位置・操作フラグ）を取得する。</summary>
    /// <value>通知時点のスナップショット。</value>
    public MarqueeSnapshot Snapshot { get; } = snapshot;
}
