using Avalonia;
using EsUtil.Platform.Core.Geometry;

// 複製元: EsUtil.SaveEvidence.Platform.AvaloniaUI.Interop（独立パッケージ用の意図的な複製。 .github/依存関係.md §4.1）
namespace EsUtil.Feature.Loupe.AvaloniaUI.Internal;

/// <summary>コア幾何型と Avalonia の座標型を相互変換する。</summary>
/// <remarks>
/// コアロジック（<see cref="EsPoint"/> 等）は UI フレームワークに依存せず、UI 境界でのみ本クラスで変換する。<br/>
/// 変換は拡張メソッドとして提供し、呼び出し側は <c>using EsUtil.SaveEvidence.Platform.AvaloniaUI;</c> のみで利用できる。<br/>
/// </remarks>
internal static class EsGeometryAvaloniaInterop
{
    /// <summary><see cref="EsPoint"/> を Avalonia の座標型へ変換する。</summary>
    /// <param name="point">変換元の座標。</param>
    /// <returns>Avalonia の座標。</returns>
    public static Point ToAvaloniaPoint(this EsPoint point) => new(point.X, point.Y);

    /// <summary>Avalonia の座標型を <see cref="EsPoint"/> へ変換する。</summary>
    /// <param name="point">変換元の座標。</param>
    /// <returns>コアの座標。</returns>
    public static EsPoint ToEsPoint(this Point point) => new(point.X, point.Y);

    /// <summary><see cref="EsSize"/> を Avalonia のサイズ型へ変換する。</summary>
    /// <param name="size">変換元のサイズ。</param>
    /// <returns>Avalonia のサイズ。</returns>
    public static Size ToAvaloniaSize(this EsSize size) => new(size.Width, size.Height);

    /// <summary>Avalonia のサイズ型を <see cref="EsSize"/> へ変換する。</summary>
    /// <param name="size">変換元のサイズ。</param>
    /// <returns>コアのサイズ。</returns>
    public static EsSize ToEsSize(this Size size) => new(size.Width, size.Height);

    /// <summary><see cref="EsVector"/> を Avalonia のベクター型へ変換する。</summary>
    /// <param name="vector">変換元のベクター。</param>
    /// <returns>Avalonia のベクター。</returns>
    public static Vector ToAvaloniaVector(this EsVector vector) => new(vector.X, vector.Y);

    /// <summary>Avalonia のベクター型を <see cref="EsVector"/> へ変換する。</summary>
    /// <param name="vector">変換元のベクター。</param>
    /// <returns>コアのベクター。</returns>
    public static EsVector ToEsVector(this Vector vector) => new(vector.X, vector.Y);

    /// <summary><see cref="EsRect"/> を Avalonia の矩形型へ変換する。</summary>
    /// <param name="rect">変換元の矩形。</param>
    /// <returns>Avalonia の矩形。</returns>
    public static Rect ToAvaloniaRect(this EsRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>Avalonia の矩形型を <see cref="EsRect"/> へ変換する。</summary>
    /// <param name="rect">変換元の矩形。</param>
    /// <returns>コアの矩形。</returns>
    public static EsRect ToEsRect(this Rect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary><see cref="EsPointInt"/> を Avalonia の画面物理座標型へ変換する。</summary>
    /// <param name="point">変換元の画面物理座標。</param>
    /// <returns>Avalonia の画面物理座標。</returns>
    public static PixelPoint ToAvaloniaPixelPoint(this EsPointInt point) => new(point.X, point.Y);

    /// <summary>Avalonia の画面物理座標型を <see cref="EsPointInt"/> へ変換する。</summary>
    /// <param name="point">変換元の画面物理座標。</param>
    /// <returns>コアの画面物理座標。</returns>
    public static EsPointInt ToEsPointInt(this PixelPoint point) => new(point.X, point.Y);

    /// <summary><see cref="EsRectInt"/> を Avalonia の画面物理矩形型へ変換する。</summary>
    /// <param name="rect">変換元の画面物理矩形。</param>
    /// <returns>Avalonia の画面物理矩形。</returns>
    public static PixelRect ToAvaloniaPixelRect(this EsRectInt rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>Avalonia の画面物理矩形型を <see cref="EsRectInt"/> へ変換する。</summary>
    /// <param name="rect">変換元の画面物理矩形。</param>
    /// <returns>コアの画面物理矩形。</returns>
    public static EsRectInt ToEsRectInt(this PixelRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
