using System.ComponentModel;
using System.Runtime.Versioning;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>画像のファイル保存・読み込みの実装（WIC を使う Windows 専用）。</summary>
/// <remarks>
/// <b>唯一の実装</b>: 公開面は画像型のメンバー（<see cref="EsReadOnlyBitmap.Save(string)"/> /
/// <see cref="EsBitmap.Save(string)"/> / <see cref="EsReadOnlyBitmap.Load(string)"/> /
/// <see cref="EsBitmap.Load(string)"/>）であり、本クラスはその実装を 1 箇所へ集約する（本層は internal）。<br/>
/// <b>画像型を知らない</b>: 符号化は受け取ったピクセル バイト列を書き
/// （<see cref="EncodeToFile(ReadOnlySpan{byte}, int, int, string)"/>）、復号は生成処理を受け取って
/// デコード結果をその領域へ直接書く（<see cref="DecodeFromFile{T}(string, Func{int, int, Action{Span{byte}}, T})"/>）。<br/>
/// <b>依存分離</b>: ファイル形式（BMP / PNG / JPEG / GIF / TIFF）のエンコード・デコードは OS の
/// WIC（<c>windowscodecs.dll</c>）が担い、<b>外部パッケージ（<c>System.Drawing.Common</c> 等）を参照しない</b>。
/// WIC は OS の一部であるため、追加の配布物も追加のスレッドも要らない。<br/>
/// <b>形式は拡張子が決める</b>: 保存先の拡張子から形式を選び、<b>未対応の拡張子は例外にする</b>
/// （PNG 等へ黙って差し替えると、呼び出し側が意図した形式と違うファイルができて気付かない。
/// 対応形式は <see cref="SupportedExtensions"/> が単一所有する）。<br/>
/// <b>Windows 専用</b>: WIC は Windows でのみ利用可能（<see cref="SupportedOSPlatformAttribute"/> で限定）。<br/>
/// <b>制限</b>: JPEG / BMP はアルファ（透明度）を保存できない（形式の仕様）。 透過を保つ場合は PNG を選ぶこと。
/// </remarks>
[SupportedOSPlatform("windows10.0.17763")]
internal static class WinWicImageCodec
{
    /// <summary>保存に使える拡張子（小文字・先頭のドットを含む）。</summary>
    internal static readonly IReadOnlyList<string> SupportedExtensions = [".bmp", ".png", ".jpg", ".jpeg", ".gif", ".tif", ".tiff"];

    /// <summary>JPEG の品質として許容する最小値。</summary>
    internal const int JpegQualityMinimum = 0;

    /// <summary>JPEG の品質として許容する最大値。</summary>
    internal const int JpegQualityMaximum = 100;

    /// <summary>形式ごとのコンテナ GUID。</summary>
    /// <remarks>
    /// <b>実測値を持つ</b>: <c>IWICImagingFactory::CreateComponentEnumerator</c> で、この環境に登録されている
    /// デコーダーとエンコーダーのコンテナ形式と拡張子を列挙して確かめた値である（既定の 5 形式は
    /// <c>wincodec.h</c> の <c>GUID_ContainerFormat*</c> と一致する）。<br/>
    /// <b>公表値を写さない理由</b>: GUID を 1 文字でも取り違えると
    /// <c>WINCODEC_ERR_COMPONENTNOTFOUND</c>（<c>0x88982F50</c>）になり、原因が分かりにくい。
    /// </remarks>
    private static readonly Dictionary<ImageFileFormat, Guid> s_containerFormats = new()
    {
        [ImageFileFormat.Bmp] = new Guid("0AF1D87E-FCFE-4188-BDEB-A7906471CBE3"),
        [ImageFileFormat.Png] = new Guid("1B7CFAF4-713F-473C-BBCD-6137425FAEAF"),
        [ImageFileFormat.Jpeg] = new Guid("19E4A5AA-5662-4FC5-A0C0-1758028E1057"),
        [ImageFileFormat.Gif] = new Guid("1F8A5601-7D4D-4CBD-9C82-1BC8D4EEB9A5"),
        [ImageFileFormat.Tiff] = new Guid("163BCC30-E2E9-4F0B-961D-A3E9FDB788A3"),
    };

    /// <summary>ピクセル データをファイルへエンコードする（形式は拡張子が決める。 オプションは既定）。</summary>
    /// <param name="pixels">エンコードするピクセル データ（BGRA8888・アルファ乗算済み）。 借用であり書き換えない。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="filePath">保存先のファイルパス（拡張子が形式を決める）。</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合、または
    /// <paramref name="pixels"/> の長さが <c>幅 × 4 × 高さ</c> と一致しない場合。</exception>
    /// <exception cref="NotSupportedException">拡張子が対応形式でない場合（<see cref="SupportedExtensions"/>）。</exception>
    /// <exception cref="Win32Exception">エンコード（WIC）が失敗した場合（状態値を持つ）。</exception>
    /// <remarks>形式ごとの調整値は <see cref="EncodeToFile(ReadOnlySpan{byte}, int, int, string, EsImageSaveOptions)"/> を使う。</remarks>
    internal static void EncodeToFile(ReadOnlySpan<byte> pixels, int width, int height, string filePath)
        => EncodeToFile(pixels, width, height, filePath, default);

    /// <summary>ピクセル データをファイルへエンコードする（形式は拡張子が決める。 オプションを指定できる）。</summary>
    /// <param name="pixels">エンコードするピクセル データ（BGRA8888・アルファ乗算済み）。 借用であり書き換えない。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="filePath">保存先のファイルパス（拡張子が形式を決める。 <see cref="EsImageSaveOptions.Format"/> を指定した場合に拡張子が無ければ補完する）。</param>
    /// <param name="options">保存のオプション（既定の場合は <c>default</c>）。</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合、
    /// <paramref name="pixels"/> の長さが <c>幅 × 4 × 高さ</c> と一致しない場合、または
    /// 拡張子と <paramref name="options"/> の形式が食い違い <see cref="EsImageSaveOptions.IgnoreFormatMismatch"/> が false の場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="options"/> の値が許容範囲外の場合。</exception>
    /// <exception cref="NotSupportedException">拡張子が対応形式でない場合（<see cref="SupportedExtensions"/>）、または
    /// <paramref name="options"/> にその形式で使えない項目が設定されている場合。</exception>
    /// <exception cref="Win32Exception">エンコード（WIC）が失敗した場合（状態値を持つ）。</exception>
    /// <remarks>
    /// <b>保存形式は拡張子から決める</b>（<c>.bmp</c> / <c>.png</c> / <c>.jpg</c> / <c>.jpeg</c> /
    /// <c>.gif</c> / <c>.tif</c> / <c>.tiff</c>）。 <see cref="EsImageSaveOptions.Format"/> を指定した場合は
    /// <b>形式の方が優先</b>され、<see cref="ResolveTarget"/> の規則で保存先が決まる。<br/>
    /// <b>減色する場合と GIF は 8bpp のインデックス カラーとして書く</b>（<see cref="ResolveColorCount"/>）。<br/>
    /// <b>JPEG / BMP はアルファを保存できない</b>（透過を保つ場合は PNG を選ぶ）。
    /// </remarks>
    internal static void EncodeToFile(ReadOnlySpan<byte> pixels, int width, int height, string filePath, EsImageSaveOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var stride = width * EsReadOnlyBitmap.BytesPerPixel;
        var expected = (long)stride * height;
        if (pixels.Length != expected)
        {
            throw new ArgumentException(
                $"ピクセル データの長さが寸法と一致しません（{width}x{height} は {expected} バイト。 実際は {pixels.Length} バイト）。",
                nameof(pixels));
        }

        var (format, targetPath) = ResolveTarget(filePath, options);

        // 減色する場合、および GIF（パレットが必須の形式）は 8bpp のインデックス カラーとして書く
        if (ResolveColorCount(format, options) is int colorCount)
        {
            EncodeIndexedToFile(
                pixels, width, height, targetPath, format, colorCount,
                options.ColorQuality ?? EsImageQuality.Mid,
                options.Dither ?? EsDitherMode.None);
            return;
        }

        var bytes = EncodeToBytes(format, width, height, options, palette: null, pixels, indexed: false);
        File.WriteAllBytes(targetPath, bytes);
    }

    /// <summary>減色したピクセルを 8bpp のインデックス カラーとしてファイルへ書き出す。</summary>
    /// <param name="pixels">エンコードするピクセル データ（BGRA8888・アルファ乗算済み）。 借用であり書き換えない。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="filePath">保存先のファイルパス（形式は決定済み）。</param>
    /// <param name="format">保存する形式（パレットを持てる形式であることは検証済み）。</param>
    /// <param name="colorCount">減色して残す色数。</param>
    /// <param name="quality">減色の品質。</param>
    /// <param name="dither">減色のディザ。</param>
    /// <exception cref="Win32Exception">エンコード（WIC）が失敗した場合。</exception>
    /// <remarks>
    /// <b>なぜ 8bpp で書くのか</b>: 32bpp のまま減色しても、エンコーダーはパレット画像を書かない
    /// （減色の効果は圧縮率の向上だけであり、BMP では 1 バイトも減らない）。
    /// <b>8bpp のインデックス カラー（パレット付き）を渡すとパレット画像になる</b>。<br/>
    /// <b>GIF もここを通る</b>（<see cref="ResolveColorCount"/> を参照。 GIF はパレットが必須であり、
    /// 自前の量子化は透明な画素を透明なパレットの色へ割り当てるため透過が保たれる）。<br/>
    /// <b>行幅を 4 バイト境界へ揃える</b>: BMP の行は 4 バイト境界へ揃えられるため、揃えないと
    /// 読み戻した画像が斜めにずれる。
    /// </remarks>
    private static void EncodeIndexedToFile(
        ReadOnlySpan<byte> pixels, int width, int height, string filePath, ImageFileFormat format, int colorCount,
        EsImageQuality quality, EsDitherMode dither)
    {
        var (indices, palette) = EsBitmapOperations.ReduceToIndexed(
            pixels, width, height, width * EsReadOnlyBitmap.BytesPerPixel, colorCount, quality, dither);

        var stride = (width + 3) & ~3;
        var rows = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            indices.AsSpan(y * width, width).CopyTo(rows.AsSpan(y * stride, width));
        }

        var bytes = EncodeToBytes(format, width, height, default, palette, rows, indexed: true);
        File.WriteAllBytes(filePath, bytes);
    }

    /// <summary>ピクセルを WIC でエンコードし、ファイルへ書く内容を返す。</summary>
    /// <param name="format">保存する形式。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="options">保存のオプション（JPEG の品質を書き込む）。</param>
    /// <param name="palette">パレット（インデックス カラーでない場合は null）。</param>
    /// <param name="pixels">書き込む画素（<paramref name="indexed"/> が true の場合は 8bpp のインデックス）。</param>
    /// <param name="indexed">8bpp のインデックス カラーとして書く場合は true。</param>
    /// <returns>エンコードした内容。</returns>
    /// <exception cref="NotSupportedException">その形式を書き出せるエンコーダーが無い場合、または
    /// パレットを持てない形式へパレットを渡した場合。</exception>
    /// <exception cref="Win32Exception">エンコード（WIC）が失敗した場合。</exception>
    /// <remarks>
    /// <b>メモリ ストリームへ書いてからファイルへ書く理由</b>: COM の解放は GC の時期に依存するため、
    /// WIC にファイルを開かせると解放までファイルが開いたままになる（実測: 直後の削除が共有違反で失敗した）。
    /// メモリ ストリームならファイルは呼び出し側が開いて閉じるため、直後から削除も上書きもできる。<br/>
    /// <b>エンコーダーの初期化は 1 回だけ</b>である（<c>IWICBitmapEncoder::Initialize</c> を 2 回呼ぶと失敗する）。
    /// そのため、ここで初期化・フレーム作成・オプション設定・寸法・パレット・画素の書き込みをまとめて行う。<br/>
    /// <b>画素の書き込みを処理として受け取らない理由</b>: 画素は <see cref="ReadOnlySpan{T}"/>（ref 構造体）であり、
    /// ラムダへ捕捉できないため。
    /// </remarks>
    private static unsafe byte[] EncodeToBytes(
        ImageFileFormat format, int width, int height, in EsImageSaveOptions options, uint[]? palette,
        ReadOnlySpan<byte> pixels, bool indexed)
    {
        var factory = OpenFactory();
        var stream = WinWicFactory.CreateMemoryStream();
        var encoder = CreateEncoderFor(factory, format);

        WinWicFactory.ThrowIfFailed(
            encoder.Initialize(stream, WinWicInterfaces.EncoderCacheTempFile),
            "エンコーダーを初期化できませんでした。");
        WinWicFactory.ThrowIfFailed(
            encoder.CreateNewFrame(out var frame, out var properties),
            "画像のフレームを作成できませんでした。");
        var instance = frame!;

        if (options.JpegQuality is int quality)
        {
            // WIC の品質は 0.0〜1.0 の実数である（公開面は 0〜100 の整数）
            WinWicFactory.ThrowIfFailed(
                WinWicFactory.WriteRealOption(properties!, "ImageQuality", quality / (float)JpegQualityMaximum),
                "JPEG の品質を設定できませんでした。");
        }

        WinWicFactory.ThrowIfFailed(instance.Initialize(properties), "画像のフレームを初期化できませんでした。");
        WinWicFactory.ThrowIfFailed(
            instance.SetSize((uint)width, (uint)height),
            $"画像の寸法（{width}x{height}）を設定できませんでした。");

        if (palette is not null)
        {
            WinWicFactory.ThrowIfFailed(factory.CreatePalette(out var colors), "パレットを作成できませんでした。");
            fixed (uint* values = palette)
            {
                WinWicFactory.ThrowIfFailed(
                    colors!.InitializeCustom(values, (uint)palette.Length),
                    "パレットの色を設定できませんでした。");
            }

            WinWicFactory.ThrowIfFailed(instance.SetPalette(colors), "パレットを画像へ設定できませんでした。");
        }

        if (indexed)
        {
            var indexFormat = WinWicFactory.PixelFormat8bppIndexed;
            WinWicFactory.ThrowIfFailed(
                instance.SetPixelFormat(ref indexFormat),
                $"減色した画像のピクセル形式を設定できませんでした（{ToPublicName(format)}）。");
            if (indexFormat != WinWicFactory.PixelFormat8bppIndexed)
            {
                throw new NotSupportedException(
                    $"この環境の {ToPublicName(format)} エンコーダーはパレット画像を書けません（{indexFormat}）。");
            }

            WritePixelsCore(instance, height, (width + 3) & ~3, pixels, format);
        }
        else
        {
            var pixelFormat = WinWicFactory.PixelFormat32bppPBgra;
            WinWicFactory.ThrowIfFailed(
                instance.SetPixelFormat(ref pixelFormat),
                $"保存する画像のピクセル形式を設定できませんでした（{ToPublicName(format)}）。");

            WritePixels(instance, width, height, pixels, pixelFormat, format);
        }

        WinWicFactory.ThrowIfFailed(instance.Commit(), "画像を確定できませんでした。");

        // ストリームが生きているうちに確定する（確定してから解放すると内容が書かれない）
        WinWicFactory.ThrowIfFailed(encoder.Commit(), "エンコードした内容を確定できませんでした。");
        return WinWicFactory.ReadStream(stream);
    }

    /// <summary>WIC のイメージング ファクトリを生成する。</summary>
    /// <returns>生成したファクトリ。</returns>
    /// <exception cref="Win32Exception">WIC を使えない場合。</exception>
    private static IWICImagingFactory OpenFactory()
        => WinWicFactory.CreateFactory()
            ?? throw new Win32Exception(0, "WIC（Windows Imaging Component）を使えませんでした。");

    /// <summary>形式を書き出せるエンコーダーを作る。</summary>
    /// <param name="factory">イメージング ファクトリ。</param>
    /// <param name="format">保存する形式。</param>
    /// <returns>生成したエンコーダー。</returns>
    /// <exception cref="NotSupportedException">その形式を書き出せるエンコーダーが無い場合。</exception>
    private static IWICBitmapEncoder CreateEncoderFor(IWICImagingFactory factory, ImageFileFormat format)
    {
        var containerFormat = s_containerFormats[format];
        if (factory.CreateEncoder(in containerFormat, IntPtr.Zero, out var encoder) >= 0 && encoder is not null)
        {
            return encoder;
        }

        throw new NotSupportedException(
            $"この環境には {ToPublicName(format)} 形式を書き出すエンコーダーがありません。");
    }

    /// <summary>エンコーダーが受け付けたピクセル形式へ直してから画素を書き込む。</summary>
    /// <param name="frame">対象のフレーム。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="pixels">画素（BGRA8888・アルファ乗算済み。 行幅は密）。</param>
    /// <param name="pixelFormat"><c>SetPixelFormat</c> が返したピクセル形式。</param>
    /// <param name="format">保存する形式（例外メッセージ用）。</param>
    /// <exception cref="NotSupportedException">想定していないピクセル形式が返された場合。</exception>
    /// <exception cref="Win32Exception">書き込めなかった場合。</exception>
    /// <remarks>
    /// <b>要求と返答が違う（実測）</b>: アルファ乗算済みの BGRA を要求しても、PNG / TIFF は
    /// アルファ乗算なしの BGRA、JPEG は 24bpp の BGR を返す（JPEG はアルファを持てない形式である）。
    /// そのため<b>返された形式に合わせて並びを直してから書く</b>（エンコーダーは形式の変換をしない）。
    /// BMP だけは要求どおりアルファ乗算済みを受け付けるため、コピーを作らない。<br/>
    /// <b>変換器（<c>IWICFormatConverter</c>）を使わない理由（実測）</b>: 変換器へ渡す画素の源は
    /// <c>IWICImagingFactory::CreateBitmapFromMemory</c> でしか作れないが、この API は
    /// <b><c>[GeneratedComInterface]</c> 経由でも生の vtable 呼び出しでもアクセス違反で落ちる</b>
    /// （同じ引数・同じプロセスでも <c>[ComImport]</c> 経由なら成功する。 Native AOT では
    /// <c>[ComImport]</c> を使えない）。 そのため並びの変換だけは自前で行う。
    /// </remarks>
    private static void WritePixels(
        IWICBitmapFrameEncode frame, int width, int height, ReadOnlySpan<byte> pixels, Guid pixelFormat,
        ImageFileFormat format)
    {
        if (pixelFormat == WinWicFactory.PixelFormat32bppPBgra)
        {
            WritePixelsCore(frame, height, width * EsReadOnlyBitmap.BytesPerPixel, pixels, format);
            return;
        }

        if (pixelFormat == WinWicFactory.PixelFormat32bppBgra)
        {
            WritePixelsCore(frame, height, width * EsReadOnlyBitmap.BytesPerPixel, Unpremultiply(pixels), format);
            return;
        }

        if (pixelFormat == WinWicFactory.PixelFormat24bppBgr)
        {
            WritePixelsCore(frame, height, width * 3, ToBgr24(pixels, width, height), format);
            return;
        }

        throw new NotSupportedException(
            $"この環境の {ToPublicName(format)} エンコーダーは想定外のピクセル形式を要求しました（{pixelFormat}）。");
    }

    /// <summary>行幅を指定して画素を書き込む。</summary>
    /// <param name="frame">対象のフレーム。</param>
    /// <param name="height">行数。</param>
    /// <param name="stride">行幅（バイト）。</param>
    /// <param name="pixels">画素（行幅が密な並び）。</param>
    /// <param name="format">保存する形式（例外メッセージ用）。</param>
    /// <exception cref="Win32Exception">書き込めなかった場合。</exception>
    private static unsafe void WritePixelsCore(IWICBitmapFrameEncode frame, int height, int stride, ReadOnlySpan<byte> pixels, ImageFileFormat format)
    {
        fixed (byte* source = pixels)
        {
            WinWicFactory.ThrowIfFailed(
                frame.WritePixels((uint)height, (uint)stride, (uint)pixels.Length, source),
                $"{ToPublicName(format)} 形式の画素を書き込めませんでした。");
        }
    }

    /// <summary>アルファ乗算済みの画素を、乗算していない並びへ直す。</summary>
    /// <param name="pixels">アルファ乗算済みの画素（BGRA）。</param>
    /// <returns>乗算していない画素（BGRA）。</returns>
    /// <remarks>
    /// <b>必要な場合だけ呼ぶ</b>: エンコーダーがアルファ乗算済みを受け付ける場合はコピーを作らない
    /// （受け付けるかどうかは <c>SetPixelFormat</c> が返した形式で判定する）。
    /// </remarks>
    private static byte[] Unpremultiply(ReadOnlySpan<byte> pixels)
    {
        var straight = pixels.ToArray();
        for (var i = 0; i < straight.Length; i += EsReadOnlyBitmap.BytesPerPixel)
        {
            var alpha = straight[i + 3];
            if (alpha is 0 or 255)
            {
                continue;
            }

            for (var channel = 0; channel < 3; channel++)
            {
                var value = (straight[i + channel] * 255) + (alpha / 2);
                straight[i + channel] = (byte)Math.Min(255, value / alpha);
            }
        }

        return straight;
    }

    /// <summary>アルファ乗算済みの BGRA を 24bpp の BGR へ直す。</summary>
    /// <param name="pixels">アルファ乗算済みの画素（BGRA）。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>BGR の画素（行幅は <c>幅 × 3</c>）。</returns>
    /// <remarks>アルファを持てない形式（JPEG）向けに、乗算を解いてからアルファを落とす。</remarks>
    private static byte[] ToBgr24(ReadOnlySpan<byte> pixels, int width, int height)
    {
        var straight = Unpremultiply(pixels);
        var result = new byte[width * 3 * height];
        for (var i = 0; i < width * height; i++)
        {
            result[(i * 3) + 0] = straight[(i * 4) + 0];
            result[(i * 3) + 1] = straight[(i * 4) + 1];
            result[(i * 3) + 2] = straight[(i * 4) + 2];
        }

        return result;
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
    private static ImageFileFormat ResolveExtension(string extension, in EsImageSaveOptions options)
    {
        var format = TryResolveExtension(extension) ?? throw CreateUnsupportedExtension(extension);
        ValidateOptions(format, options);
        return format;
    }

    /// <summary>拡張子から形式を求める（未対応の場合は null）。</summary>
    /// <param name="extension">拡張子（先頭のドットを含む。 空の場合もある）。</param>
    /// <returns>形式。 未対応の場合は null。</returns>
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
    /// <exception cref="InvalidOperationException">減色しない保存へ減色の指定（品質・ディザ）が設定されている場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException">値が許容範囲外の場合。</exception>
    /// <remarks>
    /// <b>品質を使えるのは JPEG だけである</b>（他の形式は品質を受け取っても出力が変わらない）。
    /// <b>減色を使えるのは BMP / PNG / GIF だけである</b>（パレットを持てる形式）。
    /// <b>減色の品質とディザは減色が行われる場合だけ意味を持つ</b>（<see cref="EsImageSaveOptions.ColorCount"/> を
    /// 指定した場合、または GIF へ保存する場合）。 黙って無視すると「設定したのに効かない」ことに気付かないため、
    /// 例外にする。
    /// </remarks>
    private static void ValidateOptions(ImageFileFormat format, in EsImageSaveOptions options)
    {
        if (options.ColorCount is int colorCount)
        {
            if (!SupportsPalette(format))
            {
                throw CreateUnsupportedOption(format, nameof(EsImageSaveOptions.ColorCount));
            }

            if (colorCount < EsBitmapOperations.MinimumColorCount || colorCount > EsBitmapOperations.MaximumColorCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    colorCount,
                    $"減色して残す色数は {EsBitmapOperations.MinimumColorCount}〜{EsBitmapOperations.MaximumColorCount} の範囲で指定します。");
            }
        }

        if (options.ColorQuality is EsImageQuality quality
            && quality is not (EsImageQuality.Low or EsImageQuality.Mid or EsImageQuality.High))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options), quality, "定義されていない減色の品質の指定です。");
        }

        if (options.Dither is EsDitherMode dither
            && dither is not (EsDitherMode.None or EsDitherMode.FloydSteinberg))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options), dither, "定義されていないディザの指定です。");
        }

        // 減色の指定（品質・ディザ）は減色が行われる場合だけ意味を持つ（GIF は指定が無くても量子化される）
        if (options.ColorCount is null && format != ImageFileFormat.Gif)
        {
            if (options.ColorQuality is not null)
            {
                throw CreateReductionRequired(nameof(EsImageSaveOptions.ColorQuality));
            }

            if (options.Dither is not null)
            {
                throw CreateReductionRequired(nameof(EsImageSaveOptions.Dither));
            }
        }

        if (options.JpegQuality is not int jpegQuality)
        {
            return;
        }

        if (format != ImageFileFormat.Jpeg)
        {
            throw CreateUnsupportedOption(format, nameof(EsImageSaveOptions.JpegQuality));
        }

        if (jpegQuality < JpegQualityMinimum || jpegQuality > JpegQualityMaximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options), jpegQuality, $"JPEG の品質は {JpegQualityMinimum}〜{JpegQualityMaximum} の範囲で指定します。");
        }
    }

    /// <summary>減色しない保存へ減色の指定（品質・ディザ）を渡された場合の例外を作る。</summary>
    /// <param name="optionName">指定されたオプションの名前。</param>
    /// <returns>送出する例外。</returns>
    /// <remarks>
    /// <b>黙って無視しない理由</b>: 本型の他の項目と同じ規則である（設定したのに効かないことに気付かないため）。
    /// 例外の種類は「その組み合わせでは使えない」を表す <see cref="InvalidOperationException"/> にする
    /// （形式そのものが使えない場合は <see cref="NotSupportedException"/> である）。
    /// </remarks>
    private static InvalidOperationException CreateReductionRequired(string optionName)
        => new($"オプション '{optionName}' は減色する場合だけ使えます"
            + $"（{nameof(EsImageSaveOptions.ColorCount)} を指定するか、GIF へ保存してください）。");

    /// <summary>その形式がパレット（インデックス カラー）を持てるかを求める。</summary>
    /// <param name="format">保存する形式。</param>
    /// <returns>持てる場合 true。</returns>
    /// <remarks>
    /// <b>実測で確認した 3 形式だけを返す</b>: 8bpp のインデックス カラーを渡して意図どおりのパレット画像になることを
    /// 確かめたのは BMP / PNG / GIF である。 JPEG はパレットを持てない（必ず減色結果が壊れる）。
    /// TIFF はパレットを持てる形式だが<b>未検証</b>のため受け付けない（未検証の組み合わせを黙って通さない）。
    /// </remarks>
    private static bool SupportsPalette(ImageFileFormat format)
        => format is ImageFileFormat.Bmp or ImageFileFormat.Png or ImageFileFormat.Gif;

    /// <summary>量子化して 8bpp のインデックス カラーとして書く場合の色数を求める。</summary>
    /// <param name="format">保存する形式。</param>
    /// <param name="options">保存のオプション（検証済み）。</param>
    /// <returns>残す色数。 量子化しない場合は null。</returns>
    /// <remarks>
    /// <b>GIF は指定が無くても常に量子化する</b>: GIF はパレットが必須の形式である。
    /// 自前の量子化は透明な画素を透明なパレットの色へ割り当てるため、透過が保たれる。
    /// 副次的に<b>ファイルも小さく画質も良くなる</b>。
    /// </remarks>
    private static int? ResolveColorCount(ImageFileFormat format, in EsImageSaveOptions options)
        => options.ColorCount is int colorCount
            ? colorCount
            : format == ImageFileFormat.Gif ? EsBitmapOperations.MaximumColorCount : null;

    /// <summary>形式を表す enum を内部の形式へ変換する。</summary>
    /// <param name="format">公開の形式。</param>
    /// <returns>内部の形式。</returns>
    /// <exception cref="ArgumentOutOfRangeException">定義されていない値の場合。</exception>
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
    private static NotSupportedException CreateUnsupportedExtension(string extension)
        => new(extension.Length == 0
            ? $"拡張子が無いため保存形式を決められません。 拡張子を付けるか {nameof(EsImageSaveOptions)}.{nameof(EsImageSaveOptions.Format)} を指定してください。"
                + $" 対応形式: {string.Join(" / ", SupportedExtensions)}"
            : $"拡張子 '{extension}' へは保存できません。 対応形式: {string.Join(" / ", SupportedExtensions)}");

    /// <summary>形式ごとの標準の拡張子（拡張子が無い場合の補完に使う）。</summary>
    /// <param name="format">保存する形式。</param>
    /// <returns>先頭のドットを含む拡張子。</returns>
    /// <remarks>別名（<c>.jpeg</c> / <c>.tiff</c>）ではなく、対応形式の一覧の先頭にある値を返す。</remarks>
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

    /// <summary>その形式で使えないオプションが設定されたことを表す例外を作る。</summary>
    /// <param name="format">保存する形式。</param>
    /// <param name="optionName">使えないオプションの名前。</param>
    /// <returns>例外。</returns>
    private static NotSupportedException CreateUnsupportedOption(ImageFileFormat format, string optionName)
        => new($"オプション '{optionName}' は {ToPublicName(format)} 形式では使えません"
            + $"（現在オプションを持つのは JPEG の {nameof(EsImageSaveOptions.JpegQuality)} だけです）。");

    /// <summary>保存する形式。</summary>
    /// <remarks>WIC のコンテナ形式と 1 対 1 に対応する（拡張子の一覧は <see cref="SupportedExtensions"/> が単一所有する）。</remarks>
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
    /// <typeparam name="T">生成する画像の型（<see cref="EsReadOnlyBitmap"/> または <see cref="EsBitmap"/>）。</typeparam>
    /// <param name="filePath">読み込むファイルのパス（形式は内容から判別される）。</param>
    /// <param name="create">寸法と充填処理を受け取って画像を生成する処理（デコード結果をこの充填処理で
    /// 受け取った領域へ直接書くため、中間バッファも追加のコピーも要らない）。</param>
    /// <returns>生成された画像。 所有権は呼び出し側へ移る。</returns>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合、ファイルが存在しない場合、
    /// または画像として解釈できない場合（WIC はどちらも同じ状態値を返すため区別しない）。</exception>
    /// <remarks>元の寸法でデコードする（縮小読み込みは <see cref="DecodeFromFile{T}(string, int, Func{int, int, Action{Span{byte}}, T})"/> を使う）。</remarks>
    internal static T DecodeFromFile<T>(string filePath, Func<int, int, Action<Span<byte>>, T> create)
        => DecodeFromFile(filePath, 0, create);

    /// <summary>ファイルをデコードし、長辺が指定サイズを超えない画像として返す（形式は内容から判別される）。</summary>
    /// <typeparam name="T">生成する画像の型（<see cref="EsReadOnlyBitmap"/> または <see cref="EsBitmap"/>）。</typeparam>
    /// <param name="filePath">読み込むファイルのパス（形式は内容から判別される）。</param>
    /// <param name="maxSize">長辺の上限（ピクセル）。 0 以下の場合は元の寸法でデコードする（拡大はしない）。</param>
    /// <param name="create">寸法と充填処理を受け取って画像を生成する処理（デコード結果をこの充填処理で
    /// 受け取った領域へ直接書くため、中間バッファも追加のコピーも要らない）。</param>
    /// <returns>生成された画像。 所有権は呼び出し側へ移る。</returns>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合、ファイルが存在しない場合、
    /// または画像として解釈できない場合（WIC はどちらも同じ状態値を返すため区別しない）。</exception>
    /// <remarks>
    /// <b>画像型を知らない</b>: 本メソッドは生成処理を受け取るだけで、画像型（<c>EsReadOnlyBitmap</c> 等）を名指ししない。<br/>
    /// <b><paramref name="create"/> は充填処理を 1 回だけ呼ぶ責任を持つ</b>。<br/>
    /// <b>ピクセルは 1 回だけ書かれる</b>: 中間バッファを作らず、WIC の変換結果を画像のバッファへ直接コピーする。<br/>
    /// <b>縮小は WIC のスケーラー（<c>IWICBitmapScaler</c>）で行う</b>: 原寸のバッファを確保せず、
    /// デコードしながら目標の寸法へ縮小する（4K の画像を多数並べるときのメモリと時間を抑える）。
    /// 補間は <c>Fant</c>（画素の面積平均に近い）であり、WinUI 3 版と同じ画質になる。<br/>
    /// <b>拡大はしない</b>（元の寸法が上限以下なら元の寸法で返す）。<br/>
    /// <b>形式は内容から判別する</b>（拡張子と内容が一致しない場合は内容に従う）。<br/>
    /// <b>ピクセルはアルファ乗算済みの BGRA8888 へ変換して取り込む</b>（元の形式が異なっても WIC が変換する）。
    /// </remarks>
    internal static T DecodeFromFile<T>(string filePath, int maxSize, Func<int, int, Action<Span<byte>>, T> create)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var factory = OpenFactory();

        // メモリ ストリームから読む理由: WIC にファイルを開かせると COM の解放（GC）まで掴んだままになる
        var stream = WinWicFactory.CreateMemoryStream(ReadAllBytes(filePath));
        if (factory.CreateDecoderFromStream(stream, IntPtr.Zero, 0, out var decoder) < 0 || decoder is null)
        {
            throw new ArgumentException($"'{filePath}' を画像として読み込めませんでした。", nameof(filePath));
        }

        if (decoder.GetFrame(0, out var frame) < 0 || frame is null)
        {
            throw new ArgumentException($"'{filePath}' の画像を取得できませんでした。", nameof(filePath));
        }

        // 回転（EXIF の向き）→ 縮小 → 画素形式の変換の順に挟む（拡大はしない）
        var oriented = WinWicFactory.ApplyOrientation(factory, frame);
        var scaled = WinWicFactory.Scale(factory, oriented, maxSize);
        var source = WinWicFactory.Convert(factory, scaled, WinWicFactory.PixelFormat32bppPBgra);

        var (width, height) = WinWicFactory.GetSize(source);
        return create(width, height, destination => WinWicFactory.CopyPixels(source, width, height, destination));
    }

    /// <summary>ファイルの内容を読み込む。</summary>
    /// <param name="filePath">対象のファイル。</param>
    /// <returns>内容。</returns>
    /// <exception cref="ArgumentException">ファイルが存在しない場合、または読み込めない場合。</exception>
    /// <remarks>
    /// <b>WIC にファイルを開かせない理由</b>: COM の解放は GC の時期に依存するため、WIC にファイルを開かせると
    /// <b>解放までファイルが掴んだまま</b>になる（実測: 削除が共有違反で失敗した）。
    /// <see cref="File"/> で読めば開いた直後に閉じるため、読み込み中でも削除も上書きもできる。
    /// </remarks>
    private static byte[] ReadAllBytes(string filePath)
    {
        try
        {
            return File.ReadAllBytes(filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // 存在しない・権限が無い・パスとして不正などの失敗は「画像として読めない」へ翻訳する
            throw new ArgumentException($"'{filePath}' を画像として読み込めませんでした。", nameof(filePath), ex);
        }
    }
}
