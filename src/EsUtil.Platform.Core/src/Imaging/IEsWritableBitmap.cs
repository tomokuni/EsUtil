using Color = System.Drawing.Color;

namespace EsUtil.Platform.Core.Imaging;

/// <summary>画像を「書き換える」ための抽象（借用して書き換える側の型）。</summary>
/// <remarks>
/// <b>目的</b>: <see cref="IEsBitmap"/> が表す読み取りの権利に、<b>書き換えの権利</b>だけを足した型。
/// 書き換えてよい相手（自分が生成した画像・ホストから書き換えを委ねられた画像）にだけこの型を使う。<br/>
/// <br/>
/// <b>含めないもの</b>: <see cref="IDisposable"/>（書き換えてよいことと破棄してよいことは別の権利である。
/// 破棄するのは生成した側であり、その側は書き込み可能な具象型として画像を保持する）。<br/>
/// <b>破棄済みの扱い</b>: 実装は破棄済みの場合に <see cref="ObjectDisposedException"/> を投げる。
/// </remarks>
public interface IEsWritableBitmap : IEsBitmap
{
    /// <summary>ピクセル データ（BGRA8888・アルファ乗算済み）の<b>書き込み可能なビュー</b>。</summary>
    /// <value>先頭から <c><see cref="IEsBitmap.Stride"/> × <see cref="IEsBitmap.Height"/></c> バイト。 ピクセルを持たない場合は空。</value>
    /// <remarks>
    /// <b>コピーしない</b>（同じバッファを指すビュー）。 長さは <c><see cref="IEsBitmap.Stride"/> × <see cref="IEsBitmap.Height"/></c>。<br/>
    /// 生ポインタが要る場合は <c>fixed</c> で取る（アンマネージドのため、アドレスは生存中動かない）。<br/>
    /// <b>名前を <c>Pixels</c> と分ける理由</b>: 同じ名前にすると <see cref="IEsBitmap"/> 経由か本型経由かで
    /// 返る型が変わり（書き込みできるかどうかが読み手に分からない）、取り違えを招く。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    Span<byte> WritablePixels { get; }

    /// <summary>指定座標のピクセルを BGRA で設定する。</summary>
    /// <param name="x">X 座標（ピクセル）。</param>
    /// <param name="y">Y 座標（ピクセル）。</param>
    /// <param name="b">青成分（0〜255）。</param>
    /// <param name="g">緑成分（0〜255）。</param>
    /// <param name="r">赤成分（0〜255）。</param>
    /// <param name="a">アルファ成分（0〜255）。</param>
    /// <exception cref="IndexOutOfRangeException">座標が画像の範囲外の場合。</exception>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    void SetPixel(int x, int y, byte b, byte g, byte r, byte a);

    /// <summary>指定座標へ色を設定する（範囲外は無視する）。</summary>
    /// <param name="x">X 座標（ピクセル）。</param>
    /// <param name="y">Y 座標（ピクセル）。</param>
    /// <param name="color">設定する色。</param>
    /// <returns>書き込めたかどうか。 範囲外の場合は false（例外にしない）。</returns>
    /// <remarks>
    /// 描画（破線・矩形オーバーレイ等）のように<b>画像の端をはみ出す矩形を描く</b>場合に使う。<br/>
    /// <see cref="SetPixel(int, int, byte, byte, byte, byte)"/> は範囲外で例外のため、
    /// 範囲外を無視したい呼び出し側は本メソッドを使う（範囲判定を各呼び出し側へ書かない）。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    bool TrySetPixel(int x, int y, Color color);

    /// <summary>全ピクセルを指定色（BGRA）で塗りつぶす。</summary>
    /// <param name="b">青成分（0〜255）。</param>
    /// <param name="g">緑成分（0〜255）。</param>
    /// <param name="r">赤成分（0〜255）。</param>
    /// <param name="a">アルファ成分（0〜255）。</param>
    /// <remarks>与えられた成分をそのまま書き込む（乗算済みへの変換は行わない）。</remarks>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    void Clear(byte b, byte g, byte r, byte a);

    /// <summary>全ピクセルを指定色で塗りつぶす。</summary>
    /// <param name="color">塗りつぶし色。</param>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    void Clear(Color color);
}
