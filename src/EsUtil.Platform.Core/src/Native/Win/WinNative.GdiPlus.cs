using System.Runtime.InteropServices;

namespace EsUtil.Platform.Core.Native.Win;

// 本ファイルは gdiplus.dll の宣言（画像のエンコード・デコード）を集約する。 型の説明は WinNative.cs を参照する。
internal static partial class WinNative
{

    // ============================================================
    // gdiplus.dll（画像のエンコード・デコード）
    // ============================================================
    // GDI+ のメンバーは `Gp` を付けて Win32 のメンバーと区別する（GDI+ 自身の型名（GpRect 等）と同じ慣習）。

    /// <summary>GDI+ の呼び出しが成功したことを表す状態値。</summary>
    internal const int GpOk = 0;

    /// <summary>アルファ乗算済みの 32bpp BGRA 形式（PixelFormat32bppPArgb）。 コアの画像と同じ並び。</summary>
    internal const int GpPixelFormat32bppPArgb = 0x000E200B;

    /// <summary>ピクセルの読み取りを目的としてロックする（ImageLockMode.Read）。</summary>
    internal const uint GpImageLockModeRead = 1;

    /// <summary>PNG エンコーダーの CLSID。</summary>
    internal static readonly Guid GpPngEncoderClsid = new("557CF406-1A04-11D3-9A73-0000F81EF32E");

    /// <summary>JPEG エンコーダーの CLSID。</summary>
    internal static readonly Guid GpJpegEncoderClsid = new("557CF401-1A04-11D3-9A73-0000F81EF32E");

    /// <summary>BMP エンコーダーの CLSID。</summary>
    internal static readonly Guid GpBmpEncoderClsid = new("557CF400-1A04-11D3-9A73-0000F81EF32E");

    /// <summary>GIF エンコーダーの CLSID。</summary>
    internal static readonly Guid GpGifEncoderClsid = new("557CF402-1A04-11D3-9A73-0000F81EF32E");

    /// <summary>TIFF エンコーダーの CLSID。</summary>
    internal static readonly Guid GpTiffEncoderClsid = new("557CF405-1A04-11D3-9A73-0000F81EF32E");

    /// <summary>GDI+ の初期化へ渡す入力（GdiplusStartupInput）。</summary>
    /// <remarks>
    /// デバッグ コールバックと抑制フラグは使わない（既定値のまま渡す）。
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal struct GdiplusStartupInput
    {
        /// <summary>GDI+ のバージョン（1 を指定する）。</summary>
        public uint GdiplusVersion;

        /// <summary>デバッグ イベントのコールバック（使わない場合は 0）。</summary>
        public IntPtr DebugEventCallback;

        /// <summary>バックグラウンド スレッドを抑制するか（0）。</summary>
        public int SuppressBackgroundThread;

        /// <summary>外部コーデックを抑制するか（0）。</summary>
        public int SuppressExternalCodecs;
    }

    /// <summary>GDI+ の矩形（GpRect）。</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct GpRect
    {
        /// <summary>左端。</summary>
        public int X;

        /// <summary>上端。</summary>
        public int Y;

        /// <summary>幅。</summary>
        public int Width;

        /// <summary>高さ。</summary>
        public int Height;
    }

    /// <summary>ロックしたピクセル データ（GDI+ の BitmapData）。</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct GpBitmapData
    {
        /// <summary>幅（ピクセル）。</summary>
        public int Width;

        /// <summary>高さ（ピクセル）。</summary>
        public int Height;

        /// <summary>1 行のバイト数（実装依存。 コアの画像のストライドと一致するとは限らない）。</summary>
        public int Stride;

        /// <summary>ピクセルの形式（P/Invoke では <see cref="int"/> として受け渡す）。</summary>
        public int PixelFormat;

        /// <summary>ピクセル データの先頭アドレス。</summary>
        public IntPtr Scan0;

        /// <summary>予約領域（GDI+ が内部で使う）。</summary>
        public UIntPtr Reserved;
    }

    /// <summary>GDI+ をプロセスで使用可能にする（gdiplus.GdiplusStartup）。</summary>
    /// <param name="token">初期化トークンの出力先。 以降の呼び出しには不要だが、終了時に使う。</param>
    /// <param name="input">初期化の入力。</param>
    /// <param name="output">バックグラウンド スレッドを抑制した場合の出力先（不要なら 0）。</param>
    /// <returns>成功した場合は <see cref="GpOk"/>。</returns>
    /// <remarks>プロセス単位の初期化であるため、呼び出しは <c>WinGdiPlusLifetime</c> が単一所有する。</remarks>
    [LibraryImport("gdiplus.dll")]
    internal static partial int GdiplusStartup(out IntPtr token, ref GdiplusStartupInput input, IntPtr output);

    /// <summary>ピクセル バッファを指す GDI+ のビットマップを作成する（gdiplus.GdipCreateBitmapFromScan0）。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="stride">1 行のバイト数。</param>
    /// <param name="format">ピクセルの形式（<see cref="GpPixelFormat32bppPArgb"/>）。</param>
    /// <param name="scan0">ピクセルの先頭アドレス。</param>
    /// <param name="bitmap">作成されたビットマップの出力先。</param>
    /// <returns>成功した場合は <see cref="GpOk"/>。</returns>
    /// <remarks>
    /// <b>ピクセルはコピーされない</b>（渡したバッファを参照するだけ）。 バッファはビットマップを破棄するまで生存させる。<br/>
    /// ビットマップは <c>GdipDisposeImage</c> で破棄する（バッファの所有権は移動しない）。
    /// </remarks>
    [LibraryImport("gdiplus.dll")]
    internal static partial int GdipCreateBitmapFromScan0(
        int width, int height, int stride, int format, IntPtr scan0, out IntPtr bitmap);

    /// <summary>ファイルから画像を読み込む（gdiplus.GdipCreateBitmapFromFile）。</summary>
    /// <param name="filename">読み込むファイルのパス。</param>
    /// <param name="bitmap">読み込まれたビットマップの出力先。</param>
    /// <returns>成功した場合は <see cref="GpOk"/>。</returns>
    /// <remarks>
    /// 形式は内容から判別される。 拡張子と内容が一致しない場合も内容に従う。<br/>
    /// <b>存在しないファイルも <see cref="GpOk"/> 以外を返す</b>（マネージドの例外へ翻訳するのは呼び出し側）。
    /// </remarks>
    [LibraryImport("gdiplus.dll", EntryPoint = "GdipCreateBitmapFromFile", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int GdipCreateBitmapFromFile(string filename, out IntPtr bitmap);

    /// <summary>画像をファイルへ保存する（gdiplus.GdipSaveImageToFile）。</summary>
    /// <param name="image">保存する画像。</param>
    /// <param name="filename">保存先のファイル パス。</param>
    /// <param name="clsidEncoder">使用するエンコーダーの CLSID。</param>
    /// <param name="encoderParams">エンコーダーのパラメーター（既定値なら 0）。</param>
    /// <returns>成功した場合は <see cref="GpOk"/>。</returns>
    /// <remarks>
    /// <b>形式はエンコーダーの CLSID が決める</b>（拡張子は見ない。 拡張子と CLSID を一致させるのは呼び出し側）。
    /// </remarks>
    [LibraryImport("gdiplus.dll", EntryPoint = "GdipSaveImageToFile", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int GdipSaveImageToFile(IntPtr image, string filename, ref Guid clsidEncoder, IntPtr encoderParams);

    /// <summary>画像の幅を取得する（gdiplus.GdipGetImageWidth）。</summary>
    /// <param name="image">対象の画像。</param>
    /// <param name="width">幅の出力先。</param>
    /// <returns>成功した場合は <see cref="GpOk"/>。</returns>
    [LibraryImport("gdiplus.dll")]
    internal static partial int GdipGetImageWidth(IntPtr image, out uint width);

    /// <summary>画像の高さを取得する（gdiplus.GdipGetImageHeight）。</summary>
    /// <param name="image">対象の画像。</param>
    /// <param name="height">高さの出力先。</param>
    /// <returns>成功した場合は <see cref="GpOk"/>。</returns>
    [LibraryImport("gdiplus.dll")]
    internal static partial int GdipGetImageHeight(IntPtr image, out uint height);

    /// <summary>ピクセルを読み取り用にロックする（gdiplus.GdipBitmapLockBits）。</summary>
    /// <param name="bitmap">対象のビットマップ。</param>
    /// <param name="rect">ロックする矩形。</param>
    /// <param name="flags">ロックの目的（<see cref="GpImageLockModeRead"/>）。</param>
    /// <param name="format">取得するピクセルの形式（<see cref="GpPixelFormat32bppPArgb"/>）。</param>
    /// <param name="lockedBitmapData">ロックしたデータの出力先。</param>
    /// <returns>成功した場合は <see cref="GpOk"/>。</returns>
    /// <remarks>
    /// <b>形式が異なる場合は GDI+ が変換する</b>（読み込んだ画像が BGRA・アルファ乗算済みでなくても、
    /// PArgb を指定すればコアの画像と同じ並びで受け取れる）。<br/>
    /// 使い終わったら <c>GdipBitmapUnlockBits</c> で解放する。
    /// </remarks>
    [LibraryImport("gdiplus.dll")]
    internal static partial int GdipBitmapLockBits(
        IntPtr bitmap, ref GpRect rect, uint flags, int format, ref GpBitmapData lockedBitmapData);

    /// <summary>ピクセルのロックを解放する（gdiplus.GdipBitmapUnlockBits）。</summary>
    /// <param name="bitmap">対象のビットマップ。</param>
    /// <param name="lockedBitmapData">ロックしたデータ。</param>
    /// <remarks>
    /// <b>状態値を宣言しない</b>: GDI+ は解放の成否を返すが、解放は後始末の中で行うため呼び出し側が復帰できない
    /// （読み取り専用のロックでは書き戻す内容も無い）。
    /// </remarks>
    [LibraryImport("gdiplus.dll")]
    internal static partial void GdipBitmapUnlockBits(IntPtr bitmap, ref GpBitmapData lockedBitmapData);

    /// <summary>GDI+ の画像を破棄する（gdiplus.GdipDisposeImage）。</summary>
    /// <param name="image">破棄する画像（ビットマップを含む）。</param>
    /// <remarks>
    /// 作成元のピクセル バッファは解放しない（<c>GdipCreateBitmapFromScan0</c> はバッファを所有しない）。<br/>
    /// <b>状態値を宣言しない</b>: 破棄は後始末の中で行うため呼び出し側が復帰できない（GDI+ は常に成功を返す）。
    /// </remarks>
    [LibraryImport("gdiplus.dll")]
    internal static partial void GdipDisposeImage(IntPtr image);
}
