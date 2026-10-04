using EsUtil.Platform.Core.Geometry;
using Xunit;
using EsUtil.Feature.Marquee.Core.Interaction;

namespace EsUtil.Feature.Marquee.Tests.Core.Interaction;

/// <summary><see cref="MarqueeFeedback"/> の単体テスト（UI 層へ要求する効果の適用）。</summary>
/// <remarks>
/// 効果の選択・適用順はコアが単一所有するため、UI 実装に依存せずここで検証する。<br/>
/// 適用先は <see cref="IMarqueeEffects"/> のスパイ（<see cref="EffectsSpy"/>）とカーソルサービスのスパイに記録し、呼び出し順を検証する。
/// </remarks>
public class MarqueeFeedbackTests
{
    [Fact]
    internal void ApplyCursor_移動してから表示を切り替える()
    {
        var cursor = new CursorServiceSpy();
        // 3 つの操作をまとめて要求する（nudge の終了と同じ組み合わせ）
        var feedback = new MarqueeFeedback
        {
            Cursor = MarqueeCursorActions.MoveTo | MarqueeCursorActions.Hide | MarqueeCursorActions.Show,
            CursorSourcePosition = new EsPoint(10, 20),
        };

        feedback.ApplyCursor(cursor, new EffectsSpy());

        // 適用順は「移動 → 非表示 → 再表示」（画面座標への変換は効果が担う）
        Assert.Equal(["move:20,40", "show:False", "show:True"], cursor.Calls);
    }

    [Fact]
    internal void ApplyCursor_操作が無ければ何もしない()
    {
        var cursor = new CursorServiceSpy();
        var feedback = new MarqueeFeedback();

        feedback.ApplyCursor(cursor, new EffectsSpy());

        Assert.Empty(cursor.Calls);
    }

    [Fact]
    internal void ApplyVisual_フォーカスしてからカーソル形状と再描画を適用する()
    {
        var feedback = new MarqueeFeedback
        {
            ShouldRequestFocus = true,
            ShouldUpdateCursorShape = true,
            CursorKind = MarqueeCursorKind.DiagonalNWSE,
            ShouldRedraw = true,
        };
        var effects = new EffectsSpy();

        feedback.ApplyVisual(effects);

        Assert.Equal(["focus", "cursor:DiagonalNWSE", "redraw"], effects.Calls);
    }

    [Fact]
    internal void ApplyVisual_要求されていない効果は適用しない()
    {
        var feedback = new MarqueeFeedback { ShouldRedraw = true };
        var effects = new EffectsSpy();

        feedback.ApplyVisual(effects);

        Assert.Equal(["redraw"], effects.Calls);
    }

    [Fact]
    internal void ApplyVisual_効果が無ければ何も適用しない()
    {
        var feedback = new MarqueeFeedback();
        var effects = new EffectsSpy();

        feedback.ApplyVisual(effects);

        Assert.Empty(effects.Calls);
    }

    [Fact]
    internal void ApplyVisual_カーソル形状の種別を渡す()
    {
        var effects = new EffectsSpy();
        // 各ハンドル種別がそのまま渡ることを確認する（写像は UI 層の責務）
        foreach (var kind in new[]
                 {
                     MarqueeCursorKind.Default, MarqueeCursorKind.DiagonalNWSE, MarqueeCursorKind.DiagonalNESW,
                     MarqueeCursorKind.Vertical, MarqueeCursorKind.Horizontal, MarqueeCursorKind.Move,
                 })
        {
            new MarqueeFeedback { ShouldUpdateCursorShape = true, CursorKind = kind }.ApplyVisual(effects);
        }

        Assert.Equal(
            [
                "cursor:Default", "cursor:DiagonalNWSE", "cursor:DiagonalNESW",
                "cursor:Vertical", "cursor:Horizontal", "cursor:Move",
            ],
            effects.Calls);
    }
}
