using Avalonia;
using Avalonia.Controls;

// 複製元: EsUtil.SaveEvidence.Platform.AvaloniaUI.Coord（独立パッケージ用の意図的な複製。 .github/依存関係.md §4.1）
namespace EsUtil.Feature.Marquee.AvaloniaUI.Internal;

/// <summary>コントロールローカル DIP と画面物理座標の変換を提供するヘルパー。</summary>
/// <remarks>
/// nudge のカーソル移動等の用途で、コントロールが属する <see cref="TopLevel"/> のスケーリングと画面上位置から変換する。<br/>
/// <see cref="TopLevel"/> は境界でのみ解決し、コントロール本体は本ヘルパーへ委譲する。<br/>
/// ビジュアルツリー未接続時は等価変換（画面座標をそのまま使用）する。<br/>
/// <br/>
/// OS 依存の画面物理変換（<see cref="TopLevel"/> の画面上位置 / PointToScreen / PointToClient）を集約する唯一の場所である。<br/>
/// <b>座標変換ポートは提供しない</b>: ポートの軸は「ローカル DIP ↔ 対象座標」であり、画面物理はその軸に含まれない（別の軸）。<br/>
/// 向きが名前で分かる <see cref="LocalDipToScreen"/> / <see cref="ScreenToLocalDip"/> だけを提供する。
/// </remarks>
internal static class ScreenConverter
{
    /// <summary>コントロールローカル DIP を画面物理座標に変換する。</summary>
    /// <param name="source">変換元のコントロール。</param>
    /// <param name="local">コントロールローカル DIP 座標。</param>
    /// <returns>画面物理座標。</returns>
    /// <remarks><c>TopLevel.PointToScreen</c> に委譲する。</remarks>
    public static PixelPoint LocalDipToScreen(Visual source, Point local)
    {
        var topLevel = TopLevel.GetTopLevel(source);
        if (topLevel is null) return new PixelPoint((int)local.X, (int)local.Y);
        var inTopLevel = VisualExtensions.TranslatePoint(source, local, topLevel) ?? local;
        return topLevel.PointToScreen(inTopLevel);
    }

    /// <summary>画面物理座標をコントロールローカル DIP に変換する。</summary>
    /// <param name="source">変換元のコントロール。</param>
    /// <param name="screen">画面物理座標。</param>
    /// <returns>コントロールローカル DIP 座標。</returns>
    /// <remarks>
    /// <c>TopLevel.PointToClient</c> と <see cref="VisualExtensions.TranslatePoint"/> に委譲する。<br/>
    /// ビジュアルツリー未接続時は等価変換（画面座標をそのまま返す）。
    /// </remarks>
    public static Point ScreenToLocalDip(Visual source, PixelPoint screen)
    {
        var topLevel = TopLevel.GetTopLevel(source);
        if (topLevel is null) return new Point(screen.X, screen.Y);
        var clientDip = topLevel.PointToClient(screen);
        var origin = VisualExtensions.TranslatePoint(source, default, topLevel) ?? default;
        return clientDip - origin;
    }
}
