using EsUtil.Platform.Core.Geometry;
using MewUIColor = Aprillz.MewUI.Color;
using MewUIPoint = Aprillz.MewUI.Point;
using MewUIRect = Aprillz.MewUI.Rect;
using MewUISize = Aprillz.MewUI.Size;
using MewUIVector = Aprillz.MewUI.Vector;

// 複製元: 製品共通の MewUI 変換ヘルパー（独立パッケージ用の意図的な複製。 .github/依存関係.md §4.1）
namespace EsUtil.Feature.Marquee.MewUI.Internal;

/// <summary>コア幾何型と MewUI の座標型を相互変換する。</summary>
/// <remarks>
/// コアロジック（<see cref="EsPoint"/> 等）は UI フレームワークに依存せず、UI 境界でのみ本クラスで変換する。<br/>
/// 変換は拡張メソッドとして提供し、呼び出し側は <c>using</c> 1 行のみで利用できる。<br/>
/// MewUI の幾何型（<see cref="MewUIPoint"/> 等）は Avalonia と同じく <c>double</c> ベースの値型のため、値の損失なく往復できる。<br/>
/// <b>例外（負のサイズ）</b>: MewUI の <see cref="MewUIRect"/> / <see cref="MewUISize"/> は<b>負のサイズを 0 にクランプ</b>する<br/>
/// （Avalonia は負の値を保持する）。 空の矩形（負のサイズ）を MewUI へ渡すと 0 サイズになる（座標 X / Y は保持される）。<br/>
/// </remarks>
internal static class EsGeometryMewUIInterop
{
    /// <summary><see cref="EsPoint"/> を MewUI の座標型へ変換する。</summary>
    /// <param name="point">変換元の座標。</param>
    /// <returns>MewUI の座標。</returns>
    public static MewUIPoint ToMewUIPoint(this EsPoint point) => new(point.X, point.Y);

    /// <summary>画面物理ピクセルの座標を MewUI の座標へ変換する。</summary>
    /// <param name="point">画面物理ピクセルの座標。</param>
    /// <returns>MewUI の座標（MewUI は DIP と物理ピクセルを同じ型で表すため、そのまま写すだけになる）。</returns>
    /// <remarks>DPI 照会（<c>DpiHelper</c> 等）へコア型を渡すために使う（Avalonia 版の <c>ToAvaloniaPixelPoint</c> と対称）。</remarks>
    public static MewUIPoint ToMewUIPoint(this EsPointInt point) => new(point.X, point.Y);

    /// <summary>MewUI の座標型を <see cref="EsPoint"/> へ変換する。</summary>
    /// <param name="point">変換元の座標。</param>
    /// <returns>コアの座標。</returns>
    public static EsPoint ToEsPoint(this MewUIPoint point) => new(point.X, point.Y);

    /// <summary>MewUI の座標型を画面物理ピクセルの座標（整数）へ変換する。</summary>
    /// <param name="point">変換元の座標（画面物理の小数値）。</param>
    /// <returns>四捨五入した画面物理ピクセルの座標。</returns>
    /// <remarks>丸め規則を本メソッドが単一所有し、UI 側に <c>(int)Math.Round(...)</c> を書かせない。</remarks>
    public static EsPointInt ToEsPointInt(this MewUIPoint point)
        => new((int)Math.Round(point.X), (int)Math.Round(point.Y));

    /// <summary><see cref="EsSize"/> を MewUI のサイズ型へ変換する。</summary>
    /// <param name="size">変換元のサイズ。</param>
    /// <returns>MewUI のサイズ。</returns>
    public static MewUISize ToMewUISize(this EsSize size) => new(size.Width, size.Height);

    /// <summary>MewUI のサイズ型を <see cref="EsSize"/> へ変換する。</summary>
    /// <param name="size">変換元のサイズ。</param>
    /// <returns>コアのサイズ。</returns>
    public static EsSize ToEsSize(this MewUISize size) => new(size.Width, size.Height);

    /// <summary><see cref="EsVector"/> を MewUI のベクター型へ変換する。</summary>
    /// <param name="vector">変換元のベクター。</param>
    /// <returns>MewUI のベクター。</returns>
    public static MewUIVector ToMewUIVector(this EsVector vector) => new(vector.X, vector.Y);

    /// <summary>MewUI のベクター型を <see cref="EsVector"/> へ変換する。</summary>
    /// <param name="vector">変換元のベクター。</param>
    /// <returns>コアのベクター。</returns>
    public static EsVector ToEsVector(this MewUIVector vector) => new(vector.X, vector.Y);

    /// <summary><see cref="EsRect"/> を MewUI の矩形型へ変換する。</summary>
    /// <param name="rect">変換元の矩形。</param>
    /// <returns>MewUI の矩形。</returns>
    public static MewUIRect ToMewUIRect(this EsRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>MewUI の矩形型を <see cref="EsRect"/> へ変換する。</summary>
    /// <param name="rect">変換元の矩形。</param>
    /// <returns>コアの矩形。</returns>
    public static EsRect ToEsRect(this MewUIRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>コア色型を MewUI の色型へ変換する。</summary>
    /// <param name="color">変換元の色。</param>
    /// <returns>MewUI の色（ARGB はそのまま保持される）。</returns>
    public static MewUIColor ToMewUIColor(this System.Drawing.Color color)
        => MewUIColor.FromArgb(color.A, color.R, color.G, color.B);

    /// <summary>MewUI の色型をコア色型へ変換する。</summary>
    /// <param name="color">変換元の色。</param>
    /// <returns>コアの色（ARGB はそのまま保持される）。</returns>
    public static System.Drawing.Color ToDrawingColor(this MewUIColor color)
        => System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
}
