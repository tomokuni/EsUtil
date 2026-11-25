using EsUtil.Feature.StyledText.Presenter.Formatting;
using Xunit;

namespace EsUtil.Feature.StyledText.Core.Tests.Presenter.Formatting;

/// <summary>書式バーに出す項目の組み合わせ（並び・幅・全非表示の判定）を検証する。</summary>
public sealed class TextFormatBarItemVisibilityTests
{
    /// <summary>既定はすべての項目を出し、並びはコアの順（装飾 → 色 → 大きさ → フォント名）になる。</summary>
    [Fact]
    internal void VisibleItems_既定はすべての項目を順に返す()
    {
        var visible = TextFormatBarItemVisibility.Default.VisibleItems;

        Assert.Equal(TextFormatBarLayout.Order, visible);
        Assert.True(TextFormatBarItemVisibility.Default.HasAny);
    }

    /// <summary>出さない項目は並びから外れる（場所ごと詰める）。</summary>
    [Fact]
    internal void VisibleItems_出さない項目を含めない()
    {
        var items = TextFormatBarItemVisibility.Default with
        {
            ShowItalic = false,
            ShowStrike = false,
            ShowFont = false,
        };

        Assert.Equal(
            [
                TextFormatBarItem.Bold,
                TextFormatBarItem.Underline,
                TextFormatBarItem.Color,
                TextFormatBarItem.Size,
            ],
            items.VisibleItems);
    }

    /// <summary>すべて出さない場合は項目が無く、表示しないと判定される。</summary>
    [Fact]
    internal void VisibleItems_すべて出さない場合は空になる()
    {
        var items = new TextFormatBarItemVisibility
        {
            ShowBold = false,
            ShowItalic = false,
            ShowUnderline = false,
            ShowStrike = false,
            ShowColor = false,
            ShowFontSize = false,
            ShowFont = false,
        };

        Assert.Empty(items.VisibleItems);
        Assert.False(items.HasAny);
    }

    /// <summary>幅は出す項目だけで決まり、項目を減らすと狭くなる（余分な余白を残さない）。</summary>
    [Fact]
    internal void ResolveWidth_出す項目だけで幅を求める()
    {
        var all = TextFormatBarLayout.ResolveWidth(TextFormatBarItemVisibility.Default, colorSelectorWidth: 100);
        var onlyBold = TextFormatBarLayout.ResolveWidth(
            TextFormatBarItemVisibility.Default with
            {
                ShowItalic = false,
                ShowUnderline = false,
                ShowStrike = false,
                ShowColor = false,
                ShowFontSize = false,
                ShowFont = false,
            },
            colorSelectorWidth: 100);

        Assert.Equal(TextEditorLayout.ToggleWidth, onlyBold);
        Assert.True(all > onlyBold);
    }

    /// <summary>幅の合計は項目の幅と項目間の間隔の合計になる（出さない項目の間隔も残らない）。</summary>
    [Fact]
    internal void ResolveWidth_項目の幅と間隔の合計になる()
    {
        var items = TextFormatBarItemVisibility.Default with
        {
            ShowItalic = false,
            ShowUnderline = false,
            ShowStrike = false,
            ShowFont = false,
            ShowFontSize = false,
        };

        var width = TextFormatBarLayout.ResolveWidth(items, colorSelectorWidth: 90);

        Assert.Equal(TextEditorLayout.ToggleWidth + TextEditorLayout.ControlSpacing + 90, width);
    }
}
