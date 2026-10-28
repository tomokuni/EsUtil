namespace EsUtil.Platform.Core.Imaging;

/// <summary>画像を縮小するときの品質（速度と画質の釣り合い）。</summary>
/// <remarks>
/// 縮小の計算は <b>WIC（<c>IWICBitmapScaler</c>）が単一所有</b>し、段階は<b>補間の指定</b>へ写ります
/// （読み込み時の縮小と同じ実装を使うため、経路によって画質が変わりません）:
///
/// <list type="table">
/// <item>
///   <term><see cref="Low"/></term>
///   <description><b>最速</b>。 最近傍補間（<c>NearestNeighbor</c>。 縮小先の 1 画素につき元の 1 画素だけを写す）。
///   細線は<b>位置が合えば残り、合わなければ消えます</b>。</description>
/// </item>
/// <item>
///   <term><see cref="Mid"/></term>
///   <description><b>既定</b>。 双線形補間（<c>Linear</c>。 元の 2x2 画素を重み付き平均する）。
///   細線は薄く広がります（幅 1 px の線は 2 画素へ半分ずつ）。</description>
/// </item>
/// <item>
///   <term><see cref="High"/></term>
///   <description><b>最も画質が高い</b>。 Fant（<c>Fant</c>。 画素の面積平均に近い補間）。
///   細線・罫線が落ちにくく、<b>文字の多い画面キャプチャ</b>に向きます。
///   <b>読み込み時（<c>EsReadOnlyBitmap.Load(string, int)</c>）の縮小と同じ補間</b>です。</description>
/// </item>
/// </list>
///
/// <b>速さ</b>: <see cref="High"/> が最も遅く、<see cref="Low"/> と <see cref="Mid"/> はほぼ同じです
/// （WIC のスケーラーはどの補間でも元画像を走査するため、最近傍が特別に速いわけではありません）。
/// 実測値は [`Result_Measurement.md`](../docs/Result_Measurement.md) が単一所有します。
/// 所要は<b>元画像の画素数</b>に比例します。
///
/// <b>立方畳み込み（bicubic）を使わない理由</b>: 縮小ではリンギング（細線の縁にできる白黒の縞）が出やすく、
/// 画面キャプチャの文字が読みにくくなります。 立方畳み込みが正しい選択になるのは<b>拡大</b>であり、
/// 本ライブラリは拡大を行いません（拡大を要求すると <see cref="System.NotSupportedException"/> になる）。
///
/// <b>減色（<c>EsImageCodec.ReduceColors</c>）に品質の指定はありません</b>: 減色は WIC のパレット生成と
/// 8bpp への変換で行い、段階を持ちません（<see cref="EsDitherMode"/> で見た目を選びます）。
/// </remarks>
public enum EsImageQuality
{
    /// <summary>最速（画質は最も低い）。</summary>
    Low,

    /// <summary>速度と画質の釣り合い（既定）。</summary>
    Mid,

    /// <summary>最も画質が高い（最も遅い）。</summary>
    High,
}