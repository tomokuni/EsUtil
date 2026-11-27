using EsUtil.Platform.Core.Palette;

namespace EsUtil.Ui.WinForms.StyledText;

/// <summary>書式バーの構成（フォントの一覧・色の選び方・出す項目）。</summary>
/// <remarks>
/// <b>ホストが自分の色の選び方を渡せる</b>: WinForms には色選択コントロールが無いため、色のボタンは
/// ホストが用意する（注釈は代表色のメニュー、他のアプリは色ダイアログなど）。
/// </remarks>
public sealed record StyledTextFormatBarOptions
{
    /// <summary>書式バーに出す項目の組み合わせを取得または設定する。</summary>
    /// <value>既定はすべての項目を出す（<see cref="TextFormatBarItemVisibility.Default"/>）。</value>
    /// <remarks>すべて出さない場合は書式バー自体を表示しない（判定は <see cref="TextFormatBarVisibility.ShouldShow"/> が持つ）。</remarks>
    public TextFormatBarItemVisibility Items { get; init; } = TextFormatBarItemVisibility.Default;

    /// <summary>選べるフォント名の一覧を取得または設定する。</summary>
    /// <value>既定は環境のフォント（<see cref="StyledTextFonts.All"/>）。</value>
    public IReadOnlyList<string> Fonts { get; init; } = StyledTextFonts.All;

    /// <summary>色の入力を作る処理を取得または設定する。</summary>
    /// <value>現在の色と、色が選ばれたときに呼ぶ処理を受け取る処理。 設定しない場合は色ダイアログを使う。</value>
    /// <remarks>WinForms には色選択コントロールが無いため、入力はボタンになる（4 つの UI 版で同じ名前を持つ）。</remarks>
    public Func<Color, Action<Color>, Button>? CreateColorInput { get; init; }

    /// <summary>色の入力欄へ出す表記を取得または設定する。</summary>
    public ColorTextFormat TextFormat { get; init; } = ColorTextFormat.NameWithAlpha;
}