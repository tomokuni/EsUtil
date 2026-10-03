using Avalonia.Media;

namespace EsUtil.Feature.Marquee.AvaloniaUI.Sample.ViewModels;

/// <summary>色選択コンボボックス用の選択肢。</summary>
/// <param name="Name">表示名。</param>
/// <param name="Color">対応する色。</param>
public sealed record ColorOption(string Name, Color Color)
{
    /// <inheritdoc />
    public override string ToString() => Name;
}
