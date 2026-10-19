using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;


namespace EsUtil.Feature.Marquee;

/// <summary>選択矩形コントロールのラベル 1 件を、UI のテキスト要素として配置するための指示。</summary>
/// <param name="DipRect">要素を置く矩形（DIP。 呼び出し側の座標系に従う）。</param>
/// <param name="PaddingX">要素の左右の内側余白（DIP）。 0 以上。</param>
/// <param name="PaddingY">要素の上下の内側余白（DIP）。 0 以上。</param>
/// <param name="Text">表示する文言。</param>
/// <param name="FontSize">フォントサイズ（DIP）。</param>
/// <param name="TextColor">文字色。</param>
/// <param name="BackgroundColor">背景色（アルファ 0 は透明）。</param>
/// <param name="IsVisible">外観（<see cref="MarqueeAppearance"/>）が指定する表示可否（UI が要素として置くかどうかの判断に使う）。</param>
/// <remarks>
/// <b>位置付け</b>: 「文字を UI 要素として配置する」UI のための配置指示である。 コアが決めた文言・基準点と、<br/>
/// 描画の外観（<see cref="MarqueeAppearance"/>）が単一所有する体裁（フォントサイズ・色・内側余白）を、<br/>
/// UI が「テキスト要素を 1 つ置く」だけで反映できる形へ写した値である。<br/>
/// <br/>
/// <b>余白は値として渡す</b>: 背景の大きさは「文字の実寸 + 内側余白 × 2」であり、文字は同じ余白だけ内側へ入る。<br/>
/// UI が要素の内側余白としてそのまま設定できるよう、算出の基準（余白そのもの）を値として持つ<br/>
/// （背景矩形と文字原点の差から導出させない）。<br/>
/// <b>角丸は持たない</b>: 選択矩形のラベルは矩形（角丸なし）であり、指定の余地を作らない。<br/>
/// <b>表示可否は値として持つ</b>: 計画は表示可否（<see cref="MarqueeAppearance.ShowSizeLabel"/> /
/// <see cref="MarqueeAppearance.ShowCoordLabel"/>）でも配置指示を消さないため、<br/>
/// UI は本値で要素を置くかどうかを判断する（所有者は外観であり、本値はそれを写したものである）。<br/>
/// <b>色は UI 非依存の <see cref="DrawingColor"/></b> であり、UI 境界で自 UI の色型へ変換する。
/// </remarks>
public readonly record struct MarqueeLabelPlacement(
    EsRect DipRect,
    double PaddingX,
    double PaddingY,
    string Text,
    double FontSize,
    DrawingColor TextColor,
    DrawingColor BackgroundColor,
    bool IsVisible);
