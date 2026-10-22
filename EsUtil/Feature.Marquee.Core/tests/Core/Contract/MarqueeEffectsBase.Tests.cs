using EsUtil.Feature.Marquee.Core.Contract;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Feature.Marquee.Tests.Core.Contract;

/// <summary><see cref="MarqueeEffectsBase"/> の単体テスト（効果の既定実装）。</summary>
/// <remarks>
/// 独自のコントロールへ組み込むときの入力を減らすための基底クラスであり、<br/>
/// (1) 全メンバーが何もせず既定値を返すこと、(2) 必要な効果だけを override すれば<br/>
/// ファサードが使えること（＝簡易に利用できる最小構成）を検証する。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public class MarqueeEffectsBaseTests
{
    [Fact]
    internal void 既定実装はすべて何もしない()
    {
        MarqueeEffectsBase effects = new RedrawOnly(() => { });

        // 例外を投げず、画面座標の変換は既定値を返す
        effects.RequestFocus();
        effects.RequestRedraw();
        effects.SetCursorShape(MarqueeCursorKind.DiagonalNWSE);
        effects.SetPointerCapture(false);
        Assert.Equal(default, effects.ToScreenPoint(new EsPoint(10, 20)));
    }

    [Fact]
    internal void 再描画だけをoverrideした実装でファサードを使える()
    {
        // 独自コントロールの最小構成（再描画だけを実装し、他の効果は基底の既定実装を使う）
        var redraws = 0;
        var marquee = new MarqueeCore(new RedrawOnly(() => redraws++));

        marquee.Press(new EsPoint(10, 10), clickCount: 1);
        marquee.Move(new EsPoint(60, 40));
        marquee.Release(new EsPoint(60, 40));

        // 入力のたびにコアが再描画を要求する（基底クラスが他の効果の実装を要求しない）
        Assert.True(redraws > 0, "再描画が要求されていない");
        Assert.True(marquee.HasSelection, "選択が確定していない");
    }

    [Fact]
    internal void 効果を実装しない場合はカーソル操作も例外にならない()
    {
        // カーソルサービスを渡した場合でも、画面座標の変換は基底の既定値で完結する（例外を投げない）
        var marquee = new MarqueeCore(new RedrawOnly(() => { }), new CursorServiceSpy());

        marquee.KeyDown(EsKey.Right, new EsPoint(10, 10));

        Assert.True(marquee.CreateSnapshot().IsNudgeActive, "nudge が開始していない");
    }

    /// <summary>再描画だけを実装した最小の効果（独自コントロールの例）。</summary>
    /// <param name="requestRedraw">再描画の要求を受け取る操作。</param>
    private sealed class RedrawOnly(Action requestRedraw) : MarqueeEffectsBase
    {
        /// <summary>再描画を要求する。</summary>
        public override void RequestRedraw() => requestRedraw();
    }
}
