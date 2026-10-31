using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Headless.XUnit;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Ui.AvaloniaUI.Thumbnail.Tests;

/// <summary>サムネイル 1 件分の要素（Avalonia 版）の表示反映の単体テスト（ヘッドレス）。</summary>
/// <remarks>
/// 描画計画の文言・ツールヒントが要素へ反映されることを検証する（ウィンドウ表示を必要としない）。
/// </remarks>
[Collection(AvaloniaHeadlessGroup.Name)]
public class ThumbnailTileTests
{
    /// <summary>描画計画を適用すると名前行の文言が計画の値になることを検証する。</summary>
    /// <remarks>2 行に収まらない名前は提示層が省略済みの文言を返すため、要素はその文言をそのまま表示する。</remarks>
    [AvaloniaFact]
    internal void Apply_名前行の文言を計画の値へ置き換える()
    {
        var tile = new ThumbnailTile(null, "capture-0123456789.png", null, null, isCurrent: false);
        var plan = CreatePlan("captu....png");

        tile.Apply(plan, ThumbnailOptions.Default);

        Assert.Contains("captu....png", TextOf(tile), StringComparison.Ordinal);
    }

    /// <summary>ツールチップに省略していない名前が入ることを検証する。</summary>
    [AvaloniaFact]
    internal void ツールチップに省略していない名前を設定する()
    {
        var tile = new ThumbnailTile(null, "capture-0123456789.png", null, null, isCurrent: false);

        var tip = ToolTip.GetTip(tile) as TextBlock;

        Assert.NotNull(tip);
        Assert.Contains("capture-0123456789.png", tip.Text, StringComparison.Ordinal);
    }

    /// <summary>付加情報がある場合はツールチップへ併記することを検証する。</summary>
    [AvaloniaFact]
    internal void ツールチップに付加情報を併記する()
    {
        var tile = new ThumbnailTile(null, "capture-0001.png", "生成した画像 1", null, isCurrent: false);

        var tip = ToolTip.GetTip(tile) as TextBlock;

        Assert.NotNull(tip);
        Assert.Contains("生成した画像 1", tip.Text, StringComparison.Ordinal);
    }

    /// <summary>要素の中の文字を集める。</summary>
    /// <param name="tile">対象の要素。</param>
    /// <returns>見つかった文字を連結した文字列。</returns>
    private static string TextOf(ThumbnailTile tile)
    {
        var builder = new System.Text.StringBuilder();

        foreach (var text in tile.GetVisualDescendants())
        {
            if (text is TextBlock block && !string.IsNullOrEmpty(block.Text))
            {
                builder.Append(block.Text);
            }
        }

        return builder.ToString();
    }

    /// <summary>名前行の文言だけを差し替えた描画計画を作る。</summary>
    /// <param name="labelText">名前行の文言。</param>
    /// <returns>描画計画。</returns>
    private static ThumbnailDrawPlan CreatePlan(string labelText)
    {
        var slot = new EsRect(0, 0, 120, 120);
        var image = new EsRect(0, 0, 120, 90);
        var label = new ThumbnailLabelPlan(labelText, new EsRect(0, 90, 120, 30), 11, false, 0, 1);

        return new ThumbnailDrawPlan(
            0, slot, image, image, label, [], 0, null, false, false, false);
    }
}
