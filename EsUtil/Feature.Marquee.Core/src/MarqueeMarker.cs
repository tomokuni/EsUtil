using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Marquee;

/// <summary>始点マーカー（十字線）の描画内容（描画に必要な値のみをコアが決定した結果）。</summary>
/// <param name="Center">十字線の中心（DIP）。</param>
/// <param name="Size">十字線の半幅（DIP）。</param>
/// <remarks>
/// <b>色と線の太さは含まない</b>: どちらも描画にだけ必要な設定（体裁）であり、描画層の外観が単一所有する。
/// </remarks>
public readonly record struct MarqueeMarker(
    EsPoint Center,
    double Size);
