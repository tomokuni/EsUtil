using System.Drawing.Drawing2D;

namespace EsUtil.Ui.WinForms.Interop.Imaging;

/// <summary>GDI+ の描画面へ画像を 1 回の描画で敷く描画層。</summary>
/// <remarks>
/// <b>適用パターン</b>: 描画層（コントロールから描画手順を分離する）。<br/>
/// コントロールの <c>OnPaint</c> から呼ぶ描画手順をコントロール内の private メソッドに置くと、<br/>
/// コントロール（ウィンドウ）を必要とするため画素を検証できない。 本クラスへ分離すると<br/>
/// <c>Bitmap</c> と <c>Graphics.FromImage</c> だけで検証できる。<br/>
/// <br/>
/// <b>手順</b>:<br/>
/// 1. 画像が null の場合は描画しない。<br/>
/// 2. 配置の矩形（<see cref="EsImageDrawMath"/>）が空の場合は描画しない。<br/>
/// 3. 矩形へ画像を 1 回の <c>DrawImage</c> で描く。<br/>
/// <br/>
/// <b>背景を塗らない理由</b>: GDI+ の描画面はコントロールの下地と共有される（別のキャンバスを持たない）。<br/>
/// 余白の下地は呼び出し側（コントロール）が塗る（透明の画像を敷くだけでは下地が消えない）。<br/>
/// <b>画像を持たない理由</b>: コアの画像（<see cref="EsUtil.Platform.Core.Imaging.EsReadOnlyBitmap"/>）から GDI+ のビットマップへの<br/>
/// 変換は全面の複製であり高価であるため、変換と保持（キャッシュ）は呼び出し側が担い、本クラスは描くだけにする。
/// </remarks>
public static class ImageRenderer
{
    /// <summary>画像を描画先の領域へ配置して描画する。</summary>
    /// <param name="graphics">描画先。 null 不可。</param>
    /// <param name="targetSize">描画先の領域のサイズ（物理ピクセル）。</param>
    /// <param name="source">描画する画像。 null の場合は何も描画しない。</param>
    /// <param name="mode">配置方法（縦横比を保つ / 領域全体へ引き伸ばす）。</param>
    /// <remarks>前提条件の判定（未設定・配置が空）をすべて本メソッドが担うため、呼び出し側は判定を持たない。</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="graphics"/> が null の場合。</exception>
    public static void Render(Graphics graphics, Size targetSize, Bitmap? source, EsImageDrawMode mode)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        if (source is null)
        {
            return;
        }

        var destination = EsImageDrawMath.ComputeDestination(
            source.Width, source.Height, targetSize.Width, targetSize.Height, mode);

        DrawScaled(graphics, source, destination);
    }

    /// <summary>画像を指定の矩形へ描画する（拡大・縮小の設定を単一所有する入口）。</summary>
    /// <param name="graphics">描画先。 null 不可。</param>
    /// <param name="source">描画する画像。 null 不可。</param>
    /// <param name="destination">描画先の矩形。 幅・高さが 0 以下の場合は何も描画しない。</param>
    /// <remarks>
    /// 拡大・縮小は最近傍で行う（等倍の画素を保ったまま敷く）。 <c>PixelOffsetMode.Half</c> は<br/>
    /// 「描画先の画素の中心が元画像の画素の中心に重なる」配置を指示する設定であり、指定しないと<br/>
    /// 元画像の外側を透明として拾い、縁の 1 画素が薄くなる（実測: 4x2 → 8x8 で縁のアルファが 32 になる）。<br/>
    /// 描画先の設定（補間・画素のオフセット・スムージング）は<b>本メソッドが呼び出しの前後で保存・復帰する</b><br/>
    /// （呼び出し側が他の描画と混在させても設定が漏れない）。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="graphics"/> または <paramref name="source"/> が null の場合。</exception>
    public static void DrawScaled(Graphics graphics, Bitmap source, EsRect destination)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(source);

        if (destination.IsEmpty)
        {
            return;
        }

        var state = graphics.Save();
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.SmoothingMode = SmoothingMode.None;
        try
        {
            graphics.DrawImage(source, destination.ToRectangleF());
        }
        finally
        {
            graphics.Restore(state);
        }
    }
}
