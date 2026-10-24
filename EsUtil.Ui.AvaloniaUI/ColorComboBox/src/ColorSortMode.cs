using CoreColorSortMode = EsUtil.Platform.Core.Palette.ColorSortMode;

namespace EsUtil.Ui.AvaloniaUI.ColorComboBox;

/// <summary>色の並び替えモード。</summary>
/// <remarks>
/// <b>値はコアと同じ順序である</b>（<see cref="CoreColorSortMode"/> の <c>ByName</c> / <c>ByHue</c> と 1 対 1）。
/// 本 enum は本コントロールの公開面（XAML から指定する値）であり、並びの計算はコアが単一所有する。
/// </remarks>
public enum ColorSortMode
{
    /// <summary>色名（英名）のアルファベット順。</summary>
    ByName = CoreColorSortMode.ByName,

    /// <summary>色相順（無彩色を明度順で先に、有彩色を虹色順に並べる）。</summary>
    ByHue = CoreColorSortMode.ByHue,
}