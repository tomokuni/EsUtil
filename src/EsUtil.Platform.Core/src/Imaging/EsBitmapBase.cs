using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native.Win;
using Color = System.Drawing.Color;

namespace EsUtil.Platform.Core.Imaging;

/// <summary>画像の実体（ピクセル バッファ）を保有する抽象基底クラス（読み取り専用の面を実装する）。</summary>
/// <remarks>
/// <b>目的</b>: 「読み取り専用の画像」と「書き換え可能な画像」で<b>バッファの確保・公開・解放を 1 回だけ書く</b>ための中間基底。
/// 読み取りの面（<see cref="IEsBitmap"/>）を実装し、書き込み可能なビューは派生（<see cref="EsWritableBitmap"/>）だけが公開する
/// （基底に書き込み可能なメンバーを置くと、読み取り専用の派生が書き込みの入口を公開してしまう）。<br/>
/// <br/>
/// <b>解放</b>: バッファの所有者は <see cref="SafeAlignedBuffer"/>（<see cref="System.Runtime.InteropServices.SafeHandle"/>）であり、
/// 解放の実装はその <c>ReleaseHandle</c> が単一所有する。 本型は自前のファイナライザを持たない
/// （手書きのファイナライザを避け、解放を <see cref="SafeAlignedBuffer"/> へ委ねる。 <c>SafeHandle</c> 自身が
/// ファイナライザを持つため、解放の経路は <c>ReleaseHandle</c> の 1 か所になる）。 したがって
/// <b><c>Dispose</c> を忘れても参照を切れば GC が解放する</b>（大きな画像は早期解放のために <c>Dispose</c> すること）。<br/>
/// <br/>
/// <b>派生の責務</b>: <c>Dispose</c>（<see cref="IDisposable"/> の実装）と <see cref="Clone"/>（共変戻り値）を宣言する。
/// 基底に <c>Dispose</c> を置かないのは、**破棄の権利をどこに与えるかを派生ごとに決める**ためである
/// （読み取り専用の実体でも早期解放はできるが、破棄の入口を読み取りの面には出さない）。<br/>
/// <b>派生できる範囲</b>: コンストラクターが <c>private protected</c> のため、派生は本アセンブリの
/// <see cref="EsBitmap"/> と <see cref="EsWritableBitmap"/> だけである（本型は「2 つの権利の実装を 1 回だけ書く」ための型である）。
/// 外部が独自の画像を作る場合は本型ではなく <see cref="IEsBitmap"/> / <see cref="IEsWritableBitmap"/> を実装する。<br/>
/// <b>バッファの仕様</b>: BGRA8888（アルファ乗算済み）、ストライドは <c>幅 × <see cref="IEsBitmap.BytesPerPixel"/></c>
/// （行パディングなし）、64 バイト整列で確保する。
/// </remarks>
public abstract class EsBitmapBase : IEsBitmap
{
    /// <summary>1 ピクセルあたりのバイト数（BGRA8888）。</summary>
    /// <value>4。 ストライドは常に <c>幅 × 本値</c> である。</value>
    /// <remarks>定義は <see cref="IEsBitmap.BytesPerPixel"/> が単一所有する（本定数はその転送）。
    /// 相互変換（System.Drawing / Avalonia / Skia）がストライドを計算するために使う。</remarks>
    public const int BytesPerPixel = IEsBitmap.BytesPerPixel;

    /// <summary>確保するバッファの整列バイト数（SIMD・GPU 転送の要件を満たす）。</summary>
    /// <remarks>派生だけに公開する（公開すると 64 バイト整列を前提にした外部コードが生まれ、実装を変えられなくなる）。</remarks>
    protected const int AlignmentBytes = 64;

    /// <summary>ピクセル バッファの所有者（<see cref="SafeAlignedBuffer"/>。 解放の担い手はこのハンドル）。</summary>
    private readonly SafeAlignedBuffer _buffer;

    private bool _disposed;

    /// <summary>指定サイズのピクセル バッファを確保する。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <param name="clear">true の場合は全領域を 0（透明）で初期化する。 直後に全領域を書く場合は false にして 0 埋めを省く。</param>
    /// <remarks>
    /// <b><paramref name="clear"/> = false は全領域を直後に書く場合だけ使う</b>（書かない領域は不定値のまま残る）。<br/>
    /// <b><c>private protected</c> にする理由</b>: 派生を本アセンブリの 2 型（<see cref="EsBitmap"/> / <see cref="EsWritableBitmap"/>）へ限り、
    /// 「読取のみ」と「書き換え可」の 2 つの権利だけが存在する状態を型で保つ（外部が派生できると、
    /// どちらの権利でもない第 3 の画像が作れてしまう）。
    /// </remarks>
    private protected unsafe EsBitmapBase(int width, int height, bool clear)
    {
        Width = width;
        Height = height;
        Size = new EsSizeInt(width, height);
        Stride = width * BytesPerPixel;

        var byteCount = (long)Stride * height;
        _buffer = SafeAlignedBuffer.Allocate(byteCount, AlignmentBytes);
        if (clear && byteCount > 0)
        {
            // 透明（全成分 0）で初期化する（未描画領域を「描かれていない」として扱うため）
            NativeMemory.Clear((void*)AddressCore, (nuint)byteCount);
        }
    }

    /// <summary>破棄済みなら例外を投げる。</summary>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    public void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <inheritdoc/>
    /// <remarks>読み取りの面（<see cref="IEsBitmap.IsDisposed"/>）が公開面である。 利用者は面の型で判定できる（本型へのダウンキャストは不要）。</remarks>
    public bool IsDisposed => _disposed;

    /// <inheritdoc/>
    public int Width { get; }

    /// <inheritdoc/>
    public int Height { get; }

    /// <inheritdoc/>
    public EsSizeInt Size { get; }

    /// <inheritdoc/>
    public int Stride { get; }

    /// <summary>ピクセル データ先頭アドレス（BGRA8888）。 ピクセルを持たない場合と破棄後は 0。</summary>
    /// <remarks>
    /// <b>派生と本アセンブリ内部だけが使う</b>（公開しない。 生ポインタを渡すと「読み取り専用」の意図が消えるため）。
    /// ポインタが要る外部の相互変換は <see cref="Pixels"/> を <c>fixed</c> で固定して取る
    /// （バッファはアンマネージドのため、生存中はアドレスが動かない）。
    /// </remarks>
    protected nint AddressCore => _buffer.IsClosed ? 0 : _buffer.DangerousGetHandle();

    /// <inheritdoc/>
    /// <remarks>
    /// <see cref="AddressCore"/> から <c><see cref="Stride"/> × <see cref="Height"/></c> バイトを指すビューを返す
    /// （コピーしない）。 ピクセルを持たない場合（サイズ 0）は空のビューを返す。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    public unsafe ReadOnlySpan<byte> Pixels
    {
        get
        {
            ThrowIfDisposed();
            var address = AddressCore;
            return address == 0 ? default : new ReadOnlySpan<byte>((void*)address, Stride * Height);
        }
    }

    /// <summary>ピクセル データの<b>書き込み可能なビュー</b>（派生と本アセンブリ内部だけが使う）。</summary>
    /// <value>先頭から <c><see cref="Stride"/> × <see cref="Height"/></c> バイト。 ピクセルを持たない場合は空。</value>
    /// <remarks>
    /// <see cref="Pixels"/> と同じバッファを指す（コピーしない）。 公開するのは書き換えを認める派生だけであり、
    /// 読み取り専用の派生（<see cref="EsBitmap"/>）は公開しない（書き込みの入口を作らない）。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    protected unsafe Span<byte> WritableBuffer
    {
        get
        {
            ThrowIfDisposed();
            var address = AddressCore;
            return address == 0 ? default : new Span<byte>((void*)address, Stride * Height);
        }
    }

    /// <inheritdoc/>
    /// <remarks>座標の範囲判定は行わない（範囲外は <see cref="IndexOutOfRangeException"/>）。</remarks>
    public unsafe (byte B, byte G, byte R, byte A) GetPixel(int x, int y)
    {
        ThrowIfDisposed();
        var p = (byte*)AddressCore + y * Stride + x * BytesPerPixel;
        return (p[0], p[1], p[2], p[3]);
    }

    /// <inheritdoc/>
    /// <remarks>座標の範囲判定は行わない（範囲外は <see cref="IndexOutOfRangeException"/>）。</remarks>
    public Color GetPixel(EsPointInt point)
    {
        var (b, g, r, a) = GetPixel(point.X, point.Y);
        return Color.FromArgb(a, r, g, b);
    }

    /// <inheritdoc/>
    public string ToSizeText() => $"{Width}x{Height}";

    /// <summary>
    /// ある画像のピクセルを別の画像へ複製する（複製の実体はここ 1 か所）。
    /// </summary>
    /// <param name="source">複製元。</param>
    /// <param name="destination">複製先（同じサイズで確保済みであること）。</param>
    /// <remarks>
    /// 切り出し（<c>Crop</c>）とは異なり、<b>パディングを作らない</b>（同一サイズの全体コピー）。<br/>
    /// <b>静的メソッドにする理由</b>: 読み取り専用の派生と書き換え可能な派生は<b>兄弟</b>であるため、
    /// インスタンス メソッドにすると自型以外のインスタンスへアクセスできない（C# の protected の制約）。
    /// </remarks>
    protected static void CopyPixels(EsBitmapBase source, EsBitmapBase destination)
        => source.Pixels.CopyTo(destination.WritableBuffer);

    /// <summary>ピクセル バッファを解放する（解放の入口は <c>Dispose</c> だけに集約する。 冪等）。</summary>
    protected void ReleaseBuffer()
    {
        if (_disposed) return;
        _disposed = true;

        _buffer.Dispose();
    }

    /// <summary>本画像（自身）の複製を生成する（派生が戻り型を共変にして公開する）。</summary>
    /// <returns>独立した複製。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>別のオブジェクトから実体を生成するのは <c>Create</c> である（<see cref="EsBitmap.Create(IEsBitmap)"/> /
    /// <see cref="EsWritableBitmap.Create(IEsBitmap)"/>）。 本メソッドは<b>自身の複製</b>（同じ型・同じ権利）であり意味が異なる
    /// （実装が同じでも片方へ寄せない）。</remarks>
    public abstract EsBitmapBase Clone();

    /// <summary>保存に使える拡張子（小文字・先頭のドットを含む）。</summary>
    /// <value>`.bmp` / `.png` / `.jpg` / `.jpeg` / `.gif` / `.tif` / `.tiff`。 形式はこの一覧で決まる。</value>
    /// <remarks>
    /// <b>Windows 専用の値</b>（実装は GDI+ のエンコーダーであり、<see cref="Save(string)"/> と同じ制約）。
    /// 形式の判定（拡張子 → エンコーダー）の単一所有は本一覧である（<c>Save</c> は本一覧に無い拡張子を拒否する）。
    /// </remarks>
    public static IReadOnlyList<string> SupportedFileExtensions => WinGdiPlusImageCodec.SupportedExtensions;

    /// <summary>本画像をファイルへ保存する（形式は拡張子が決める）。</summary>
    /// <param name="filePath">保存先のファイルパス（拡張子が形式を決める）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> が null の場合。</exception>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合。</exception>
    /// <exception cref="NotSupportedException">拡張子が対応形式でない場合（<see cref="SupportedFileExtensions"/>）。</exception>
    /// <exception cref="ObjectDisposedException">本画像が破棄済みの場合。</exception>
    /// <exception cref="System.Runtime.InteropServices.ExternalException">エンコード（GDI+）が失敗した場合。</exception>
    /// <remarks>
    /// <b>インターフェイスのではなく本型のメンバーである理由</b>: ファイル入出力は<b>ピクセルを書く権利を要さない</b>ため、
    /// 読み取り専用の面（<see cref="IEsBitmap"/>）の利用者にも必要だが、<see cref="IEsBitmap"/> に置くと
    /// 外部が実装する画像（UI のアダプタ等）にも保存能力を要求することになる。 本型は 2 つの権利の共通基底であるため、
    /// <b>実装を持てる最小の位置</b>がここである（同じ理由で <see cref="SupportedFileExtensions"/> も本型が持つ）。<br/>
    /// <b>ピクセルは複製しない</b>: <see cref="Pixels"/> のビューをそのままエンコードへ渡す
    /// （一時バッファを確保しない）。<br/>
    /// <b>Windows 専用</b>: OS の GDI+ を呼ぶため <see cref="SupportedOSPlatformAttribute"/> で限定する
    /// （副作用は <see cref="EsUtil.Platform.Core.Native.Win.WinGdiPlusLifetime"/> を参照）。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public void Save(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        WinGdiPlusImageCodec.EncodeToFile(Pixels, Width, Height, filePath);
    }

    /// <summary>本画像をファイルへ保存する（形式は拡張子が決める。保存のオプションを指定できる）。</summary>
    /// <param name="filePath">保存先のファイルパス（拡張子が形式を決める）。</param>
    /// <param name="options">保存のオプション（形式ごとに使える項目が異なる）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> が null の場合。</exception>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="options"/> の値が許容範囲外の場合。</exception>
    /// <exception cref="NotSupportedException">拡張子が対応形式でない場合（<see cref="SupportedFileExtensions"/>）、または
    /// <paramref name="options"/> にその形式で使えない項目が設定されている場合。</exception>
    /// <exception cref="ObjectDisposedException">本画像が破棄済みの場合。</exception>
    /// <exception cref="System.Runtime.InteropServices.ExternalException">エンコード（GDI+）が失敗した場合。</exception>
    /// <remarks>
    /// <b>どのオプションが効くかは形式が決める</b>: 実測で効果が確認できたのは JPEG の品質
    /// （<see cref="EsImageSaveOptions.JpegQuality"/>）だけであり、他の形式へ設定すると例外になる
    /// （BMP / PNG / GIF は品質を受け取っても出力が変わらず、TIFF は既定より小さい値が選べない。
    /// 実測値と理由は <see cref="EsImageSaveOptions"/> を参照）。<br/>
    /// <b>オプションを省略した場合との違い</b>: 本オーバーロードは明示された値だけをエンコーダーへ渡すため、
    /// 既定値は GDI+ の既定と一致しない（実測: 1280x720 の不規則な画像で既定 826 KB に対し品質 75 は 554 KB）。
    /// 調整しない場合は <see cref="Save(string)"/> を使う。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public void Save(string filePath, EsImageSaveOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        WinGdiPlusImageCodec.EncodeToFile(Pixels, Width, Height, filePath, options);
    }

    /// <summary>ファイルを読み込んで新しい画像を生成する（派生が公開する <c>Load</c> の共通手順）。</summary>
    /// <typeparam name="T">生成する画像の型（<see cref="EsBitmap"/> または <see cref="EsWritableBitmap"/>）。</typeparam>
    /// <param name="filePath">読み込むファイルのパス（形式は内容から判別される）。 null 不可。</param>
    /// <param name="create">寸法を受け取って、まだピクセルを書いていない画像を生成する処理。</param>
    /// <returns>読み込んだ画像。 所有権は呼び出し側へ移る。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> が null の場合。</exception>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合、ファイルが存在しない場合、
    /// または画像として解釈できない場合。</exception>
    /// <remarks>
    /// <b>本型に置く理由</b>: デコードしたピクセルを整列したバッファへ書き込む手順（<see cref="WritableBuffer"/> への
    /// コピー）は 2 つの派生で同一であり、<b>書き込みの入口を公開しない派生（<see cref="EsBitmap"/>）でも
    /// ここでだけは書ける</b>必要がある。 派生の公開面（<c>EsBitmap.Load</c> / <c>EsWritableBitmap.Load</c>）は
    /// 戻り型だけが違うため、読み込みの規則は本メソッドが単一所有する。<br/>
    /// <b>0 埋めを省く</b>: 生成の直後にデコード結果で全領域を書くため、未初期化で確保する
    /// （<paramref name="create"/> が全領域を書く責任を持つ）。<br/>
    /// <b>中間バッファを作らない</b>: デコードは <see cref="WinGdiPlusImageCodec.DecodeFromFile{T}(string, Func{int, int, Action{Span{byte}}, T})"/>
    /// が本メソッドの生成した画像のバッファへ直接書く（実測: 4K PNG の読み込みで 29.9 ms。 中間バッファを挟むと
    /// +31 MiB と +2.7 ms ＝ +8%）。<br/>
    /// <b>形式と縮小の規則</b>は <see cref="WinGdiPlusImageCodec.DecodeFromFile{T}(string, Func{int, int, Action{Span{byte}}, T})"/> を参照。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    private protected static T LoadCore<T>(string filePath, Func<int, int, T> create)
        where T : EsBitmapBase
        => WinGdiPlusImageCodec.DecodeFromFile(filePath, (width, height, fill) =>
        {
            var image = create(width, height);
            fill(image.WritableBuffer);
            return image;
        });

    /// <summary>全ピクセルを指定色（BGRA）で塗りつぶす（派生と本アセンブリ内部だけが使う）。</summary>
    /// <param name="b">青成分（0〜255）。</param>
    /// <param name="g">緑成分（0〜255）。</param>
    /// <param name="r">赤成分（0〜255）。</param>
    /// <param name="a">アルファ成分（0〜255）。</param>
    /// <remarks>与えられた成分をそのまま書き込む（乗算済みへの変換は行わない）。 色付き生成（<c>Create(w, h, color)</c>）が使う。</remarks>
    protected void ClearCore(byte b, byte g, byte r, byte a)
    {
        var buffer = WritableBuffer;
        if (buffer.IsEmpty)
        {
            return;
        }

        // BGRA の並びをそのまま uint として書き込む（成分の入れ替えをしない）
        var pattern = (uint)(b | (g << 8) | (r << 16) | (a << 24));
        // 要素数は幅×高さ（ストライドは幅×4 のため、ストライド経由で数えると桁が大きくなるだけ）
        MemoryMarshal.Cast<byte, uint>(buffer).Fill(pattern);
    }
}
