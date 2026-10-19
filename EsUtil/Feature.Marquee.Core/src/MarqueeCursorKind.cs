
namespace EsUtil.Feature.Marquee;

/// <summary>操作位置に対応するカーソル形状の種別（UI 非依存）。</summary>
/// <remarks>
/// どの操作位置（ハンドル）が同じカーソル形状になるか（例: 左上と右下は同じ斜めカーソル）は UI に依存しない判断のため、<br/>
/// コアが単一所有する。 各 UI は本種別を自 UI のカーソル型へ写像するだけでよい。<br/>
/// ハンドルから本種別への変換は <see cref="Core.Interaction.MarqueeSession.ToCursorKind"/> が 担う。<br/>
/// <br/>
/// - <see cref="Default"/>: ハンドル外（既定の矢印）<br/>
/// - <see cref="DiagonalNWSE"/>: 左上 / 右下（斜め）<br/>
/// - <see cref="DiagonalNESW"/>: 右上 / 左下（斜め）<br/>
/// - <see cref="Vertical"/>: 上中央 / 下中央（上下）<br/>
/// - <see cref="Horizontal"/>: 中左 / 中右（左右）<br/>
/// - <see cref="Move"/>: 矩形内（移動）
/// </remarks>
public enum MarqueeCursorKind
{
    /// <summary>ハンドル外（既定の矢印）。</summary>
    Default,

    /// <summary>左上 / 右下（斜め）。</summary>
    DiagonalNWSE,

    /// <summary>右上 / 左下（斜め）。</summary>
    DiagonalNESW,

    /// <summary>上中央 / 下中央（上下）。</summary>
    Vertical,

    /// <summary>中左 / 中右（左右）。</summary>
    Horizontal,

    /// <summary>矩形内（移動）。</summary>
    Move,
}
