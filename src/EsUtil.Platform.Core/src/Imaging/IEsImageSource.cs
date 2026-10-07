namespace EsUtil.Platform.Core.Imaging;

/// <summary>画像の供給元を表す UI 非依存の抽象（表示境界の画像をコアの処理へ橋渡しする）。</summary>
/// <remarks>
/// <b>目的</b>: UI（Avalonia / MewUI）の表示境界は画像の型が異なる（Avalonia は <c>IImage</c>、MewUI は
/// <see cref="EsBitmap"/> を直接描く）。 表示の境界が <see cref="IEsImageSource"/> を公開することで、<br/>
/// コアの処理（切り出しの対象・ルーペの拡大元）は UI の型を知らずに画像を取り出せる。<br/>
/// <br/>
/// <b>ピクセル操作は含めない</b>: 画素（<c>Address</c> / <c>Stride</c> / <c>GetPixel</c> / <c>SetPixel</c>）は
/// <see cref="EsBitmap"/> が単一所有する。 本抽象に含めると、実装側が画素を持たない場合に 1 ピクセルずつ
/// コピーする実装を強いることになり（ホットパスが遅くなる）、画素 API を持つ型（<see cref="EsBitmap"/>）へ
/// 委譲する余地がなくなる。<br/>
/// <b>寸法の文字列化は含めない</b>: 書式の単一所有は <see cref="EsImageSourceText.ToSizeText"/> が担う
/// （インターフェイスの既定実装にすると、実装ごとに書式を持てる余地が残る）。
/// </remarks>
public interface IEsImageSource
{
    /// <summary>幅（ピクセル）。</summary>
    int Width { get; }

    /// <summary>高さ（ピクセル）。</summary>
    int Height { get; }

    /// <summary>コアの画像を取得する。</summary>
    /// <returns>コアの画像。<b>借用</b>（破棄しない。 所有権は実装が保持する）。</returns>
    /// <remarks>
    /// <b>戻り値は借用であり、破棄・書き換えを行わないこと</b>（所有権は実装にある）。<br/>
    /// 内容を変える場合・切り出す場合は、新しい画像を生成して実装を作り直す<br/>
    /// （本抽象は変更操作を提供しない。 ピクセル操作が要る場合は取得した <see cref="EsBitmap"/> の API を使う）。
    /// </remarks>
    EsBitmap GetEsBitmap();
}
