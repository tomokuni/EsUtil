using Avalonia.Media;

namespace EsUtil.Feature.Loupe.AvaloniaUI.Sample.ViewModels;

/// <summary>色選択コンボボックス用の選択肢。</summary>
/// <param name="Name">表示名。</param>
/// <param name="Brush">対応するブラシ（null は「なし」= 描画しない）。</param>
public sealed record BrushOption(string Name, IBrush? Brush)
{
    /// <summary>選択肢の色を取得する。</summary>
    /// <value>単色ブラシの色。 「なし」（null）・単色以外のブラシの場合は null。</value>
    /// <remarks>ルーペの外観は UI 非依存の色型で渡すため、ブラシから色を取り出す。</remarks>
    public Color? Color => (Brush as ISolidColorBrush)?.Color;

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>色選択肢の一覧を提供する。</summary>
public static class BrushOptions
{
    /// <summary>「なし」選択肢（null ブラシ）。</summary>
    public static BrushOption None { get; } = new("なし", null);

    /// <summary>赤。</summary>
    public static BrushOption Red { get; } = new("赤", Brushes.Red);

    /// <summary>緑。</summary>
    public static BrushOption Green { get; } = new("緑", Brushes.Green);

    /// <summary>青。</summary>
    public static BrushOption Blue { get; } = new("青", Brushes.Blue);

    /// <summary>黄。</summary>
    public static BrushOption Yellow { get; } = new("黄", Brushes.Yellow);

    /// <summary>シアン。</summary>
    public static BrushOption Cyan { get; } = new("シアン", Brushes.Cyan);

    /// <summary>マゼンタ。</summary>
    public static BrushOption Magenta { get; } = new("マゼンタ", Brushes.Magenta);

    /// <summary>白。</summary>
    public static BrushOption White { get; } = new("白", Brushes.White);

    /// <summary>黒。</summary>
    public static BrushOption Black { get; } = new("黒", Brushes.Black);

    /// <summary>グレー。</summary>
    public static BrushOption Gray { get; } = new("グレー", Brushes.Gray);

    /// <summary>薄グレー。</summary>
    public static BrushOption LightGray { get; } = new("薄グレー", Brushes.LightGray);

    /// <summary>オレンジ。</summary>
    public static BrushOption Orange { get; } = new("オレンジ", Brushes.Orange);

    /// <summary>画像外（ペーストボード）の既定色（#CCCCCC。 従来の市松模様 B と同じ濃さ）。</summary>
    public static BrushOption Pasteboard { get; } = new("#CCCCCC", new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)));

    /// <summary>全選択肢。</summary>
    public static IReadOnlyList<BrushOption> All { get; } =
        [None, Red, Green, Blue, Yellow, Cyan, Magenta, White, Black, Gray, LightGray, Orange, Pasteboard];
}
