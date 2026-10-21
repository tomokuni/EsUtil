using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinUI3.Interop.Drawing;

/// <summary>描画する角丸矩形 1 件（塗り色付き）。</summary>
/// <param name="Bounds">矩形（描画先の座標）。</param>
/// <param name="CornerRadius">角の半径（<paramref name="Bounds"/> と同じ単位）。 0 以下の場合は四角形。</param>
/// <param name="Color">塗り色（不透明度はアルファ）。</param>
/// <remarks>
/// 角丸の指定を「描くかどうかの分岐」にしない（半径 0 は四角形として描く）。<br/>
/// これは移行前の <c>SkiaFill.RoundRect</c> と同じ規則であり、移行の前後で見た目が変わらない。
/// </remarks>
public readonly record struct DrawRoundRect(EsRect Bounds, double CornerRadius, DrawingColor Color);
