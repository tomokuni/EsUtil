using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native.Win;

namespace EsUtil.Platform.Core.Imaging;

/// <summary>画像の画素バッファ（確保・借用・共有・解放・ファイル入出力）を担う内部型。</summary>
/// <remarks>
/// <b>目的</b>: 公開する 2 型（<see cref="EsBitmap"/> / <see cref="EsReadOnlyBitmap"/>）が同じ実装を使うため、
/// ポインタの扱い・共有・解放・ファイル入出力を本型へ 1 回だけ書く。 公開しないのは、生ポインタと書き込みの入口を
/// アセンブリの外へ出さないためである（公開面の型に置くと、読み取り専用の型が書き込みの入口を持つ）。<br/>
/// <b>ファイル入出力</b>: <see cref="LoadFromFile"/> / <see cref="SaveToFile(string)"/> は<b>画素の寿命の両端</b>であり、
/// 画素を持つ本型が受ける（公開面の <c>Load</c> / <c>Save</c> は本型へ転送するだけになる）。 実装は
/// <see cref="WinGdiPlusImageCodec"/> が単一所有する（<b>Windows 専用</b>）。<br/>
/// <b>画素形式</b>: BGRA8888（アルファ乗算済み）。 ストライドは <c>幅 × <see cref="BytesPerPixel"/></c>（行パディングなし）に
/// 固定する（連続した領域として <see cref="ReadOnlyPixels"/> で公開し、相互変換が同じ手順を使えるようにするため）。<br/>
/// <b>確保と借用</b>: <see cref="Allocate"/> は 64 バイト整列で確保し、<see cref="Borrow"/> は呼び出し側が確保した
/// 領域を<b>複製せずに</b>指す（解放は呼び出し側の責任。 必要なら解放のコールバックを受け取る）。<br/>
/// <b>共有</b>: <see cref="Share"/> は同じ領域を指す別のバッファを返す（<see cref="SafeHandle.DangerousAddRef"/> で
/// 参照を数える）。 先に元を破棄しても共有側の画素は有効であり、最後の 1 つが解放する。<br/>
/// <b>読み取り専用の表明</b>: <see cref="SetReadOnly"/> は以降の書き込みを拒否する（<see cref="Pixels"/> が例外）。
/// 領域そのものが不変になるのではなく、<b>このハンドルから書けなくなる</b>。
/// </remarks>
internal sealed class EsBitmapBuffer : IDisposable
{
    /// <summary>1 ピクセルあたりのバイト数（BGRA8888）。</summary>
    /// <remarks>リテラルの定義はここ 1 か所であり、公開する 2 型（<see cref="EsBitmap"/> / <see cref="EsReadOnlyBitmap"/>）が
    /// それぞれ転送して公開する（同じ 4 を各所へ書かない）。</remarks>
    internal const int BytesPerPixel = 4;

    /// <summary>確保するときの整列バイト数（SIMD・GPU 転送の要件を満たす）。</summary>
    private const int AlignmentBytes = 64;

    /// <summary>画素領域の所有者（確保した場合は解放し、借用の場合は解放しない）。</summary>
    /// <remarks>解放の実装は各 <see cref="SafeHandle"/> の <c>ReleaseHandle</c> が単一所有する。</remarks>
    private readonly SafeHandle _handle;

    /// <summary>共有した側だけが true（<see cref="Dispose"/> で自分の参照だけを返す）。</summary>
    private readonly bool _holdsShare;

    /// <summary>画素領域の共有状態（同じ領域を指す実体が同じインスタンスを共有する）。</summary>
    /// <remarks>
    /// <b>共有する理由</b>: 凍結（<see cref="SetReadOnly"/>）は「<b>この画素領域を書かせない</b>」という
    /// <b>領域そのものの性質</b>であり、実体ごとの性質ではない。 実体ごとに持つと、共有した実体
    /// （<see cref="Share"/> や共有した読み取り専用の実体）を通した凍結が他の実体へ届かず、
    /// 「凍結したのに書ける」状態が残る。
    /// </remarks>
    private readonly StoreState _state;

    /// <summary>画素の先頭アドレス（確保または借用の時点で 1 度だけ求める）。</summary>
    /// <remarks>
    /// <b>保持する理由</b>: <see cref="SafeHandle.DangerousGetHandle"/> は仮想呼び出しを含むため、画素へ触れるたびに呼ぶと
    /// <b>1 画素あたりの費用がその呼び出しで決まる</b>。<br/>
    /// アドレスは領域の生存中に動かない（アンマネージドの確保であり、借用では所有者が動かさない契約）ため、
    /// 1 度求めて保持する。
    /// </remarks>
    private readonly nint _address;

    private bool _disposed;

    private EsBitmapBuffer(SafeHandle handle, int width, int height, bool holdsShare, StoreState state)
    {
        _handle = handle;
        _holdsShare = holdsShare;
        _state = state;
        _address = handle.DangerousGetHandle();
        Width = width;
        Height = height;
        Size = new EsSizeInt(width, height);
        Stride = width * BytesPerPixel;
        ByteCount = (long)Stride * height;
    }

    /// <summary>破棄済みかどうか。</summary>
    internal bool IsDisposed => _disposed;

    /// <summary>この画素領域が凍結されているかどうか（<see cref="SetReadOnly"/> の後は true）。</summary>
    /// <remarks><b>領域の性質</b>であるため、同じ領域を指すすべての実体で同じ値を返す。</remarks>
    internal bool IsReadOnly => _state.IsReadOnly;

    /// <summary>画素を持たないかどうか（サイズ 0 の場合）。</summary>
    internal bool IsEmpty => ByteCount <= 0;

    /// <summary>幅（ピクセル）。</summary>
    internal int Width { get; }

    /// <summary>高さ（ピクセル）。</summary>
    internal int Height { get; }

    /// <summary>幅・高さ（ピクセル）。</summary>
    internal EsSizeInt Size { get; }

    /// <summary>1 ラインあたりのバイト数（常に <c>幅 × <see cref="BytesPerPixel"/></c>）。</summary>
    internal int Stride { get; }

    /// <summary>画素領域のバイト数（<c><see cref="Stride"/> × <see cref="Height"/></c>）。</summary>
    internal long ByteCount { get; }

    /// <summary>画素データの読み取り専用ビューを返す（コピーしない）。</summary>
    /// <returns>先頭から <see cref="ByteCount"/> バイトのビュー。 画素を持たない場合は空。</returns>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    internal unsafe ReadOnlySpan<byte> ReadOnlyPixels
    {
        get
        {
            ThrowIfDisposed();
            var address = Address;
            return address == 0 ? default : new ReadOnlySpan<byte>((void*)address, Stride * Height);
        }
    }

    /// <summary>画素データの書き込み可能なビューを返す（コピーしない）。</summary>
    /// <returns>先頭から <see cref="ByteCount"/> バイトのビュー。 画素を持たない場合は空。</returns>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    /// <exception cref="InvalidOperationException">読み取り専用の場合。</exception>
    internal unsafe Span<byte> Pixels
    {
        get
        {
            ThrowIfDisposed();
            ThrowIfReadOnly();
            var address = Address;
            return address == 0 ? default : new Span<byte>((void*)address, Stride * Height);
        }
    }

    /// <summary>ファイルを読み込んで画素バッファを生成する（公開面の <c>Load</c> の共通手順）。</summary>
    /// <param name="filePath">読み込むファイルのパス（形式は内容から判別される）。 null 不可。</param>
    /// <returns>読み込んだ画素を持つバッファ。 所有権は呼び出し側へ移る。</returns>
    /// <remarks>
    /// <b>0 埋めを省く</b>: 生成の直後にデコード結果で全領域を書くため、未初期化で確保する。<br/>
    /// <b>中間バッファを作らない</b>: デコードは確保した画素領域へ直接書く（中間バッファを挟むと、
    /// その確保と複製の分だけ余分に費用がかかる）。<br/>
    /// <b>Windows 専用</b>（デコードの実装は GDI+ であり、<see cref="WinGdiPlusImageCodec"/> が単一所有する）。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> が null の場合。</exception>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合、ファイルが存在しない場合、
    /// または画像として解釈できない場合。</exception>
    [SupportedOSPlatform("windows")]
    internal static EsBitmapBuffer LoadFromFile(string filePath)
        => WinGdiPlusImageCodec.DecodeFromFile(filePath, (width, height, fill) =>
        {
            var buffer = Allocate(width, height, clear: false);
            try
            {
                fill(buffer.Pixels);
                return buffer;
            }
            catch
            {
                // 確保した領域を解放してから例外を伝える（呼び出し側に解放の機会が無い）
                buffer.Dispose();
                throw;
            }
        });

    /// <summary>指定サイズの画素バッファを 64 バイト整列で確保する。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <param name="clear">true の場合は全領域を 0（透明）で初期化する。 直後に全領域を書く場合は false（0 埋めを省く）。</param>
    /// <returns>確保したバッファ。 解放の責任は呼び出し側が持つ。</returns>
    /// <remarks><b><paramref name="clear"/> = false は全領域を直後に書く場合だけ使う</b>（書かない領域は不定値のまま残る）。</remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> または <paramref name="height"/> が負の場合。</exception>
    /// <exception cref="OutOfMemoryException">確保に失敗した場合。</exception>
    internal static unsafe EsBitmapBuffer Allocate(int width, int height, bool clear)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);

        var byteCount = (long)width * BytesPerPixel * height;
        var handle = SafeAlignedBuffer.Allocate(byteCount, AlignmentBytes);
        var buffer = new EsBitmapBuffer(handle, width, height, holdsShare: false, new StoreState());
        if (clear && byteCount > 0)
        {
            // 透明（全成分 0）で初期化する（未描画領域を「描かれていない」として扱うため）
            NativeMemory.Clear((void*)handle.DangerousGetHandle(), (nuint)byteCount);
        }

        return buffer;
    }

    /// <summary>呼び出し側が確保した領域を、複製せずに画素として扱う（借用）。</summary>
    /// <param name="width">幅（ピクセル）。 0 以上。</param>
    /// <param name="height">高さ（ピクセル）。 0 以上。</param>
    /// <param name="pixels">画素の先頭アドレス（BGRA8888。 <c>幅 × 4 × 高さ</c> バイト以上）。</param>
    /// <param name="release">解放時に 1 回だけ呼ぶ処理（アドレスを受け取る）。 解放が不要な場合は null。</param>
    /// <returns>借用したバッファ。 領域の解放は呼び出し側が行う（<paramref name="release"/> を渡した場合は本バッファの破棄時に呼ばれる）。</returns>
    /// <remarks>
    /// <b>行パディングがある領域は借用できない</b>: 本ライブラリの画素は連続した 1 本の領域として公開するため、
    /// ストライドは <c>幅 × <see cref="BytesPerPixel"/></c> に限る（それ以外は <see cref="ArgumentException"/>）。<br/>
    /// <b>借用中は呼び出し側が書き込んではいけない</b>（本ライブラリは内容の不変を保証しない。 借用は「その時点の画素を読む」
    /// ための入口である）。<br/>
    /// <b>解放のコールバックは <see cref="SafeHandle"/> の解放経路から呼ぶ</b>ため、<c>Dispose</c> と GC の
    /// どちらでも 1 回だけ呼ばれる。
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> または <paramref name="height"/> が負の場合。</exception>
    /// <exception cref="ArgumentException"><paramref name="pixels"/> が 0 の場合（画素がある場合）、または領域が連続していない場合。</exception>
    internal static EsBitmapBuffer Borrow(int width, int height, nint pixels, Action<nint>? release = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);

        var byteCount = (long)width * BytesPerPixel * height;
        if (byteCount > 0 && pixels == 0)
        {
            throw new ArgumentException("画素がある場合はアドレスが必要です。", nameof(pixels));
        }

        var handle = new SafeBorrowedBuffer(pixels, release);
        return new EsBitmapBuffer(handle, width, height, holdsShare: false, new StoreState());
    }

    /// <summary>同じ領域を指す別のバッファを返す（コピーしない。 参照を数える）。</summary>
    /// <returns>共有したバッファ。 破棄は独立して行える（先に元を破棄しても画素は有効なまま）。</returns>
    /// <remarks>共有した側は<b>自分の参照だけ</b>を返す（領域の解放は元の所有者が行う）。 最後の 1 つが解放する。</remarks>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    internal EsBitmapBuffer Share()
    {
        ThrowIfDisposed();
        var added = false;
        _handle.DangerousAddRef(ref added);
        return new EsBitmapBuffer(_handle, Width, Height, holdsShare: true, _state);
    }

    /// <summary>以降の書き込みを拒否する（<b>同じ領域を指すすべての実体</b>が書けなくなる）。</summary>
    /// <remarks>
    /// <b>領域の性質として凍結する</b>（実体ごとではない）: 読み取り専用として渡した先で凍結すると、
    /// <b>書き換え可能な側の実体も書けなくなる</b>（渡した相手の画素が後から変わることを防ぐ）。<br/>
    /// 破棄済みの場合は何もしない。
    /// </remarks>
    internal void SetReadOnly() => _state.SetReadOnly();

    /// <summary>指定座標のピクセルを BGRA で取得する。</summary>
    /// <param name="x">X 座標（ピクセル）。</param>
    /// <param name="y">Y 座標（ピクセル）。</param>
    /// <returns>(B, G, R, A) のタプル（アルファ乗算済み）。</returns>
    /// <remarks>
    /// <b>インライン化を明示する理由</b>: 画素ごとに呼ばれるため、呼び出しの費用が支配的になる。
    /// 本メソッドと <see cref="CheckCoordinates"/> をインライン化すると、呼び出しの分だけ費用が下がる。
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">座標が画像の範囲外の場合。</exception>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal unsafe (byte B, byte G, byte R, byte A) GetPixel(int x, int y)
    {
        ThrowIfDisposed();
        CheckCoordinates(x, y);
        var p = (byte*)_address + y * Stride + x * BytesPerPixel;
        return (p[0], p[1], p[2], p[3]);
    }

    /// <summary>指定座標のピクセルを BGRA で設定する。</summary>
    /// <param name="x">X 座標（ピクセル）。</param>
    /// <param name="y">Y 座標（ピクセル）。</param>
    /// <param name="b">青成分（0〜255）。</param>
    /// <param name="g">緑成分（0〜255）。</param>
    /// <param name="r">赤成分（0〜255）。</param>
    /// <param name="a">アルファ成分（0〜255）。</param>
    /// <remarks>
    /// 座標の計算と範囲判定は本メソッドが単一所有する（読み取りと書き込みで同じ判定を使う）。
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">座標が画像の範囲外の場合。</exception>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    /// <exception cref="InvalidOperationException">読み取り専用の場合。</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal unsafe void SetPixel(int x, int y, byte b, byte g, byte r, byte a)
    {
        ThrowIfDisposed();
        ThrowIfReadOnly();
        CheckCoordinates(x, y);
        var p = (byte*)_address + y * Stride + x * BytesPerPixel;
        p[0] = b;
        p[1] = g;
        p[2] = r;
        p[3] = a;
    }

    /// <summary>全ピクセルを指定色（BGRA）で塗りつぶす。</summary>
    /// <param name="b">青成分（0〜255）。</param>
    /// <param name="g">緑成分（0〜255）。</param>
    /// <param name="r">赤成分（0〜255）。</param>
    /// <param name="a">アルファ成分（0〜255）。</param>
    /// <remarks>与えられた成分をそのまま書き込む（乗算済みへの変換は行わない）。</remarks>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    /// <exception cref="InvalidOperationException">読み取り専用の場合。</exception>
    internal void Clear(byte b, byte g, byte r, byte a)
    {
        var pixels = Pixels;
        if (pixels.IsEmpty)
        {
            return;
        }

        // BGRA の並びをそのまま uint として書き込む（成分の入れ替えをしない）
        var pattern = (uint)(b | (g << 8) | (r << 16) | (a << 24));
        MemoryMarshal.Cast<byte, uint>(pixels).Fill(pattern);
    }

    /// <summary>同じ寸法の別のバッファへ画素を複製する（全体コピー。 パディングは作らない）。</summary>
    /// <param name="destination">複製先（同じ寸法であること）。</param>
    /// <exception cref="ArgumentException">寸法が一致しない場合。</exception>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    /// <exception cref="InvalidOperationException">複製先が読み取り専用の場合。</exception>
    internal void CopyTo(EsBitmapBuffer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (destination.Width != Width || destination.Height != Height)
        {
            throw new ArgumentException($"複製先の寸法が一致しません（元 {Width}x{Height} / 先 {destination.Width}x{destination.Height}）。", nameof(destination));
        }

        ReadOnlyPixels.CopyTo(destination.Pixels);
    }

    /// <summary>本バッファの一部を指す 0 コピーの読み取りビューを生成する（生成の実装を 1 回だけ書く）。</summary>
    /// <param name="rect">ビューの範囲（本バッファの座標。 <b>内側であること</b>）。</param>
    /// <returns>ビュー。 所有権は呼び出し側へ移る。</returns>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rect"/> が空、または範囲外に出る場合。</exception>
    internal EsPixmap ToPixmap(EsRectInt rect)
    {
        ThrowIfDisposed();

        if (rect.Width <= 0 || rect.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rect), rect, "ビューの幅と高さは 1 以上である必要があります。");
        }

        if (rect.X < 0 || rect.Y < 0 || rect.Right > Width || rect.Bottom > Height)
        {
            throw new ArgumentOutOfRangeException(nameof(rect), rect, $"ビューの範囲が画像の外に出ています（画像 {Width}x{Height}）。");
        }

        // 元の画素の参照を 1 つ取り、ビューはその先頭からのオフセットを持つ
        var offsetBytes = (rect.Y * Stride) + (rect.X * BytesPerPixel);
        return new EsPixmap(Share(), offsetBytes, rect.Width, rect.Height, Stride);
    }

    /// <summary>画素をファイルへ保存する（形式は拡張子が決める。 公開面の <c>Save</c> の共通手順）。</summary>
    /// <param name="filePath">保存先のファイル パス。 形式は拡張子が決める。 null 不可。</param>
    /// <remarks>
    /// <b>画素は複製しない</b>: <see cref="ReadOnlyPixels"/> のビューをそのままエンコードへ渡す（一時バッファを確保しない）。<br/>
    /// 形式の決め方と使える値は <see cref="SaveToFile(string, EsImageSaveOptions)"/> を参照。<br/>
    /// <b>Windows 専用</b>（エンコードの実装は GDI+ であり、<see cref="WinGdiPlusImageCodec"/> が単一所有する）。
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合。</exception>
    /// <exception cref="NotSupportedException">拡張子が対応形式でない場合（<see cref="EsReadOnlyBitmap.SupportedFileExtensions"/>）。</exception>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    /// <exception cref="System.Runtime.InteropServices.ExternalException">エンコード（GDI+）が失敗した場合。</exception>
    [SupportedOSPlatform("windows")]
    internal void SaveToFile(string filePath) => SaveToFile(filePath, default);

    /// <summary>画素をファイルへ保存する（形式とオプションを指定できる。 公開面の <c>Save</c> の共通手順）。</summary>
    /// <param name="filePath">保存先のファイル パス（拡張子が形式を決める。 <paramref name="options"/> で形式を指定した場合に拡張子が無ければ補完する）。</param>
    /// <param name="options">保存のオプション（形式ごとに使える項目が異なる。 既定は拡張子が形式を決める）。</param>
    /// <remarks>
    /// <b>形式の決め方と使える値</b>の規則は公開面（<see cref="EsReadOnlyBitmap.Save(string, EsImageSaveOptions)"/> の備考）が
    /// 単一所有し、本メソッドは受け取った値をそのまま <see cref="WinGdiPlusImageCodec.EncodeToFile(ReadOnlySpan{byte}, int, int, string, EsImageSaveOptions)"/> へ渡す。
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合、または
    /// <paramref name="filePath"/> の拡張子と <see cref="EsImageSaveOptions.Format"/> が食い違い
    /// <see cref="EsImageSaveOptions.IgnoreFormatMismatch"/> が false の場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="options"/> の値が許容範囲外の場合。</exception>
    /// <exception cref="NotSupportedException">拡張子が対応形式でない場合、または <paramref name="options"/> に
    /// その形式で使えない項目が設定されている場合。</exception>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    /// <exception cref="System.Runtime.InteropServices.ExternalException">エンコード（GDI+）が失敗した場合。</exception>
    [SupportedOSPlatform("windows")]
    internal void SaveToFile(string filePath, EsImageSaveOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ThrowIfDisposed();
        WinGdiPlusImageCodec.EncodeToFile(ReadOnlyPixels, Width, Height, filePath, options);
    }

    /// <summary>破棄済みなら <see cref="ObjectDisposedException"/> を送出する。</summary>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, typeof(EsBitmapBuffer));

    /// <summary>読み取り専用なら <see cref="InvalidOperationException"/> を送出する。</summary>
    /// <exception cref="InvalidOperationException">読み取り専用の場合。</exception>
    internal void ThrowIfReadOnly()
    {
        if (_state.IsReadOnly)
        {
            throw new InvalidOperationException("読み取り専用の画像には書き込めません（SetReadOnly で凍結されています）。 書き換える場合は EsBitmap.Create で複製してください。");
        }
    }

    /// <summary>画素領域を解放する（冪等）。</summary>
    /// <remarks>
    /// <b>共有している場合は自分の参照だけを返す</b>: 実体の解放は元の所有者が行う（参照を数えているため、
    /// 先に破棄した側が実体を解放してしまうことはない）。
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_holdsShare)
        {
            _handle.DangerousRelease();
            return;
        }

        _handle.Dispose();
    }

    /// <summary>画素の先頭アドレス（破棄後は 0）。</summary>
    /// <remarks>
    /// 本アセンブリの中だけで使う（公開しない。 生ポインタを渡すと読み取り専用の意図が消えるため）。<br/>
    /// アドレスは <see cref="_address"/>（生成時に 1 度だけ求めた値）を返す（<see cref="SafeHandle.DangerousGetHandle"/>
    /// を画素ごとに呼ばない）。
    /// </remarks>
    internal nint Address => _disposed ? 0 : _address;

    /// <summary>座標が画像の範囲内かを確かめる。</summary>
    /// <param name="x">X 座標（ピクセル）。</param>
    /// <param name="y">Y 座標（ピクセル）。</param>
    /// <remarks>
    /// <b>範囲を確かめる理由</b>: 範囲外の書き込みは<b>隣の行の画素を壊す</b>（境界を跨いだ位置はバッファの内側に
    /// 落ちるため、判定しないと静かに別の画素が変わる）。<br/>
    /// <b><see cref="ArgumentOutOfRangeException"/> を投げる理由</b>: <see cref="IndexOutOfRangeException"/> は
    /// ランタイムが予約した例外であり、送出を禁止するアナライザー（CA2201 / MA0012）がある。 同じ用途を持つ
    /// 標準の型（<c>System.Drawing.Bitmap.GetPixel</c>）も <see cref="ArgumentOutOfRangeException"/> を投げる。<br/>
    /// <b>インライン化を明示する理由</b>: 画素ごとに呼ばれるため、呼び出しの費用が支配的になる
    /// （<see cref="GetPixel(int, int)"/> の備考）。
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="x"/> または <paramref name="y"/> が画像の範囲外の場合。</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CheckCoordinates(int x, int y)
    {
        if ((uint)x >= (uint)Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x), x, $"X 座標が画像の範囲外です（幅 {Width}）。");
        }

        if ((uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y), y, $"Y 座標が画像の範囲外です（高さ {Height}）。");
        }
    }

    /// <summary>画素領域の共有状態（同じ領域を指す実体の間で共有する）。</summary>
    /// <remarks>
    /// <b>参照を持つだけで解放しない</b>（領域の解放は <see cref="SafeHandle"/> が担う）。 したがって
    /// 本状態の寿命は領域の寿命より短いか同じであり、最後の実体が解放した後に本状態へ触れることはない。
    /// </remarks>
    internal sealed class StoreState
    {
        /// <summary>凍結済みかどうか（0 = 未凍結 / 1 = 凍結）。 複数のスレッドから読める。</summary>
        private int _isReadOnly;

        /// <summary>凍結済みかどうか。</summary>
        internal bool IsReadOnly => Volatile.Read(ref _isReadOnly) != 0;

        /// <summary>凍結する（<b>同じ領域を指すすべての実体</b>に効く）。</summary>
        internal void SetReadOnly() => Volatile.Write(ref _isReadOnly, 1);
    }
}
