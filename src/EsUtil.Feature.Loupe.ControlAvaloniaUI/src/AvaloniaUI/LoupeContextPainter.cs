using Avalonia.Media;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Interop;
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
    /// <exception cref="ArgumentNullException"><paramref name="context"/> が null の場合。</exception>
    public static void Paint(DrawingContext context, EsBitmap? source, in LoupeDrawPlan plan)
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
                    DrawMagnifiedImage(context, source, in plan);
                    break;

                case LoupeDrawLayer.Crosshair:
                    FillRects(context, plan.CrosshairPixelRects, appearance.CrossColor);
                    break;

                case LoupeDrawLayer.Border:
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

    /// <summary>ソース画像を切り出して拡大描画する（ソースまたは切り出し領域が無い場合は何もしない）。</summary>
    /// <param name="context">描画先の描画コンテキスト。</param>
    /// <param name="source">拡大元の 100% 画像。</param>
    /// <param name="plan">描画計画（切り出し領域と描画の矩形を持つ）。</param>
    /// <remarks>
    /// 切り出しの実行（<c>EsBitmap.Crop</c>）と描画の矩形は計画が単一所有する<br/>
    /// （切り出し領域の算出（切り上げ・中心 → 左上の変換）はコアが済ませている）。<br/>
    /// 描画中にソースが破棄された場合（<see cref="ObjectDisposedException"/>）は何も描かずに戻る<br/>
    /// （ホストの状態と競合しても描画を継続できる。 画像外のみが残る）。
    /// </remarks>
    private static void DrawMagnifiedImage(DrawingContext context, EsBitmap? source, in LoupeDrawPlan plan)
    {
        if (source is null || plan.MagnifiedImage is not { } image)
        {
            return;
        }

        try
        {
            using var region = source.Crop(image.CropSourceRect);
            using var avalonia = region.ToAvaloniaBitmap();
            context.DrawImage(avalonia, image.CroppedRect.ToAvaloniaRect(), image.DrawClientPixelRect.ToAvaloniaRect());
        }
        catch (ObjectDisposedException)
        {
            // 描画中にソースが破棄された: 画像の層を描かずに戻る（ペーストボードのみが残る）
        }
    }
}
