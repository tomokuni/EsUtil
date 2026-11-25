using EsUtil.Feature.StyledText.Presenter.Formatting;
using Xunit;

namespace EsUtil.Feature.StyledText.Core.Tests.Presenter.Formatting;

/// <summary>書式バーの表示条件（自動表示・編集中・1 文字以上の選択）を検証する。</summary>
public sealed class TextFormatBarVisibilityTests
{
    /// <summary>3 つの条件をすべて満たす間だけ表示する。</summary>
    [Theory]
    [InlineData(true, true, 1, true)]
    [InlineData(true, true, 3, true)]
    [InlineData(true, true, 0, false)]
    [InlineData(true, false, 2, false)]
    [InlineData(false, true, 2, false)]
    [InlineData(false, false, 0, false)]
    internal void ShouldShow_3つの条件を満たす間だけ表示する(
        bool autoShow, bool isEditing, int selectionLength, bool expected)
    {
        Assert.Equal(expected, TextFormatBarVisibility.ShouldShow(autoShow, isEditing, selectionLength));
    }
}
