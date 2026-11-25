using EsUtil.Feature.StyledText.Presenter.Formatting;
using Xunit;

namespace EsUtil.Feature.StyledText.Core.Tests.Presenter.Formatting;

/// <summary>書式バーの表示条件（自動表示・編集中・1 文字以上の選択・出す項目）を検証する。</summary>
public sealed class TextFormatBarVisibilityTests
{
    /// <summary>4 つの条件をすべて満たす間だけ表示する。</summary>
    [Theory]
    [InlineData(true, true, 1, true)]
    [InlineData(true, true, 3, true)]
    [InlineData(true, true, 0, false)]
    [InlineData(true, false, 2, false)]
    [InlineData(false, true, 2, false)]
    [InlineData(false, false, 0, false)]
    internal void ShouldShow_4つの条件を満たす間だけ表示する(
        bool autoShow, bool isEditing, int selectionLength, bool expected)
    {
        Assert.Equal(
            expected,
            TextFormatBarVisibility.ShouldShow(
                autoShow, isEditing, selectionLength, TextFormatBarItemVisibility.Default));
    }

    /// <summary>すべての項目を出さない場合は、他の条件を満たしても表示しない。</summary>
    [Fact]
    internal void ShouldShow_すべての項目を出さない場合は表示しない()
    {
        var hidden = TextFormatBarItemVisibility.Default with
        {
            ShowBold = false,
            ShowItalic = false,
            ShowUnderline = false,
            ShowStrike = false,
            ShowColor = false,
            ShowFontSize = false,
            ShowFont = false,
        };

        Assert.False(TextFormatBarVisibility.ShouldShow(true, true, 3, hidden));
    }

    /// <summary>1 つでも項目を出せば、他の条件を満たす間は表示する。</summary>
    [Fact]
    internal void ShouldShow_1つでも項目を出せば表示する()
    {
        var onlyBold = TextFormatBarItemVisibility.Default with
        {
            ShowItalic = false,
            ShowUnderline = false,
            ShowStrike = false,
            ShowColor = false,
            ShowFontSize = false,
            ShowFont = false,
        };

        Assert.True(TextFormatBarVisibility.ShouldShow(true, true, 1, onlyBold));
    }
}
