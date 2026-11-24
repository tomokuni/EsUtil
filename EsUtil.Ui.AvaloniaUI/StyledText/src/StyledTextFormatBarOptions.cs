using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Palette;
using EsUtil.Ui.AvaloniaUI.ColorSelector;

namespace EsUtil.Ui.AvaloniaUI.StyledText;

/// <summary>書式バーの構成（フォントの一覧・色のタブ・色の表記）。</summary>
/// <remarks>
/// <b>ホストが自分の色の並びを渡せる</b>: 代表色やカラーピッカーの有無はアプリごとに違うため、
/// 本クラスで受け取る（バーが特定のアプリの色の一覧を持たない）。
/// </remarks>
public sealed class StyledTextFormatBarOptions
{
    /// <summary>選べるフォント名の一覧を取得または設定する。</summary>
    /// <value>既定は環境のフォント（<see cref="StyledTextFonts.All"/>）。</value>
    public IReadOnlyList<string> Fonts { get; init; } = StyledTextFonts.All;

    /// <summary>代表色の列数を取得または設定する（■ を何列で並べるか）。</summary>
    public int PaletteColumns { get; init; } = 8;

    /// <summary>色の選択を構成する処理を取得または設定する。</summary>
    /// <value>色の選択と列数を受け取る処理。 設定しない場合は色の選択を既定の構成のまま使う。</value>
    public Action<ColorSelectorControl, int>? ConfigureColorSelector { get; init; }

    /// <summary>文字の実寸を測る関数を取得または設定する。</summary>
    /// <value>色の入力欄の幅を決めるのに使う。 設定しない場合は描画と同じエンジンで測る。</value>
    public Func<string, TextStyle, EsSize>? MeasureText { get; init; }

    /// <summary>色の入力欄へ出す表記を取得または設定する。</summary>
    public ColorTextFormat TextFormat { get; init; } = ColorTextFormat.NameWithAlpha;
}