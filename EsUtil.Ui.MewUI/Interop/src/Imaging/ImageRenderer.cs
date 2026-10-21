using Aprillz.MewUI;
using Aprillz.MewUI.Rendering;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Ui.MewUI.Interop.Imaging;

/// <summary><see cref="EsReadOnlyBitmap"/> を MewUI の描画コンテキストへ敷く描画層。</summary>
/// <remarks>
/// <b>適用パターン</b>: 描画層（コントロールから描画手順を分離する）。<br/>
/// <b>手順</b>: 1. 計画（<see cref="ImageRenderPlan"/>）を組み立てる。 2. 描く対象が無ければ何もしない。<br/>
/// 3. 画像を MewUI の画像へ結び付け（<see cref="ImageBinding"/>）、配置の矩形へ 1 回で描く。<br/>
/// <b>クリアしない理由</b>: Skia 版はキャンバスを透明でクリアしていたが、MewUI のコンテキストは
/// <b>親の描画結果の上へ重ねて描く</b>ため、クリアすると親の背景まで消える。 背景は各コントロールの
/// <c>Background</c> が担う。<br/>
/// <b>画素補間</b>: <see cref="ImageScaleQuality.Fast"/>（最近傍相当）を既定にする。 撮影画像の画素を
/// そのまま見せる用途（切り出しの確認）で、拡大時に色を混ぜないためである。
/// </remarks>
public sealed class ImageRenderer : IDisposable
{
    /// <summary>変換済みの画像を保持する結び付け。</summary>
    private readonly ImageBinding _binding = new();

    /// <summary>画像を描画先の寸法へ配置して描画する。</summary>
    /// <param name="context">描画先のコンテキスト（原点はコントロールの左上）。</param>
    /// <param name="source">描画する画像。 null・破棄済みの場合は何も描画しない。</param>
    /// <param name="targetWidth">描画先の幅（DIP）。</param>
    /// <param name="targetHeight">描画先の高さ（DIP）。</param>
    /// <param name="mode">配置方法（縦横比を保つ / 領域全体へ引き伸ばす）。</param>
    public void Render(
        IGraphicsContext context, EsReadOnlyBitmap? source, double targetWidth, double targetHeight, ImageDrawMode mode)
    {
        ArgumentNullException.ThrowIfNull(context);

        var plan = ImageRenderPlan.Create(source, targetWidth, targetHeight, mode);
        if (!plan.ShouldDraw)
        {
            return;
        }

        DrawTo(context, source, plan.Destination);
    }

    /// <summary>画像の全面を指定の矩形へ描画する（拡大・縮小の倍率は呼び出し側が決める）。</summary>
    /// <param name="context">描画先のコンテキスト。</param>
    /// <param name="source">描画する画像。 null・破棄済みの場合は何も描画しない。</param>
    /// <param name="destination">描画先の矩形（DIP）。 空の場合は描画しない。</param>
    /// <remarks>
    /// ズーム表示のように「表示する矩形が既に確定している」場合の入口である。
    /// 画像が無い・破棄済み・矩形が空の場合の判定は本メソッドが持つ。
    /// </remarks>
    public void DrawTo(IGraphicsContext context, EsReadOnlyBitmap? source, EsRect destination)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (source is not { IsDisposed: false } bitmap || destination.IsEmpty)
        {
            return;
        }

        if (_binding.Resolve(bitmap) is not { } image)
        {
            return;
        }

        context.ImageScaleQuality = ImageScaleQuality.Fast;
        context.DrawImage(image, new EsRect(0, 0, bitmap.Width, bitmap.Height).ToMewUIRect(), destination.ToMewUIRect());
    }

    /// <summary>保持している画像を解放する。</summary>
    public void Dispose() => _binding.Dispose();
}
