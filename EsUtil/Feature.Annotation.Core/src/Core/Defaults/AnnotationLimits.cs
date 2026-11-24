
namespace EsUtil.Feature.Annotation.Core.Defaults;

/// <summary>注釈の寸法・書式の既定値と安全域（単一所有）。</summary>
/// <remarks>
/// <b>既定値を 1 か所に置く理由</b>: 設定ページ・JSON・UI の入力のどの経路から来た値も同じ補正を受け、
/// 既定値の変更が 4 つの UI 版へ自動で行き渡る（規則の所有者は <c>.github/コーディング規約.md</c> §1）。<br/>
/// 値は<b>元画像ピクセル</b>である（画面の DIP ではない）。
/// </remarks>
public static class AnnotationLimits
{
    /// <summary>枠線の太さの下限（元画像ピクセル）。</summary>
    /// <remarks>入力欄が小数を扱う UI（数値入力コントロール）のために、10 進の値も同じ箇所で所有する。</remarks>
    public const decimal BorderThicknessMinDecimal = 1m;

    /// <summary>枠線の太さの上限（元画像ピクセル）。</summary>
    public const decimal BorderThicknessMaxDecimal = 64m;

    /// <summary>角丸の半径の上限（元画像ピクセル）。</summary>
    public const decimal CornerRadiusMaxDecimal = 4096m;

    /// <summary>本文の余白の上限（元画像ピクセル）。</summary>
    public const decimal BodyPaddingMaxDecimal = 256m;

    /// <summary>しっぽの幅の下限（元画像ピクセル）。</summary>
    public const decimal TailWidthMinDecimal = 2m;

    /// <summary>しっぽの幅の上限（元画像ピクセル）。</summary>
    public const decimal TailWidthMaxDecimal = 512m;

    /// <summary>枠線の太さの下限（元画像ピクセル）。</summary>
    public const double BorderThicknessMin = (double)BorderThicknessMinDecimal;

    /// <summary>枠線の太さの上限（元画像ピクセル）。</summary>
    public const double BorderThicknessMax = (double)BorderThicknessMaxDecimal;

    /// <summary>角丸の半径の上限（元画像ピクセル）。</summary>
    public const double CornerRadiusMax = (double)CornerRadiusMaxDecimal;

    /// <summary>本文の余白の上限（元画像ピクセル）。</summary>
    public const double BodyPaddingMax = (double)BodyPaddingMaxDecimal;

    /// <summary>しっぽの幅の下限（元画像ピクセル）。</summary>
    public const double TailWidthMin = (double)TailWidthMinDecimal;

    /// <summary>しっぽの幅の上限（元画像ピクセル）。</summary>
    public const double TailWidthMax = (double)TailWidthMaxDecimal;

    /// <summary>枠線の太さを安全域へ補正する。</summary>
    /// <param name="value">補正する値。</param>
    /// <returns><see cref="BorderThicknessMin"/>〜<see cref="BorderThicknessMax"/> の値（非有限は下限）。</returns>
    public static double CoerceBorderThickness(double value)
        => double.IsFinite(value) ? Math.Clamp(value, BorderThicknessMin, BorderThicknessMax) : BorderThicknessMin;

    /// <summary>角丸の半径を安全域へ補正する。</summary>
    /// <param name="value">補正する値。</param>
    /// <returns>0〜<see cref="CornerRadiusMax"/> の値（非有限は 0）。</returns>
    public static double CoerceCornerRadius(double value)
        => double.IsFinite(value) ? Math.Clamp(value, 0.0, CornerRadiusMax) : 0.0;


    /// <summary>本文の余白を安全域へ補正する。</summary>
    /// <param name="value">補正する値。</param>
    /// <returns>0〜<see cref="BodyPaddingMax"/> の値（非有限は 0）。</returns>
    public static double CoerceBodyPadding(double value)
        => double.IsFinite(value) ? Math.Clamp(value, 0.0, BodyPaddingMax) : 0.0;

    /// <summary>しっぽの幅を安全域へ補正する。</summary>
    /// <param name="value">補正する値。</param>
    /// <returns><see cref="TailWidthMin"/>〜<see cref="TailWidthMax"/> の値（非有限は下限）。</returns>
    public static double CoerceTailWidth(double value)
        => double.IsFinite(value) ? Math.Clamp(value, TailWidthMin, TailWidthMax) : TailWidthMin;

    /// <summary>しっぽの付け根の位置（辺上の比率）を 0〜1 へ補正する。</summary>
    /// <param name="value">補正する値。</param>
    /// <returns>0〜1 の値（非有限は 0.5）。</returns>
    public static double CoerceTailBaseOffset(double value)
        => double.IsFinite(value) ? Math.Clamp(value, 0.0, 1.0) : 0.5;

    /// <summary>正の大きさを返す（0 以下・非有限は <paramref name="fallback"/>）。</summary>
    /// <param name="value">補正する値。</param>
    /// <param name="fallback">無効な値のときに使う値。</param>
    /// <returns>補正した値。</returns>
    public static double CoercePositive(double value, double fallback)
        => double.IsFinite(value) && value > 0 ? value : fallback;
}
