using Avalonia;
using Avalonia.Media;
using EsUtil.Feature.Loupe.AvaloniaUI.Internal;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Loupe.AvaloniaUI;

/// <summary>描画計画（<see cref="LoupeDrawPlan"/>）を Avalonia の描画 API へ写す描画実装。</summary>
/// <remarks>
/// <b>位置付け</b>: 描く層とその順序は描画層（<see cref="LoupePresenter"/>）が <see cref="LoupeDrawPlan.Layers"/> として単一所有する。<br/>
/// 本クラスは順序を判断せず、層ごとに Avalonia の描画 API を呼ぶだけである（座標・色・寸法はすべて計画に解決済み）。<br/>
/// <br/>
/// <b>キャンバスの抽象化を持たない</b>: 描画 API は本クラスが直接呼ぶ（UI フレームワークをまたぐ描画の抽象は、<br/>
/// フレームワークごとの描画モデルの差を吸収しきれないため導入しない）。 色 → ブラシの共有は<br/>
/// <see cref="SolidColorBrushCache"/> が単一所有する。<br/>
/// <br/>
/// <b>テキストは描かない</b>: 中心座標ラベルは UI 要素（<c>Border</c> + <c>TextBlock</c>）として配置するため、<br/>
/// <see cref="LoupeDrawLayer.Label"/> の層では何もしない（要素は親より後に描かれるため常に最上位になる）。<br/>
/// <br/>
/// <b>UI 非依存のテスト</b>: 画素の検証は同じ計画を MewUI の <c>LoupeSkiaPainter</c> で行う<br/>
/// （Avalonia ヘッドレスでは <c>RenderTargetBitmap</c> の画素を読めないため）。
/// </remarks>
internal static class LoupeContextPainter
{
    /// <summary>描画計画を描画順に描く。</summary>
    /// <param name="context">描画先の描画コンテキスト（<c>Render</c> の実行中のみ有効）。 null 不可。</param>
    /// <param name="source">拡大元の 100% 画像。 null の場合は拡大画像を描かない（画像外の色のみ）。</param>
    /// <param name="plan">描画層が解決した描画計画（層の順序・矩形・色を持つ）。</param>
    /// <param name="display">拡大元の全体を写した Avalonia の画像（省略可）。 渡すと<b>切り出しの複製を作らず</b>、元矩形つきの描画で済む。</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> が null の場合。</exception>
    /// <remarks>
    /// <b>層ごとに外観の表示可否（<c>Show*</c>）を見て描く</b>: 計画は表示可否で値を消さないため、<br/>
    /// 描くかどうかの判断は本クラス（描画側）が行う（色のアルファを「描かない」の代用にしない）。<br/>
    /// ペーストボード（描画面の消去）と拡大画像は表示可否を持たず、常に描く。<br/>
    /// <b><paramref name="display"/> を渡せるのは、その寸法が <paramref name="source"/> と一致する場合だけです</b>
    /// （縮小された射影を渡すと元矩形の座標系が変わるため、一致しない場合は 0 コピーのビューを表示用へ変換して描きます）。
    /// </remarks>
    public static void Paint(
        DrawingContext context, EsReadOnlyBitmap? source, in LoupeDrawPlan plan, IImage? display = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var appearance = plan.Appearance;

        // 描画順は描画層（Layers）が単一所有する。 本クラスは順に写すだけで、順序を判断しない
        foreach (var layer in plan.Layers)
        {
            switch (layer)
            {
                case LoupeDrawLayer.Pasteboard:
                    context.DrawRectangle(
                        SolidColorBrushCache.GetOrCreate(appearance.ResolvePasteboardColor()), null, plan.ClientPixelRect.ToAvaloniaRect());
                    break;

                case LoupeDrawLayer.Image:
                    DrawMagnifiedImage(context, source, display, in plan);
                    break;

                case LoupeDrawLayer.Crosshair when appearance.ShowCrosshair:
                    FillRects(context, plan.CrosshairPixelRects, appearance.CrossColor);
                    break;

                case LoupeDrawLayer.Border when appearance.ShowBorder:
                    FillRects(context, plan.BorderPixelRects, appearance.BorderColor);
                    break;

                case LoupeDrawLayer.Label:
                    // 中心座標ラベルは UI 要素として配置するため、本層では何もしない
                    break;
            }
        }
    }

    /// <summary>同色の矩形の一覧を塗る。</summary>
    /// <param name="context">描画先の描画コンテキスト。</param>
    /// <param name="rects">塗る矩形の一覧（空の場合は何もしない）。</param>
    /// <param name="color">塗り色。</param>
    private static void FillRects(DrawingContext context, IReadOnlyList<EsRect> rects, DrawingColor color)
    {
        if (rects.Count == 0)
        {
            return;
        }

        var brush = SolidColorBrushCache.GetOrCreate(color);

        foreach (var rect in rects)
        {
            context.DrawRectangle(brush, null, rect.ToAvaloniaRect());
        }
    }

    /// <summary>ソース画像の切り出し領域を拡大描画する（ソースまたは切り出し領域が無い場合は何もしない）。</summary>
    /// <param name="context">描画先の描画コンテキスト。</param>
    /// <param name="source">拡大元の 100% 画像。</param>
    /// <param name="display">拡大元の全体を写した Avalonia の画像（寸法が一致する場合だけ使う）。</param>
    /// <param name="plan">描画計画（切り出し領域と描画の矩形を持つ）。</param>
    /// <remarks>
    /// <b>表示用の射影をそのまま使える場合は切り出しの複製を作りません</b>: <paramref name="display"/> があり寸法が
    /// <paramref name="source"/> と一致する場合は、<b>元矩形つきの描画</b>（<c>DrawImage(image, 元矩形, 描画矩形)</c>）で
    /// 済ませます。 切り出しが画像の外へ出る場合も<b>交差する部分だけ</b>を対応する描画先の部分矩形へ写し、外側は描きません
    /// （ペーストボードが出る ＝ 透明で埋めた画像を貼るのと同じ見た目）。<br/>
    /// <b>射影を使えない場合も切り出しの複製を作りません</b>: (1) 表示用の射影が無い（オフスクリーン描画の経路）、
    /// (2) 射影の寸法が拡大元と違う（表示用の縮小が指定されている）のいずれでも、切り出しは
    /// <b>0 コピーのビュー</b>（<see cref="EsReadOnlyBitmap.ToPixmap"/>）で読み、表示用へ変換して描きます
    /// （切り出しの複製を作ると、表示用の変換がピクセルを複製する前に余分な複製を払うことになります）。<br/>
    /// 描画中にソースが破棄された場合（<see cref="ObjectDisposedException"/>）は何も描かずに戻る<br/>
    /// （ホストの状態と競合しても描画を継続できる。 画像外のみが残る）。
    /// </remarks>
    private static void DrawMagnifiedImage(
        DrawingContext context, EsReadOnlyBitmap? source, IImage? display, in LoupeDrawPlan plan)
    {
        if (source is null || plan.MagnifiedImage is not { } image)
        {
            return;
        }

        var rect = image.CropSourceRect;
        var (crop, draw) = ClipToImage(source, rect, image.DrawClientPixelRect);

        try
        {
            if (crop.Width <= 0 || crop.Height <= 0)
            {
                return;
            }

            // 表示用の射影をそのまま使う（切り出しの複製も表示用の変換も作らない）
            if (display is not null
                && display.Size.Width == source.Width && display.Size.Height == source.Height)
            {
                context.DrawImage(
                    display,
                    new Rect(crop.X, crop.Y, crop.Width, crop.Height),
                    draw.ToAvaloniaRect());
                return;
            }

            // 射影が無い（または寸法が違う）場合は切り出しのビューを表示用へ変換して描く
            // （ビューの座標は切り出しの左上が原点であり、描画先は切り詰めた矩形へ写す）
            using var view = source.ToPixmap(crop);
            using var avalonia = view.ToAvaloniaBitmap();
            context.DrawImage(
                avalonia,
                new Rect(0, 0, crop.Width, crop.Height),
                draw.ToAvaloniaRect());
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
