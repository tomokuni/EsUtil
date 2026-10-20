using Aprillz.MewUI.Rendering;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Ui.MewUI.Loupe.Internal;

namespace EsUtil.Ui.MewUI.Loupe;

/// <summary>描画計画（<see cref="LoupeDrawPlan"/>）を MewUI の描画 API へ写す描画実装。</summary>
/// <remarks>
/// <b>位置付け</b>: 描く層とその順序は描画層（<see cref="LoupePresenter"/>）が <see cref="LoupeDrawPlan.Layers"/> として単一所有する。<br/>
/// 本クラスは順序を判断せず、層ごとに描画 API を呼ぶだけである（座標・色・寸法はすべて計画に解決済み）。<br/>
/// <b>キャンバスの抽象化を持たない</b>: 描画 API は本クラスが直接呼ぶ（フレームワーク標準の <see cref="IGraphicsContext"/>）。<br/>
/// <b>画素の検証</b>: 画素の比較はテストが行う（バックエンドのオフスクリーン表面を読み戻す）。
/// </remarks>
internal static class LoupePainter
{
    /// <summary>画像の変換結果を保持する結び付け（層をまたいで使い回す）。</summary>
    [ThreadStatic]
    private static MewImageBinding? s_images;

    /// <summary>描画計画を描画先へ写す。</summary>
    /// <param name="context">描画先のコンテキスト。</param>
    /// <param name="source">拡大元の 100% 画像。 null の場合は画像の層を描かない（ペーストボードのみ）。</param>
    /// <param name="plan">描画計画（層の順序・矩形・色を持つ）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> が null の場合。</exception>
    /// <remarks>
    /// <b>層ごとに外観の表示可否（<c>Show*</c>）を見て描く</b>: 計画は表示可否で値を消さないため、<br/>
    /// 描くかどうかの判断は本クラス（描画側）が行う（色のアルファを「描かない」の代用にしない）。<br/>
    /// ペーストボード（描画面の消去）と拡大画像は表示可否を持たず、常に描く。
    /// </remarks>
    public static void Paint(IGraphicsContext context, EsReadOnlyBitmap? source, in LoupeDrawPlan plan)
    {
        ArgumentNullException.ThrowIfNull(context);

        var appearance = plan.Appearance;
        var images = s_images ??= new MewImageBinding();

        // 描画順は描画層（Layers）が単一所有する。 本クラスは順に写すだけで、順序を判断しない
        foreach (var layer in plan.Layers)
        {
            switch (layer)
            {
                case LoupeDrawLayer.Pasteboard:
                    context.Clear(appearance.ResolvePasteboardColor().ToMewUIColor());
                    break;

                case LoupeDrawLayer.Image:
                    DrawMagnifiedImage(context, images, source, in plan);
                    break;

                case LoupeDrawLayer.Crosshair when appearance.ShowCrosshair:
                    MewFill.Rects(context, plan.CrosshairPixelRects, appearance.CrossColor);
                    break;

                case LoupeDrawLayer.Border when appearance.ShowBorder:
                    MewFill.Rects(context, plan.BorderPixelRects, appearance.BorderColor);
                    break;

                case LoupeDrawLayer.Label:
                    // 中心座標ラベルは UI 要素として配置するため、本層では何もしない
                    break;
            }
        }
    }

    /// <summary>ソース画像の切り出し領域を拡大描画する（ソースまたは切り出し領域が無い場合は何もしない）。</summary>
    /// <param name="context">描画先のコンテキスト。</param>
    /// <param name="images">画像の変換結果を保持する結び付け。</param>
    /// <param name="source">拡大元の 100% 画像。</param>
    /// <param name="plan">描画計画（切り出し領域と描画の矩形を持つ）。</param>
    /// <remarks>
    /// <b>切り出しの領域は「描画の元矩形」で渡します</b>（複製した画像を作りません）: 切り出しが画像の外へ出る場合も、
    /// <b>画像と交差する部分だけ</b>を対応する描画先の部分矩形へ写し、外側は描きません（ペーストボードが出る）。<br/>
    /// 描画中にソースが破棄された場合（<see cref="ObjectDisposedException"/>）は何も描かずに戻る。
    /// </remarks>
    private static void DrawMagnifiedImage(
        IGraphicsContext context, MewImageBinding images, EsReadOnlyBitmap? source, in LoupeDrawPlan plan)
    {
        if (source is null || plan.MagnifiedImage is not { } image)
        {
            return;
        }

        var rect = image.CropSourceRect;

        try
        {
            // 画像と交差する部分だけを描く（外側は描かない ＝ ペーストボードが出る）
            var (cropRect, drawRect) = ClipToImage(source, rect, image.DrawClientPixelRect);
            if (cropRect.Width <= 0 || cropRect.Height <= 0)
            {
                return;
            }

            images.Draw(context, source, cropRect, drawRect);
        }
        catch (ObjectDisposedException)
        {
            // 描画中にソースが破棄された: 画像の層を描かずに戻る（ペーストボードのみが残る）
        }
    }

    /// <summary>切り出し矩形を画像の内側へ切り詰め、対応する描画先の矩形を求める。</summary>
    /// <param name="source">拡大元の画像（寸法の取得に使う。 読み取りのみ）。</param>
    /// <param name="cropRect">切り出す矩形（対象座標。 画像の外へ出てよい）。</param>
    /// <param name="drawRect">描画の全体矩形（クライアント座標）。</param>
    /// <returns>画像の内側の切り出し矩形と、それに対応する描画先の矩形（交差しない場合は幅 0）。</returns>
    /// <remarks>
    /// <b>外側を切り落とすだけです</b>: 切り出し画像の全体から <paramref name="drawRect"/> への写像は一様な拡大であり、
    /// <b>画像の内側の部分は同じ倍率で同じ位置へ写ります</b>。<br/>
    /// 描画先の矩形は<b>同じ倍率で切り詰めます</b>（比例で求める）。
    /// </remarks>
    private static (EsRectInt Crop, EsRect Draw) ClipToImage(
        EsReadOnlyBitmap source, EsRectInt cropRect, EsRect drawRect)
    {
        // 画像の内側と交差する部分（交差しない場合は幅 0 を返し、呼び出し側が描かない）
        var left = Math.Max(0, cropRect.X);
        var top = Math.Max(0, cropRect.Y);
        var right = Math.Min(source.Width, cropRect.Right);
        var bottom = Math.Min(source.Height, cropRect.Bottom);
        if (right <= left || bottom <= top)
        {
            return (new EsRectInt(0, 0, 0, 0), default);
        }

        // 切り出し画像（左上が原点）から描画先への倍率
        var scaleX = drawRect.Width / cropRect.Width;
        var scaleY = drawRect.Height / cropRect.Height;
        var crop = new EsRectInt(left, top, right - left, bottom - top);
        var draw = new EsRect(
            drawRect.X + ((left - cropRect.X) * scaleX),
            drawRect.Y + ((top - cropRect.Y) * scaleY),
            crop.Width * scaleX,
            crop.Height * scaleY);
        return (crop, draw);
    }
}
