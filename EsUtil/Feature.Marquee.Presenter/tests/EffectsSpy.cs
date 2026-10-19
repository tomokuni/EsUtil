using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Marquee.Tests;

/// <summary>テストで使用する効果（フォーカス・再描画・カーソル形状・キャプチャ・座標変換）のスパイ。</summary>
/// <remarks>
/// 提示層のテストは描画計画の値のみを検証するため、効果は再描画の回数を数える以外は何もしない最小実装とする。
/// </remarks>
internal sealed class EffectsSpy : IMarqueeEffects
{
    /// <summary>再描画の要求回数。</summary>
    public int RedrawCount { get; private set; }

    /// <inheritdoc/>
    public void RequestFocus() { }

    /// <inheritdoc/>
    public void RequestRedraw() => RedrawCount++;

    /// <inheritdoc/>
    public void SetCursorShape(MarqueeCursorKind kind) { }

    /// <inheritdoc/>
    public void SetPointerCapture(bool capture) { }

    /// <inheritdoc/>
    public EsPointInt ToScreenPoint(EsPoint sourcePoint) => default;
}
