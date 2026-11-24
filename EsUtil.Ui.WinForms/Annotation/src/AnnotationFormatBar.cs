using EsUtil.Platform.Core.Palette;
using EsUtil.Feature.Annotation.Presenter.Editor;
using EsUtil.Feature.StyledText;
using EsUtil.Feature.StyledText.Core.Defaults;

namespace EsUtil.Ui.WinForms.Annotation;

/// <summary>本文の書式（文字単位）を操作するバー（WinForms 版）。</summary>
/// <remarks>
/// <b>適用先は選択範囲</b>: 本文を編集中は本文の選択範囲、編集していないときは本文全体へ適用する。<br/>
/// <b>色は代表色のメニューから選ぶ</b>（任意の色は色ダイアログ。 WinForms には色選択コントロールが無い）。
/// </remarks>
internal sealed class AnnotationFormatBar : Panel
{
    /// <summary>適用先のレイヤー。</summary>
    private readonly AnnotationLayerControl _layer;

    /// <summary>太字の切り替え。</summary>
    private readonly CheckBox _bold;

    /// <summary>斜体の切り替え。</summary>
    private readonly CheckBox _italic;

    /// <summary>下線の切り替え。</summary>
    private readonly CheckBox _underline;

    /// <summary>取り消し線の切り替え。</summary>
    private readonly CheckBox _strike;

    /// <summary>フォントの選択。</summary>
    private readonly ComboBox _font;

    /// <summary>文字サイズの入力。</summary>
    private readonly NumericUpDown _size;

    /// <summary>色のボタン。</summary>
    private readonly Button _color;

    /// <summary>通知による更新中かどうか（利用者の操作と区別する）。</summary>
    private bool _refreshing;

    /// <summary>書式バーを初期化する。</summary>
    /// <param name="layer">適用先のレイヤー。</param>
    internal AnnotationFormatBar(AnnotationLayerControl layer)
    {
        _layer = layer;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Color.FromArgb(250, 250, 250);
        BorderStyle = BorderStyle.FixedSingle;
        Padding = new Padding(4);

        _bold = AnnotationEditorControls.Toggle("B", false, value => Toggle(style => style with { IsBold = value }), bold: true);
        _italic = AnnotationEditorControls.Toggle("I", false, value => Toggle(style => style with { IsItalic = value }), italic: true);
        _underline = AnnotationEditorControls.Toggle("U", false, value => Toggle(style => style with { IsUnderline = value }));
        _strike = AnnotationEditorControls.Toggle("S", false, value => Toggle(style => style with { IsStrike = value }));

        // **フォント名は直接入力できる**（一覧に無いフォントも指定できる。 標準の編集可の
        // コンボボックスであるため Ctrl+C / Ctrl+V もそのまま使える）
        _font = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDown,
            Width = (int)Math.Round(AnnotationFormatBarLayout.FontSelectorWidth),
            Font = new Font(AnnotationEditorControls.UiFontFamily, AnnotationEditorControls.FontSize),
            Margin = new Padding(1),
        };

        foreach (var family in AnnotationFonts.All)
        {
            _font.Items.Add(family);
        }

        _font.SelectedIndex = 0;
        _font.SelectedIndexChanged += (_, _) =>
        {
            if (!_refreshing && _font.SelectedItem is string family)
            {
                ApplyFull(style => style with { FontFamily = family });
            }
        };

        // 入力したフォント名は Enter とフォーカスを外したときに適用する（1 文字ごとに適用しない）
        _font.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && !_refreshing && !string.IsNullOrWhiteSpace(_font.Text))
            {
                ApplyFull(style => style with { FontFamily = _font.Text });
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        };
        _font.Validated += (_, _) =>
        {
            if (!_refreshing && !string.IsNullOrWhiteSpace(_font.Text))
            {
                ApplyFull(style => style with { FontFamily = _font.Text });
            }
        };

        _size = AnnotationEditorControls.NumberBox(
            TextDefaults.FontSizeDefault, 6, 512,
            value => ApplyFull(style => style with { FontSize = value }));
        _color = AnnotationEditorControls.ColorButton(
            TextDefaults.DefaultStyle.Color, color => ApplyFull(style => style with { Color = color }));

        // 並び順はコア（AnnotationFormatBarLayout.Order）が単一所有する。
        // ここで順を決めると 4 つの UI 版で操作の位置がずれる。
        var items = new Dictionary<AnnotationFormatBarItem, Control>
        {
            [AnnotationFormatBarItem.Bold] = _bold,
            [AnnotationFormatBarItem.Italic] = _italic,
            [AnnotationFormatBarItem.Underline] = _underline,
            [AnnotationFormatBarItem.Strike] = _strike,
            [AnnotationFormatBarItem.Font] = _font,
            [AnnotationFormatBarItem.Size] = _size,
            [AnnotationFormatBarItem.Color] = _color,
        };

        foreach (var item in AnnotationFormatBarLayout.Order)
        {
            Controls.Add(items[item]);
        }

        LayoutRow();

        RefreshState();
    }

    /// <summary>入力欄の表記を取得または設定する（色のメニューは表記を持たないため保持だけする）。</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal ColorTextFormat TextFormat { get; set; } = ColorTextFormat.NameWithAlpha;

    /// <summary>現在の選択範囲の書式を各操作へ反映する。</summary>
    internal void RefreshState()
    {
        _refreshing = true;
        try
        {
            var style = _layer.SelectedTextStyle;
            _bold.Checked = _layer.SelectedTextMatches(static candidate => candidate.IsBold);
            _italic.Checked = _layer.SelectedTextMatches(static candidate => candidate.IsItalic);
            _underline.Checked = _layer.SelectedTextMatches(static candidate => candidate.IsUnderline);
            _strike.Checked = _layer.SelectedTextMatches(static candidate => candidate.IsStrike);

            var index = AnnotationFonts.All.ToList().IndexOf(style.FontFamily);
            _font.SelectedIndex = index >= 0 ? index : 0;
            _size.Value = (decimal)TextLimits.CoerceFontSize(style.FontSize);
            _color.BackColor = style.Color;
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>装飾を切り替える。</summary>
    /// <param name="change">書式を変える処理。</param>
    private void Toggle(Func<TextStyle, TextStyle> change)
    {
        if (_refreshing)
        {
            return;
        }

        _layer.TransformSelectionStyle(change);
        RefreshState();
    }

    /// <summary>書式を丸ごと適用する（色・大きさ・フォント）。</summary>
    /// <param name="change">書式を変える処理。</param>
    private void ApplyFull(Func<TextStyle, TextStyle> change)
    {
        if (_refreshing)
        {
            return;
        }

        _layer.ApplyFullStyle(change(_layer.SelectedTextStyle));
        RefreshState();
    }

    /// <summary>子要素を左から順に並べる。</summary>
    private void LayoutRow()
    {
        // 行の高さはコア（AnnotationFormatBarLayout）が単一所有する。 項目ごとに高さが違うと縦位置が揃わない。
        // ComboBox（DropDownList）は高さを指定できないため、実際の高さで縦方向の中央へ寄せる
        var rowHeight = (int)Math.Round(AnnotationFormatBarLayout.ItemHeight);
        var x = Padding.Left;
        foreach (Control control in Controls)
        {
            control.Height = rowHeight;
            control.Left = x;
            control.Top = Padding.Top + Math.Max(0, (rowHeight - control.Height) / 2);
            x += control.Width + control.Margin.Horizontal;
        }
    }
}
