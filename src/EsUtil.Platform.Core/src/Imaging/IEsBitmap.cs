using EsUtil.Platform.Core.Geometry;
using Color = System.Drawing.Color;

namespace EsUtil.Platform.Core.Imaging;

/// <summary>画像を「読み取る」ためだけの抽象（借用して読む側の型）。</summary>
/// <remarks>
/// <b>目的</b>: 画像の利用者を「読み取る / 書き換える / 所有する」の 3 つに分け、**どの権利を持つかを型で表す**。
/// <see cref="IEsBitmap"/> を受け取った側は、画像を<b>書き換えられず・破棄もできない</b>
/// （書き換えが要る場合は <see cref="IEsWritableBitmap"/>、破棄が要る場合は生成した側が具象型で保持する）。<br/>
/// <br/>
/// <b>含めないもの</b>: <see cref="IDisposable"/>（借用に破棄の権利を与えない）・変更操作・
/// 生ポインタ（<c>Address</c>。 渡すと読み取り専用の意図が消えるため、ピクセルは <see cref="Pixels"/> で読む）。<br/>
/// <b>破棄状態は公開する</b>: <see cref="IsDisposed"/> は「破棄の権利」ではなく「その画像がまだ使えるか」の情報であり、
/// 受け取った側が破棄済みを描画・計算へ渡さないための判定に使う。 実体の型（<see cref="EsBitmapBase"/> の派生）へ
/// ダウンキャストすると、本インターフェイスを直接実装する画像（表示アダプタ等）を判定できずに取りこぼす。<br/>
/// <b>実装の責務</b>: ピクセル形式は BGRA8888（アルファ乗算済み）、ストライドは <c>幅 × <see cref="BytesPerPixel"/></c>
/// （行パディングなし）に固定する（相互変換が同じ手順を使えるようにするため）。<br/>
/// <b>破棄済みの扱い</b>: 実装は破棄済みの場合に <see cref="ObjectDisposedException"/> を投げる
/// （無効な画像の値を正常値として渡さない）。
/// </remarks>
public interface IEsBitmap
{
    /// <summary>1 ピクセルあたりのバイト数（BGRA8888）。</summary>
    /// <value>4。 ストライドは常に <c>幅 × 本値</c> である。</value>
    /// <remarks>リテラルの定義はここ 1 か所であり、実装（<see cref="EsBitmap"/>）は転送して公開する
    /// （相互変換がストライドを計算するために使う。 同じ 4 を各所へ書かない）。</remarks>
    const int BytesPerPixel = 4;

    /// <summary>破棄済みかどうか（true の場合は他のメンバーが <see cref="ObjectDisposedException"/> を投げる）。</summary>
    /// <remarks>破棄の<b>権利</b>は与えない（破棄できるのは生成した側だけ）。 本値は「まだ使えるか」の情報である。
    /// 実装は、真実（実際のビットマップ）の破棄状態を返す。</remarks>
    bool IsDisposed { get; }

    /// <summary>幅（ピクセル）。</summary>
    int Width { get; }

    /// <summary>高さ（ピクセル）。</summary>
    int Height { get; }

    /// <summary>幅・高さ（ピクセル）。</summary>
    EsSizeInt Size { get; }

    /// <summary>1 ラインあたりのバイト数（パディングなし。 常に <c>幅 × <see cref="BytesPerPixel"/></c>）。</summary>
    int Stride { get; }

    /// <summary>ピクセル データ（BGRA8888・アルファ乗算済み）の<b>読み取り専用ビュー</b>。</summary>
    /// <value>先頭から <c><see cref="Stride"/> × <see cref="Height"/></c> バイト。 ピクセルを持たない場合は空。</value>
    /// <remarks>
    /// <b>コピーしない</b>（同じバッファを指すビュー）。 長さは <c><see cref="Stride"/> × <see cref="Height"/></c>。<br/>
    /// 生ポインタが要る場合（他フレームワークの非所有ビューへ渡す等）は <c>fixed</c> で取る
    /// （本画像のバッファはアンマネージドのため、アドレスは生存中動かない）。<br/>
    /// <b>書き込みは提供しない</b>（書き換える場合は <see cref="IEsWritableBitmap.WritablePixels"/> を使う）。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    ReadOnlySpan<byte> Pixels { get; }

    /// <summary>指定座標のピクセルを BGRA で取得する。</summary>
    /// <param name="x">X 座標（ピクセル）。</param>
    /// <param name="y">Y 座標（ピクセル）。</param>
    /// <returns>(B, G, R, A) のタプル。 成分はアルファ乗算済み。</returns>
    /// <exception cref="IndexOutOfRangeException">座標が画像の範囲外の場合。</exception>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    (byte B, byte G, byte R, byte A) GetPixel(int x, int y);

    /// <summary>指定座標のピクセルを色として取得する（座標型を受け取る版）。</summary>
    /// <param name="point">座標（ピクセル）。</param>
    /// <returns>取得した色（<see cref="Color"/>）。 成分はアルファ乗算済み。</returns>
    /// <exception cref="IndexOutOfRangeException">座標が画像の範囲外の場合。</exception>
    /// <exception cref="ObjectDisposedException">破棄済みの場合。</exception>
    Color GetPixel(EsPointInt point);
}
