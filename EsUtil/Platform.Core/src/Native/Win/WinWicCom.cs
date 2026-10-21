using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>WIC の COM インターフェイスを vtable の関数ポインターで呼ぶ。</summary>
/// <remarks>
/// <b>なぜ <c>[ComImport]</c> を使わないか</b>: Native AOT では <c>[ComImport]</c> と
/// <c>Marshal.GetObjectForIUnknown</c> の組み合わせが動作しない
/// （実測: <c>NotSupportedException: COM Interop requires ComWrapper instance registered for marshalling.</c>）。
/// vtable の位置を直接呼ぶ方式は P/Invoke と同じ性質であり、AOT でも動作することを実測済み。<br/>
/// <b>vtable の位置は wincodec.h の宣言順</b>である（0〜2 は <c>IUnknown</c>）。 位置を推測しないこと。<br/>
/// <b>解放</b>: 生成したオブジェクトは <see cref="Release"/> で参照カウントを 1 減らす
/// （<c>AddRef</c> は呼ばないため、生成したものを 1 回だけ解放すれば足りる）。<br/>
/// <b>失敗の翻訳</b>: 呼び出し側は HRESULT を <see cref="ThrowIfFailed"/> で例外へ翻訳する。
/// </remarks>
[SupportedOSPlatform("windows")]
internal static unsafe class WinWicCom
{
    /// <summary><c>IWICImagingFactory::CreateDecoderFromFilename</c> の vtable 位置。</summary>
    private const int FactoryCreateDecoderFromFilename = 3;

    /// <summary><c>IWICImagingFactory::CreateEncoder</c> の vtable 位置。</summary>
    private const int FactoryCreateEncoder = 8;

    /// <summary><c>IWICImagingFactory::CreatePalette</c> の vtable 位置。</summary>
    private const int FactoryCreatePalette = 9;

    /// <summary><c>IWICImagingFactory::CreateFormatConverter</c> の vtable 位置。</summary>
    private const int FactoryCreateFormatConverter = 10;

    /// <summary><c>IWICImagingFactory::CreateBitmapScaler</c> の vtable 位置。</summary>
    private const int FactoryCreateBitmapScaler = 11;

    /// <summary><c>IWICImagingFactory::CreateStream</c> の vtable 位置。</summary>
    private const int FactoryCreateStream = 14;

    /// <summary><c>IWICBitmapDecoder::GetContainerFormat</c> の vtable 位置。</summary>

    /// <summary><c>IWICBitmapDecoder::GetFrame</c> の vtable 位置。</summary>
    private const int DecoderGetFrame = 13;

    /// <summary><c>IWICBitmapSource::GetSize</c> の vtable 位置。</summary>
    private const int SourceGetSize = 3;

    /// <summary><c>IWICBitmapSource::CopyPixels</c> の vtable 位置。</summary>
    private const int SourceCopyPixels = 7;

    /// <summary><c>IWICFormatConverter::Initialize</c> の vtable 位置。</summary>
    private const int ConverterInitialize = 8;

    /// <summary><c>IWICBitmapScaler::Initialize</c> の vtable 位置（<c>IWICBitmapSource</c> の 5 件の後）。</summary>
    private const int ScalerInitialize = 8;

    /// <summary><c>IWICPalette::InitializeCustom</c> の vtable 位置。</summary>
    private const int PaletteInitializeCustom = 4;

    /// <summary><c>IWICStream::InitializeFromFilename</c> の vtable 位置（<c>IStream</c> の 11 件の後）。</summary>
    private const int StreamInitializeFromFilename = 15;

    /// <summary><c>IWICBitmapEncoder::Initialize</c> の vtable 位置。</summary>
    private const int EncoderInitialize = 3;

    /// <summary><c>IWICBitmapEncoder::CreateNewFrame</c> の vtable 位置。</summary>
    private const int EncoderCreateNewFrame = 10;

    /// <summary><c>IWICBitmapEncoder::Commit</c> の vtable 位置。</summary>
    private const int EncoderCommit = 11;

    /// <summary><c>IWICBitmapFrameEncode::Initialize</c> の vtable 位置。</summary>
    private const int FrameInitialize = 3;

    /// <summary><c>IWICBitmapFrameEncode::SetSize</c> の vtable 位置。</summary>
    private const int FrameSetSize = 4;

    /// <summary><c>IWICBitmapFrameEncode::SetPalette</c> の vtable 位置。</summary>
    private const int FrameSetPalette = 8;

    /// <summary><c>IWICBitmapFrameEncode::WritePixels</c> の vtable 位置。</summary>
    private const int FrameWritePixels = 10;

    /// <summary><c>IWICBitmapFrameEncode::SetPixelFormat</c> の vtable 位置。</summary>
    private const int FrameSetPixelFormat = 6;

    /// <summary><c>IWICBitmapFrameEncode::Commit</c> の vtable 位置。</summary>
    private const int FrameCommit = 12;

    /// <summary><c>IPropertyBag2::Write</c> の vtable 位置。</summary>
    /// <remarks><c>IPropertyBag2</c> は <c>Read</c>（3）→ <c>Write</c>（4）→ <c>CountProperties</c>（5）の順である。</remarks>
    private const int BagWrite = 4;

    /// <summary>
    /// エンコーダーのキャッシュ方法（<c>WICBitmapEncoderCacheTempFile</c> = 2）。
    /// </summary>
    /// <remarks>
    /// <b><c>WICBitmapEncoderNoCache</c>（0）は使えない（実測）</b>: <c>IWICStream::InitializeFromFilename</c> で
    /// 開いたストリームを渡すと <c>WINCODEC_ERR_UNSUPPORTEDOPERATION</c>（<c>0x88982F81</c>）で初期化が失敗する。
    /// 一時ファイルへ書いてから確定する方法は全形式で成功した。
    /// </remarks>
    private const uint EncoderCacheTempFile = 2;

    /// <summary>書き込み用にファイルを開くアクセス権（<c>GENERIC_WRITE</c>）。</summary>
    private const uint GenericWrite = 0x40000000;

    /// <summary>スケーラーの補間方法（<c>WICBitmapInterpolationModeFant</c>）。</summary>
    /// <remarks>
    /// <c>Fant</c> は画素の面積平均に近い補間であり、縮小で細線が消えたり濃くなったりしにくい。<br/>
    /// 他の UI 版（WinUI 3 の <c>BitmapInterpolationMode.Fant</c>）と同じ補間である。
    /// </remarks>
    private const int InterpolationFant = 3;
    /// <summary>読み込み用にファイルを開くアクセス権（<c>GENERIC_READ</c>）。</summary>
    private const uint GenericRead = 0x80000000;

    /// <summary>HRESULT が失敗であれば例外を送出する。</summary>
    /// <param name="hr">HRESULT。</param>
    /// <param name="message">例外のメッセージ。</param>
    /// <exception cref="Win32Exception"><paramref name="hr"/> が失敗の場合（状態値を持つ）。</exception>
    internal static void ThrowIfFailed(int hr, string message)
    {
        if (!WinNative.Succeeded(hr))
        {
            throw new Win32Exception(hr, $"{message}（WIC の状態: 0x{hr:X8}）");
        }
    }

    /// <summary>COM のオブジェクトの参照カウントを 1 減らす。</summary>
    /// <param name="instance">対象のオブジェクト。 0 の場合は何もしない。</param>
    internal static void Release(IntPtr instance)
    {
        if (instance == IntPtr.Zero)
        {
            return;
        }

        var release = (delegate* unmanaged[Stdcall]<IntPtr, uint>)(*(IntPtr**)instance.ToPointer())[2];
        _ = release(instance);
    }

    /// <summary>ファイルからデコーダーを作る。</summary>
    /// <param name="factory">イメージング ファクトリ。</param>
    /// <param name="filePath">読み込むファイル。</param>
    /// <param name="decoder">生成したデコーダー（失敗した場合は 0）。</param>
    /// <returns>HRESULT。</returns>
    internal static int CreateDecoderFromFilename(IntPtr factory, string filePath, out IntPtr decoder)
    {
        var create = (delegate* unmanaged[Stdcall]<IntPtr, char*, IntPtr, uint, int, IntPtr*, int>)
            (*(IntPtr**)factory.ToPointer())[FactoryCreateDecoderFromFilename];
        fixed (char* path = filePath)
        {
            IntPtr instance;
            // 引数: ファイル名 / 希望するベンダー（指定しない）/ アクセス / キャッシュ（既定）
            var hr = create(factory, path, IntPtr.Zero, GenericRead, 0, &instance);
            decoder = WinNative.Succeeded(hr) ? instance : IntPtr.Zero;
            return hr;
        }
    }


    /// <summary>コンテナ形式からエンコーダーを作る。</summary>
    /// <param name="factory">イメージング ファクトリ。</param>
    /// <param name="containerFormat">コンテナ形式の GUID。</param>
    /// <param name="encoder">生成したエンコーダー（失敗した場合は 0）。</param>
    /// <returns>HRESULT。</returns>
    internal static int CreateEncoder(IntPtr factory, Guid containerFormat, out IntPtr encoder)
    {
        var create = (delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr, IntPtr*, int>)
            (*(IntPtr**)factory.ToPointer())[FactoryCreateEncoder];
        IntPtr instance;
        var hr = create(factory, &containerFormat, IntPtr.Zero, &instance);
        encoder = WinNative.Succeeded(hr) ? instance : IntPtr.Zero;
        return hr;
    }

    /// <summary>パレットを作る。</summary>
    /// <param name="factory">イメージング ファクトリ。</param>
    /// <param name="palette">生成したパレット（失敗した場合は 0）。</param>
    /// <returns>HRESULT。</returns>
    internal static int CreatePalette(IntPtr factory, out IntPtr palette)
    {
        var create = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)(*(IntPtr**)factory.ToPointer())[FactoryCreatePalette];
        IntPtr instance;
        var hr = create(factory, &instance);
        palette = WinNative.Succeeded(hr) ? instance : IntPtr.Zero;
        return hr;
    }

    /// <summary>画素形式の変換器を作る。</summary>
    /// <param name="factory">イメージング ファクトリ。</param>
    /// <param name="converter">生成した変換器（失敗した場合は 0）。</param>
    /// <returns>HRESULT。</returns>
    internal static int CreateFormatConverter(IntPtr factory, out IntPtr converter)
    {
        var create = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)(*(IntPtr**)factory.ToPointer())[FactoryCreateFormatConverter];
        IntPtr instance;
        var hr = create(factory, &instance);
        converter = WinNative.Succeeded(hr) ? instance : IntPtr.Zero;
        return hr;
    }

    /// <summary>縮小（拡大）用のスケーラーを作る。</summary>
    /// <param name="factory">イメージング ファクトリ。</param>
    /// <param name="scaler">生成したスケーラー（失敗した場合は 0）。</param>
    /// <returns>HRESULT。</returns>
    internal static int CreateBitmapScaler(IntPtr factory, out IntPtr scaler)
    {
        var create = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)(*(IntPtr**)factory.ToPointer())[FactoryCreateBitmapScaler];
        IntPtr instance;
        var hr = create(factory, &instance);
        scaler = WinNative.Succeeded(hr) ? instance : IntPtr.Zero;
        return hr;
    }

    /// <summary>スケーラーへ変換元と目標の寸法を設定する。</summary>
    /// <param name="scaler">対象のスケーラー。</param>
    /// <param name="source">変換元の画素源（デコーダーのフレーム）。</param>
    /// <param name="width">目標の幅。</param>
    /// <param name="height">目標の高さ。</param>
    /// <returns>HRESULT。</returns>
    /// <remarks>
    /// <b>補間は Fant を指定する</b>（画素の面積平均に近く、縮小で細線が消えたり濃くなったりしにくい）。
    /// 他の UI 版（WinUI 3 の <c>BitmapInterpolationMode.Fant</c>）と同じ補間であり、同じ画質になる。
    /// </remarks>
    internal static int InitializeScaler(IntPtr scaler, IntPtr source, uint width, uint height)
    {
        var initialize = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, uint, int, int>)
            (*(IntPtr**)scaler.ToPointer())[ScalerInitialize];
        return initialize(scaler, source, width, height, InterpolationFant);
    }

    /// <summary>書き込み用のストリームを作る。</summary>
    /// <param name="factory">イメージング ファクトリ。</param>
    /// <param name="stream">生成したストリーム（失敗した場合は 0）。</param>
    /// <returns>HRESULT。</returns>
    internal static int CreateStream(IntPtr factory, out IntPtr stream)
    {
        var create = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)(*(IntPtr**)factory.ToPointer())[FactoryCreateStream];
        IntPtr instance;
        var hr = create(factory, &instance);
        stream = WinNative.Succeeded(hr) ? instance : IntPtr.Zero;
        return hr;
    }

    /// <summary>ストリームを書き込み用のファイルへ結び付ける。</summary>
    /// <param name="stream">対象のストリーム。</param>
    /// <param name="filePath">対象のファイル。</param>
    /// <returns>HRESULT。</returns>
    internal static int InitializeStreamForWrite(IntPtr stream, string filePath)
    {
        var initialize = (delegate* unmanaged[Stdcall]<IntPtr, char*, uint, int>)
            (*(IntPtr**)stream.ToPointer())[StreamInitializeFromFilename];
        fixed (char* path = filePath)
        {
            return initialize(stream, path, GenericWrite);
        }
    }


    /// <summary>デコーダーからフレームを得る。</summary>
    /// <param name="decoder">対象のデコーダー。</param>
    /// <param name="index">フレームの番号（先頭は 0）。</param>
    /// <param name="frame">得たフレーム（失敗した場合は 0）。</param>
    /// <returns>HRESULT。</returns>
    internal static int GetFrame(IntPtr decoder, uint index, out IntPtr frame)
    {
        var get = (delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int>)(*(IntPtr**)decoder.ToPointer())[DecoderGetFrame];
        IntPtr instance;
        var hr = get(decoder, index, &instance);
        frame = WinNative.Succeeded(hr) ? instance : IntPtr.Zero;
        return hr;
    }

    /// <summary>画素の寸法を得る。</summary>
    /// <param name="source">対象の画素源。</param>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    /// <returns>HRESULT。</returns>
    internal static int GetSize(IntPtr source, out uint width, out uint height)
    {
        var get = (delegate* unmanaged[Stdcall]<IntPtr, uint*, uint*, int>)(*(IntPtr**)source.ToPointer())[SourceGetSize];
        uint w;
        uint h;
        var hr = get(source, &w, &h);
        width = w;
        height = h;
        return hr;
    }

    /// <summary>画素をコピーする。</summary>
    /// <param name="source">コピー元の画素源。</param>
    /// <param name="stride">コピー先の行幅（バイト）。</param>
    /// <param name="length">コピー先の大きさ（バイト）。</param>
    /// <param name="destination">コピー先。</param>
    /// <returns>HRESULT。</returns>
    internal static int CopyPixels(IntPtr source, uint stride, uint length, void* destination)
    {
        var copy = (delegate* unmanaged[Stdcall]<IntPtr, void*, uint, uint, void*, int>)(*(IntPtr**)source.ToPointer())[SourceCopyPixels];
        return copy(source, null, stride, length, destination);
    }

    /// <summary>画素形式の変換を設定する。</summary>
    /// <param name="converter">対象の変換器。</param>
    /// <param name="source">変換元の画素源。</param>
    /// <param name="pixelFormat">変換先の画素形式。</param>
    /// <returns>HRESULT。</returns>
    internal static int InitializeConverter(IntPtr converter, IntPtr source, Guid pixelFormat)
    {
        var initialize = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, Guid*, int, IntPtr, double, int, int>)
            (*(IntPtr**)converter.ToPointer())[ConverterInitialize];
        // 引数: 変換元 / 変換先の画素形式 / ディザ（なし）/ パレット（なし）/ アルファ閾値（0）/ パレットの種類（任意）
        return initialize(converter, source, &pixelFormat, 0, IntPtr.Zero, 0.0, 0);
    }

    /// <summary>パレットの色を設定する。</summary>
    /// <param name="palette">対象のパレット。</param>
    /// <param name="colors">色（<c>0xAARRGGBB</c> の並び）。</param>
    /// <returns>HRESULT。</returns>
    internal static int InitializePalette(IntPtr palette, ReadOnlySpan<uint> colors)
    {
        var initialize = (delegate* unmanaged[Stdcall]<IntPtr, uint*, uint, int>)(*(IntPtr**)palette.ToPointer())[PaletteInitializeCustom];
        fixed (uint* values = colors)
        {
            return initialize(palette, values, (uint)colors.Length);
        }
    }

    /// <summary>エンコーダーを初期化する。</summary>
    /// <param name="encoder">対象のエンコーダー。</param>
    /// <param name="stream">書き込み先のストリーム。</param>
    /// <returns>HRESULT。</returns>
    internal static int InitializeEncoder(IntPtr encoder, IntPtr stream)
    {
        var initialize = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, int>)(*(IntPtr**)encoder.ToPointer())[EncoderInitialize];
        // 引数: ストリーム / キャッシュ（一時ファイルへ書いてから確定する）
        return initialize(encoder, stream, EncoderCacheTempFile);
    }

    /// <summary>エンコーダーへ新しいフレームを作る。</summary>
    /// <param name="encoder">対象のエンコーダー。</param>
    /// <param name="frame">生成したフレーム（失敗した場合は 0）。</param>
    /// <param name="properties">エンコーダーが用意したオプション（品質などを書き込む。 失敗した場合は 0）。</param>
    /// <returns>HRESULT。</returns>
    internal static int CreateNewFrame(IntPtr encoder, out IntPtr frame, out IntPtr properties)
    {
        var create = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, IntPtr*, int>)(*(IntPtr**)encoder.ToPointer())[EncoderCreateNewFrame];
        IntPtr instance;
        IntPtr bag;
        var hr = create(encoder, &instance, &bag);
        frame = WinNative.Succeeded(hr) ? instance : IntPtr.Zero;
        properties = WinNative.Succeeded(hr) ? bag : IntPtr.Zero;
        return hr;
    }

    /// <summary>エンコーダーの内容を確定する。</summary>
    /// <param name="encoder">対象のエンコーダー。</param>
    /// <returns>HRESULT。</returns>
    internal static int CommitEncoder(IntPtr encoder)
    {
        var commit = (delegate* unmanaged[Stdcall]<IntPtr, int>)(*(IntPtr**)encoder.ToPointer())[EncoderCommit];
        return commit(encoder);
    }

    /// <summary>フレームを初期化する。</summary>
    /// <param name="frame">対象のフレーム。</param>
    /// <param name="properties">エンコーダーが用意したオプション。</param>
    /// <returns>HRESULT。</returns>
    internal static int InitializeFrame(IntPtr frame, IntPtr properties)
    {
        var initialize = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int>)(*(IntPtr**)frame.ToPointer())[FrameInitialize];
        return initialize(frame, properties);
    }

    /// <summary>フレームの寸法を設定する。</summary>
    /// <param name="frame">対象のフレーム。</param>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    /// <returns>HRESULT。</returns>
    internal static int SetFrameSize(IntPtr frame, uint width, uint height)
    {
        var set = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, int>)(*(IntPtr**)frame.ToPointer())[FrameSetSize];
        return set(frame, width, height);
    }

    /// <summary>フレームへパレットを設定する。</summary>
    /// <param name="frame">対象のフレーム。</param>
    /// <param name="palette">対象のパレット。</param>
    /// <returns>HRESULT。</returns>
    internal static int SetFramePalette(IntPtr frame, IntPtr palette)
    {
        var set = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int>)(*(IntPtr**)frame.ToPointer())[FrameSetPalette];
        return set(frame, palette);
    }

    /// <summary>フレームの画素形式を設定する。</summary>
    /// <param name="frame">対象のフレーム。</param>
    /// <param name="pixelFormat">設定する画素形式（エンコーダーが対応しない場合は最も近い形式へ書き換えられる）。</param>
    /// <returns>HRESULT。</returns>
    /// <remarks>書き換えられたかどうかは <paramref name="pixelFormat"/> を見て判定する（呼び出し側の責任）。</remarks>
    internal static int SetFramePixelFormat(IntPtr frame, ref Guid pixelFormat)
    {
        var set = (delegate* unmanaged[Stdcall]<IntPtr, Guid*, int>)(*(IntPtr**)frame.ToPointer())[FrameSetPixelFormat];
        fixed (Guid* format = &pixelFormat)
        {
            return set(frame, format);
        }
    }

    /// <summary>フレームへ画素を書き込む。</summary>
    /// <param name="frame">対象のフレーム。</param>
    /// <param name="lineCount">行数。</param>
    /// <param name="stride">行幅（バイト）。</param>
    /// <param name="length">書き込む大きさ（バイト）。</param>
    /// <param name="pixels">画素。</param>
    /// <returns>HRESULT。</returns>
    internal static int WritePixels(IntPtr frame, uint lineCount, uint stride, uint length, void* pixels)
    {
        var write = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, uint, void*, int>)(*(IntPtr**)frame.ToPointer())[FrameWritePixels];
        return write(frame, lineCount, stride, length, pixels);
    }

    /// <summary>フレームの内容を確定する。</summary>
    /// <param name="frame">対象のフレーム。</param>
    /// <returns>HRESULT。</returns>
    internal static int CommitFrame(IntPtr frame)
    {
        var commit = (delegate* unmanaged[Stdcall]<IntPtr, int>)(*(IntPtr**)frame.ToPointer())[FrameCommit];
        return commit(frame);
    }

    /// <summary>エンコーダーのオプションへ実数値（0.0〜1.0）を書き込む。</summary>
    /// <param name="properties">エンコーダーが用意したオプション。</param>
    /// <param name="name">オプションの名前（<c>ImageQuality</c> など）。</param>
    /// <param name="value">書き込む値。</param>
    /// <returns>HRESULT。</returns>
    /// <remarks>
    /// <c>PROPBAG2</c>（名前と値の型を指定する構造体）と <c>VARIANT</c>（<c>VT_R4</c>）を組み立てて渡す。
    /// 位置は型の定義から求める（64 ビットでのパディングを手計算した定数で表さない）。
    /// </remarks>
    internal static int WriteRealOption(IntPtr properties, string name, float value)
    {
        var write = (delegate* unmanaged[Stdcall]<IntPtr, uint, void*, void*, int>)(*(IntPtr**)properties.ToPointer())[BagWrite];

        var bag = new PropBag2 { ValueType = VariantTypeR4 };
        var variant = new Variant { Type = VariantTypeR4, Float = value };
        var bagSize = Marshal.SizeOf<PropBag2>();
        var memory = Marshal.AllocHGlobal(bagSize + Marshal.SizeOf<Variant>());
        try
        {
            fixed (char* text = name)
            {
                bag.Name = (IntPtr)text;
                Marshal.StructureToPtr(bag, memory, false);
                Marshal.StructureToPtr(variant, IntPtr.Add(memory, bagSize), false);
                return write(properties, 1, (void*)memory, (void*)IntPtr.Add(memory, bagSize));
            }
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }

    /// <summary><c>VARIANT</c> の型（<c>VT_R4</c> = 単精度の実数）。</summary>
    private const ushort VariantTypeR4 = 4;

    /// <summary><c>PROPBAG2</c>（<c>IPropertyBag2::Write</c> へ渡す 1 件分の指定）。</summary>
    /// <remarks>
    /// 並びは <c>ocidl.h</c> の定義順であり、位置は実行時に決まる（32/64 ビットで異なる）。
    /// <b><c>IPropertyBag::Write</c> の <c>tagPROPBAG2</c> とは別の型である</b>（名前・値の型・形式・
    /// クラス ID を持つ点が異なる）。
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    private struct PropBag2
    {
        /// <summary>種類（0 は既定）。</summary>
        public uint Type;

        /// <summary>値の型（<c>VARTYPE</c>）。</summary>
        public ushort ValueType;

        /// <summary>クリップボード形式（指定しない）。</summary>
        public ushort ClipFormat;

        /// <summary>ヒント（指定しない）。</summary>
        public uint Hint;

        /// <summary>オプションの名前。</summary>
        public IntPtr Name;

        /// <summary>対象のクラス（指定しない）。</summary>
        public Guid Clsid;
    }

    /// <summary><c>VARIANT</c>（<c>PROPBAG2</c> と対になる値）。</summary>
    /// <remarks>型は先頭、値は 8 バイト目に置かれる（32/64 ビットで同じ）。</remarks>
    [StructLayout(LayoutKind.Explicit)]
    private struct Variant
    {
        /// <summary>値の型（<c>VARTYPE</c>）。</summary>
        [FieldOffset(0)]
        public ushort Type;

        /// <summary>単精度の実数の値。</summary>
        [FieldOffset(8)]
        public float Float;
    }
}
