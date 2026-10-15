using SkiaSharp;

namespace EsUtil.Feature.Loupe.WinUI3.Sample.Presentation;

/// <summary>選択矩形の破線枠（オレンジ）を描く（テスト画像の側とルーペ画像の側で共用する）。</summary>
/// <remarks>
/// <b>示すこと</b>: 破線・太さの単位は DIP とし、描画面（物理ピクセル）へ合わせる換算は呼び出し側が与える<br/>
/// （テスト画像の側は表示倍率、ルーペの側は描画計画が持つスケーリング係数）。<br/>
/// 単位の換算を引数にすることで、同じ見た目の破線を両方の描画面へ同じ規則で描ける。
/// </remarks>
internal static class SelectionRectPainter
{
    /// <summary>枠線の色（オレンジ）。</summary>
    private static readonly SKColor s_strokeColor = SKColor.Parse("#FF8C00");

    /// <summary>内側の塗りの色（半透明のオレンジ）。</summary>
    private static readonly SKColor s_fillColor = new(0xFF, 0x8C, 0x00, 0x30);

    /// <summary>枠線の太さ（DIP）。</summary>
    private const float StrokeThickness = 2.0f;

    /// <summary>破線 1 本の長さ（DIP）。</summary>
    private const float DashLength = 4.0f;

    /// <summary>破線の間隔（DIP）。</summary>
    private const float DashGap = 2.0f;

    /// <summary>矩形の破線枠と半透明の塗りを描く。</summary>
    /// <param name="canvas">描画先のキャンバス（物理ピクセル）。 null 不可。</param>
    /// <param name="rect">描く矩形（物理ピクセル）。</param>
    /// <param name="scaling">DIP → 描画面（物理ピクセル）のスケーリング係数。</param>
    /// <param name="withFill">内側を半透明で塗るかどうか。</param>
    /// <exception cref="ArgumentNullException"><paramref name="canvas"/> が null の場合。</exception>
    /// <remarks>空の矩形（幅または高さが 0 以下）は何も描かない（「描く対象がない」の判断を呼び出し側へ持ち込まない）。</remarks>
    public static void Paint(SKCanvas canvas, SKRect rect, double scaling, bool withFill = false)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        var factor = (float)(double.IsFinite(scaling) && scaling > 0 ? scaling : 1.0);

        if (withFill)
        {
            using var fill = new SKPaint { Style = SKPaintStyle.Fill, Color = s_fillColor, IsAntialias = false };
            canvas.DrawRect(rect, fill);
        }

        using var stroke = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = s_strokeColor,
            StrokeWidth = StrokeThickness * factor,
            PathEffect = SKPathEffect.CreateDash([DashLength * factor, DashGap * factor], 0),
            IsAntialias = false,
        };
        canvas.DrawRect(rect, stroke);
    }
}
