using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Ui.WinUI3.Interop;
using Microsoft.UI.Xaml;

namespace EsUtil.Ui.WinUI3.Marquee.Internal;

/// <summary>コントロール ローカル DIP と画面物理座標の変換を提供するヘルパー。</summary>
/// <remarks>
/// nudge のカーソル移動等の用途で、コントロールが属するウィンドウの表示倍率と画面上位置から変換する。<br/>
/// ウィンドウの特定は XAML のルート（<c>XamlRoot</c>）を境界で 1 度だけ行い、コントロール本体は本ヘルパーへ委譲する。<br/>
/// ビジュアル ツリー未接続（<c>XamlRoot</c> が無い・ウィンドウを特定できない）ときは等価変換する<br/>
/// （画面座標をそのまま扱う。 例外にしない規則は [`例外処理規約.md`] §3）。<br/>
/// <br/>
/// OS 依存の画面物理変換（ウィンドウの位置 / <c>ScreenToClient</c> / <c>ClientToScreen</c>）を集約する唯一の場所である。<br/>
/// <b>座標変換ポートは提供しない</b>: ポートの軸は「ローカル DIP ↔ 対象座標」であり、画面物理はその軸に含まれない（別の軸）。<br/>
/// 向きが名前で分かる <see cref="LocalDipToScreen"/> / <see cref="TryScreenToLocalDip"/> だけを提供する。
/// </remarks>
internal static class ScreenConverter
{
    /// <summary>コントロール ローカル DIP を画面物理座標に変換する。</summary>
    /// <param name="source">変換元のコントロール。</param>
    /// <param name="local">コントロール ローカル DIP 座標。</param>
    /// <returns>画面物理座標。 ウィンドウを特定できない場合は <paramref name="local"/> を丸めた座標（等価変換）。</returns>
    /// <remarks>
    /// ローカル DIP → コンテンツ ルートの DIP（<c>TransformToVisual(null)</c>）→ クライアントの物理ピクセル（表示倍率を掛ける）<br/>
    /// → 画面物理座標（<c>ClientToScreen</c>）の順に変換する（境界の 1 行変換）。
    /// </remarks>
    public static EsPointInt LocalDipToScreen(UIElement source, EsPoint local)
    {
        if (!TryGetWindowContext(source, out var windowHandle, out var scaling))
        {
            return ToScreenPoint(local);
        }

        var root = source.TransformToVisual(null).TransformPoint(local.ToWinUIPoint());
        return Win32Interop.ClientToScreen(windowHandle, ToScreenPoint(new EsPoint(root.X * scaling, root.Y * scaling)));
    }

    /// <summary>画面物理座標をコントロール ローカル DIP に変換する。</summary>
    /// <param name="source">変換元のコントロール。</param>
    /// <param name="screen">画面物理座標。</param>
    /// <param name="local">変換したコントロール ローカル DIP 座標。 変換できない場合は既定値。</param>
    /// <returns>変換できた場合は true。 ウィンドウを特定できない場合と、逆変換できない場合は false。</returns>
    /// <remarks>
    /// 画面物理座標 → クライアントの物理ピクセル（<c>ScreenToClient</c>）→ コンテンツ ルートの DIP（表示倍率で割る）<br/>
    /// → ローカル DIP（<c>TransformToVisual(null)</c> の逆変換）の順に変換する。<br/>
    /// 逆変換できない状態（行列が特異・要素が未接続）は例外にせず false を返す（[`例外処理規約.md`] §3）。
    /// </remarks>
    public static bool TryScreenToLocalDip(UIElement source, EsPointInt screen, out EsPoint local)
    {
        local = default;

        if (!TryGetWindowContext(source, out var windowHandle, out var scaling))
        {
            return false;
        }

        var client = Win32Interop.ScreenToClient(windowHandle, screen);
        var root = new EsPoint(client.X / scaling, client.Y / scaling);
        if (source.TransformToVisual(null).Inverse is not { } inverse)
        {
            return false;
        }

        local = inverse.TransformPoint(root.ToWinUIPoint()).ToEsPoint();
        return true;
    }

    /// <summary>変換に必要なウィンドウ ハンドルと表示倍率を解決する。</summary>
    /// <param name="source">変換元のコントロール。</param>
    /// <param name="windowHandle">解決したウィンドウ ハンドル。</param>
    /// <param name="scaling">解決した表示倍率（物理ピクセル / DIP）。</param>
    /// <returns>両方を解決できた場合は true。</returns>
    /// <remarks>
    /// 表示倍率は XAML のルートが報告する値（<c>RasterizationScale</c>）を使う（描画の倍率とは独立に、ウィンドウの座標変換の倍率として使う）。
    /// </remarks>
    private static bool TryGetWindowContext(UIElement source, out IntPtr windowHandle, out double scaling)
    {
        windowHandle = IntPtr.Zero;
        scaling = DpiScaler.StandardScaling;

        if (source.XamlRoot is not { } xamlRoot)
        {
            return false;
        }

        scaling = DpiScaler.NormalizeScaling(xamlRoot.RasterizationScale);
        return Win32Interop.TryGetWindowHandle(xamlRoot.ContentIslandEnvironment.AppWindowId, out windowHandle);
    }

    /// <summary>DIP 座標を画面物理座標（整数）へ丸める。</summary>
    /// <param name="point">丸める座標。</param>
    /// <returns>四捨五入した座標。</returns>
    /// <remarks>丸め規則を本クラスが単一所有し、呼び出し側へ <c>(int)Math.Round(...)</c> を書かせない。</remarks>
    private static EsPointInt ToScreenPoint(EsPoint point) => new((int)Math.Round(point.X), (int)Math.Round(point.Y));
}
