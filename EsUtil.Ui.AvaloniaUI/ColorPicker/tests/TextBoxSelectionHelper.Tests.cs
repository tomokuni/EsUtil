using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace EsUtil.Ui.AvaloniaUI.ColorPicker.Tests;

/// <summary><see cref="TextBoxSelectionHelper"/> の単体テスト。</summary>
/// <remarks>
/// ビジュアルツリーの検索（<see cref="TextBoxSelectionHelper.FindTextBox"/>）と
/// テキスト選択の解除（<see cref="TextBoxSelectionHelper.ResetSelection"/>）を検証する。<br/>
/// コントロールのビジュアルツリー操作を含むため、Avalonia ヘッドレス実行（<c>[AvaloniaFact]</c>）で直列検証する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class TextBoxSelectionHelperTests
{
    [AvaloniaFact]
    internal void FindTextBox_TextBoxを含むツリーから検索できる()
    {
        var textBox = new TextBox { Name = "Target" };
        var stack = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = "x" },
                new StackPanel { Children = { textBox } },
            },
        };

        var found = TextBoxSelectionHelper.FindTextBox(stack);

        Assert.Same(textBox, found);
    }

    [AvaloniaFact]
    internal void FindTextBox_TextBoxが無ければnullを返す()
    {
        var stack = new StackPanel
        {
            Children = { new TextBlock { Text = "x" } },
        };

        var found = TextBoxSelectionHelper.FindTextBox(stack);

        Assert.Null(found);
    }

    [AvaloniaFact]
    internal void FindTextBox_自身がTextBoxなら自身を返す()
    {
        var textBox = new TextBox();

        var found = TextBoxSelectionHelper.FindTextBox(textBox);

        Assert.Same(textBox, found);
    }

    [AvaloniaFact]
    internal void ResetSelection_TextBoxがあれば選択が末尾に移動する()
    {
        var textBox = new TextBox
        {
            Text = "Hello",
            SelectionStart = 0,
            SelectionEnd = 5
        };
        var stack = new StackPanel { Children = { textBox } };

        TextBoxSelectionHelper.ResetSelection(stack);

        Assert.Equal(5, textBox.SelectionStart);
        Assert.Equal(5, textBox.SelectionEnd);
    }

    [AvaloniaFact]
    internal void ResetSelection_TextBoxが無ければ何もしない()
    {
        var stack = new StackPanel { Children = { new TextBlock { Text = "x" } } };

        // 例外なく完了すれば成功
        TextBoxSelectionHelper.ResetSelection(stack);
    }

    [AvaloniaFact]
    internal void ResetSelection_TextBoxのTextがnullでも例外にならない()
    {
        var textBox = new TextBox(); // Text = null
        var stack = new StackPanel { Children = { textBox } };

        TextBoxSelectionHelper.ResetSelection(stack);

        Assert.Equal(0, textBox.SelectionStart);
        Assert.Equal(0, textBox.SelectionEnd);
    }
}
