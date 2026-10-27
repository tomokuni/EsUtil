using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;

namespace EsUtil.Feature.ZenHan;


/// <summary>変換のペア集合を保持し列挙可能な形式で提供します。</summary>
public partial record ConvertPairs : IEnumerable<(string Source, string Target)>
{
    private ImmutableList<(string Source, string Target)>? _pairs;
    private Func<IEnumerable<(string Source, string Target)>>? _factory;

    private ImmutableList<(string Source, string Target)> Values
    {
        get
        {
#pragma warning disable CA1508 // ダブルチェック ロッキング（lock の内側で再確認する）を CA1508 は「常に true」と誤検出する
            if (_pairs is null)
            {
                lock (_lock)
                {
                    if (_pairs is null)
                    {
                        _pairs = [.. _factory!()];
                        _factory = null;
                    }
                }
            }
#pragma warning restore CA1508
            return _pairs;
        }
    }
    private readonly System.Threading.Lock _lock = new();

    /// <summary>即時初期化用コンストラクタ</summary>
    public ConvertPairs(IEnumerable<(string Source, string Target)> pairs)
        => _pairs = [.. pairs];

    /// <summary>遅延初期化用コンストラクタ</summary>
    private ConvertPairs(Func<IEnumerable<(string Source, string Target)>> factory)
        => _factory = factory;

    /// <summary>エントリから <b>正順変換</b> の <see cref="ConvertPairs"/> を生成します。</summary>
    public static ConvertPairs FromForward(EntryRecord entry)
        => new([(entry.Source, entry.Target)]);

    /// <summary>エントリから <b>逆順変換</b> の <see cref="ConvertPairs"/> を生成します。</summary>
    public static ConvertPairs FromInverse(EntryRecord entry)
        => new([(entry.Target, entry.Source)]);

    /// <summary>キーで識別される <see cref="ConvertPairs"/> を取得します。</summary>
    public static ConvertPairs FromFunc(string key, Func<IEnumerable<(string Source, string Target)>> func)
        => s_cachePairsFuncDictionary.GetOrAdd(key, _ => new ConvertPairs(func));
    private static readonly ConcurrentDictionary<string, ConvertPairs> s_cachePairsFuncDictionary = [];

    /// <summary>キーで識別される正順変換の <see cref="ConvertPairs"/> を取得します。</summary>
    public static ConvertPairs FromForward(string key)
        => s_cachePairsForwardDictionary.GetOrAdd(key,
            key => new ConvertPairs(() => EntryRecord.GetEntryList(key).Select(s => (s.Source, s.Target))));
    private static readonly ConcurrentDictionary<string, ConvertPairs> s_cachePairsForwardDictionary = [];

    /// <summary>キーで識別される逆順変換の <see cref="ConvertPairs"/> を取得します。</summary>
    public static ConvertPairs FromInverse(string key)
        => s_cachePairsInverseDictionary.GetOrAdd(key,
            key => new ConvertPairs(() => EntryRecord.GetEntryList(key).Select(s => (s.Target, s.Source))));
    private static readonly ConcurrentDictionary<string, ConvertPairs> s_cachePairsInverseDictionary = [];
}



/// <summary>変換のペア集合を保持し列挙可能な形式で提供します。</summary>
public partial record ConvertPairs : IEnumerable<(string Source, string Target)>
{
    /// <summary>列挙子を取得します。</summary>
    /// <returns>ペア集合の列挙子</returns>
    public IEnumerator<(string Source, string Target)> GetEnumerator() => Values.GetEnumerator();

    /// <summary>列挙子を取得します。</summary>
    /// <returns>ペア集合の列挙子</returns>
    IEnumerator IEnumerable.GetEnumerator() => Values.GetEnumerator();


    /// <summary>空の変換リストを取得します。</summary>
    /// <remarks>初回アクセス時に生成して共有します（バッキング フィールドを持たない遅延初期化）。<br/></remarks>
    public static ConvertPairs Empty
    {
        get => field ??= new([]);
    }


    /// <summary>複数のペア集合を平坦化して初期化します。</summary>
    /// <param name="pairs">統合するペア集合の配列</param>
    /// <remarks>引数で受け取った複数の列挙を SelectMany でフラット化します。<br/>
    /// 遅延初期化コンストラクタを使用し、最初に値が必要になったタイミングで実体化されます。</remarks>
    public static ConvertPairs Concat(params IEnumerable<(string Source, string Target)>[] pairs)
        => new(() => pairs.SelectMany(x => x));


    /// <summary>２つの <see cref="ConvertPairs"/> を結合した新しい <see cref="ConvertPairs"/> を生成します。</summary>
    /// <param name="left">一方のペア集合</param>
    /// <param name="right">もう一方のペア集合</param>
    /// <returns>結合されたペア集合</returns>
    public static ConvertPairs operator +(ConvertPairs left, ConvertPairs right)
        => Add(left, right);

    /// <summary>２つの <see cref="ConvertPairs"/> を結合した新しい <see cref="ConvertPairs"/> を生成します（<c>+</c> 演算子の代替）。</summary>
    /// <param name="left">一方のペア集合</param>
    /// <param name="right">もう一方のペア集合</param>
    /// <returns>結合されたペア集合</returns>
    /// <remarks><see cref="Concat"/> の <c>params</c> 版は呼び出しごとに配列を作るため、2 つを結合する場合は本メソッドを使います。</remarks>
    public static ConvertPairs Add(ConvertPairs left, ConvertPairs right)
        => new(() => left.Values.Concat(right.Values));


    /// <summary>２つの <see cref="ConvertPairs"/> を連鎖変換させる <see cref="ConvertPairs"/> を生成します。</summary>
    /// <param name="second">連鎖させる <see cref="ConvertPairs"/></param>
    /// <param name="includeUnmatchedFirst">first (自身) の Target と一致する second の Source がない場合でも first の要素を保持するか。</param>
    /// <param name="includeUnmatchedSecond">second の Source が 自身の変換先と Target がマッチしない場合でも second の要素を保持するか。</param>
    /// <returns>連鎖した <see cref="ConvertPairs"/></returns>
    /// <remarks>first の Target と second の Source をキーに連結し、必要に応じてマッチしない要素を残します。</remarks>
    public ConvertPairs Chain(ConvertPairs second, bool includeUnmatchedFirst, bool includeUnmatchedSecond)
    {
        // 第2段の Source ごとに Target 群をまとめた辞書を構築し、後段のルックアップを O(1) にする
        // （GroupBy + ToDictionary は中間のグループと遅延列挙を作るため、1 回の走査で組み立てる）
        var secondValues = second.Values;
        var lookup = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (source, target) in secondValues)
        {
            if (!lookup.TryGetValue(source, out var targets))
            {
                targets = [];
                lookup[source] = targets;
            }

            targets.Add(target);
        }

        // 連鎖先が存在する場合は Source を維持したまま Target を置き換える。存在しない場合は includeUnmatchedFirst に応じて保持または除外。
        var firstValues = Values;
        var chained = new List<(string Source, string Target)>(firstValues.Count);
        foreach (var entry in firstValues)
        {
            if (lookup.TryGetValue(entry.Target, out var targets))
            {
                foreach (var to in targets)
                    chained.Add((entry.Source, to));
            }
            else if (includeUnmatchedFirst)
            {
                chained.Add(entry);
            }
        }

        if (!includeUnmatchedSecond)
            return new ConvertPairs(chained);

        // first に登場する Target をセット化し、second のみが持つエントリを抽出して結合する。
        var firstTargets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, target) in firstValues)
            firstTargets.Add(target);

        foreach (var entry in secondValues)
        {
            if (!firstTargets.Contains(entry.Source))
                chained.Add(entry);
        }

        return new ConvertPairs(chained);
    }

    /// <summary>２つの <see cref="ConvertPairs"/> を連鎖変換させる <see cref="ConvertPairs"/> を生成します。</summary>
    /// <remarks>重ならない要素は切り捨てます。<br/>マッチしない要素は保持しないため、結果は共通部分のみになります。</remarks>
    /// <param name="second">連鎖させる <see cref="ConvertPairs"/></param>
    /// <returns>連鎖した <see cref="ConvertPairs"/></returns>
    public ConvertPairs Chain(ConvertPairs second)
        => Chain(second, includeUnmatchedFirst: false, includeUnmatchedSecond: false);

    /// <summary>２つの <see cref="ConvertPairs"/> を連鎖変換させる <see cref="ConvertPairs"/> を生成します。</summary>
    /// <remarks>重ならない要素も結果に含めます。<br/>両方の未マッチ要素を残すため、Union 的な結果を得たい場合に利用します。</remarks>
    /// <param name="second">連鎖させる <see cref="ConvertPairs"/></param>
    /// <returns>連鎖した <see cref="ConvertPairs"/></returns>
    public ConvertPairs ChainMerge(ConvertPairs second)
        => Chain(second, includeUnmatchedFirst: true, includeUnmatchedSecond: true);


    /// <summary>登録された変換表に従い文字列を置換します。</summary>
    /// <param name="text">置換対象の文字列</param>
    /// <returns>置換後の文字列</returns>
    /// <remarks>変換の手順（文字の走査・正規表現）はインスタンス内にキャッシュし、２回目以降の呼び出しで作り直しません。</remarks>
    public string Convert(string text)
    {
        // 早期リターン
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        // 遅延初期化された変換手順を取得する（変換元が空の場合は生成されない）
        return Compiled?.Apply(text) ?? text;
    }

    /// <summary>変換の手順を保持します。</summary>
    /// <returns>変換の手順。 変換元が 1 件もない場合は null</returns>
    /// <remarks>
    /// C# 14 の <c>field</c> キーワードにより、バッキング フィールドを宣言せずに遅延初期化します。<br/>
    /// 初回アクセス時のみ生成し、以降は同じインスタンスを返して再生成を避けます。<br/>
    /// 生成できない場合は null を返し、次回呼び出しで再試行されます。<br/>
    /// </remarks>
    private ConvertPlan? Compiled
    {
        get
        {
#pragma warning disable CA1508 // ダブルチェック ロッキング（lock の内側で再確認する）を CA1508 は「常に true」と誤検出する
            // ダブルチェックロッキングによる遅延初期化
            if (field == null)
            {
                lock (_lock)
                {
                    // ロック待ちの間に他スレッドが生成している可能性があるため再確認する
                    if (field == null)
                    {
                        field = ConvertPlan.Create(Values);
                    }
                }
            }

            return field;
#pragma warning restore CA1508
        }
    }

    /// <summary>変換の手順（1 文字ずつの対応表、または正規表現）を表します。</summary>
    /// <remarks>
    /// <b>変換元がすべて 1 文字なら文字の対応表で変換し、正規表現を作らない</b>。<br/>
    /// 2 文字以上の変換元（かなと濁点の合成など）を含む場合は、長い並びを優先する必要があるため
    /// 正規表現で照合する。
    /// </remarks>
    private sealed class ConvertPlan
    {
        /// <summary>1 文字ずつの対応表（正規表現を使わない場合にだけ作る）。</summary>
        private readonly Dictionary<char, string>? _chars;

        /// <summary>正規表現（1 文字ずつの対応に落とせない場合にだけ作る）。</summary>
        private readonly Regex? _regex;

        /// <summary>正規表現の置換の評価器。</summary>
        private readonly MatchEvaluator? _evaluator;

        /// <summary>変換の手順を生成します。</summary>
        /// <param name="chars">1 文字ずつの対応表。</param>
        /// <param name="regex">正規表現。</param>
        /// <param name="evaluator">正規表現の置換の評価器。</param>
        private ConvertPlan(Dictionary<char, string>? chars, Regex? regex, MatchEvaluator? evaluator)
        {
            _chars = chars;
            _regex = regex;
            _evaluator = evaluator;
        }

        /// <summary>変換元から変換の手順を生成します。</summary>
        /// <param name="pairs">変換元と変換先の組。</param>
        /// <returns>変換の手順。 変換元が 1 件もない場合は null。</returns>
        public static ConvertPlan? Create(ImmutableList<(string Source, string Target)> pairs)
        {
            // 変換元がすべて 1 文字なら、正規表現を作らずに対応表だけで変換できる
            var singleChars = true;
            foreach (var (source, _) in pairs)
            {
                if (!string.IsNullOrEmpty(source) && source.Length != 1)
                {
                    singleChars = false;
                    break;
                }
            }

            if (singleChars)
            {
                var chars = new Dictionary<char, string>(pairs.Count);
                foreach (var (source, target) in pairs)
                {
                    if (string.IsNullOrEmpty(source))
                        continue;

                    // 同一キーが複数ある場合は最初に登場した組を優先する
                    chars.TryAdd(source[0], target ?? string.Empty);
                }

                return chars.Count == 0 ? null : new ConvertPlan(chars, null, null);
            }

            var regex = CreateConvertRegex(pairs);
            if (regex == null)
            {
                return null;
            }

            var map = CreateConvertMap(pairs);
            return new ConvertPlan(null, regex, m => map.TryGetValue(m.Value, out var replacement) ? replacement : m.Value);
        }

        /// <summary>変換を適用します。</summary>
        /// <param name="text">変換対象の文字列。</param>
        /// <returns>変換後の文字列。</returns>
        public string Apply(string text)
            => _chars is not null ? ApplyChars(text, _chars) : _regex!.Replace(text, _evaluator!);

        /// <summary>1 文字ずつの対応表で変換します。</summary>
        /// <param name="text">変換対象の文字列。</param>
        /// <param name="chars">1 文字ずつの対応表。</param>
        /// <returns>変換後の文字列。 変換する文字が無い場合は渡された文字列をそのまま返す。</returns>
        /// <remarks>変換する文字が 1 つも無い場合に文字列を作り直さないよう、最初に走査して有無を確かめます。</remarks>
        private static string ApplyChars(string text, Dictionary<char, string> chars)
        {
            var first = -1;
            for (var i = 0; i < text.Length; i++)
            {
                if (chars.ContainsKey(text[i]))
                {
                    first = i;
                    break;
                }
            }

            if (first < 0)
            {
                return text;
            }

            var builder = new StringBuilder(text.Length + 16);
            builder.Append(text, 0, first);
            for (var i = first; i < text.Length; i++)
            {
                var c = text[i];
                if (chars.TryGetValue(c, out var replacement))
                {
                    builder.Append(replacement);
                }
                else
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }
    }

    /// <summary>変換対象文字列を網羅する正規表現を生成します。</summary>
    /// <returns>生成した正規表現</returns>
    /// <remarks>空 Source を除外し、重複を排除した上で "|" 連結したパターンをコンパイルします。</remarks>
    private static Regex? CreateConvertRegex(IReadOnlyCollection<(string Source, string Target)> pairs)
    {
        var uniques = new HashSet<string>(StringComparer.Ordinal);
        var builder = new StringBuilder();
        foreach (var (source, _) in pairs)
        {
            if (string.IsNullOrEmpty(source) || !uniques.Add(source))
                continue;

            if (builder.Length > 0)
                builder.Append('|');

            builder.Append(Regex.Escape(source));
        }

        if (builder.Length == 0)
            return null;

        var pattern = builder.ToString();
        return new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    }

    /// <summary>変換元から変換先へのマップを生成します。</summary>
    /// <returns>変換元から変換先への辞書</returns>
    /// <remarks>同一キーが複数ある場合は最初に登場したペアを優先し、変換先が null の場合は空文字列にフォールバックします。</remarks>
    private static Dictionary<string, string> CreateConvertMap(IReadOnlyCollection<(string Source, string Target)> pairs)
    {
        var map = new Dictionary<string, string>(pairs.Count, StringComparer.Ordinal);
        foreach (var (source, target) in pairs)
        {
            if (string.IsNullOrEmpty(source))
                continue;

            // 既存キーがある場合は上書きせず、初出のマッピングを維持する。
            map.TryAdd(source, target ?? string.Empty);
        }
        return map;
    }

}
