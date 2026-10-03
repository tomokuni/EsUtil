using Avalonia.Media;

namespace EsUtil.Feature.Marquee.AvaloniaUI.Sample.ViewModels;

/// <summary>色選択肢の一覧を提供する。</summary>
public static class ColorOptions
{
    /// <summary>青。</summary>
    public static ColorOption Blue { get; } = new("青", Colors.Blue);

    /// <summary>赤。</summary>
    public static ColorOption Red { get; } = new("赤", Colors.Red);

    /// <summary>緑。</summary>
    public static ColorOption Green { get; } = new("緑", Colors.Green);

    /// <summary>白。</summary>
    public static ColorOption White { get; } = new("白", Colors.White);

    /// <summary>黒。</summary>
    public static ColorOption Black { get; } = new("黒", Colors.Black);

    /// <summary>黄。</summary>
    public static ColorOption Yellow { get; } = new("黄", Colors.Yellow);

    /// <summary>オレンジ。</summary>
    public static ColorOption Orange { get; } = new("オレンジ", Colors.Orange);

    /// <summary>マゼンタ。</summary>
    public static ColorOption Magenta { get; } = new("マゼンタ", Colors.Magenta);

    /// <summary>シアン。</summary>
    public static ColorOption Cyan { get; } = new("シアン", Colors.Cyan);

    /// <summary>グレー。</summary>
    public static ColorOption Gray { get; } = new("グレー", Colors.Gray);

    /// <summary>全選択肢。</summary>
    public static IReadOnlyList<ColorOption> All { get; } =
        [Blue, Red, Green, White, Black, Yellow, Orange, Magenta, Cyan, Gray];
}
