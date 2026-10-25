using System.Collections.Concurrent;
using System.Collections.Immutable;

using static EsUtil.Feature.ZenHan.Define;
using static EsUtil.Feature.ZenHan.Helper;

namespace EsUtil.Feature.ZenHan;


/// <summary>変換エントリ情報を保持し Unicode デコード済みのレコードとして提供します。</summary>
/// <remarks>
/// 変換カテゴリや入出力の種別、名称、概要などの情報を保持します。<br/>
/// コンストラクタで Unicode 表記 (U+XXXX) を即時デコードして利用側の負担を削減します。<br/>
/// </remarks>
public partial record EntryRecord
{
    /// <summary>変換対象のカテゴリ (例: Ascii, Kana) を表します。</summary>
    public string Category { get; }
    /// <summary>変換文字のグループ (例: Numeric, Alphabet, Symbol) を表します。</summary>
    public string Group { get; }
    /// <summary>変換文字のサブグループ (例: Large, Small, Zen, Han) を表します。</summary>
    public string SubGroup { get; }
    /// <summary>正変換 (例: ToHan, ToLower) の識別子を表します。</summary>
    public string Forward { get; }
    /// <summary>逆変換 (例: ToZen, ToUpper) の識別子を表します。</summary>
    public string Inverse { get; }
    /// <summary>変換前の文字列を表します。</summary>
    public string Source { get; }
    /// <summary>変換後の文字列を表します。</summary>
    public string Target { get; }
    /// <summary>列挙名として扱う識別子を表します。</summary>
    public string Name { get; }
    /// <summary>ドキュメントや UI に表示する説明を表します。</summary>
    public string Summary { get; }


    /// <summary>エントリを初期化し Unicode 表記を実体文字へデコードします。</summary>
    /// <remarks>
    /// 入力が null の場合は空文字列にフォールバックし、以降の置換処理での NullReferenceException を防ぎます。<br/>
    /// DecodeUnicodeNotation の結果をキャッシュし、同一入力の再デコードを避けてパフォーマンスを高めます。<br/>
    /// </remarks>
    public EntryRecord(string Category, string Group, string SubGroup, string Forward, string Inverse, string Source, string Target, string Name, string Summary)
    {
        // null を許容しつつ後段処理で必ず空文字列以上の値にすることで、置換処理や辞書アクセス時の安全性を確保する。
        this.Category = Category ?? string.Empty;
        this.Group = Group ?? string.Empty;
        this.SubGroup = SubGroup ?? string.Empty;
        this.Forward = Forward ?? string.Empty;
        this.Inverse = Inverse ?? string.Empty;
        this.Source = DecodeUnicodeNotation(Source ?? string.Empty);
        this.Target = DecodeUnicodeNotation(Target ?? string.Empty);
        this.Name = Name ?? string.Empty;
        this.Summary = Summary ?? string.Empty;
    }


    /// <summary>タプル入力を受け取りレコードを初期化します。</summary>
    /// <param name="t">カテゴリや名称などの要素を含むタプル。</param>
    public EntryRecord((string Category, string Group, string SubGroup, string Forward, string Inverse, string Source, string Target, string Name, string Summary) t)
        : this(t.Category, t.Group, t.SubGroup, t.Forward, t.Inverse, t.Source, t.Target, t.Name, t.Summary) { }


    /// <summary>タプルから <see cref="EntryRecord"/> への暗黙的変換を提供します。</summary>
    /// <param name="t">カテゴリや名称などの要素を含むタプル</param>
    /// <returns>タプルから生成されたエントリ</returns>
    public static implicit operator EntryRecord((string Category, string Group, string SubGroup, string Forward, string Inverse, string Source, string Target, string Name, string Summary) t)
        => new(t);

    /// <summary>タプルから <see cref="EntryRecord"/> を生成します（暗黙変換の代替）。</summary>
    /// <param name="t">カテゴリや名称などの要素を含むタプル</param>
    /// <returns>タプルから生成されたエントリ</returns>
    public static EntryRecord ToEntryRecord((string Category, string Group, string SubGroup, string Forward, string Inverse, string Source, string Target, string Name, string Summary) t)
        => new(t);


    /// <summary>デコンストラクタとして (Source, Target) のみを抽出します。</summary>
    /// <param name="Source">変換前の値</param>
    /// <param name="Target">変換後の値</param>
    /// <remarks>
    /// 最小限の情報だけを取り出す用途で利用し、不要なプロパティ読み出しを避けます。<br/>
    /// </remarks>
    public void Deconstruct(out string Source, out string Target)
    {
        Source = this.Source;
        Target = this.Target;
    }


    /// <summary>完全なデコンストラクタとしてすべてのプロパティを分解します。</summary>
    /// <param name="Category">カテゴリ</param>
    /// <param name="Group">グループの識別子</param>
    /// <param name="SubGroup">グループの識別子</param>
    /// <param name="Forward">正変換の識別子</param>
    /// <param name="Inverse">逆変換の識別子</param>
    /// <param name="Source">変換前の値</param>
    /// <param name="Target">変換後の値</param>
    /// <param name="Name">列挙名の識別子</param>
    /// <param name="Summary">説明文</param>
    /// <remarks>
    /// 全プロパティをタプル分解で取得する際に利用し、型安全に値を渡します。<br/>
    /// </remarks>
    public void Deconstruct(out string Category, out string Group, out string SubGroup, out string Forward, out string Inverse, out string Source, out string Target, out string Name, out string Summary)
    {
        Category = this.Category;
        Group = this.Group;
        SubGroup = this.SubGroup;
        Forward = this.Forward;
        Inverse = this.Inverse;
        Source = this.Source;
        Target = this.Target;
        Name = this.Name;
        Summary = this.Summary;
    }
}


public partial record EntryRecord
{
    /// <summary>カテゴリ/種別/名称を指定してエントリのリストを取得します。</summary>
    /// <param name="entryKey">取得する値のキー</param>
    /// <returns>指定されたキーに一致するエントリを含む不変リスト</returns>
    public static ImmutableList<EntryRecord> GetEntryList(string entryKey)
    {
        // キャッシュに存在する場合はそのまま返す。頻出クエリの再計算を避け、低コストで返却する。
        if (s_cacheEntryListMap.TryGetValue(entryKey, out var value))
            return value;

        // キーを分解し、階層的にフィルタリングするための配列を取得する。
        var keys = entryKey.Split("|");

        // 階層ごとのキー組成に応じてフィルタリングを重ね、結果をキャッシュへ格納する。
        // ZenHanConverter|{forward}|{category}|{group}|{subGroup}|{name}
        return keys.Length switch
        {
            // 全件取得: 事前生成済み AllList をそのまま複製してキャッシュする。
            1 when (string.Equals(keys[0], "ZenHanConverter", StringComparison.Ordinal)) => s_cacheEntryListMap.GetOrAdd(entryKey, [.. AllList]),
            // 正変換指定で絞り込み。
            2 => s_cacheEntryListMap.GetOrAdd(entryKey, [.. GetEntryList($"{keys[0]}").Where(e => string.Equals(e.Forward, keys[1], StringComparison.Ordinal))]),
            // カテゴリ指定でさらに絞り込み。
            3 => s_cacheEntryListMap.GetOrAdd(entryKey, [.. GetEntryList($"{keys[0]}|{keys[1]}").Where(e => string.Equals(e.Category, keys[2], StringComparison.Ordinal))]),
            // グループ指定でさらに絞り込み。
            4 => s_cacheEntryListMap.GetOrAdd(entryKey, [.. GetEntryList($"{keys[0]}|{keys[1]}|{keys[2]}").Where(e => string.Equals(e.Group, keys[3], StringComparison.Ordinal))]),
            // サブグループ指定で絞り込み。
            5 => s_cacheEntryListMap.GetOrAdd(entryKey, [.. GetEntryList($"{keys[0]}|{keys[1]}|{keys[2]}|{keys[3]}").Where(e => string.Equals(e.SubGroup, keys[4], StringComparison.Ordinal))]),
            // 名称指定で 1 件に確定させる。
            6 when (string.Equals(keys[2], "", StringComparison.Ordinal) && string.Equals(keys[3], "", StringComparison.Ordinal) && string.Equals(keys[4], "", StringComparison.Ordinal)) => s_cacheEntryListMap.GetOrAdd(entryKey, [.. GetEntryList($"{keys[0]}|{keys[1]}").Where(e => string.Equals(e.Name, keys[5], StringComparison.Ordinal))]),
            // 名称指定で 1 件に確定させる。
            6 when (string.Equals(keys[3], "", StringComparison.Ordinal) && string.Equals(keys[4], "", StringComparison.Ordinal)) => s_cacheEntryListMap.GetOrAdd(entryKey, [.. GetEntryList($"{keys[0]}|{keys[1]}|{keys[2]}").Where(e => string.Equals(e.Name, keys[5], StringComparison.Ordinal))]),
            // 名称指定で 1 件に確定させる。
            6 when (string.Equals(keys[4], "", StringComparison.Ordinal)) => s_cacheEntryListMap.GetOrAdd(entryKey, [.. GetEntryList($"{keys[0]}|{keys[1]}|{keys[2]}|{keys[3]}").Where(e => string.Equals(e.Name, keys[5], StringComparison.Ordinal))]),
            // 名称指定で 1 件に確定させる。
            6 => s_cacheEntryListMap.GetOrAdd(entryKey, [.. GetEntryList($"{keys[0]}|{keys[1]}|{keys[2]}|{keys[3]}|{keys[4]}").Where(e => string.Equals(e.Name, keys[5], StringComparison.Ordinal))]),
            _ => throw new KeyNotFoundException($"Invalid entry key format: {entryKey}"),
        };
    }
    private static readonly ConcurrentDictionary<string, ImmutableList<EntryRecord>> s_cacheEntryListMap = [];

    /// <summary>カテゴリ/種別/名称を指定してエントリを 1 件取得します。</summary>
    /// <param name="entryKey">取得する値のキー</param>
    /// <returns>指定されたキーに一致するエントリ</returns>
    public static EntryRecord GetEntry(string entryKey)
        => GetEntryList(entryKey)[0];


    /// <summary>カテゴリを指定してエントリキーを取得します。</summary>
    /// <param name="forward">正変換</param>
    /// <returns>エントリキー</returns>
    public static string GetEntryKey(string forward)
        => $"ZenHanConverter|{forward}";

    /// <summary>カテゴリを指定してエントリキーを取得します。</summary>
    /// <param name="forward">正変換</param>
    /// <param name="category">カテゴリ</param>
    /// <returns>エントリキー</returns>
    public static string GetEntryKey(string forward, string category)
        => $"ZenHanConverter|{forward}|{category}";

    /// <summary>カテゴリ/変換種別/グループを指定してエントリキーを取得します。</summary>
    /// <param name="forward">正変換</param>
    /// <param name="category">カテゴリ</param>
    /// <param name="group">グループ</param>
    /// <returns>エントリキー</returns>
    public static string GetEntryKey(string forward, string category, string group)
        => $"ZenHanConverter|{forward}|{category}|{group}";

    /// <summary>カテゴリ/変換種別/グループ/名称を指定してエントリキーを取得します。</summary>
    /// <param name="forward">正変換</param>
    /// <param name="category">カテゴリ</param>
    /// <param name="group">グループ</param>
    /// <param name="subGroup">サブグループ</param>
    /// <returns>エントリキー</returns>
    public static string GetEntryKey(string forward, string category, string group, string subGroup)
        => $"ZenHanConverter|{forward}|{category}|{group}|{subGroup}";

    /// <summary>カテゴリ/変換種別/グループ/名称を指定してエントリキーを取得します。</summary>
    /// <param name="forward">正変換の</param>
    /// <param name="category">カテゴリ</param>
    /// <param name="group">グループ</param>
    /// <param name="subGroup">サブグループ</param>
    /// <param name="name">取得するエントリの列挙名</param>
    /// <returns>エントリキー</returns>
    public static string GetEntryKey(string forward, string category, string group, string subGroup, string name)
        => $"ZenHanConverter|{forward}|{category}|{group}|{subGroup}|{name}";

}
