using System.Globalization;

namespace EsUtil.Platform.Core.Common;

/// <summary>数値を自然な表記の文字列へ書式化する（ログ・画面表示用）。</summary>
/// <remarks>
/// <b>最短往復表記（<see cref="double.ToString()"/>）は使わない</b>: 2 進小数の誤差がそのまま現れ、
/// <c>1.5999999999999996</c> のような値がログへ出る（実測 2026-10-31。 DPI 1.25 の環境で 2 画素 = 1.6 DIP がこの表記になった）。<br/>
/// 実数は 12 桁で丸めて末尾の 0 を落とし、<b>指数表記にしない</b>（座標・倍率・寸法は小さい値のため、
/// <c>1.6E+00</c> のような表記は読みにくい）。<br/>
/// <b>桁数を固定しない</b>: 丸めた結果だけを出す（<c>1.6</c> は <c>1.6</c>、<c>12</c> は <c>12</c>）。
/// </remarks>
public static class NumberText
{
    /// <summary>実数を自然な表記の文字列へ書式化する。</summary>
    /// <param name="value">値。</param>
    /// <returns>丸めて末尾の 0 を落とした表記（カルチャ非依存）。 有限でない値（NaN・無限大）は型の標準表記。</returns>
    public static string Format(double value)
        => double.IsFinite(value)
            ? value.ToString("0.############", CultureInfo.InvariantCulture)
            : value.ToString(CultureInfo.InvariantCulture);

    /// <summary>数値を自然な表記の文字列へ書式化する（整数はそのまま）。</summary>
    /// <typeparam name="T">数値の型（整数・実数）。</typeparam>
    /// <param name="value">値。</param>
    /// <returns>自然な表記の文字列（カルチャ非依存）。</returns>
    /// <remarks>
    /// 実数だけを丸める（整数は誤差を持たないため、そのままの表記が自然である）。<br/>
    /// 型で分岐する理由は、汎用の <c>ToString(format, provider)</c> が丸めの指定を受け取れないためである。
    /// </remarks>
    public static string Format<T>(T value)
        where T : IFormattable
        => value is double real ? Format(real) : value.ToString(format: null, CultureInfo.InvariantCulture);
}
