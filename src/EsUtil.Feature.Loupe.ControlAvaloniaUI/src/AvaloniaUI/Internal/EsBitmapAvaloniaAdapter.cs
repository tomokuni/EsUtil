using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Color = System.Drawing.Color;

// 複製元: EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging（独立パッケージ用の意図的な複製。 .github/依存関係.md §4.1）
namespace EsUtil.Feature.Loupe.AvaloniaUI.Internal;

/// <summary>コアの画像（<see cref="IEsBitmap"/>）を、表示境界の <see cref="IImage"/> とコアの読み取り面 <see cref="IEsBitmap"/> の両面で公開するアダプタ。</summary>
/// <remarks>
/// <b>目的</b>: 表示境界（<c>CropViewControl.Source</c> 等）が画像の型に依存しないようにする。 ホストはコアの画像を
/// 本クラスで包み、コアの処理は <see cref="IEsBitmap"/> として画像を取り出す。<br/>
/// <br/>
/// <b>射影元はコアの画像に限る</b>: Avalonia の <see cref="Bitmap"/> を真実とする入口は持たない。 本クラスは
/// 「コアの画像 → 表示（Avalonia）」の<b>一方向</b>の適合であり、その向きを名前に持つ
/// （<c>IEsBitmap</c> → <c>IImage</c>）。 Avalonia の画像をコアで扱う場合は
/// <c>ToEsWritableBitmap()</c> でコアの画像へ変換してから本クラスで包む（変換の責務が呼び出し側に現れる）。<br/>
/// <b>読み取りの面しか公開しない</b>: 本クラスは <see cref="IEsBitmap"/> だけを実装する（<see cref="IEsWritableBitmap"/> を
/// 実装しない）。 これは<b>表示境界の画像を書き換えられないようにする</b>ためであり、破棄の権利も公開しない
/// （<see cref="IImage"/> は非所有の契約であり、真実の画像を破棄するのは保持するホストである）。<br/>
/// <b>射影は初回に一度だけ</b>: 表示用の射影（Avalonia の <see cref="Bitmap"/>）は初回の描画（または寸法取得）で
/// 1 回だけ作り、以降は作り直さない（ピクセルを複製するため。 実測: 4K で 7.8 ms / 32 MB）。<br/>
/// <b>表示用の射影だけを縮小できる</b>: <see cref="DisplayMaxSize"/> を指定すると、表示用の射影は
/// コアの面積平均（<see cref="EsBitmapExtensions.Resize"/> の <see cref="DownscaleAlgorithm.Box"/>）で縮小される
/// （実測: 4K から 320x180 で 1.8 ms / 230 KB。 等倍の射影は 7.8 ms / 32 MB）。
/// 一覧のサムネイルのように小さく表示する場面で、元の寸法の複製を保持しないための入口である。
/// <b>画像が上限に収まっている場合は縮小しない</b>（等倍の射影を共用する）。<br/>
/// <b>保存・クリップボード用の <see cref="GetAvaloniaBitmap"/> は等倍のまま</b>である（表示と保存で射影を分ける）。<br/>
/// <b>不変条件</b>: 真実の内容は<b>本クラスを生成した後は書き換えないこと</b>（射影は初回で固定されるため、
/// 書き換えても表示へ反映されない）。 内容を変える場合は新しい画像と新しい本クラスを生成する。<br/>
/// <b>破棄済みの扱い</b>: 真実が破棄済みの場合、寸法やピクセルの取得は <see cref="ObjectDisposedException"/> を投げる
/// （無効な画像の値を正常値として渡さない。 <see cref="Draw"/> だけは no-op）。<br/>
/// <b>本クラス自体の破棄</b>: <see cref="IImage"/> は非所有の契約（<see cref="IImage"/> は <see cref="IDisposable"/> を継承せず、
/// <c>Image.Source</c> の設定では破棄されない）のため、<b>本クラスを保持するホストが差し替え時に <see cref="Dispose"/> する</b>。
/// 破棄しないと射影（Avalonia の <see cref="Bitmap"/>）が残る（4K で 32 MB/枚）。<br/>
/// <b>差し替えの順序</b>: 差し替えるときは<b>新しいアダプタを先に表示（<c>Image.Source</c> 等）へ設定し、
/// 旧アダプタは後から <see cref="Dispose"/> する</b>。 逆順にすると、表示が旧アダプタの射影を参照している間に
/// 破棄され、描画が破棄済みバッファへ触れる（例外・チラつき）。
/// </remarks>
internal sealed class EsBitmapAvaloniaAdapter : IImage, IEsBitmap, IDisposable
{
    /// <summary>真実のコア画像（非所有。 <see cref="Adopt"/> で生成した場合だけ本クラスが所有する）。</summary>
    private readonly IEsBitmap _es;

    /// <summary>Avalonia 側の射影（初回だけ作る）。 所有権は本クラス。</summary>
    private Bitmap? _avaloniaProjection;

    /// <summary>表示用の縮小射影（<see cref="DisplayMaxSize"/> で実際に縮小したときだけ作る）。 所有権は本クラス。</summary>
    private Bitmap? _displayProjection;

    /// <summary>内包するコア画像を所有するかどうか（<see cref="Adopt"/> で生成した場合のみ true）。</summary>
    private readonly bool _ownsSource;

    /// <summary>表示に使う長辺の上限（ピクセル。 0 は上限なし = 等倍で表示する）。</summary>
    /// <remarks>
    /// <b>大きな画像を小さく表示する場面（一覧のサムネイル等）で、表示用の射影を縮小する</b>。
    /// 等倍の射影は画像 1 枚につき元の寸法の複製を保持するため（実測: 4K で 32 MB / 7.8 ms）、
    /// 96px のサムネイルを 5 枚並べると 160 MB を超える。<br/>
    /// <b>アルゴリズムは面積平均</b>（コアの <see cref="EsBitmapExtensions.Resize"/> へ <see cref="DownscaleAlgorithm.Box"/> を渡す）。
    /// <b>画質を優先する理由</b>: 同じ寸法のサムネイルを出すフォルダビュー（ファイル → <c>Bitmap.DecodeToWidth</c>）は
    /// <b>面積平均相当</b>の画質であり、双線形だと<b>同じサイズなのに一覧ごとに見え方が変わる</b>（実測: 4K → 320x180 で
    /// 幅 1 px の細線の輝度は面積平均 21 / 双線形 127 で、どちらが残るかが逆になる）。
    /// 面積平均は内部で並列化されるため 4K 1 枚が 1.8 ms で済む（並列化前は 27 ms で、11 枚なら 0.3 秒の停止だった）。<br/>
    /// <b>画像が上限に収まっている場合は縮小しない</b>（等倍の射影を共用し、同じ寸法の複製を 2 つ作らない）。<br/>
    /// 上限を設定すると <see cref="IImage.Size"/> は縮小後の寸法を返す（Avalonia のレイアウトと描画がその寸法を基準にする）。
    /// <b>コアの読み取り面（<see cref="IEsBitmap.Size"/> / <see cref="IEsBitmap.Width"/> 等）は真実の寸法のまま</b>である。
    /// </remarks>
    public int DisplayMaxSize { get; }

    /// <summary>コアの画像からアダプタを生成する（表示は初回に複製、コアへは恒等で渡す）。</summary>
    /// <param name="source">真実となるコアの画像。 破棄はホストが行う。</param>
    /// <param name="displayMaxSize">表示に使う長辺の上限（ピクセル）。 0 は上限なし（等倍。 既定）。</param>
    /// <remarks><paramref name="displayMaxSize"/> は<b>表示用の射影だけ</b>を縮小する（<see cref="DisplayMaxSize"/>）。</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> が null の場合。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="displayMaxSize"/> が負の場合。</exception>
    public EsBitmapAvaloniaAdapter(IEsBitmap source, int displayMaxSize = 0)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(displayMaxSize);
        _es = source;
        DisplayMaxSize = displayMaxSize;
    }

    /// <summary>画像を引き取って所有するアダプタを生成する（<see cref="Dispose"/> で画像も解放する）。</summary>
    /// <param name="source">所有するコアの画像。</param>
    /// <returns>画像を所有するアダプタ。</returns>
    /// <remarks>
    /// <b>生成した画像を解放する人がいない場合に限る</b>（借用して包む場合はコンストラクタを使う）。<br/>
    /// 所有の有無を生成の入口で分けることで、コンストラクタ（非所有）の意味を変えない
    /// （アダプタは「表示の適合」と「自分の射影の解放」だけを担い、元画像は生成した側が解放するのが原則）。<br/>
    /// <paramref name="source"/> は <see cref="EsWritableBitmap"/> に限る（解放できる型だけを引き取れるようにする）。
    /// </remarks>
    public static EsBitmapAvaloniaAdapter Adopt(EsWritableBitmap source) => new(source, ownsSource: true);

    /// <summary>画像を所有するかどうかを指定して生成する（所有の入口は <see cref="Adopt"/> だけに集約する）。</summary>
    /// <param name="source">真実となるコアの画像。</param>
    /// <param name="ownsSource">true の場合は本クラスが <paramref name="source"/> も解放する。</param>
    private EsBitmapAvaloniaAdapter(IEsBitmap source, bool ownsSource)
    {
        _es = source ?? throw new ArgumentNullException(nameof(source));
        _ownsSource = ownsSource;
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">真実が破棄済みの場合。</exception>
    public int Width
    {
        get
        {
            ThrowIfTruthDisposed(_es);
            return _es.Width;
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">真実が破棄済みの場合。</exception>
    public int Height
    {
        get
        {
            ThrowIfTruthDisposed(_es);
            return _es.Height;
        }
    }

    /// <inheritdoc/>
    /// <remarks><see cref="Size"/> は表示用の Avalonia 型であり、本プロパティは明示的実装にする
    /// （同名で戻り値の型が異なるため、公開面では表示用の <see cref="Size"/> を優先する）。</remarks>
    /// <exception cref="ObjectDisposedException">真実が破棄済みの場合。</exception>
    EsSizeInt IEsBitmap.Size
    {
        get
        {
            ThrowIfTruthDisposed(_es);
            return _es.Size;
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">真実が破棄済みの場合。</exception>
    public int Stride
    {
        get
        {
            ThrowIfTruthDisposed(_es);
            return _es.Stride;
        }
    }

    /// <inheritdoc/>
    /// <remarks>真実のコア画像のビューをそのまま返す（コピーしない）。</remarks>
    /// <exception cref="ObjectDisposedException">真実が破棄済みの場合。</exception>
    public ReadOnlySpan<byte> Pixels => GetEsBitmap().Pixels;

    /// <inheritdoc/>
    /// <remarks>
    /// <b>本クラス自身の <see cref="Dispose"/> では真にならない</b>（射影だけを解放し、本クラスは生存して再び射影を作れる）。
    /// 真実の破棄状態を返す。
    /// </remarks>
    public bool IsDisposed => _es.IsDisposed;

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">真実が破棄済みの場合。</exception>
    public (byte B, byte G, byte R, byte A) GetPixel(int x, int y) => GetEsBitmap().GetPixel(x, y);

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">真実が破棄済みの場合。</exception>
    public Color GetPixel(EsPointInt point) => GetEsBitmap().GetPixel(point);

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">真実が破棄済みの場合。</exception>
    public string ToSizeText() => GetEsBitmap().ToSizeText();

    /// <summary>表示サイズ（DIP）。</summary>
    /// <value>真実の画像から得られる <see cref="Size"/>（<see cref="DisplayMaxSize"/> で縮小した場合は縮小後の寸法）。</value>
    /// <remarks>縮小が必要な場合（画像が上限より大きい場合のときだけ）に表示用の射影を作る。 上限に収まっている場合は真実の寸法を返す。</remarks>
    /// <exception cref="ObjectDisposedException">真実が破棄済みの場合。</exception>
    public Size Size
    {
        get
        {
            ThrowIfTruthDisposed(_es);
            return NeedsScaledProjection(_es)
                ? EnsureDisplayProjection(_es).Size
                : new Size(_es.Width, _es.Height);
        }
    }

    /// <summary>コアの画像を取得する。</summary>
    /// <returns>コアの画像。<b>借用</b>（破棄・書き換えを行わない）。</returns>
    /// <remarks>
    /// 真実をそのまま返す（恒等。 コピーしない）。<br/>
    /// <b>真実が破棄済みの場合は <see cref="ObjectDisposedException"/></b>（無効な画像を渡さない。 null を返して
    /// 呼び出し側へ判定を強いない）。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">真実が破棄済みの場合。</exception>
    public IEsBitmap GetEsBitmap()
    {
        ThrowIfTruthDisposed(_es);
        return _es;
    }

    /// <summary>Avalonia の画像を取得する（<b>等倍</b>。 保存・クリップボード用）。</summary>
    /// <returns>Avalonia の画像。<b>借用</b>（破棄しない）。</returns>
    /// <remarks>
    /// 初回だけ複製し、以降は同じ結果を再利用する。<br/>
    /// 保存（<c>Save</c>）・クリップボード（<c>SetBitmapAsync</c>）のように具体型 <see cref="Bitmap"/> を要求する API へ渡す用途。
    /// <b>表示用の縮小（<see cref="DisplayMaxSize"/>）は適用しない</b>（表示と保存で射影を分ける）。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">真実が破棄済みの場合。</exception>
    public Bitmap GetAvaloniaBitmap()
    {
        ThrowIfTruthDisposed(_es);
        return _avaloniaProjection ??= _es.ToAvaloniaBitmap();
    }

    /// <summary>画像を描画する（<see cref="IImage"/>）。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="sourceRect">画像内の描画元矩形。</param>
    /// <param name="destRect">描画先の矩形。</param>
    /// <remarks>
    /// 縮小が必要な場合は<b>縮小した射影</b>を描画する（<see cref="Size"/> が返す寸法と一致する）。<br/>
    /// 破棄済みの真実の場合は何も描画しない（safe no-op）。
    /// </remarks>
    public void Draw(DrawingContext context, Rect sourceRect, Rect destRect)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_es.IsDisposed)
        {
            // 真実が破棄済み: 射影が残っていれば破棄して何も描画しない
            DisposeProjections();
            return;
        }

        context.DrawImage(EnsureDisplayProjection(_es), sourceRect, destRect);
    }

    /// <summary>表示用の射影を縮小する必要があるか（表示の上限より画像が大きいか）を返す。</summary>
    /// <param name="source">真実のコア画像。</param>
    /// <returns>縮小が必要な場合は true。</returns>
    /// <remarks>
    /// 上限に収まっている画像は縮小しない（同じ寸法の複製を 2 つ作らない。 等倍の射影を共用する）。<br/>
    /// 上限と長辺が等しい場合も縮小しない（コアの縮小は「長辺が上限と一致するときは等倍の複製」を返す）。
    /// </remarks>
    private bool NeedsScaledProjection(IEsBitmap source)
        => DisplayMaxSize > 0 && Math.Max(source.Width, source.Height) > DisplayMaxSize;

    /// <summary>表示用の射影を取得する（初回だけ作る）。</summary>
    /// <param name="source">真実のコア画像。</param>
    /// <returns>表示に使う Avalonia の画像（本クラスが所有する。 縮小が不要な場合は等倍の射影）。</returns>
    /// <remarks>縮小のアルゴリズムはコアの面積平均（<see cref="DownscaleAlgorithm.Box"/>）が単一所有する
    /// （縮小した中間画像は小さく、変換（表示用）のコピーは無視できる。 実測: 320x180 で 230 KB）。</remarks>
    private Bitmap EnsureDisplayProjection(IEsBitmap source)
    {
        if (_displayProjection is { } cached)
        {
            return cached;
        }

        if (!NeedsScaledProjection(source))
        {
            // 縮小が不要（画像が上限に収まっている）: 等倍の射影を共用する
            return _avaloniaProjection ??= source.ToAvaloniaBitmap();
        }

        using var scaled = source.Resize(DisplayMaxSize, DownscaleAlgorithm.Box);
        return _displayProjection = scaled.ToAvaloniaBitmap();
    }

    /// <summary>射影（等倍・縮小）を解放する。</summary>
    private void DisposeProjections()
    {
        _avaloniaProjection?.Dispose();
        _avaloniaProjection = null;
        _displayProjection?.Dispose();
        _displayProjection = null;
    }

    /// <summary>射影を解放する（冪等）。</summary>
    /// <remarks>
    /// <see cref="Adopt"/> で生成した場合は内包する画像も解放する（それ以外は所有しないため破棄しない）。<br/>
    /// 破棄後に <see cref="GetEsBitmap"/> / <see cref="GetAvaloniaBitmap"/> / <see cref="Draw"/> を呼ぶと、
    /// 真実が生存していれば再び射影が作られる（真実が破棄済みの場合は取得が例外、<see cref="Draw"/> が no-op）。
    /// </remarks>
    public void Dispose()
    {
        DisposeProjections();

        if (_ownsSource)
        {
            (_es as IDisposable)?.Dispose();
        }
    }

    /// <summary>真実が破棄済みなら例外を投げる。</summary>
    /// <param name="source">真実の画像。</param>
    /// <remarks>破棄状態は読み取りの面（<see cref="IEsBitmap.IsDisposed"/>）が公開するため、真実の型に依らず判定できる。</remarks>
    private static void ThrowIfTruthDisposed(IEsBitmap source)
        => ObjectDisposedException.ThrowIf(source.IsDisposed, source);
}
