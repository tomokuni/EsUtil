using System.Runtime.Versioning;
using Windows.Win32.Graphics.Imaging;
using Windows.Win32.System.Com;

namespace EsUtil.Platform.Core.Native.Win.Wic;

/// <summary>実行環境の WIC が書き出せる形式の一覧（OS のエンコーダーから動的に作る）。</summary>
/// <remarks>
/// <b>形式を列挙しない</b>: WIC が扱える形式は OS とコーデック拡張（Store アプリ）で変わるため、
/// <b>固定の一覧を持たない</b>。 起動時に <c>IWICImagingFactory::CreateComponentEnumerator</c> で
/// <b>登録されているエンコーダーを列挙</b>し、コンテナ GUID と拡張子を OS から受け取る。<br/>
/// <b>一覧は試し書きで確かめる</b>: 列挙に現れても<b>本ライブラリの画素（BGRA8888）を書けない</b>エンコーダーがある
/// （実測: JPEG XL のエンコーダーは <c>32bppRGBA</c> を要求し、変換を与えても書けなかった）。
/// 一覧に載せると「一覧の拡張子で保存すれば成功する」という約束が崩れるため、8x8 の試し書きが
/// <b>通った形式だけ</b>を残す（結果は保持する）。<br/>
/// <b>拡張子は小文字に揃える</b>: OS は <c>.JXL</c> のように大文字で申告することがある。
/// </remarks>
[SupportedOSPlatform("windows10.0.17763")]
internal static unsafe class WinWicFormats
{
    /// <summary>エンコーダーを列挙するときに指定する種別（<c>WICEncoder</c>）。</summary>
    private const uint ComponentTypeEncoder = 2;

    /// <summary>列挙の指定（<c>WICComponentEnumerateDefault</c>）。</summary>
    private const uint EnumerateDefault = 0;

    /// <summary>書き出せる形式（初回のアクセスで 1 度だけ調べる）。</summary>
    private static readonly Lazy<IReadOnlyList<WinWicFormat>> s_all = new(Discover);

    /// <summary>書き出せる拡張子（初回のアクセスで 1 度だけ作る）。</summary>
    private static readonly Lazy<IReadOnlyList<string>> s_extensions =
        new(() => [.. s_all.Value.SelectMany(format => format.Extensions)]);

    /// <summary>実行環境で書き出せる形式。</summary>
    internal static IReadOnlyList<WinWicFormat> All => s_all.Value;

    /// <summary>実行環境で書き出せる拡張子（小文字・先頭のドットを含む）。</summary>
    /// <remarks>WIC を使えない環境では空になる。</remarks>
    internal static IReadOnlyList<string> Extensions => s_extensions.Value;

    /// <summary>拡張子から形式を求める。</summary>
    /// <param name="extension">拡張子（先頭のドットを含む。 大文字・小文字は区別しない）。</param>
    /// <returns>形式。 書き出せない拡張子の場合は null。</returns>
    internal static WinWicFormat? FindByExtension(string extension)
    {
        foreach (var format in All)
        {
            foreach (var candidate in format.Extensions)
            {
                if (string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase))
                {
                    return format;
                }
            }
        }

        return null;
    }

    /// <summary>コンテナ GUID から形式を求める。</summary>
    /// <param name="container">コンテナ形式の GUID。</param>
    /// <returns>形式。 書き出せない形式の場合は null。</returns>
    internal static WinWicFormat? FindByContainer(Guid container)
    {
        foreach (var format in All)
        {
            if (format.Container == container)
            {
                return format;
            }
        }

        return null;
    }

    /// <summary>OS に登録されているエンコーダーを列挙し、試し書きが通った形式だけを返す。</summary>
    /// <returns>書き出せる形式（OS が申告した順）。</returns>
    private static IReadOnlyList<WinWicFormat> Discover()
    {
        var factory = WinWicFactory.Create();
        if (factory is null)
        {
            return [];
        }

        var formats = new List<WinWicFormat>();
        try
        {
            var enumerator = CreateEnumerator(factory);
            try
            {
                IUnknown* unknown;
                while ((unknown = Next(enumerator)) != null)
                {
                    try
                    {
                        if (TryRead(unknown) is { } format)
                        {
                            formats.Add(format);
                        }
                    }
                    finally
                    {
                        unknown->Release();
                    }
                }
            }
            finally
            {
                enumerator->Release();
            }
        }
        finally
        {
            factory->Release();
        }

        return formats;
    }

    /// <summary>コンポーネントの列挙子を作る。</summary>
    /// <param name="factory">イメージング ファクトリ。</param>
    /// <returns>生成した列挙子（呼び出し側が <c>Release()</c> を呼ぶ）。</returns>
    private static IEnumUnknown* CreateEnumerator(IWICImagingFactory* factory)
    {
        IEnumUnknown* enumerator;
        factory->CreateComponentEnumerator(ComponentTypeEncoder, EnumerateDefault, &enumerator);
        return enumerator;
    }

    /// <summary>列挙子から次の 1 件を取り出す。</summary>
    /// <param name="enumerator">列挙子。</param>
    /// <returns>次のオブジェクト（呼び出し側が <c>Release()</c> を呼ぶ）。 残りが無い場合は null。</returns>
    /// <remarks>
    /// <b>個数を問い合わせない理由</b>: <c>Next</c> は残りが無い場合に <c>S_FALSE</c> を返す（成功扱い）ため、
    /// <b>返された値が null かどうか</b>で終わりを判定する。
    /// </remarks>
    private static IUnknown* Next(IEnumUnknown* enumerator)
    {
        IUnknown* unknown;
        return enumerator->Next(1, &unknown, null).Succeeded && unknown != null ? unknown : null;
    }

    /// <summary>列挙された 1 件から形式を読み取る（試し書きが通った場合だけ返す）。</summary>
    /// <param name="unknown">列挙されたオブジェクト。</param>
    /// <returns>形式。 読み取れない・書き出せない場合は null。</returns>
    private static WinWicFormat? TryRead(IUnknown* unknown)
    {
        IWICBitmapEncoderInfo* info;
        var iid = typeof(IWICBitmapEncoderInfo).GUID;
        if (unknown->QueryInterface(&iid, (void**)&info).Failed)
        {
            return null;
        }

        try
        {
            var codec = (IWICBitmapCodecInfo*)info;
            Guid container;
            codec->GetContainerFormat(&container);
            var extensions = ReadExtensions(codec);
            if (extensions.Length == 0 || !WinWicImageCodec.CanEncode(container))
            {
                return null;
            }

            return new WinWicFormat
            {
                Container = container,
                Name = ReadText(codec, friendly: true),
                Extensions = extensions,
            };
        }
        finally
        {
            ((IUnknown*)info)->Release();
        }
    }

    /// <summary>エンコーダーが申告した拡張子を読む。</summary>
    /// <param name="codec">エンコーダーの情報。</param>
    /// <returns>小文字に揃えた拡張子（先頭のドットを含む）。 申告が無い場合は空。</returns>
    private static string[] ReadExtensions(IWICBitmapCodecInfo* codec)
        => [.. ReadText(codec, friendly: false)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(extension => string.Concat(extension.Select(char.ToLowerInvariant)))
            .Where(extension => extension.StartsWith('.'))
            .Distinct(StringComparer.Ordinal)];

    /// <summary>エンコーダーの情報から文字列を読む。</summary>
    /// <param name="codec">エンコーダーの情報。</param>
    /// <param name="friendly">表示名を読む場合は true。 拡張子を読む場合は false。</param>
    /// <returns>読み取った文字列。 読めない場合は空。</returns>
    private static string ReadText(IWICBitmapCodecInfo* codec, bool friendly)
    {
        uint length;
        if (friendly)
        {
            codec->GetFriendlyName(0, null, &length);
        }
        else
        {
            codec->GetFileExtensions(0, null, &length);
        }

        if (length == 0)
        {
            return string.Empty;
        }

        var buffer = new char[length];
        fixed (char* text = buffer)
        {
            if (friendly)
            {
                codec->GetFriendlyName(length, text, &length);
            }
            else
            {
                codec->GetFileExtensions(length, text, &length);
            }
        }

        return new string(buffer, 0, (int)length - 1);
    }
}
