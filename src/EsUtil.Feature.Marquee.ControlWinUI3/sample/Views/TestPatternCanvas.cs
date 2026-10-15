using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace EsUtil.Feature.Marquee.WinUI3.Sample.Views;

/// <summary>選択の対象（背景のテスト パターン）を描くキャンバス。</summary>
/// <remarks>
/// <b>示すこと</b>: コントロールは選択の装飾だけを描き、背景は透明のまま残す（下の内容が透ける）。<br/>
/// そのため背景は別のキャンバス（本クラス）が描く。 画像ファイルを用意せずに選択の位置と寸法が読めるよう、<br/>
/// 格子・色見本・中心の十字を描く。<br/>
/// <b>座標系</b>: 描画面は DIP 座標で描く（コントロールの倍率と同じ規則で、描画面の実寸と割り当て領域の比を使う）。
/// </remarks>
public sealed class TestPatternCanvas : SKXamlCanvas
{
    /// <summary>格子の間隔（DIP）。</summary>
    private const float GridStep = 50.0f;

    /// <summary>色見本の一辺（DIP）。</summary>
    private const float SwatchSize = 80.0f;

    /// <summary>格子線の色。</summary>
    private static readonly SKColor s_gridColor = new(0x60, 0x60, 0x60);

    /// <summary>色見本の色（左上から順に並べる）。</summary>
    private static readonly SKColor[] s_swatchColors =
    [
        new(0xE5, 0x39, 0x35),
        new(0xFB, 0x8C, 0x00),
        new(0xFD, 0xD8, 0x35),
        new(0x43, 0xA0, 0x47),
        new(0x1E, 0x88, 0xE5),
        new(0x8E, 0x24, 0xAA),
    ];

    /// <summary>描画面へテスト パターンを描く。</summary>
    /// <param name="e">描画面の情報（表面・寸法）。</param>
    /// <remarks>
    /// 倍率（描画面の実寸 ÷ 割り当て領域）を自分で求め、DIP 座標で描く（コントロールと同じ規則）。<br/>
    /// 前フレームの内容を残さないため、描く前に自分の領域を消す。
    /// </remarks>
    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);

        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var scaling = (float)(e.Info.Width / ActualWidth);
        var width = (float)(e.Info.Width / scaling);
        var height = (float)(e.Info.Height / scaling);

        canvas.Save();
        canvas.Scale(scaling);
        try
        {
            DrawBackground(canvas, width, height);
            DrawGrid(canvas, width, height);
            DrawSwatches(canvas, width, height);
            DrawCenterCross(canvas, width, height);
        }
        finally
        {
            canvas.Restore();
        }
    }

    /// <summary>背景（濃い灰色の下地）を塗る。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="width">幅（DIP）。</param>
    /// <param name="height">高さ（DIP）。</param>
    private static void DrawBackground(SKCanvas canvas, float width, float height)
    {
        using var paint = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill, Color = new SKColor(0x30, 0x30, 0x30) };
        canvas.DrawRect(0, 0, width, height, paint);
    }

    /// <summary>格子線を描く（選択の位置と寸法を読めるようにする）。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="width">幅（DIP）。</param>
    /// <param name="height">高さ（DIP）。</param>
    private static void DrawGrid(SKCanvas canvas, float width, float height)
    {
        using var paint = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 1, Color = s_gridColor };

        for (var x = GridStep; x < width; x += GridStep)
        {
            canvas.DrawLine(x, 0, x, height, paint);
        }

        for (var y = GridStep; y < height; y += GridStep)
        {
            canvas.DrawLine(0, y, width, y, paint);
        }
    }

    /// <summary>色見本を描く（選択の重なりを見分けられるようにする）。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="width">幅（DIP）。</param>
    /// <param name="height">高さ（DIP）。</param>
    private static void DrawSwatches(SKCanvas canvas, float width, float height)
    {
        using var paint = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill };

        for (var index = 0; index < s_swatchColors.Length; index++)
        {
            var x = 20.0f + (index * (SwatchSize + 10.0f));
            var y = height - SwatchSize - 20.0f;
            if (x + SwatchSize > width || y < 0)
            {
                break;
            }

            paint.Color = s_swatchColors[index];
            canvas.DrawRect(x, y, SwatchSize, SwatchSize, paint);
        }
    }

    /// <summary>中心の十字を描く（注目位置の基準）。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="width">幅（DIP）。</param>
    /// <param name="height">高さ（DIP）。</param>
    private static void DrawCenterCross(SKCanvas canvas, float width, float height)
    {
        using var paint = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = SKColors.White };

        canvas.DrawLine((width / 2) - 20, height / 2, (width / 2) + 20, height / 2, paint);
        canvas.DrawLine(width / 2, (height / 2) - 20, width / 2, (height / 2) + 20, paint);
    }
}
