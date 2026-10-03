using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using EsUtil.Feature.Marquee.Presenter.Drawing;

namespace EsUtil.Feature.Marquee.Tests;

/// <summary><see cref="MarqueePresenter.ResolveDrawPlan"/> が返す描画計画のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 描画計画は「UI が見る唯一の描画値」であり、幾何（コア）と外観・層の順序・装飾矩形・ラベル配置を解決済みで持つ。<br/>
/// 画素への反映は MewUI の描画実装テストが担うため、本テストは値（層・矩形・有無）だけを検証する。
/// </remarks>
public class MarqueeDrawPlanTests
{
    /// <summary>提示層を生成する（テスト用の効果スパイとカーソルなし）。</summary>
    /// <param name="appearance">外観。 null の場合は既定。</param>
    /// <param name="options">設定。 null の場合は既定。</param>
    /// <returns>提示層。</returns>
    private static MarqueePresenter CreatePresenter(MarqueeAppearance? appearance = null, MarqueeOptions? options = null)
    {
        var presenter = new MarqueePresenter(new EffectsSpy(), cursor: null, options) { Log = NullLogger.Instance };

        if (appearance is not null)
        {
            presenter.Appearance = appearance;
        }

        return presenter;
    }

    /// <summary>実寸を固定で返す測定関数。</summary>
    /// <param name="text">測定する文言。</param>
    /// <param name="fontSize">フォントサイズ。</param>
    /// <returns>固定の実寸。</returns>
    private static EsSize MeasureText(string text, double fontSize) => new(40.0, 14.0);

    /// <summary>描画計画を解決する（描画領域 200×100）。</summary>
    /// <param name="appearance">外観。 null の場合は既定。</param>
    /// <param name="options">設定。 null の場合は既定。</param>
    /// <returns>描画計画。</returns>
    private static MarqueeDrawPlan ResolvePlan(MarqueeAppearance? appearance = null, MarqueeOptions? options = null)
        => CreatePresenter(appearance, options).ResolveDrawPlan(new EsSize(200, 100), MeasureText);

    [Fact]
    public void ResolveDrawPlan_外観をそのまま保持する()
    {
        var appearance = MarqueeAppearance.Default with { ShowOuterBorder = false, OuterBorderThickness = 3.0 };

        var plan = ResolvePlan(appearance);

        Assert.Equal(appearance, plan.Appearance);
    }

    [Fact]
    public void ResolveDrawPlan_描画面を解決する()
    {
        var plan = ResolvePlan();

        Assert.Equal(new EsRect(0, 0, 200, 100), plan.ClientRect);
    }

    [Fact]
    public void ResolveDrawPlan_注目矩形が無い場合は装飾もハンドルも持たない()
    {
        // 座標ラベルはカーソル位置を基準にするため、注目矩形が無くても表示できる（外観で無効化して素の状態を見る）
        var plan = ResolvePlan(MarqueeAppearance.Default with { ShowCoordLabel = false });

        Assert.Null(plan.FocusDipRect);
        Assert.Null(plan.Handles);
        Assert.Null(plan.Marker);
        Assert.Null(plan.InnerFillDipRect);
        Assert.Empty(plan.OverlayDipRects);
        Assert.Empty(plan.OuterBorderDipRects);
        Assert.Empty(plan.InnerBorderDipRects);
        Assert.False(plan.HasDecoration);
        Assert.False(plan.HasText);
    }

    [Fact]
    public void ResolveDrawPlan_選択がある場合は装飾とハンドルを解決する()
    {
        var presenter = CreatePresenter(MarqueeAppearance.Default with { ShowCoordLabel = false });
        presenter.SetSelection(new EsRect(10, 20, 100, 50, EsRectInterval.Closed));

        var plan = presenter.ResolveDrawPlan(new EsSize(200, 100), MeasureText);

        Assert.Equal(new EsRect(10, 20, 101, 51), plan.FocusDipRect);
        Assert.NotNull(plan.Handles);
        Assert.Equal(8, plan.Handles!.Value.HandleDipRects.Count);
        // 暗転は注目矩形を除いた 4 領域、枠線は 4 辺
        Assert.Equal(4, plan.OverlayDipRects.Count);
        Assert.Equal(4, plan.OuterBorderDipRects.Count);
        Assert.True(plan.HasDecoration);
    }

    [Fact]
    public void ResolveDrawPlan_内側の塗りと枠線は表示可否に従う()
    {
        var appearance = MarqueeAppearance.Default with
        {
            ShowCoordLabel = false,
            ShowInnerFill = true,
            ShowInnerBorder = true,
        };
        var presenter = CreatePresenter(appearance);
        presenter.SetSelection(new EsRect(10, 20, 100, 50, EsRectInterval.Closed));

        var plan = presenter.ResolveDrawPlan(new EsSize(200, 100), MeasureText);

        // 内側の塗りは注目矩形そのもの、内側の枠線は 4 辺
        Assert.Equal(new EsRect(10, 20, 101, 51), plan.InnerFillDipRect);
        Assert.Equal(4, plan.InnerBorderDipRects.Count);
    }

    [Fact]
    public void ResolveDrawPlan_全面暗転は注目矩形が無くても暗転領域を持つ()
    {
        var appearance = MarqueeAppearance.Default with { ShowCoordLabel = false, ShowFullOverlay = true };

        var plan = ResolvePlan(appearance);

        // 注目矩形が無い全面暗転では、描画面の全体が 1 領域になる
        var region = Assert.Single(plan.OverlayDipRects);
        Assert.Equal(new EsRect(0, 0, 200, 100), region);
        Assert.True(plan.HasDecoration);
    }

    [Fact]
    public void ResolveDrawPlan_層の順序は装飾ハンドルマーカーテキストの順になる()
    {
        var presenter = CreatePresenter();
        presenter.Press(new EsPoint(10, 10), clickCount: 1);
        presenter.Move(new EsPoint(50, 40));
        presenter.KeyDown(EsKey.Right, cursorPosition: null);   // nudge 中（マーカー可視）

        var plan = presenter.ResolveDrawPlan(new EsSize(200, 100), MeasureText);

        Assert.Equal(
            [MarqueeDrawLayer.Decoration, MarqueeDrawLayer.Handles, MarqueeDrawLayer.Marker, MarqueeDrawLayer.Label],
            plan.Layers);
    }

    [Fact]
    public void ResolveDrawPlan_描く値の無い層は含めない()
    {
        var appearance = MarqueeAppearance.Default with { ShowCoordLabel = false };

        var plan = ResolvePlan(appearance);

        // 暗転も枠線も描かない設定では、描く値が無いため層も空になる
        var none = ResolvePlan(appearance with { ShowOverlay = false, ShowOuterBorder = false });
        Assert.Empty(none.Layers);

        // 既定（暗転と枠線を描く）でも注目矩形が無いため装飾の層は含まれない
        Assert.DoesNotContain(MarqueeDrawLayer.Decoration, plan.Layers);
    }

    [Fact]
    public void ResolveDrawPlan_テキストがある場合は層の最後に置く()
    {
        var presenter = CreatePresenter();
        presenter.SetSelection(new EsRect(10, 20, 100, 50, EsRectInterval.Closed));

        var plan = presenter.ResolveDrawPlan(new EsSize(200, 100), MeasureText);

        // テキストは常に最上位（層の末尾）
        Assert.Equal(MarqueeDrawLayer.Label, plan.Layers[^1]);
        Assert.True(plan.HasText);
    }

    [Fact]
    public void ResolveDrawPlan_ハンドルを隠す外観ではハンドルの層を含めない()
    {
        var appearance = MarqueeAppearance.Default with { ShowHandles = false, ShowCoordLabel = false };
        var presenter = CreatePresenter(appearance);
        presenter.SetSelection(new EsRect(10, 20, 100, 50, EsRectInterval.Closed));

        var plan = presenter.ResolveDrawPlan(new EsSize(200, 100), MeasureText);

        Assert.Null(plan.Handles);
        Assert.DoesNotContain(MarqueeDrawLayer.Handles, plan.Layers);
    }

    [Fact]
    public void ResolveDrawPlan_座標変換ポートを装飾矩形へ適用する()
    {
        var presenter = CreatePresenter(MarqueeAppearance.Default with { ShowCoordLabel = false });
        // ToDip（対象 → DIP）は 2 倍
        presenter.SetCoordTransform(new CoordTransform(
            p => new EsPoint(p.X / 2, p.Y / 2), p => new EsPoint(p.X * 2, p.Y * 2), () => null));
        presenter.SetSelection(new EsRect(10, 20, 100, 50, EsRectInterval.Closed));

        var plan = presenter.ResolveDrawPlan(default, MeasureText);

        // 幾何（コア）と同じ変換結果が注目矩形になる
        Assert.Equal(new EsRect(20, 40, 202, 102), plan.FocusDipRect);
        Assert.Equal(4, plan.OuterBorderDipRects.Count);
    }

    [Fact]
    public void ResolveDrawPlan_ラベル配置はラベルが無ければ空になる()
    {
        var appearance = MarqueeAppearance.Default with { ShowSizeLabel = false, ShowCoordLabel = false };

        var plan = ResolvePlan(appearance);

        Assert.Empty(plan.Labels);
        Assert.False(plan.HasText);
    }
}
