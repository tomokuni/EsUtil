using DrawingBrush = System.Drawing.SolidBrush;
using DrawingColor = System.Drawing.Color;
using DrawingCompositingMode = System.Drawing.Drawing2D.CompositingMode;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingInterpolationMode = System.Drawing.Drawing2D.InterpolationMode;
using DrawingPath = System.Drawing.Drawing2D.GraphicsPath;
using DrawingPixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode;
using DrawingRectangleF = System.Drawing.RectangleF;
using DrawingSmoothingMode = System.Drawing.Drawing2D.SmoothingMode;
using EsUtil.Ui.WinForms.Loupe.Internal;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Ui.WinForms.Loupe;

/// <summary>描画計画（<see cref="LoupeDrawPlan"/>）を GDI+ の描画 API へ写す描画実装。</summary>
/// <remarks>
/// <b>位置付け</b>: 描く層とその順序は描画層（<see cref="LoupePresenter"/>）が <see cref="LoupeDrawPlan.Layers"/> として単一所有する。<br/>
/// 本クラスは順序を判断せず、層ごとに GDI+ の描画 API を呼ぶだけである（座標・色・寸法はすべて計画に解決済み）。<br/>
/// <br/>
/// <b>キャンバスの抽象化を持たない</b>: 描画 API は本クラスが直接呼ぶ（UI フレームワークをまたぐ描画の抽象は、<br/>
/// フレームワークごとの描画モデルの差を吸収しきれないため導入しない）。 共有できるプリミティブは<br/>
/// <see cref="EsGeometryDrawingInterop"/> / <see cref="DrawingLabelText"/> が本プロジェクトの Internal で所有する。<br/>
/// <br/>
/// <b>文字も本クラスが描く</b>: WinForms の子コントロールは半透明の背景を持てないため、<br/>
/// ラベルを要素として置くと背景のアルファ（既定は黒 60%）を再現できない。 したがって<br/>
/// <see cref="LoupeDrawLayer.Label"/> の層では、計画の配置指示（<see cref="LoupeLabelPlacement"/>）から<br/>
/// 背景と文字を GDI+ で描く（配置指示の矩形は DIP であるため、計画が持つ倍率で物理ピクセルへ換算する）。<br/>
/// <br/>
/// <b>透明の扱い</b>: ペーストボード（描画面の塗り）の色が透明の場合は<b>描画面を置き換えない</b>。<br/>
/// WinForms は画面へアルファを伝えられないため、透明は「下地（コントロールの背景色）を見せる」ことを意味する<br/>
/// （透明で塗ると未定義の下地が残る。<see cref="LoupeAppearance.ResolvePasteboardColor"/> が透明を返すのは<br/>
/// ペーストボードを非表示にした場合である）。<br/>
/// <br/>
/// <b>引数の検証</b>: 描画先の <see cref="DrawingGraphics"/> は必須である（null の場合は例外）。<br/>
/// 拡大元の画像は null を許す（画像が無い場合は拡大画像の層を描かない）。<br/>
/// <br/>
/// <b>UI 非依存のテスト</b>: 画素の検証は本クラスで行う（<see cref="DrawingGraphics"/> はビットマップから作れるため、<br/>
/// ウィンドウもメッセージ ループも要らない）。
/// </remarks>
internal static class LoupeGraphicsPainter
{
    /// <summary>描画計画を描画順に描く。</summary>
    /// <param name="graphics">描画先の描画面（物理ピクセル）。 null 不可。</param>
    /// <param name="source">拡大元の 100% 画像。 null の場合は拡大画像を描かない（画像外の色のみ）。</param>
    /// <param name="plan">描画層が解決した描画計画（層の順序・矩形・色を持つ）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="graphics"/> が null の場合。</exception>
    /// <remarks>
    /// <b>層ごとに外観の表示可否（<c>Show*</c>）を見て描く</b>: 計画は表示可否で値を消さないため、<br/>
    /// 描くかどうかの判断は本クラス（描画側）が行う（色のアルファを「描かない」の代用にしない）。<br/>
    /// ペーストボード（描画面の消去）と拡大画像は表示可否を持たず、常に描く（ペーストボードは色が透明の場合だけ下地を残す）。<br/>
    /// <b>描画の前後で描画先の状態を保存・復帰する</b>（拡大画像の補間方法や塗りの合成方法を変えるため、<br/>
    /// 呼び出し側が同じ描画面へ続けて描いても影響が残らない。 描画完了通知のオーバーレイ描画が該当する）。
    /// </remarks>
    public static void Paint(DrawingGraphics graphics, EsReadOnlyBitmap? source, in LoupeDrawPlan plan)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        var appearance = plan.Appearance;
        var state = graphics.Save();
        try
        {
            // 描画順は描画層（Layers）が単一所有する。 本クラスは順に写すだけで、順序を判断しない
            foreach (var layer in plan.Layers)
            {
                switch (layer)
                {
                    case LoupeDrawLayer.Pasteboard:
                        PaintPasteboard(graphics, in plan);
                        break;

                    case LoupeDrawLayer.Image:
                        PaintMagnifiedImage(graphics, source, in plan);
                        break;

                    case LoupeDrawLayer.Crosshair when appearance.ShowCrosshair:
                        PaintRects(graphics, plan.CrosshairPixelRects, appearance.CrossColor);
                        break;

                    case LoupeDrawLayer.Border when appearance.ShowBorder:
                        PaintRects(graphics, plan.BorderPixelRects, appearance.BorderColor);
                        break;

                    case LoupeDrawLayer.Label:
                        PaintLabels(graphics, in plan);
                        break;
                }
            }
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    /// <summary>描画面をペーストボード（画像外の色）で置き換える。</summary>
    /// <param name="graphics">描画先の描画面。</param>
    /// <param name="plan">描画計画（描画面の矩形と外観を持つ）。</param>
    /// <remarks>
    /// <b>色をそのまま置き換える</b>（重ね塗りしない）: 半透明のペーストボードを前のフレームの上へ重ねると、<br/>
    /// 残像が透けて見える（描画面を保持する UI では前のフレームが残るため）。 そのため合成方法を「コピー」にして塗る。<br/>
    /// 透明（アルファ 0）の場合は塗らない（WinForms では下地を見せることが透明の意味になる）。
    /// </remarks>
    private static void PaintPasteboard(DrawingGraphics graphics, in LoupeDrawPlan plan)
    {
        var color = plan.Appearance.ResolvePasteboardColor();
        if (color.A == 0)
        {
            return;
        }

        using var brush = new DrawingBrush(color);
        var previous = graphics.CompositingMode;
        graphics.CompositingMode = DrawingCompositingMode.SourceCopy;
        try
        {
            graphics.FillRectangle(brush, plan.ClientPixelRect.ToRectangleF());
        }
        finally
        {
            graphics.CompositingMode = previous;
        }
    }

    /// <summary>矩形の一覧を同じ色で塗る（十字線・枠線）。</summary>
    /// <param name="graphics">描画先の描画面。</param>
    /// <param name="rects">塗る矩形の一覧（物理ピクセル）。 空の場合は何もしない。</param>
    /// <param name="color">塗り色（アルファを含む）。</param>
    /// <remarks>
    /// 矩形は画素境界に沿って塗る（輪郭を滑らかにしない）ため、アンチエイリアスを無効にする<br/>
    /// （有効にすると矩形の縁が半透明になり、計画どおりの画素にならない）。
    /// </remarks>
    private static void PaintRects(DrawingGraphics graphics, IReadOnlyList<EsRect> rects, DrawingColor color)
    {
        if (rects.Count == 0 || color.A == 0)
        {
            return;
        }

        using var brush = new DrawingBrush(color);
        var previous = graphics.SmoothingMode;
        graphics.SmoothingMode = DrawingSmoothingMode.None;
        try
        {
            for (var index = 0; index < rects.Count; index++)
            {
                graphics.FillRectangle(brush, rects[index].ToRectangleF());
            }
        }
        finally
        {
            graphics.SmoothingMode = previous;
        }
    }

    /// <summary>ソース画像の切り出し領域を拡大描画する（ソースまたは切り出し領域が無い場合は何もしない）。</summary>
    /// <param name="graphics">描画先の描画面。</param>
    /// <param name="source">拡大元の 100% 画像。</param>
    /// <param name="plan">描画計画（切り出し領域と描画の矩形を持つ）。</param>
    /// <remarks>
    /// <b>切り出しの領域は「描画の元矩形」で渡す</b>（切り出した画像を全体として作らない）: 切り出しが画像の外へ出る場合も、<br/>
    /// <b>画像と交差する部分だけ</b>を対応する描画先の部分矩形へ写し、外側は描かない（ペーストボードが出る）。<br/>
    /// 拡大は最近傍で行い、拡大率の整数倍をそのまま画素の区切りとして写す（滑らかにすると拡大した画素がぼやける）。<br/>
    /// 描画中にソースが破棄された場合（<see cref="ObjectDisposedException"/>）は何も描かずに戻る<br/>
    /// （ホストの状態と競合しても描画を継続できる。 画像外のみが残る）。
    /// </remarks>
    private static void PaintMagnifiedImage(
        DrawingGraphics graphics, EsReadOnlyBitmap? source, in LoupeDrawPlan plan)
    {
        if (source is null || plan.MagnifiedImage is not { } image)
        {
            return;
        }

        try
        {
            var (crop, draw) = ClipToImage(source, image.CropSourceRect, image.DrawClientPixelRect);
            if (crop.Width <= 0 || crop.Height <= 0)
            {
                return;
            }

            // 切り出しの領域だけを GDI+ のビットマップへ複製する（画像全体は複製しない）
            using var region = EsBitmapDrawingInterop.CopyRegion(source, crop.X, crop.Y, crop.Width, crop.Height);

            var previousInterpolation = graphics.InterpolationMode;
            var previousOffset = graphics.PixelOffsetMode;
            var previousSmoothing = graphics.SmoothingMode;
            graphics.InterpolationMode = DrawingInterpolationMode.NearestNeighbor;
            // 画素の中心を半ピクセルずらす（ずらさないと整数倍の拡大で端の区画が 1 画素ぶんずれる）
            graphics.PixelOffsetMode = DrawingPixelOffsetMode.Half;
            graphics.SmoothingMode = DrawingSmoothingMode.None;
            try
            {
                graphics.DrawImage(region, draw.ToRectangleF());
            }
            finally
            {
                graphics.InterpolationMode = previousInterpolation;
                graphics.PixelOffsetMode = previousOffset;
                graphics.SmoothingMode = previousSmoothing;
            }
        }
        catch (ObjectDisposedException)
        {
            // 描画中にソースが破棄された: 画像の層を描かずに戻る（ペーストボードのみが残る）
        }
    }

    /// <summary>中心座標ラベルの背景と文字を描く（表示する指示だけを描く）。</summary>
    /// <param name="graphics">描画先の描画面。</param>
    /// <param name="plan">描画計画（配置指示とスケーリング係数を持つ）。</param>
    /// <remarks>
    /// <b>表示可否は配置指示が持つ</b>: 計画は表示を止めても指示を消さないため、<br/>
    /// 描くかどうかは <see cref="LoupeLabelPlacement.IsVisible"/> を見て本クラスが判断する。<br/>
    /// 配置指示の矩形・余白・角丸・フォントサイズは DIP であるため、計画が持つ倍率で物理ピクセルへ換算する<br/>
    /// （文字は測る側と描く側で同じ画素サイズのフォントを使う。 <see cref="DrawingLabelText"/>）。
    /// </remarks>
    private static void PaintLabels(DrawingGraphics graphics, in LoupeDrawPlan plan)
    {
        if (plan.Labels.Count == 0)
        {
            return;
        }

        var scaling = DpiScaler.NormalizeScaling(plan.ClientToPixelScaling);
        for (var index = 0; index < plan.Labels.Count; index++)
        {
            var label = plan.Labels[index];
            if (!label.IsVisible)
            {
                continue;
            }

            var bounds = ToPixelRect(label.DipRect, scaling);
            PaintLabelBackground(graphics, bounds, label.CornerRadius * scaling, label.BackgroundColor);

            if (label.Text.Length == 0)
            {
                continue;
            }

            var origin = new PointF(
                bounds.X + (float)(label.PaddingX * scaling),
                bounds.Y + (float)(label.PaddingY * scaling));
            DrawingLabelText.Draw(graphics, label.Text, label.FontSize, scaling, origin, label.TextColor);
        }
    }

    /// <summary>ラベルの背景を描く（色が透明または角丸が 0 の場合は四角形として描く）。</summary>
    /// <param name="graphics">描画先の描画面。</param>
    /// <param name="bounds">背景の矩形（物理ピクセル）。</param>
    /// <param name="cornerRadius">角丸の半径（物理ピクセル）。 0 の場合は四角形。</param>
    /// <param name="color">背景色（アルファを含む）。 アルファ 0 の場合は描かない。</param>
    private static void PaintLabelBackground(
        DrawingGraphics graphics, DrawingRectangleF bounds, double cornerRadius, DrawingColor color)
    {
        if (color.A == 0)
        {
            return;
        }

        using var brush = new DrawingBrush(color);
        if (cornerRadius > 0)
        {
            using var path = CreateRoundRectPath(bounds, (float)cornerRadius);
            graphics.FillPath(brush, path);
            return;
        }

        graphics.FillRectangle(brush, bounds);
    }

    /// <summary>角丸矩形の輪郭を作る。</summary>
    /// <param name="bounds">矩形（物理ピクセル）。</param>
    /// <param name="radius">角の半径（物理ピクセル）。</param>
    /// <returns>角丸矩形の輪郭。 呼び出し側が破棄する。</returns>
    /// <remarks>
    /// 半径は矩形の短辺の半分で頭打ちにする（半径が大きすぎると輪郭が矩形の外へ出るため）。
    /// </remarks>
    private static DrawingPath CreateRoundRectPath(DrawingRectangleF bounds, float radius)
    {
        var limited = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2f);
        var diameter = limited * 2f;

        var path = new DrawingPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>DIP の矩形を物理ピクセルの矩形へ換算する。</summary>
    /// <param name="rect">換算する矩形（DIP）。</param>
    /// <param name="scaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <returns>物理ピクセルの矩形（端数は保持する）。</returns>
    /// <remarks>位置は丸めない（丸めると同じ大きさのラベルでも倍率によって 1 画素ずれる）。</remarks>
    private static DrawingRectangleF ToPixelRect(EsRect rect, double scaling)
        => new(
            (float)(rect.X * scaling),
            (float)(rect.Y * scaling),
            (float)(rect.Width * scaling),
            (float)(rect.Height * scaling));

    /// <summary>切り出し矩形を画像の内側へ切り詰め、対応する描画先の矩形を求める。</summary>
    /// <param name="source">拡大元の画像（寸法の取得に使う。 読み取りのみ）。</param>
    /// <param name="cropRect">切り出す矩形（対象座標。 画像の外へ出てよい。 切り出し画像の自座標はこの左上を原点とする）。</param>
    /// <param name="drawRect">描画の全体矩形（クライアント座標。 切り出し画像の全体を描く場合の矩形）。</param>
    /// <returns>画像の内側の切り出し矩形と、それに対応する描画先の矩形（交差しない場合は幅 0）。</returns>
    /// <remarks>
    /// <b>外側を切り落とすだけである</b>: 切り出し画像の全体（<paramref name="cropRect"/> の寸法）から
    /// <paramref name="drawRect"/> への写像は一様な拡大であり、<b>画像の内側の部分は同じ倍率で同じ位置へ写る</b>。<br/>
    /// 描画先の矩形は<b>同じ倍率で切り詰める</b>（切り出し矩形の外側の幅・高さの比を保つため、比例で求める）。
    /// </remarks>
    private static (EsRectInt Crop, EsRect Draw) ClipToImage(
        EsReadOnlyBitmap source, EsRectInt cropRect, EsRect drawRect)
    {
        // 画像の内側と交差する部分（交差しない場合は幅 0 を返し、呼び出し側が描かない）
        var left = Math.Max(0, cropRect.X);
        var top = Math.Max(0, cropRect.Y);
        var right = Math.Min(source.Width, cropRect.Right);
        var bottom = Math.Min(source.Height, cropRect.Bottom);
        if (right <= left || bottom <= top || cropRect.Width <= 0 || cropRect.Height <= 0)
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
