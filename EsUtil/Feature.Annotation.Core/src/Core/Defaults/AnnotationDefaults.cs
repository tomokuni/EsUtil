using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Annotation.Core.Defaults;

/// <summary>注釈の既定値（新規作成時の外観・しっぽの寸法）。</summary>
/// <remarks>
/// <b>既定値は 1 か所に置く</b>: 設定ページの初期値・新規作成時の値・JSON の欠落値が同じ値になる
/// （UI 版ごとに書き分けない。 規則の所有者は <c>.github/コーディング規約.md</c> §1）。<br/>
/// 設定ページで変えられるのは<b>新規作成時の既定値</b>であり、既存の注釈の外観は変わらない（各注釈が値を持つ）。<br/>
/// <b>文字の既定値は <c>TextDefaults</c> が単一所有する</b>（本文の書式は注釈に固有ではないため）。
/// </remarks>
public static class AnnotationDefaults
{
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
}