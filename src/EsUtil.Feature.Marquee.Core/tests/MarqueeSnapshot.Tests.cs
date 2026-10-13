using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Feature.Marquee.Tests;

/// <summary><see cref="MarqueeSnapshot"/> の導出プロパティのテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// ホストが実カーソルから追従位置を取り直してよいかの判断（<see cref="MarqueeSnapshot.IsMarkerFollowingPointer"/>）を検証する。<br/>
/// ドラッグ中は「動いている側のハンドル」、nudge 中は「nudge 位置」が追従位置であり、実カーソルを表さない（取り直してはいけない）。<br/>
/// UI 初期化に依存せず <c>[Theory]</c> で並列実行する。
/// </remarks>
public class MarqueeSnapshotTests
{
    /// <summary>検証に使うスナップショットを生成する（判定に関係しない項目は固定値にする）。</summary>
    /// <param name="state">対話状態。</param>
    /// <param name="isNudgeActive">nudge 中かどうか。</param>
    /// <returns>生成したスナップショット。</returns>
    private static MarqueeSnapshot Create(MarqueeInteractionState state, bool isNudgeActive)
        => new(state, new EsRect(10, 20, 30, 40), MarqueeHitTarget.None, new EsPoint(25, 40), false, isNudgeActive);

    /// <summary>追従位置が実カーソルを表すかどうかが対話状態と nudge から決まることを検証する。</summary>
    /// <param name="state">対話状態。</param>
    /// <param name="isNudgeActive">nudge 中かどうか。</param>
    /// <param name="expected">実カーソルを表すと期待するかどうか。</param>
    [Theory]
    [InlineData(MarqueeInteractionState.NoSelection, false, true)]
    [InlineData(MarqueeInteractionState.Selected, false, true)]
    [InlineData(MarqueeInteractionState.Dragging, false, false)]
    [InlineData(MarqueeInteractionState.Dragging, true, false)]
    [InlineData(MarqueeInteractionState.DraggingNudging, true, false)]
    [InlineData(MarqueeInteractionState.StartPointNudging, true, false)]
    [InlineData(MarqueeInteractionState.SelectedNudging, true, false)]
    internal void IsMarkerFollowingPointer_対話状態とnudgeから決まる(
        MarqueeInteractionState state, bool isNudgeActive, bool expected)
        => Assert.Equal(expected, Create(state, isNudgeActive).IsMarkerFollowingPointer);
}
