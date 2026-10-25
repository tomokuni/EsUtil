
namespace EsUtil.Feature.Text;


/// <summary><see cref="ZenHanConverter"/> の変換処理をインスタンス メソッドのように呼び出せる拡張メンバーを提供します。</summary>
/// <remarks>
/// C# 14 の拡張メンバー（<c>extension</c> ブロック）で実装しています。<br/>
/// 型を変更せずに、文字列と変換ペア集合へメンバーを追加できます。<br/>
/// 例: <c>"ＡＢＣ１２３".ToHanOnlyAscii()</c> ／ <c>GroupOf.Ascii.ToHanMap.IsEmpty</c><br/>
/// </remarks>
public static class ZenHanConverterExtensions
{
    extension(string text)
    {
        /// <summary>特殊空白・各種ダッシュを正規化し、分離した全角カナを合成します。</summary>
        /// <returns>正規化された文字列</returns>
        public string ToNormalize()
            => ZenHanConverter.ToNormalize(text);

        /// <summary>数字・英字・記号・カナを半角へ統一します。</summary>
        /// <returns>半角化された文字列</returns>
        public string ToHan()
            => ZenHanConverter.ToHan(text);

        /// <summary>全角化し、半角カナを全角カタカナへ統一します。</summary>
        /// <returns>全角カタカナで統一された文字列</returns>
        public string ToZenWithKatakana()
            => ZenHanConverter.ToZenWithKatakana(text);

        /// <summary>全角化し、半角カナを全角ひらがなへ統一します。</summary>
        /// <returns>全角ひらがなで統一された文字列</returns>
        public string ToZenWithHiragana()
            => ZenHanConverter.ToZenWithHiragana(text);

        /// <summary>数字・英字・記号のみを半角化します。</summary>
        /// <returns>数字・英字・記号のみ半角化された文字列</returns>
        public string ToHanOnlyAscii()
            => ZenHanConverter.ToHanOnlyAscii(text);

        /// <summary>数字・英字・記号のみを全角化します。</summary>
        /// <returns>数字・英字・記号のみ全角化された文字列</returns>
        public string ToZenOnlyAscii()
            => ZenHanConverter.ToZenOnlyAscii(text);

        /// <summary>ひらがな・カタカナとかな記号を半角化します。</summary>
        /// <returns>かなを半角化した文字列</returns>
        public string ToHanOnlyKana()
            => ZenHanConverter.ToHanOnlyKana(text);

        /// <summary>カタカナとかな記号のみを半角化します。</summary>
        /// <returns>カタカナのみ半角化した文字列</returns>
        public string ToHanOnlyKatakana()
            => ZenHanConverter.ToHanOnlyKatakana(text);

        /// <summary>半角カナとかな記号を全角カタカナへ統一します。</summary>
        /// <returns>半角カナを全角カタカナへ置換した文字列</returns>
        public string ToZenOnlyKatakana()
            => ZenHanConverter.ToZenOnlyKatakana(text);

        /// <summary>ひらがなと半角カナを全角カタカナへ統一します。</summary>
        /// <returns>かなを全角カタカナで統一した文字列</returns>
        public string ToZenKatakanaOnlyKana()
            => ZenHanConverter.ToZenKatakanaOnlyKana(text);

        /// <summary>カタカナ（全角/半角）をひらがなへ統一します。</summary>
        /// <returns>カナをひらがなで統一した文字列</returns>
        public string ToZenHiraganaOnlyKana()
            => ZenHanConverter.ToZenHiraganaOnlyKana(text);

        /// <summary>全角/半角の英字を大文字へ変換します。</summary>
        /// <returns>英字を大文字にした文字列</returns>
        public string ToUpperCase()
            => ZenHanConverter.ToUpperCase(text);

        /// <summary>全角/半角の英字を小文字へ変換します。</summary>
        /// <returns>英字を小文字にした文字列</returns>
        public string ToLowerCase()
            => ZenHanConverter.ToLowerCase(text);

        /// <summary>タブ文字を半角スペースへ置換します。</summary>
        /// <returns>タブを半角スペースへ置換した文字列</returns>
        public string ConvertTabToSpace()
            => ZenHanConverter.ConvertTabToSpace(text);

        /// <summary>バックスラッシュを半角円記号へ置換します。</summary>
        /// <returns>バックスラッシュを半角円記号へ置換した文字列</returns>
        public string ConvertBackslashToHanYen()
            => ZenHanConverter.ConvertBackslashToHanYen(text);
    }

    extension(IEnumerable<(string Source, string Target)> pairs)
    {
        /// <summary>変換ペアを 1 件も含まないかどうかを取得します。</summary>
        /// <remarks><see cref="IEnumerable{T}"/> を実体化せずに判定します。<br/></remarks>
        public bool IsEmpty
            => !pairs.Any();

        /// <summary>変換ペア集合から <see cref="ConvertPairs"/> を生成します。</summary>
        /// <returns>ペア集合を保持する変換リスト</returns>
        public ConvertPairs ToConvertPairs()
            => new(pairs);
    }
}
