using EsUtil.Feature.StyledText.Presenter.Formatting;
using Xunit;
using EsUtil.Feature.Annotation.Presenter.Editor;
using EsUtil.Feature.Annotation.Presenter.Panel;

namespace EsUtil.Feature.Annotation.Core.Tests;

/// <summary>右クリックの外観フライアウトの寸法（<see cref="AnnotationPanelAppearance.StyleEditor"/>）の規則を検証する。</summary>
/// <remarks>
/// <b>行の入力欄はすべて同じ幅</b>であること、<b>フライアウトの幅がその合計から決まる</b>ことを固定します。
/// 幅が揃っていないと行の右端がばらばらに見え、固定値にすると右側に使わない幅が空きます。
/// </remarks>
public sealed class AnnotationEditorLayoutTests
{
    /// <summary>入力欄の幅は項目名より広く、行の合計がフライアウトの幅に収まる。</summary>
    [Fact]
    internal void 入力欄の幅はフライアウトの幅に収まる()
    {
        var content = AnnotationPanelAppearance.StyleEditor.ContentWidth;
        var row = AnnotationEditorLayout.LabelWidth + AnnotationEditorLayout.RowSpacing + AnnotationEditorLayout.FieldWidth;

        Assert.True(AnnotationEditorLayout.FieldWidth > 0);
        Assert.True(
            row < content,
            $"行の幅 {row} がフライアウトの幅 {content} を超えています（余白と枠の分が足りません）。");
    }

    /// <summary>フライアウトの幅は「項目名 + 入力欄 + 余白 + 枠」で決まる。</summary>
    [Fact]
    internal void フライアウトの幅は行の合計から決まる()
    {
        var panel = AnnotationPanelAppearance.StyleEditor;
        var expected = AnnotationEditorLayout.LabelWidth
            + AnnotationEditorLayout.RowSpacing
            + AnnotationEditorLayout.FieldWidth
            + (panel.Padding * 2)
            + (panel.BorderThickness * 2);

        Assert.Equal(expected, panel.ContentWidth, 6);
    }

    /// <summary>書式バーの項目の高さは装飾ボタンの文字サイズより高い。</summary>
    [Fact]
    internal void 装飾ボタンの文字は項目の高さに収まる()
        => Assert.True(AnnotationEditorLayout.ToggleFontSize < TextFormatBarLayout.ItemHeight);
}
