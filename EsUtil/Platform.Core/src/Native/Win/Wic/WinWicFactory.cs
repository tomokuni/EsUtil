using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using EsUtil.Platform.Core.Imaging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Imaging;
using Windows.Win32.System.Com;
using Windows.Win32.System.Com.StructuredStorage;
using Windows.Win32.System.Variant;

namespace EsUtil.Platform.Core.Native.Win.Wic;

/// <summary>WIC の COM の生成・解放と、画素の入出力の補助。</summary>
/// <remarks>
/// <b>宣言は CsWin32 が生成する</b>: <c>wincodec.h</c> の宣言から生成した型（<c>IWICImagingFactory</c> など）を
/// <b>生ポインターのまま</b>使う（<c>[GeneratedComInterface]</c> も <c>[ComImport]</c> も使わない）。
/// 生成された型は vtable を<b>メソッドの位置ではなく関数ポインターの表</b>として持つため、
/// <b>位置を数え間違えることがない</b>（実測: 手書きの宣言は位置がずれて <c>0xC0000005</c> になった）。<br/>
/// <b>解放は明示する</b>: <c>Release()</c> を呼ぶ（Native AOT では <c>Marshal.ReleaseComObject</c> が使えず、
/// GC 任せにすると COM にファイル ハンドルを持たせたときに解放の時期を読めない）。<br/>
/// <b>COM にファイル ハンドルを持たせない</b>: <c>IWICStream::InitializeFromFilename</c> でファイルを開くと
/// <b>解放までファイルが開いたまま</b>になる（実測: 保存直後の削除が <c>IOException</c> になった）。
/// 読み書きは<b>メモリ ストリーム</b>（<c>CreateStreamOnHGlobal</c>）で行い、ファイルは <see cref="File"/> が開いて閉じる。<br/>
/// <b>COM の初期化はプロセスで 1 回だけ</b>行う（<c>RPC_E_CHANGED_MODE</c> は「初期化済み」として成功に扱う）。
/// </remarks>
[SupportedOSPlatform("windows10.0.17763")]
internal static unsafe class WinWicFactory
{
    /// <summary>COM を初期化済みかどうか。</summary>
    private static bool s_comInitialized;

    /// <summary>HRESULT が失敗であれば例外を送出する。</summary>
    /// <param name="result">HRESULT。</param>
    /// <param name="message">例外のメッセージ。</param>
    /// <exception cref="Win32Exception"><paramref name="result"/> が失敗の場合（状態値を持つ）。</exception>
    internal static void ThrowIfFailed(HRESULT result, string message)
    {
        if (result.Failed)
        {
            throw new Win32Exception(result.Value, $"{message}（WIC の状態: 0x{result.Value:X8}）");
        }
    }

    /// <summary>COM を初期化する（1 回だけ呼ぶ）。</summary>
    /// <returns>初期化できた場合は true。 既に別のモデルで初期化済みの場合も true。</returns>
    /// <remarks>
    /// <c>RPC_E_CHANGED_MODE</c>（<c>0x80010106</c>）は「既に初期化済み」を表すため、成功として扱う
    /// （WIC は呼び出し側のアパートメントに依存しない。 実測で問題なく動作した）。
    /// </remarks>
    internal static bool EnsureInitialized()
    {
        if (s_comInitialized)
        {
            return true;
        }

        const int ChangedMode = unchecked((int)0x80010106);
        var result = PInvoke.CoInitializeEx(null, COINIT.COINIT_APARTMENTTHREADED);
        if (result.Value != ChangedMode && result.Failed)
        {
            return false;
        }

        s_comInitialized = true;
        return true;
    }

    /// <summary>WIC のイメージング ファクトリを生成する。</summary>
    /// <returns>生成したファクトリ。 生成できなかった場合は null。</returns>
    /// <remarks>呼び出し側が <c>Release()</c> を呼ぶ。</remarks>
    internal static IWICImagingFactory* Create()
    {
        if (!EnsureInitialized())
        {
            return null;
        }

        var result = PInvoke.CoCreateInstance(
            PInvoke.CLSID_WICImagingFactory, null, CLSCTX.CLSCTX_INPROC_SERVER, out IWICImagingFactory* factory);
        return result.Failed ? null : factory;
    }

    /// <summary>空のメモリ ストリームを作る（書き込み先）。</summary>
    /// <returns>生成したストリーム。</returns>
    /// <exception cref="Win32Exception">生成できなかった場合。</exception>
    /// <remarks>呼び出し側が <c>Release()</c> を呼ぶ（解放時に領域も解放される）。</remarks>
    internal static IStream* CreateMemoryStream()
    {
        IStream* stream;
        ThrowIfFailed(
            PInvoke.CreateStreamOnHGlobal(default, true, &stream),
            "メモリ ストリームを作成できませんでした。");
        return stream;
    }

    /// <summary>内容を持つメモリ ストリームを作る（読み込み元）。</summary>
    /// <param name="data">ストリームの内容（借用であり書き換えない）。</param>
    /// <returns>生成したストリーム。</returns>
    /// <exception cref="Win32Exception">生成できなかった場合、または書き込めなかった場合。</exception>
    /// <remarks>呼び出し側が <c>Release()</c> を呼ぶ。</remarks>
    internal static IStream* CreateMemoryStream(ReadOnlySpan<byte> data)
    {
        var stream = CreateMemoryStream();
        try
        {
            fixed (byte* source = data)
            {
                uint written;
                ThrowIfFailed(
                    stream->Write(source, (uint)data.Length, &written),
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
        catch
        {
            // 失敗したストリームを解放してから、呼び出し側へ伝える（状態値はそのまま残す）
            stream->Release();
            throw;
        }
    }

    /// <summary>ストリームの現在の内容をバイト列として取り出す。</summary>
    /// <param name="stream">対象のストリーム。</param>
    /// <returns>内容（ストリームの先頭から末尾まで）。</returns>
    /// <exception cref="Win32Exception">大きさを得られない・移動できない・読み込めない場合。</exception>
    internal static byte[] ReadStream(IStream* stream)
    {
        STATSTG stat;
        stream->Stat(&stat, 0);
        if (stat.cbSize == 0)
        {
            return [];
        }

        if (stat.cbSize > int.MaxValue)
        {
            throw new Win32Exception(0, $"メモリ ストリームが大きすぎます（{stat.cbSize} バイト）。");
        }

        SeekToStart(stream);
        var buffer = new byte[(int)stat.cbSize];
        fixed (byte* destination = buffer)
        {
            uint read;
            ThrowIfFailed(
                stream->Read(destination, (uint)buffer.Length, &read),
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
    /// <exception cref="Win32Exception">寸法が 1 以上でない場合。</exception>
    internal static (int Width, int Height) GetSize(IWICBitmapSource* source)
    {
        uint width;
        uint height;
        source->GetSize(&width, &height);
        return width is > 0 and <= int.MaxValue && height is > 0 and <= int.MaxValue
            ? ((int)width, (int)height)
            : throw new Win32Exception(0, $"画像の寸法が不正です（{width}x{height}）。");
    }

    /// <summary>長辺が上限を超える場合だけ、縮小するスケーラーを作る。</summary>
    /// <param name="factory">イメージング ファクトリ。</param>
    /// <param name="source">変換元の画素の源。</param>
    /// <param name="maxSize">長辺の上限（ピクセル）。 0 以下の場合は縮小しない。</param>
    /// <returns>縮小した画素の源。 縮小しない場合は null（<paramref name="source"/> をそのまま使う）。</returns>
    /// <remarks>
    /// <b>目標の寸法は長辺を上限へ揃えて求める</b>（縦横比を保つ。 各辺は 1 以上）。
    /// 上限以下なら作らない（拡大はしない）。 補間は <c>Fant</c> を指定する。
    /// 呼び出し側が <c>Release()</c> を呼ぶ。
    /// </remarks>
    internal static IWICBitmapScaler* Scale(IWICImagingFactory* factory, IWICBitmapSource* source, int maxSize)
    {
        if (maxSize <= 0)
        {
            return null;
        }

        var (width, height) = GetSize(source);
        if (width <= maxSize && height <= maxSize)
        {
            return null;
        }

        var scale = Math.Min((double)maxSize / width, (double)maxSize / height);
        var targetWidth = (uint)Math.Max(1, Math.Round(width * scale));
        var targetHeight = (uint)Math.Max(1, Math.Round(height * scale));

        IWICBitmapScaler* scaler;
        factory->CreateBitmapScaler(&scaler);
        try
        {
            scaler->Initialize(
                source, targetWidth, targetHeight, WICBitmapInterpolationMode.WICBitmapInterpolationModeFant);
            return scaler;
        }
        catch
        {
            // 失敗したスケーラーを解放してから、呼び出し側へ伝える
            scaler->Release();
            throw;
        }
    }

    /// <summary>画素形式を変換する変換器を作る。</summary>
    /// <param name="factory">イメージング ファクトリ。</param>
    /// <param name="source">変換元の画素の源。</param>
    /// <param name="pixelFormat">変換先の画素形式。</param>
    /// <returns>変換器。</returns>
    /// <remarks>呼び出し側が <c>Release()</c> を呼ぶ。</remarks>
    internal static IWICFormatConverter* Convert(
        IWICImagingFactory* factory, IWICBitmapSource* source, in Guid pixelFormat)
    {
        IWICFormatConverter* converter;
        factory->CreateFormatConverter(&converter);
        try
        {
            converter->Initialize(
                source, pixelFormat, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0.0,
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

    /// <summary>EXIF の方向を適用する回転・反転を作る。</summary>
    /// <param name="factory">イメージング ファクトリ。</param>
    /// <param name="frame">デコードしたフレーム。</param>
    /// <returns>回転・反転。 方向が 1（そのまま）または読めない場合は null（<paramref name="frame"/> をそのまま使う）。</returns>
    /// <remarks>
    /// <b>回転してから縮小する</b>: 縮小してから回転すると丸め誤差の入り方が変わり、
    /// 他の UI 版（WinUI 3 の <c>ExifOrientationMode.RespectExifOrientation</c>）と画素が一致しなくなる。
    /// 呼び出し側が <c>Release()</c> を呼ぶ。
    /// </remarks>
    internal static IWICBitmapFlipRotator* ApplyOrientation(IWICImagingFactory* factory, IWICBitmapFrameDecode* frame)
    {
        var options = TransformOptions(ReadOrientation(frame));
        if (options == WICBitmapTransformOptions.WICBitmapTransformRotate0)
        {
            return null;
        }

        IWICBitmapFlipRotator* rotator;
        factory->CreateBitmapFlipRotator(&rotator);
        try
        {
            rotator->Initialize((IWICBitmapSource*)frame, options);
            return rotator;
        }
        catch
        {
            // 失敗した回転・反転を解放してから、呼び出し側へ伝える
            rotator->Release();
            throw;
        }
    }

    /// <summary>EXIF の方向（<c>Orientation</c>）を読む。</summary>
    /// <param name="frame">デコードしたフレーム。</param>
    /// <returns>方向（1〜8）。 読めない場合は 1（そのまま）。</returns>
    /// <remarks>
    /// <b>読めなくても例外にしない</b>: メタデータを持たない形式（BMP など）では読み取り器自体が無い。
    /// その場合は「回転しない」が正しい扱いである。<br/>
    /// <b>名前の階層は形式で異なる</b>ため、<see cref="WinWicConstants.OrientationNames"/> の順に試す。
    /// </remarks>
    internal static ushort ReadOrientation(IWICBitmapFrameDecode* frame)
    {
        IWICMetadataQueryReader* reader;
        try
        {
            frame->GetMetadataQueryReader(&reader);
        }
        catch (COMException)
        {
            // メタデータを持たない形式（BMP など）は読み取り器自体が作れない（回転しないのが正しい扱い）
            return 1;
        }

        try
        {
            foreach (var name in WinWicConstants.OrientationNames)
            {
                PROPVARIANT value = default;
                try
                {
                    reader->GetMetadataByName(name, ref value);
                }
                catch (COMException)
                {
                    // その形式に無い名前は失敗する（次の候補へ進む）
                    continue;
                }

                ushort orientation = value.Anonymous.Anonymous.vt switch
                {
                    VARENUM.VT_UI2 => value.Anonymous.Anonymous.Anonymous.uiVal,
                    VARENUM.VT_UI4 => (ushort)value.Anonymous.Anonymous.Anonymous.ulVal,
                    _ => 0,
                };
                if (orientation is >= 1 and <= 8)
                {
                    return orientation;
                }
            }

            return 1;
        }
        finally
        {
            reader->Release();
        }
    }

    /// <summary>EXIF の方向を WIC の変換の指定へ変換する。</summary>
    /// <param name="orientation">EXIF の方向（1〜8）。</param>
    /// <returns>変換の指定。 回転しない場合は <c>Rotate0</c>。</returns>
    internal static WICBitmapTransformOptions TransformOptions(ushort orientation) => orientation switch
    {
        2 => WICBitmapTransformOptions.WICBitmapTransformFlipHorizontal,
        3 => WICBitmapTransformOptions.WICBitmapTransformRotate180,
        4 => WICBitmapTransformOptions.WICBitmapTransformFlipVertical,
        5 => WICBitmapTransformOptions.WICBitmapTransformRotate90 | WICBitmapTransformOptions.WICBitmapTransformFlipHorizontal,
        6 => WICBitmapTransformOptions.WICBitmapTransformRotate90,
        7 => WICBitmapTransformOptions.WICBitmapTransformRotate270 | WICBitmapTransformOptions.WICBitmapTransformFlipHorizontal,
        8 => WICBitmapTransformOptions.WICBitmapTransformRotate270,
        _ => WICBitmapTransformOptions.WICBitmapTransformRotate0,
    };

    /// <summary>画素の源から画素をコピーする。</summary>
    /// <param name="source">コピー元（行幅は密である前提）。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="destination">コピー先（<c>幅 × 4 × 高さ</c> バイト以上）。</param>
    /// <remarks>コピー先の行幅は常に密（<c>幅 × 4</c>）であるため、1 回の呼び出しで全領域を書く。</remarks>
    internal static void CopyPixels(IWICBitmapSource* source, int width, int height, Span<byte> destination)
    {
        var stride = width * EsReadOnlyBitmap.BytesPerPixel;
        fixed (byte* target = destination)
        {
            source->CopyPixels(null, (uint)stride, (uint)(stride * height), target);
        }
    }

    /// <summary>エンコーダーのオプションへ実数値（0.0〜1.0）を書き込む。</summary>
    /// <param name="properties">エンコーダーが用意したオプション。</param>
    /// <param name="name">オプションの名前（<c>ImageQuality</c> など）。</param>
    /// <param name="value">書き込む値。</param>
    /// <remarks>
    /// <c>PROPBAG2</c>（名前と値の型を指定する構造体）と <c>VARIANT</c>（<c>VT_R4</c>）を組み立てて渡す。
    /// <b>どちらもアンマネージド型であるため、領域へ直接書き込む</b>（実行時の型情報を使わない）。
    /// </remarks>
    internal static void WriteRealOption(IPropertyBag2* properties, string name, float value)
    {
        fixed (char* text = name)
        {
            var bag = new PROPBAG2
            {
                vt = VARENUM.VT_R4,
                pstrName = text,
            };
            var variant = new VARIANT();
            variant.Anonymous.Anonymous.vt = VARENUM.VT_R4;
            variant.Anonymous.Anonymous.Anonymous.fltVal = value;
            properties->Write(1, &bag, &variant);
        }
    }

    /// <summary>ストリームの位置を先頭へ戻す。</summary>
    /// <param name="stream">対象のストリーム。</param>
    private static void SeekToStart(IStream* stream) => stream->Seek(0, SeekOrigin.Begin, null);
}
