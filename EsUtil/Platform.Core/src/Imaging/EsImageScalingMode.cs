namespace EsUtil.Platform.Core.Imaging;

/// <summary>画像を縮小するときの方法（どの補間で縮小するか）。</summary>
/// <remarks>
/// <b>2 系統あります</b>: <c>Wic</c> で始まる値は <b>OS の WIC（<c>IWICBitmapScaler</c>）</b>へ任せ、
/// <c>Es</c> で始まる値は <b>本ライブラリの計算</b>で縮小します。
///
/// <list type="table">
/// <item>
///   <term><see cref="WicNearestNeighbor"/></term>
///   <description>WIC の最近傍。 <b>最も低い画質</b>（位置が合えば細線が残り、合わなければ消える）。</description>
/// </item>
/// <item>
///   <term><see cref="WicLinear"/></term>
///   <description>WIC の双線形。 2x2 の重み付き平均。 縮小率がちょうど 2 倍のときだけ面積平均と一致します。</description>
/// </item>
/// <item>
///   <term><see cref="WicCubic"/></term>
///   <description>WIC の立方畳み込み（タップ数が少ない）。 <b>縮小には向きません</b>（4 倍以上の縮小でリンギングが増え、双線形より誤差が大きくなります）。</description>
/// </item>
/// <item>
///   <term><see cref="WicFant"/></term>
///   <description>WIC の Fant。<b>既定</b>。 画素の面積平均と同じフィルタ（丸めだけが四捨五入）であり、
///   細線・罫線が落ちにくい。 <b>読み込み時の縮小（<see cref="EsReadOnlyBitmap.Load(string, int)"/>）と同じ補間</b>です。</description>
/// </item>
/// <item>
///   <term><see cref="WicHighQualityCubic"/></term>
///   <description>WIC の高品質立方畳み込み。<b>最も遅い</b>（Fant の 2〜3 倍）。 写真風の鮮鋭さは最も高い一方、
///   細線の縁に白黒の縞（リンギング）が出ます。</description>
/// </item>
/// <item>
///   <term><see cref="EsNearestNeighbor"/></term>
///   <description>本ライブラリの最近傍。<b>最も速い</b>（縮小先の画素だけを読む）。 画質は <see cref="WicNearestNeighbor"/> と同じです。</description>
/// </item>
/// <item>
///   <term><see cref="EsBilinear"/></term>
///   <description>本ライブラリの双線形。 <b><see cref="WicLinear"/> と同じ算法</b>で、画素は<b>最大 1 だけ違います</b>（丸めの違い）。</description>
/// </item>
/// <item>
///   <term><see cref="EsBox"/></term>
///   <description>本ライブラリの面積平均（box フィルタ）。 縮小先の 1 画素を、対応する元の矩形の平均で作ります。
///   <b>縮小で情報を落とさず、リンギングも出ません</b>（文字・罫線が最も濁らない）。 <see cref="WicFant"/> と同じフィルタで、
///   丸めだけが切捨てです。</description>
/// </item>
/// </list>
///
/// <b>速度</b>: <c>Es</c> で始まる方法は<b>縮小先に必要な画素だけ</b>を読むため、<c>Wic</c> で始まる方法より
/// <b>2〜78 倍速い</b>です（WIC のスケーラーは補間の種類によらず元画像を全走査します）。 4K → 長辺 320 で
/// <see cref="EsBox"/> は 1.2 ms、<see cref="WicFant"/> は 10.1 ms（実測。 [`Result_Measurement.md`](../docs/Result_Measurement.md) §11.3）。
///
/// <b>どれを選ぶか</b>: <b>読み込み経路と画素を揃えたい</b>なら <see cref="WicFant"/>（既定）、
/// <b>画面キャプチャ（文字・罫線）を速く縮小したい</b>なら <see cref="EsBox"/>、
/// <b>写真風の鮮鋭さ</b>を優先するなら <see cref="WicHighQualityCubic"/> です。
///
/// <b>画素の一致</b>: <see cref="EsBilinear"/> は <see cref="WicLinear"/> と<b>同じ算法</b>であり、
/// 画素は<b>最大 1 だけ違います</b>（丸めの違い。 本ライブラリの方が速い）。 <see cref="EsBox"/> は
/// <see cref="WicFant"/> と<b>同じフィルタ</b>ですが、<b>整数倍の縮小でのみ最大誤差 1</b>です
/// （丸めが切捨てと四捨五入）。 整数倍でない縮小では <see cref="WicFant"/> が支持幅を広げるため差が広がります
/// （実測: 64x48 → 24 で最大 4）。 <see cref="EsNearestNeighbor"/> と <see cref="WicNearestNeighbor"/> は
/// <b>元の列の選び方が違う</b>ため画素が一致しません（実測: 64x48 → 24 で最大 17）。
/// </remarks>
public enum EsImageScalingMode
{
    /// <summary>WIC の最近傍（<c>WICBitmapInterpolationModeNearestNeighbor</c>）。</summary>
    WicNearestNeighbor,

    /// <summary>WIC の双線形（<c>WICBitmapInterpolationModeLinear</c>）。</summary>
    WicLinear,

    /// <summary>WIC の立方畳み込み（<c>WICBitmapInterpolationModeCubic</c>）。</summary>
    WicCubic,

    /// <summary>WIC の Fant（<c>WICBitmapInterpolationModeFant</c>）。 既定。</summary>
    WicFant,

    /// <summary>WIC の高品質立方畳み込み（<c>WICBitmapInterpolationModeHighQualityCubic</c>）。</summary>
    WicHighQualityCubic,

    /// <summary>本ライブラリの最近傍（最も速い）。</summary>
    EsNearestNeighbor,

    /// <summary>本ライブラリの双線形（WIC の双線形と画素が一致）。</summary>
    EsBilinear,

    /// <summary>本ライブラリの面積平均（box フィルタ。 細線・罫線が落ちない）。</summary>
    EsBox,
}
