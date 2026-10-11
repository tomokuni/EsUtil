using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>画像のファイル保存・読み込みの実装（GDI+ のフラット API を使う Windows 専用）。</summary>
/// <remarks>
/// <b>唯一の実装</b>: 公開面は画像型のメンバー（<see cref="EsUtil.Platform.Core.Imaging.EsBitmapBase.Save(string)"/> /
/// <see cref="EsUtil.Platform.Core.Imaging.EsBitmap.Load(string)"/> / <see cref="EsUtil.Platform.Core.Imaging.EsWritableBitmap.Load(string)"/>) であり、
/// 本クラスはその実装を 1 箇所へ集約する（本層は internal）。<br/>
/// <b>画像型を知らない</b>: 符号化は受け取ったピクセル バイト列を書き
/// （<see cref="EncodeToFile(ReadOnlySpan{byte}, int, int, string)"/>）、復号は生成処理を受け取って
/// デコード結果をその領域へ直接書く（<see cref="DecodeFromFile{T}(string, Func{int, int, Action{Span{byte}}, T})"/>）。
/// 画像型（<c>EsBitmap</c> 等）を名指ししないため、画像型が要るのは公開面だけになる。<br/>
/// <b>依存分離</b>: ファイル形式（BMP / PNG / JPEG / GIF / TIFF）のエンコード・デコードは OS の
/// <c>gdiplus.dll</c> が担い、<b>外部パッケージ（<c>System.Drawing.Common</c> 等）を参照しない</b>。<br/>
/// <b>形式は拡張子が決める</b>: <see cref="EncodeToFile(ReadOnlySpan{byte}, int, int, string)"/> は保存先の拡張子から形式を選ぶ。
/// <b>未対応の拡張子は例外にする</b>（PNG 等へ黙って差し替えると、呼び出し側が意図した形式と違うファイルが
/// できて気付かない。 対応形式は <see cref="SupportedExtensions"/> が単一所有する）。<br/>
/// <b>Windows 専用</b>: <c>gdiplus.dll</c> は Windows でのみ利用可能（<see cref="SupportedOSPlatformAttribute"/> で限定）。<br/>
/// <b>副作用（実測）</b>: 初回の呼び出しで GDI+ をプロセス単位に初期化するため、
/// <b>バックグラウンド スレッドが 1 本増え</b>（GDI+ が表示設定の通知を受け取るため）、
/// <b>解放されない参照カウントを 1 つ取り</b>（<c>GdiplusShutdown</c> は呼ばない。 呼ぶとホストの GDI+ を止めうる）、
/// <b>初回だけ約 5 ms の遅延が乗る</b>。 GDI+ を他に使っていないプロセスだけが影響を受けます
/// （<c>System.Drawing.Common</c> を使うホストには同じ初期化とスレッドが既にあります）。<br/>
/// <b>WebP / HEIF を対応形式へ入れない理由（実測）</b>: GDI+ のエンコーダーは 5 件だけである
/// （<c>GdipGetImageEncoders</c> が返すのは BMP / JPEG / GIF / TIFF / PNG）。 WebP / HEIF のエンコーダーは無く、
/// 存在しない形式の CLSID を渡した <c>GdipSaveImageToFile</c> は失敗するか<b>別形式（PNG）を書く</b>ため、
/// 拡張子と実体が食い違うファイルができる（実測: PNG の CLSID と <c>.webp</c> のパスで 2831 バイトの PNG が書かれた）。<br/>
/// <b>制限</b>: JPEG / BMP はアルファ（透明度）を保存できない（形式の仕様）。 透過を保つ場合は PNG を選ぶこと。
/// </remarks>
internal static class WinGdiPlusImageCodec
{
    /// <summary>保存に使える拡張子（小文字・先頭のドットを含む）。</summary>
    internal static readonly IReadOnlyList<string> SupportedExtensions = [".bmp", ".png", ".jpg", ".jpeg", ".gif", ".tif", ".tiff"];

    /// <summary>JPEG の品質として許容する最小値。</summary>
    internal const int JpegQualityMinimum = 0;

    /// <summary>JPEG の品質として許容する最大値。</summary>
    internal const int JpegQualityMaximum = 100;

    /// <summary>ピクセル データをファイルへエンコードする（形式は拡張子が決める。オプションは既定）。</summary>
    /// <param name="pixels">エンコードするピクセル データ（BGRA8888・アルファ乗算済み）。 借用であり書き換えない。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="filePath">保存先のファイルパス（拡張子が形式を決める）。</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合、または
    /// <paramref name="pixels"/> の長さが <c>幅 × 4 × 高さ</c> と一致しない場合。</exception>
    /// <exception cref="NotSupportedException">拡張子が対応形式でない場合（<see cref="SupportedExtensions"/>）。</exception>
    /// <exception cref="System.Runtime.InteropServices.ExternalException">エンコード（GDI+）が失敗した場合（<see cref="Win32Exception"/> で状態値を持つ）。</exception>
    /// <remarks>形式ごとの調整値は <see cref="EncodeToFile(ReadOnlySpan{byte}, int, int, string, EsImageSaveOptions)"/> を使う。</remarks>
    [SupportedOSPlatform("windows")]
    internal static void EncodeToFile(ReadOnlySpan<byte> pixels, int width, int height, string filePath)
        => EncodeToFile(pixels, width, height, filePath, default);

    /// <summary>ピクセル データをファイルへエンコードする（形式は拡張子が決める。オプションを指定できる）。</summary>
    /// <param name="pixels">エンコードするピクセル データ（BGRA8888・アルファ乗算済み）。 借用であり書き換えない。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="filePath">保存先のファイルパス（拡張子が形式を決める。 <see cref="EsImageSaveOptions.Format"/> を指定した場合に拡張子が無ければ補完する）。</param>
    /// <param name="options">保存のオプション（既定の場合は <c>default</c>）。</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合、
    /// <paramref name="pixels"/> の長さが <c>幅 × 4 × 高さ</c> と一致しない場合、または
    /// <paramref name="filePath"/> の拡張子と <paramref name="options"/> の形式が食い違い
    /// <see cref="EsImageSaveOptions.IgnoreFormatMismatch"/> が false の場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="options"/> の値が許容範囲外の場合。</exception>
    /// <exception cref="NotSupportedException">拡張子が対応形式でない場合（<see cref="SupportedExtensions"/>）、または
    /// <paramref name="options"/> にその形式で使えない項目が設定されている場合。</exception>
    /// <exception cref="System.Runtime.InteropServices.ExternalException">エンコード（GDI+）が失敗した場合（<see cref="Win32Exception"/> で状態値を持つ）。</exception>
    /// <remarks>
    /// 保存形式は<b>拡張子</b>から決める（<c>.bmp</c> = Windows Bitmap / <c>.png</c> / <c>.jpg</c> / <c>.jpeg</c> /
    /// <c>.gif</c> / <c>.tif</c> / <c>.tiff</c>）。 <see cref="EsImageSaveOptions.Format"/> を指定した場合は
    /// <b>形式の方が優先</b>され、次のように扱う:
    ///
    /// <list type="table">
    /// <item><term>拡張子が無い</term><description>指定形式の拡張子を補完して保存する（<c>shot</c> + PNG → <c>shot.png</c>）。</description></item>
    /// <item><term>拡張子が同じ形式</term><description>そのまま保存する。</description></item>
    /// <item><term>拡張子が違う形式</term><description><see cref="EsImageSaveOptions.IgnoreFormatMismatch"/> が true なら<b>指定形式で</b>与えられたファイル名へ保存する。 false なら <see cref="ArgumentException"/>。</description></item>
    /// <item><term>拡張子が未対応（<c>.webp</c> 等）</term><description>フラグが true なら指定形式で保存し、false なら <see cref="NotSupportedException"/>（拡張子が対応形式でない場合と同じ）。</description></item>
    /// </list>
    ///
    /// <b>形式を引数にしない理由</b>: 形式を表す引数（enum 等）を必須にすると、拡張子との整合を呼び出し側が保つ必要が生じ、
    /// 食い違った場合は拡張子と実体がずれたファイルができる（形式の単一所有は <see cref="SupportedExtensions"/> である）。
    /// <b>本オプションはその食い違いを明示的に選んだ場合だけ許す</b>。<br/>
    /// <b>オプションは形式ごとに検証する</b>: その形式で反映されない項目を設定した場合は例外にする
    /// （黙って無視すると「設定したのに効かない」ことに気付かない）。<br/>
    /// <b>GDI+ の初期化より前に判定する</b>: 拡張子とオプションの検証は GDI+ の初期化（副作用を持つ）より先に行うため、
    /// 拒否される呼び出しは GDI+ を初期化しない。<br/>
    /// <b>ピクセルは複製しない</b>: 受け取ったバイト列を指す GDI+ のビットマップを作り、そのままエンコードする<br/>
    /// （ストライドは <c>幅 × 4</c> 固定。 行パディングを持つ画像を受け取らない）。<br/>
    /// <b>JPEG / BMP はアルファを保存できない</b>（透過を保つ場合は PNG を選ぶ）。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    internal static void EncodeToFile(ReadOnlySpan<byte> pixels, int width, int height, string filePath, EsImageSaveOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var stride = width * IEsBitmap.BytesPerPixel;
        var expected = (long)stride * height;
        if (pixels.Length != expected)
        {
            throw new ArgumentException(
                $"ピクセル データの長さが寸法と一致しません（{width}x{height} は {expected} バイト。 実際は {pixels.Length} バイト）。",
                nameof(pixels));
        }

        var (format, targetPath) = ResolveTarget(filePath, options);
        WinGdiPlusLifetime.EnsureInitialized();

        unsafe
        {
            fixed (byte* p = pixels)
            {
                var status = WinNative.GdipCreateBitmapFromScan0(
                    width, height, stride, WinNative.GpPixelFormat32bppPArgb, (nint)p, out var image);
                if (status != WinNative.GpOk)
                {
                    throw new Win32Exception(status, $"保存用のビットマップを作成できませんでした（GDI+ の状態: {status}）。");
                }

                try
                {
                    SaveImage(image, targetPath, format, options);
                }
                finally
                {
                    WinNative.GdipDisposeImage(image);
                }
            }
        }
    }

    /// <summary>保存する形式と実際の保存先を決める（拡張子の補完と食い違いの判定を含む）。</summary>
    /// <param name="filePath">保存先のファイルパス。</param>
    /// <param name="options">保存のオプション。</param>
    /// <returns>保存する形式と実際の保存先。</returns>
    /// <exception cref="ArgumentException">拡張子と指定形式が食い違い、無視する設定でない場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException">JPEG の品質が許容範囲外の場合。</exception>
    /// <exception cref="NotSupportedException">拡張子が対応形式でない場合、または設定されたオプションがその形式で使えない場合。</exception>
    /// <remarks>
    /// <b>形式を指定しない場合は拡張子が形式を決める</b>（従来の挙動を変えない）。
    /// <b>形式を指定した場合は形式が優先</b>され、拡張子が無ければ補完し、食い違いは
    /// <see cref="EsImageSaveOptions.IgnoreFormatMismatch"/> で判定する。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    private static (ImageFileFormat Format, string FilePath) ResolveTarget(string filePath, in EsImageSaveOptions options)
    {
        var extension = Path.GetExtension(filePath);
        if (options.Format is not EsImageFileFormat requested)
        {
            // 形式の指定が無い場合は拡張子が形式を決める（未対応の拡張子は従来どおり拒否する）
            return (ResolveExtension(extension, options), filePath);
        }

        var format = ToFormat(requested);
        if (extension.Length == 0)
        {
            // 拡張子が無い場合は指定形式の拡張子を補完する（保存先は補完後のパスになる）
            ValidateOptions(format, options);
            return (format, string.Concat(filePath, CanonicalExtension(format)));
        }

        var fromExtension = TryResolveExtension(extension);
        if (fromExtension == format)
        {
            ValidateOptions(format, options);
            return (format, filePath);
        }

        if (!options.IgnoreFormatMismatch)
        {
            // 未対応の拡張子は「拡張子が対応形式でない」として扱う（指定形式で黙って上書きしない）
            if (fromExtension is null)
            {
                throw CreateUnsupportedExtension(extension);
            }

            throw new ArgumentException(
                $"拡張子 '{extension}' は指定された形式（{requested}）と一致しません。 "
                + $"一致させるか {nameof(EsImageSaveOptions)}.{nameof(EsImageSaveOptions.IgnoreFormatMismatch)} を true にしてください。",
                nameof(filePath));
        }

        // 食い違いを無視する場合は指定形式で与えられたファイル名へ保存する
        ValidateOptions(format, options);
        return (format, filePath);
    }

    /// <summary>拡張子から形式を決め、その形式で使えるオプションだけが設定されていることを確かめる。</summary>
    /// <param name="extension">拡張子（先頭のドットを含む。 空の場合もある）。</param>
    /// <param name="options">保存のオプション。</param>
    /// <returns>保存する形式。</returns>
    /// <exception cref="NotSupportedException">拡張子が対応形式でない場合、または設定されたオプションがその形式で使えない場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException">JPEG の品質が許容範囲外の場合。</exception>
    [SupportedOSPlatform("windows")]
    private static ImageFileFormat ResolveExtension(string extension, in EsImageSaveOptions options)
    {
        var format = TryResolveExtension(extension) ?? throw CreateUnsupportedExtension(extension);
        ValidateOptions(format, options);
        return format;
    }

    /// <summary>拡張子から形式を求める（未対応の場合は null）。</summary>
    /// <param name="extension">拡張子（先頭のドットを含む。 空の場合もある）。</param>
    /// <returns>形式。 未対応の場合は null。</returns>
    [SupportedOSPlatform("windows")]
    private static ImageFileFormat? TryResolveExtension(string extension)
        => extension.ToUpperInvariant() switch
        {
            ".BMP" => ImageFileFormat.Bmp,
            ".PNG" => ImageFileFormat.Png,
            ".JPG" or ".JPEG" => ImageFileFormat.Jpeg,
            ".GIF" => ImageFileFormat.Gif,
            ".TIF" or ".TIFF" => ImageFileFormat.Tiff,
            _ => null,
        };

    /// <summary>その形式のオプションの組み合わせを検証する。</summary>
    /// <param name="format">保存する形式。</param>
    /// <param name="options">保存のオプション。</param>
    /// <exception cref="NotSupportedException">その形式で使えない項目が設定されている場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException">JPEG の品質が許容範囲外の場合。</exception>
    /// <remarks>
    /// <b>品質を使えるのは JPEG だけである</b>（他の形式は品質を受け取っても出力が変わらない。 実測）。
    /// 黙って無視すると「設定したのに効かない」ことに気付かないため、例外にする。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    private static void ValidateOptions(ImageFileFormat format, in EsImageSaveOptions options)
    {
        if (options.JpegQuality is not int quality)
        {
            return;
        }

        if (format != ImageFileFormat.Jpeg)
        {
            throw CreateUnsupportedOption(format, nameof(EsImageSaveOptions.JpegQuality));
        }

        if (quality < JpegQualityMinimum || quality > JpegQualityMaximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options), quality, $"JPEG の品質は {JpegQualityMinimum}〜{JpegQualityMaximum} の範囲で指定します。");
        }
    }

    /// <summary>形式を表す enum を内部の形式へ変換する。</summary>
    /// <param name="format">公開の形式。</param>
    /// <returns>内部の形式。</returns>
    /// <exception cref="ArgumentOutOfRangeException">定義されていない値の場合。</exception>
    [SupportedOSPlatform("windows")]
    private static ImageFileFormat ToFormat(EsImageFileFormat format)
        => format switch
        {
            EsImageFileFormat.Bmp => ImageFileFormat.Bmp,
            EsImageFileFormat.Png => ImageFileFormat.Png,
            EsImageFileFormat.Jpeg => ImageFileFormat.Jpeg,
            EsImageFileFormat.Gif => ImageFileFormat.Gif,
            EsImageFileFormat.Tiff => ImageFileFormat.Tiff,
            var other => throw new ArgumentOutOfRangeException(nameof(format), other, "定義されていない画像形式です。"),
        };

    /// <summary>形式を表す enum の名前を得る（例外メッセージ用）。</summary>
    /// <param name="format">内部の形式。</param>
    /// <returns><see cref="EsImageFileFormat"/> の対応する値の名前。</returns>
    /// <remarks>公開の enum と内部の enum は<b>同じ名前の値を持つ</b>（対応を名前で読み取れるようにするため）。
    /// 内部の名前をそのまま使うと、対応が崩れたときに気付きにくくなるため対応表を持つ。</remarks>
    [SupportedOSPlatform("windows")]
    private static string ToPublicName(ImageFileFormat format)
        => format switch
        {
            ImageFileFormat.Bmp => nameof(EsImageFileFormat.Bmp),
            ImageFileFormat.Png => nameof(EsImageFileFormat.Png),
            ImageFileFormat.Jpeg => nameof(EsImageFileFormat.Jpeg),
            ImageFileFormat.Gif => nameof(EsImageFileFormat.Gif),
            ImageFileFormat.Tiff => nameof(EsImageFileFormat.Tiff),
            var other => other.ToString(),
        };

    /// <summary>拡張子が対応形式でないことを表す例外を作る。</summary>
    /// <param name="extension">対象の拡張子（空の場合もある）。</param>
    /// <returns>例外。</returns>
    [SupportedOSPlatform("windows")]
    private static NotSupportedException CreateUnsupportedExtension(string extension)
        => new(extension.Length == 0
            ? $"拡張子が無いため保存形式を決められません。 拡張子を付けるか {nameof(EsImageSaveOptions)}.{nameof(EsImageSaveOptions.Format)} を指定してください。"
                + $" 対応形式: {string.Join(" / ", SupportedExtensions)}"
            : $"拡張子 '{extension}' へは保存できません。 対応形式: {string.Join(" / ", SupportedExtensions)}");

    /// <summary>形式ごとの標準の拡張子（拡張子が無い場合の補完に使う）。</summary>
    /// <param name="format">保存する形式。</param>
    /// <returns>先頭のドットを含む拡張子。</returns>
    /// <remarks>別名（<c>.jpeg</c> / <c>.tiff</c>）ではなく、対応形式の一覧の先頭にある値を返す。</remarks>
    [SupportedOSPlatform("windows")]
    private static string CanonicalExtension(ImageFileFormat format)
        => format switch
        {
            ImageFileFormat.Bmp => ".bmp",
            ImageFileFormat.Png => ".png",
            ImageFileFormat.Jpeg => ".jpg",
            ImageFileFormat.Gif => ".gif",
            ImageFileFormat.Tiff => ".tif",
            var other => throw new ArgumentOutOfRangeException(nameof(format), other, "未知の画像形式です。"),
        };

    /// <summary>GDI+ の画像をファイルへ書き出す（形式ごとのエンコーダーとオプションを使う）。</summary>
    /// <param name="image">書き出す GDI+ の画像。</param>
    /// <param name="filePath">保存先のファイルパス。</param>
    /// <param name="format">保存する形式（拡張子から決定済み）。</param>
    /// <param name="options">保存のオプション（<paramref name="format"/> との整合は検証済み）。</param>
    /// <exception cref="System.Runtime.InteropServices.ExternalException">エンコード（GDI+）が失敗した場合。</exception>
    [SupportedOSPlatform("windows")]
    private static void SaveImage(IntPtr image, string filePath, ImageFileFormat format, in EsImageSaveOptions options)
    {
        var clsid = GetEncoderClsid(format);
        var parameters = CreateEncoderParameters(format, options);
        try
        {
            var status = WinNative.GdipSaveImageToFile(image, filePath, ref clsid, parameters);
            if (status != WinNative.GpOk)
            {
                throw new Win32Exception(status, $"'{filePath}' へ保存できませんでした（GDI+ の状態: {status}）。");
            }
        }
        finally
        {
            if (parameters != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(parameters);
            }
        }
    }

    /// <summary>GDI+ へ渡すエンコーダー パラメーターの領域を 1 つの塊として確保して組み立てる。</summary>
    /// <param name="format">保存する形式（オプションの整合は検証済み）。</param>
    /// <param name="options">保存のオプション。</param>
    /// <returns>確保した領域。 解放が必要（既定値を渡す場合は <see cref="IntPtr.Zero"/> であり、解放も不要）。</returns>
    /// <remarks>
    /// <b>値も同じ塊に置く</b>: GDI+ は値へのポインターを保持し、<c>GdipSaveImageToFile</c> が返るまで解放してはならない。
    /// 1 回の確保にまとめれば解放も 1 回で済む。<br/>
    /// <b>位置は型の定義から求める</b>: 並びの先頭から <c>Marshal.OffsetOf</c> で求めた位置へエントリーを書き、
    /// その直後（<c>Marshal.SizeOf</c> で求めた大きさの倍数）へ値を書く。 64 ビットでのパディングを手計算した定数で表さない。<br/>
    /// <b>渡す値は 1 件だけ</b>: オプションを持つのは JPEG の品質だけである（理由は <see cref="EsImageSaveOptions"/> を参照）。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    private static IntPtr CreateEncoderParameters(ImageFileFormat format, in EsImageSaveOptions options)
    {
        if (format != ImageFileFormat.Jpeg || options.JpegQuality is not int quality)
        {
            // 既定値の場合はパラメーターを渡さない（IntPtr.Zero が GDI+ の既定を表す）
            return IntPtr.Zero;
        }

        var entryOffset = Marshal.OffsetOf<WinNative.GpEncoderParameters>(nameof(WinNative.GpEncoderParameters.Parameter)).ToInt32();
        var entrySize = Marshal.SizeOf<WinNative.GpEncoderParameter>();
        var memory = Marshal.AllocHGlobal(entryOffset + entrySize + sizeof(int));

        Marshal.WriteInt32(memory, 0, 1);

        // 値のアドレスは並びの直後（エントリーは 4 バイト境界に並ぶため、値も 4 バイト境界に載る）
        var address = IntPtr.Add(memory, entryOffset + entrySize);
        Marshal.WriteInt32(address, quality);

        var entry = CreateLongEntry(WinNative.GpEncoderQuality);
        entry.Value = address;
        Marshal.StructureToPtr(entry, IntPtr.Add(memory, entryOffset), false);

        return memory;
    }

    /// <summary>32 ビット整数 1 件を渡すエンコーダー パラメーターを作る。</summary>
    /// <param name="parameterGuid">パラメーターの種類を表す GUID。</param>
    /// <returns>値のアドレスだけを書き入れていないエントリー。</returns>
    [SupportedOSPlatform("windows")]
    private static WinNative.GpEncoderParameter CreateLongEntry(Guid parameterGuid)
        => new()
        {
            Guid = parameterGuid,
            NumberOfValues = 1,
            Type = WinNative.GpEncoderParameterValueTypeLong,
        };

    /// <summary>使用するエンコーダーの CLSID を形式から決める。</summary>
    /// <param name="format">保存する形式。</param>
    /// <returns>エンコーダーの CLSID。</returns>
    [SupportedOSPlatform("windows")]
    private static Guid GetEncoderClsid(ImageFileFormat format)
        => format switch
        {
            ImageFileFormat.Bmp => WinNative.GpBmpEncoderClsid,
            ImageFileFormat.Png => WinNative.GpPngEncoderClsid,
            ImageFileFormat.Jpeg => WinNative.GpJpegEncoderClsid,
            ImageFileFormat.Gif => WinNative.GpGifEncoderClsid,
            ImageFileFormat.Tiff => WinNative.GpTiffEncoderClsid,
            var other => throw new ArgumentOutOfRangeException(nameof(format), other, "未知の画像形式です。"),
        };

    /// <summary>その形式で使えないオプションが設定されたことを表す例外を作る。</summary>
    /// <param name="format">保存する形式。</param>
    /// <param name="optionName">使えないオプションの名前。</param>
    /// <returns>例外。</returns>
    [SupportedOSPlatform("windows")]
    private static NotSupportedException CreateUnsupportedOption(ImageFileFormat format, string optionName)
        => new($"オプション '{optionName}' は {ToPublicName(format)} 形式では使えません"
            + $"（現在オプションを持つのは JPEG の {nameof(EsImageSaveOptions.JpegQuality)} だけです）。");

    /// <summary>保存する形式。</summary>
    /// <remarks>GDI+ のエンコーダーと 1 対 1 に対応する（拡張子の一覧は <see cref="SupportedExtensions"/> が単一所有する）。</remarks>
    private enum ImageFileFormat
    {
        /// <summary>Windows Bitmap（<c>.bmp</c>）。</summary>
        Bmp,

        /// <summary>PNG（<c>.png</c>）。</summary>
        Png,

        /// <summary>JPEG（<c>.jpg</c> / <c>.jpeg</c>）。</summary>
        Jpeg,

        /// <summary>GIF（<c>.gif</c>）。</summary>
        Gif,

        /// <summary>TIFF（<c>.tif</c> / <c>.tiff</c>）。</summary>
        Tiff,
    }

    /// <summary>ファイルをデコードし、生成された画像として返す（形式は内容から判別される）。</summary>
    /// <typeparam name="T">生成する画像の型（<see cref="EsUtil.Platform.Core.Imaging.EsBitmap"/> または
    /// <see cref="EsUtil.Platform.Core.Imaging.EsWritableBitmap"/>）。</typeparam>
    /// <param name="filePath">読み込むファイルのパス（形式は内容から判別される）。</param>
    /// <param name="create">寸法と充填処理を受け取って画像を生成する処理（デコード結果をこの充填処理で
    /// 受け取った領域へ直接書くため、中間バッファも追加のコピーも要らない）。</param>
    /// <returns>生成された画像。 所有権は呼び出し側へ移る。</returns>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合、ファイルが存在しない場合、
    /// または画像として解釈できない場合（GDI+ はどちらも同じ状態値を返すため区別しない）。</exception>
    /// <remarks>
    /// <b>画像型を知らない</b>: 本メソッドは生成処理を受け取るだけで、画像型（<c>EsBitmap</c> 等）を名指ししない
    /// （型引数と充填処理は呼び出し側が決める）。 画像型が要るのは読み込みと保存の公開面
    /// （<see cref="EsUtil.Platform.Core.Imaging.EsBitmapBase"/>）だけになる。<br/>
    /// <b><paramref name="create"/> は充填処理を 1 回だけ呼ぶ責任を持つ</b>（呼ばなければピクセルが不定のまま、
    /// 2 回呼べば同じ領域を二度書く）。<br/>
    /// <b>ピクセルは 1 回だけ書かれる</b>: 中間バッファを作らず、GDI+ のロックしたデータから画像のバッファへ
    /// 直接コピーする（画像型のバッファは 64 バイト整列されているため、一時バッファを挟むと 4K で +31 MiB と
    /// 実測 +3.0 ms のコピーが増える）。<br/>
    /// <b>縮小読み込みはしない</b>（元の寸法でデコードする。 大きな画像を小さく使う場合は
    /// <c>Resize</c> で縮小すること。 サムネイルのようにデコード自体を小さくしたい場合は、各 UI の
    /// コーデック（縮小デコード対応）を使う方が速い）。<br/>
    /// <b>形式は内容から判別する</b>（拡張子と内容が一致しない場合は内容に従う）。<br/>
    /// <b>ピクセルはアルファ乗算済みの BGRA8888 へ変換して取り込む</b>（元の形式が異なっても GDI+ が変換する）。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    internal static T DecodeFromFile<T>(string filePath, Func<int, int, Action<Span<byte>>, T> create)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        WinGdiPlusLifetime.EnsureInitialized();

        var status = WinNative.GdipCreateBitmapFromFile(filePath, out var image);
        if (status != WinNative.GpOk)
        {
            throw new ArgumentException($"'{filePath}' を画像として読み込めませんでした。", nameof(filePath));
        }

        try
        {
            var (width, height) = ReadSize(image);
            return create(width, height, destination => CopyPixelsFrom(image, width, height, destination));
        }
        finally
        {
            WinNative.GdipDisposeImage(image);
        }
    }

    /// <summary>GDI+ の画像の寸法を読み取る。</summary>
    /// <param name="image">対象の GDI+ の画像。</param>
    /// <returns>幅と高さ（ピクセル）。</returns>
    /// <exception cref="Win32Exception">寸法を取得できなかった場合。</exception>
    [SupportedOSPlatform("windows")]
    private static (int Width, int Height) ReadSize(IntPtr image)
    {
        var width = GetDimension(WinNative.GdipGetImageWidth(image, out var w), w, "幅");
        var height = GetDimension(WinNative.GdipGetImageHeight(image, out var h), h, "高さ");
        return (width, height);
    }

    /// <summary>GDI+ が返した寸法を検証して取り出す。</summary>
    /// <param name="status">GDI+ の状態値。</param>
    /// <param name="value">GDI+ が返した寸法。</param>
    /// <param name="name">寸法の名前（例外メッセージ用）。</param>
    /// <returns>検証済みの寸法。</returns>
    /// <exception cref="Win32Exception">寸法を取得できなかった場合。</exception>
    [SupportedOSPlatform("windows")]
    private static int GetDimension(int status, uint value, string name)
        => status == WinNative.GpOk && value <= int.MaxValue
            ? (int)value
            : throw new Win32Exception(status, $"画像の{name}を取得できませんでした（GDI+ の状態: {status}）。");

    /// <summary>GDI+ の画像のピクセルをコアの画像のバッファへコピーする。</summary>
    /// <param name="image">コピー元の GDI+ の画像。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="destination">コピー先（<c>幅 × 4 × 高さ</c> バイト以上）。</param>
    /// <exception cref="Win32Exception">ピクセルを取得できなかった場合。</exception>
    /// <remarks>
    /// <b>アルファ乗算済みの BGRA8888 を指定してロックする</b>ため、元の画像が別の形式でも GDI+ が変換する。<br/>
    /// <b>経路は 2 つ</b>: 行幅が一致する場合は連続領域として全領域を 1 回でコピーし、一致しない場合は行単位でコピーする
    /// （GDI+ の行幅は 4 バイト境界へ揃えられるため、幅が奇数の画像では一致しない）。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    private static void CopyPixelsFrom(IntPtr image, int width, int height, Span<byte> destination)
    {
        var stride = width * IEsBitmap.BytesPerPixel;
        var rect = new WinNative.GpRect { X = 0, Y = 0, Width = width, Height = height };
        var data = default(WinNative.GpBitmapData);

        var status = WinNative.GdipBitmapLockBits(
            image, ref rect, WinNative.GpImageLockModeRead, WinNative.GpPixelFormat32bppPArgb, ref data);
        if (status != WinNative.GpOk)
        {
            throw new Win32Exception(status, $"画像のピクセルを取得できませんでした（GDI+ の状態: {status}）。");
        }

        try
        {
            var rowBytes = Math.Min(data.Stride, stride);
            unsafe
            {
                var source = (byte*)data.Scan0;
                if (data.Stride == stride)
                {
                    // 行幅が同一の場合は連続領域なので、行単位ではなく全領域を 1 回でコピーする（高速化）
                    new ReadOnlySpan<byte>(source, rowBytes * height).CopyTo(destination);
                }
                else
                {
                    for (var y = 0; y < height; y++)
                    {
                        new ReadOnlySpan<byte>(source + (y * data.Stride), rowBytes)
                            .CopyTo(destination.Slice(y * stride, rowBytes));
                    }
                }
            }
        }
        finally
        {
            WinNative.GdipBitmapUnlockBits(image, ref data);
        }
    }
}
