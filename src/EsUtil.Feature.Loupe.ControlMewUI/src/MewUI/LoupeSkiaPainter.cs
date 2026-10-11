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

    /// <summary>ソース画像を切り出して拡大描画する（ソースまたは切り出し領域が無い場合は何もしない）。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="source">拡大元の 100% 画像。</param>
    /// <param name="plan">描画計画（切り出し領域と描画の矩形を持つ）。</param>
    /// <remarks>
    /// 切り出しの実行（<c>EsReadOnlyBitmap.Crop</c>）と描画の矩形は計画が単一所有する<br/>
    /// （切り出し領域の算出（切り上げ・中心 → 左上の変換）はコアが済ませている）。<br/>
    /// 描画中にソースが破棄された場合（<see cref="ObjectDisposedException"/>）は何も描かずに戻る<br/>
    /// （ホストの状態と競合しても描画を継続できる。 画像外のみが残る）。
    /// </remarks>
    private static void DrawMagnifiedImage(SKCanvas canvas, EsReadOnlyBitmap? source, in LoupeDrawPlan plan)
    {
        if (source is null || plan.MagnifiedImage is not { } image)
        {
            return;
        }

        try
        {
            using var region = source.Crop(image.CropSourceRect);
            // ビューは非所有（ピクセルは複製しない）だがファイナライザを持つため、描画の直後に破棄する
            using var view = region.ToSKBitmap();
            canvas.DrawBitmap(view, image.CroppedRect.ToSKRect(), image.DrawClientPixelRect.ToSKRect());
        }
        catch (ObjectDisposedException)
        {
            // 描画中にソースが破棄された: 画像の層を描かずに戻る（ペーストボードのみが残る）
        }
    }
}
