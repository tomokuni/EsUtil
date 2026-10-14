using Avalonia;
using Avalonia.Media;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Loupe.AvaloniaUI.Sample.Presentation;

/// <summary>ルーペの描画面の上へ選択矩形の破線を描く（<c>LoupeControl.Rendered</c> の使用例）。</summary>
/// <remarks>
/// <b>示すこと</b>: 描画完了イベントで渡された描画コンテキストへ、ホスト固有の付加情報（選択矩形の破線）を<br/>
/// <b>描画 API で</b>描ける（ルーペの内容の上・中心座標ラベル（テキスト要素）の下に重なる）。<br/>
/// 描画位置は描画計画（<see cref="LoupeDrawPlan.MagnifiedImage"/>）から求める。<br/>
/// 切り出し領域（対象座標）と描画領域（クライアントの物理ピクセル）の比が「対象 1 ピクセル → 描画面の物理ピクセル」の倍率であるため、<br/>
/// 対象座標の矩形をこの倍率で換算すれば拡大画像と正確に一致する（切り出しの位置ずれ（切り上げ）も自動的に吸収される）。<br/>
/// <br/>
/// <b>座標の単位</b>: 描画コンテキストの座標は描画面の物理ピクセルである（ルーペが DIP への倍率を適用済みの状態で通知する）。<br/>
/// <b>破線と太さの単位</b>: DIP で見せたい寸法は <see cref="LoupeDrawPlan.ScaleDipToPixel"/> で換算する。<br/>
/// <b>描画範囲</b>: ルーペは自分の矩形で切り取るため、枠がはみ出しても端の判定は不要である。
/// </remarks>
internal static class SelectionRectPainter
{
    /// <summary>枠線のブラシ（オレンジ。 測定・描画で共有する 1 つを使い回す）。</summary>
    private static readonly IBrush s_brush = new SolidColorBrush(Color.FromArgb(255, 255, 140, 0));

    /// <summary>破線 1 本の長さ（DIP）。</summary>
    private const double DashLengthDip = 4.0;

    /// <summary>破線の間隔（DIP）。</summary>
    private const double DashGapDip = 2.0;

    /// <summary>枠線の太さ（DIP）。</summary>
    private const double StrokeThicknessDip = 2.0;

    /// <summary>選択矩形の破線をルーペの描画面へ描く。</summary>
    /// <param name="context">描画完了イベントが渡した描画コンテキスト（描画中のみ有効）。</param>
    /// <param name="plan">実際に使われた描画計画（切り出しと描画の矩形を持つ）。</param>
    /// <param name="selection">選択矩形（対象座標 = 元画像ピクセル）。</param>
    /// <remarks>拡大中心が未設定（<see cref="LoupeDrawPlan.MagnifiedImage"/> が null）の場合は描かない（描く位置を決める入力が無い）。</remarks>
    internal static void Paint(DrawingContext context, in LoupeDrawPlan plan, EsRect selection)
    {
        if (plan.MagnifiedImage is not { } magnified
            || selection.Width <= 0
            || selection.Height <= 0
            || magnified.CropSourceRect.Width <= 0)
        {
            return;
        }

        // 対象 1 ピクセル → 描画面の物理ピクセルの倍率（切り出し領域と描画領域の比）
        var scale = magnified.DrawClientPixelRect.Width / magnified.CropSourceRect.Width;
        double ToClientX(double x) => (x - magnified.CropSourceRect.X) * scale;
        double ToClientY(double y) => (y - magnified.CropSourceRect.Y) * scale;

        var left = ToClientX(selection.X);
        var top = ToClientY(selection.Y);
        var right = ToClientX(selection.Right);
        var bottom = ToClientY(selection.Bottom);

        var thickness = plan.ScaleDipToPixel(StrokeThicknessDip);
        var dash = plan.ScaleDipToPixel(DashLengthDip);
        var gap = plan.ScaleDipToPixel(DashGapDip);

        // 上辺・下辺（太さは矩形の内側へ寄せる）
        DrawDashedEdge(context, left, top, right, top, thickness, dash, gap, horizontal: true);
        DrawDashedEdge(context, left, bottom - thickness, right, bottom - thickness, thickness, dash, gap, horizontal: true);

        // 左辺・右辺
        DrawDashedEdge(context, left, top, left, bottom, thickness, dash, gap, horizontal: false);
        DrawDashedEdge(context, right - thickness, top, right - thickness, bottom, thickness, dash, gap, horizontal: false);
    }

    /// <summary>破線の 1 辺を描く。</summary>
    /// <param name="context">描画先の描画コンテキスト。</param>
    /// <param name="startX">始点 X（描画面の物理ピクセル）。</param>
    /// <param name="startY">始点 Y（描画面の物理ピクセル）。</param>
    /// <param name="endX">終点 X（描画面の物理ピクセル）。</param>
    /// <param name="endY">終点 Y（描画面の物理ピクセル）。</param>
    /// <param name="thickness">線の太さ（物理ピクセル）。</param>
    /// <param name="dash">破線 1 本の長さ（物理ピクセル）。</param>
    /// <param name="gap">破線の間隔（物理ピクセル）。</param>
    /// <param name="horizontal">水平方向の辺かどうか。</param>
    private static void DrawDashedEdge(
        DrawingContext context, double startX, double startY, double endX, double endY,
        int thickness, int dash, int gap, bool horizontal)
    {
        var length = horizontal ? endX - startX : endY - startY;
        var pitch = Math.Max(1, dash + gap);

        for (var offset = 0.0; offset < length; offset += pitch)
        {
            var segment = Math.Min(dash, length - offset);
            var band = horizontal
                ? new Rect(startX + offset, startY, segment, thickness)
                : new Rect(startX, startY + offset, thickness, segment);
            context.FillRectangle(s_brush, band);
        }
    }
}
