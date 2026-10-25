namespace EsUtil.Feature.Text;


/// <summary>全角・半角文字や仮名の変換をまとめて提供する静的ユーティリティクラスです。</summary>
/// <remarks>文字幅や仮名の表記を揃える必要がある日本語テキストの正規化や書式整備で利用できます。</remarks>
public static partial class ZenHanConverter
{

    /// <summary>フリンジケースの空白やダッシュ表現、仮名の合成ルールを適用して文字列を正規化します。</summary>
    /// <param name="text">変換対象の入力文字列。</param>
    /// <returns>正規化が完了した文字列。</returns>
    public static string ToNormalize(string text)
        => ConvertPairs.FromFunc("ZenHanConverter.ToNormalize", () => ConvertPairs.Concat(
            GroupOf.Ascii.ToASpaceMap,
            GroupOf.Ascii.ToAHyphenMap,
            GroupOf.Kana.ComposeMap
        )).Convert(text);


    /// <summary>指定されたテキストに含まれる全角の英数字、記号、ひらがな、カタカナを対応する半角文字に変換します。</summary>
    /// <param name="text">変換対象の入力文字列。</param>
    /// <returns>半角化された文字列。</returns>
    public static string ToHan(string text)
        => ConvertPairs.FromFunc("ZenHanConverter.ToHan", () => ConvertPairs.Concat(
            GroupOf.Ascii.ToHanMap,
            GroupOf.Kana.ToHanMap
        )).Convert(ToNormalize(text));


    /// <summary>英数字、記号、ひらがな、カタカナを全角にし、半角カナは全角カタカナに統一して変換します。</summary>
    /// <param name="text">変換対象の入力文字列。</param>
    /// <returns>全角化され、半角カナはカタカナに揃えた文字列。</returns>
    public static string ToZenWithKatakana(string text)
        => ConvertPairs.FromFunc("ZenHanConverter.ToZenWithKatakana", () => ConvertPairs.Concat(
            GroupOf.Ascii.ToZenMap,
            GroupOf.Kana.ToZenMap
        )).Convert(ToNormalize(text));


    /// <summary>英数字、記号、ひらがな、カタカナを全角にし、半角カナは全角ひらがなに統一して変換します。</summary>
    /// <param name="text">変換対象の入力文字列。</param>
    /// <returns>全角化され、半角カナをひらがなに揃えた文字列。</returns>
    public static string ToZenWithHiragana(string text)
        => ConvertPairs.FromFunc("ZenHanConverter.ToZenWithHiragana", () => ConvertPairs.Concat(
            GroupOf.Ascii.ToZenMap,
            GroupOf.Kana.Hira.ToZenMap,
            GroupOf.Kana.ToZenMap
        )).Convert(ToNormalize(text));


    /// <summary>全角の英数字と記号のみを半角に変換し、ひらがな・カタカナなど他の文字はそのままにします。</summary>
    /// <param name="text">変換対象の入力文字列。</param>
    /// <returns>英数字と記号だけが半角化された文字列。</returns>
    public static string ToHanOnlyAscii(string text)
        => ConvertPairs.FromFunc("ZenHanConverter.ToHanOnlyAscii", () => GroupOf.Ascii.ToHanMap).Convert(ToNormalize(text));


    /// <summary>半角の英数字と記号のみを全角に変換し、仮名文字は変更を加えません。</summary>
    /// <param name="text">変換対象の入力文字列。</param>
    /// <returns>英数字と記号のみが全角化された文字列。</returns>
    public static string ToZenOnlyAscii(string text)
        => ConvertPairs.FromFunc("ZenHanConverter.ToZenOnlyAscii", () => GroupOf.Ascii.ToZenMap).Convert(ToNormalize(text));


    /// <summary>全角のカタカナ・ひらがなと濁点・半濁点付きの仮名を半角に変換し、その他の文字はそのままに保ちます。</summary>
    /// <param name="text">変換対象の入力文字列。</param>
    /// <returns>カタカナを中心に半角化を適用した文字列。</returns>
    public static string ToHanOnlyKana(string text)
        => ConvertPairs.FromFunc("ZenHanConverter.ToHanOnlyKana", () => GroupOf.Kana.ToHanMap).Convert(ToNormalize(text));


    /// <summary>カタカナ文字のみを半角に統一します。</summary>
    /// <param name="text">変換対象の入力文字列。</param>
    /// <returns>カタカナだけを半角化した文字列。</returns>
    public static string ToHanOnlyKatakana(string text)
        => ConvertPairs.FromFunc("ZenHanConverter.ToHanOnlyKatakana", () => ConvertPairs.Concat(
            GroupOf.Kana.Kata.ToHanMap,
            GroupOf.Kana.Symbol.ToHanMap
        )).Convert(ToNormalize(text));


    /// <summary>半角カナを全角カタカナに変換し、その他の文字列は変更しません。</summary>
    /// <param name="text">変換対象の入力文字列。</param>
    /// <returns>半角カナが全角カタカナに置き換えられた文字列。</returns>
    public static string ToZenOnlyKatakana(string text)
        => ConvertPairs.FromFunc("ZenHanConverter.ToZenOnlyKatakana", () => ConvertPairs.Concat(
            GroupOf.Kana.Kata.ToZenMap,
            GroupOf.Kana.Symbol.ToZenMap
        )).Convert(ToNormalize(text));


    /// <summary>全角ひらがなと半角カナの一部を全角カタカナに変換し、可能な限りカタカナ表記に統一します。</summary>
    /// <param name="text">変換対象の入力文字列。</param>
    /// <returns>ひらがなや半角カナを全角カタカナに正規化した文字列。</returns>
    public static string ToZenKatakanaOnlyKana(string text)
        => ConvertPairs.FromFunc("ZenHanConverter.ToZenKatakanaOnlyKana", () => ConvertPairs.Concat(
            GroupOf.Kana.ToZenMap,
            GroupOf.Kana.ToKataMap
        )).Convert(ToNormalize(text));


    /// <summary>全角カタカナと半角カナを全角ひらがなに変換し、ひらがな表記に統一します。</summary>
    /// <param name="text">変換対象の入力文字列。</param>
    /// <returns>カタカナ（全角/半角）をひらがなに置き換えた文字列。</returns>
    public static string ToZenHiraganaOnlyKana(string text)
        => ConvertPairs.FromFunc("ZenHanConverter.ToZenHiraganaOnlyKana", () => ConvertPairs.Concat(
            GroupOf.Kana.Hira.ToZenMap,
            GroupOf.Kana.ToHiraMap,
            GroupOf.Kana.ToZenMap
        )).Convert(ToNormalize(text));


    /// <summary>英小文字を半角・全角問わず対応する英大文字に変換します。</summary>
    /// <param name="text">変換対象の入力文字列。</param>
    /// <returns>アルファベットをすべて大文字で表現した文字列。</returns>
    public static string ToUpperCase(string text)
        => ConvertPairs.FromFunc("ZenHanConverter.ToUpperCase", () => GroupOf.Ascii.ToUpperMap).Convert(ToNormalize(text));


    /// <summary>英大文字を半角・全角問わず対応する英小文字に変換します。</summary>
    /// <param name="text">変換対象の入力文字列。</param>
    /// <returns>アルファベットをすべて小文字で表現した文字列。</returns>
    public static string ToLowerCase(string text)
        => ConvertPairs.FromFunc("ZenHanConverter.ToLowerCase", () => GroupOf.Ascii.ToLowerMap).Convert(ToNormalize(text));


    /// <summary>テキスト内のタブ文字を半角スペースに置換します。</summary>
    /// <param name="text">入力文字列。</param>
    /// <returns>タブがスペースに変換された文字列。</returns>
    public static string ConvertTabToSpace(string text)
        => ConvertPairs.FromFunc("ZenHanConverter.ConvertTabToSpace", () => GroupOf.Ascii.ToSpaceFromTabMap).Convert(text);


    /// <summary>日本語環境でバックスラッシュを半角円記号に変換します。</summary>
    /// <param name="text">入力文字列。</param>
    /// <returns>バックスラッシュが円記号に置換された文字列。</returns>
    public static string ConvertBackslashToHanYen(string text)
        => ConvertPairs.FromFunc("ZenHanConverter.ConvertBackslashToHanYen", () => GroupOf.Ascii.ToYenFromBslashMap).Convert(text);

}

