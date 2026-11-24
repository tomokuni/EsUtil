using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.AvaloniaUI.RichTextEditor;

/// <summary>本文の編集（入力・削除・キャレット・選択・書式・クリップボード・IME）の配線。</summary>
/// <remarks>
/// <b>状態と規則は <see cref="TextEditState"/> が単一所有する</b>: 本ファイルは UI のイベントをその API へ
/// つなぎ、描画の更新と通知を行うだけである。 キーの割り当ても <see cref="TextEditKeyMap"/> が持つ。<br/>
/// <b>本文の変更は <see cref="Text"/> へ書き戻す</b>: ホストは <see cref="TextChanged"/> を購読して文書へ写す。
/// </remarks>
public sealed partial class RichTextEditorControl
{
    /// <summary>キャレットと選択の状態（規則は提示層が単一所有する）。</summary>
    private TextEditState _edit = new();

    /// <summary>キャレット・選択・書式の状態を取得または設定する。</summary>
    /// <value>既定は本コントロールが作った状態。 ホストが既に状態を持っている場合はその実体を設定する。</value>
    /// <remarks>
    /// <b>状態を 2 つ持たない</b>: ホスト（注釈の編集セッションなど）が状態を持っている場合は、
    /// 同じ実体を設定する（別の状態を持つとキャレットと本文が食い違う）。
    /// </remarks>
    public TextEditState EditState
    {
        get => _edit;
        set
        {
            _edit = value ?? new TextEditState();
            InvalidateVisual();
        }
    }

    /// <summary>IME の変換中の文字列を取得または設定する（本文へは入れない）。</summary>
    /// <value>変換中の文字列。 空なら変換していない。</value>
    /// <remarks>ホストが入力（IME）を持つ場合に、変換中の文字列を描画へ渡すために使う。</remarks>
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
            InvalidateVisual();
        }
    }

    /// <summary>IME の変換中の文字列（本文へは入れない）。</summary>
    private string _preeditText = string.Empty;

    /// <summary>入力を受け付けるかどうかを取得または設定する。</summary>
    /// <value>入力を受け付ける場合は <see langword="true"/>（既定）。 表示専用にする場合は <see langword="false"/>。</value>
    /// <remarks>
    /// <b>ホストが入力を所有する場合に使う</b>: 注釈のレイヤーのように入力とフォーカスを自分で持つホストへ
    /// 重ねるとき、本コントロールがフォーカスや編集状態を持つとキーと IME の宛先が変わってしまう。
    /// </remarks>
    public bool IsInputEnabled { get; set; } = true;

    /// <summary>ポインターで文字を選んでいる最中かどうか。</summary>
    private bool _isSelectingText;

    /// <summary>IME へ渡すクライアント（編集中だけ使う）。</summary>
    private RichTextEditorInputClient? _textInputClient;

    /// <summary>キャレットの点滅を始めた時刻。</summary>
    private DateTimeOffset _caretBlinkStart = DateTimeOffset.UtcNow;

    /// <summary>キャレットの点滅を進めるタイマー（編集中だけ動かす）。</summary>
    private DispatcherTimer? _caretTimer;

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

    /// <summary>本文の編集を始める（キャレットを本文の末尾へ置く）。</summary>
    /// <remarks>フォーカスを移し、キャレットの点滅を点灯から始める。</remarks>
    public void BeginEditing()
    {
        if (!IsInputEnabled)
        {
            return;
        }

        if (_edit.IsActive)
        {
            Focus();
            return;
        }

        _edit.Begin(Text.PlainText.Length);
        Focus();
        RestartCaretBlink();
        RaiseEditingChanged();
        RaiseSelectionChanged();
        InvalidateVisual();
    }

    /// <summary>本文の編集を終える（内容は既に <see cref="Text"/> へ入っている）。</summary>
    public void EndEditing()
    {
        if (!_edit.IsActive)
        {
            return;
        }

        _edit.End();
        _preeditText = string.Empty;
        StopCaretBlink();
        RaiseEditingChanged();
        RaiseSelectionChanged();
        InvalidateVisual();
    }

    /// <summary>選択範囲を指定する。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（文字数。 0 は選択なし）。</param>
    public void Select(int start, int length)
    {
        _edit.Select(start, length, Text.PlainText.Length);
        AfterSelectionChanged();
    }

    /// <summary>本文をすべて選択する。</summary>
    public void SelectAll()
    {
        var length = Text.PlainText.Length;
        _edit.Select(0, length, length);
        AfterSelectionChanged();
    }

    /// <summary>キャレットを指定位置へ移す。</summary>
    /// <param name="index">移動先（文字数。 範囲外は本文の範囲へ丸める）。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    public void MoveCaret(int index, bool extend)
    {
        var text = Text.PlainText;
        var target = Math.Clamp(index, 0, text.Length);

        // **キャレットは書記素クラスタの境界にだけ置く**（サロゲート ペア・結合文字の内側へ入れない）
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
        _edit.MoveByLine(ResolveLineIndex(), Text.PlainText.Length, delta, extend);
        SnapCaretToCluster(extend);
        AfterSelectionChanged();
    }

    /// <summary>キャレットを行の先頭・末尾へ移す。</summary>
    /// <param name="first">行の先頭へ移す場合は <see langword="true"/>。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    public void MoveCaretToLineEdge(bool first, bool extend)
    {
        _edit.MoveToLineEdge(ResolveLineIndex(), Text.PlainText.Length, first, extend);
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

        _edit.SetCaretFromPoint(index, point, Text.PlainText.Length, extend, MeasureText);
        AfterSelectionChanged();
    }

    /// <summary>選択範囲を文字列で置き換える（選択が無ければ挿入）。</summary>
    /// <param name="text">挿入する文字列（空なら削除）。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool ReplaceSelection(string text) => Apply(edit => edit.ReplaceSelection(Text, text));

    /// <summary>指定した範囲を文字列で置き換える（ホストが範囲を指定する場合）。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="end">終了位置（文字数）。</param>
    /// <param name="text">置き換える文字列（空なら削除）。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool ReplaceRange(int start, int end, string text)
        => Apply(edit => edit.ReplaceRange(Text, start, end, text));

    /// <summary>選択範囲、またはキャレットの前後の 1 クラスタを削除する。</summary>
    /// <param name="forward">キャレットの次の文字を削除する場合は <see langword="true"/>。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool DeleteAtCaret(bool forward) => Apply(edit => edit.Delete(Text, forward));

    /// <summary>選択範囲の書式を丸ごと適用する（選択が無ければ以降の入力へ回す）。</summary>
    /// <param name="style">適用する書式。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool ApplyTextStyle(TextStyle style) => Apply(edit => edit.ApplyStyle(Text, style));

    /// <summary>指定した範囲の書式を丸ごと適用する（ホストが範囲を指定する場合）。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（文字数。 0 以下は本文全体）。</param>
    /// <param name="style">適用する書式。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool ApplyTextStyle(int start, int length, TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);

        var updated = TextStyleEdit.ResolveRange(start, length) is { } range
            ? Text.ApplyStyle(range.Start, range.Length, style)
            : Text;
        _edit.Remember(style);
        return Commit(updated);
    }

    /// <summary>選択範囲の書式を 1 属性だけ変換する（選択が無ければ以降の入力へ回す）。</summary>
    /// <param name="transform">現在の書式を受け取って新しい書式を返す関数。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool TransformTextStyle(Func<TextStyle, TextStyle> transform)
        => Apply(edit => edit.TransformStyle(Text, transform));

    /// <summary>指定した範囲の書式を 1 属性だけ変換する（ホストが範囲を指定する場合）。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（文字数。 0 以下は本文全体）。</param>
    /// <param name="transform">現在の書式を受け取って新しい書式を返す関数。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool TransformTextStyle(int start, int length, Func<TextStyle, TextStyle> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        var at = Math.Clamp(start, 0, Text.PlainText.Length);
        var next = transform(Text.StyleAt(at));
        _edit.Remember(next);
        return Commit(Text.TransformStyle(start, length, _ => next));
    }

    /// <summary>書式バーへ表示する書式を取得する（選択が無い間は覚えた書式）。</summary>
    public TextStyle SelectedTextStyle => _edit.ResolveDisplayStyle(Text);

    /// <summary>選択範囲の文字がすべて条件を満たすかどうかを取得する。</summary>
    /// <param name="match">書式が条件を満たすかどうかを返す関数。</param>
    /// <returns>すべて満たす場合は <see langword="true"/>。</returns>
    public bool SelectedTextMatches(Func<TextStyle, bool> match) => _edit.AllMatch(Text, match);

    /// <summary>選択中の文字列を取得する（クリップボードへ渡す）。</summary>
    /// <returns>選択中の文字列（選択が無い場合は空）。</returns>
    public string SelectedText()
    {
        var text = Text.PlainText;
        var start = Math.Clamp(SelectionStart, 0, text.Length);
        var end = Math.Clamp(SelectionEnd, start, text.Length);
        return text[start..end];
    }

    /// <summary>選択中の文字列をクリップボードへコピーする。</summary>
    /// <returns>コピーの非同期処理。</returns>
    public async Task CopyAsync()
    {
        var text = SelectedText();
        if (text.Length == 0)
        {
            return;
        }

        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    /// <summary>選択中の文字列を切り取る。</summary>
    /// <returns>切り取りの非同期処理。</returns>
    public async Task CutAsync()
    {
        if (SelectedText().Length == 0)
        {
            return;
        }

        await CopyAsync();
        ReplaceSelection(string.Empty);
    }

    /// <summary>クリップボードの文字列を挿入する。</summary>
    /// <returns>貼り付けの非同期処理。</returns>
    public async Task PasteAsync()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        var text = await clipboard.TryGetTextAsync();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        ReplaceSelection(NormalizeInput(text));
    }

    /// <summary>キャレットの矩形（このコントロールの座標の DIP）を取得する。</summary>
    /// <returns>キャレットの矩形。 解決できない場合は空の矩形。</returns>
    public Rect CaretRect
    {
        get
        {
            if (ResolveLineIndex() is not { } index
                || index.ResolveCaret(_edit.CaretIndex, MeasureText) is not { } caret)
            {
                return default;
            }

            var scale = ResolveScale();
            return new Rect(
                caret.X * scale,
                caret.Y * scale,
                TextEditorAppearance.CaretThicknessDip,
                caret.Height * scale);
        }
    }

    /// <summary>IME とやり取りするクライアントを取得する（IME はライブラリから制御できないため例外扱い）。</summary>
    internal RichTextEditorInputClient InputClient => _textInputClient ??= new RichTextEditorInputClient(this);

    /// <summary>本文の文字列（IME へ渡す）。</summary>
    internal string TextForInput => Text.PlainText;

    /// <summary>IME から選択範囲を設定する。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="end">終了位置（文字数）。</param>
    internal void SetSelectionFromInput(int start, int end)
    {
        var length = Text.PlainText.Length;
        var from = Math.Clamp(start, 0, length);
        var to = Math.Clamp(end, from, length);
        _edit.Select(from, to - from, length);
        InvalidateVisual();
    }

    /// <summary>IME の変換中文字列を設定する（本文へは入れない）。</summary>
    /// <param name="preeditText">変換中の文字列（<see langword="null"/> は変換の終了）。</param>
    internal void SetPreeditTextForInput(string? preeditText)
    {
        _preeditText = preeditText ?? string.Empty;
        InputClient.NotifySelectionChanged();
        InvalidateVisual();
    }

    /// <summary>キーが押されたときの処理（割り当ては <see cref="TextEditKeyMap"/> が単一所有する）。</summary>
    /// <param name="e">キーの引数。</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!IsInputEnabled || !IsEditing || e.Handled)
        {
            return;
        }

        var request = TextEditKeyMap.Resolve(ToTextEditKey(e.Key), ToTextEditModifiers(e.KeyModifiers));
        switch (request.Command)
        {
            case TextEditCommand.ToggleBold:
                e.Handled = TransformTextStyle(style => style with { IsBold = !style.IsBold });
                return;
            case TextEditCommand.ToggleItalic:
                e.Handled = TransformTextStyle(style => style with { IsItalic = !style.IsItalic });
                return;
            case TextEditCommand.ToggleUnderline:
                e.Handled = TransformTextStyle(style => style with { IsUnderline = !style.IsUnderline });
                return;
            case TextEditCommand.ToggleStrike:
                e.Handled = TransformTextStyle(style => style with { IsStrike = !style.IsStrike });
                return;
            case TextEditCommand.EndEditing:
                EndEditing();
                e.Handled = true;
                return;
            case TextEditCommand.InsertLineBreak:
                e.Handled = ReplaceSelection("\n");
                return;
            case TextEditCommand.DeleteBackward:
                e.Handled = DeleteAtCaret(forward: false);
                return;
            case TextEditCommand.DeleteForward:
                e.Handled = DeleteAtCaret(forward: true);
                return;
            case TextEditCommand.CaretLeft:
                MoveCaretByCluster(-1, request.Extend);
                e.Handled = true;
                return;
            case TextEditCommand.CaretRight:
                MoveCaretByCluster(1, request.Extend);
                e.Handled = true;
                return;
            case TextEditCommand.CaretUp:
                MoveCaretByLine(-1, request.Extend);
                e.Handled = true;
                return;
            case TextEditCommand.CaretDown:
                MoveCaretByLine(1, request.Extend);
                e.Handled = true;
                return;
            case TextEditCommand.CaretLineStart:
                MoveCaretToLineEdge(first: true, request.Extend);
                e.Handled = true;
                return;
            case TextEditCommand.CaretLineEnd:
                MoveCaretToLineEdge(first: false, request.Extend);
                e.Handled = true;
                return;
            case TextEditCommand.SelectAll:
                SelectAll();
                e.Handled = true;
                return;
            case TextEditCommand.Copy:
                _ = CopyAsync();
                e.Handled = true;
                return;
            case TextEditCommand.Cut:
                _ = CutAsync();
                e.Handled = true;
                return;
            case TextEditCommand.Paste:
                _ = PasteAsync();
                e.Handled = true;
                return;
        }
    }

    /// <summary>文字入力を処理する（IME の確定文字列もここへ来る）。</summary>
    /// <param name="e">入力の引数。</param>
    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);

        if (!IsInputEnabled || !IsEditing || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        ReplaceSelection(NormalizeInput(e.Text));
        e.Handled = true;
    }

    /// <summary>ポインターが押されたときの処理（編集の開始・キャレットの移動・選択の開始）。</summary>
    /// <param name="e">ポインターの引数。</param>
    /// <remarks>
    /// <b>押下で編集を始める</b>: 表示専用でない限り、本文を押したらその位置から編集できる（利用者は
    /// 「編集を開始」を押さずに書き始められる）。 既に編集中ならフォーカスを取り戻してキャレットを移す
    /// （書式バーなど別の要素を触った後に、本文へ戻ってそのまま入力できる）。
    /// </remarks>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!IsInputEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        BeginEditing();
        Focus();

        var extend = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        SetCaretFromPoint(ToSource(e.GetPosition(this)), extend);
        _isSelectingText = true;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <summary>ポインターが動いたときの処理（選択の拡張）。</summary>
    /// <param name="e">ポインターの引数。</param>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_isSelectingText)
        {
            return;
        }

        SetCaretFromPoint(ToSource(e.GetPosition(this)), extend: true);
        e.Handled = true;
    }

    /// <summary>ポインターが離されたときの処理（選択の終了）。</summary>
    /// <param name="e">ポインターの引数。</param>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_isSelectingText)
        {
            return;
        }

        _isSelectingText = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /// <summary>本文へ編集を適用して書き戻す。</summary>
    /// <param name="update">編集の状態を受け取り、変更後の本文を返す関数。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    private bool Apply(Func<TextEditState, RichText> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        return Commit(update(_edit));
    }

    /// <summary>変更後の本文を <see cref="Text"/> へ書き戻し、通知する。</summary>
    /// <param name="updated">変更後の本文。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    private bool Commit(RichText updated)
    {
        if (ReferenceEquals(updated, Text))
        {
            AfterSelectionChanged();
            return false;
        }

        Text = updated;
        TextChanged?.Invoke(this, EventArgs.Empty);

        // **本文が変わったことを IME へ知らせる**（周囲の文字列を渡し直し、変換の基準を最新にする）
        InputClient.NotifyTextChanged();
        RestartCaretBlink();
        AfterSelectionChanged();
        return true;
    }

    /// <summary>選択範囲が変わったことを通知して描画を更新する。</summary>
    private void AfterSelectionChanged()
    {
        InputClient.NotifySelectionChanged();
        RaiseSelectionChanged();
        InvalidateVisual();
    }

    /// <summary>キャレットを書記素クラスタの境界へ揃える（行の移動で境界の内側に落ちたとき）。</summary>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    private void SnapCaretToCluster(bool extend)
    {
        var text = Text.PlainText;
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
    /// <value>点滅の位相が点灯の間は <see langword="true"/>。</value>
    private bool IsCaretVisible => TextCaretBlink.IsVisible(DateTimeOffset.UtcNow - _caretBlinkStart);

    /// <summary>キャレットの点滅を点灯から始め直す。</summary>
    /// <remarks>消えた瞬間に操作すると入力位置を見失うため、操作の直後は必ず点灯にします。</remarks>
    private void RestartCaretBlink()
    {
        _caretBlinkStart = DateTimeOffset.UtcNow;
        _caretTimer ??= CreateCaretTimer();
        if (!_caretTimer.IsEnabled)
        {
            _caretTimer.Start();
        }
    }

    /// <summary>キャレットの点滅を点灯から始め直す（ホストが入力を持つ場合に呼ぶ）。</summary>
    /// <remarks>
    /// <b>ホストが入力を持つ場合</b>: 本文の編集をホスト（注釈のレイヤーなど）が行うときは
    /// <see cref="BeginEditing"/> を通らないため、点滅の起点を進める入口が要る。
    /// </remarks>
    public void RefreshCaretBlink()
    {
        if (IsEditing)
        {
            RestartCaretBlink();
            InvalidateVisual();
        }
    }

    /// <summary>キャレットの点滅を止める（編集を終えたとき）。</summary>
    private void StopCaretBlink() => _caretTimer?.Stop();

    /// <summary>キャレットの点滅を進めるタイマーを作る。</summary>
    /// <returns>タイマー。</returns>
    private DispatcherTimer CreateCaretTimer()
    {
        var timer = new DispatcherTimer { Interval = TextCaretBlink.Interval };
        timer.Tick += (_, _) =>
        {
            if (IsEditing)
            {
                InvalidateVisual();
            }
            else
            {
                timer.Stop();
            }
        };
        return timer;
    }

    /// <summary><see cref="SelectionChanged"/> を発生させる。</summary>
    private void RaiseSelectionChanged() => SelectionChanged?.Invoke(this, EventArgs.Empty);

    /// <summary><see cref="EditingChanged"/> を発生させる。</summary>
    private void RaiseEditingChanged() => EditingChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>UI の修飾キーを本層の修飾キーへ写す。</summary>
    /// <param name="modifiers">UI の修飾キー。</param>
    /// <returns>本層の修飾キー。</returns>
    private static TextEditModifiers ToTextEditModifiers(KeyModifiers modifiers)
    {
        var result = TextEditModifiers.None;
        if ((modifiers & KeyModifiers.Control) != 0)
        {
            result |= TextEditModifiers.Control;
        }

        if ((modifiers & KeyModifiers.Shift) != 0)
        {
            result |= TextEditModifiers.Shift;
        }

        if ((modifiers & KeyModifiers.Alt) != 0)
        {
            result |= TextEditModifiers.Alt;
        }

        return result;
    }

    /// <summary>UI のキーを本層のキーへ写す（本層が扱うキーだけ）。</summary>
    /// <param name="key">UI のキー。</param>
    /// <returns>本層のキー。 扱わないキーは <see cref="TextEditKey.None"/>。</returns>
    private static TextEditKey ToTextEditKey(Key key) => key switch
    {
        Key.A => TextEditKey.A,
        Key.B => TextEditKey.B,
        Key.C => TextEditKey.C,
        Key.I => TextEditKey.I,
        Key.U => TextEditKey.U,
        Key.V => TextEditKey.V,
        Key.X => TextEditKey.X,
        Key.D5 or Key.NumPad5 => TextEditKey.Digit5,
        Key.Escape => TextEditKey.Escape,
        Key.Enter => TextEditKey.Enter,
        Key.Back => TextEditKey.Back,
        Key.Delete => TextEditKey.Delete,
        Key.Left => TextEditKey.Left,
        Key.Right => TextEditKey.Right,
        Key.Up => TextEditKey.Up,
        Key.Down => TextEditKey.Down,
        Key.Home => TextEditKey.Home,
        Key.End => TextEditKey.End,
        _ => TextEditKey.None,
    };
}
