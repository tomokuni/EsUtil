using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinUI3.Interop.Drawing;

/// <summary>描画する矩形 1 件（塗り色付き）。</summary>
/// <param name="Bounds">矩形（描画先の座標。 単位は呼び出し側が決める）。</param>
/// <param name="Color">塗り色（不透明度はアルファ）。</param>
/// <remarks>
/// <b>UI 型を持たない</b>: 本型はコアの <see cref="EsRect"/> と UI 非依存の色だけで構成する。<br/>
/// そのため「どの矩形がどの色で、どの順に並ぶか」をウィンドウ無しで検証できる<br/>
/// （XAML の要素はウィンドウ無しで生成できないため、要素の生成を検証の対象にしない）。
/// </remarks>
public readonly record struct DrawRect(EsRect Bounds, DrawingColor Color);
