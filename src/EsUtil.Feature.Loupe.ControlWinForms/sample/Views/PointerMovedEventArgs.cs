using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Loupe.WinForms.Sample.Views;

/// <summary>画像上のマウス位置（元画像ピクセル座標）の通知。</summary>
/// <remarks>
/// <see cref="LoupeHostPanel.PointerMoved"/> の引数。 ルーペの中心の設定はホスト（ウィンドウ）が行う。
/// </remarks>
/// <param name="position">マウス位置（元画像ピクセル座標）。</param>
internal sealed class PointerMovedEventArgs(EsPoint position) : EventArgs
{
    /// <summary>マウス位置（元画像ピクセル座標）を取得する。</summary>
    internal EsPoint Position { get; } = position;
}
