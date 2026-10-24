using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using EsUtil.Platform.Core.Imaging;
using Windows.Win32;
using Windows.Win32.Graphics.Imaging;
using Windows.Win32.System.Com;
using Windows.Win32.System.Com.StructuredStorage;

namespace EsUtil.Platform.Core.Native.Win.Wic;

/// <summary>画像のファイル保存・読み込みの実装（WIC を使う Windows 専用）。</summary>
/// <remarks>
/// <b>唯一の実装</b>: 公開面は画像型のメンバー（<see cref="EsReadOnlyBitmap.Save(string)"/> /
/// <see cref="EsBitmap.Save(string)"/> / <see cref="EsReadOnlyBitmap.Load(string)"/> /
/// <see cref="EsBitmap.Load(string)"/>）であり、本クラスはその実装を 1 箇所へ集約する（本層は internal）。<br/>
/// <b>画像型を知らない</b>: 符号化は受け取ったピクセル バイト列を書き
/// （<see cref="EncodeToFile(ReadOnlySpan{byte}, int, int, string)"/>）、復号は生成処理を受け取って
/// デコード結果をその領域へ直接書く（<see cref="DecodeFromFile{T}(string, Func{int, int, Action{Span{byte}}, T})"/>）。<br/>
/// <b>依存分離</b>: ファイル形式のエンコード・デコードは OS の WIC（<c>windowscodecs.dll</c>）が担い、
/// <b>外部パッケージ（<c>System.Drawing.Common</c> 等）を参照しない</b>。
/// WIC は OS の一部であるため、追加の配布物も追加のスレッドも要らない。<br/>
/// <b>形式は拡張子が決める</b>: 保存先の拡張子から形式を選び、<b>未対応の拡張子は例外にする</b>
/// （PNG 等へ黙って差し替えると、呼び出し側が意図した形式と違うファイルができて気付かない）。
/// <b>対応形式の単一所有は <see cref="WinWicFormats"/></b> であり、本クラスは固定の一覧を持たない。<br/>
/// <b>Windows 専用</b>: WIC は Windows でのみ利用可能（<see cref="SupportedOSPlatformAttribute"/> で限定）。<br/>
/// <b>制限</b>: 透過（アルファ）を保存できない形式がある（JPEG / BMP / HEIF など。 形式の仕様、または
/// エンコーダーがアルファ無しのピクセル形式を要求するため）。 透過を保つ場合は PNG / TIFF / JPEG XR を選ぶこと。
/// </remarks>
[SupportedOSPlatform("windows10.0.17763")]
internal static unsafe class WinWicImageCodec
{
    /// <summary>保存に使える拡張子（小文字・先頭のドットを含む）。</summary>
    /// <remarks>
    /// <b>実行環境で書き出せる形式だけを含む</b>: OS にコーデック拡張が入っていない形式は一覧から外れる
    /// （<see cref="WinWicFormats"/> が初回のアクセスで調べ、結果を保持する）。
    /// <b>一覧に無い拡張子は保存できない</b>ため、呼び出し側は本一覧をそのまま選択肢にできる。
    /// </remarks>
    internal static IReadOnlyList<string> SupportedExtensions => WinWicFormats.Extensions;

    /// <summary>JPEG の品質として許容する最小値。</summary>
    internal const int JpegQualityMinimum = 0;

    /// <summary>JPEG の品質として許容する最大値。</summary>
    internal const int JpegQualityMaximum = 100;

    /// <summary>試し書きに使う寸法（ピクセル）。</summary>
    /// <remarks>
    /// <b>8 にする理由</b>: HEIF は 8x8 未満を書けない（HEVC の最小の符号化単位）ため、
    /// これより小さいと<b>書ける形式まで落ちる</b>。
    /// </remarks>
    private const int ProbeSize = 8;

    /// <summary>パレットの透明色へ寄せるアルファのしきい値（パーセント）。</summary>
    /// <remarks>
    /// <b>実測</b>: <c>IWICPalette::InitializeFromBitmap</c> へ透明色の追加を指示しても、
    /// <c>IWICFormatConverter</c> のしきい値が <c>0.0</c> のままでは<b>透明な画素が透明なパレットの色へ寄りません</b>
    /// （実測: 市松の透明画像で透明画素 0 / 1024）。 <c>1.0</c> 以上にすると<b>透明な画素がすべて透明の色番号</b>になります
    /// （実測: 512 / 1024）。 アルファが 1 以上の画素は不透明として扱うため、値は <c>1.0</c> で足ります。
    /// </remarks>
    private const double AlphaThresholdPercent = 1.0;

    /// <summary>HEIF が書ける最小の寸法（ピクセル）。</summary>
    /// <remarks>
    /// <b>8x8 未満は書けない（実測）</b>: 保存自体は成功するが、書き出したファイルを読み戻すと
    /// <c>WINCODEC_ERR_INVALIDPARAMETER</c>（<c>0x80070057</c>）になる（HEVC の最小の符号化単位）。
    /// </remarks>
    private const int HeifMinimumSize = 8;

    /// <summary>HEIF のコンテナ形式の GUID（実測値）。</summary>
    /// <remarks>
    /// <b><c>wincodec.h</c> に無い</b>: HEIF のコーデック拡張（Store アプリ）が登録する GUID であり、
    /// 本環境の列挙（<c>CreateComponentEnumerator</c>）で得た値を持つ。 <b>この形式だけは寸法の下限がある</b>ため、
    /// 拡張子ではなくコンテナで判定できるようにここへ置く。
    /// </remarks>
    private static readonly Guid s_heifContainer = new("E1E62521-6787-405B-A339-500715B5763F");

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
    /// <b>保存形式は拡張子から決める</b>（<see cref="WinWicFormats"/> が OS のエンコーダーから作った一覧を引く）。
    /// <see cref="EsImageSaveOptions.Format"/> を指定した場合は
    /// <b>形式の方が優先</b>され、<see cref="ResolveTarget"/> の規則で保存先が決まる。<br/>
    /// <b>減色する場合と GIF は 8bpp のインデックス カラーとして書く</b>（<see cref="ResolveColorCount"/>）。<br/>
    /// <b>透過を保てない形式がある</b>（JPEG / BMP / HEIF など。 透過を保つ場合は PNG / TIFF / JPEG XR を選ぶ）。
    /// </remarks>
    internal static void EncodeToFile(ReadOnlySpan<byte> pixels, int width, int height, string filePath, EsImageSaveOptions options)
        => EncodeToFile(pixels, width, height, width * EsReadOnlyBitmap.BytesPerPixel, filePath, options);

    /// <summary>ピクセル データをファイルへエンコードする（行幅を指定できる）。</summary>
    /// <param name="pixels">エンコードするピクセル データ（BGRA8888・アルファ乗算済み）。 借用であり書き換えない。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数（<c>幅 × 4</c> 以上）。</param>
    /// <param name="filePath">保存先のファイルパス（拡張子が形式を決める）。</param>
    /// <param name="options">保存のオプション。</param>
    /// <remarks>
    /// <b>行幅が密でない場合は 1 回だけ複製します</b>（WIC のエンコーダーへは密な並びで渡すため）。
    /// </remarks>
    internal static void EncodeToFile(
        ReadOnlySpan<byte> pixels, int width, int height, int stride, string filePath, EsImageSaveOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var expected = (long)stride * height;
        if (pixels.Length < expected)
        {
            throw new ArgumentException(
                $"ピクセル データの長さが寸法と一致しません（{width}x{height}・行幅 {stride} は {expected} バイト。 実際は {pixels.Length} バイト）。",
                nameof(pixels));
        }

        if (stride != width * EsReadOnlyBitmap.BytesPerPixel)
        {
            pixels = PackRows(pixels, width, height, stride);
        }

        var (format, targetPath) = ResolveTarget(filePath, options);

        // 減色する場合、および GIF（パレットが必須の形式）は 8bpp のインデックス カラーとして書く
        if (ResolveColorCount(format, options) is int colorCount)
        {
            EncodeIndexedToFile(
                pixels, width, height, targetPath, format, colorCount, options.Dither ?? EsDitherMode.None);
            return;
        }

        var bytes = EncodeToBytes(format.Container, width, height, options, palette: null, pixels, indexed: false);
        File.WriteAllBytes(targetPath, bytes);
    }

    /// <summary>画素を指定の色数へ減色し、色番号とパレットで返す。</summary>
    /// <param name="pixels">元の画素（BGRA8888・アルファ乗算済み。 行の余白を含んでよい）。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数。</param>
    /// <param name="colorCount">残す色数（2〜256。 呼び出し側が検証済みであること）。</param>
    /// <param name="dither">ディザの指定。</param>
    /// <returns>色番号（1 画素 1 バイト・行は密）とパレット（BGRA）。</returns>
    /// <exception cref="Win32Exception">減色（WIC）が失敗した場合。</exception>
    /// <remarks>
    /// <b>WIC の部品で減色します</b>: パレットの生成は <c>IWICPalette::InitializeFromBitmap</c>（中央値分割）、
    /// 画素の割り当ては <c>IWICFormatConverter</c> の 8bpp インデックスへの変換（ディザは
    /// <c>WICBitmapDitherTypeErrorDiffusion</c>）です。 <b>本ライブラリは減色の計算を自前で持ちません</b>
    /// （同じ計算を 2 つ持つと、保存したファイルの画素が経路によって変わる）。<br/>
    /// <b>行の余白は数えない・読まない</b>（<paramref name="stride"/> を使って行単位で渡す）。<br/>
    /// <b>透過はパレットのアルファで保たれます</b>: <c>IWICPalette::InitializeFromBitmap</c> へ
    /// 透明色の追加を指示するため、透明な画素は透明なパレットの色へ寄ります（PNG の <c>tRNS</c> /
    /// GIF の透明色番号として書かれます）。<br/>
    /// <b>パレットの色は乗算していない ARGB です</b>: 画素（乗算済み BGRA）へ戻すときは
    /// <see cref="Reduce"/> が乗算し直します。
    /// </remarks>
    internal static (byte[] Indices, uint[] Palette) ReduceToIndexed(
        ReadOnlySpan<byte> pixels, int width, int height, int stride, int colorCount, EsDitherMode dither)
    {
        var factory = OpenFactory();
        void* buffer = null;
        try
        {
            var bitmap = CreateBitmapFromPixels(factory, pixels, width, height, stride, out buffer);
            try
            {
                var palette = CreatePalette(factory, (IWICBitmapSource*)bitmap, colorCount);
                try
                {
                    var converter = CreateIndexedConverter(factory, (IWICBitmapSource*)bitmap, palette, dither);
                    try
                    {
                        var indices = new byte[width * height];
                        fixed (byte* destination = indices)
                        {
                            ((IWICBitmapSource*)converter)->CopyPixels(
                                null, (uint)width, (uint)indices.Length, destination);
                        }

                        return (indices, ReadPalette(palette));
                    }
                    finally
                    {
                        converter->Release();
                    }
                }
                finally
                {
                    palette->Release();
                }
            }
            finally
            {
                bitmap->Release();
            }
        }
        finally
        {
            if (buffer != null)
            {
                NativeMemory.Free(buffer);
            }

            factory->Release();
        }
    }

    /// <summary>画素を指定の色数へ減色し、画素（BGRA8888・アルファ乗算済み）で返す。</summary>
    /// <param name="pixels">元の画素（BGRA8888・アルファ乗算済み。 行の余白を含んでよい）。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数。</param>
    /// <param name="colorCount">残す色数（2〜256）。 呼び出し側が検証済みであること）。</param>
    /// <param name="dither">ディザの指定。</param>
    /// <returns>減色した画素（行は密）。</returns>
    /// <exception cref="Win32Exception">減色（WIC）が失敗した場合。</exception>
    /// <remarks>
    /// <b>色番号を経由します</b>: 減色の結果は 8bpp の色番号とパレットであり、画素へ戻すのは
    /// 本メソッドの役目です（保存は色番号のまま書くため、戻す必要がありません）。<br/>
    /// <b>乗算し直します</b>: パレットの色は乗算していない ARGB であるため、本ライブラリの画素の約束
    /// （BGRA8888・アルファ乗算済み）へ揃えます。
    /// </remarks>
    internal static byte[] Reduce(
        ReadOnlySpan<byte> pixels, int width, int height, int stride, int colorCount, EsDitherMode dither)
    {
        var (indices, palette) = ReduceToIndexed(pixels, width, height, stride, colorCount, dither);
        var result = new byte[width * height * EsReadOnlyBitmap.BytesPerPixel];
        for (var i = 0; i < indices.Length; i++)
        {
            var color = palette[indices[i]];
            var alpha = (byte)(color >> 24);
            var offset = i * EsReadOnlyBitmap.BytesPerPixel;
            var multiplier = alpha == 255 ? 1.0 : alpha / 255.0;
            result[offset + 0] = Premultiply((byte)color, multiplier);
            result[offset + 1] = Premultiply((byte)(color >> 8), multiplier);
            result[offset + 2] = Premultiply((byte)(color >> 16), multiplier);
            result[offset + 3] = alpha;
        }

        return result;
    }

    /// <summary>色成分をアルファで乗算する。</summary>
    /// <param name="value">乗算していない色成分。</param>
    /// <param name="multiplier">アルファ（0.0〜1.0）。</param>
    /// <returns>乗算した色成分。</returns>
    private static byte Premultiply(byte value, double multiplier)
        => multiplier == 1.0 ? value : (byte)Math.Min(255, (int)((value * multiplier) + 0.5));

    /// <summary>形式を書き出せるかを試し書きで確かめる（<see cref="WinWicFormats"/> の一覧を作るために使う）。</summary>
    /// <param name="container">コンテナ形式の GUID。</param>
    /// <returns>8x8 の画像を書けた場合は true。</returns>
    /// <remarks>
    /// <b>列挙に現れることと書けることは違う</b>: 実測では JPEG XL のエンコーダーが一覧に現れるが、
    /// 本ライブラリの画素（BGRA8888）からは書けなかった。 一覧に載せると「一覧の拡張子で保存すれば成功する」
    /// という約束が崩れるため、実際に書いて確かめる。<br/>
    /// <b>パレットを要求する形式</b>（GIF など）のために、通常の画素で失敗した場合はインデックス カラーでも試す。
    /// </remarks>
    internal static bool CanEncode(Guid container)
    {
        var pixels = new byte[ProbeSize * ProbeSize * EsReadOnlyBitmap.BytesPerPixel];
        for (var i = 0; i < ProbeSize * ProbeSize; i++)
        {
            pixels[(i * EsReadOnlyBitmap.BytesPerPixel) + 0] = (byte)(i * 3);
            pixels[(i * EsReadOnlyBitmap.BytesPerPixel) + 1] = (byte)(i * 5);
            pixels[(i * EsReadOnlyBitmap.BytesPerPixel) + 2] = 200;
            pixels[(i * EsReadOnlyBitmap.BytesPerPixel) + 3] = 255;
        }

        if (TryEncode(container, pixels, indexed: false))
        {
            return true;
        }

        return TryEncode(container, CreateProbeIndices(), indexed: true);
    }

    /// <summary>試し書きを行い、失敗を例外にせず真偽で返す。</summary>
    /// <param name="container">コンテナ形式の GUID。</param>
    /// <param name="pixels">書き込む画素（8bpp のインデックスの場合もある）。</param>
    /// <param name="indexed">8bpp のインデックス カラーとして書く場合は true。</param>
    /// <returns>書けた場合は true。</returns>
    private static bool TryEncode(Guid container, ReadOnlySpan<byte> pixels, bool indexed)
    {
        uint[]? palette = indexed ? CreateProbePalette() : null;
        try
        {
            EncodeToBytes(container, ProbeSize, ProbeSize, default, palette, pixels, indexed);
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or COMException or NotSupportedException or InvalidOperationException)
        {
            // 試し書きの失敗は「この環境では書けない」を意味するだけであり、例外にしない
            return false;
        }
    }

    /// <summary>試し書きに使う 8bpp のインデックスを作る。</summary>
    /// <returns>インデックス（2 色を交互に置く）。</returns>
    private static byte[] CreateProbeIndices()
    {
        var indices = new byte[ProbeSize * ProbeSize];
        for (var i = 0; i < indices.Length; i++)
        {
            indices[i] = (byte)(i % 2);
        }

        return indices;
    }

    /// <summary>試し書きに使うパレットを作る。</summary>
    /// <returns>パレット（BGRA の 2 色）。</returns>
    private static uint[] CreateProbePalette() => [0xFF000000, 0xFFFFFFFF];

    /// <summary>行幅が密でない画素を、密な並びへ複製する。</summary>
    /// <param name="pixels">元の画素（行の余白を含む）。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="stride">元の行幅（バイト）。</param>
    /// <returns>密な並びの画素。</returns>
    private static byte[] PackRows(ReadOnlySpan<byte> pixels, int width, int height, int stride)
    {
        var rowBytes = width * EsReadOnlyBitmap.BytesPerPixel;
        var packed = new byte[rowBytes * height];
        for (var y = 0; y < height; y++)
        {
            pixels.Slice(y * stride, rowBytes).CopyTo(packed.AsSpan(y * rowBytes, rowBytes));
        }

        return packed;
    }

    /// <summary>減色したピクセルを 8bpp のインデックス カラーとしてファイルへ書き出す。</summary>
    /// <param name="pixels">エンコードするピクセル データ（BGRA8888・アルファ乗算済み）。 借用であり書き換えない。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="filePath">保存先のファイルパス（形式は決定済み）。</param>
    /// <param name="format">保存する形式。</param>
    /// <param name="colorCount">減色して残す色数。</param>
    /// <param name="dither">減色のディザ。</param>
    /// <exception cref="Win32Exception">エンコード（WIC）が失敗した場合。</exception>
    /// <remarks>
    /// <b>なぜ 8bpp で書くのか</b>: 32bpp のまま減色しても、エンコーダーはパレット画像を書かない
    /// （減色の効果は圧縮率の向上だけであり、BMP では 1 バイトも減らない）。
    /// <b>8bpp のインデックス カラー（パレット付き）を渡すとパレット画像になる</b>。<br/>
    /// <b>GIF もここを通る</b>（<see cref="ResolveColorCount"/> を参照。 GIF はパレットが必須であるため
    /// 指定が無くても量子化する）。<br/>
    /// <b>行幅を 4 バイト境界へ揃える</b>: BMP の行は 4 バイト境界へ揃えられるため、揃えないと
    /// 読み戻した画像が斜めにずれる。
    /// </remarks>
    private static void EncodeIndexedToFile(
        ReadOnlySpan<byte> pixels, int width, int height, string filePath, WinWicFormats.Entry format, int colorCount,
        EsDitherMode dither)
    {
        var (indices, palette) = ReduceToIndexed(
            pixels, width, height, width * EsReadOnlyBitmap.BytesPerPixel, colorCount, dither);

        var stride = (width + 3) & ~3;
        var rows = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            indices.AsSpan(y * width, width).CopyTo(rows.AsSpan(y * stride, width));
        }

        var bytes = EncodeToBytes(format.Container, width, height, default, palette, rows, indexed: true);
        File.WriteAllBytes(filePath, bytes);
    }

    /// <summary>メモリ上の画素から WIC のビットマップを作る。</summary>
    /// <param name="factory">イメージング ファクトリ。</param>
    /// <param name="pixels">画素（BGRA8888・アルファ乗算済み。 行の余白を含んでよい）。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数。</param>
    /// <param name="buffer">画素を複製したアンマネージド領域（呼び出し側が <c>NativeMemory.Free</c> で解放する）。</param>
    /// <returns>生成したビットマップ（呼び出し側が <c>Release()</c> を呼ぶ）。</returns>
    /// <remarks>
    /// <b>アンマネージド領域へ複製します</b>: <c>CreateBitmapFromMemory</c> は<b>渡した領域を参照する</b>
    /// （コピーしない）ため、マネージド配列のアドレスを渡すと GC の移動で壊れます。 実測でも、
    /// <c>fixed</c> の中で作って抜けた後に使うと<b>色数が 2 になる</b>などの壊れ方をしました。
    /// 費用は 1 枚分の複製ですが、<b>壊れた結果を返さないこと</b>を優先します。<br/>
    /// <b>行の余白をそのまま渡せます</b>: <c>cbStride</c> に <paramref name="stride"/> を渡すため、
    /// 密でない並びも受け付けます。
    /// </remarks>
    private static IWICBitmap* CreateBitmapFromPixels(
        IWICImagingFactory* factory, ReadOnlySpan<byte> pixels, int width, int height, int stride, out void* buffer)
    {
        var size = (nuint)pixels.Length;
        buffer = NativeMemory.Alloc(size);
        fixed (byte* source = pixels)
        {
            Buffer.MemoryCopy(source, buffer, pixels.Length, pixels.Length);
        }

        var pixelFormat = PInvoke.GUID_WICPixelFormat32bppPBGRA;
        IWICBitmap* bitmap;
        factory->CreateBitmapFromMemory(
            (uint)width, (uint)height, &pixelFormat, (uint)stride, (uint)pixels.Length, (byte*)buffer, &bitmap);
        return bitmap;
    }

    /// <summary>画素から色数ぶんのパレットを作る。</summary>
    /// <param name="factory">イメージング ファクトリ。</param>
    /// <param name="source">元の画素の源。</param>
    /// <param name="colorCount">残す色数。</param>
    /// <returns>生成したパレット（呼び出し側が <c>Release()</c> を呼ぶ）。</returns>
    /// <remarks>
    /// <b>透明色を加える</b>: <c>fAddTransparentColor</c> を true にするため、透明な画素は
    /// 透明なパレットの色へ寄ります（PNG の <c>tRNS</c> / GIF の透明色番号）。
    /// </remarks>
    private static IWICPalette* CreatePalette(
        IWICImagingFactory* factory, IWICBitmapSource* source, int colorCount)
    {
        IWICPalette* palette;
        factory->CreatePalette(&palette);
        try
        {
            palette->InitializeFromBitmap(source, (uint)colorCount, true);
            return palette;
        }
        catch
        {
            // 失敗したパレットを解放してから、呼び出し側へ伝える
            palette->Release();
            throw;
        }
    }

    /// <summary>8bpp のインデックス カラーへ変換する変換器を作る。</summary>
    /// <param name="factory">イメージング ファクトリ。</param>
    /// <param name="source">元の画素の源。</param>
    /// <param name="palette">割り当てに使うパレット。</param>
    /// <param name="dither">ディザの指定。</param>
    /// <returns>生成した変換器（呼び出し側が <c>Release()</c> を呼ぶ）。</returns>
    private static IWICFormatConverter* CreateIndexedConverter(
        IWICImagingFactory* factory, IWICBitmapSource* source, IWICPalette* palette, EsDitherMode dither)
    {
        var ditherType = dither switch
        {
            EsDitherMode.None => WICBitmapDitherType.WICBitmapDitherTypeNone,
            EsDitherMode.ErrorDiffusion => WICBitmapDitherType.WICBitmapDitherTypeErrorDiffusion,
            var other => throw new ArgumentOutOfRangeException(nameof(dither), other, "定義されていないディザの指定です。"),
        };

        IWICFormatConverter* converter;
        factory->CreateFormatConverter(&converter);
        try
        {
            converter->Initialize(
                source, PInvoke.GUID_WICPixelFormat8bppIndexed, ditherType, palette, AlphaThresholdPercent,
                WICBitmapPaletteType.WICBitmapPaletteTypeCustom);
            return converter;
        }
        catch
        {
            // 失敗した変換器を解放してから、呼び出し側へ伝える
            converter->Release();
            throw;
        }
    }

    /// <summary>パレットの色を読み取る。</summary>
    /// <param name="palette">対象のパレット。</param>
    /// <returns>色（ARGB の並び。 パレットの色数ぶん）。</returns>
    private static uint[] ReadPalette(IWICPalette* palette)
    {
        uint count;
        palette->GetColorCount(&count);
        if (count == 0)
        {
            return [];
        }

        var colors = new uint[count];
        fixed (uint* destination = colors)
        {
            uint actual;
            palette->GetColors(count, destination, &actual);
        }

        return colors;
    }

    /// <summary>ピクセルを WIC でエンコードし、ファイルへ書く内容を返す。</summary>
    /// <param name="container">コンテナ形式の GUID。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="options">保存のオプション（JPEG の品質を書き込む）。</param>
    /// <param name="palette">パレット（インデックス カラーでない場合は null）。</param>
    /// <param name="pixels">書き込む画素（<paramref name="indexed"/> が true の場合は 8bpp のインデックス）。</param>
    /// <param name="indexed">8bpp のインデックス カラーとして書く場合は true。</param>
    /// <returns>エンコードした内容。</returns>
    /// <exception cref="NotSupportedException">その形式を書き出せるエンコーダーが無い場合、または
    /// エンコーダーがパレット画像を書けない場合。</exception>
    /// <exception cref="Win32Exception">エンコード（WIC）が失敗した場合。</exception>
    /// <remarks>
    /// <b>メモリ ストリームへ書いてからファイルへ書く理由</b>: COM の解放は明示するが、
    /// WIC にファイルを開かせると解放までファイルが開いたままになる（実測: 直後の削除が共有違反で失敗した）。
    /// メモリ ストリームならファイルは呼び出し側が開いて閉じるため、直後から削除も上書きもできる。<br/>
    /// <b>エンコーダーのキャッシュは <c>NoCache</c> を使う（実測）</b>: メモリ ストリームでは
    /// <c>CacheInMemory</c>（0）も <c>CacheTempFile</c>（1）も <c>WINCODEC_ERR_UNSUPPORTEDOPERATION</c>
    /// （<c>0x88982F81</c>）で初期化に失敗し、<c>NoCache</c>（2）だけが成功した。<br/>
    /// <b>エンコーダーの初期化は 1 回だけ</b>である（<c>IWICBitmapFrameEncode::Initialize</c> を 2 回呼ぶと失敗する）。
    /// そのため、ここで初期化・フレーム作成・オプション設定・寸法・パレット・画素の書き込みをまとめて行う。<br/>
    /// <b>画素の書き込みを処理として受け取らない理由</b>: 画素は <see cref="ReadOnlySpan{T}"/>（ref 構造体）であり、
    /// ラムダへ捕捉できないため。
    /// </remarks>
    private static byte[] EncodeToBytes(
        Guid container, int width, int height, in EsImageSaveOptions options, uint[]? palette,
        ReadOnlySpan<byte> pixels, bool indexed)
    {
        ValidateSize(container, width, height);

        var factory = OpenFactory();
        try
        {
            var stream = WinWicFactory.CreateMemoryStream();
            try
            {
                var encoder = CreateEncoder(factory, container);
                try
                {
                    encoder->Initialize(stream, WICBitmapEncoderCacheOption.WICBitmapEncoderNoCache);

                    IPropertyBag2* properties;
                    var frame = CreateFrame(encoder, &properties);
                    try
                    {
                        if (options.JpegQuality is int quality)
                        {
                            // WIC の品質は 0.0〜1.0 の実数である（公開面は 0〜100 の整数）
                            WinWicFactory.WriteRealOption(
                                properties, "ImageQuality", quality / (float)JpegQualityMaximum);
                        }

                        frame->Initialize(properties);
                        frame->SetSize((uint)width, (uint)height);
                        if (palette is not null)
                        {
                            ApplyPalette(factory, frame, palette);
                        }

                        WritePixels(factory, frame, width, height, pixels, indexed, container);
                        frame->Commit();
                    }
                    finally
                    {
                        frame->Release();
                        properties->Release();
                    }

                    // ストリームが生きているうちに確定する（確定してから解放すると内容が書かれない）
                    encoder->Commit();
                    return WinWicFactory.ReadStream(stream);
                }
                finally
                {
                    encoder->Release();
                }
            }
            finally
            {
                stream->Release();
            }
        }
        finally
        {
            factory->Release();
        }
    }

    /// <summary>エンコーダーを作る。</summary>
    /// <param name="factory">イメージング ファクトリ。</param>
    /// <param name="container">コンテナ形式の GUID。</param>
    /// <returns>生成したエンコーダー（呼び出し側が <c>Release()</c> を呼ぶ）。</returns>
    /// <exception cref="NotSupportedException">その形式を書き出せるエンコーダーが無い場合。</exception>
    private static IWICBitmapEncoder* CreateEncoder(IWICImagingFactory* factory, Guid container)
    {
        try
        {
            var noVendor = Guid.Empty;
            return factory->CreateEncoder(in container, in noVendor);
        }
        catch (COMException ex)
        {
            // エンコーダーが無いことを「未対応の形式」として翻訳する（原因の状態値は内部例外へ残す）
            throw CreateEncoderMissing(container, ex);
        }
    }

    /// <summary>エンコーダーからフレームを作る。</summary>
    /// <param name="encoder">エンコーダー。</param>
    /// <param name="properties">エンコーダーが用意したオプション（呼び出し側が <c>Release()</c> を呼ぶ）。</param>
    /// <returns>生成したフレーム（呼び出し側が <c>Release()</c> を呼ぶ）。</returns>
    private static IWICBitmapFrameEncode* CreateFrame(IWICBitmapEncoder* encoder, IPropertyBag2** properties)
    {
        IWICBitmapFrameEncode* frame;
        encoder->CreateNewFrame(&frame, properties);
        return frame;
    }

    /// <summary>減色したパレットをフレームへ設定する。</summary>
    /// <param name="factory">イメージング ファクトリ。</param>
    /// <param name="frame">対象のフレーム。</param>
    /// <param name="palette">設定するパレット（BGRA の配列）。</param>
    private static void ApplyPalette(IWICImagingFactory* factory, IWICBitmapFrameEncode* frame, uint[] palette)
    {
        IWICPalette* wicPalette;
        factory->CreatePalette(&wicPalette);
        try
        {
            fixed (uint* colors = palette)
            {
                wicPalette->InitializeCustom(colors, (uint)palette.Length);
            }

            frame->SetPalette(wicPalette);
        }
        finally
        {
            wicPalette->Release();
        }
    }

    /// <summary>WIC のイメージング ファクトリを生成する。</summary>
    /// <returns>生成したファクトリ。</returns>
    /// <exception cref="Win32Exception">WIC を使えない場合。</exception>
    private static IWICImagingFactory* OpenFactory()
    {
        var factory = WinWicFactory.Create();
        if (factory is null)
        {
            throw new Win32Exception(0, "WIC（Windows Imaging Component）を使えませんでした。");
        }

        return factory;
    }

    /// <summary>エンコーダーが無いことを表す例外を作る。</summary>
    /// <param name="container">コンテナ形式の GUID。</param>
    /// <param name="inner">WIC が返した失敗。</param>
    /// <returns>送出する例外。</returns>
    private static NotSupportedException CreateEncoderMissing(Guid container, Exception inner)
        => new(
            $"コンテナ形式 {container} を書き出すエンコーダーがありません"
            + "（コーデック拡張が要る形式は Microsoft Store の拡張が必要です）。",
            inner);

    /// <summary>エンコーダーが受け付けたピクセル形式へ直してから画素を書き込む。</summary>
    /// <param name="factory">イメージング ファクトリ。</param>
    /// <param name="frame">対象のフレーム。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="pixels">画素（BGRA8888・アルファ乗算済み。 行幅は密）。 <paramref name="indexed"/> が true の場合はインデックス。</param>
    /// <param name="indexed">8bpp のインデックス カラーとして書く場合は true。</param>
    /// <param name="container">保存する形式（例外メッセージ用）。</param>
    /// <exception cref="NotSupportedException">エンコーダーが想定外のピクセル形式を要求した場合。</exception>
    /// <remarks>
    /// <b>要求と返答が違う（実測）</b>: アルファ乗算済みの BGRA を要求しても、PNG / TIFF は
    /// アルファ乗算なしの BGRA、JPEG は 24bpp の BGR、HEIF は 32bpp の BGR、JPEG XL は RGBA を返す
    /// （エンコーダーは形式の変換をしない）。 そのため<b>返された形式に合わせて並びを直してから書く</b>。<br/>
    /// <b>変換器（<c>IWICFormatConverter</c>）で並びを直さない理由</b>: 画素の源を
    /// <c>CreateBitmapFromMemory</c> で作れば変換器も使えるが、そのためには毎回 1 枚分の
    /// <c>IWICBitmap</c> を作ることになり、<b>画素の複製が 1 回増える</b>。 並びの付け替えは
    /// 自前の 1 パスで足りるため、そちらで行う（BMP だけは要求どおりの形式なのでコピーを作らない）。
    /// </remarks>
    private static void WritePixels(
        IWICImagingFactory* factory, IWICBitmapFrameEncode* frame, int width, int height,
        ReadOnlySpan<byte> pixels, bool indexed, Guid container)
    {
        if (indexed)
        {
            var indexFormat = PInvoke.GUID_WICPixelFormat8bppIndexed;
            frame->SetPixelFormat(&indexFormat);
            if (indexFormat != PInvoke.GUID_WICPixelFormat8bppIndexed)
            {
                throw new NotSupportedException(
                    $"この形式（コンテナ {container}）のエンコーダーはパレット画像を書けません（{indexFormat}）。"
                    + "減色（ColorCount）は PNG / GIF / BMP などパレットを持てる形式で指定してください。");
            }

            WritePixelsCore(frame, height, (width + 3) & ~3, pixels, container);
            return;
        }

        var pixelFormat = PInvoke.GUID_WICPixelFormat32bppPBGRA;
        frame->SetPixelFormat(&pixelFormat);

        if (pixelFormat == PInvoke.GUID_WICPixelFormat32bppPBGRA)
        {
            WritePixelsCore(frame, height, width * EsReadOnlyBitmap.BytesPerPixel, pixels, container);
            return;
        }

        if (pixelFormat == PInvoke.GUID_WICPixelFormat32bppBGRA || pixelFormat == PInvoke.GUID_WICPixelFormat32bppBGR)
        {
            // どちらも画素は BGRA の並びである（BGR の 4 バイト目は詰め物であり、書いても無視される）
            WritePixelsCore(
                frame, height, width * EsReadOnlyBitmap.BytesPerPixel, Unpremultiply(pixels), container);
            return;
        }

        if (pixelFormat == PInvoke.GUID_WICPixelFormat24bppBGR)
        {
            WritePixelsCore(frame, height, width * 3, ToBgr24(pixels, width, height), container);
            return;
        }

        if (pixelFormat == PInvoke.GUID_WICPixelFormat32bppRGBA)
        {
            WritePixelsCore(
                frame, height, width * EsReadOnlyBitmap.BytesPerPixel, ToRgba32(pixels), container);
            return;
        }

        throw new NotSupportedException(
            $"この形式（コンテナ {container}）のエンコーダーは想定外のピクセル形式を要求しました（{pixelFormat}）。");
    }

    /// <summary>行幅を指定して画素を書き込む。</summary>
    /// <param name="frame">対象のフレーム。</param>
    /// <param name="height">行数。</param>
    /// <param name="stride">行幅（バイト）。</param>
    /// <param name="pixels">画素（行幅が密な並び）。</param>
    /// <param name="container">保存する形式（例外メッセージ用）。</param>
    private static void WritePixelsCore(
        IWICBitmapFrameEncode* frame, int height, int stride, ReadOnlySpan<byte> pixels, Guid container)
    {
        try
        {
            fixed (byte* source = pixels)
            {
                frame->WritePixels((uint)height, (uint)stride, (uint)pixels.Length, source);
            }
        }
        catch (COMException ex)
        {
            // 状態値を Win32 の例外へ翻訳する（公開面は Win32Exception で状態値を運ぶ）
            throw new Win32Exception(
                ex.HResult, $"画素を書き込めませんでした（コンテナ {container}。 WIC の状態: 0x{ex.HResult:X8}）");
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

    /// <summary>アルファ乗算済みの BGRA を 32bpp の RGBA（乗算なし）へ直す。</summary>
    /// <param name="pixels">アルファ乗算済みの画素（BGRA）。</param>
    /// <returns>RGBA の画素。</returns>
    /// <remarks>RGBA を要求するエンコーダー（実測: JPEG XL）向けに、乗算を解いてから並びを入れ替える。</remarks>
    private static byte[] ToRgba32(ReadOnlySpan<byte> pixels)
    {
        var straight = Unpremultiply(pixels);
        for (var i = 0; i < straight.Length; i += EsReadOnlyBitmap.BytesPerPixel)
        {
            (straight[i + 0], straight[i + 2]) = (straight[i + 2], straight[i + 0]);
        }

        return straight;
    }

    /// <summary>形式ごとの最小寸法を満たしているかを確かめる。</summary>
    /// <param name="container">コンテナ形式の GUID。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <exception cref="NotSupportedException">その形式で書けない寸法の場合。</exception>
    /// <remarks>
    /// <b>HEIF は 8x8 未満を書けません（実測）</b>: 8x8 以上は往復できるが、8x7 / 8x6 / 4x4 は
    /// <b>保存自体は成功する</b>ものの、書き出したファイルを読み戻すと
    /// <c>WINCODEC_ERR_INVALIDPARAMETER</c>（<c>0x80070057</c>）になります（HEVC の最小の符号化単位）。
    /// 読めないファイルを作らないため、保存の前に断ります。
    /// </remarks>
    private static void ValidateSize(Guid container, int width, int height)
    {
        if (container == s_heifContainer
            && (width < HeifMinimumSize || height < HeifMinimumSize))
        {
            throw new NotSupportedException(
                $"HEIF 形式は {HeifMinimumSize}x{HeifMinimumSize} 未満の画像を書けません"
                + $"（指定された寸法は {width}x{height}）。");
        }
    }

    /// <summary>保存する形式と実際の保存先を決める（拡張子の補完と食い違いの判定を含む）。</summary>
    /// <param name="filePath">保存先のファイルパス。</param>
    /// <param name="options">保存のオプション。</param>
    /// <returns>保存する形式と実際の保存先。</returns>
    /// <exception cref="ArgumentException">拡張子と指定形式が食い違い、無視する設定でない場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException">オプションの値が許容範囲外の場合。</exception>
    /// <exception cref="NotSupportedException">拡張子が対応形式でない場合、または設定されたオプションがその形式で使えない場合。</exception>
    /// <remarks>
    /// <b>形式を指定しない場合は拡張子が形式を決める</b>（従来の挙動を変えない）。
    /// <b>形式を指定した場合は形式が優先</b>され、拡張子が無ければ補完し、食い違いは
    /// <see cref="EsImageSaveOptions.IgnoreFormatMismatch"/> で判定する。
    /// </remarks>
    private static (WinWicFormats.Entry Format, string FilePath) ResolveTarget(string filePath, in EsImageSaveOptions options)
    {
        var extension = Path.GetExtension(filePath);
        if (options.Format is not EsImageFileFormat requested)
        {
            // 形式の指定が無い場合は拡張子が形式を決める（未対応の拡張子は従来どおり拒否する）
            return (ResolveExtension(extension, options), filePath);
        }

        var format = ResolveFormat(requested);
        if (extension.Length == 0)
        {
            // 拡張子が無い場合は指定形式の代表の拡張子を補完する（保存先は補完後のパスになる）
            ValidateOptions(format, options);
            return (format, string.Concat(filePath, ExtensionOf(requested)));
        }

        if (MatchesExtension(format, extension))
        {
            ValidateOptions(format, options);
            return (format, filePath);
        }

        if (!options.IgnoreFormatMismatch)
        {
            // 未対応の拡張子は「拡張子が対応形式でない」として扱う（指定形式で黙って上書きしない）
            if (WinWicFormats.FindByExtension(extension) is null)
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
    /// <exception cref="ArgumentOutOfRangeException">オプションの値が許容範囲外の場合。</exception>
    private static WinWicFormats.Entry ResolveExtension(string extension, in EsImageSaveOptions options)
    {
        var format = WinWicFormats.FindByExtension(extension) ?? throw CreateUnsupportedExtension(extension);
        ValidateOptions(format, options);
        return format;
    }

    /// <summary>指定された形式を、実行環境で書き出せる形式へ解決する。</summary>
    /// <param name="format">公開の形式。</param>
    /// <returns>解決した形式。</returns>
    /// <exception cref="ArgumentOutOfRangeException">定義されていない値の場合。</exception>
    /// <exception cref="NotSupportedException">その形式を書き出せるエンコーダーが無い場合。</exception>
    /// <remarks>
    /// <b>拡張子を経由して解決する</b>: 形式の一覧は OS が持つため、本クラスは
    /// 「<see cref="EsImageFileFormat"/> の値 → 代表の拡張子 → OS のエンコーダー」の順に引く。
    /// </remarks>
    private static WinWicFormats.Entry ResolveFormat(EsImageFileFormat format)
    {
        var extension = ExtensionOf(format);
        return WinWicFormats.FindByExtension(extension) ?? throw new NotSupportedException(
            $"{format} 形式（{extension}）を書き出すエンコーダーがありません"
            + "（コーデック拡張が要る形式は Microsoft Store の拡張が必要です）。");
    }

    /// <summary>形式の代表の拡張子を求める。</summary>
    /// <param name="format">公開の形式。</param>
    /// <returns>代表の拡張子（先頭のドットを含む）。</returns>
    /// <exception cref="ArgumentOutOfRangeException">定義されていない値の場合。</exception>
    /// <remarks>
    /// <b>OS が申告する順に依らない理由</b>: 同じ形式に複数の拡張子があり（<c>.jpeg</c> / <c>.jpe</c> /
    /// <c>.jpg</c> など）、<b>OS の並びは保証されない</b>。 補完に使う名前は本ライブラリが固定する
    /// （そうしないと、環境によって <c>shot</c> から作られる名前が変わる）。
    /// </remarks>
    private static string ExtensionOf(EsImageFileFormat format) => format.ToExtension();

    /// <summary>形式が指定された拡張子を持つかを調べる。</summary>
    /// <param name="format">形式。</param>
    /// <param name="extension">拡張子。</param>
    /// <returns>持つ場合は true（大文字・小文字は区別しない）。</returns>
    private static bool MatchesExtension(WinWicFormats.Entry format, string extension)
    {
        foreach (var candidate in format.Extensions)
        {
            if (string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>その形式のオプションの組み合わせを検証する。</summary>
    /// <param name="format">保存する形式。</param>
    /// <param name="options">保存のオプション。</param>
    /// <exception cref="NotSupportedException">その形式で使えない項目が設定されている場合。</exception>
    /// <exception cref="InvalidOperationException">減色しない保存へ減色の指定（品質・ディザ）が設定されている場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException">値が許容範囲外の場合。</exception>
    /// <remarks>
    /// <b>品質を使えるのは JPEG と JPEG XR だけである</b>（他の形式は品質を受け取っても出力が変わらない）。
    /// <b>減色の品質とディザは減色が行われる場合だけ意味を持つ</b>（<see cref="EsImageSaveOptions.ColorCount"/> を
    /// 指定した場合、または GIF へ保存する場合）。 黙って無視すると「設定したのに効かない」ことに気付かないため、
    /// 例外にする。<br/>
    /// <b>減色をその形式が扱えるかは保存を試すまで分からない</b>ため、ここでは判定しない
    /// （<see cref="WritePixels"/> がエンコーダーの返答を見て <see cref="NotSupportedException"/> にする）。
    /// </remarks>
    private static void ValidateOptions(WinWicFormats.Entry format, in EsImageSaveOptions options)
    {
        if (options.ColorCount is int colorCount
            && (colorCount < EsBitmapOperations.MinimumColorCount || colorCount > EsBitmapOperations.MaximumColorCount))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                colorCount,
                $"減色して残す色数は {EsBitmapOperations.MinimumColorCount}〜{EsBitmapOperations.MaximumColorCount} の範囲で指定します。");
        }

        if (options.Dither is EsDitherMode dither && dither is not (EsDitherMode.None or EsDitherMode.ErrorDiffusion))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options), dither, "定義されていないディザの指定です。");
        }

        // 減色の指定（ディザ）は減色が行われる場合だけ意味を持つ（GIF は指定が無くても量子化される）
        if (options.ColorCount is null && !IsPaletteRequired(format) && options.Dither is not null)
        {
            throw CreateReductionRequired(nameof(EsImageSaveOptions.Dither));
        }

        if (options.JpegQuality is not int jpegQuality)
        {
            return;
        }

        if (!SupportsQuality(format))
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

    /// <summary>その形式がパレットを必須とするかを求める。</summary>
    /// <param name="format">保存する形式。</param>
    /// <returns>必須の場合は true。</returns>
    /// <remarks>
    /// <b>GIF はパレットが必須である</b>（コンテナ形式で判定する。 拡張子ではない）。
    /// 指定が無くても常に量子化するのは、GIF が 256 色しか持てないためである。
    /// </remarks>
    private static bool IsPaletteRequired(WinWicFormats.Entry format)
        => format.Container == PInvoke.GUID_ContainerFormatGif;

    /// <summary>量子化して 8bpp のインデックス カラーとして書く場合の色数を求める。</summary>
    /// <param name="format">保存する形式。</param>
    /// <param name="options">保存のオプション（検証済み）。</param>
    /// <returns>残す色数。 量子化しない場合は null。</returns>
    /// <remarks>
    /// <b>GIF は指定が無くても常に量子化する</b>: GIF はパレットが必須の形式である。
    /// 自前の量子化は透明な画素を透明なパレットの色へ割り当てるため、透過が保たれる。
    /// 副次的に<b>ファイルも小さく画質も良くなる</b>。
    /// </remarks>
    private static int? ResolveColorCount(WinWicFormats.Entry format, in EsImageSaveOptions options)
        => options.ColorCount is int colorCount
            ? colorCount
            : IsPaletteRequired(format) ? EsBitmapOperations.MaximumColorCount : null;

    /// <summary>その形式が品質（<c>ImageQuality</c>）を受け付けるかを求める。</summary>
    /// <param name="format">保存する形式。</param>
    /// <returns>受け付ける場合は true。</returns>
    /// <remarks>
    /// <b>WIC が品質を受け取るのは非可逆の JPEG 系だけである</b>（JPEG と JPEG XR）。
    /// コンテナ形式で判定する（拡張子ではない）ため、<c>.jfif</c> のような別名でも同じ扱いになる。
    /// </remarks>
    private static bool SupportsQuality(WinWicFormats.Entry format)
        => format.Container == PInvoke.GUID_ContainerFormatJpeg || format.Container == PInvoke.GUID_ContainerFormatWmp;

    /// <summary>その形式で使えないオプションが設定されたことを表す例外を作る。</summary>
    /// <param name="format">保存する形式。</param>
    /// <param name="optionName">使えないオプションの名前。</param>
    /// <returns>例外。</returns>
    private static NotSupportedException CreateUnsupportedOption(WinWicFormats.Entry format, string optionName)
        => new($"オプション '{optionName}' は {format.Name} 形式では使えません"
            + $"（{nameof(EsImageSaveOptions.JpegQuality)} を使えるのは JPEG と JPEG XR だけであり、"
            + $"減色（{nameof(EsImageSaveOptions.ColorCount)}）を使えるのは BMP / PNG / GIF だけです）。");

    /// <summary>拡張子が対応形式でないことを表す例外を作る。</summary>
    /// <param name="extension">拡張子。</param>
    /// <returns>例外。</returns>
    private static NotSupportedException CreateUnsupportedExtension(string extension)
        => new(extension.Length == 0
            ? $"保存先の拡張子がありません。 対応形式: {string.Join(" / ", SupportedExtensions)}"
            : $"拡張子 '{extension}' へは保存できません。 対応形式: {string.Join(" / ", SupportedExtensions)}");

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
        return DecodeFromFile(ReadAllBytes(filePath), maxSize, create);
    }

    /// <summary>ファイルの内容をデコードし、生成された値を返す。</summary>
    /// <typeparam name="T">生成する値の型。</typeparam>
    /// <param name="fileBytes">ファイルの内容（借用であり書き換えない）。</param>
    /// <param name="maxSize">長辺の上限（ピクセル）。 0 以下の場合は元の寸法。</param>
    /// <param name="create">寸法と充填処理を受け取る処理。</param>
    /// <returns>生成した値。</returns>
    /// <exception cref="ArgumentException">画像として解釈できない場合。</exception>
    internal static T DecodeFromFile<T>(
        ReadOnlySpan<byte> fileBytes, int maxSize, Func<int, int, Action<Span<byte>>, T> create)
    {
        var factory = OpenFactory();
        try
        {
            // メモリ ストリームから読む理由: WIC にファイルを開かせると解放まで掴んだままになる
            var stream = WinWicFactory.CreateMemoryStream(fileBytes);
            try
            {
                var decoder = CreateDecoder(factory, stream);
                try
                {
                    var frame = CreateFrame(decoder);
                    try
                    {
                        // 回転（EXIF の向き）→ 縮小 → 画素形式の変換の順に挟む（拡大はしない）
                        var rotator = WinWicFactory.ApplyOrientation(factory, frame);
                        try
                        {
                            IWICBitmapSource* oriented =
                                rotator != null ? (IWICBitmapSource*)rotator : (IWICBitmapSource*)frame;
                            var scaler = WinWicFactory.Scale(factory, oriented, maxSize);
                            try
                            {
                                IWICBitmapSource* scaled =
                                    scaler != null ? (IWICBitmapSource*)scaler : oriented;
                                var pixelFormat = PInvoke.GUID_WICPixelFormat32bppPBGRA;
                                var converter = WinWicFactory.Convert(factory, scaled, in pixelFormat);
                                try
                                {
                                    var (width, height) = WinWicFactory.GetSize((IWICBitmapSource*)converter);
                                    return create(
                                        width, height,
                                        destination => WinWicFactory.CopyPixels(
                                            (IWICBitmapSource*)converter, width, height, destination));
                                }
                                finally
                                {
                                    converter->Release();
                                }
                            }
                            finally
                            {
                                if (scaler != null)
                                {
                                    scaler->Release();
                                }
                            }
                        }
                        finally
                        {
                            if (rotator != null)
                            {
                                rotator->Release();
                            }
                        }
                    }
                    finally
                    {
                        frame->Release();
                    }
                }
                finally
                {
                    decoder->Release();
                }
            }
            finally
            {
                stream->Release();
            }
        }
        finally
        {
            factory->Release();
        }
    }

    /// <summary>ストリームからデコーダーを作る。</summary>
    /// <param name="factory">イメージング ファクトリ。</param>
    /// <param name="stream">内容を持つストリーム。</param>
    /// <returns>生成したデコーダー（呼び出し側が <c>Release()</c> を呼ぶ）。</returns>
    /// <exception cref="ArgumentException">画像として解釈できない場合。</exception>
    private static IWICBitmapDecoder* CreateDecoder(IWICImagingFactory* factory, IStream* stream)
    {
        try
        {
            return factory->CreateDecoderFromStream(
                stream, null, WICDecodeOptions.WICDecodeMetadataCacheOnDemand);
        }
        catch (COMException ex)
        {
            // 「画像として解釈できない」へ翻訳する（WIC はファイル不在と同じ状態値を返すため区別しない）
            throw new ArgumentException("画像として読み込めませんでした（形式が未対応か、ファイルが壊れています）。", ex);
        }
    }

    /// <summary>デコーダーから最初のフレームを取り出す。</summary>
    /// <param name="decoder">デコーダー。</param>
    /// <returns>生成したフレーム（呼び出し側が <c>Release()</c> を呼ぶ）。</returns>
    /// <exception cref="ArgumentException">フレームを取り出せない場合。</exception>
    private static IWICBitmapFrameDecode* CreateFrame(IWICBitmapDecoder* decoder)
    {
        try
        {
            IWICBitmapFrameDecode* frame;
            decoder->GetFrame(0, &frame);
            return frame;
        }
        catch (COMException ex)
        {
            // 「画像を取得できない」へ翻訳する（デコードできない形式や壊れたフレーム）
            throw new ArgumentException("画像を取得できませんでした（フレームが壊れています）。", ex);
        }
    }

    /// <summary>ファイルの内容を読み込む。</summary>
    /// <param name="filePath">対象のファイル。</param>
    /// <returns>内容。</returns>
    /// <exception cref="ArgumentException">ファイルが存在しない場合、または読み込めない場合。</exception>
    /// <remarks>
    /// <b>WIC にファイルを開かせない理由</b>: COM の解放は GC の時期に依存するため、WIC にファイルを開かせると
    /// <b>解放までファイルを掴んだまま</b>になる（実測: 削除が共有違反で失敗した）。
    /// <see cref="File"/> で読めば開いた直後に閉じるため、読み込み中でも削除も上書きもできる。
    /// </remarks>
    internal static byte[] ReadAllBytes(string filePath)
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
