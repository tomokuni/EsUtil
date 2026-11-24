using Aprillz.MewUI;
using Aprillz.MewUI.Rendering;
using Aprillz.MewUI.Text;
using DrawingColor = System.Drawing.Color;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.MewUI.RichTextEditor;

/// <summary>書式付き文字列の描画と実寸の計測（MewUI の描画 API への写し）。</summary>
/// <remarks>
/// <b>位置は決めない</b>: 折り返しと行の配置は <see cref="TextLayout"/> が、行の中の文字位置は
/// <see cref="TextLineLayout"/> が単一所有する。 本クラスは解決済みの行を描画 API へ写すだけである。<br/>
/// <b>測定と描画で同じ DPI を使う</b>: レイアウトを <c>Dpi = 96</c> 固定で作ると、描画側は画面の DPI で
/// グリフを組むため送りが食い違う。 描画の直前に UI が <see cref="DpiScale"/> を設定する。<br/>
/// <b>斜体を外して行の高さを測る</b>: 斜体は字を傾けるだけで行の高さを変えない。
/// </remarks>
internal static class RichTextPainter
{
    /// <summary>文字の実寸を測る基準の DPI。</summary>
    private const uint BaseDpi = (uint)EsUtil.Platform.Core.Coord.DpiScaler.StandardDpi;

    /// <summary>行の高さとベースラインを測るための文字列。</summary>
    private const string LineMetricsText = "Ag";

    /// <summary>下線・取り消し線の太さの比率（文字の大きさに対する比）。</summary>
    private const double DecorationThicknessRatio = 0.06;

    /// <summary>レイアウトを作るときの画面の倍率を取得または設定する。</summary>
    /// <value>画面の DPI 倍率（125% なら 1.25）。 既定は 1.0。</value>
    internal static double DpiScale { get; set; } = 1.0;

    /// <summary>文字列と書式から実寸を測る。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <returns>実寸（元画像ピクセル）。 高さは斜体を外した書式で測る。</returns>
    internal static EsSize Measure(string text, TextStyle style)
    {
        var layout = CreateLayout(text, style, ignoreItalic: false);
        return new EsSize(layout.MeasuredSize.Width, MeasureLineMetrics(style).Height);
    }

    /// <summary>行の実寸（高さとベースライン）を測る（斜体を外した書式で測る）。</summary>
    /// <param name="style">書式。</param>
    /// <returns>行の実寸。</returns>
    internal static TextLineMetrics MeasureLineMetrics(TextStyle style)
    {
        var layout = CreateLayout(LineMetricsText, style, ignoreItalic: true);
        var height = layout.MeasuredSize.Height;
        var baseline = layout.Lines.Count > 0 ? layout.Lines[0].Baseline : height;
        return new TextLineMetrics(height, baseline);
    }

    /// <summary>行を描く（ベースラインを 1 本に決める）。</summary>
    /// <param name="context">描画先。</param>
    /// <param name="line">行。</param>
    /// <param name="layouts">文字のレイアウトを取得する関数（同じフレームでは同じ実体を返す）。</param>
    internal static void DrawLine(IGraphicsContext context, TextLinePlan line, Func<string, TextStyle, ITextLayout> layouts)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(layouts);

        var baseline = TextLineLayout.ResolveBaseline(line, MeasureLineMetrics);
        foreach (var segment in line.Segments)
        {
            var layout = layouts(segment.Text, segment.Style);
            var top = baseline - layout.Lines[0].Baseline;
            if (layout.MeasuredSize.Width > 0)
            {
                var options = new TextDrawOptions { Foreground = ToMewColor(segment.Style.Color) };
                context.Text.Draw(layout, new Point(segment.Bounds.X, top), in options);
            }

            DrawDecorations(context, segment, layout, top);
        }
    }

    /// <summary>文字列を描画できる形（レイアウト）へ変換する。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <param name="ignoreItalic">斜体を外して作る場合は <see langword="true"/>（行の実寸を測るとき）。</param>
    /// <returns>レイアウト。</returns>
    internal static ITextLayout CreateLayout(string text, TextStyle style, bool ignoreItalic)
        => Application.DefaultGraphicsFactory.TextEngine.CreateLayout(new TextLayoutRequest
        {
            Text = text.AsMemory(),
            Dpi = (uint)Math.Max(BaseDpi, Math.Round(BaseDpi * DpiScale)),
            DefaultStyle = new TextRunStyle
            {
                FontFamily = style.FontFamily,
                FontSize = style.FontSize,
                Weight = style.IsBold ? FontWeight.Bold : FontWeight.Normal,
                Italic = style.IsItalic && !ignoreItalic,
            },
        });

    /// <summary>色を MewUI の色へ変換する。</summary>
    /// <param name="color">色。</param>
    /// <returns>MewUI の色。</returns>
    internal static Color ToMewColor(DrawingColor color) => new(color.A, color.R, color.G, color.B);

    /// <summary>下線・取り消し線を描く。</summary>
    /// <param name="context">描画先。</param>
    /// <param name="segment">区間。</param>
    /// <param name="layout">レイアウト（文字の実寸を取るため）。</param>
    /// <param name="top">文字の上端（ベースラインから求めた描画位置）。</param>
    private static void DrawDecorations(
        IGraphicsContext context, TextSegmentPlan segment, ITextLayout layout, double top)
    {
        if (!segment.Style.IsUnderline && !segment.Style.IsStrike)
        {
            return;
        }

        var thickness = Math.Max(1.0, segment.Style.FontSize * DecorationThicknessRatio);
        var color = ToMewColor(segment.Style.Color);
        var left = segment.Bounds.X;
        var right = segment.Bounds.X + layout.MeasuredSize.Width;
        var height = layout.MeasuredSize.Height;

        if (segment.Style.IsUnderline)
        {
            var y = top + height - (thickness * 2);
            context.DrawLine(new Point(left, y), new Point(right, y), color, thickness);
        }

        if (segment.Style.IsStrike)
        {
            var y = top + (height / 2);
            context.DrawLine(new Point(left, y), new Point(right, y), color, thickness);
        }
    }
}
