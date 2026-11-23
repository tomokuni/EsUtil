using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Annotation.Presenter.Drawing;

/// <summary>本文の編集中に描くキャレットと選択範囲の見た目（単一所有）。</summary>
/// <remarks>
/// <b>4 つの UI 版で同じ見た目にする</b>: 文字の上へ重ねる装飾は、下の画像が何であっても読める必要があるため
/// 明暗で色を変えない（テーマ非依存の固定色）。 値は本クラスが単一所有し、UI は自分の描画 API へ写すだけにする。<br/>
/// <b>太さは DIP</b>である（表示倍率を変えても細くならない）。 文字の寸法は元画像ピクセルだが、キャレットの
/// 線幅は画面の細さを保つ。
/// </remarks>
public static class AnnotationEditorAppearance
{
    /// <summary>キャレットの太さ（DIP）。</summary>
    public const double CaretThicknessDip = 1.6;

    /// <summary>キャレットの色（不透明の濃い灰色）。</summary>
    public static DrawingColor CaretColor { get; } = DrawingColor.FromArgb(255, 20, 20, 20);

    /// <summary>選択範囲の色（本文の上へ薄く重ねる半透明の青）。</summary>
    public static DrawingColor SelectionColor { get; } = DrawingColor.FromArgb(72, 0, 120, 215);
}
