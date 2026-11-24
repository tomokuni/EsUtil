namespace EsUtil.Feature.StyledText.Core.Text;

/// <summary>本文へ入れる文字列の正規化（単一所有）。</summary>
/// <remarks>
/// <b>本文の改行は <c>\n</c> だけ</b>である: 折り返し・行の対応・保存する JSON が同じ規則を使う。 入力欄・貼り付け・
/// IME の確定文字列は改行の表し方が異なるため、本文へ入れる前にここで揃える。
/// </remarks>
public static class TextInput
{
    /// <summary>改行を本文の規則（<c>\n</c>）へ揃える。</summary>
    /// <param name="text">入力された文字列。</param>
    /// <returns>改行を揃えた文字列。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> が null の場合。</exception>
    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }
}
