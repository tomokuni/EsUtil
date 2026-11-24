namespace EsUtil.Feature.Text.Core.Defaults;

/// <summary>文字の寸法の既定値と安全域（単一所有）。</summary>
/// <remarks>
/// <b>既定値を 1 か所に置く理由</b>: 設定ページ・JSON・UI の入力のどの経路から来た値も同じ補正を受け、
/// 既定値の変更が 4 つの UI 版へ自動で行き渡る（規則の所有者は <c>.github/コーディング規約.md</c> §1）。<br/>
/// 値は<b>元画像ピクセル</b>である（画面の DIP ではない）。
/// </remarks>
public static class TextLimits
{
    /// <summary>文字の大きさの下限（元画像ピクセル）。</summary>
    /// <remarks>入力欄が小数を扱う UI（数値入力コントロール）のために、10 進の値も同じ箇所で所有する。</remarks>
    public const decimal FontSizeMinDecimal = 6m;

    /// <summary>文字の大きさの上限（元画像ピクセル）。</summary>
    public const decimal FontSizeMaxDecimal = 512m;

    /// <summary>文字の大きさの下限（元画像ピクセル）。</summary>
    public const double FontSizeMin = (double)FontSizeMinDecimal;

    /// <summary>文字の大きさの上限（元画像ピクセル）。</summary>
    public const double FontSizeMax = (double)FontSizeMaxDecimal;

    /// <summary>文字の大きさを安全域へ補正する。</summary>
    /// <param name="value">補正する値。</param>
    /// <returns><see cref="FontSizeMin"/>〜<see cref="FontSizeMax"/> の値（非有限は下限）。</returns>
    public static double CoerceFontSize(double value)
        => double.IsFinite(value) ? Math.Clamp(value, FontSizeMin, FontSizeMax) : FontSizeMin;

    /// <summary>正の大きさを返す（0 以下・非有限は <paramref name="fallback"/>）。</summary>
    /// <param name="value">補正する値。</param>
    /// <param name="fallback">無効な値のときに使う値。</param>
    /// <returns>補正した値。</returns>
    public static double CoercePositive(double value, double fallback)
        => double.IsFinite(value) && value > 0 ? value : fallback;
}