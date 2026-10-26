using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Feature.Thumbnail.Tests;

/// <summary>影の計画・描画計画（状態と層）の単体テスト。</summary>
public class ThumbnailPresenterTests
{
    /// <summary>影の濃さが実測値と一致することを検証する。</summary>
    [Fact]
    public void 影の濃さは実測値と一致する()
    {
        var shadow = ThumbnailShadowPlan.Default;

        Assert.Equal(10, shadow.BlurRadius);
        Assert.Equal(0.245, shadow.AlphaAtDistance(0.5), 6);
        Assert.Equal(0.245, shadow.AlphaAtDistance(1), 6);
        Assert.Equal(0, shadow.AlphaAtDistance(11), 6);
        Assert.Equal(10, shadow.Insets.Left);
    }

    /// <summary>影の帯が画像の周囲へ作られることを検証する。</summary>
    [Fact]
    public void 影の帯は画像の周囲へ作られる()
    {
        var bands = ThumbnailShadowPlan.Default.BuildDeviceBands(20, 20, 96, 48, scaling: 1);

        Assert.NotEmpty(bands);
        Assert.All(bands, band => Assert.NotEqual(0, band.Width));
        Assert.All(bands, band => Assert.NotEqual(0, band.Height));

        // 画像の外側（上辺）へも帯が伸びる
        Assert.Contains(bands, band => band.Y < 20);
    }

    /// <summary>同じ寸法の帯は、位置を変えても同じ形（平行移動）になることを検証する。</summary>
    /// <remarks>
    /// 帯の形は寸法と倍率だけで決まるため実装は形を覚えて平行移動する。 覚えた形を返すようになっても
    /// **位置だけが変わり、形と濃さは変わらない**ことを固定する（描画結果が変わらないための条件）。
    /// 端数のある位置（切り捨てが効く場合）と倍率が 1 でない場合も含める。
    /// </remarks>
    [Fact]
    public void 影の帯は位置を変えても同じ形になる()
    {
        var shadow = ThumbnailShadowPlan.Default;

        foreach (var (x, y, scaling) in new[] { (20.0, 20.0, 1.0), (20.5, 20.7, 1.0), (0.0, 0.0, 1.5), (120.3, 64.9, 2.0) })
        {
            var first = shadow.BuildDeviceBands(x, y, 96, 48, scaling);
            var moved = shadow.BuildDeviceBands(x + 300, y + 200, 96, 48, scaling);

            Assert.Equal(first.Count, moved.Count);
            var dx = 300 * scaling;
            var dy = 200 * scaling;
            for (var i = 0; i < first.Count; i++)
            {
                Assert.Equal(first[i].Width, moved[i].Width);
                Assert.Equal(first[i].Height, moved[i].Height);
                Assert.Equal(first[i].Argb, moved[i].Argb);

                // 位置は画素へ丸めた後に平行移動する（丸めの誤差は 1 画素以内）
                Assert.InRange(moved[i].X - first[i].X, dx - 1, dx + 1);
                Assert.InRange(moved[i].Y - first[i].Y, dy - 1, dy + 1);
            }
        }
    }

    /// <summary>帯の形を覚えても、寸法が違えば別の形を返すことを検証する。</summary>
    [Fact]
    public void 寸法が違えば別の帯になる()
    {
        var shadow = ThumbnailShadowPlan.Default;

        var small = shadow.BuildDeviceBands(0, 0, 48, 24, scaling: 1);
        var large = shadow.BuildDeviceBands(0, 0, 192, 96, scaling: 1);

        // 帯の本数は広がりだけで決まる（大きさでは変わらない）。 変わるのは辺の長さと位置である
        Assert.Equal(small.Count, large.Count);
        Assert.Equal(48, small.First(band => band.Y == 0 && band.Height == 1).Width);
        Assert.Equal(192, large.First(band => band.Y == 0 && band.Height == 1).Width);
    }

    /// <summary>強調中に影を隠す外観で層から影が除かれることを検証する。</summary>
    [Fact]
    public void 影を描かない場合は層から影が除かれる()
    {
        var presenter = new ThumbnailPresenter(appearance: ThumbnailAppearance.Default with { HideShadowWhenHighlighted = true });
        var layout = presenter.ResolveLayout(300, 96, [96, 96]);
        var plan = presenter.ResolveDrawPlan(layout, 0, "a", isCurrent: false, selectedIndex: -1, hoveredIndex: 0, isDark: false);

        Assert.True(plan.IsHovered);
        Assert.Null(plan.Shadow);
        Assert.DoesNotContain(ThumbnailDrawLayer.Shadow, plan.Layers);
        Assert.Equal(ThumbnailAppearance.Default.HoverBackground(false), plan.BackgroundArgb);
    }

    /// <summary>選択の背景がホバーより優先されることを検証する。</summary>
    [Fact]
    public void 選択の背景はホバーより優先される()
    {
        var presenter = new ThumbnailPresenter();
        var layout = presenter.ResolveLayout(300, 96, [96]);
        var plan = presenter.ResolveDrawPlan(layout, 0, "a", isCurrent: false, selectedIndex: 0, hoveredIndex: 0, isDark: false);

        Assert.True(plan.IsSelected);
        Assert.True(plan.IsHovered);
        Assert.Equal(ThumbnailAppearance.Default.SelectionBackground(false), plan.BackgroundArgb);
    }

    /// <summary>現在表示中の画像の名前が太字になることを検証する。</summary>
    [Fact]
    public void 現在表示中の画像の名前は太字になる()
    {
        var presenter = new ThumbnailPresenter();
        var layout = presenter.ResolveLayout(300, 96, [96]);
        var plan = presenter.ResolveDrawPlan(layout, 0, "a", isCurrent: true, selectedIndex: -1, hoveredIndex: -1, isDark: false);

        Assert.True(plan.Label.IsBold);
        Assert.Equal(ThumbnailAppearance.Default.LabelFontSize, plan.Label.FontSize);
    }

    /// <summary>名前の色が強調中だけ指定されることを検証する。</summary>
    [Fact]
    public void 名前の色は強調中だけ指定される()
    {
        var appearance = ThumbnailAppearance.Default with { HighlightedLabelArgb = 0xFF0078D4 };
        var presenter = new ThumbnailPresenter(appearance: appearance);
        var layout = presenter.ResolveLayout(300, 96, [96]);

        var normal = presenter.ResolveDrawPlan(layout, 0, "a", false, -1, -1, false);
        var hovered = presenter.ResolveDrawPlan(layout, 0, "a", false, -1, 0, false);

        Assert.Equal(0u, normal.Label.ForegroundArgb);
        Assert.Equal(0xFF0078D4u, hovered.Label.ForegroundArgb);
    }

    /// <summary>描画計画の矩形が配置と一致することを検証する。</summary>
    [Fact]
    public void 描画計画の矩形は配置と一致する()
    {
        var presenter = new ThumbnailPresenter();
        var layout = presenter.ResolveLayout(300, 96, [96, 40]);
        var plan = presenter.ResolveDrawPlan(layout, 1, "b", false, -1, -1, false);

        Assert.Equal(layout.Slot(1), plan.Slot);
        Assert.Equal(layout.Image(1), plan.Image);
        Assert.Equal(layout.Label(1), plan.Label.Bounds);
    }

    /// <summary>影の算法を差し替えてもレイアウトの余白が追従することを検証する。</summary>
    /// <remarks>
    /// 影の広がり（<see cref="IThumbnailShadowPlan.Insets"/>）が設定の余白より広い場合、項目の外枠が<br/>
    /// 広がる（影が切れない）。 逆に広がりが小さい場合は設定の余白を保つ。
    /// </remarks>
    [Fact]
    public void 影の算法の余白がレイアウトへ反映される()
    {
        var wide = new WideShadowPlan();
        var presenter = new ThumbnailPresenter(appearance: ThumbnailAppearance.Default with { Shadow = wide });
        var layout = presenter.ResolveLayout(300, 96, [96]);

        // 外枠の幅 = 画像 + 影の余白（片側 40）
        Assert.Equal(96, layout.ImageWidth);
        Assert.Equal(96 + (40 * 2), layout.SlotWidth);

        var plan = presenter.ResolveDrawPlan(layout, 0, "a", false, -1, -1, false);
        Assert.Same(wide, plan.Shadow);
        Assert.Equal(layout.Image(0).X - 40, plan.ShadowArea.X, 6);
        Assert.Equal(0u, plan.BackgroundArgb);
        Assert.Contains(ThumbnailDrawLayer.Shadow, plan.Layers);
    }

    /// <summary>影を描かない外観で層と影の矩形が縮むことを検証する。</summary>
    [Fact]
    public void 影なしの外観では影の層と余白を持たない()
    {
        var presenter = new ThumbnailPresenter(appearance: ThumbnailAppearance.Default with { Shadow = null });
        var layout = presenter.ResolveLayout(300, 96, [96]);

        // 既定の余白（ItemMargin + ItemPadding = 14）だけが残る
        Assert.Equal(96 + (14 * 2), layout.SlotWidth);

        var plan = presenter.ResolveDrawPlan(layout, 0, "a", false, -1, -1, false);
        Assert.Null(plan.Shadow);
        Assert.Equal(plan.Image, plan.ShadowArea);
        Assert.DoesNotContain(ThumbnailDrawLayer.Shadow, plan.Layers);
    }

    /// <summary>描画計画が派生クラスから差し替えられることを検証する。</summary>
    /// <remarks>
    /// 計画（<see cref="ThumbnailDrawPlan"/>）は公開の契約であるため、ホストは影を外す・層を変えるといった<br/>
    /// 独自の提示を <see cref="ThumbnailPresenter.ResolveDrawPlan"/> の上書きで提供できる。
    /// </remarks>
    [Fact]
    public void 描画計画は派生クラスで差し替えられる()
    {
        var presenter = new OutlineOnlyPresenter();
        var layout = presenter.ResolveLayout(300, 96, [96]);

        var plan = presenter.ResolveDrawPlan(layout, 0, "a", false, -1, 0, false);

        Assert.Null(plan.Shadow);
        Assert.DoesNotContain(ThumbnailDrawLayer.Shadow, plan.Layers);
        Assert.Equal(
            [ThumbnailDrawLayer.Background, ThumbnailDrawLayer.Image, ThumbnailDrawLayer.Label], plan.Layers);
    }

    /// <summary>余白の広い影の算法（差し替えの例）。</summary>
    /// <remarks>既定と違う点は余白（<see cref="Insets"/>）だけで、帯は既定の計画から借りる。</remarks>
    private sealed class WideShadowPlan : IThumbnailShadowPlan
    {
        /// <inheritdoc/>
        public double BlurRadius => ThumbnailShadowPlan.Default.BlurRadius;

        /// <inheritdoc/>
        public uint ColorArgb => ThumbnailShadowPlan.Default.ColorArgb;

        /// <inheritdoc/>
        public EsThickness Insets { get; } = new(40, 40, 40, 40);

        /// <inheritdoc/>
        public IReadOnlyList<ThumbnailShadowEdge> Edges => ThumbnailShadowPlan.Default.Edges;

        /// <inheritdoc/>
        public IReadOnlyList<ThumbnailShadowStop> CornerStops => ThumbnailShadowPlan.Default.CornerStops;

        /// <inheritdoc/>
        public IReadOnlyList<ThumbnailShadowBand> BuildDeviceBands(
            double x, double y, double width, double height, double scaling)
            => ThumbnailShadowPlan.Default.BuildDeviceBands(x, y, width, height, scaling);
    }

    /// <summary>影を外した計画を返す提示（差し替えの例）。</summary>
    private sealed class OutlineOnlyPresenter() : ThumbnailPresenter
    {
        /// <inheritdoc/>
        public override ThumbnailDrawPlan ResolveDrawPlan(
            ThumbnailLayoutResult layout,
            int index,
            string text,
            bool isCurrent,
            int selectedIndex,
            int hoveredIndex,
            bool isDark)
        {
            var plan = base.ResolveDrawPlan(layout, index, text, isCurrent, selectedIndex, hoveredIndex, isDark);

            return plan with
            {
                Shadow = null,
                Layers = [ThumbnailDrawLayer.Background, ThumbnailDrawLayer.Image, ThumbnailDrawLayer.Label],
            };
        }
    }
}
