using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Annotation.Core.Defaults;

/// <summary>注釈の既定値（新規作成時の外観・しっぽ・本文の体裁）。</summary>
/// <remarks>
/// <b>既定値は 1 か所に置く</b>: 設定ページの初期値・新規作成時の値・JSON の欠落値が同じ値になる
/// （UI 版ごとに書き分けない。 規則の所有者は <c>.github/コーディング規約.md</c> §1）。<br/>
/// 設定ページで変えられるのは<b>新規作成時の既定値</b>であり、既存の注釈の外観は変わらない（各注釈が値を持つ）。
/// </remarks>
public static class AnnotationDefaults
{
    /// <summary>使うフォントの優先順（先頭から順に、見つかった最初のものを使う）。</summary>
    /// <remarks>
    /// 先頭は <c>Yu Gothic UI</c>（日本語の画面で標準的に使える UI フォント）、
    /// 無い環境では <c>Segoe UI</c> を使う（両方無い場合は UI の既定フォントへ落ちる）。
    /// </remarks>
    public static IReadOnlyList<string> PreferredFontFamilies { get; } = ["Yu Gothic UI", "Segoe UI"];

    /// <summary>文字の大きさの既定値（元画像ピクセル）。</summary>
    public const double FontSizeDefault = 20.0;

    /// <summary>枠線の太さの既定値（元画像ピクセル）。</summary>
    public const double BorderThicknessDefault = 3.0;

    /// <summary>角丸の半径の既定値（元画像ピクセル）。</summary>
    public const double CornerRadiusDefault = 8.0;

    /// <summary>本文の余白の既定値（元画像ピクセル）。</summary>
    public const double BodyPaddingDefault = 12.0;

    /// <summary>しっぽの幅の既定値（元画像ピクセル）。</summary>
    public const double TailWidthDefault = 28.0;

    /// <summary>しっぽの付け根の位置（辺上の比率）の既定値。</summary>
    public const double TailBaseOffsetDefault = 0.5;

    /// <summary>新規作成する注釈の幅の既定値（元画像ピクセル）。</summary>
    /// <remarks>追加メニューや本文の無いクリックで作る枠の大きさ。 4 つの UI 版で同じ値にする。</remarks>
    public const double NewItemWidthDefault = 240.0;

    /// <summary>新規作成する注釈の高さの既定値（元画像ピクセル）。</summary>
    public const double NewItemHeightDefault = 160.0;

    /// <summary>枠線の既定の外観を取得する（赤い枠・内側は透明）。</summary>
    public static AnnotationAppearance FrameAppearance { get; } = new(
        DrawingColor.FromArgb(255, 220, 38, 38),
        BorderThicknessDefault,
        DrawingColor.Transparent,
        CornerRadiusDefault);

    /// <summary>吹き出しの既定の外観を取得する（黒い枠・白い内側）。</summary>
    public static AnnotationAppearance BubbleAppearance { get; } = new(
        DrawingColor.FromArgb(255, 0, 0, 0),
        BorderThicknessDefault,
        DrawingColor.FromArgb(255, 255, 255, 255),
        CornerRadiusDefault);

    /// <summary>吹き出しの本文の既定の書式を取得する。</summary>
    public static AnnotationTextStyle TextStyle { get; } = new(
        PreferredFontFamilies[0],
        FontSizeDefault,
        DrawingColor.FromArgb(255, 0, 0, 0));
}
