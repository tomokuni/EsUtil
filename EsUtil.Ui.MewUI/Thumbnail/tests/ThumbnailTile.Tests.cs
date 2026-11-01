using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Ui.MewUI.Thumbnail.Tests;

/// <summary>サムネイル 1 件分の要素（MewUI 版）の表示反映の単体テスト。</summary>
/// <remarks>
/// 描画計画の文言・ツールヒントが要素へ反映されることを検証する（ウィンドウ表示を必要としない）。
/// </remarks>
public class ThumbnailTileTests
{
    /// <summary>描画計画を適用すると名前行の文言が計画の値になることを検証する。</summary>
    /// <remarks>2 行に収まらない名前は提示層が省略済みの文言を返すため、要素はその文言をそのまま表示する。</remarks>
    [Fact]
    internal void Apply_名前行の文言を計画の値へ置き換える()
    {
        var tile = new ThumbnailTile(null, "capture-0123456789.png", null, null, isCurrent: false);

        tile.Apply(CreatePlan("captu....png"), ThumbnailOptions.Default);

        Assert.Equal("captu....png", tile.LabelText);
    }

    /// <summary>ツールチップに省略していない名前が入ることを検証する。</summary>
    [Fact]
    internal void ツールチップに省略していない名前を設定する()
    {
        var tile = new ThumbnailTile(null, "capture-0123456789.png", null, null, isCurrent: false);

        var tip = Assert.IsType<Aprillz.MewUI.Controls.TextBlock>(tile.ToolTip);

        Assert.Equal("capture-0123456789.png", tip.Text);
    }

    /// <summary>付加情報がある場合はツールチップへ併記することを検証する。</summary>
    [Fact]
    internal void ツールチップに付加情報を併記する()
    {
        var tile = new ThumbnailTile(null, "capture-0001.png", "生成した画像 1", null, isCurrent: false);

        var tip = Assert.IsType<Aprillz.MewUI.Controls.TextBlock>(tile.ToolTip);

        Assert.Contains("生成した画像 1", tip.Text, StringComparison.Ordinal);
    }

    /// <summary>名前行の文言だけを差し替えた描画計画を作る。</summary>
    /// <param name="labelText">名前行の文言。</param>
    /// <returns>描画計画。</returns>
    private static ThumbnailDrawPlan CreatePlan(string labelText)
    {
        var slot = new EsRect(0, 0, 120, 120);
        var image = new EsRect(0, 0, 120, 90);
        var label = new ThumbnailLabelPlan(labelText, new EsRect(0, 90, 120, 30), 11, false, 0, 1);

        return new ThumbnailDrawPlan(0, slot, image, image, label, [], 0, null, false, false, false);
    }
}
