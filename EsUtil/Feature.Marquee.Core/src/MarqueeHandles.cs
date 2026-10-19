using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Marquee;

/// <summary>ドラッグハンドルの描画内容（描画に必要な値のみをコアが決定した結果）。</summary>
/// <param name="HandleDipRects">ハンドルの矩形（DIP。 ハンドル 8 方向の位置と大きさをコアが解決済み）。</param>
/// <param name="BorderThickness">ハンドル枠の線の太さ（DIP）。</param>
/// <remarks>
/// ハンドルの中心位置（8 方向）と一辺サイズの適用は <see cref="MarqueeCore.ResolveGeometry"/> が単一所有するため、<br/>
/// 描画側は本値を渡して描くだけとする（位置を描画層が計算すると、ハンドルのヒットテストと同じ式が 2 箇所になる）。<br/>
/// <b>色は含まない</b>: 色は描画にだけ必要な設定であり、描画層の外観が単一所有する。
/// </remarks>
public readonly record struct MarqueeHandles(
    IReadOnlyList<EsRect> HandleDipRects,
    double BorderThickness);
