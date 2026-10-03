using EsUtil.Platform.Core.Geometry;


namespace EsUtil.Feature.Marquee.Presenter.Drawing;

/// <summary>描画層が UI 層へ渡すテキスト（文言と基準点）を表す値。</summary>
/// <param name="Text">表示する文言（描画層が生成する。 ホストがプロバイダで与えた場合はその文字列）。</param>
/// <param name="Point">基準点（DIP。 文字の左上を置く点）。</param>
/// <param name="ClampArea">描画領域のサイズ（DIP。 0 以下の軸はクランプしない。 既定（0）はクランプなし）。</param>
/// <remarks>
/// <b>描画層はラベルを特殊扱いしない</b>: 描画層が決めるのは「文言・位置・領域」までであり、<br/>
/// ラベルを<b>テキスト要素（<c>TextBlock</c> 等）として置くか、画像として描くかは UI 層が決める</b>。<br/>
/// そのためフォントサイズ・色・背景・内側余白は本値に含まない（外観が単一所有する）。<br/>
/// 背景矩形と文字の原点はテキストの実寸に依存するため、UI 層が実寸を測って解決する<br/>
/// （体裁の計算は描画層（<c>MarqueeAppearance</c> / <c>MarqueePresenter</c>）が単一所有する）。<br/>
/// <br/>
/// <b>サイズラベルとカーソル座標ラベルは同じ型を使う</b>: 差は「基準点・領域」だけであり、<br/>
/// 位置の算出（注目矩形の左上 / カーソル + オフセット）は描画層が単一所有する。<br/>
/// <b>単位は DIP</b>である（描画領域・選択矩形の座標系と同じ）。
/// </remarks>
public readonly record struct MarqueeTextPlacement(
    string Text,
    EsPoint Point,
    EsSize ClampArea);
