using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>WIC の COM オブジェクトの生成・解放と呼び出しの補助。</summary>
/// <remarks>
/// <b>COM の解放は GC に任せる</b>: Native AOT では <c>Marshal.ReleaseComObject</c> /
/// <c>Marshal.FinalReleaseComObject</c> が <c>NotSupportedException: COM Interop is not supported on this platform.</c>
/// になる（実測）。 <see cref="StrategyBasedComWrappers"/> が作ったラッパーはファイナライズで参照を 1 つ戻し、
/// <c>GetOrCreateObjectForComInstance</c> は元の参照とは別に 1 つ数えるため、<b>合計は作った数と一致する</b>
/// （二重解放にはならない。 実測: GC を走らせても異常終了しない）。<br/>
/// <b>そのため COM にファイル ハンドルを持たせない</b>: 解放が GC の時期に依存するため、
/// <c>IWICStream::InitializeFromFilename</c> でファイルを開くと<b>解放までファイルが開いたまま</b>になる
/// （実測: 保存直後の削除が <c>IOException</c> になった）。 読み書きは<b>メモリ ストリーム</b>
/// （<c>CreateStreamOnHGlobal</c>）で行い、ファイルは <see cref="File"/> で読む・書く（実測: 直後の削除が成功する）。<br/>
/// <b>ジェネリックで包まない理由</b>: <c>(T)GetOrCreateObjectForComInstance(...)</c> のように型引数へキャストすると、
/// Native AOT では型のメタデータが残らず <c>InvalidOperationException</c> になる（実測）。
/// <b>インターフェイス型へ直接キャストする</b>こと。
/// </remarks>
[SupportedOSPlatform("windows")]
internal static unsafe class WinWicFactory
{
    /// <summary>COM のラッパーを作るための唯一のインスタンス。</summary>
    /// <remarks>
    /// <b>1 つだけ持つ理由</b>: <see cref="StrategyBasedComWrappers"/> はインスタンスごとに COM の同一性を持つため、
    /// 複数作ると同じ COM オブジェクトが別のラッパーになる（参照の数え方が分からなくなる）。
    /// </remarks>
    internal static readonly StrategyBasedComWrappers Wrappers = new();

    /// <summary>HRESULT が失敗であれば例外を送出する。</summary>
    /// <param name="hr">HRESULT。</param>
    /// <param name="message">例外のメッセージ。</param>
    /// <exception cref="Win32Exception"><paramref name="hr"/> が失敗の場合（状態値を持つ）。</exception>
    internal static void ThrowIfFailed(int hr, string message)
    {
        if (hr < 0)
        {
            throw new Win32Exception(hr, $"{message}（WIC の状態: 0x{hr:X8}）");
        }
    }

    /// <summary>WIC のイメージング ファクトリを生成する。</summary>
    /// <returns>生成したファクトリ。 生成できなかった場合は null。</returns>
    internal static IWICImagingFactory? CreateFactory()
    {
        if (!WinNative.EnsureInitialized())
        {
            return null;
        }

        var hr = WinNative.CoCreateInstance(
            WinNative.WicImagingFactoryClsid,
            IntPtr.Zero,
            1 /* CLSCTX_INPROC_SERVER */,
            WinNative.WicImagingFactoryIid,
            out var instance);
        return hr >= 0 && instance != IntPtr.Zero
            ? (IWICImagingFactory)Wrappers.GetOrCreateObjectForComInstance(instance, CreateObjectFlags.None)
            : null;
    }

    /// <summary>空のメモリ ストリームを作る（書き込み先）。</summary>
    /// <returns>生成したストリーム。</returns>
    /// <exception cref="Win32Exception">生成できなかった場合。</exception>
    internal static IStream CreateMemoryStream()
    {
        ThrowIfFailed(
            WinNative.CreateStreamOnHGlobal(IntPtr.Zero, 1 /* 解放時に領域も解放する */, out var instance),
            "メモリ ストリームを作成できませんでした。");
        return (IStream)Wrappers.GetOrCreateObjectForComInstance(instance, CreateObjectFlags.None);
    }

    /// <summary>内容を持つメモリ ストリームを作る（読み込み元）。</summary>
    /// <param name="data">ストリームの内容（借用であり書き換えない）。</param>
    /// <returns>生成したストリーム。</returns>
    /// <exception cref="Win32Exception">生成できなかった場合、または書き込めなかった場合。</exception>
    internal static IStream CreateMemoryStream(ReadOnlySpan<byte> data)
    {
        var stream = CreateMemoryStream();
        fixed (byte* source = data)
        {
            ThrowIfFailed(
                stream.Write(source, (uint)data.Length, out var written),
                "メモリ ストリームへ書き込めませんでした。");
            if (written != data.Length)
            {
                throw new Win32Exception(
                    0, $"メモリ ストリームへ書き込めた大きさが足りません（{written} / {data.Length} バイト）。");
            }
        }

        SeekToStart(stream);
        return stream;
    }

    /// <summary>ストリームの現在の内容をバイト列として取り出す。</summary>
    /// <param name="stream">対象のストリーム。</param>
    /// <returns>内容（ストリームの先頭から末尾まで）。</returns>
    /// <exception cref="Win32Exception">大きさを得られない・移動できない・読み込めない場合。</exception>
    internal static byte[] ReadStream(IStream stream)
    {
        var stat = default(WinStatStg);
        ThrowIfFailed(stream.Stat(&stat, 0), "メモリ ストリームの大きさを取得できませんでした。");
        if (stat.Size == 0)
        {
            return [];
        }

        if (stat.Size > int.MaxValue)
        {
            throw new Win32Exception(0, $"メモリ ストリームが大きすぎます（{stat.Size} バイト）。");
        }

        SeekToStart(stream);
        var buffer = new byte[(int)stat.Size];
        fixed (byte* destination = buffer)
        {
            ThrowIfFailed(
                stream.Read(destination, (uint)buffer.Length, out var read),
                "メモリ ストリームから読み込めませんでした。");
            if (read != buffer.Length)
            {
                throw new Win32Exception(
                    0, $"メモリ ストリームから読み込めた大きさが足りません（{read} / {buffer.Length} バイト）。");
            }
        }

        return buffer;
    }

    /// <summary>画素の寸法を得る。</summary>
    /// <param name="source">対象の画素の源。</param>
    /// <returns>幅と高さ（ピクセル）。</returns>
    /// <exception cref="Win32Exception">寸法を取得できなかった場合、または寸法が 1 以上でない場合。</exception>
    internal static (int Width, int Height) GetSize(IWICBitmapSource source)
    {
        ThrowIfFailed(source.GetSize(out var width, out var height), "画像の寸法を取得できませんでした。");
        return width is > 0 and <= int.MaxValue && height is > 0 and <= int.MaxValue
            ? ((int)width, (int)height)
            : throw new Win32Exception(0, $"画像の寸法が不正です（{width}x{height}）。");
    }

    /// <summary>長辺が上限を超える場合だけ、縮小する画素の源を作る。</summary>
    /// <param name="factory">イメージング ファクトリ。</param>
    /// <param name="source">変換元の画素の源。</param>
    /// <param name="maxSize">長辺の上限（ピクセル）。 0 以下の場合は縮小しない。</param>
    /// <returns>縮小した画素の源。 縮小しない場合は <paramref name="source"/> をそのまま返す。</returns>
    /// <exception cref="Win32Exception">縮小できなかった場合。</exception>
    /// <remarks>
    /// <b>目標の寸法は長辺を上限へ揃えて求める</b>（縦横比を保つ。 各辺は 1 以上）。
    /// 上限以下なら作らない（拡大はしない）。 補間は <see cref="WinWicInterfaces.InterpolationFant"/> を指定する。
    /// </remarks>
    internal static IWICBitmapSource Scale(IWICImagingFactory factory, IWICBitmapSource source, int maxSize)
    {
        if (maxSize <= 0)
        {
            return source;
        }

        var (width, height) = GetSize(source);
        if (width <= maxSize && height <= maxSize)
        {
            return source;
        }

        var scale = Math.Min((double)maxSize / width, (double)maxSize / height);
        var targetWidth = (uint)Math.Max(1, Math.Round(width * scale));
        var targetHeight = (uint)Math.Max(1, Math.Round(height * scale));

        ThrowIfFailed(factory.CreateBitmapScaler(out var scaler), "縮小用のスケーラーを作成できませんでした。");
        ThrowIfFailed(
            scaler!.Initialize(source, targetWidth, targetHeight, WinWicInterfaces.InterpolationFant),
            "画像を縮小できませんでした。");
        return scaler;
    }

    /// <summary>画素形式を変換した画素の源を作る。</summary>
    /// <param name="factory">イメージング ファクトリ。</param>
    /// <param name="source">変換元の画素の源。</param>
    /// <param name="pixelFormat">変換先の画素形式。</param>
    /// <returns>変換した画素の源。</returns>
    /// <exception cref="Win32Exception">変換器を作れない・初期化できない場合。</exception>
    internal static IWICBitmapSource Convert(IWICImagingFactory factory, IWICBitmapSource source, Guid pixelFormat)
    {
        ThrowIfFailed(factory.CreateFormatConverter(out var converter), "画素形式の変換器を作成できませんでした。");
        ThrowIfFailed(
            converter!.Initialize(source, in pixelFormat, 0, null, 0.0, 0),
            "画像の画素形式を変換できませんでした。");
        return converter;
    }

    /// <summary>EXIF の方向を適用した画素の源を返す。</summary>
    /// <param name="factory">イメージング ファクトリ。</param>
    /// <param name="frame">デコードしたフレーム。</param>
    /// <returns>回転・反転した画素の源。 方向が 1（そのまま）または読めない場合は <paramref name="frame"/> をそのまま返す。</returns>
    /// <exception cref="Win32Exception">回転・反転を作れない・初期化できない場合。</exception>
    /// <remarks>
    /// <b>回転してから縮小する</b>: 縮小してから回転すると丸め誤差の入り方が変わり、
    /// 他の UI 版（WinUI 3 の <c>ExifOrientationMode.RespectExifOrientation</c>）と画素が一致しなくなる。
    /// </remarks>
    internal static IWICBitmapSource ApplyOrientation(IWICImagingFactory factory, IWICBitmapFrameDecode frame)
    {
        var options = TransformOptions(ReadOrientation(frame));
        if (options == 0)
        {
            return frame;
        }

        ThrowIfFailed(factory.CreateBitmapFlipRotator(out var rotator), "回転・反転を作成できませんでした。");
        ThrowIfFailed(rotator!.Initialize(frame, options), "画像を回転できませんでした。");
        return rotator;
    }

    /// <summary>EXIF の方向（<c>Orientation</c>）を読む。</summary>
    /// <param name="frame">デコードしたフレーム。</param>
    /// <returns>方向（1〜8）。 読めない場合は 1（そのまま）。</returns>
    /// <remarks>
    /// <b>読めなくても例外にしない</b>: メタデータを持たない形式（BMP など）では読み取り器自体が無い。
    /// その場合は「回転しない」が正しい扱いである。<br/>
    /// <b>名前の階層は形式で異なる</b>ため、<see cref="WinWicInterfaces.OrientationNames"/> の順に試す。
    /// </remarks>
    internal static ushort ReadOrientation(IWICBitmapFrameDecode frame)
    {
        if (frame.GetMetadataQueryReader(out var reader) < 0 || reader is null)
        {
            return 1;
        }

        foreach (var name in WinWicInterfaces.OrientationNames)
        {
            var value = default(WinPropVariant);
            if (reader.GetMetadataByName(name, &value) < 0)
            {
                continue;
            }

            ushort orientation = value.Type switch
            {
                WinWicInterfaces.VariantTypeUnsignedShort => value.UnsignedShort,
                WinWicInterfaces.VariantTypeUnsignedInt => (ushort)value.UnsignedInt,
                _ => 0,
            };
            if (orientation is >= 1 and <= 8)
            {
                return orientation;
            }
        }

        return 1;
    }

    /// <summary>EXIF の方向を WIC の変換の指定へ変換する。</summary>
    /// <param name="orientation">EXIF の方向（1〜8）。</param>
    /// <returns>変換の指定（<c>WICBitmapTransformOptions</c>）。 回転しない場合は 0。</returns>
    /// <remarks>
    /// 指定は <c>Rotate0=0</c> / <c>Rotate90=1</c> / <c>Rotate180=2</c> / <c>Rotate270=3</c> /
    /// <c>FlipHorizontal=4</c> / <c>FlipVertical=8</c> の組み合わせである。
    /// </remarks>
    internal static int TransformOptions(ushort orientation) => orientation switch
    {
        2 => 4,
        3 => 2,
        4 => 8,
        5 => 1 | 4,
        6 => 1,
        7 => 3 | 4,
        8 => 3,
        _ => 0,
    };

    /// <summary>画素の源から画素をコピーする。</summary>
    /// <param name="source">コピー元（行幅は密である前提）。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="destination">コピー先（<c>幅 × 4 × 高さ</c> バイト以上）。</param>
    /// <exception cref="Win32Exception">画素を取得できなかった場合。</exception>
    /// <remarks>コピー先の行幅は常に密（<c>幅 × 4</c>）であるため、1 回の呼び出しで全領域を書く。</remarks>
    internal static void CopyPixels(IWICBitmapSource source, int width, int height, Span<byte> destination)
    {
        var stride = width * EsReadOnlyBitmap.BytesPerPixel;
        fixed (byte* target = destination)
        {
            ThrowIfFailed(
                source.CopyPixels(null, (uint)stride, (uint)(stride * height), target),
                "画像のピクセルを取得できませんでした。");
        }
    }

    /// <summary>エンコーダーのオプションへ実数値（0.0〜1.0）を書き込む。</summary>
    /// <param name="properties">エンコーダーが用意したオプション。</param>
    /// <param name="name">オプションの名前（<c>ImageQuality</c> など）。</param>
    /// <param name="value">書き込む値。</param>
    /// <returns>HRESULT。</returns>
    /// <remarks>
    /// <c>PROPBAG2</c>（名前と値の型を指定する構造体）と <c>VARIANT</c>（<c>VT_R4</c>）を組み立てて渡す。
    /// <b><c>Marshal.StructureToPtr</c> を使わない理由</b>: 実行時の型情報を必要とするため Native AOT で確実でない。
    /// どちらもアンマネージド型であるため、領域へ直接書き込む。
    /// </remarks>
    internal static unsafe int WriteRealOption(IPropertyBag2 properties, string name, float value)
    {
        const ushort VariantTypeR4 = 4;
        var bagSize = sizeof(WinPropBag2);
        var variantSize = sizeof(WinVariant);
        var memory = stackalloc byte[bagSize + variantSize];
        var bag = (WinPropBag2*)memory;
        var variant = (WinVariant*)(memory + bagSize);

        fixed (char* text = name)
        {
            *bag = new WinPropBag2 { ValueType = VariantTypeR4, Name = (IntPtr)text };
            *variant = new WinVariant { Type = VariantTypeR4, Float = value };
            return properties.Write(1, bag, variant);
        }
    }

    /// <summary>ストリームの位置を先頭へ戻す。</summary>
    /// <param name="stream">対象のストリーム。</param>
    /// <exception cref="Win32Exception">移動できなかった場合。</exception>
    private static void SeekToStart(IStream stream)
        => ThrowIfFailed(stream.Seek(0, 0 /* STREAM_SEEK_SET */, out _), "ストリームの位置を先頭へ戻せませんでした。");
}
