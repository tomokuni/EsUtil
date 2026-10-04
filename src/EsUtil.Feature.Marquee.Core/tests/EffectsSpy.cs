using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Marquee.Tests;

/// <summary>テストで使用する効果（フォーカス・再描画・カーソル形状・キャプチャ・座標変換）のスパイ。</summary>
/// <remarks>
/// UI 層の代わりに <see cref="IMarqueeEffects"/> を実装し、呼び出しを発生順に記録する。<br/>
/// 座標変換は固定の倍率（<see cref="ScreenScale"/>）で行い、カーソル移動先の計算を検証できるようにする。
/// </remarks>
internal sealed class EffectsSpy : IMarqueeEffects
{
    /// <summary>呼び出しの履歴（種類と値。 発生順の検証に使用する）。</summary>
    public List<string> Calls { get; } = [];

    /// <summary>再描画の要求回数。</summary>
    public int RedrawCount => Calls.Count(c => c == "redraw");

    /// <summary>対象座標 → 画面物理座標の変換倍率。</summary>
    public int ScreenScale { get; set; } = 2;

    /// <inheritdoc/>
    public void RequestFocus() => Calls.Add("focus");

    /// <inheritdoc/>
    public void RequestRedraw() => Calls.Add("redraw");

    /// <inheritdoc/>
    public void SetCursorShape(MarqueeCursorKind kind) => Calls.Add($"cursor:{kind}");

    /// <inheritdoc/>
    public void SetPointerCapture(bool capture) => Calls.Add($"capture:{capture}");

    /// <inheritdoc/>
    public EsPointInt ToScreenPoint(EsPoint sourcePoint)
        => new((int)sourcePoint.X * ScreenScale, (int)sourcePoint.Y * ScreenScale);
}
