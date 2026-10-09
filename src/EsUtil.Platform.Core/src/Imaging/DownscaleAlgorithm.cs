namespace EsUtil.Platform.Core.Imaging;

/// <summary>コアの画像を縮小するときのアルゴリズム（<see cref="EsBitmapExtensions.Resize"/> の引数）。</summary>
/// <remarks>
/// <b>どれを選ぶか</b>（実測は Release・4K 3840x2160 の画像。 環境によりぶれるため桁で見ること）:
///
/// <list type="table">
/// <item>
///   <term><see cref="Nearest"/></term>
///   <description>最速（4K → 96x54 で 23 µs・4K → 320x180 で 141 µs）。 縮小先の 1 画素につき元の 1 画素（比例位置）だけを
///   読む。<b>縮小率が大きいと内容が落ちる</b>（実測: 40 倍の縮小で幅 1 px の細線は輝度 0。 位置が合えばそのまま残るため
///   結果が位置に依存する）。 <b>向く用途</b>: 単色・写真のサムネイル、縮小率が 2 倍程度の場面。</description>
/// </item>
/// <item>
///   <term><see cref="Bilinear"/></term>
///   <description>速さと画質の釣り合い（4K → 96x54 で 110 µs・4K → 320x180 で 1.3 ms）。 元の 2x2 画素を重み付き平均する。
///   <b>既定</b>。 40 倍の縮小では細線が落ちる（2x2 しか見ないため）。 <b>向く用途</b>: 表示用のサムネイル（縮小率 2〜10 倍）。</description>
/// </item>
/// <item>
///   <term><see cref="Box"/></term>
///   <description>最も画質が高い（面積平均 = box フィルター）。 縮小先の 1 画素を、対応する元画像の矩形（全画素）の平均で作るため
///   <b>細線（文字・罫線）が落ちない</b>（実測: 40 倍の縮小で輝度 6。 最近傍 0 / 双線形 0 / GDI+ の高画質 4）。
///   速度は元画像の画素数で決まり（4K で 1 枚 1.6〜1.8 ms。 <b>内部で並列化する</b>）、最近傍より 2 桁遅い。
///   <b>向く用途</b>: 文字・罫線を判読したいサムネイル、保存用の縮小、ファイルのサムネイル（コーデックの縮小デコードで得られる画質と同じ）。</description>
/// </item>
/// </list>
///
/// <b>同じ機能の別名を作らない</b>ためにアルゴリズムは本列挙体で選び、入口は <see cref="EsBitmapExtensions.Resize"/> の 1 つに統一する。
/// </remarks>
public enum DownscaleAlgorithm
{
    /// <summary>最近傍補間（最速。 縮小率が大きいと細線が落ちる）。</summary>
    Nearest,

    /// <summary>双線形補間（既定。 元の 2x2 を平均する）。</summary>
    Bilinear,

    /// <summary>面積平均（box フィルター。 最も画質が高く、元画像の画素数に比例して遅い）。</summary>
    Box,
}
