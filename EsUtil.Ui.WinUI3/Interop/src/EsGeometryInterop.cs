using EsUtil.Platform.Core.Geometry;
using System.Numerics;
using WinUIColor = Windows.UI.Color;
using WinUIPoint = Windows.Foundation.Point;
using WinUIRect = Windows.Foundation.Rect;
using WinUISize = Windows.Foundation.Size;

namespace EsUtil.Ui.WinUI3.Interop;

/// <summary>コア幾何型と WinUI 3 の座標型を相互変換する。</summary>
/// <remarks>
/// コアロジック（<see cref="EsPoint"/> 等）は UI フレームワークに依存せず、UI 境界でのみ本クラスで変換する。<br/>
/// 変換は拡張メソッドとして提供し、呼び出し側は <c>using EsUtil.Ui.WinUI3.Interop;</c> のみで利用できる。<br/>
/// <br/>
/// <b>丸め</b>: WinUI 3 の幾何型（<see cref="WinUIPoint"/> / <see cref="WinUISize"/> / <see cref="WinUIRect"/>）は<br/>
/// 公開プロパティが <c>double</c> だが<b>内部の記憶域は <c>float</c></b> であり、値の変換で丸めが入る<br/>
/// （<see cref="EsPoint"/> は <c>double</c> を保持する。 往復で有効桁が <c>float</c> まで落ちる）。<br/>
/// <br/>
/// <b>例外（負のサイズ）</b>: <see cref="WinUIRect"/> / <see cref="WinUISize"/> のコンストラクターは<br/>
/// <b>負の幅・高さを拒否して例外を投げる</b>（実測。 <see cref="EsRect"/> / <see cref="EsSize"/> は負の値を保持できる）。<br/>
/// そのため本クラスは負のサイズを <b>0 へクランプ</b>する（座標 X / Y は保持される）。<br/>
/// <br/>
/// <b>XAML の値型は使わない</b>: 本クラスは WinRT の投影型（<c>Windows.Foundation</c>）だけを扱う<br/>
/// （色は <see cref="WinUIColor"/>。 XAML のブラシは作らない）。
/// </remarks>
public static class EsGeometryInterop
{
    /// <summary><see cref="EsPoint"/> を WinUI 3 の座標型へ変換する。</summary>
    /// <param name="point">変換元の座標。</param>
    /// <returns>WinUI 3 の座標（<c>float</c> の記憶域へ丸められる）。</returns>
    public static WinUIPoint ToWinUIPoint(this EsPoint point) => new(point.X, point.Y);

    /// <summary>画面物理ピクセルの座標を WinUI 3 の座標へ変換する。</summary>
    /// <param name="point">画面物理ピクセルの座標。</param>
    /// <returns>WinUI 3 の座標（WinUI 3 は DIP と物理ピクセルを同じ型で表すため、そのまま写すだけになる）。</returns>
    /// <remarks>DPI 照会（<c>DisplayArea</c> 等）へコア型を渡すために使う（MewUI 版の <c>ToMewUIPoint</c> と対称）。</remarks>
    public static WinUIPoint ToWinUIPoint(this EsPointInt point) => new(point.X, point.Y);

    /// <summary>WinUI 3 の座標型を <see cref="EsPoint"/> へ変換する。</summary>
    /// <param name="point">変換元の座標。</param>
    /// <returns>コアの座標。</returns>
    public static EsPoint ToEsPoint(this WinUIPoint point) => new(point.X, point.Y);

    /// <summary>WinUI 3 の座標型を画面物理ピクセルの座標（整数）へ変換する。</summary>
    /// <param name="point">変換元の座標（画面物理の小数値）。</param>
    /// <returns>四捨五入した画面物理ピクセルの座標。</returns>
    /// <remarks>丸め規則を本メソッドが単一所有し、UI 側に <c>(int)Math.Round(...)</c> を書かせない。</remarks>
    public static EsPointInt ToEsPointInt(this WinUIPoint point)
        => new((int)Math.Round(point.X), (int)Math.Round(point.Y));

    /// <summary><see cref="EsSize"/> を WinUI 3 のサイズ型へ変換する。</summary>
    /// <param name="size">変換元のサイズ。</param>
    /// <returns>WinUI 3 のサイズ（負の幅・高さは 0 へクランプされる）。</returns>
    public static WinUISize ToWinUISize(this EsSize size)
        => new(Math.Max(0, size.Width), Math.Max(0, size.Height));

    /// <summary>WinUI 3 のサイズ型を <see cref="EsSize"/> へ変換する。</summary>
    /// <param name="size">変換元のサイズ。</param>
    /// <returns>コアのサイズ。</returns>
    public static EsSize ToEsSize(this WinUISize size) => new(size.Width, size.Height);

    /// <summary><see cref="EsVector"/> を WinUI 3 のベクター型へ変換する。</summary>
    /// <param name="vector">変換元のベクター。</param>
    /// <returns>WinUI 3 のベクター（拡大・縮小の変換は <c>float</c> で表現される）。</returns>
    /// <remarks>
    /// WinUI 3 の UI 要素（<c>UIElement</c>）の変換は <see cref="Vector2"/>（<c>System.Numerics</c>）で表されるため、<br/>
    /// 本クラスも同じ型を使う（<see cref="EsVector"/> の <c>double</c> から <c>float</c> へ丸められる）。
    /// </remarks>
    public static Vector2 ToVector2(this EsVector vector) => new((float)vector.X, (float)vector.Y);

    /// <summary>WinUI 3 のベクター型を <see cref="EsVector"/> へ変換する。</summary>
    /// <param name="vector">変換元のベクター。</param>
    /// <returns>コアのベクター。</returns>
    public static EsVector ToEsVector(this Vector2 vector) => new(vector.X, vector.Y);

    /// <summary><see cref="EsRect"/> を WinUI 3 の矩形型へ変換する。</summary>
    /// <param name="rect">変換元の矩形。</param>
    /// <returns>WinUI 3 の矩形（負の幅・高さは 0 へクランプされる）。</returns>
    public static WinUIRect ToWinUIRect(this EsRect rect)
        => new(rect.X, rect.Y, Math.Max(0, rect.Width), Math.Max(0, rect.Height));

    /// <summary>WinUI 3 の矩形型を <see cref="EsRect"/> へ変換する。</summary>
    /// <param name="rect">変換元の矩形。</param>
    /// <returns>コアの矩形。</returns>
    public static EsRect ToEsRect(this WinUIRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>コア色型を WinUI 3 の色型へ変換する。</summary>
    /// <param name="color">変換元の色。</param>
    /// <returns>WinUI 3 の色（ARGB はそのまま保持される）。</returns>
    public static WinUIColor ToWinUIColor(this System.Drawing.Color color)
        => WinUIColor.FromArgb(color.A, color.R, color.G, color.B);

    /// <summary>WinUI 3 の色型をコア色型へ変換する。</summary>
    /// <param name="color">変換元の色。</param>
    /// <returns>コアの色（ARGB はそのまま保持される）。</returns>
    public static System.Drawing.Color ToDrawingColor(this WinUIColor color)
        => System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
}
