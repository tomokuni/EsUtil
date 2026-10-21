using System.ComponentModel;
using System.Runtime.Versioning;
using EsUtil.Platform.Core.Imaging;

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
[SupportedOSPlatform("windows")]
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

        var factory = OpenFactory();
        try
        {
            var encoder = CreateEncoderFor(factory, format);
            try
            {
                var stream = CreateStreamFor(factory, targetPath);
                try
                {
                    var frame = CreateFrame(factory, encoder, stream, width, height, options, palette: null, out var properties);
                    try
                    {
                        var pixelFormat = WinNative.WicPixelFormat32bppPBgra;
                        WinWicCom.ThrowIfFailed(
                            WinWicCom.SetFramePixelFormat(frame, ref pixelFormat),
                            $"保存する画像のピクセル形式を設定できませんでした（{ToPublicName(format)}）。");

                        WritePixels(frame, width, height, pixels, pixelFormat, format);
                        WinWicCom.ThrowIfFailed(WinWicCom.CommitFrame(frame), $"'{targetPath}' の画像を確定できませんでした。");

                        // ストリームが生きているうちに確定する（確定してから解放すると内容が書かれない）
                        WinWicCom.ThrowIfFailed(WinWicCom.CommitEncoder(encoder), $"'{targetPath}' へ保存できませんでした。");
                    }
                    finally
                    {
                        WinWicCom.Release(frame);
                        WinWicCom.Release(properties);
                    }
                }
                finally
                {
                    WinWicCom.Release(stream);
                }
            }
            finally
            {
                WinWicCom.Release(encoder);
            }
        }
        finally
        {
            WinWicCom.Release(factory);
        }
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

        var factory = OpenFactory();
        try
        {
            var encoder = CreateEncoderFor(factory, format);
            try
            {
                var stream = CreateStreamFor(factory, filePath);
                try
                {
                    var frame = CreateFrame(factory, encoder, stream, width, height, default, palette, out var properties);
                    try
                    {
                        var pixelFormat = WinNative.WicPixelFormat8bppIndexed;
                        WinWicCom.ThrowIfFailed(
                            WinWicCom.SetFramePixelFormat(frame, ref pixelFormat),
                            $"減色した画像のピクセル形式を設定できませんでした（{ToPublicName(format)}）。");

                        if (pixelFormat != WinNative.WicPixelFormat8bppIndexed)
                        {
                            throw new NotSupportedException(
                                $"この環境の {ToPublicName(format)} エンコーダーはパレット画像を書けません（{pixelFormat}）。");
                        }

                        WritePixelsCore(frame, height, stride, rows, format);
                        WinWicCom.ThrowIfFailed(WinWicCom.CommitFrame(frame), $"'{filePath}' の画像を確定できませんでした。");

                        // ストリームが生きているうちに確定する（確定してから解放すると内容が書かれない）
                        WinWicCom.ThrowIfFailed(WinWicCom.CommitEncoder(encoder), $"'{filePath}' へ保存できませんでした。");
                    }
                    finally
                    {
                        WinWicCom.Release(frame);
                        WinWicCom.Release(properties);
                    }
                }
                finally
                {
                    WinWicCom.Release(stream);
                }
            }
            finally
            {
                WinWicCom.Release(encoder);
            }
        }
        finally
        {
            WinWicCom.Release(factory);
        }
    }

    /// <summary>WIC のイメージング ファクトリを生成する。</summary>
    /// <returns>生成したファクトリ。 解放が必要。</returns>
    /// <exception cref="Win32Exception">WIC を使えない場合。</exception>
    private static IntPtr OpenFactory()
    {
        var factory = WinNative.CreateImagingFactory();
        return factory != IntPtr.Zero
            ? factory
            : throw new Win32Exception(0, "WIC（Windows Imaging Component）を使えませんでした。");
    }

    /// <summary>形式を書き出せるエンコーダーを作る。</summary>
    /// <param name="factory">イメージング ファクトリ。</param>
    /// <param name="format">保存する形式。</param>
    /// <returns>生成したエンコーダー。 解放が必要。</returns>
    /// <exception cref="NotSupportedException">その形式を書き出せるエンコーダーが無い場合。</exception>
    private static IntPtr CreateEncoderFor(IntPtr factory, ImageFileFormat format)
    {
        if (WinNative.Succeeded(WinWicCom.CreateEncoder(factory, s_containerFormats[format], out var encoder)))
        {
            return encoder;
        }

        throw new NotSupportedException(
            $"この環境には {ToPublicName(format)} 形式を書き出すエンコーダーがありません。");
    }

    /// <summary>ファイルへ結び付けた書き込み用のストリームを作る。</summary>
    /// <param name="factory">イメージング ファクトリ。</param>
    /// <param name="filePath">保存先のファイルパス。</param>
    /// <returns>生成したストリーム。 解放が必要。</returns>
    /// <exception cref="Win32Exception">ファイルを開けなかった場合、またはストリームを作れなかった場合。</exception>
    private static IntPtr CreateStreamFor(IntPtr factory, string filePath)
    {
        WinWicCom.ThrowIfFailed(WinWicCom.CreateStream(factory, out var stream), $"'{filePath}' を書き込み用に開けませんでした。");
        try
        {
            WinWicCom.ThrowIfFailed(
                WinWicCom.InitializeStreamForWrite(stream, filePath),
                $"'{filePath}' を書き込み用に開けませんでした。");
            return stream;
        }
        catch
        {
            // 失敗したストリームを解放してから呼び出し側へ伝える（解放しないとファイルが開いたままになる）
            WinWicCom.Release(stream);
            throw;
        }
    }

    /// <summary>エンコーダーへフレームを作り、寸法・パレット・オプションを設定する。</summary>
    /// <param name="factory">イメージング ファクトリ。</param>
    /// <param name="encoder">対象のエンコーダー。</param>
    /// <param name="stream">書き込み先のストリーム。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="options">保存のオプション（品質を書き込む）。</param>
    /// <param name="palette">パレット（インデックス カラーでない場合は <c>null</c>）。</param>
    /// <param name="properties">エンコーダーが用意したオプション（解放が必要）。</param>
    /// <returns>生成したフレーム（解放が必要）。</returns>
    /// <exception cref="Win32Exception">エンコーダーを初期化できなかった場合。</exception>
    /// <remarks>
    /// <b>エンコーダーの初期化は 1 回だけ</b>である（<c>IWICBitmapEncoder::Initialize</c> を 2 回呼ぶと失敗する）。
    /// そのため、ここで初期化・フレーム作成・オプション設定・寸法・パレットをまとめて行う。<br/>
    /// <b>返した 2 つは呼び出し側が解放する</b>: 解放しないとエンコーダーとストリームが生き残り、
    /// <b>保存したファイルが開いたまま</b>になる（実測: 直後の読み込みや削除が共有違反で失敗した）。
    /// </remarks>
    private static IntPtr CreateFrame(
        IntPtr factory, IntPtr encoder, IntPtr stream, int width, int height, in EsImageSaveOptions options,
        uint[]? palette, out IntPtr properties)
    {
        WinWicCom.ThrowIfFailed(WinWicCom.InitializeEncoder(encoder, stream), "エンコーダーを初期化できませんでした。");
        WinWicCom.ThrowIfFailed(
            WinWicCom.CreateNewFrame(encoder, out var frame, out properties),
            "画像のフレームを作成できませんでした。");

        if (options.JpegQuality is int quality)
        {
            // WIC の品質は 0.0〜1.0 の実数である（公開面は 0〜100 の整数）
            WinWicCom.ThrowIfFailed(
                WinWicCom.WriteRealOption(properties, "ImageQuality", quality / (float)JpegQualityMaximum),
                "JPEG の品質を設定できませんでした。");
        }

        WinWicCom.ThrowIfFailed(WinWicCom.InitializeFrame(frame, properties), "画像のフレームを初期化できませんでした。");
        WinWicCom.ThrowIfFailed(
            WinWicCom.SetFrameSize(frame, (uint)width, (uint)height),
            $"画像の寸法（{width}x{height}）を設定できませんでした。");

        if (palette is null)
        {
            return frame;
        }

        WinWicCom.ThrowIfFailed(WinWicCom.CreatePalette(factory, out var instance), "パレットを作成できませんでした。");
        try
        {
            WinWicCom.ThrowIfFailed(
                WinWicCom.InitializePalette(instance, palette),
                "パレットの色を設定できませんでした。");
            WinWicCom.ThrowIfFailed(WinWicCom.SetFramePalette(frame, instance), "パレットを画像へ設定できませんでした。");
        }
        finally
        {
            WinWicCom.Release(instance);
        }

        return frame;
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
    /// BMP だけは要求どおりアルファ乗算済みを受け付けるため、コピーを作らない。
    /// </remarks>
    private static void WritePixels(IntPtr frame, int width, int height, ReadOnlySpan<byte> pixels, Guid pixelFormat, ImageFileFormat format)
    {
        if (pixelFormat == WinNative.WicPixelFormat32bppPBgra)
        {
            WritePixelsCore(frame, height, width * EsReadOnlyBitmap.BytesPerPixel, pixels, format);
            return;
        }

        if (pixelFormat == WinNative.WicPixelFormat32bppBgra)
        {
            WritePixelsCore(frame, height, width * EsReadOnlyBitmap.BytesPerPixel, Unpremultiply(pixels), format);
            return;
        }

        if (pixelFormat == WinNative.WicPixelFormat24bppBgr)
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
    private static unsafe void WritePixelsCore(IntPtr frame, int height, int stride, ReadOnlySpan<byte> pixels, ImageFileFormat format)
    {
        fixed (byte* source = pixels)
        {
            WinWicCom.ThrowIfFailed(
                WinWicCom.WritePixels(frame, (uint)height, (uint)stride, (uint)pixels.Length, source),
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
        try
        {
            var hr = WinWicCom.CreateDecoderFromFilename(factory, filePath, out var decoder);
            if (!WinNative.Succeeded(hr))
            {
                throw new ArgumentException($"'{filePath}' を画像として読み込めませんでした。", nameof(filePath));
            }

            try
            {
                WinWicCom.ThrowIfFailed(WinWicCom.GetFrame(decoder, 0, out var frame), $"'{filePath}' の画像を取得できませんでした。");
                try
                {
                    // 縮小する場合だけスケーラーを挟む（元の寸法が上限以下なら原寸でデコードする）
                    var scaler = CreateScaler(factory, frame, maxSize);
                    try
                    {
                        var source = scaler != IntPtr.Zero ? scaler : frame;

                        WinWicCom.ThrowIfFailed(
                            WinWicCom.CreateFormatConverter(factory, out var converter),
                            $"'{filePath}' の画素を変換する準備ができませんでした。");

                        try
                        {
                            WinWicCom.ThrowIfFailed(
                                WinWicCom.InitializeConverter(converter, source, WinNative.WicPixelFormat32bppPBgra),
                                $"'{filePath}' の画素を変換できませんでした。");

                            var (width, height) = ReadSize(converter);
                            return create(width, height, destination => CopyPixelsFrom(converter, width, height, destination));
                        }
                        finally
                        {
                            WinWicCom.Release(converter);
                        }
                    }
                    finally
                    {
                        WinWicCom.Release(scaler);
                    }
                }
                finally
                {
                    WinWicCom.Release(frame);
                }
            }
            finally
            {
                WinWicCom.Release(decoder);
            }
        }
        finally
        {
            WinWicCom.Release(factory);
        }
    }

    /// <summary>元の寸法が上限を超える場合だけ、縮小用のスケーラーを作る。</summary>
    /// <param name="factory">イメージング ファクトリ。</param>
    /// <param name="frame">デコードしたフレーム（画素源）。</param>
    /// <param name="maxSize">長辺の上限（ピクセル）。 0 以下の場合は縮小しない。</param>
    /// <returns>生成したスケーラー。 縮小しない場合は <see cref="IntPtr.Zero"/>。</returns>
    /// <remarks>
    /// <b>目標の寸法は長辺を上限へ揃えて求める</b>（縦横比を保つ。 各辺は 1 以上）。
    /// 上限以下なら作らない（拡大はしない）。
    /// </remarks>
    private static IntPtr CreateScaler(IntPtr factory, IntPtr frame, int maxSize)
    {
        if (maxSize <= 0)
        {
            return IntPtr.Zero;
        }

        var (width, height) = ReadSize(frame);
        if (width <= maxSize && height <= maxSize)
        {
            return IntPtr.Zero;
        }

        var scale = Math.Min((double)maxSize / width, (double)maxSize / height);
        var targetWidth = (uint)Math.Max(1, Math.Round(width * scale));
        var targetHeight = (uint)Math.Max(1, Math.Round(height * scale));

        WinWicCom.ThrowIfFailed(WinWicCom.CreateBitmapScaler(factory, out var scaler), "縮小用のスケーラーを作成できませんでした。");
        try
        {
            WinWicCom.ThrowIfFailed(
                WinWicCom.InitializeScaler(scaler, frame, targetWidth, targetHeight),
                "画像を縮小できませんでした。");
            return scaler;
        }
        catch
        {
            // 失敗したスケーラーを解放してから呼び出し側へ伝える（解放しないと COM の参照が残る）
            WinWicCom.Release(scaler);
            throw;
        }
    }

    /// <summary>画素源の寸法を読み取る。</summary>
    /// <param name="source">対象の画素源。</param>
    /// <returns>幅と高さ（ピクセル）。</returns>
    /// <exception cref="Win32Exception">寸法を取得できなかった場合、または寸法が 1 以上でない場合。</exception>
    private static (int Width, int Height) ReadSize(IntPtr source)
    {
        WinWicCom.ThrowIfFailed(WinWicCom.GetSize(source, out var width, out var height), "画像の寸法を取得できませんでした。");
        return width is > 0 and <= int.MaxValue && height is > 0 and <= int.MaxValue
            ? ((int)width, (int)height)
            : throw new Win32Exception(0, $"画像の寸法が不正です（{width}x{height}）。");
    }

    /// <summary>画素源のピクセルをコアの画像のバッファへコピーする。</summary>
    /// <param name="source">コピー元の画素源（アルファ乗算済みの BGRA8888 へ変換済み）。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="destination">コピー先（<c>幅 × 4 × 高さ</c> バイト以上）。</param>
    /// <exception cref="Win32Exception">ピクセルを取得できなかった場合。</exception>
    /// <remarks>コピー先の行幅は常に密（<c>幅 × 4</c>）であるため、1 回の呼び出しで全領域を書く。</remarks>
    private static unsafe void CopyPixelsFrom(IntPtr source, int width, int height, Span<byte> destination)
    {
        var stride = width * EsReadOnlyBitmap.BytesPerPixel;
        fixed (byte* target = destination)
        {
            WinWicCom.ThrowIfFailed(
                WinWicCom.CopyPixels(source, (uint)stride, (uint)(stride * height), target),
                "画像のピクセルを取得できませんでした。");
        }
    }
}
