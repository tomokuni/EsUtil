using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using SkiaSharp;
using DrawingColor = System.Drawing.Color;

// 複製元: EsUtil.SaveEvidence.Platform.Skia（独立パッケージ用の意図的な複製。 .github/依存関係.md §4.1）
namespace EsUtil.Feature.Marquee.MewUI.Internal;

/// <summary>Skia のテキスト描画（フォント解決・実寸測定・描画）を単一所有する。</summary>
/// <remarks>
/// テキストの実寸は描画 API でしか測れないため、測定と描画を本クラスに集約する。<br/>
/// SkiaSharp は UI フレームワークではなくピクセル・描画の基盤であるため、<see cref="EsBitmap"/> と同じ層（本プロジェクト）が担う。<br/>
/// レイアウト規則（余白・行送り・配置）は各コントロールのコアが単一所有し、本クラスは描画のプリミティブのみを提供する。<br/>
/// フォントのフォールバック（非 ASCII は字形を持つフォントを探す）も本クラスが単一所有するため、<br/>
/// 呼び出し側（描画層・ホスト）は豆腐（□）になる環境を意識しない。<br/>
/// 矩形の塗りは <see cref="SkiaFill"/> が単一所有する（本クラスは文字のみ）。
/// </remarks>
internal static class SkiaText
{
    /// <summary>非 ASCII（日本語等）を含むテキスト用の予備フォント（初回のみ解決する）。</summary>
    private static SKTypeface? s_fallbackTypeface;

    /// <summary>テキストを描画するフォントを解決する。</summary>
    /// <param name="text">描画するテキスト。</param>
    /// <returns>ASCII のみの場合は既定のフォント。 非 ASCII を含む場合は字形を持つフォント（見つからなければ既定のフォント）。</returns>
    /// <remarks>
    /// <see cref="SKTypeface.Default"/> は日本語等のグリフを持たない環境があり、その場合は豆腐（□）になる。<br/>
    /// 非 ASCII を含む場合は先頭の非 ASCII 文字の字形を持つフォントを探し、解決結果を保持して再利用する。
    /// </remarks>
    public static SKTypeface ResolveTypeface(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (IsAsciiOnly(text))
        {
            return SKTypeface.Default;
        }

        s_fallbackTypeface ??= ResolveFallbackTypeface();
        return s_fallbackTypeface ?? SKTypeface.Default;
    }

    /// <summary>テキストの実寸（幅・行高）を測定する。</summary>
    /// <param name="text">測定するテキスト。</param>
    /// <param name="fontSize">フォントサイズ（物理ピクセル）。</param>
    /// <returns>テキストの実寸（幅・行高）。 行高はフォントの <c>Descent - Ascent</c>。</returns>
    public static EsSize Measure(string text, double fontSize)
    {
        using var font = new SKFont(ResolveTypeface(text), (float)fontSize);
        using var paint = new SKPaint { IsAntialias = true };
        return new EsSize(font.MeasureText(text, paint), font.Metrics.Descent - font.Metrics.Ascent);
    }

    /// <summary>テキストを左上原点で描画する。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="text">描画するテキスト。</param>
    /// <param name="left">描画領域の左端。</param>
    /// <param name="top">描画領域の上端（ベースラインではない）。</param>
    /// <param name="fontSize">フォントサイズ（物理ピクセル）。</param>
    /// <param name="color">文字色。</param>
    /// <remarks>Skia の描画はベースライン指定のため、フォントの <c>Ascent</c>（負値）で上端からの位置へ換算する。</remarks>
    public static void Draw(SKCanvas canvas, string text, double left, double top, double fontSize, DrawingColor color)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        using var font = new SKFont(ResolveTypeface(text), (float)fontSize);
        using var paint = new SKPaint { IsAntialias = true, Color = color.ToSKColor() };
        canvas.DrawText(text, (float)left, (float)(top - font.Metrics.Ascent), font, paint);
    }

    /// <summary>テキストが ASCII のみで構成されているかを判定する。</summary>
    /// <param name="text">判定するテキスト。</param>
    /// <returns>すべての文字が ASCII（0x7F 以下）の場合は true。 空文字も true。</returns>
    private static bool IsAsciiOnly(string text)
    {
        foreach (var character in text)
        {
            if (character > 0x7F)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>非 ASCII の字形を持つフォントを解決する。</summary>
    /// <returns>字形を持つフォント。 見つからない場合は null。</returns>
    private static SKTypeface? ResolveFallbackTypeface()
    {
        foreach (var character in "あ漢")
        {
            if (SKFontManager.Default.MatchCharacter(character) is { } typeface)
            {
                return typeface;
            }
        }

        return null;
    }
}
