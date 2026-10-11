using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Loupe;

/// <summary>拡大画像の切り出しと描画の矩形（描画に必要な値のみをコアが決定した結果）。</summary>
/// <param name="CropSourceRect">対象（元画像）から切り出す領域（対象座標・正方形。 範囲外は透明パディングになる）。</param>
/// <param name="CroppedRect">切り出した画像の全体矩形（切り出し画像の自座標。 常に左上原点からの切り出しサイズ）。 描画の元（読み取り領域）として使う。</param>
/// <param name="DrawClientPixelRect">クライアント領域（描画先）へ描く矩形（左上原点・切り出しサイズ × 拡大率。 物理ピクセル）。</param>
/// <remarks>
/// 中心 → 切り出し領域の左上の変換（切り上げを含む）と「切り出した領域を左上原点から拡大率倍で描く」規則を<br/>
/// コアが単一所有する（描画実装は計算を持たない）。<br/>
/// 切り出し元の画像（<see cref="EsUtil.Platform.Core.Imaging.EsReadOnlyBitmap"/>）は本値に含めない<br/>
/// （描画の起動時に引数で受け取る。 切り出しの実行は <c>EsReadOnlyBitmap.Crop</c> が担う）。<br/>
/// <b><see cref="CroppedRect"/> と <see cref="DrawClientPixelRect"/> は切り出しサイズから決まる全体矩形である</b>（位置は常に原点）。<br/>
/// 描画の入力（切り出し画像のどこを読むか）と出力（描画先のどこへ描くか）の対を表し、可変なのは切り出し位置だけである。<br/>
/// <b>描画先は切り上げ分だけクライアント領域より大きくなりうる</b>（最大 <c>拡大率 - 1</c> 物理ピクセル）。<br/>
/// はみ出した部分は描画先のクリップに任せる（切り捨てると拡大の倍率が整数でなくなり画素がぼやける）。
/// </remarks>
public readonly record struct LoupeMagnifiedImage(EsRectInt CropSourceRect, EsRect CroppedRect, EsRect DrawClientPixelRect);
