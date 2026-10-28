using System.Runtime.Versioning;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Native.Win.Wic;

namespace EsUtil.Platform.Core;

/// <summary>画像の符号化・復号と形式の窓口（Windows の WIC を汎用に扱う）。</summary>
/// <remarks>
/// <b>画像型に依存しません</b>: 画素は <see cref="ReadOnlySpan{T}"/>（<b>BGRA8888・アルファ乗算済み</b>）+ 寸法 +
/// 行幅で受け渡すため、<see cref="EsBitmap"/> を作らずに他の UI のビットマップ（<c>Avalonia.Media.Imaging.WriteableBitmap</c>
/// など）の画素をそのまま渡せます。 画像型（<see cref="EsBitmap"/> / <see cref="EsReadOnlyBitmap"/> / <see cref="EsPixmap"/>）も
/// <b>本クラスへ委譲</b>します（実装を 2 つ持たない）。<br/>
/// <b>画素の形式は 1 つに固定します</b>: BGRA8888・アルファ乗算済み、行の並びは上が先頭、行幅は <c>幅 × 4</c> 以上です。
/// 形式を引数にすると、呼び出し側が変換の有無を毎回判断することになり、取り違えが起きるためです。<br/>
/// <b>画素を複製しません</b>: 復号は呼び出し側が確保した領域へ直接書きます（寸法は <see cref="ReadSize(ReadOnlySpan{byte}, int)"/> で
/// 先に求めます）。 確保を含む版（<see cref="Decode(ReadOnlySpan{byte}, int)"/>）は 1 回だけ確保します。
/// <b>パスから読む場合は <see cref="Imaging.EsImageFile"/> を使います</b>（寸法と画素を 1 回の読み込みから求めます）。<br/>
/// <b>Windows 専用のメンバーがあります</b>: WIC を呼ぶもの（<see cref="Decode(ReadOnlySpan{byte}, int)"/> / <see cref="Save(ReadOnlySpan{byte}, EsSizeInt, string, EsImageSaveOptions)"/> /
/// <see cref="ReduceColors(ReadOnlySpan{byte}, EsSizeInt, int, int, EsDitherMode)"/> / <see cref="SaveExtensions"/>）は
/// <see cref="SupportedOSPlatformAttribute"/> で限定します。 <b>縮小（<see cref="Resize(ReadOnlySpan{byte}, EsSizeInt, int, int, EsImageQuality)"/>）と
/// 形式の対応（<see cref="GetExtension(EsImageFileFormat)"/> など）は可搬</b>です（縮小は本ライブラリの計算です）。<br/>
/// <b>静的クラスである理由</b>: 保持する状態が無く（WIC のファクトリは呼び出しごとに生成して解放する）、
/// 4 つの UI 版へ DI を持ち込まないためです。
/// </remarks>
public static class EsImageCodec
{
    /// <summary>保存に使える拡張子（小文字・先頭のドットを含む）。</summary>
    /// <value>実行環境で書き出せる形式の拡張子。 WIC を使えない環境では空。</value>
    /// <remarks>
    /// <b>実行環境で書き出せる形式だけを含みます</b>（コーデック拡張が入っていない形式は含まない）。
    /// 一覧をそのまま保存先の選択肢にできます（一覧の拡張子で保存すれば成功します）。
    /// </remarks>
    [SupportedOSPlatform("windows10.0.17763")]
    public static IReadOnlyList<string> SaveExtensions => WinWicImageCodec.SupportedExtensions;

    /// <summary>一覧に並べる画像ファイルの拡張子（先頭のドットを含む）。</summary>
    /// <value>一覧の対象になる拡張子（保存できない形式も含む。 例: <c>.webp</c>）。</value>
    /// <remarks>
    /// <b>保存できる形式の一覧（<see cref="SaveExtensions"/>）より広い</b>です（一覧に並べる判断と、読み書きできるかの
    /// 判断は別であるため）。 判定の規則は <see cref="EsImageFileExtensions"/> が単一所有します。
    /// </remarks>
    public static IReadOnlyCollection<string> ListableExtensions => EsImageFileExtensions.All;

    /// <summary>指定されたパスが画像ファイルかどうかを判定する。</summary>
    /// <param name="path">判定するパス（拡張子だけを使う）。</param>
    /// <returns>一覧に含める画像の場合は true。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> が null の場合。</exception>
    public static bool IsImageFile(string path) => EsImageFileExtensions.IsImageFile(path);

    /// <summary>文字列から保存する形式を解釈する。</summary>
    /// <param name="text">拡張子（<c>.png</c> / <c>png</c>）または形式の名前（<c>Png</c>）。 大文字・小文字は区別しない。</param>
    /// <param name="format">解釈した形式。 解釈できなかった場合は <see cref="EsImageFileFormat.Png"/>。</param>
    /// <returns>解釈できた場合は true。</returns>
    public static bool TryParseFormat(string? text, out EsImageFileFormat format)
        => EsImageFileFormatExtensions.TryParse(text, out format);

    /// <summary>形式の代表の拡張子を求める（拡張子を補完するときに使う名前）。</summary>
    /// <param name="format">形式。</param>
    /// <returns>代表の拡張子（小文字・先頭のドットを含む）。</returns>
    /// <exception cref="ArgumentOutOfRangeException">定義されていない値の場合。</exception>
    /// <remarks>
    /// <b>同じ形式の別名は返しません</b>（<see cref="EsImageFileFormat.Jpeg"/> は <c>.jpg</c> であり <c>.jpeg</c> ではない）。
    /// 補完の結果が環境で変わらないよう、形式ごとに 1 つへ固定しています。
    /// </remarks>
    public static string GetExtension(EsImageFileFormat format) => format.ToExtension();

    /// <summary>形式の表示名を求める（画面に出す名前）。</summary>
    /// <param name="format">形式。</param>
    /// <returns>表示名（例: <c>PNG</c> / <c>JPEG XR</c>）。</returns>
    /// <exception cref="ArgumentOutOfRangeException">定義されていない値の場合。</exception>
    /// <remarks><b>OS に依りません</b>（WIC のエンコーダーが返す英語名は使いません）。</remarks>
    public static string GetDisplayName(EsImageFileFormat format) => format.ToDisplayName();

    /// <summary>縮小後の寸法を求める（縦横比を保ち、長辺を上限へ揃える）。</summary>
    /// <param name="size">元の寸法。</param>
    /// <param name="maxSize">長辺の上限（ピクセル）。 1 以上。</param>
    /// <returns>縮小後の寸法（各辺は 1 以上）。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> の辺が 0 以下、または <paramref name="maxSize"/> が 0 以下の場合。</exception>
    /// <exception cref="NotSupportedException"><paramref name="maxSize"/> が長辺より大きい（拡大が必要な）場合。</exception>
    /// <remarks>
    /// <b>拡大は行いません</b>（黙って拡大すると「拡大された」と誤解されるため例外にします）。<br/>
    /// <b>丸めは切り捨て</b>です（<b>1 px 未満の誤差が出ます</b>。 座標の対応付けが必要な場面では使わないこと）。
    /// </remarks>
    public static EsSizeInt GetResizedSize(EsSizeInt size, int maxSize)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(size), size, "元の寸法は 1 以上である必要があります。");
        }

        if (maxSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSize), maxSize, "長辺の上限は正の値である必要があります。");
        }

        var longSide = Math.Max(size.Width, size.Height);
        if (maxSize > longSide)
        {
            throw new NotSupportedException(
                $"拡大（{size.Width}x{size.Height} → 長辺 {maxSize}）は未サポートです。 長辺以下の上限を渡してください。");
        }

        if (maxSize == longSide)
        {
            return size;
        }

        var ratio = (double)maxSize / longSide;
        return new EsSizeInt(Math.Max(1, (int)(size.Width * ratio)), Math.Max(1, (int)(size.Height * ratio)));
    }

    /// <summary>ファイルの内容を復号し、密な画素（BGRA8888・アルファ乗算済み）を返す。</summary>
    /// <param name="fileBytes">ファイルの内容（パスから読む場合は <see cref="Imaging.EsImageFile"/> を使う）。</param>
    /// <param name="maxSize">長辺の上限（ピクセル）。 0 以下の場合は元の寸法（拡大はしない）。</param>
    /// <returns>復号した画素とその寸法。</returns>
    /// <exception cref="ArgumentException">画像として解釈できない場合。</exception>
    /// <remarks>
    /// <b>EXIF の向きを適用してから縮小します</b>（回転した JPEG も正しい向きで読めます）。<br/>
    /// <b>パスから読み込む場合は <see cref="Imaging.EsImageFile"/> を使います</b>（寸法と画素を同じ内容から求めるため、
    /// ファイルを 2 回読みません）。
    /// </remarks>
    [SupportedOSPlatform("windows10.0.17763")]
    public static (byte[] Pixels, EsSizeInt Size) Decode(ReadOnlySpan<byte> fileBytes, int maxSize = 0)
        => WinWicImageCodec.DecodeFromFile(
            fileBytes,
            maxSize,
            (width, height, fill) =>
            {
                var pixels = new byte[(long)width * EsReadOnlyBitmap.BytesPerPixel * height];
                fill(pixels);
                return (pixels, new EsSizeInt(width, height));
            });

    /// <summary>ファイルの内容を復号し、呼び出し側の領域へ書く（画素を複製しない）。</summary>

    /// <summary>ファイルの内容を復号し、呼び出し側の領域へ書く（画素を複製しない）。</summary>
    /// <param name="fileBytes">ファイルの内容。</param>
    /// <param name="maxSize">長辺の上限（ピクセル）。 0 以下の場合は元の寸法（拡大はしない）。</param>
    /// <param name="destination">書き込み先（<c>寸法の幅 × 4 × 高さ</c> バイト以上。 行は密に書く）。</param>
    /// <exception cref="ArgumentException"><paramref name="destination"/> が足りない場合、または画像として解釈できない場合。</exception>
    [SupportedOSPlatform("windows10.0.17763")]
    public static unsafe void Decode(ReadOnlySpan<byte> fileBytes, int maxSize, Span<byte> destination)
    {
        var capacity = destination.Length;
        fixed (byte* pinned = destination)
        {
            // 固定したローカルはラムダが取り込めないため、通常のポインター変数へ写す
            var sink = pinned;
            WinWicImageCodec.DecodeFromFile(
                fileBytes,
                maxSize,
                (width, height, fill) =>
                {
                    EnsureCapacity(capacity, width, height, nameof(destination));
                    fill(new Span<byte>(sink, capacity));
                    return true;
                });
        }
    }

    /// <summary>ファイルの内容を復号し、その寸法だけを返す（画素を複製しない）。</summary>
    /// <param name="fileBytes">ファイルの内容（パスから読む場合は <see cref="Imaging.EsImageFile"/> を使う）。</param>
    /// <param name="maxSize">長辺の上限（ピクセル）。 0 以下の場合は元の寸法（拡大はしない）。</param>
    /// <returns>復号したときの寸法（EXIF の向きと縮小を反映する）。</returns>
    /// <exception cref="ArgumentException">画像として解釈できない場合。</exception>
    /// <remarks>
    /// <b>画素を複製しません</b>（デコーダーから寸法だけを読みます）。 確保の前に寸法を知りたい場合は
    /// <see cref="Imaging.EsImageFile.GetSize"/> を使うと、続く復号で同じ内容を読み直さずに済みます。
    /// </remarks>
    [SupportedOSPlatform("windows10.0.17763")]
    public static EsSizeInt ReadSize(ReadOnlySpan<byte> fileBytes, int maxSize = 0)
        => WinWicImageCodec.DecodeFromFile(
            fileBytes, maxSize, (width, height, _) => new EsSizeInt(width, height));

    /// <summary>画素をファイルへ保存する（行幅は <c>幅 × 4</c>）。</summary>
    /// <param name="pixels">保存する画素（BGRA8888・アルファ乗算済み。 行は密）。</param>
    /// <param name="size">画素の寸法。</param>
    /// <param name="filePath">保存先のファイルパス（拡張子が形式を決める）。</param>
    /// <param name="options">保存のオプション（既定の場合は省略できる）。</param>
    /// <exception cref="ArgumentException"><paramref name="pixels"/> の長さが寸法と一致しない場合、または拡張子と形式が食い違う場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="options"/> の値が許容範囲外の場合。</exception>
    /// <exception cref="NotSupportedException">拡張子が対応形式でない場合（<see cref="SaveExtensions"/>）、または設定されたオプションがその形式で使えない場合。</exception>
    [SupportedOSPlatform("windows10.0.17763")]
    public static void Save(
        ReadOnlySpan<byte> pixels, EsSizeInt size, string filePath, EsImageSaveOptions options = default)
        => Save(pixels, size, size.Width * EsReadOnlyBitmap.BytesPerPixel, filePath, options);

    /// <summary>画素をファイルへ保存する（行幅を指定できる）。</summary>
    /// <param name="pixels">保存する画素（BGRA8888・アルファ乗算済み）。</param>
    /// <param name="size">画素の寸法。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数（<c>幅 × 4</c> 以上）。</param>
    /// <param name="filePath">保存先のファイルパス（拡張子が形式を決める）。</param>
    /// <param name="options">保存のオプション（既定の場合は省略できる）。</param>
    /// <exception cref="ArgumentException"><paramref name="pixels"/> の長さが寸法と一致しない場合、または拡張子と形式が食い違う場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="options"/> の値が許容範囲外の場合。</exception>
    /// <exception cref="NotSupportedException">拡張子が対応形式でない場合（<see cref="SaveExtensions"/>）、または設定されたオプションがその形式で使えない場合。</exception>
    /// <remarks><b>行幅が密でない場合</b>は 1 回だけ複製してから書きます（WIC のエンコーダーへは密な並びで渡します）。</remarks>
    [SupportedOSPlatform("windows10.0.17763")]
    public static void Save(
        ReadOnlySpan<byte> pixels, EsSizeInt size, int stride, string filePath, EsImageSaveOptions options = default)
    {
        EnsurePixels(pixels, size, stride, nameof(pixels));
        WinWicImageCodec.EncodeToFile(pixels, size.Width, size.Height, stride, filePath, options);
    }

    /// <summary>画素を縮小し、密な画素を返す（拡大は未サポート）。</summary>
    /// <param name="pixels">元の画素（BGRA8888・アルファ乗算済み。 行の余白を含んでよい）。</param>
    /// <param name="size">元の寸法。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数（<c>幅 × 4</c> 以上）。</param>
    /// <param name="maxSize">長辺の上限（ピクセル）。 元の長辺以下。</param>
    /// <param name="quality">縮小の品質（速度と画質の釣り合い）。 既定は <see cref="EsImageQuality.Mid"/>。</param>
    /// <returns>縮小した画素とその寸法。</returns>
    /// <exception cref="ArgumentException"><paramref name="pixels"/> の長さが寸法と一致しない場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxSize"/> または <paramref name="quality"/> が許容範囲外の場合。</exception>
    /// <exception cref="NotSupportedException"><paramref name="maxSize"/> が長辺より大きい（拡大が必要な）場合。</exception>
    public static (byte[] Pixels, EsSizeInt Size) Resize(
        ReadOnlySpan<byte> pixels, EsSizeInt size, int stride, int maxSize,
        EsImageQuality quality = EsImageQuality.Mid)
    {
        var target = GetResizedSize(size, maxSize);
        EnsurePixels(pixels, size, stride, nameof(pixels));
        EnsureQuality(quality);

        var destination = new byte[(long)target.Width * EsReadOnlyBitmap.BytesPerPixel * target.Height];
        ScaleInto(pixels, size, stride, target, quality, destination);
        return (destination, target);
    }

    /// <summary>画素を縮小し、呼び出し側の領域へ書く（画素を複製しない）。</summary>
    /// <param name="pixels">元の画素（BGRA8888・アルファ乗算済み。 行の余白を含んでよい）。</param>
    /// <param name="size">元の寸法。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数（<c>幅 × 4</c> 以上）。</param>
    /// <param name="target">縮小後の寸法（<see cref="GetResizedSize(EsSizeInt, int)"/> が返す値）。</param>
    /// <param name="quality">縮小の品質。</param>
    /// <param name="destination">書き込み先（<c>target の幅 × 4 × 高さ</c> バイト以上。 行は密に書く）。</param>
    /// <exception cref="ArgumentException"><paramref name="pixels"/> の長さが寸法と一致しない場合、または <paramref name="destination"/> が足りない場合。</exception>
    /// <remarks><b>縮小が不要な場合</b>（<paramref name="target"/> が <paramref name="size"/> と同じ）は行を写すだけです。</remarks>
    internal static void ScaleInto(
        ReadOnlySpan<byte> pixels, EsSizeInt size, int stride, EsSizeInt target, EsImageQuality quality,
        Span<byte> destination)
    {
        EnsurePixels(pixels, size, stride, nameof(pixels));
        EnsureQuality(quality);
        EnsureCapacity(destination, target.Width, target.Height, nameof(destination));

        if (target == size)
        {
            var rowBytes = size.Width * EsReadOnlyBitmap.BytesPerPixel;
            for (var y = 0; y < size.Height; y++)
            {
                pixels.Slice(y * stride, rowBytes).CopyTo(destination[(y * rowBytes)..]);
            }

            return;
        }

        EsBitmapOperations.ScaleInto(pixels, size.Width, size.Height, stride, target, quality, destination);
    }

    /// <summary>画素を指定の色数へ減色し、密な画素を返す。</summary>
    /// <param name="pixels">元の画素（BGRA8888・アルファ乗算済み。 行の余白を含んでよい）。</param>
    /// <param name="size">元の寸法。</param>
    /// <param name="stride"><paramref name="pixels"/> の 1 ラインあたりのバイト数（<c>幅 × 4</c> 以上）。</param>
    /// <param name="colorCount">残す色数（2〜256）。</param>
    /// <param name="dither">ディザの指定。 既定は <see cref="EsDitherMode.None"/>。</param>
    /// <returns>減色した画素（寸法は変わらない）。</returns>
    /// <exception cref="ArgumentException"><paramref name="pixels"/> の長さが寸法と一致しない場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="colorCount"/> または <paramref name="dither"/> が許容範囲外の場合。</exception>
    /// <remarks>
    /// <b>減色は WIC が行います</b>（パレットは中央値分割、割り当ては最近傍）。 保存の
    /// <see cref="EsImageSaveOptions.ColorCount"/> と同じパレットを使うため、<b>保存したファイルの画素と一致します</b>。
    /// </remarks>
    [SupportedOSPlatform("windows10.0.17763")]
    public static byte[] ReduceColors(
        ReadOnlySpan<byte> pixels, EsSizeInt size, int stride, int colorCount,
        EsDitherMode dither = EsDitherMode.None)
    {
        EnsurePixels(pixels, size, stride, nameof(pixels));
        if (colorCount < EsBitmapOperations.MinimumColorCount || colorCount > EsBitmapOperations.MaximumColorCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(colorCount),
                colorCount,
                $"減色して残す色数は {EsBitmapOperations.MinimumColorCount}〜{EsBitmapOperations.MaximumColorCount} の範囲で指定します。");
        }

        if (dither is not (EsDitherMode.None or EsDitherMode.ErrorDiffusion))
        {
            throw new ArgumentOutOfRangeException(nameof(dither), dither, "定義されていないディザの指定です。");
        }

        return WinWicImageCodec.Reduce(pixels, size.Width, size.Height, stride, colorCount, dither);
    }

    /// <summary>画素を復号し、生成処理へ渡す（画像型が中間バッファを作らずに済ませるための内部経路）。</summary>
    /// <typeparam name="T">生成する値の型。</typeparam>
    /// <param name="filePath">読み込むファイルのパス。</param>
    /// <param name="maxSize">長辺の上限（ピクセル）。 0 以下の場合は元の寸法。</param>
    /// <param name="create">寸法と充填処理を受け取り、値を生成する処理。</param>
    /// <returns>生成した値。</returns>
    [SupportedOSPlatform("windows10.0.17763")]
    internal static T Decode<T>(string filePath, int maxSize, Func<int, int, Action<Span<byte>>, T> create)
        => WinWicImageCodec.DecodeFromFile(filePath, maxSize, create);

    /// <summary>画素の長さと行幅が寸法と一致することを確かめる。</summary>
    /// <param name="pixels">画素。</param>
    /// <param name="size">寸法。</param>
    /// <param name="stride">行幅（バイト）。</param>
    /// <param name="parameterName">例外へ載せる引数名。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> の辺が 0 以下、または <paramref name="stride"/> が
    /// <c>幅 × 4</c> 未満の場合。</exception>
    /// <exception cref="ArgumentException"><paramref name="pixels"/> の長さが <c>行幅 × 高さ</c> に満たない場合。</exception>
    private static void EnsurePixels(ReadOnlySpan<byte> pixels, EsSizeInt size, int stride, string parameterName)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(size), size, "寸法は 1 以上である必要があります。");
        }

        var minimumStride = size.Width * EsReadOnlyBitmap.BytesPerPixel;
        if (stride < minimumStride)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stride), stride, $"行幅は {minimumStride} バイト（幅 × 4）以上である必要があります。");
        }

        var expected = (long)stride * size.Height;
        if (pixels.Length < expected)
        {
            throw new ArgumentException(
                $"ピクセル データの長さが寸法と一致しません（{size.Width}x{size.Height}・行幅 {stride} は {expected} バイト。 実際は {pixels.Length} バイト）。",
                parameterName);
        }
    }

    /// <summary>書き込み先が寸法に足りることを確かめる。</summary>
    /// <param name="destination">書き込み先。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="parameterName">例外へ載せる引数名。</param>
    /// <exception cref="ArgumentException"><paramref name="destination"/> が足りない場合。</exception>
    private static void EnsureCapacity(Span<byte> destination, int width, int height, string parameterName)
        => EnsureCapacity(destination.Length, width, height, parameterName);

    /// <summary>書き込み先が寸法に足りることを長さで確かめる（<c>Span</c> を保持できない場所から呼ぶ）。</summary>
    /// <param name="capacity">書き込み先の長さ（バイト）。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="parameterName">例外へ載せる引数名。</param>
    /// <exception cref="ArgumentException"><paramref name="capacity"/> が足りない場合。</exception>
    private static void EnsureCapacity(int capacity, int width, int height, string parameterName)
    {
        var expected = (long)width * EsReadOnlyBitmap.BytesPerPixel * height;
        if (capacity < expected)
        {
            throw new ArgumentException(
                $"書き込み先が足りません（{width}x{height} は {expected} バイト。 実際は {capacity} バイト）。",
                parameterName);
        }
    }

    /// <summary>縮小の品質が定義されている値か確かめる。</summary>
    /// <param name="quality">品質。</param>
    /// <exception cref="ArgumentOutOfRangeException">定義されていない値の場合。</exception>
    private static void EnsureQuality(EsImageQuality quality)
    {
        if (quality is not (EsImageQuality.Low or EsImageQuality.Mid or EsImageQuality.High))
        {
            throw new ArgumentOutOfRangeException(nameof(quality), quality, "定義されていない縮小の品質です。");
        }
    }
}
