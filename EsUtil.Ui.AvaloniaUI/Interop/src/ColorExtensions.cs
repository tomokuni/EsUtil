using Avalonia.Media;

namespace EsUtil.Ui.AvaloniaUI.Interop;

/// <summary>Avalonia <see cref="Color"/> の生成拡張（C# 14 静的拡張メンバー）。</summary>
/// <remarks>
/// 名前付き色（<c>Colors.Black</c> 等）と不透明度から <see cref="Color"/> を生成する。<br/>
/// <c>Color.FromArgb(128, 0, 0, 0)</c> のような RGB 直指定では何色か判別しにくいため、<br/>
/// 名前付き色を基準に生成する（例: <c>Color.FromAColor(128, Colors.Black)</c> = 黒 50%）。
/// </remarks>
public static class ColorExtensions
{
    extension(Color)
    {
        /// <summary>不透明度を指定して、基準色（名前付き色等）から色を生成する。</summary>
        /// <param name="alpha">不透明度（0〜255。 255 = 完全不透明）。</param>
        /// <param name="baseColor">基準色（例: <see cref="Colors.Black"/>）。 RGB はそのまま、アルファのみ差し替えられる。</param>
        /// <returns>指定の不透明度を適用した色。</returns>
        /// <remarks>
        /// 既存の <c>Color.FromArgb(alpha, r, g, b)</c> では RGB 値の意味が判別しにくいため、<br/>
        /// 名前付き色を基準に生成できるようにする（可読性向上）。<br/>
        /// 例: <c>Color.FromAColor(128, Colors.Black)</c> = 黒 50%（アルファ 128）。
        /// </remarks>
        public static Color FromAColor(byte alpha, Color baseColor)
            => new(alpha, baseColor.R, baseColor.G, baseColor.B);
    }
}
