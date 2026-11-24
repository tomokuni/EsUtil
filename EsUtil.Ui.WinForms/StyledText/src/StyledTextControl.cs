using System.Runtime.InteropServices;
using System.Text;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.WinForms.StyledText;

/// <summary>本文を表示し、その場で編集するコントロール（表示と編集を 1 つの部品にまとめる）。</summary>
/// <remarks>
/// <b>表示と編集を分けない</b>: 入力欄を重ねる方法では、書式（run 単位の色・太字）が反映されず、
/// 書式バーを操作するたびにフォーカスと選択が外れる。 本コントロールは本文を自分で描き、キー入力のたびに
/// 本文を書き換える（書式はその場で反映される）。<br/>
/// <b>規則は提示層が単一所有する</b>: キャレット・選択・書式の適用は <see cref="TextEditState"/>、
/// キーの割り当ては <see cref="TextEditKeyMap"/>、折り返しと行の配置は <see cref="TextLayout"/> が決める。
/// 本コントロールは GDI+ の描画・入力・クリップボードの配線だけを行う。<br/>
/// <b>座標は元画像ピクセル</b>である: <see cref="RenderScale"/> で画面の DIP へ写す。
/// </remarks>
public sealed class StyledTextControl : Control
{
    /// <summary>キャレットの点滅を進めるタイマー（編集中だけ動かす）。</summary>
    private readonly System.Windows.Forms.Timer _caretTimer = new() { Interval = 530 };

    /// <summary>キャレットと選択の状態（規則は提示層が単一所有する）。</summary>
    private TextEditState _edit = new();

    /// <summary>IME の変換中の文字列（本文へは入れない）。</summary>
    private string _preeditText = string.Empty;

    /// <summary>キャレットの点滅を始めた時刻。</summary>
    private DateTimeOffset _caretBlinkStart = DateTimeOffset.UtcNow;

    /// <summary>ポインターで文字を選んでいる最中かどうか。</summary>
    private bool _isSelectingText;

    /// <summary>入力を受け付けるかどうか（既定は受け付ける）。</summary>
    private bool _isInputEnabled = true;

    /// <summary>表示する本文。</summary>
    private RichText _text = RichText.Empty;

    /// <summary>本文の編集と表示を担うコントロールを初期化する。</summary>
    public StyledTextControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.Selectable
            | ControlStyles.UserPaint,
            true);
        TabStop = true;
        _caretTimer.Tick += (_, _) =>
        {
            if (IsEditing)
            {
                Invalidate();
            }
            else
            {
                _caretTimer.Stop();
            }
        };
    }

    /// <summary>表示する本文を取得または設定する。</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public RichText RichTextValue
    {
        get => _text;
        set
        {
            _text = value ?? RichText.Empty;

            // **本文が外から変わったら編集位置を本文の範囲へ収める**（差し替えで範囲外に残さない）
            if (_edit.IsActive)
            {
                _edit.Clamp(_text.PlainText.Length);
            }

            Invalidate();
        }
    }

    /// <summary>画面の DIP への倍率を取得または設定する。</summary>
    /// <value>1 以上で拡大。 1 未満・非有限は 1 として扱う。</value>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public double RenderScale { get; set; } = 1.0;

    /// <summary>行間の倍率を取得または設定する。</summary>
    /// <value>1 未満・非有限は 1 として扱う。</value>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public double LineHeightFactor { get; set; } = 1.0;

    /// <summary>文字列と書式から実寸を測る関数を取得または設定する。</summary>
    /// <value>既定は本コントロールの描画 API で測る関数。</value>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<string, TextStyle, EsSize> MeasureText { get; set; } = RichTextPainter.Measure;

    /// <summary>キャレット・選択・書式の状態を取得または設定する。</summary>
    /// <value>既定は本コントロールが作った状態。 ホストが既に状態を持っている場合はその実体を設定する。</value>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public TextEditState EditState
    {
        get => _edit;
        set
        {
            _edit = value ?? new TextEditState();
            Invalidate();
        }
    }

    /// <summary>IME の変換中の文字列を取得または設定する（本文へは入れない）。</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string PreeditText
    {
        get => _preeditText;
        set
        {
            var next = value ?? string.Empty;
            if (string.Equals(_preeditText, next, StringComparison.Ordinal))
            {
                return;
            }

            _preeditText = next;
            Invalidate();
        }
    }

    /// <summary>本文を編集中かどうかを取得する。</summary>
    public bool IsEditing => _edit.IsActive;

    /// <summary>入力を受け付けるかどうかを取得または設定する。</summary>
    /// <value>入力を受け付ける場合は <see langword="true"/>（既定）。 表示専用にする場合は <see langword="false"/>。</value>
    /// <remarks>
    /// <b>ホストが入力を所有する場合に使う</b>: 注釈のレイヤーのように入力とフォーカスを自分で持つホストへ
    /// 重ねるとき、本コントロールがフォーカスを奪うとキーと IME の宛先が変わってしまう。 表示専用にすると
    /// フォーカスも入力も取らない（描画だけを行う）。
    /// </remarks>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool IsInputEnabled
    {
        get => _isInputEnabled;
        set
        {
            if (_isInputEnabled == value)
            {
                return;
            }

            _isInputEnabled = value;
            SetStyle(ControlStyles.Selectable, value);
            TabStop = value;
            Invalidate();
        }
    }

    /// <summary>キャレットの位置（文字数）を取得する。</summary>
    public int CaretIndex => _edit.CaretIndex;

    /// <summary>選択範囲の開始位置（文字数）を取得する。</summary>
    public int SelectionStart => _edit.SelectionStart;

    /// <summary>選択範囲の終了位置（文字数）を取得する。</summary>
    public int SelectionEnd => _edit.SelectionEnd;

    /// <summary>選択範囲の長さ（文字数）を取得する。</summary>
    public int SelectionLength => _edit.SelectionLength;

    /// <summary>本文の文字を選んでいるかどうかを取得する。</summary>
    public bool HasSelection => _edit.HasSelection;

    /// <summary>本文を書き換えたときに発生する。</summary>
    public event EventHandler? RichTextChanged;

    /// <summary>キャレットまたは選択範囲が変わったときに発生する。</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>編集の開始・終了で発生する。</summary>
    public event EventHandler? EditingChanged;

    /// <summary>本文の編集を始める（キャレットを本文の末尾へ置く）。</summary>
    public void BeginEditing()
    {
        if (_edit.IsActive)
        {
            return;
        }

        _edit.Begin(RichTextValue.PlainText.Length);
        Focus();
        RestartCaretBlink();
        EditingChanged?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    /// <summary>本文の編集を終える（内容は既に <see cref="RichTextValue"/> へ入っている）。</summary>
    public void EndEditing()
    {
        if (!_edit.IsActive)
        {
            return;
        }

        _edit.End();
        _preeditText = string.Empty;
        _caretTimer.Stop();
        EditingChanged?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    /// <summary>選択範囲を指定する。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（文字数。 0 は選択なし）。</param>
    public void Select(int start, int length)
    {
        _edit.Select(start, length, RichTextValue.PlainText.Length);
        AfterSelectionChanged();
    }

    /// <summary>本文をすべて選択する。</summary>
    public void SelectAll()
    {
        var length = RichTextValue.PlainText.Length;
        _edit.Select(0, length, length);
        AfterSelectionChanged();
    }

    /// <summary>キャレットを指定位置へ移す。</summary>
    /// <param name="index">移動先（文字数）。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    public void MoveCaret(int index, bool extend)
    {
        var text = RichTextValue.PlainText;
        var target = Math.Clamp(index, 0, text.Length);

        // **キャレットは書記素クラスタの境界にだけ置く**
        target = target < _edit.CaretIndex
            ? TextCursor.SnapBackward(text, target)
            : TextCursor.SnapForward(text, target);
        _edit.MoveTo(target, text.Length, extend);
        AfterSelectionChanged();
    }

    /// <summary>キャレットを前後の 1 クラスタへ移す（選択があるときは選択の端へ畳む）。</summary>
    /// <param name="direction">移動する向き（負は左、正は右）。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    public void MoveCaretByCluster(int direction, bool extend)
        => MoveCaret(_edit.ResolveClusterTarget(direction, extend), extend);

    /// <summary>キャレットを上下の行の同じ桁へ移す。</summary>
    /// <param name="delta">移動する行数。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    public void MoveCaretByLine(int delta, bool extend)
    {
        _edit.MoveByLine(ResolveLineIndex(), RichTextValue.PlainText.Length, delta, extend);
        SnapCaretToCluster(extend);
        AfterSelectionChanged();
    }

    /// <summary>キャレットを行の先頭・末尾へ移す。</summary>
    /// <param name="first">行の先頭へ移す場合は <see langword="true"/>。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    public void MoveCaretToLineEdge(bool first, bool extend)
    {
        _edit.MoveToLineEdge(ResolveLineIndex(), RichTextValue.PlainText.Length, first, extend);
        SnapCaretToCluster(extend);
        AfterSelectionChanged();
    }

    /// <summary>位置（元画像ピクセル）に最も近い文字位置へキャレットを移す。</summary>
    /// <param name="point">位置（元画像ピクセル）。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    public void SetCaretFromPoint(EsPoint point, bool extend)
    {
        if (ResolveLineIndex() is not { } index)
        {
            return;
        }

        _edit.SetCaretFromPoint(index, point, RichTextValue.PlainText.Length, extend, MeasureText);
        AfterSelectionChanged();
    }

    /// <summary>選択範囲を文字列で置き換える（選択が無ければ挿入）。</summary>
    /// <param name="text">挿入する文字列（空なら削除）。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool ReplaceSelection(string text) => Commit(_edit.ReplaceSelection(RichTextValue, text));

    /// <summary>指定した範囲を文字列で置き換える。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="end">終了位置（文字数）。</param>
    /// <param name="text">置き換える文字列（空なら削除）。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool ReplaceRange(int start, int end, string text)
        => Commit(_edit.ReplaceRange(RichTextValue, start, end, text));

    /// <summary>選択範囲、またはキャレットの前後の 1 クラスタを削除する。</summary>
    /// <param name="forward">キャレットの次の文字を削除する場合は <see langword="true"/>。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool DeleteAtCaret(bool forward) => Commit(_edit.Delete(RichTextValue, forward));

    /// <summary>選択範囲の書式を丸ごと適用する（選択が無ければ以降の入力へ回す）。</summary>
    /// <param name="style">適用する書式。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool ApplyTextStyle(TextStyle style) => Commit(_edit.ApplyStyle(RichTextValue, style));

    /// <summary>指定した範囲の書式を丸ごと適用する。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（文字数。 0 以下は本文全体）。</param>
    /// <param name="style">適用する書式。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool ApplyTextStyle(int start, int length, TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);

        var updated = TextStyleEdit.ResolveRange(start, length) is { } range
            ? RichTextValue.ApplyStyle(range.Start, range.Length, style)
            : RichTextValue;
        _edit.Remember(style);
        return Commit(updated);
    }

    /// <summary>選択範囲の書式を 1 属性だけ変換する（選択が無ければ以降の入力へ回す）。</summary>
    /// <param name="transform">現在の書式を受け取って新しい書式を返す関数。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool TransformTextStyle(Func<TextStyle, TextStyle> transform)
        => Commit(_edit.TransformStyle(RichTextValue, transform));

    /// <summary>指定した範囲の書式を 1 属性だけ変換する。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（文字数。 0 以下は本文全体）。</param>
    /// <param name="transform">現在の書式を受け取って新しい書式を返す関数。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool TransformTextStyle(int start, int length, Func<TextStyle, TextStyle> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        var at = Math.Clamp(start, 0, RichTextValue.PlainText.Length);
        var next = transform(RichTextValue.StyleAt(at));
        _edit.Remember(next);
        return Commit(RichTextValue.TransformStyle(start, length, _ => next));
    }

    /// <summary>書式バーへ表示する書式を取得する（選択が無い間は覚えた書式）。</summary>
    public TextStyle SelectedTextStyle => _edit.ResolveDisplayStyle(RichTextValue);

    /// <summary>選択範囲の文字がすべて条件を満たすかどうかを取得する。</summary>
    /// <param name="match">書式が条件を満たすかどうかを返す関数。</param>
    /// <returns>すべて満たす場合は <see langword="true"/>。</returns>
    public bool SelectedTextMatches(Func<TextStyle, bool> match) => _edit.AllMatch(RichTextValue, match);

    /// <summary>選択中の文字列を取得する。</summary>
    /// <returns>選択中の文字列（選択が無い場合は空）。</returns>
    public string SelectedText()
    {
        var text = RichTextValue.PlainText;
        var start = Math.Clamp(SelectionStart, 0, text.Length);
        var end = Math.Clamp(SelectionEnd, start, text.Length);
        return text[start..end];
    }

    /// <summary>選択中の文字列をクリップボードへコピーする。</summary>
    public void Copy()
    {
        var text = SelectedText();
        if (text.Length > 0)
        {
            Clipboard.SetText(text);
        }
    }

    /// <summary>選択中の文字列を切り取る。</summary>
    public void Cut()
    {
        if (SelectedText().Length == 0)
        {
            return;
        }

        Copy();
        ReplaceSelection(string.Empty);
    }

    /// <summary>クリップボードの文字列を挿入する。</summary>
    public void Paste()
    {
        if (Clipboard.ContainsText())
        {
            ReplaceSelection(NormalizeInput(Clipboard.GetText()));
        }
    }

    /// <summary>キャレットの矩形（このコントロールの座標の DIP）を取得する。</summary>
    public RectangleF CaretRect
    {
        get
        {
            // **変換中の文字列の分だけ進めたキャレットを使う**: 変換候補は変換中の文字の末尾へ出す
            var caretIndex = TextCompositionPreview.ResolveCaret(_edit.CaretIndex, _preeditText);
            if (ResolveLineIndex() is not { } index || index.ResolveCaret(caretIndex, MeasureText) is not { } caret)
            {
                return default;
            }

            var scale = ResolveScale();
            return new RectangleF(
                (float)(caret.X * scale),
                (float)(caret.Y * scale),
                (float)TextEditorAppearance.CaretThicknessDip,
                (float)(caret.Height * scale));
        }
    }

    /// <summary>キャレットの点滅を点灯から始め直す（ホストが入力を持つ場合に呼ぶ）。</summary>
    public void RefreshCaretBlink()
    {
        if (IsEditing)
        {
            RestartCaretBlink();
            Invalidate();
        }
    }

    /// <summary>画像の表示倍率を掛けたうえで本文・選択範囲・キャレットを描く。</summary>
    /// <param name="e">描画の引数。</param>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var graphics = e.Graphics;
        graphics.Clear(BackColor);
        var scale = ResolveScale();
        var state = graphics.Save();
        try
        {
            graphics.ScaleTransform((float)scale, (float)scale);

            // **変換中の文字列も本文として描く**（重ねて描くと既存の文字と二重になる）
            var displayed = DisplayText;
            var lines = ResolveLines(displayed);
            foreach (var line in lines)
            {
                RichTextPainter.DrawLine(graphics, line);
            }

            if (!IsEditing || TextLineIndex.Create(lines, displayed.PlainText) is not { } index)
            {
                return;
            }

            var selectionColor = TextEditorAppearance.SelectionColor;
            using (var selectionBrush = new SolidBrush(
                Color.FromArgb(selectionColor.A, selectionColor.R, selectionColor.G, selectionColor.B)))
            {
                foreach (var rect in index.ResolveSelectionRects(SelectionStart, SelectionEnd, MeasureText))
                {
                    graphics.FillRectangle(
                        selectionBrush,
                        (float)rect.X,
                        (float)rect.Y,
                        (float)rect.Width,
                        (float)rect.Height);
                }
            }

            // **変換中はキャレットを変換中の文字列の末尾へ置く**
            var caretIndex = TextCompositionPreview.ResolveCaret(_edit.CaretIndex, _preeditText);
            if (index.ResolveCaret(caretIndex, MeasureText) is not { } caret || !IsCaretVisible)
            {
                return;
            }

            var color = TextEditorAppearance.CaretColor;
            using var caretBrush = new SolidBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
            var thickness = TextEditorAppearance.CaretThicknessDip / scale;
            graphics.FillRectangle(
                caretBrush, (float)caret.X, (float)caret.Y, (float)thickness, (float)caret.Height);
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    /// <summary>キーが押されたときの処理（割り当ては <see cref="TextEditKeyMap"/> が単一所有する）。</summary>
    /// <param name="e">キーの引数。</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (TryHandleEditingKey(e.KeyData))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    /// <summary>キーを本文の編集操作へ適用する（ホストの別要素がフォーカスを持つ場合の転送口）。</summary>
    /// <param name="keyData">キーと修飾キー（<see cref="Keys"/> の組み合わせ）。</param>
    /// <returns>処理した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>割り当ては <see cref="TextEditKeyMap"/> が単一所有する</b>: 本コントロールとホストの
    /// 書式バーが同じ規則で動くよう、キーの解釈を 2 か所に書かない。
    /// </remarks>
    internal bool TryHandleEditingKey(Keys keyData)
    {
        if (!_isInputEnabled || !IsEditing)
        {
            return false;
        }

        var request = TextEditKeyMap.Resolve(ToTextEditKey(keyData & Keys.KeyCode), ToTextEditModifiers(keyData));
        switch (request.Command)
        {
            case TextEditCommand.ToggleBold:
                return TransformTextStyle(static style => style with { IsBold = !style.IsBold });
            case TextEditCommand.ToggleItalic:
                return TransformTextStyle(static style => style with { IsItalic = !style.IsItalic });
            case TextEditCommand.ToggleUnderline:
                return TransformTextStyle(static style => style with { IsUnderline = !style.IsUnderline });
            case TextEditCommand.ToggleStrike:
                return TransformTextStyle(static style => style with { IsStrike = !style.IsStrike });
            case TextEditCommand.EndEditing:
                EndEditing();
                return true;
            case TextEditCommand.InsertLineBreak:
                return ReplaceSelection("\n");
            case TextEditCommand.DeleteBackward:
                return DeleteAtCaret(forward: false);
            case TextEditCommand.DeleteForward:
                return DeleteAtCaret(forward: true);
            case TextEditCommand.CaretLeft:
                MoveCaretByCluster(-1, request.Extend);
                return true;
            case TextEditCommand.CaretRight:
                MoveCaretByCluster(1, request.Extend);
                return true;
            case TextEditCommand.CaretUp:
                MoveCaretByLine(-1, request.Extend);
                return true;
            case TextEditCommand.CaretDown:
                MoveCaretByLine(1, request.Extend);
                return true;
            case TextEditCommand.CaretLineStart:
                MoveCaretToLineEdge(first: true, request.Extend);
                return true;
            case TextEditCommand.CaretLineEnd:
                MoveCaretToLineEdge(first: false, request.Extend);
                return true;
            case TextEditCommand.SelectAll:
                SelectAll();
                return true;
            case TextEditCommand.Copy:
                Copy();
                return true;
            case TextEditCommand.Cut:
                Cut();
                return true;
            case TextEditCommand.Paste:
                Paste();
                return true;
            default:
                return false;
        }
    }

    /// <summary>文字入力を処理する。</summary>
    /// <param name="e">入力の引数。</param>
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);

        if (!_isInputEnabled || !IsEditing || char.IsControl(e.KeyChar))
        {
            return;
        }

        ReplaceSelection(e.KeyChar.ToString());
        e.Handled = true;
    }

    /// <summary>ポインターが押されたときの処理（キャレットの移動・選択の開始）。</summary>
    /// <param name="e">ポインターの引数。</param>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (!_isInputEnabled || e.Button != MouseButtons.Left)
        {
            return;
        }

        // **押下で編集を始める**: 利用者は「編集を開始」を押さずに本文へ書き始められる
        BeginEditing();
        Focus();
        var extend = (ModifierKeys & Keys.Shift) != 0;
        SetCaretFromPoint(ToSource(e.Location), extend);
        _isSelectingText = true;
        Capture = true;
    }

    /// <summary>ポインターが動いたときの処理（選択の拡張）。</summary>
    /// <param name="e">ポインターの引数。</param>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_isSelectingText)
        {
            SetCaretFromPoint(ToSource(e.Location), extend: true);
        }
    }

    /// <summary>ポインターが離されたときの処理（選択の終了）。</summary>
    /// <param name="e">ポインターの引数。</param>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (_isSelectingText)
        {
            _isSelectingText = false;
            Capture = false;
        }
    }

    /// <summary>本文を折り返して行へ配置する（表示と編集で同じ結果を使う）。</summary>
    /// <returns>行の並び（大きさが 0 の場合は空）。</returns>
    /// <summary>描画に使う本文を取得する（変換中の文字列を含む）。</summary>
    /// <value>変換中でなければ <see cref="RichTextValue"/>。 変換中は変換中の文字列を入れた描画用の値。</value>
    /// <remarks>
    /// <b>変換中の文字列は本文へ入れない</b>: 表示だけに入れる（確定したときだけ本文が変わる）。
    /// 入れないと、入力中の文字が表示されず「入力が反映されていない」ように見える。
    /// </remarks>
    internal RichText DisplayText => TextCompositionPreview.Apply(RichTextValue, _edit.CaretIndex, _preeditText);

    /// <summary>本文を折り返して行へ配置する（表示と編集で同じ結果を使う）。</summary>
    /// <param name="text">折り返す本文（変換中の文字列を含む表示用の値）。</param>
    /// <returns>行の並び。</returns>
    private IReadOnlyList<TextLinePlan> ResolveLines(RichText text)
    {
        var scale = ResolveScale();
        var width = Width / scale;
        var height = Height / scale;
        if (width <= 0 || height <= 0)
        {
            return [];
        }

        return TextLayout.ResolveLines(text, new EsRect(0, 0, width, height), MeasureText, LineHeightFactor);
    }

    /// <summary>行と文字位置の対応を作る。</summary>
    /// <returns>対応。 行が無い場合は <see langword="null"/>。</returns>
    /// <remarks>
    /// <b>表示と同じ本文で作る</b>: 変換中の文字列を入れないと、キャレットの位置が確定後の位置と食い違う。
    /// </remarks>
    private TextLineIndex? ResolveLineIndex()
    {
        var displayed = DisplayText;
        return TextLineIndex.Create(ResolveLines(displayed), displayed.PlainText);
    }

    /// <summary>表示倍率を取得する（1 未満・非有限は 1）。</summary>
    /// <returns>表示倍率。</returns>
    private double ResolveScale()
    {
        var scale = RenderScale;
        return double.IsFinite(scale) && scale > 0 ? scale : 1.0;
    }

    /// <summary>変更後の本文を書き戻し、通知する。</summary>
    /// <param name="updated">変更後の本文。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    private bool Commit(RichText updated)
    {
        if (ReferenceEquals(updated, RichTextValue))
        {
            AfterSelectionChanged();
            return false;
        }

        RichTextValue = updated;
        RichTextChanged?.Invoke(this, EventArgs.Empty);
        RestartCaretBlink();
        AfterSelectionChanged();
        return true;
    }

    /// <summary>選択範囲が変わったことを通知して描画を更新する。</summary>
    private void AfterSelectionChanged()
    {
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    /// <summary>キャレットを書記素クラスタの境界へ揃える。</summary>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    private void SnapCaretToCluster(bool extend)
    {
        var text = RichTextValue.PlainText;
        var snapped = TextCursor.SnapBackward(text, _edit.CaretIndex);
        if (snapped != _edit.CaretIndex)
        {
            _edit.MoveTo(snapped, text.Length, extend);
        }
    }

    /// <summary>位置（このコントロールの DIP）を元画像ピクセルへ写す。</summary>
    /// <param name="point">位置（DIP）。</param>
    /// <returns>位置（元画像ピクセル）。</returns>
    private EsPoint ToSource(Point point)
    {
        var scale = ResolveScale();
        return new EsPoint(point.X / scale, point.Y / scale);
    }

    /// <summary>入力を本文の改行の規則（<c>\n</c>）へ揃える。</summary>
    /// <param name="text">入力された文字列。</param>
    /// <returns>揃えた文字列。</returns>
    private static string NormalizeInput(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    /// <summary>キャレットを点灯して描くかどうかを取得する。</summary>
    private bool IsCaretVisible => TextCaretBlink.IsVisible(DateTimeOffset.UtcNow - _caretBlinkStart);

    /// <summary>キャレットの点滅を点灯から始め直す。</summary>
    private void RestartCaretBlink()
    {
        _caretBlinkStart = DateTimeOffset.UtcNow;
        if (!_caretTimer.Enabled)
        {
            _caretTimer.Start();
        }
    }

    /// <summary>UI の修飾キーを本層の修飾キーへ写す。</summary>
    /// <param name="keyData">キーと修飾キー（<see cref="Keys"/> の組み合わせ）。</param>
    /// <returns>本層の修飾キー。</returns>
    private static TextEditModifiers ToTextEditModifiers(Keys keyData)
    {
        var result = TextEditModifiers.None;
        if ((keyData & Keys.Control) != 0)
        {
            result |= TextEditModifiers.Control;
        }

        if ((keyData & Keys.Shift) != 0)
        {
            result |= TextEditModifiers.Shift;
        }

        if ((keyData & Keys.Alt) != 0)
        {
            result |= TextEditModifiers.Alt;
        }

        return result;
    }

    /// <summary>UI のキーを本層のキーへ写す（本層が扱うキーだけ）。</summary>
    /// <param name="key">UI のキー。</param>
    /// <returns>本層のキー。</returns>
    private static TextEditKey ToTextEditKey(Keys key) => key switch
    {
        Keys.A => TextEditKey.A,
        Keys.B => TextEditKey.B,
        Keys.C => TextEditKey.C,
        Keys.I => TextEditKey.I,
        Keys.U => TextEditKey.U,
        Keys.V => TextEditKey.V,
        Keys.X => TextEditKey.X,
        Keys.D5 or Keys.NumPad5 => TextEditKey.Digit5,
        Keys.Escape => TextEditKey.Escape,
        Keys.Enter => TextEditKey.Enter,
        Keys.Back => TextEditKey.Back,
        Keys.Delete => TextEditKey.Delete,
        Keys.Left => TextEditKey.Left,
        Keys.Right => TextEditKey.Right,
        Keys.Up => TextEditKey.Up,
        Keys.Down => TextEditKey.Down,
        Keys.Home => TextEditKey.Home,
        Keys.End => TextEditKey.End,
        _ => TextEditKey.None,
    };
    /// <summary>IME の変換内容をウィンドウ メッセージから受け取る。</summary>
    /// <param name="m">メッセージ。</param>
    /// <remarks>
    /// <b>変換中の文字列は本文へ入れない</b>: 表示にだけ入れ、確定した文字列だけを本文へ入れる
    /// （変換を取り消しても本文が変わらない）。 入力欄を重ねないため、変換の窓口は本コントロールが持つ。
    /// </remarks>
    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WmImeStartComposition:
                SetPreedit(string.Empty);
                break;
            case WmImeEndComposition:
                SetPreedit(string.Empty);
                break;
            case WmImeComposition:
                HandleImeComposition((int)m.LParam);
                break;
            default:
                break;
        }

        base.WndProc(ref m);
    }

    /// <summary>IME の変換内容を本文へ反映する。</summary>
    /// <param name="flags">変換の指定（<c>GCS_*</c> の組み合わせ）。</param>
    private void HandleImeComposition(int flags)
    {
        if (!_isInputEnabled || !IsEditing)
        {
            return;
        }

        if ((flags & GcsResultStr) != 0)
        {
            var committed = ReadCompositionString(GcsResultStr);
            SetPreedit(string.Empty);
            if (committed.Length > 0)
            {
                ReplaceSelection(NormalizeInput(committed));
                PositionCompositionWindow();
                return;
            }
        }

        if ((flags & GcsCompStr) != 0)
        {
            SetPreedit(ReadCompositionString(GcsCompStr));
            PositionCompositionWindow();
        }
    }

    /// <summary>変換候補のウィンドウをキャレットの位置へ置く。</summary>
    /// <remarks>
    /// <b>位置を指定しないと候補が画面の隅に出る</b>: 入力欄を重ねないため、キャレットの位置を IME へ
    /// 伝えるのは本コントロールの責務である（変換中の文字の末尾へ出す）。
    /// </remarks>
    private void PositionCompositionWindow()
    {
        var context = ImmGetContext(Handle);
        if (context == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var caret = CaretRect;
            var form = new CompositionForm
            {
                dwStyle = CfsPoint,
                ptCurrentPos = new NativePoint
                {
                    X = (int)Math.Round(caret.X),
                    Y = (int)Math.Round(caret.Bottom),
                },
            };

            _ = ImmSetCompositionWindow(context, ref form);
        }
        finally
        {
            _ = ImmReleaseContext(Handle, context);
        }
    }

    /// <summary>IME の変換文字列を読み取る。</summary>
    /// <param name="kind">読み取る種類（<c>GCS_COMPSTR</c> / <c>GCS_RESULTSTR</c>）。</param>
    /// <returns>読み取った文字列（取得できない場合は空）。</returns>
    private string ReadCompositionString(int kind)
    {
        var context = ImmGetContext(Handle);
        if (context == IntPtr.Zero)
        {
            return string.Empty;
        }

        try
        {
            var size = ImmGetCompositionStringW(context, kind, null, 0);
            if (size <= 0)
            {
                return string.Empty;
            }

            var buffer = new byte[size];
            var read = ImmGetCompositionStringW(context, kind, buffer, size);
            return read <= 0 ? string.Empty : Encoding.Unicode.GetString(buffer, 0, read);
        }
        finally
        {
            _ = ImmReleaseContext(Handle, context);
        }
    }

    /// <summary>IME の変換中の文字列を設定する（本文へは入れない）。</summary>
    /// <param name="value">変換中の文字列。</param>
    private void SetPreedit(string value) => PreeditText = value;

    /// <summary>IME の変換開始（<c>WM_IME_STARTCOMPOSITION</c>）。</summary>
    private const int WmImeStartComposition = 0x0105;

    /// <summary>IME の変換終了（<c>WM_IME_ENDCOMPOSITION</c>）。</summary>
    private const int WmImeEndComposition = 0x0106;

    /// <summary>IME の変換内容（<c>WM_IME_COMPOSITION</c>）。</summary>
    private const int WmImeComposition = 0x010F;

    /// <summary>確定した文字列（<c>GCS_RESULTSTR</c>）。</summary>
    private const int GcsResultStr = 0x0800;

    /// <summary>変換中の文字列（<c>GCS_COMPSTR</c>）。</summary>
    private const int GcsCompStr = 0x0008;

    /// <summary>変換候補の位置を指定する（<c>CFS_POINT</c>）。</summary>
    private const int CfsPoint = 0x0002;

    /// <summary>キャレットの矩形を渡すための引数。</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct CompositionForm
    {
        /// <summary>指定の種類（<c>CFS_POINT</c> など）。</summary>
        public int dwStyle;

        /// <summary>候補の位置。</summary>
        public NativePoint ptCurrentPos;

        /// <summary>除外する矩形（使わない）。</summary>
        public NativeRectangle rcArea;
    }

    /// <summary>整数の点。</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        /// <summary>X 座標。</summary>
        public int X;

        /// <summary>Y 座標。</summary>
        public int Y;
    }

    /// <summary>整数の矩形。</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        /// <summary>左端。</summary>
        public int Left;

        /// <summary>上端。</summary>
        public int Top;

        /// <summary>右端。</summary>
        public int Right;

        /// <summary>下端。</summary>
        public int Bottom;
    }

    [DllImport("imm32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr ImmGetContext(IntPtr hWnd);

    [DllImport("imm32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);

    [DllImport("imm32.dll", CharSet = CharSet.Unicode, EntryPoint = "ImmGetCompositionStringW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int ImmGetCompositionStringW(IntPtr hIMC, int dwIndex, byte[]? lpBuf, int dwBufLen);

    [DllImport("imm32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool ImmSetCompositionWindow(IntPtr hIMC, ref CompositionForm lpCompositionForm);
}
