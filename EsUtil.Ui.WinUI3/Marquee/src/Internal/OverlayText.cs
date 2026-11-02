using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinUI3.Marquee.Internal;

/// <summary>ホストが追加した文字（位置・文言・体裁）を表す値。</summary>
/// <param name="Bounds">置く位置（DIP。 幅・高さは文字の実寸に任せるため 0）。</param>
/// <param name="Text">表示する文字列。</param>
/// <param name="FontSize">文字サイズ（DIP）。</param>
/// <param name="Color">文字色。</param>
/// <remarks>
/// <b>描画 API の代わりの値である</b>: 本 UI は XAML の要素として描くため、ホストの追加描画は<br/>
/// 矩形（<c>DrawList</c>）と文字（本値）の 2 種類の値として受け取る。
/// </remarks>
internal readonly record struct OverlayText(EsRect Bounds, string Text, double FontSize, DrawingColor Color);
