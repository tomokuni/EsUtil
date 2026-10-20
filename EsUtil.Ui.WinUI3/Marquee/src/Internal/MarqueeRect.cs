namespace EsUtil.Ui.WinUI3.Marquee.Internal;

/// <summary>描画する矩形 1 件（塗り色付き。 単位は呼び出し側が決める）。</summary>
/// <param name="X">左端。</param>
/// <param name="Y">上端。</param>
/// <param name="Width">幅。</param>
/// <param name="Height">高さ。</param>
/// <param name="ColorArgb">塗り色（<c>0xAARRGGBB</c>）。</param>
/// <remarks>
/// <b>UI 型を持たない</b>: 本型は数値と色だけで構成するため、XAML の要素を作らずに「どこへ何色を置くか」を検証できる。<br/>
/// 本コントロールの描画計画は物理ピクセルで解決されるため、本型も物理ピクセルで持つ<br/>
/// （要素へ写すときにだけ DIP へ換算する）。
/// <para/>
/// <b>複製元: EsUtil.SaveEvidence.Platform.WinUI3.Drawing.DrawRect</b>（単独パッケージとして配布するための意図的な複製。 依存関係 §4.1）。
/// </remarks>
public readonly record struct MarqueeRect(double X, double Y, double Width, double Height, uint ColorArgb);
