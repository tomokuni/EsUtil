namespace EsUtil.Ui.MewUI.ColorSelector;

/// <summary>利用者が追加するタブの 1 色（表示名と適用する色）。</summary>
/// <param name="Name">一覧に表示する名前。</param>
/// <param name="Value">選んだときに適用する色（色名または <c>#AARRGGBB</c>）。</param>
public sealed record ColorSelectorCustomColor(string Name, string Value);
