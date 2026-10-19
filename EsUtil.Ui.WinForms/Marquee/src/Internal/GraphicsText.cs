using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.WinForms.Marquee.Internal;

/// <summary>GDI+ のテキスト描画（フォント解決・実寸測定・描画）を単一所有する。</summary>
/// <remarks>
/// テキストの実寸は描画に使う API で測らなければ一致しないため、測定と描画を本クラスに集約する。<br/>
/// レイアウト規則（余白・配置）は各コントロールのコアと提示層が単一所有し、本クラスは描画のプリミティブのみを提供する。<br/>
/// 矩形の塗りは <see cref="GraphicsFill"/> が単一所有する（本クラスは文字のみ）。<br/>
/// <br/>
/// <b>測定に専用の描画面を使う理由</b>: 描画先の <see cref="Graphics"/> は DPI に応じて拡大されているため、<br/>
/// そのまま測ると DIP ではなく物理ピクセルの寸法が返る。 測定は基準 DPI（96）の専用描画面で行い、<br/>
/// 描画は与えられた描画面の変換（DPI の拡大）に従う（両者を同じフォント サイズで扱うことで寸法が一致する）。<br/>
/// <b>フォントの単位はピクセル</b>である（描画先の <c>PageUnit</c> に依存しない固定の単位で扱う）。
/// </remarks>
internal static class GraphicsText
{
    /// <summary>測定に使う描画面（スレッドごとに 1 つを保持する）。</summary>
    [ThreadStatic]
    private static Graphics? s_measure;

    /// <summary>測定に使う描画面の下地（スレッドごとに 1 つを保持する）。</summary>
    [ThreadStatic]
    private static Bitmap? s_probe;

    /// <summary>描画に使うフォント（スレッドごとに 1 つを、大きさごとに作り直して使い回す）。</summary>
    [ThreadStatic]
    private static Font? s_font;

    /// <summary><see cref="s_font"/> を作ったときのフォント サイズ（DIP）。</summary>
    [ThreadStatic]
    private static double s_fontSize;

    /// <summary>描画に使うブラシ（スレッドごとに 1 つを、色だけ書き換えて使い回す）。</summary>
    [ThreadStatic]
    private static SolidBrush? s_text;

    /// <summary>測定と描画に使う書式（スレッドごとに 1 つを保持する）。</summary>
    /// <remarks>
    /// <see cref="StringFormat.GenericTypographic"/> を測定と描画の両方で使う（余白の加算規則を一致させ、<br/>
    /// 測定した寸法で作った背景から文字がはみ出さないようにする）。
    /// </remarks>
    [ThreadStatic]
    private static StringFormat? s_format;

    /// <summary>測定に使う描画面を取得する。</summary>
    /// <returns>基準 DPI（96）の描画面。</returns>
    /// <remarks>
    /// 下地（<see cref="Bitmap"/>）は描画面の寿命の間保持する（破棄されると描画面が無効になる）。<br/>
    /// 保持はプロセス（スレッド）の寿命と同じであり、測定のたびに生成と破棄を繰り返さない。
    /// </remarks>
    private static Graphics ResolveMeasure()
    {
        var probe = s_probe;
        if (probe is null)
        {
            probe = new Bitmap(1, 1);
            s_probe = probe;
        }

        return s_measure ??= Graphics.FromImage(probe);
    }

    /// <summary>描画に使うフォント ファミリ（既定の UI フォント。 取得できない場合はコントロールの既定フォント）。</summary>
    /// <remarks>
    /// <see cref="SystemFonts.MessageBoxFont"/> は null を返しうるため、フォールバックを 1 箇所に閉じる<br/>
    /// （<see cref="Control.DefaultFont"/> は非 null）。
    /// </remarks>
    private static readonly FontFamily s_family = (SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily;

    /// <summary>文字を描くフォントを取得する（大きさが変わったときだけ作り直す）。</summary>
    /// <param name="fontSize">フォント サイズ（DIP）。</param>
    /// <returns>指定の大きさのフォント（スレッドごとに共有する 1 つ）。</returns>
    /// <remarks><see cref="Font.Size"/> は読み取り専用であるため、大きさごとに作り直し、古いフォントは破棄する。</remarks>
    private static Font ResolveFont(double fontSize)
    {
        if (s_font is null || s_fontSize != fontSize)
        {
            s_font?.Dispose();
            s_font = new Font(s_family, (float)fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
            s_fontSize = fontSize;
        }

        return s_font;
    }

    /// <summary>文字を描くブラシを取得する（色だけを書き換える）。</summary>
    /// <param name="color">文字色。</param>
    /// <returns>書き換え済みのブラシ（スレッドごとに共有する 1 つ）。</returns>
    private static SolidBrush ResolveText(Color color)
    {
        var brush = s_text ??= new SolidBrush(color);
        brush.Color = color;
        return brush;
    }

    /// <summary>測定と描画に使う書式を取得する。</summary>
    /// <returns>文字の送りを詰めた書式（スレッドごとに共有する 1 つ）。</returns>
    private static StringFormat ResolveFormat() => s_format ??= StringFormat.GenericTypographic;

    /// <summary>テキストの実寸（幅・行高）を測定する。</summary>
    /// <param name="text">測定するテキスト。 null 不可。</param>
    /// <param name="fontSize">フォント サイズ（DIP）。</param>
    /// <returns>テキストの実寸（DIP）。</returns>
    /// <remarks>測定は基準 DPI の専用描画面で行う（描画先の DPI に影響されない DIP の寸法を返す）。</remarks>
    public static EsSize Measure(string text, double fontSize)
    {
        ArgumentNullException.ThrowIfNull(text);

        var size = ResolveMeasure().MeasureString(text, ResolveFont(fontSize), new PointF(0, 0), ResolveFormat());
        return new EsSize(size.Width, size.Height);
    }

    /// <summary>テキストを左上原点で描画する。</summary>
    /// <param name="graphics">描画先（DPI の拡大が適用されていてよい）。 null 不可。</param>
    /// <param name="text">描画するテキスト。 null 不可。</param>
    /// <param name="left">描画領域の左端。</param>
    /// <param name="top">描画領域の上端（ベースラインではない）。</param>
    /// <param name="fontSize">フォント サイズ（DIP）。</param>
    /// <param name="color">文字色。</param>
    /// <remarks>描画先の変換（DPI の拡大）に従うため、フォント サイズは DIP のまま渡す。</remarks>
    public static void Draw(Graphics graphics, string text, double left, double top, double fontSize, Color color)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(text);

        graphics.DrawString(
            text, ResolveFont(fontSize), ResolveText(color), new PointF((float)left, (float)top), ResolveFormat());
    }
}
