using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Loupe.MewUI.Sample.Views;

/// <summary>画像上のマウス位置の通知（<see cref="LoupeHostPanel"/>）。</summary>
internal sealed class PointerMovedEventArgs(EsPoint position) : EventArgs
{
    /// <summary>通知されたマウス位置（パネル内の DIP 座標 = 元画像ピクセル座標）。</summary>
    internal EsPoint Position { get; } = position;
}
