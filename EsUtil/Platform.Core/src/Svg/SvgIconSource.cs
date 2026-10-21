using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Xml.Linq;

namespace EsUtil.Platform.Core.Svg;

/// <summary>SVG アイコンの読み込み元（埋め込みリソース・フォルダー・テキスト）を表し、解析結果を共有する。</summary>
/// <remarks>
/// <b>読み込み元を差し替えられる理由</b>: アイコンは「アプリへ埋め込む」「テーマごとにフォルダーから読む」
/// 「テストのために文字列で与える」の 3 通りがあり、<b>解析は 1 か所</b>にしたい（読み込み元ごとに解析を写すと、
/// 片方だけ直したときに同じ SVG が違う形になる）。<br/>
/// <b>解析は <see cref="XDocument"/> を使う</b>: 正規表現で属性を拾うと、要素の入れ子・属性の順序・
/// 実体参照の扱いを自前で持つことになる（XML の解釈は .NET 標準に任せる）。<br/>
/// <b>失敗しても止めない</b>: リソース欠落・解析失敗の場合は空の <see cref="SvgIcon"/> を返す（UI を壊さない）。
/// 例外が要る場合は <see cref="TryLoad"/> を使う。<br/>
/// <b>パス データの解釈は <see cref="SvgPathParser"/> が担う</b>（本クラスは SVG の要素から <c>d</c> 属性を取り出すだけ）。
/// </remarks>
public sealed class SvgIconSource
{
    /// <summary>キーから SVG のテキストを読む処理（読めない場合は null）。</summary>
    private readonly Func<string, string?> _read;

    /// <summary>キーの一覧を返す処理。</summary>
    private readonly Func<IReadOnlyList<string>> _enumerate;

    /// <summary>解析結果のキャッシュ（アイコンは表示のたびに参照されるため）。</summary>
    /// <remarks>件数は読み込み元が持つアイコンの数で決まるため、容量の上限は設けない。</remarks>
    private readonly ConcurrentDictionary<string, SvgIcon> _cache = new(StringComparer.Ordinal);

    /// <summary>読み込み元を指定して生成する。</summary>
    /// <param name="read">キーから SVG のテキストを読む処理。</param>
    /// <param name="enumerate">キーの一覧を返す処理。</param>
    /// <exception cref="ArgumentNullException">引数が null の場合。</exception>
    private SvgIconSource(Func<string, string?> read, Func<IReadOnlyList<string>> enumerate)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(enumerate);

        _read = read;
        _enumerate = enumerate;
    }

    /// <summary>アセンブリの埋め込みリソースから読み込む。</summary>
    /// <param name="assembly">リソースを持つアセンブリ。</param>
    /// <param name="namePrefix">リソース名の前置き（<c>名前空間.</c> の形。 例: <c>MyApp.Icons.</c>）。</param>
    /// <returns>読み込み元。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="assembly"/> または <paramref name="namePrefix"/> が null の場合。</exception>
    /// <remarks>キーは前置きを除いた残り（例: <c>ic_fluent_save_24_regular.svg</c>）である。</remarks>
    public static SvgIconSource FromEmbeddedResource(Assembly assembly, string namePrefix)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(namePrefix);

        return new SvgIconSource(
            key =>
            {
                using var stream = assembly.GetManifestResourceStream(namePrefix + key);
                if (stream is null)
                {
                    return null;
                }

                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            },
            () =>
            [
                .. assembly.GetManifestResourceNames()
                    .Where(name => name.StartsWith(namePrefix, StringComparison.Ordinal))
                    .Select(name => name[namePrefix.Length..]),
            ]);
    }

    /// <summary>フォルダーから読み込む。</summary>
    /// <param name="directory">SVG ファイルのあるフォルダー。</param>
    /// <returns>読み込み元。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="directory"/> が null の場合。</exception>
    /// <remarks>
    /// キーはファイル名（拡張子を含む）である。 フォルダーは<b>読み込み時に走査する</b>
    /// （アプリの起動後にファイルが増減しても一覧へ反映される）。
    /// </remarks>
    public static SvgIconSource FromDirectory(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);

        return new SvgIconSource(
            key => File.Exists(Path.Combine(directory, key)) ? File.ReadAllText(Path.Combine(directory, key)) : null,
            () => Directory.Exists(directory)
                ? [.. Directory.EnumerateFiles(directory, "*.svg").Select(Path.GetFileName).OfType<string>()]
                : []);
    }

    /// <summary>SVG のテキストから読み込む（1 件だけ）。</summary>
    /// <param name="svg">SVG のテキスト。</param>
    /// <returns>読み込み元。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="svg"/> が null の場合。</exception>
    /// <remarks>キーは無視される（<see cref="Load"/> の引数に任意の値を渡してよい）。</remarks>
    public static SvgIconSource FromText(string svg)
    {
        ArgumentNullException.ThrowIfNull(svg);

        return new SvgIconSource(key => svg, () => [string.Empty]);
    }

    /// <summary>アイコンを読み込む。</summary>
    /// <param name="key">アイコンのキー（読み込み元ごとの名前）。</param>
    /// <returns>アイコンの内容。 読み込めない場合は空のアイコン。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> が null の場合。</exception>
    public SvgIcon Load(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _cache.GetOrAdd(key, ParseKey);
    }

    /// <summary>アイコンを読み込む（成否を返す）。</summary>
    /// <param name="key">アイコンのキー。</param>
    /// <param name="icon">読み込んだアイコンの内容。</param>
    /// <returns>読み込めた場合は true。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> が null の場合。</exception>
    public bool TryLoad(string key, out SvgIcon icon)
    {
        icon = Load(key);
        return icon.PathData.Count > 0;
    }

    /// <summary>キーの一覧を取得する。</summary>
    /// <returns>キーの一覧（並びは不定）。</returns>
    public IReadOnlyList<string> EnumerateKeys() => _enumerate();

    /// <summary>キーからアイコンを 1 件読み込む（キャッシュの外側）。</summary>
    /// <param name="key">アイコンのキー。</param>
    /// <returns>アイコンの内容。 読み込めない場合は空のアイコン。</returns>
    private SvgIcon ParseKey(string key)
    {
        string? svg;
        try
        {
            svg = _read(key);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // アクセス拒否・削除競合など、読み込み元の状態による失敗は「アイコンが無い」として扱う
            return Empty(key);
        }

        return svg is null ? Empty(key) : Parse(key, svg);
    }

    /// <summary>SVG のテキストを解析する。</summary>
    /// <param name="key">アイコンのキー。</param>
    /// <param name="svg">SVG のテキスト。</param>
    /// <returns>アイコンの内容。 解析できない場合は空のアイコン。</returns>
    private static SvgIcon Parse(string key, string svg)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(svg);
        }
        catch (System.Xml.XmlException)
        {
            // 壊れた SVG は空で代替する（描画は何も現れない）
            return Empty(key);
        }

        var root = document.Root;
        if (root is null)
        {
            return Empty(key);
        }

        var paths = new List<string>();
        foreach (var element in root.DescendantsAndSelf())
        {
            if (!string.Equals(element.Name.LocalName, "path", StringComparison.Ordinal))
            {
                continue;
            }

            var data = element.Attribute("d")?.Value;
            if (!string.IsNullOrWhiteSpace(data))
            {
                paths.Add(data);
            }
        }

        if (paths.Count == 0)
        {
            return Empty(key);
        }

        var (width, height) = ParseViewBox(root);
        return new SvgIcon(key, width, height, paths, ParseFill(root));
    }

    /// <summary>viewBox（無ければ width / height）から論理的な大きさを求める。</summary>
    /// <param name="root">SVG のルート要素。</param>
    /// <returns>幅と高さ（数値でない場合は 24）。</returns>
    private static (double Width, double Height) ParseViewBox(XElement root)
    {
        if (root.Attribute("viewBox")?.Value is string viewBox)
        {
            var parts = viewBox.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4
                && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var w)
                && double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var h)
                && w > 0 && h > 0)
            {
                return (w, h);
            }
        }

        return (ParseLength(root, "width"), ParseLength(root, "height"));
    }

    /// <summary>長さの属性（<c>width</c> / <c>height</c>）を数値として読む。</summary>
    /// <param name="root">SVG のルート要素。</param>
    /// <param name="name">属性の名前。</param>
    /// <returns>数値（読めない場合は 24）。</returns>
    private static double ParseLength(XElement root, string name)
        => double.TryParse(root.Attribute(name)?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && value > 0
            ? value
            : 24;

    /// <summary>塗りの色を求める。</summary>
    /// <param name="root">SVG のルート要素。</param>
    /// <returns>色（<c>0xAARRGGBB</c>）。 見つからない場合は不透明の黒。</returns>
    /// <remarks>ルート要素の <c>fill</c> を使う（アイコンの色はテーマに依存するため、描画側で置き換えられる）。</remarks>
    private static uint ParseFill(XElement root)
    {
        var value = root.Attribute("fill")?.Value.Trim();
        if (value is { Length: 7 } && value[0] == '#'
            && uint.TryParse(value[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return 0xFF000000 | rgb;
        }

        if (value is { Length: 9 } && value[0] == '#'
            && uint.TryParse(value[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
        {
            return argb;
        }

        return 0xFF000000;
    }

    /// <summary>空のアイコンを作る。</summary>
    /// <param name="key">アイコンのキー。</param>
    /// <returns>パスを持たないアイコン。</returns>
    private static SvgIcon Empty(string key) => new(key, 24, 24, [], 0xFF000000);
}
