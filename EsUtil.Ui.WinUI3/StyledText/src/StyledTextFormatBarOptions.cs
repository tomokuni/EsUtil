using EsUtil.Platform.Core.Palette;
using EsUtil.Ui.WinUI3.ColorSelector;

namespace EsUtil.Ui.WinUI3.StyledText;

/// <summary>書式バーの構成（フォントの一覧・色の選択）。</summary>
/// <remarks>
/// <b>ホストが自分の色の並びを渡せる</b>: 代表色やカラーピッカーの有無はアプリごとに違うため、
/// 本クラスで受け取る（バーが特定のアプリの色の一覧を持たない）。
/// </remarks>
public sealed class StyledTextFormatBarOptions
{
    /// <summary>選べるフォント名の一覧を取得または設定する。</summary>
    /// <value>既定は代表的なフォント（<see cref="StyledTextFonts.All"/>）。</value>
    public IReadOnlyList<string> Fonts { get; init; } = StyledTextFonts.All;

    /// <summary>色の選択を作る処理を取得または設定する。</summary>
    /// <value>現在の色と表記を受け取る処理。 設定しない場合は標準のタブだけの色選択を使う。</value>
    public Func<string, ColorTextFormat, ColorSelectorControl>? CreateColorSelector { get; init; }

    /// <summary>色の入力欄へ出す表記を取得または設定する。</summary>
    public ColorTextFormat TextFormat { get; init; } = ColorTextFormat.NameWithAlpha;
}