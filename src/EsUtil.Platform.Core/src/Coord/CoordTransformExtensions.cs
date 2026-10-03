namespace EsUtil.Platform.Core.Coord;

/// <summary>座標変換ポート（<see cref="ICoordTransform"/>）の既定解決の共通ヘルパー。</summary>
/// <remarks>
/// 「座標変換ポートが未設定（null）の場合は等価変換（<see cref="CoordTransform.Identity"/>）として扱う」規則を単一所有する。<br/>
/// コントロール・コアのジオメトリは <c>port ?? CoordTransform.Identity</c> を自前で書かず本メソッドへ委譲する<br/>
/// （各所で書くと、ポート未設定時の扱いが UI ごとにずれる）。<br/>
/// 拡張メソッド（<c>this ICoordTransform?</c> 第一引数）として提供し、呼び出し側は <c>port.OrIdentity()</c> と記述する。
/// </remarks>
public static class CoordTransformExtensions
{
    /// <summary>座標変換ポートを解決する。</summary>
    /// <param name="port">座標変換ポート。 null 可。</param>
    /// <returns>指定されたポート。 null の場合は <see cref="CoordTransform.Identity"/>（等価変換）。</returns>
    public static ICoordTransform OrIdentity(this ICoordTransform? port) => port ?? CoordTransform.Identity;
}
