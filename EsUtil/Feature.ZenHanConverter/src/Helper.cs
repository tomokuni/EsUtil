using System.Text.RegularExpressions;

namespace EsUtil.Feature.Text;


/// <summary>ユーティリティ系の補助メソッドをまとめて提供する静的ユーティリティクラスです。</summary>
public static partial class Helper
{
    /// <summary>"U+XXXX" 形式の文字列を対応する Unicode 文字列に変換します。</summary>
    /// <remarks>32 ビットのコードポイント範囲（0x0000～0x10FFFF）をサポートし、不正な定義はそのまま保持します。<br/></remarks>
    /// <param name="s">変換対象の文字列（例: "U+3042" または "U+3042 U+3044"）</param>
    /// <returns>変換後の文字列</returns>
    public static string DecodeUnicodeNotation(string s)
        => DecodeUnicodeNotationRegex().Replace(s, static m =>
        {
            // 捕捉した 16 進表記を string 化せず ReadOnlySpan<char> のまま解析し、割り当てを 1 件分削減する
            var hex = m.Groups["hex"].ValueSpan;
            return int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int cp)
                && cp <= 0x10FFFF && (cp < 0xD800 || cp > 0xDFFF)
                ? char.ConvertFromUtf32(cp)
                : m.Value;
        });

    [GeneratedRegex(@"[Uu]\+(?<hex>[0-9A-Fa-f]{4,6})(?: |\z)", RegexOptions.Compiled, matchTimeoutMilliseconds: 500)]
    private static partial Regex DecodeUnicodeNotationRegex();
}
