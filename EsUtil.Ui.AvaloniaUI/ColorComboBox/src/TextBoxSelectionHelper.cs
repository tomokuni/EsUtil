using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace EsUtil.Ui.AvaloniaUI.ColorComboBox;

/// <summary>ビジュアルツリー内の <see cref="TextBox"/> の検索とテキスト選択の解除を行うユーティリティ。</summary>
/// <remarks>
/// <see cref="ColorComboBoxControl"/> のドロップダウン選択後処理（テキスト選択の自動解除）から利用する。<br/>
/// UI 依存（Avalonia ビジュアルツリー）だが <see cref="ColorComboBoxControl"/> とは独立しており、<br/>
/// ComboBox 等のテキスト選択を扱う他のコントロールでも流用可能である。
/// </remarks>
internal static class TextBoxSelectionHelper
{
    /// <summary>指定ビジュアルツリー内の <see cref="TextBox"/> のテキスト選択を末尾に移動する。</summary>
    /// <param name="visual">検索対象のビジュアルツリー。</param>
    /// <remarks>
    /// ドロップダウン選択後、ComboBox 内のテキストが全選択されたままになるのを防ぐ。<br/>
    /// <see cref="TextBox"/> が見つからない場合は何もしない（null 安全）。
    /// </remarks>
    internal static void ResetSelection(Visual visual)
    {
        var textBox = FindTextBox(visual);
        if (textBox is not null)
        {
            var len = textBox.Text?.Length ?? 0;
            textBox.SelectionStart = len;
            textBox.SelectionEnd = len;
        }
    }

    /// <summary>ビジュアルツリーから <see cref="TextBox"/> を再帰検索する。</summary>
    /// <param name="visual">検索対象のビジュアルツリー。</param>
    /// <returns>最初に見つかった <see cref="TextBox"/>。 存在しない場合は null。</returns>
    /// <remarks>
    /// 深さ優先探索で <see cref="TextBox"/> を検索する。 自身が <see cref="TextBox"/> の場合は自身を返す。
    /// </remarks>
    internal static TextBox? FindTextBox(Visual visual)
    {
        if (visual is TextBox tb) return tb;
        foreach (var child in visual.GetVisualChildren())
        {
            var result = FindTextBox(child);
            if (result is not null) return result;
        }
        return null;
    }
}
