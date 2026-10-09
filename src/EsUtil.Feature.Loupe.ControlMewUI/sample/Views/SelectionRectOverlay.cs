using Aprillz.MewUI;
using Aprillz.MewUI.Skia.Controls;
using EsUtil.Platform.Core.Geometry;
using SkiaSharp;

namespace EsUtil.Feature.Loupe.MewUI.Sample.Views;

/// <summary>選択矩形の位置を示す破線の枠（テスト画像の上へ重ねる透明なオーバーレイ）。</summary>
/// <remarks>
/// <b>示すこと</b>: ルーペのソース（選択対象）が元画像のどこにあるかを、画像側にも示せる。<br/>
/// 描画は選択矩形（対象座標 = 元画像ピクセル）を DIP のまま写すだけで、位置の判断は持たない。<br/>
/// 破線は Skia のパス効果（<c>CreateDash</c>）で描くため、線の太さ・破線長は DIP で指定できる。<br/>
/// <br/>
/// <b>入力</b>: 透明なオーバーレイのため、入力は下の要素へ通す（<c>IsHitTestVisible</c> を false にする）。
/// </remarks>
internal sealed class SelectionRectOverlay : SkiaCanvasView
{
    /// <summary>枠線の色（オレンジ）。</summary>
    private static readonly SKColor s_strokeColor = SKColor.Parse("#FF8C00");

    /// <summary>内側の塗りの色（半透明のオレンジ）。</summary>
    private static readonly SKColor s_fillColor = new(0xFF, 0x8C, 0x00, 0x30);

    /// <summary>破線のパターン（線長 4 DIP・間隔 2 DIP）。</summary>
    private static readonly float[] s_dashPattern = [4.0f, 2.0f];

    /// <summary>枠線の太さ（DIP）。</summary>
    private const float StrokeThickness = 2.0f;

    /// <summary>描画する選択矩形（対象座標 = 元画像ピクセル。 画像を DIP で 1:1 に置くためローカル DIP と一致する）。</summary>
    private EsRect? _rect;

    /// <summary>選択矩形のオーバーレイを初期化する。</summary>
    /// <remarks>静止画のため連続再描画を無効化し、入力は下の要素へ通す。</remarks>
    public SelectionRectOverlay()
    {
        ContinuousAnimation = false;
        IsHitTestVisible = false;
        PaintSurface += OnPaintSurface;
    }

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
    /// <param name="canvas">描画先のキャンバス（物理ピクセル）。</param>
    /// <param name="info">描画先の情報（幅・高さ）。</param>
    /// <remarks>キャンバスを DPI スケーリングしてから DIP 座標で描く（枠線の太さと破線長を DIP で揃えるため）。</remarks>
    private void OnPaintSurface(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(SKColors.Transparent);

        if (_rect is not { } rect || rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        var scaling = Bounds.Size.Width > 0 ? (float)(info.Width / Bounds.Size.Width) : 1f;

        canvas.Save();
        canvas.Scale(scaling);
        try
        {
            var bounds = new SKRect(0, 0, (float)rect.Width, (float)rect.Height);

            using var fill = new SKPaint { Style = SKPaintStyle.Fill, Color = s_fillColor, IsAntialias = false };
            canvas.DrawRect(bounds, fill);

            using var stroke = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = s_strokeColor,
                StrokeWidth = StrokeThickness,
                PathEffect = SKPathEffect.CreateDash(s_dashPattern, 0),
                IsAntialias = false,
            };
            canvas.DrawRect(bounds, stroke);
        }
        finally
        {
            canvas.Restore();
        }
    }
}
