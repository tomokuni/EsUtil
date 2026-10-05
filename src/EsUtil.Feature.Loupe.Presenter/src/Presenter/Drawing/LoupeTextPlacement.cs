using EsUtil.Platform.Core.Geometry;


namespace EsUtil.Feature.Loupe.Presenter.Drawing;

/// <summary>描画層が UI 層へ渡すテキスト（文言と基準点）を表す値。</summary>
/// <param name="Text">表示する文言（コアが生成する）。</param>
/// <param name="Point">基準点（DIP。 文字の左上を置く点）。</param>
/// <param name="ClampArea">描画領域のサイズ（DIP。 0 以下の軸はクランプしない。 既定（0）はクランプなし）。</param>
/// <remarks>
/// <b>描画層はラベルを特殊扱いしない</b>: 描画層が決めるのは「文言・位置・領域」までであり、<br/>
/// ラベルを<b>テキスト要素（<c>TextBlock</c> 等）として置くか、画像として描くかは UI 層が決める</b>。<br/>
/// そのためフォントサイズ・色・背景・内側余白は本値に含まない（描画の設定が単一所有する）。<br/>
/// 背景矩形と文字の原点はテキストの実寸に依存するため、UI 層が実寸を測って解決する<br/>
/// （体裁の計算は描画層（<see cref="LoupeAppearance"/> / <see cref="LoupePresenter.ResolveLabelPlacements"/>）が単一所有する）。<br/>
/// <br/>
/// <b>単位は DIP</b>である（描画領域・ルーペの表示サイズと同じ座標系。 物理ピクセルへの換算は描画時に行う）。<br/>
/// <br/>
/// <b>本型は公開面ではない</b>: 本層の <c>internal</c> の写像（<c>LabelPlan</c> / <c>LabelTexts</c>）だけが使い、<br/>
/// UI 層とホストは文言・基準点を <see cref="LoupeLabelPlacement"/>（<c>LoupeDrawPlan</c> 経由）で受け取る。
/// </remarks>
internal readonly record struct LoupeTextPlacement(
    string Text,
    EsPoint Point,
    EsSize ClampArea);
