using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Platform.Core.Input;

/// <summary>押したままの移動が「ドラッグの開始」と見なせるかを判定する。</summary>
/// <remarks>
/// <b>判定の規則を 1 か所に置く</b>: 押した位置から少し動いただけでは、利用者の意図はクリックかもしれない。
/// 各 UI が「何 DIP 動いたらドラッグとするか」を書き写すと、同じ操作でも UI ごとに動き出す距離が変わる。<br/>
/// <b>座標は同じ基準で渡す</b>（画面の座標など、動かしても基準が変わらない座標）。
/// ウィンドウや要素の座標は、ドラッグでウィンドウが動くと基準も動くため、そのままでは距離を正しく測れない。
/// </remarks>
public static class DragThreshold
{
    /// <summary>ドラッグの開始と見なす最小の移動距離（DIP）。</summary>
    /// <remarks>
    /// <b>Windows の既定のドラッグ開始距離に合わせる</b>（<c>SystemInformation.DragSize</c> と同じ 4）。
    /// 小さくすると、押した指のわずかなぶれでクリックがドラッグになる。
    /// </remarks>
    public const double DefaultDistance = 4;

    /// <summary>押した位置からドラッグの開始と見なせる距離を超えて動いたかどうかを返す。</summary>
    /// <param name="pressPosition">押した位置。</param>
    /// <param name="currentPosition">いまの位置（<paramref name="pressPosition"/> と同じ基準）。</param>
    /// <param name="distance">ドラッグと見なす最小の移動距離（DIP。 0 以上）。</param>
    /// <returns>いずれかの軸で <paramref name="distance"/> 以上動いていれば <see langword="true"/>。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="distance"/> が負の場合。</exception>
    public static bool IsExceeded(
        EsPoint pressPosition, EsPoint currentPosition, double distance = DefaultDistance)
    {
        if (distance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distance), distance, "距離は 0 以上である必要があります。");
        }

        return Math.Abs(currentPosition.X - pressPosition.X) >= distance
            || Math.Abs(currentPosition.Y - pressPosition.Y) >= distance;
    }
}
