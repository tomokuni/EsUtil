using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Rendering;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.MewUI.Loupe.Sample.Views;

/// <summary>選択矩形の位置を示す破線の枠（テスト画像の上へ重ねる透明なオーバーレイ）。</summary>
/// <remarks>
/// <b>示すこと</b>: ルーペのソース（選択対象）が元画像のどこにあるかを、画像側にも示せる。<br/>
/// 描画は選択矩形（対象座標 = 元画像ピクセル）を DIP のまま写すだけで、位置の判断は持たない。<br/>
/// <b>破線の描き方</b>: MewUI の描画 API は線種を持たないため、辺ごとに線分へ分解して描く。<br/>
/// <br/>
/// <b>入力</b>: 透明なオーバーレイのため、入力は下の要素へ通す（<c>IsHitTestVisible</c> を false にする）。
/// </remarks>
internal sealed class SelectionRectOverlay : Control
{
    /// <summary>枠線の色（オレンジ）。</summary>
    private static readonly System.Drawing.Color s_strokeColor = System.Drawing.Color.FromArgb(255, 255, 140, 0);

    /// <summary>内側の塗りの色（半透明のオレンジ）。</summary>
    private static readonly System.Drawing.Color s_fillColor = System.Drawing.Color.FromArgb(0x30, 0xFF, 0x8C, 0x00);

    /// <summary>破線 1 本の長さ（DIP）。</summary>
    private const double DashLength = 4.0;

    /// <summary>破線の間隔（DIP）。</summary>
    private const double DashGap = 2.0;

    /// <summary>枠線の太さ（DIP）。</summary>
    private const double StrokeThickness = 2.0;

    /// <summary>描画する選択矩形（対象座標 = 元画像ピクセル。 画像を DIP で 1:1 に置くためローカル DIP と一致する）。</summary>
    private EsRect? _rect;

    /// <summary>選択矩形のオーバーレイを初期化する。</summary>
    /// <remarks>入力は下の要素へ通す（描画のみを担う透明な層）。</remarks>
    public SelectionRectOverlay() => IsHitTestVisible = false;

    /// <summary>描画する選択矩形を設定する。</summary>
    /// <param name="rect">選択矩形（対象座標）。 null の場合は何も描画しない。</param>
    public void SetRect(EsRect? rect)
    {
        _rect = rect;
        InvalidateVisual();
    }

    /// <summary>親の残余を埋めるため、自身の要求サイズを持たない。</summary>
    /// <param name="availableSize">親から提示された利用可能サイズ（使用しない）。</param>
    /// <returns>常に <see cref="Size.Empty"/>。</returns>
    protected override Size MeasureContent(Size availableSize) => Size.Empty;

    /// <summary>DPI の変更時に再描画する（線の太さ・破線長は DIP のため）。</summary>
    /// <param name="oldDpi">変更前の DPI。</param>
    /// <param name="newDpi">変更後の DPI。</param>
    protected override void OnDpiChanged(uint oldDpi, uint newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        InvalidateVisual();
    }

    /// <summary>選択矩形の破線枠と半透明の塗りを描画する。</summary>
    /// <param name="context">描画先のコンテキスト（DIP 座標）。</param>
    /// <remarks>座標は DIP のまま扱う（枠線の太さと破線長を DIP で揃えるため）。</remarks>
    protected override void OnRender(IGraphicsContext context)
    {
        if (_rect is not { } rect || rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        var bounds = new Rect(0, 0, rect.Width, rect.Height);
        context.FillRectangle(bounds, Color.FromArgb(s_fillColor.A, s_fillColor.R, s_fillColor.G, s_fillColor.B));

        var color = Color.FromArgb(s_strokeColor.A, s_strokeColor.R, s_strokeColor.G, s_strokeColor.B);
        StrokeDashedLine(context, 0, 0, rect.Width, 0, color);
        StrokeDashedLine(context, rect.Width, 0, rect.Width, rect.Height, color);
        StrokeDashedLine(context, rect.Width, rect.Height, 0, rect.Height, color);
        StrokeDashedLine(context, 0, rect.Height, 0, 0, color);
    }

    /// <summary>1 本の線分を破線で描く。</summary>
    /// <param name="context">描画先のコンテキスト。</param>
    /// <param name="x0">始点の X 座標。</param>
    /// <param name="y0">始点の Y 座標。</param>
    /// <param name="x1">終点の X 座標。</param>
    /// <param name="y1">終点の Y 座標。</param>
    /// <param name="color">線の色。</param>
    private static void StrokeDashedLine(
        IGraphicsContext context, double x0, double y0, double x1, double y1, Color color)
    {
        var dx = x1 - x0;
        var dy = y1 - y0;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length <= 0)
        {
            return;
        }

        var stepX = dx / length;
        var stepY = dy / length;
        for (var start = 0.0; start < length; start += DashLength + DashGap)
        {
            var end = Math.Min(start + DashLength, length);
            context.DrawLine(
                new Point(x0 + (stepX * start), y0 + (stepY * start)),
                new Point(x0 + (stepX * end), y0 + (stepY * end)),
                color,
                StrokeThickness);
        }
    }
}
