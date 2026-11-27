using EsUtil.Platform.Core.Palette;
using EsUtil.Ui.MewUI.ColorSelector;

namespace EsUtil.Ui.MewUI.StyledText;

/// <summary>書式バーの構成（フォントの一覧・色のタブ・出す項目）。</summary>
/// <remarks>
/// <b>ホストが自分の色の並びを渡せる</b>: 代表色やカラーピッカーの有無はアプリごとに違うため、
/// 本クラスで受け取る（バーが特定のアプリの色の一覧を持たない）。
/// </remarks>
public sealed record StyledTextFormatBarOptions
{
    /// <summary>書式バーに出す項目の組み合わせを取得または設定する。</summary>
    /// <value>既定はすべての項目を出す（<see cref="TextFormatBarItemVisibility.Default"/>）。</value>
    /// <remarks>すべて出さない場合は書式バー自体を表示しない（判定は <see cref="TextFormatBarVisibility.ShouldShow"/> が持つ）。</remarks>
    public TextFormatBarItemVisibility Items { get; init; } = TextFormatBarItemVisibility.Default;

    /// <summary>選べるフォント名の一覧を取得または設定する。</summary>
    /// <value>既定は代表的なフォント（<see cref="StyledTextFonts.All"/>）。</value>
    public IReadOnlyList<string> Fonts { get; init; } = StyledTextFonts.All;

    /// <summary>色の入力を作る処理を取得または設定する。</summary>
    /// <value>現在の色（<c>#AARRGGBB</c>）と表記を受け取る処理。 設定しない場合は標準のタブだけの色選択を作る。</value>
    /// <remarks>代表色のタブなどを出す場合は、構成した色の選択を返す（構成はホストが決める）。</remarks>
    public Func<string, ColorTextFormat, ColorSelectorControl>? CreateColorInput { get; init; }

    /// <summary>色の入力欄へ出す表記を取得または設定する。</summary>
    public ColorTextFormat TextFormat { get; init; } = ColorTextFormat.NameWithAlpha;
}