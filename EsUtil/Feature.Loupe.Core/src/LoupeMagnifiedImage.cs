using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Loupe;

/// <summary>拡大画像の切り出しと描画の矩形（描画に必要な値のみをコアが決定した結果）。</summary>
/// <param name="CropSourceRect">対象（元画像）から切り出す領域（対象座標・正方形。 画像の外へ出てよい。 外側は描かない）。</param>
/// <param name="DrawClientPixelRect">クライアント領域（描画先）へ描く矩形（切り出しサイズ × 拡大率。 物理ピクセル）。</param>
/// <remarks>
/// 中心 → 切り出し領域の左上の変換（切り上げを含む）と「切り出した領域を拡大率倍で描く」規則を<br/>
/// コアが単一所有する（描画実装は計算を持たない）。<br/>
/// 切り出し元の画像（<see cref="EsUtil.Platform.Core.Imaging.EsReadOnlyBitmap"/>）は本値に含めない<br/>
/// （描画の起動時に引数で受け取る）。<br/>
/// <b>切り出すのは領域だけである</b>: 描画実装は <see cref="CropSourceRect"/> を読み取りの範囲として使うだけで、<br/>
/// その大きさの画像を作らない（内側に収まる場合は 0 コピーのビューで読み、外へ出る場合は交差する部分だけを描く）。<br/>
/// <b>描画先は切り上げ分だけクライアント領域より大きくなりうる</b>（最大 <c>拡大率 - 1</c> 物理ピクセル）。<br/>
/// はみ出した部分は描画先のクリップに任せる（切り捨てると拡大の倍率が整数でなくなり画素がぼやける）。
/// </remarks>
public readonly record struct LoupeMagnifiedImage(EsRectInt CropSourceRect, EsRect DrawClientPixelRect);
