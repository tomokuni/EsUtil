namespace EsUtil.Platform.Core.Palette;

/// <summary>色の並び替えモード。</summary>
/// <remarks>ドロップダウンの選択肢の並びを決める。 並びの計算は <see cref="ColorEntry"/> が単一所有する。</remarks>
public enum ColorSortMode
{
    /// <summary>色名（英名）のアルファベット順。</summary>
    ByName,

    /// <summary>色相順（無彩色を明度順で先に、有彩色を虹色順に並べる）。</summary>
    ByHue,
}
