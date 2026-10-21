using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Ui.MewUI.Interop.Imaging;

/// <summary>1 回の描画で画像をどこへ置くかを決める純粋な計画。</summary>
/// <remarks>
/// <b>適用パターン</b>: 純粋関数（描画の可否と配置を 1 つの値へ確定する）。<br/>
/// <b>なぜ計画と描画を分けるか</b>: MewUI の描画 API（<c>IGraphicsContext</c>）は実際のウィンドウを必要とするため、
/// 画素で検証できない。 判断（描くか / どこへ置くか）を本型へ閉じ込めれば、ウィンドウ無しで検証できる。<br/>
/// <b>前提条件の単一所有</b>: 「画像が無い」「破棄済み」「描画先が空」の 3 つの判定はここだけが持つ。
/// </remarks>
/// <param name="ShouldDraw">描画する場合 true。</param>
/// <param name="Source">元画像の全域（ピクセル座標）。</param>
/// <param name="Destination">描画先の矩形（表示座標）。</param>
internal readonly record struct ImageRenderPlan(bool ShouldDraw, EsRect Source, EsRect Destination)
{
    /// <summary>描画しない計画。</summary>
    public static ImageRenderPlan None => default;

    /// <summary>表示する画像と描画先の寸法から計画を組み立てる。</summary>
    /// <param name="source">表示する画像。 null・破棄済みの場合は描画しない。</param>
    /// <param name="targetWidth">描画先の幅（DIP）。</param>
    /// <param name="targetHeight">描画先の高さ（DIP）。</param>
    /// <param name="mode">配置方法（縦横比を保つ / 領域全体へ引き伸ばす）。</param>
    /// <returns>組み立てた計画。</returns>
    /// <remarks>配置の計算は <see cref="EsImageDrawMath"/> が単一所有する（本型は可否の判定だけを足す）。</remarks>
    public static ImageRenderPlan Create(
        EsReadOnlyBitmap? source, double targetWidth, double targetHeight, EsImageDrawMode mode)
    {
        if (source is not { } bitmap || bitmap.IsDisposed)
        {
            return None;
        }

        var destination = EsImageDrawMath.ComputeDestination(
            bitmap.Width, bitmap.Height, (int)targetWidth, (int)targetHeight, mode);

        return destination.IsEmpty
            ? None
            : new ImageRenderPlan(true, new EsRect(0, 0, bitmap.Width, bitmap.Height), destination);
    }
}
