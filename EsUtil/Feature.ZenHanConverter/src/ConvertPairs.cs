using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;

namespace EsUtil.Feature.Text;


/// <summary>変換のペア集合を保持し列挙可能な形式で提供します。</summary>
public partial record ConvertPairs : IEnumerable<(string Source, string Target)>
{
    private ImmutableList<(string Source, string Target)>? _pairs;
    private Func<IEnumerable<(string Source, string Target)>>? _factory;

    private IEnumerable<(string Source, string Target)> Values
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
        => Concat(left, right);

    /// <summary>２つの <see cref="ConvertPairs"/> を結合した新しい <see cref="ConvertPairs"/> を生成します（<c>+</c> 演算子の代替）。</summary>
    /// <param name="left">一方のペア集合</param>
    /// <param name="right">もう一方のペア集合</param>
    /// <returns>結合されたペア集合</returns>
    public static ConvertPairs Add(ConvertPairs left, ConvertPairs right) => left + right;


    /// <summary>２つの <see cref="ConvertPairs"/> を連鎖変換させる <see cref="ConvertPairs"/> を生成します。</summary>
    /// <param name="second">連鎖させる <see cref="ConvertPairs"/></param>
    /// <param name="includeUnmatchedFirst">first (自身) の Target と一致する second の Source がない場合でも first の要素を保持するか。</param>
    /// <param name="includeUnmatchedSecond">second の Source が 自身の変換先と Target がマッチしない場合でも second の要素を保持するか。</param>
    /// <returns>連鎖した <see cref="ConvertPairs"/></returns>
    /// <remarks>first の Target と second の Source をキーに連結し、必要に応じてマッチしない要素を残します。</remarks>
    public ConvertPairs Chain(ConvertPairs second, bool includeUnmatchedFirst, bool includeUnmatchedSecond)
    {
        // 第2段の Source ごとに Target 群をまとめた辞書を構築し、後段のルックアップを O(1) にする。
        var lookup = second.Values
            .GroupBy(e => e.Source, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Target), StringComparer.Ordinal);

        // 連鎖先が存在する場合は Source を維持したまま Target を置き換える。存在しない場合は includeUnmatchedFirst に応じて保持または除外。
        var chained = new List<(string Source, string Target)>();
        foreach (var entry in Values)
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
        var firstTargets = new HashSet<string>(Values.Select(e => e.Target), StringComparer.Ordinal);
        foreach (var entry in second.Values)
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
    /// <remarks>正規表現と変換マップをインスタンス内にキャッシュし、２回目以降の呼び出しを高速化します。</remarks>
    public string Convert(string text)
    {
        // 早期リターン
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        // 遅延初期化された変換キャッシュを取得する（変換元が空の場合は生成されない）
        var compiled = Compiled;
        if (compiled == null)
        {
            return text;
        }

        var (regexRef, mapRef) = compiled.Value;
        return regexRef.Replace(text, m =>
        {
            var value = m.Value;
            return mapRef.TryGetValue(value, out var replacement)
                ? replacement
                : value;
        });
    }

    /// <summary>変換に使用する正規表現と変換マップを保持します。</summary>
    /// <returns>コンパイル済みの正規表現と変換マップ。変換元が 1 件もない場合は null</returns>
    /// <remarks>
    /// C# 14 の <c>field</c> キーワードにより、バッキング フィールドを宣言せずに遅延初期化します。<br/>
    /// 初回アクセス時のみ生成し、以降は同じインスタンスを返して再生成を避けます。<br/>
    /// 生成できない場合は null を返し、次回呼び出しで再試行されます。<br/>
    /// </remarks>
    private (Regex Regex, Dictionary<string, string> Map)? Compiled
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
                        var values = Values;
                        var regex = CreateConvertRegex(values);
                        if (regex == null)
                        {
                            return null;
                        }

                        field = (regex, CreateConvertMap(values));
                    }
                }
            }

            return field;
#pragma warning restore CA1508
        }
    }

    /// <summary>変換対象文字列を網羅する正規表現を生成します。</summary>
    /// <returns>生成した正規表現</returns>
    /// <remarks>空 Source を除外し、重複を排除した上で "|" 連結したパターンをコンパイルします。</remarks>
    private static Regex? CreateConvertRegex(IEnumerable<(string Source, string Target)> pairs)
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
    private static Dictionary<string, string> CreateConvertMap(IEnumerable<(string Source, string Target)> pairs)
    {
        // 事前に要素数がわかる場合は capacity を指定してリサイズを抑止
        var initialCapacity = pairs is IReadOnlyCollection<(string Source, string Target)> col ? col.Count : 0;
        var map = new Dictionary<string, string>(initialCapacity, StringComparer.Ordinal);
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
