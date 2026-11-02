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

    /// <summary>影の色が明暗どちらの地色でも見える 50% の灰色であることを検証する。</summary>
    /// <remarks>
    /// 黒の影は暗色テーマの地色に埋もれて見えない（実測 2026-11-02: コントラスト 1.05）。<br/>
    /// 50% の灰色は地色へ合成されるため明暗で同じ程度になる（1.21 / 1.27）。
    /// </remarks>
    [Fact]
    public void 影の色は明暗どちらでも見える灰色である()
    {
        var color = ThumbnailShadowPlan.Default.ColorArgb;

        Assert.Equal(0xC0808080u, color);

        // RGB が 3 成分とも同じ（無彩色）であること（有彩色だと地色によって色味が変わる）
        Assert.Equal((color >> 16) & 0xFF, (color >> 8) & 0xFF);
        Assert.Equal((color >> 8) & 0xFF, color & 0xFF);
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
    /// 帯の形は寸法と倍率だけで決まる。 **位置だけが変わり、形と濃さは変わらない**ことを固定する
    /// （描画結果が変わらないための条件）。 端数のある位置（切り捨てが効く場合）と倍率が 1 でない場合も含める。
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

    /// <summary>寸法が違えば別の帯になることを検証する。</summary>
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

    /// <summary>選択中だけ影を隠すことを検証する（ホバーでは隠さない）。</summary>
    [Fact]
    public void 選択時に影を隠す設定は選択中だけ影を除く()
    {
        var presenter = new ThumbnailPresenter(appearance: ThumbnailAppearance.Default with { HideShadowWhenSelected = true });
        var layout = presenter.ResolveLayout(300, 96, [96, 96]);

        // ホバー中でも選択していなければ影を描く（通り過ぎただけで影が消えるとちらつく）
        var hovered = presenter.ResolveDrawPlan(layout, 0, "a", isCurrent: false, selectedIndex: -1, hoveredIndex: 0);
        Assert.True(hovered.IsHovered);
        Assert.NotNull(hovered.Shadow);
        Assert.Contains(ThumbnailDrawLayer.Shadow, hovered.Layers);

        var selected = presenter.ResolveDrawPlan(layout, 0, "a", isCurrent: false, selectedIndex: 0, hoveredIndex: -1);
        Assert.True(selected.IsSelected);
        Assert.Null(selected.Shadow);
        Assert.DoesNotContain(ThumbnailDrawLayer.Shadow, selected.Layers);
    }

    /// <summary>外観から決まる寸法を求めることを検証する。</summary>
    /// <remarks>
    /// 影の余白と名前の行の高さは利用者の設定ではない（設定に混ぜると「設定したのに効かない項目」が生まれる）。
    /// 外観の影と文字サイズから求めて配置の計算へ渡す。 名前の行の高さは上下の余白を含む
    /// （含めないと余白を増やした分だけ文字を置ける高さが減り、2 行目が切れる）。
    /// </remarks>
    [Fact]
    internal void ResolveMetrics_影の余白と名前の行の高さを外観から求める()
    {
        var presenter = new ThumbnailPresenter(
            appearance: ThumbnailAppearance.Default with { LabelFontSize = 11, LabelVerticalPadding = 3 });

        var metrics = presenter.ResolveMetrics();

        Assert.Equal(10, metrics.SlotInset, 6);
        Assert.Equal(
            ThumbnailLabelText.ResolveHeight(11, ThumbnailLabelText.MaxLines) + 6, metrics.LabelHeight, 6);
    }

    /// <summary>影を描かない外観では余白が 0 になることを検証する。</summary>
    [Fact]
    internal void ResolveMetrics_影が無ければ余白は0になる()
    {
        var presenter = new ThumbnailPresenter(
            appearance: ThumbnailAppearance.Default with { Shadow = null });

        Assert.Equal(0, presenter.ResolveMetrics().SlotInset, 6);
    }

    /// <summary>影の表示を止めた外観では余白が 0 になり、計画にも影が載らないことを検証する。</summary>
    /// <remarks>表示可否（<c>ShowShadow</c>）と算法（<c>Shadow</c>）を分けているため、算法を保ったまま止められる。</remarks>
    [Fact]
    internal void ShowShadowを止めると余白も影も無くなる()
    {
        var presenter = new ThumbnailPresenter(
            appearance: ThumbnailAppearance.Default with { ShowShadow = false });
        var layout = presenter.ResolveLayout(600, 110, [90]);

        var plan = presenter.ResolveDrawPlan(layout, 0, "a", false, -1, -1);

        Assert.Equal(0, presenter.ResolveMetrics().SlotInset, 6);
        Assert.Null(plan.Shadow);
        Assert.DoesNotContain(ThumbnailDrawLayer.Shadow, plan.Layers);
        Assert.Equal(plan.Image, plan.ShadowArea);
    }
    /// <summary>選択の背景がホバーより優先されることを検証する。</summary>
    [Fact]
    public void 選択の背景はホバーより優先される()
    {
        var presenter = new ThumbnailPresenter();
        var layout = presenter.ResolveLayout(300, 96, [96]);
        var plan = presenter.ResolveDrawPlan(layout, 0, "a", isCurrent: false, selectedIndex: 0, hoveredIndex: 0);

        Assert.True(plan.IsSelected);
        Assert.True(plan.IsHovered);
        Assert.Equal(ThumbnailHighlightKind.Selected, plan.Highlight);
    }

    /// <summary>現在表示中の画像の名前が太字になることを検証する。</summary>
    [Fact]
    public void 現在表示中の画像の名前は太字になる()
    {
        var presenter = new ThumbnailPresenter();
        var layout = presenter.ResolveLayout(300, 96, [96]);
        var plan = presenter.ResolveDrawPlan(layout, 0, "a", isCurrent: true, selectedIndex: -1, hoveredIndex: -1);

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

        var normal = presenter.ResolveDrawPlan(layout, 0, "a", false, -1, -1);
        var hovered = presenter.ResolveDrawPlan(layout, 0, "a", false, -1, 0);

        Assert.Equal(0u, normal.Label.ForegroundArgb);
        Assert.Equal(0xFF0078D4u, hovered.Label.ForegroundArgb);
    }

    /// <summary>描画計画の矩形が配置と一致することを検証する。</summary>
    [Fact]
    public void 描画計画の矩形は配置と一致する()
    {
        var presenter = new ThumbnailPresenter();
        var layout = presenter.ResolveLayout(300, 96, [96, 40]);
        var plan = presenter.ResolveDrawPlan(layout, 1, "b", false, -1, -1);

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

        var plan = presenter.ResolveDrawPlan(layout, 0, "a", false, -1, -1);
        Assert.Same(wide, plan.Shadow);
        Assert.Equal(layout.Image(0).X - 40, plan.ShadowArea.X, 6);
        Assert.Equal(ThumbnailHighlightKind.None, plan.Highlight);
        Assert.Contains(ThumbnailDrawLayer.Shadow, plan.Layers);
    }

    /// <summary>影を描かない外観で層と影の矩形が縮むことを検証する。</summary>
    [Fact]
    public void 影なしの外観では影の層と余白を持たない()
    {
        var presenter = new ThumbnailPresenter(appearance: ThumbnailAppearance.Default with { Shadow = null });
        var layout = presenter.ResolveLayout(300, 96, [96]);

        // 影を描かない設定では余白を持たない（項目の幅 = 画像の幅）
        Assert.Equal(96, layout.SlotWidth);

        var plan = presenter.ResolveDrawPlan(layout, 0, "a", false, -1, -1);
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

        var plan = presenter.ResolveDrawPlan(layout, 0, "a", false, -1, 0);

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

    /// <summary>2 行に収まらない名前は拡張子を残して省略することを検証する。</summary>
    /// <remarks>
    /// 名前を置ける幅は行の幅から左右の余白を除いた値である（余白を引かないと、実際には折り返される文字数より
    /// 多く残してしまい、省略されずに切れる）。
    /// </remarks>
    [Fact]
    public void 名前が2行に収まらない場合は拡張子を残して省略する()
    {
        var presenter = new ThumbnailPresenter(
            options: ThumbnailOptions.Default with { LabelHorizontalPadding = 24 },
            appearance: ThumbnailAppearance.Default with { LabelFontSize = 24 });
        var layout = presenter.ResolveLayout(600, 110, [90]);

        var plan = presenter.ResolveDrawPlan(layout, 0, "capture-0123456789.png", false, -1, -1);

        Assert.Contains("...", plan.Label.Text, StringComparison.Ordinal);
        Assert.EndsWith(".png", plan.Label.Text, StringComparison.Ordinal);
    }

    /// <summary>太字の項目は幅を広く見積もって、より短く省略することを検証する。</summary>
    [Fact]
    public void 太字の項目はより短く省略する()
    {
        var presenter = new ThumbnailPresenter(
            options: ThumbnailOptions.Default with { LabelHorizontalPadding = 24 },
            appearance: ThumbnailAppearance.Default with { LabelFontSize = 24, BoldCurrentLabel = true });
        var layout = presenter.ResolveLayout(600, 110, [90]);

        var normal = presenter.ResolveDrawPlan(layout, 0, "capture-0123456789.png", false, -1, -1);
        var bold = presenter.ResolveDrawPlan(layout, 0, "capture-0123456789.png", true, -1, -1);

        Assert.True(bold.Label.Text.Length <= normal.Label.Text.Length);
        Assert.True(bold.Label.IsBold);
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
            ThumbnailTextMeasure? measureText = null)
        {
            var plan = base.ResolveDrawPlan(
                layout, index, text, isCurrent, selectedIndex, hoveredIndex, measureText);

            return plan with
            {
                Shadow = null,
                Layers = [ThumbnailDrawLayer.Background, ThumbnailDrawLayer.Image, ThumbnailDrawLayer.Label],
            };
        }
    }
}
