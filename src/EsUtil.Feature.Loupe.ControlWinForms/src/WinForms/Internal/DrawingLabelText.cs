using DrawingBrush = System.Drawing.SolidBrush;
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingPointF = System.Drawing.PointF;
using DrawingStringFormat = System.Drawing.StringFormat;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Loupe.WinForms.Internal;

/// <summary>中心座標ラベルの文字の実寸測定と描画を単一所有する（GDI+）。</summary>
/// <remarks>
/// <b>位置付け</b>: ラベルの配置矩形は「文字の実寸 + 内側余白」で決まるため、<b>測る API と描く API を揃えなければならない</b><br/>
/// （測った値と実際に描かれる幅が食い違うと、背景から文字がはみ出して切れる）。<br/>
/// 本クラスは GDI+ の <c>Graphics.MeasureString</c> と <c>Graphics.DrawString</c> を同じ体裁設定で使い、<br/>
/// 測る経路（配置の解決）と描く経路（描画層）が同じ実寸を使うようにする。<br/>
/// <br/>
/// <b>単位</b>: フォントは画素サイズ（<c>GraphicsUnit.Pixel</c>）で作る。 配置の解決は DIP で行われるため、<br/>
/// 本クラスは「DIP のフォントサイズ × スケーリング係数 = 画素のフォントサイズ」で測り、結果をスケーリング係数で割って DIP へ戻す<br/>
/// （描画層は文字を物理ピクセルで描くため、測る側も同じ画素サイズで測る必要がある）。<br/>
/// <br/>
/// <b>測定用の描画面</b>: 実寸はフォントと体裁だけで決まるため、1 × 1 ピクセルのビットマップから作った<br/>
/// <see cref="DrawingGraphics"/> で測る（ウィンドウもハンドルも不要であり、ウィンドウを生成しない単体テストでも測れる）。<br/>
/// UI スレッドに依存しないため <c>[Fact]</c> で並列検証できる。
/// </remarks>
internal static class DrawingLabelText
{
    /// <summary>測定に使う描画面の一辺（ピクセル）。 実寸は描画面の大きさに依存しない。</summary>
    private const int MeasureSurfaceSize = 1;

    /// <summary>測定用の描画面から作る描画先を取得する。</summary>
    /// <returns>測定用の描画先。 使い終わったら破棄する。</returns>
    /// <remarks>文字の実寸はフォントと体裁だけで決まるため、描画面は 1 ピクセルで足りる。</remarks>
    private static DrawingGraphics CreateMeasureDevice()
        => DrawingGraphics.FromImage(new Bitmap(MeasureSurfaceSize, MeasureSurfaceSize));

    /// <summary>ラベル用のフォント（画素サイズ）を生成する。</summary>
    /// <param name="pixelSize">フォントサイズ（物理ピクセル）。</param>
    /// <returns>生成したフォント。 呼び出し側が破棄する。</returns>
    /// <remarks>
    /// 書体はアプリケーションの既定フォント（<see cref="Control.DefaultFont"/>）の書体を使う<br/>
    /// （ホストの既定の見た目に合わせるため。 大きさだけをラベルの設定が決める）。<br/>
    /// 呼び出しごとに生成する（保持すると描画のたびの書体の寿命管理が必要になるため、生成と破棄を 1 か所に閉じる）。
    /// </remarks>
    private static DrawingFont CreateFont(double pixelSize)
        => new(Control.DefaultFont.FontFamily, (float)pixelSize, FontStyle.Regular, GraphicsUnit.Pixel);

    /// <summary>文字の体裁を生成する（測る側と描く側で同じ設定を使う）。</summary>
    /// <returns>生成した体裁。 呼び出し側が破棄する。</returns>
    /// <remarks>
    /// 体裁なし（既定）では文字の周囲に余分な送りが入り、測った値と描いた位置がずれる。<br/>
    /// <c>GenericTypographic</c> は文字の実寸（送り・行高）を返すため、背景の大きさと文字の原点が一致する。
    /// </remarks>
    private static DrawingStringFormat CreateFormat() => new(DrawingStringFormat.GenericTypographic);

    /// <summary>文字の実寸を測る（DIP）。</summary>
    /// <param name="text">測る文字列。 null 不可。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <param name="scaling">表示 DIP → 物理ピクセルのスケーリング係数（1 以上を想定するが 0 以下でも安全に動く）。</param>
    /// <returns>文字の実寸（DIP。 描画に使う API が報告する送り幅と行高）。</returns>
    /// <remarks>
    /// 画素で測ってから DIP へ戻す（描画層が使うフォントと同じ画素サイズで測ることで、実際に描かれる幅と一致させる）。
    /// </remarks>
    public static EsSize Measure(string text, double fontSize, double scaling)
    {
        ArgumentNullException.ThrowIfNull(text);

        var normalizedScaling = DpiScaler.NormalizeScaling(scaling);
        using var font = CreateFont(fontSize * normalizedScaling);
        using var format = CreateFormat();
        using var device = CreateMeasureDevice();

        var size = device.MeasureString(text, font, new DrawingPointF(0, 0), format);
        return new EsSize(size.Width / normalizedScaling, size.Height / normalizedScaling);
    }

    /// <summary>文字を描く（物理ピクセルの座標へ置く）。</summary>
    /// <param name="graphics">描画先。 null 不可。</param>
    /// <param name="text">描く文字列。 空の場合は何もしない。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <param name="scaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <param name="origin">文字の原点（物理ピクセル。 文字の左上）。</param>
    /// <param name="color">文字色（アルファを含む）。</param>
    /// <remarks>
    /// 原点は「文字の左上」であり、背景の矩形から内側余白を引いた位置を呼び出し側（描画層）が渡す。<br/>
    /// 測る側（<see cref="Measure"/>）と同じ体裁で描くため、渡された体裁の実寸と実際に描かれる文字が一致する。
    /// </remarks>
    public static void Draw(
        DrawingGraphics graphics, string text, double fontSize, double scaling, DrawingPointF origin, DrawingColor color)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        using var font = CreateFont(fontSize * DpiScaler.NormalizeScaling(scaling));
        using var format = CreateFormat();
        using var brush = new DrawingBrush(color);

        graphics.DrawString(text, font, brush, origin, format);
    }
}
