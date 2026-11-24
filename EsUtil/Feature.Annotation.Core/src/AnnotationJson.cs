using System.Text;
using System.Text.Json;
using EsUtil.Feature.Annotation.Core.Internal;

namespace EsUtil.Feature.Annotation;

/// <summary>注釈の JSON（<c>&lt;name&gt;.annotations.json</c>）の読み書き（単一所有）。</summary>
/// <remarks>
/// <b>書式は本クラスが単一所有する</b>: 4 つの UI 版・アプリが同じ書式で保存する（ファイル入出力は <see cref="AnnotationCore"/> が記録して行う）
/// （UI ごとに書くと、片方で保存した注釈が他方で読めない）。<br/>
/// <b>ソース生成 + 明示の変換器を使う</b>（反射を使わない。 Native AOT / トリミングでも動く）。<br/>
/// <b>未知の値は落とさない</b>: 解釈できない色は透明、欠落した項目は既定値として読む（1 件の破損で
/// ファイル全体を失わない）。
/// </remarks>
public static class AnnotationJson
{
    /// <summary>注釈ファイルの拡張子（元画像のファイル名へ付ける）を取得する。</summary>
    public const string FileSuffix = ".annotations.json";

    /// <summary>合成画像の名前に挟まる目印（元画像の名と拡張子の間へ入れる）を取得する。</summary>
    public const string ComposedInfix = ".annotated";

    /// <summary>本ライブラリが読み書きする JSON のオプション（インデントあり・変換器は固定）。</summary>
    private static readonly JsonSerializerOptions s_options = CreateOptions();

    /// <summary>注釈を JSON 文字列へ変換する。</summary>
    /// <param name="document">対象の文書。</param>
    /// <returns>JSON 文字列（UTF-8 の BOM 無しでファイルへ書ける）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> が null の場合。</exception>
    public static string Serialize(AnnotationDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.Serialize(document, s_options.GetTypeInfo(typeof(AnnotationDocument)));
    }

    /// <summary>JSON 文字列から注釈を読み込む。</summary>
    /// <param name="json">JSON 文字列。</param>
    /// <returns>読み込んだ文書。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> が null の場合。</exception>
    /// <exception cref="JsonException">JSON として解釈できない場合。</exception>
    public static AnnotationDocument Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize(json, s_options.GetTypeInfo(typeof(AnnotationDocument))) as AnnotationDocument
            ?? new AnnotationDocument();
    }

    /// <summary>元画像のパスから注釈ファイルのパスを求める。</summary>
    /// <param name="imagePath">元画像のパス（例: <c>…\screenshot.png</c>）。</param>
    /// <returns>注釈ファイルのパス（例: <c>…\screenshot.annotations.json</c>）。</returns>
    /// <remarks>
    /// <b>拡張子を置き換える</b>: 合成画像（<c>screenshot.annotated.png</c>）と同じく
    /// <c>&lt;ファイル名&gt;.annotations.json</c> にする。 元画像の拡張子を残すと
    /// <c>screenshot.png.annotations.json</c> になり、1 枚の画像に対する付随ファイルの名前が
    /// 合成画像と揃わない（利用者が対応を読み取りにくい）。
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="imagePath"/> が空白の場合。</exception>
    public static string GetDocumentPath(string imagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        var directory = Path.GetDirectoryName(imagePath);
        var name = Path.GetFileNameWithoutExtension(imagePath) + FileSuffix;

        return string.IsNullOrEmpty(directory) ? name : Path.Combine(directory, name);
    }

    /// <summary>合成画像のパスを求める。</summary>
    /// <param name="imagePath">元画像のパス（例: <c>…\screenshot.png</c>）。</param>
    /// <returns>合成画像のパス（例: <c>…\screenshot.annotated.png</c>）。</returns>
    /// <exception cref="ArgumentException"><paramref name="imagePath"/> が空白の場合。</exception>
    public static string GetComposedPath(string imagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        var directory = Path.GetDirectoryName(imagePath);
        var stem = Path.GetFileNameWithoutExtension(imagePath);
        var extension = Path.GetExtension(imagePath);
        var name = stem + ComposedInfix + extension;

        return string.IsNullOrEmpty(directory) ? name : Path.Combine(directory, name);
    }

    /// <summary>指定したパスが注釈の付随ファイル（注釈ファイルまたは合成画像）かどうかを判定する。</summary>
    /// <param name="path">判定するパス。</param>
    /// <returns>付随ファイルの場合は <see langword="true"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> が null の場合。</exception>
    /// <remarks>
    /// <b>元画像の一覧から付随ファイルを除くために使う</b>: 合成画像は画像の拡張子を持つため、そのままでは
    /// 元画像と並んで一覧に出る（同じ絵が 2 つ見え、選択すると注釈付きの絵を元画像として開いてしまう）。<br/>
    /// 判定は<b>名前の規則だけ</b>で行う（ファイルの存在は問わない）。
    /// </remarks>
    public static bool IsCompanionFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var name = Path.GetFileName(path);
        return name.EndsWith(FileSuffix, StringComparison.OrdinalIgnoreCase)
            || name.Contains(ComposedInfix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>注釈ファイルから元画像のパスを求める。</summary>
    /// <param name="documentPath">注釈ファイルのパス（例: <c>…\screenshot.annotations.json</c>）。</param>
    /// <param name="document">読み込んだ文書（元画像のファイル名を持つ）。</param>
    /// <returns>元画像のパス。 見つからない場合は <see langword="null"/>。</returns>
    /// <remarks>
    /// <b>探す順序</b>: 1) 注釈ファイルと同じフォルダの <see cref="AnnotationDocument.ImageFileName"/>、
    /// 2) 注釈ファイルの名前から <see cref="FileSuffix"/> を外した名前に画像拡張子を付けたもの。<br/>
    /// 見つからない場合は呼び出し側が利用者へ知らせる（注釈だけを開いても画像が無ければ表示できない）。
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="documentPath"/> が空白の場合。</exception>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> が null の場合。</exception>
    public static string? ResolveImagePath(string documentPath, AnnotationDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        ArgumentNullException.ThrowIfNull(document);

        var directory = Path.GetDirectoryName(documentPath);
        if (!string.IsNullOrEmpty(directory) && !string.IsNullOrWhiteSpace(document.ImageFileName))
        {
            var candidate = Path.Combine(directory, document.ImageFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var fileName = Path.GetFileName(documentPath);
        var stem = fileName.EndsWith(FileSuffix, StringComparison.OrdinalIgnoreCase)
            ? fileName[..^FileSuffix.Length]
            : Path.GetFileNameWithoutExtension(fileName);

        foreach (var extension in s_imageExtensions)
        {
            var candidate = Path.Combine(directory ?? string.Empty, stem + extension);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>注釈ファイルを読み込む（ファイル入出力は <see cref="AnnotationCore"/> が公開する）。</summary>
    /// <param name="documentPath">注釈ファイルのパス。</param>
    /// <returns>読み込んだ文書。</returns>
    /// <exception cref="ArgumentException"><paramref name="documentPath"/> が空白の場合。</exception>
    /// <exception cref="IOException">ファイルを読めない場合。</exception>
    /// <exception cref="JsonException">JSON として解釈できない場合。</exception>
    /// <remarks>文字コードは UTF-8（BOM の有無は問わない）。 書式の解釈は <see cref="Deserialize"/> が単一所有する。</remarks>
    internal static AnnotationDocument Load(string documentPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        return Deserialize(File.ReadAllText(documentPath, Encoding.UTF8));
    }

    /// <summary>注釈ファイルを書き出す（ファイル入出力は <see cref="AnnotationCore"/> が公開する）。</summary>
    /// <param name="documentPath">注釈ファイルのパス。</param>
    /// <param name="document">書き出す文書。</param>
    /// <exception cref="ArgumentException"><paramref name="documentPath"/> が空白の場合。</exception>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> が null の場合。</exception>
    /// <exception cref="IOException">ファイルへ書けない場合。</exception>
    /// <remarks>
    /// <b>文字コードは UTF-8（BOM なし）</b>である（他のリポジトリの UTF-8 の読み書きと揃える）。<br/>
    /// 書式は <see cref="Serialize"/> が単一所有する（読み手と書き手で書式がずれない）。
    /// </remarks>
    internal static void Save(string documentPath, AnnotationDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        ArgumentNullException.ThrowIfNull(document);

        File.WriteAllText(documentPath, Serialize(document), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>元画像として扱える拡張子（探す順序）。</summary>
    private static readonly string[] s_imageExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp"];

    /// <summary>JSON のオプションを生成する。</summary>
    /// <returns>読み書きに使うオプション（読み取り専用）。</returns>
    /// <remarks>
    /// <b>変換器はインスタンスで渡す</b>: 属性で型を指定する方式は、変換器の生成に反射を使うため
    /// Native AOT で警告になる（実測ではなく設計上の理由。 設定ファイルと同じ方針）。
    /// </remarks>
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            TypeInfoResolver = AnnotationJsonContext.Default,
        };

        options.Converters.Add(new EsRectJsonConverter());
        options.Converters.Add(new EsPointJsonConverter());
        options.Converters.Add(new DrawingColorJsonConverter());
        options.MakeReadOnly();
        return options;
    }
}
