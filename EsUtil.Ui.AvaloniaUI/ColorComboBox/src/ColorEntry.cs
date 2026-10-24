using Avalonia.Media;
using CoreColorEntry = EsUtil.Platform.Core.Palette.ColorEntry;

namespace EsUtil.Ui.AvaloniaUI.ColorComboBox;

/// <summary>ドロップダウンの選択肢 1 件（色名と表示用の文字列）。</summary>
/// <param name="Name">色の英語名（例: <c>Red</c>）。</param>
/// <param name="Value">色を表す文字列（色名。 <see cref="ColorString.TryParse(string, out Color, out double)"/> で解釈できる）。</param>
/// <remarks>
/// <b>一覧と並び順の所有者はコアである</b>: 色名の表と並び順（色名順・色相順）は
/// <see cref="CoreColorEntry"/>（<c>EsUtil.Platform.Core.Palette</c>）が単一所有する。
/// 本型はコントロールの <c>ItemsSource</c> へ渡すための表示用の入れ物であり、並べ替えを持たない。
/// </remarks>
internal sealed record ColorEntry(string Name, string Value)
{
    /// <summary>表示に使う名前（英語名）。</summary>
    /// <returns>色名。</returns>
    public override string ToString() => Name;

    /// <summary>色名順の全色。</summary>
    public static IReadOnlyList<ColorEntry> All { get; } = Convert(CoreColorEntry.All);

    /// <summary>色相順の全色。</summary>
    public static IReadOnlyList<ColorEntry> AllByHue { get; } = Convert(CoreColorEntry.AllByHue);

    /// <summary>コアの一覧を表示用の一覧へ写す。</summary>
    /// <param name="source">コアの一覧。</param>
    /// <returns>表示用の一覧。</returns>
    private static IReadOnlyList<ColorEntry> Convert(IReadOnlyList<CoreColorEntry> source)
        => [.. source.Select(entry => new ColorEntry(entry.Name, entry.Name))];
}