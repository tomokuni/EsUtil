using DrawingRectangle = System.Drawing.Rectangle;
using DrawingRectangleF = System.Drawing.RectangleF;

namespace EsUtil.Ui.WinForms.Loupe.Internal;

/// <summary>コアの幾何型と System.Drawing の幾何型の相互変換。</summary>
/// <remarks>
/// コアの矩形（<see cref="EsRect"/> / <see cref="EsRectInt"/>）を GDI+ の描画 API へ渡すための変換を単一所有する。<br/>
/// <b>なぜ本プロジェクトが持つか</b>: System.Drawing は Windows 専用の外部パッケージであり、<br/>
/// 「外部パッケージへ依存しない」を判定基準とする <c>EsUtil.Platform.Core</c> へは置けないためである。<br/>
/// Base の型（<see cref="DrawingRectangleF"/>）は <see cref="EsRect"/> と同じ「左上 + 幅・高さ」の表現であるため、<br/>
/// 換算は成分の写像だけであり、値の丸めは行わない（丸めは描画 API が行う）。<br/>
/// UI スレッドに依存しないため <c>[Fact]</c> で並列検証できる。
/// </remarks>
internal static class EsGeometryDrawingInterop
{
    /// <summary>コアの矩形を GDI+ の浮動小数の矩形へ変換する。</summary>
    /// <param name="rect">変換元の矩形（コア座標）。</param>
    /// <returns>GDI+ の矩形（左上とサイズで表現する）。</returns>
    /// <remarks>
    /// 負のサイズもそのまま引き継ぐ（<see cref="EsRect"/> は負のサイズを保持できる）。<br/>
    /// 描画位置の端数は丸めない（<c>RectangleF</c> が端数を保持し、描画 API が画素へ割り当てる）。
    /// </remarks>
    public static DrawingRectangleF ToRectangleF(this EsRect rect)
        => new((float)rect.X, (float)rect.Y, (float)rect.Width, (float)rect.Height);

    /// <summary>コアの整数矩形を GDI+ の整数矩形へ変換する。</summary>
    /// <param name="rect">変換元の矩形（コア座標）。</param>
    /// <returns>GDI+ の矩形（左上とサイズで表現する）。</returns>
    public static DrawingRectangle ToRectangle(this EsRectInt rect)
        => new(rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>コアのサイズを GDI+ の浮動小数のサイズへ変換する。</summary>
    /// <param name="size">変換元のサイズ（DIP または物理ピクセル）。</param>
    /// <returns>GDI+ のサイズ。</returns>
    public static SizeF ToSizeF(this EsSize size) => new((float)size.Width, (float)size.Height);
}
