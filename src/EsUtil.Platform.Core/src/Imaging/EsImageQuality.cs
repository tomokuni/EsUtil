namespace EsUtil.Platform.Core.Imaging;

/// <summary>画像を変換するときの品質（速度と画質の釣り合い。 縮小と減色が使う）。</summary>
/// <remarks>
/// <b>3 段階の意味は操作ごとに決まる</b>（どちらも「低いほど速く画質が低い」であり、段階の数え方は同じ）:
///
/// <list type="table">
/// <item>
///   <term><see cref="Low"/></term>
///   <description><b>最速</b>。 <b>縮小</b>は最近傍補間（縮小先の 1 画素につき元の 1 画素だけを読む）、
///   <b>減色</b>は中央値分割で分けた箱の平均色をそのまま使う（色の詰め直しを行わない）。</description>
/// </item>
/// <item>
///   <term><see cref="Mid"/></term>
///   <description><b>既定</b>。 <b>縮小</b>は双線形補間（元の 2x2 画素を重み付き平均する）、<b>減色</b>は中央値分割の
///   結果を反復（k-means）で詰め直す（「使われている箱の数 × 色数」で決まる作業量の上限つき）。</description>
/// </item>
/// <item>
///   <term><see cref="High"/></term>
///   <description><b>最も画質が高い</b>。 <b>縮小</b>は面積平均（box フィルター。 縮小先の 1 画素を対応する元の矩形の
///   平均で作るため、文字・罫線が落ちない）、<b>減色</b>は反復の上限を外して収束まで詰め直す。</description>
/// </item>
/// </list>
///
/// <b>縮小の所要（実測。 Release・4K 3840x2160 の画像）</b>: 長辺 320 へ縮小する場合は
/// <see cref="Low"/> 0.07 ms / <see cref="Mid"/> 0.34 ms / <see cref="High"/> 1.8 ms です。
/// <b><see cref="High"/> の費用は元画像の画素数で決まり、縮小先の寸法にはよらない</b>ため、縮小先が大きい場合は
/// <see cref="Mid"/> より速くなることがあります（実測: 長辺 1920 へ縮小すると <see cref="Mid"/> 4.1 ms に対し
/// <see cref="High"/> 3.4 ms。 <see cref="Mid"/> は縮小先の 1 画素につき元の 4 画素を読む）。
/// 画質は 40 倍の縮小で幅 1 px の細線を残せるかで比べると、<see cref="Low"/> は輝度 0（位置が合えば残るが
/// 落ちやすい）・<see cref="Mid"/> も 0・<see cref="High"/> は 6 です。 立方畳み込み（bicubic）を段階へ加えない理由と
/// 実測は <see cref="EsReadOnlyBitmap.Resize(int, EsImageQuality)"/> の備考を参照してください。
///
/// <b>減色では <see cref="Low"/> に対する差が 1〜12%（実測）</b>: 1024x768 の画像を 256 色へ減色した場合の色差
/// （1 チャネルあたりの平均絶対誤差）は、グラデーション 2.91 → 2.79、写真風 3.28 → 3.15、ノイズ 7.85 → 7.75 です
/// （16 色ではグラデーション 10.74 → 10.59、写真風 <b>14.34 → 12.64</b> と、<b>色数が少ないほど効果が大きい</b>）。
/// <b><see cref="High"/> と <see cref="Mid"/> の差は小さい</b>: 使われている箱が多い画像だけに効きます
/// （実測: ノイズ 256 色で 7.75 → 7.55 ＝ 2.6%。 ノイズは 60 ms → 155 ms。 グラデーションと写真風は
/// 既定の作業量の上限でも収束するため<b>同じ結果</b>になる）。
///
/// <b>見た目の差が最も大きいのはディザ</b>であるため、減色では <see cref="EsDitherMode"/> も併せて指定してください
/// （<b>色数が内容に対して足りないときだけ</b>。 <see cref="EsDitherMode"/> の備考に実測があります）。
/// </remarks>
public enum EsImageQuality
{
    /// <summary>最速（画質は最も低い）。</summary>
    Low,

    /// <summary>速度と画質の釣り合い（既定）。</summary>
    Mid,

    /// <summary>最も画質が高い（最も遅い場合がある）。</summary>
    High,
}
