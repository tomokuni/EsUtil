using EsUtil.Feature.StyledText.Core.Defaults;
using EsUtil.Platform.Core.Palette;

namespace EsUtil.Ui.WinForms.StyledText;

/// <summary>本文の書式（文字単位）を操作するバー（WinForms 版）。</summary>
/// <remarks>
/// <b>適用先は選択範囲</b>: 本文を編集中は本文の選択範囲、編集していないときは本文全体へ適用する。<br/>
/// <b>色は代表色のメニューから選ぶ</b>（任意の色は色ダイアログ。 WinForms には色選択コントロールが無い）。
/// </remarks>
public sealed class StyledTextFormatBar : Panel, IStyledTextFormatBar
{
    /// <summary>書式を読み書きする相手（ホストが渡す）。</summary>
    private readonly ITextFormatBarContext _context;

    /// <summary>バーの構成（フォントの一覧・色のボタン）。</summary>
    private readonly StyledTextFormatBarOptions _options;

    /// <summary>選べるフォント名の一覧。</summary>
    private readonly IReadOnlyList<string> _fonts;

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

    /// <summary>書式バーに出す項目の組み合わせ。</summary>
    private readonly TextFormatBarItemVisibility _items;

    /// <summary>書式バーを初期化する。</summary>
    /// <param name="context">書式を読み書きする相手（ホストが実装する）。</param>
    /// <param name="options">バーの構成（フォントの一覧・色のボタン）。 省略すると既定の構成を使う。</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> が null の場合。</exception>
    public StyledTextFormatBar(ITextFormatBarContext context, StyledTextFormatBarOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _options = options ?? new StyledTextFormatBarOptions();
        _fonts = _options.Fonts;
        _items = _options.Items;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Color.FromArgb(250, 250, 250);

        _bold = StyledTextFormatBarControls.Toggle("B", false, value => Toggle(style => style with { IsBold = value }), bold: true);
        _italic = StyledTextFormatBarControls.Toggle("I", false, value => Toggle(style => style with { IsItalic = value }), italic: true);
        _underline = StyledTextFormatBarControls.Toggle("U", false, value => Toggle(style => style with { IsUnderline = value }));
        _strike = StyledTextFormatBarControls.Toggle("S", false, value => Toggle(style => style with { IsStrike = value }));

        // **フォント名は直接入力できる**（一覧に無いフォントも指定できる。 標準の編集可の
        // コンボボックスであるため Ctrl+C / Ctrl+V もそのまま使える）
        _font = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDown,
            Width = (int)Math.Round(TextFormatBarLayout.FontSelectorWidth),
            Font = new Font(StyledTextFormatBarControls.UiFontFamily, StyledTextFormatBarControls.FontSize),
            Margin = new Padding(1),
        };

        foreach (var family in _fonts)
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

        _size = StyledTextFormatBarControls.NumberBox(
            TextDefaults.FontSizeDefault, 6, 512,
            value => ApplyFull(style => style with { FontSize = value }));
        _color = (_options.CreateColorButton ?? CreateDefaultColorButton)(
            TextDefaults.DefaultStyle.Color, color => ApplyFull(style => style with { Color = color }));

        // 並び順はコア（TextFormatBarLayout.Order）が単一所有する。
        // ここで順を決めると 4 つの UI 版で操作の位置がずれる。
        var items = new Dictionary<TextFormatBarItem, Control>
        {
            [TextFormatBarItem.Bold] = _bold,
            [TextFormatBarItem.Italic] = _italic,
            [TextFormatBarItem.Underline] = _underline,
            [TextFormatBarItem.Strike] = _strike,
            [TextFormatBarItem.Font] = _font,
            [TextFormatBarItem.Size] = _size,
            [TextFormatBarItem.Color] = _color,
        };

        // **出さない項目は場所ごと詰める**: 空の枠を残すと、押せない余白が挟まって間延びしたバーになる
        foreach (var item in _items.VisibleItems)
        {
            Controls.Add(items[item]);
        }

        LayoutRow();

        RefreshState();
    }

    /// <summary>入力欄の表記を取得または設定する（色のメニューは表記を持たないため保持だけする）。</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public ColorTextFormat TextFormat { get; set; } = ColorTextFormat.NameWithAlpha;

    /// <summary>バーの画面要素を取得する（ホストはこれをパネルへ載せる）。</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Control View => this;

    /// <summary>バーの幅（DIP）を取得する。</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public double PreferredWidth => TextFormatBarLayout.ResolveWidth(_items, _color.Width);

    /// <summary>本文のキー操作を転送する処理を設定する。</summary>
    /// <param name="forward">転送する処理（<see langword="null"/> は転送しない）。</param>
    public void SetKeyHandler(Func<Keys, bool>? forward) => _forwardKey = forward;

    /// <summary>本文へフォーカスを戻す処理を設定する。</summary>
    /// <param name="request">フォーカスを戻す処理（<see langword="null"/> は何もしない）。</param>
    public void SetFocusRequest(Action? request) => _requestFocus = request;

    /// <summary>本文のキー操作を転送する処理（ホストが設定する）。</summary>
    private Func<Keys, bool>? _forwardKey;

    /// <summary>本文へフォーカスを戻す処理（ホストが設定する）。</summary>
    private Action? _requestFocus;

    /// <summary>バーへ届いたキーを本文へ転送する（入力欄の内側では転送しない）。</summary>
    /// <param name="msg">ウィンドウ メッセージ。</param>
    /// <param name="keyData">キーと修飾キー。</param>
    /// <returns>転送して処理した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>入力欄の内側では転送しない</b>: 数値・フォント名の入力欄は自分のキー（矢印・文字）を使う。
    /// 転送すると入力欄の編集が壊れる。<br/>
    /// <b>方向キーも先に受け取る</b>: WinForms は方向キーをフォーカス移動として扱うため、
    /// <see cref="ProcessCmdKey"/> で本文へ渡す（渡さないと選択の拡張が効かない）。
    /// </remarks>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_forwardKey is { } forward && !_size.ContainsFocus && !_font.ContainsFocus
            && forward(keyData))
        {
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>既定の色のボタンを作る（色ダイアログだけを使う）。</summary>
    /// <param name="color">現在の色。</param>
    /// <param name="apply">色が選ばれたときに呼ぶ処理。</param>
    /// <returns>色のボタン。</returns>
    private static Button CreateDefaultColorButton(Color color, Action<Color> apply)
    {
        var button = new Button
        {
            Width = 64,
            Height = 24,
            BackColor = color,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(1),
        };

        button.FlatAppearance.BorderColor = Color.FromArgb(90, 90, 90);
        button.Click += (_, _) =>
        {
            using var dialog = new ColorDialog { Color = button.BackColor, FullOpen = true };
            if (dialog.ShowDialog(button) != DialogResult.OK)
            {
                return;
            }

            button.BackColor = dialog.Color;
            apply(dialog.Color);
        };
        return button;
    }

    /// <summary>現在の選択範囲の書式を各操作へ反映する。</summary>
    internal void RefreshState()
    {
        _refreshing = true;
        try
        {
            var style = _context.SelectedTextStyle;
            _bold.Checked = _context.SelectedTextMatches(static candidate => candidate.IsBold);
            _italic.Checked = _context.SelectedTextMatches(static candidate => candidate.IsItalic);
            _underline.Checked = _context.SelectedTextMatches(static candidate => candidate.IsUnderline);
            _strike.Checked = _context.SelectedTextMatches(static candidate => candidate.IsStrike);

            var index = _fonts.ToList().IndexOf(style.FontFamily);
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

        _context.Transform(change);
        RefreshState();

        // バーの操作でフォーカスが移るため、本文へ戻して続けて入力できるようにする
        _requestFocus?.Invoke();
    }

    /// <summary>書式を丸ごと適用する（色・大きさ・フォント）。</summary>
    /// <param name="change">書式を変える処理。</param>
    private void ApplyFull(Func<TextStyle, TextStyle> change)
    {
        if (_refreshing)
        {
            return;
        }

        _context.Apply(change(_context.SelectedTextStyle));
        RefreshState();

        // バーの操作でフォーカスが移るため、本文へ戻して続けて入力できるようにする
        _requestFocus?.Invoke();
    }

    /// <summary>子要素を左から順に並べる。</summary>
    private void LayoutRow()
    {
        // 行の高さはコア（TextFormatBarLayout）が単一所有する。 項目ごとに高さが違うと縦位置が揃わない。
        // ComboBox（DropDownList）は高さを指定できないため、実際の高さで縦方向の中央へ寄せる
        var rowHeight = (int)Math.Round(TextFormatBarLayout.ItemHeight);
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
