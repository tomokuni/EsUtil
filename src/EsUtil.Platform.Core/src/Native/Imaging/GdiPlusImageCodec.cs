using System.ComponentModel;
using System.Runtime.Versioning;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Platform.Core.Native.Imaging;

/// <summary>コアの画像のファイル保存・読み込みの実装（GDI+ のフラット API を使う Windows 専用）。</summary>
/// <remarks>
/// <b>唯一の実装</b>: 公開面は画像型のメンバー（<see cref="EsUtil.Platform.Core.Imaging.EsBitmapBase.Save(string)"/> /
/// <see cref="EsUtil.Platform.Core.Imaging.EsBitmap.Load(string)"/> / <see cref="EsUtil.Platform.Core.Imaging.EsWritableBitmap.Load(string)"/>) であり、
/// 本クラスはその実装を 1 箇所へ集約する（本層は internal）。<br/>
/// <b>依存分離</b>: ファイル形式（BMP / PNG / JPEG / GIF / TIFF）のエンコード・デコードは OS の
/// <c>gdiplus.dll</c> が担い、<b>外部パッケージ（<c>System.Drawing.Common</c> 等）を参照しない</b>。<br/>
/// <b>形式は拡張子が決める</b>: <see cref="Save(IEsBitmap, string)"/> は保存先の拡張子から形式を選ぶ。
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
internal static class GdiPlusImageCodec
{
    /// <summary>保存に使える拡張子（小文字・先頭のドットを含む）。</summary>
    internal static readonly IReadOnlyList<string> SupportedExtensions = [".bmp", ".png", ".jpg", ".jpeg", ".gif", ".tif", ".tiff"];

    /// <summary>画像をファイルへ保存する（形式は拡張子が決める）。</summary>
    /// <param name="bitmap">保存する画像。 借用であり破棄しない。</param>
    /// <param name="filePath">保存先のファイルパス（拡張子が形式を決める）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="bitmap"/> または <paramref name="filePath"/> が null の場合。</exception>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合。</exception>
    /// <exception cref="NotSupportedException">拡張子が対応形式でない場合（<see cref="SupportedExtensions"/>）。</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="bitmap"/> が破棄済みの場合。</exception>
    /// <exception cref="System.Runtime.InteropServices.ExternalException">エンコード（GDI+）が失敗した場合（<see cref="Win32Exception"/> で状態値を持つ）。</exception>
    /// <remarks>
    /// 保存形式は拡張子から決める（<c>.bmp</c> = Windows Bitmap / <c>.png</c> / <c>.jpg</c> / <c>.jpeg</c> /
    /// <c>.gif</c> / <c>.tif</c> / <c>.tiff</c>）。<br/>
    /// <b>ピクセルは複製しない</b>: コアの画像のバッファを指すビットマップを作り、そのままエンコードする。<br/>
    /// <b>JPEG / BMP はアルファを保存できない</b>（透過を保つ場合は PNG を選ぶ）。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    internal static void Save(IEsBitmap bitmap, string filePath)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var encoderClsid = ResolveEncoderClsid(filePath);
        GdiPlusLifetime.EnsureInitialized();

        // 破棄済みの場合は Pixels が ObjectDisposedException を投げる（公開面の例外は IEsBitmap が定める）
        var pixels = bitmap.Pixels;
        unsafe
        {
            fixed (byte* p = pixels)
            {
                var status = GdiPlusNative.GdipCreateBitmapFromScan0(
                    bitmap.Width, bitmap.Height, bitmap.Stride, GdiPlusNative.PixelFormat32bppPArgb, (nint)p, out var image);
                if (status != GdiPlusNative.Ok)
                {
                    throw new Win32Exception(status, $"保存用のビットマップを作成できませんでした（GDI+ の状態: {status}）。");
                }

                try
                {
                    var clsid = encoderClsid;
                    status = GdiPlusNative.GdipSaveImageToFile(image, filePath, ref clsid, IntPtr.Zero);
                    if (status != GdiPlusNative.Ok)
                    {
                        throw new Win32Exception(status, $"'{filePath}' へ保存できませんでした（GDI+ の状態: {status}）。");
                    }
                }
                finally
                {
                    GdiPlusNative.GdipDisposeImage(image);
                }
            }
        }
    }

    /// <summary>ファイルから読み取り専用の画像を読み込む。</summary>
    /// <param name="filePath">読み込むファイルのパス（形式は内容から判別される）。</param>
    /// <returns>読み込んだ画像（読み取り専用）。 所有権は呼び出し側へ移る。</returns>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合、ファイルが存在しない場合、
    /// または画像として解釈できない場合（GDI+ はどちらも同じ状態値を返すため区別しない）。</exception>
    /// <remarks>
    /// <b>縮小読み込みはしない</b>（元の寸法でデコードする。 大きな画像を小さく使う場合は
    /// <c>Resize</c> で縮小すること。 サムネイルのようにデコード自体を小さくしたい場合は、各 UI の
    /// コーデック（縮小デコード対応）を使う方が速い）。<br/>
    /// <b>形式は内容から判別する</b>（拡張子と内容が一致しない場合は内容に従う）。<br/>
    /// <b>ピクセルはアルファ乗算済みの BGRA8888 へ変換して取り込む</b>（元の形式が異なっても GDI+ が変換する）。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    internal static EsBitmap LoadReadOnly(string filePath)
        => Decode(filePath, static (width, height, fill) => EsBitmap.CreateFilled(width, height, fill));

    /// <summary>ファイルから書き換え可能な画像を読み込む。</summary>
    /// <param name="filePath">読み込むファイルのパス（形式は内容から判別される）。</param>
    /// <returns>読み込んだ画像（書き換え可能）。 所有権は呼び出し側へ移る。</returns>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合、ファイルが存在しない場合、
    /// または画像として解釈できない場合（GDI+ はどちらも同じ状態値を返すため区別しない）。</exception>
    /// <remarks>
    /// <b>書き換え可能な型で返す理由</b>: デコードは新しいバッファへ 1 回だけ書くため、読み取り専用で返しても
    /// 守るものが無く、書き換えたい利用者へ 2 回目のコピー（実測: 4K で 5.7 ms / 31 MiB）を強いるだけになる
    /// （書き換えが要らない場合は <see cref="LoadReadOnly"/> を使う）。<br/>
    /// 読み込みの他の規則（縮小しない・内容から形式を判別する・アルファ乗算済み BGRA8888 へ変換する）は
    /// <see cref="LoadReadOnly"/> と同じ。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    internal static EsWritableBitmap LoadWritable(string filePath)
        => Decode(filePath, static (width, height, fill) => EsWritableBitmap.CreateFilled(width, height, fill));

    /// <summary>ファイルを読み込んで、指定された型の画像としてデコードする（読み込みの手順を 1 箇所へ集約する）。</summary>
    /// <typeparam name="T">生成する画像の型（<see cref="EsBitmap"/> または <see cref="EsWritableBitmap"/>）。</typeparam>
    /// <param name="filePath">読み込むファイルのパス。</param>
    /// <param name="create">寸法と充填処理を受け取って画像を生成する処理（デコード結果を直接書くため、コピーは 1 回で済む）。</param>
    /// <returns>読み込んだ画像。 所有権は呼び出し側へ移る。</returns>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合、ファイルが存在しない場合、
    /// または画像として解釈できない場合（GDI+ はどちらも同じ状態値を返すため区別しない）。</exception>
    [SupportedOSPlatform("windows")]
    private static T Decode<T>(string filePath, Func<int, int, Action<Span<byte>>, T> create)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        GdiPlusLifetime.EnsureInitialized();

        var status = GdiPlusNative.GdipCreateBitmapFromFile(filePath, out var image);
        if (status != GdiPlusNative.Ok)
        {
            throw new ArgumentException($"'{filePath}' を画像として読み込めませんでした。", nameof(filePath));
        }

        try
        {
            var (width, height) = ReadSize(image);
            return create(width, height, dst => CopyPixelsFrom(image, width, height, dst));
        }
        finally
        {
            GdiPlusNative.GdipDisposeImage(image);
        }
    }

    /// <summary>GDI+ の画像の寸法を読み取る。</summary>
    /// <param name="image">対象の GDI+ の画像。</param>
    /// <returns>幅と高さ（ピクセル）。</returns>
    /// <exception cref="Win32Exception">寸法を取得できなかった場合。</exception>
    [SupportedOSPlatform("windows")]
    private static (int Width, int Height) ReadSize(IntPtr image)
    {
        var width = GetDimension(GdiPlusNative.GdipGetImageWidth(image, out var w), w, "幅");
        var height = GetDimension(GdiPlusNative.GdipGetImageHeight(image, out var h), h, "高さ");
        return (width, height);
    }

    /// <summary>保存先の拡張子から使用するエンコーダーの CLSID を決める。</summary>
    /// <param name="filePath">保存先のファイルパス。</param>
    /// <returns>エンコーダーの CLSID。</returns>
    /// <exception cref="NotSupportedException">拡張子が対応形式でない場合。</exception>
    [SupportedOSPlatform("windows")]
    private static Guid ResolveEncoderClsid(string filePath)
        => Path.GetExtension(filePath).ToUpperInvariant() switch
        {
            ".BMP" => GdiPlusNative.BmpEncoderClsid,
            ".PNG" => GdiPlusNative.PngEncoderClsid,
            ".JPG" or ".JPEG" => GdiPlusNative.JpegEncoderClsid,
            ".GIF" => GdiPlusNative.GifEncoderClsid,
            ".TIF" or ".TIFF" => GdiPlusNative.TiffEncoderClsid,
            var other => throw new NotSupportedException(
                $"拡張子 '{other}' へは保存できません。 対応形式: {string.Join(" / ", SupportedExtensions)}"),
        };

    /// <summary>GDI+ が返した寸法を検証して取り出す。</summary>
    /// <param name="status">GDI+ の状態値。</param>
    /// <param name="value">GDI+ が返した寸法。</param>
    /// <param name="name">寸法の名前（例外メッセージ用）。</param>
    /// <returns>検証済みの寸法。</returns>
    /// <exception cref="Win32Exception">寸法を取得できなかった場合。</exception>
    [SupportedOSPlatform("windows")]
    private static int GetDimension(int status, uint value, string name)
        => status == GdiPlusNative.Ok && value <= int.MaxValue
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
        var rect = new GdiPlusNative.GpRect { X = 0, Y = 0, Width = width, Height = height };
        var data = default(GdiPlusNative.GpBitmapData);

        var status = GdiPlusNative.GdipBitmapLockBits(
            image, ref rect, GdiPlusNative.ImageLockModeRead, GdiPlusNative.PixelFormat32bppPArgb, ref data);
        if (status != GdiPlusNative.Ok)
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
            GdiPlusNative.GdipBitmapUnlockBits(image, ref data);
        }
    }
}
