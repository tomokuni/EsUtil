using EsUtil.Platform.Core.Geometry;

// 複製元: EsUtil.Ui.MewUI.Marquee.Internal（独立パッケージ用の意図的な複製。 .github/依存関係.md §4.1）
namespace EsUtil.Ui.WinForms.Marquee.Internal;

/// <summary>コア幾何型と GDI+ の座標型を相互変換する。</summary>
/// <remarks>
/// コアロジック（<see cref="EsPoint"/> 等）は UI フレームワークに依存せず、UI 境界でのみ本クラスで変換する。<br/>
/// 変換は拡張メソッドとして提供し、呼び出し側は <c>using</c> 1 行のみで利用できる。<br/>
/// <b>精度</b>: GDI+ の幾何型（<see cref="PointF"/> / <see cref="SizeF"/> / <see cref="RectangleF"/>）は<b>単精度</b>であるため、<br/>
/// コア型（倍精度）の値を往復させると単精度へ丸められる（整数値と 0.5 刻みの値は損失しない）。<br/>
/// 描画は単精度で行われるため、往復を避けたい場合は UI 側でコア型のまま計算する。
/// </remarks>
internal static class EsGeometryWinFormsInterop
{
    /// <summary><see cref="EsPoint"/> を GDI+ の座標型へ変換する。</summary>
    /// <param name="point">変換元の座標。</param>
    /// <returns>GDI+ の座標（単精度へ丸められる）。</returns>
    public static PointF ToPointF(this EsPoint point) => new((float)point.X, (float)point.Y);

    /// <summary><see cref="EsPoint"/> を GDI+ の整数座標型へ変換する。</summary>
    /// <param name="point">変換元の座標。</param>
    /// <returns>四捨五入した整数座標。</returns>
    /// <remarks>ウィンドウ座標（<c>PointToClient</c> / <c>PointToScreen</c>）へコア型を渡すために使う（丸め規則を本メソッドが単一所有する）。</remarks>
    public static Point ToPoint(this EsPoint point) => new(RoundToPixel(point.X), RoundToPixel(point.Y));

    /// <summary>画面物理ピクセルの座標を GDI+ の整数座標型へ変換する。</summary>
    /// <param name="point">画面物理ピクセルの座標。</param>
    /// <returns>GDI+ の整数座標。</returns>
    /// <remarks>ウィンドウ座標（<c>PointToClient</c> / <c>PointToScreen</c>）へコア型を渡すために使う。</remarks>
    public static Point ToPoint(this EsPointInt point) => new(point.X, point.Y);

    /// <summary>GDI+ の座標型を <see cref="EsPoint"/> へ変換する。</summary>
    /// <param name="point">変換元の座標。</param>
    /// <returns>コアの座標。</returns>
    public static EsPoint ToEsPoint(this PointF point) => new(point.X, point.Y);

    /// <summary>GDI+ の整数座標型を <see cref="EsPoint"/> へ変換する。</summary>
    /// <param name="point">変換元の座標。</param>
    /// <returns>コアの座標。</returns>
    public static EsPoint ToEsPoint(this Point point) => new(point.X, point.Y);

    /// <summary>GDI+ の整数座標型を画面物理ピクセルの座標（整数）へ変換する。</summary>
    /// <param name="point">変換元の座標。</param>
    /// <returns>画面物理ピクセルの座標。</returns>
    public static EsPointInt ToEsPointInt(this Point point) => new(point.X, point.Y);

    /// <summary>GDI+ の座標型を画面物理ピクセルの座標（整数）へ変換する。</summary>
    /// <param name="point">変換元の座標（画面物理の小数値）。</param>
    /// <returns>四捨五入した画面物理ピクセルの座標。</returns>
    /// <remarks>丸め規則を本メソッドが単一所有し、UI 側に <c>(int)Math.Round(...)</c> を書かせない。</remarks>
    public static EsPointInt ToEsPointInt(this PointF point)
        => new(RoundToPixel(point.X), RoundToPixel(point.Y));

    /// <summary>小数の座標を画素（整数）へ丸める。</summary>
    /// <param name="value">丸める値。</param>
    /// <returns>四捨五入した値（0.5 は 0 から遠い側へ丸める）。</returns>
    /// <remarks>
    /// <see cref="Math.Round(double)"/> の既定は最近接偶数への丸め（銀行家の丸め）であり「四捨五入」ではないため、<br/>
    /// <see cref="MidpointRounding.AwayFromZero"/> を明示する（規則を 1 箇所に閉じる）。
    /// </remarks>
    private static int RoundToPixel(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    /// <summary><see cref="EsSize"/> を GDI+ のサイズ型へ変換する。</summary>
    /// <param name="size">変換元のサイズ。</param>
    /// <returns>GDI+ のサイズ（単精度へ丸められる）。</returns>
    public static SizeF ToSizeF(this EsSize size) => new((float)size.Width, (float)size.Height);

    /// <summary>GDI+ のサイズ型を <see cref="EsSize"/> へ変換する。</summary>
    /// <param name="size">変換元のサイズ。</param>
    /// <returns>コアのサイズ。</returns>
    public static EsSize ToEsSize(this SizeF size) => new(size.Width, size.Height);

    /// <summary><see cref="EsVector"/> を GDI+ の座標型へ変換する。</summary>
    /// <param name="vector">変換元のベクター。</param>
    /// <returns>GDI+ の座標（単精度へ丸められる）。</returns>
    public static PointF ToPointF(this EsVector vector) => new((float)vector.X, (float)vector.Y);

    /// <summary>GDI+ の座標型を <see cref="EsVector"/> へ変換する。</summary>
    /// <param name="vector">変換元のベクター。</param>
    /// <returns>コアのベクター。</returns>
    public static EsVector ToEsVector(this PointF vector) => new(vector.X, vector.Y);

    /// <summary><see cref="EsRect"/> を GDI+ の矩形型へ変換する。</summary>
    /// <param name="rect">変換元の矩形。</param>
    /// <returns>GDI+ の矩形（単精度へ丸められる）。</returns>
    public static RectangleF ToRectangleF(this EsRect rect)
        => new((float)rect.X, (float)rect.Y, (float)rect.Width, (float)rect.Height);

    /// <summary>GDI+ の矩形型を <see cref="EsRect"/> へ変換する。</summary>
    /// <param name="rect">変換元の矩形。</param>
    /// <returns>コアの矩形。</returns>
    public static EsRect ToEsRect(this RectangleF rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
