using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Feature.Loupe.MewUI.Internal;
using SkiaSharp;

namespace EsUtil.Feature.Loupe.MewUI;

/// <summary>描画計画（<see cref="LoupeDrawPlan"/>）を Skia の描画 API へ写す描画実装。</summary>
/// <remarks>
/// <b>位置付け</b>: 描く層とその順序は描画層（<see cref="LoupePresenter"/>）が <see cref="LoupeDrawPlan.Layers"/> として単一所有する。<br/>
/// 本クラスは順序を判断せず、層ごとに Skia の描画 API を呼ぶだけである（座標・色・寸法はすべて計画に解決済み）。<br/>
/// <br/>
/// <b>キャンバスの抽象化を持たない</b>: 描画 API は本クラスが直接呼ぶ（UI フレームワークをまたぐ描画の抽象は、<br/>
/// フレームワークごとの描画モデルの差を吸収しきれないため導入しない）。 共有できるプリミティブは<br/>
/// <see cref="SkiaFill"/> / <see cref="SkiaText"/> が本プロジェクトの Internal で所有する。<br/>
/// <br/>
/// <b>テキストは描かない</b>: 中心座標ラベルは UI 要素（<c>Border</c> + <c>TextBlock</c>）として配置するため、<br/>
/// <see cref="LoupeDrawLayer.Label"/> の層では何もしない（要素は親より後に描かれるため常に最上位になる）。<br/>
/// <br/>
/// <b>UI 非依存のテスト</b>: 画素の検証は本クラスで行う（<c>SKCanvas</c> はウィンドウなしで描画できる）。
/// </remarks>
internal static class LoupeSkiaPainter
{
    /// <summary>描画計画を描画順に描く。</summary>
    /// <param name="canvas">描画先のキャンバス（物理ピクセル）。 null 不可。</param>
    /// <param name="source">拡大元の 100% 画像。 null の場合は拡大画像を描かない（画像外の色のみ）。</param>
    /// <param name="plan">描画層が解決した描画計画（層の順序・矩形・色を持つ）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="canvas"/> が null の場合。</exception>
    /// <remarks>
    /// <b>層ごとに外観の表示可否（<c>Show*</c>）を見て描く</b>: 計画は表示可否で値を消さないため、<br/>
    /// 描くかどうかの判断は本クラス（描画側）が行う（色のアルファを「描かない」の代用にしない）。<br/>
    /// ペーストボード（描画面の消去）と拡大画像は表示可否を持たず、常に描く。
    /// </remarks>
    public static void Paint(SKCanvas canvas, EsReadOnlyBitmap? source, in LoupeDrawPlan plan)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        var appearance = plan.Appearance;

        // 描画順は描画層（Layers）が単一所有する。 本クラスは順に写すだけで、順序を判断しない
        foreach (var layer in plan.Layers)
        {
            switch (layer)
            {
                case LoupeDrawLayer.Pasteboard:
                    canvas.Clear(appearance.ResolvePasteboardColor().ToSKColor());
                    break;

                case LoupeDrawLayer.Image:
                    DrawMagnifiedImage(canvas, source, in plan);
                    break;

                case LoupeDrawLayer.Crosshair when appearance.ShowCrosshair:
                    SkiaFill.Rects(canvas, plan.CrosshairPixelRects, appearance.CrossColor);
                    break;

                case LoupeDrawLayer.Border when appearance.ShowBorder:
                    SkiaFill.Rects(canvas, plan.BorderPixelRects, appearance.BorderColor);
                    break;

                case LoupeDrawLayer.Label:
                    // 中心座標ラベルは UI 要素として配置するため、本層では何もしない
                    break;
            }
        }
    }

    /// <summary>ソース画像の切り出し領域を拡大描画する（ソースまたは切り出し領域が無い場合は何もしない）。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="source">拡大元の 100% 画像。</param>
    /// <param name="plan">描画計画（切り出し領域と描画の矩形を持つ）。</param>
    /// <remarks>
    /// <b>切り出しの領域は「描画の元矩形」で渡します</b>（複製した画像を作りません）: 切り出しが画像の外へ出る場合も、
    /// <b>画像と交差する部分だけ</b>を対応する描画先の部分矩形へ写し、外側は描きません（ペーストボードが出る）。
    /// これは透明で埋めた画像を貼るのと同じ見た目であり、<b>毎フレームの切り出しの複製（実測: 1000x800 で 0.85 ms / 4 MB）</b>が
    /// 不要になります（実測: 交差部分のビューの生成は 0.089 µs / 112 B。 60 fps で 51 ms / 秒 と 4 MB × 60 の確保が消える）。<br/>
    /// 描画中にソースが破棄された場合（<see cref="ObjectDisposedException"/>）は何も描かずに戻る<br/>
    /// （ホストの状態と競合しても描画を継続できる。 画像外のみが残る）。
    /// </remarks>
    private static void DrawMagnifiedImage(SKCanvas canvas, EsReadOnlyBitmap? source, in LoupeDrawPlan plan)
    {
        if (source is null || plan.MagnifiedImage is not { } image)
        {
            return;
        }

        var rect = image.CropSourceRect;

        try
        {
            // 画像と交差する部分だけを描く（外側は描かない ＝ ペーストボードが出る。 透明で埋めた画像を貼るのと同じ見た目）
            var (cropRect, drawRect) = ClipToImage(source, rect, image.DrawClientPixelRect);
            if (cropRect.Width <= 0 || cropRect.Height <= 0)
            {
                return;
            }

            // 全体の非所有ビュー（ピクセルは複製しない）を作り、切り出しは描画 API の元矩形で指定する
            using var bitmap = source.ToSKBitmap();
            canvas.DrawBitmap(bitmap, new SKRect(cropRect.X, cropRect.Y, cropRect.Right, cropRect.Bottom), drawRect.ToSKRect());
        }
        catch (ObjectDisposedException)
        {
            // 描画中にソースが破棄された: 画像の層を描かずに戻る（ペーストボードのみが残る）
        }
    }

    /// <summary>切り出し矩形を画像の内側へ切り詰め、対応する描画先の矩形を求める。</summary>
    /// <param name="source">拡大元の画像（寸法の取得に使う。 読み取りのみ）。</param>
    /// <param name="cropRect">切り出す矩形（対象座標。 画像の外へ出てよい。 切り出し画像の自座標はこの左上を原点とする）。</param>
    /// <param name="drawRect">描画の全体矩形（クライアント座標。 切り出し画像の全体を描く場合の矩形）。</param>
    /// <returns>画像の内側の切り出し矩形と、それに対応する描画先の矩形（交差しない場合は幅 0）。</returns>
    /// <remarks>
    /// <b>外側を切り落とすだけです</b>: 切り出し画像の全体（<paramref name="cropRect"/> の寸法）から
    /// <paramref name="drawRect"/> への写像は一様な拡大であり、<b>画像の内側の部分は同じ倍率で同じ位置へ写ります</b>
    /// （切り出し画像を貼る代わりに元画像を「元矩形つき」で描いても、見た目が一致します）。<br/>
    /// 描画先の矩形は<b>同じ倍率で切り詰めます</b>（切り出し矩形の外側の幅・高さの比を保つため、比例で求めます）。
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
