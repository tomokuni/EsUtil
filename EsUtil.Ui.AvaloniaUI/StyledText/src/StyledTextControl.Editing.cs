using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.AvaloniaUI.StyledText;

/// <summary>本文の編集（入力・削除・キャレット・選択・書式・クリップボード・IME）の配線。</summary>
/// <remarks>
/// <b>状態と規則は <see cref="TextEditor"/> が単一所有する</b>: 本ファイルは UI のイベントをその API へ
/// つなぎ、描画の更新と通知を行うだけである。 キーの割り当ても <see cref="TextEditKeyMap"/> が持つ。<br/>
/// <b>本文の変更は <see cref="RichTextValue"/> へ書き戻す</b>: ホストは <see cref="RichTextChanged"/> を購読して文書へ写す。
/// </remarks>
public sealed partial class StyledTextControl
{
    /// <summary>本文の値と編集の状態（規則は提示層が単一所有する）。</summary>
    private TextEditor _editor = new();

    /// <summary>本文の値と編集の状態を取得または設定する。</summary>
    /// <value>既定は本コントロールが作った実体。 ホストが既に編集器を持っている場合はその実体を設定する。</value>
    /// <remarks>
    /// <b>編集器を 2 つ持たない</b>: ホスト（注釈の編集セッションなど）が本文と状態を持っている場合は、
    /// 同じ実体を設定する（別の実体を持つとキャレットと本文が食い違う）。 状態（<see cref="TextEditState"/>）は
    /// 本プロパティの実体が持つため、個別に設定しない。
    /// </remarks>
    /// <exception cref="ArgumentNullException">設定する値が <see langword="null"/> の場合。</exception>
    public TextEditor Editor
    {
        get => _editor;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (ReferenceEquals(_editor, value))
            {
                return;
            }

            _editor = value;

            // **本文のプロパティも共有した実体へ揃える**: 表示は本文のプロパティから読むため、
            // 揃えないと編集器の本文と画面の本文が食い違う。
            SetCurrentValue(RichTextValueProperty, value.Text);
            InvalidateVisual();
        }
    }

    /// <summary>IME の変換中の文字列を取得または設定する（本文へは入れない）。</summary>
    /// <value>変換中の文字列。 空なら変換していない。</value>
    /// <remarks>ホストが入力（IME）を持つ場合に、変換中の文字列を描画へ渡すために使う。</remarks>
    public string PreeditText
    {
        get => _editor.PreeditText;
        set => SetPreeditText(value);
    }

    /// <summary>入力を受け付けるかどうかを取得または設定する。</summary>
    /// <value>入力を受け付ける場合は <see langword="true"/>（既定）。 表示専用にする場合は <see langword="false"/>。</value>
    /// <remarks>
    /// <b>ホストが入力を所有する場合に使う</b>: 注釈のレイヤーのように入力とフォーカスを自分で持つホストへ
    /// 重ねるとき、本コントロールがフォーカスや編集状態を持つとキーと IME の宛先が変わってしまう。
    /// </remarks>
    public bool IsInputEnabled { get; set; } = true;

    /// <summary>編集中に書式バーを自動で表示するかどうかを取得または設定する。</summary>
    /// <value>自動で表示する場合は <see langword="true"/>（既定）。 ホストが独自にバーを出す場合は <see langword="false"/>。</value>
    /// <remarks>
    /// <b>表示の判断は <see cref="ShouldShowFormatBar"/> が持つ</b>: 自動表示が有効でも、1 文字以上を選ぶまでは
    /// 表示しない（適用先が決まらないため）。 ホスト（注釈のパネルなど）は本プロパティを読んでバーを出し入れする。
    /// </remarks>
    public bool ShowFormatBarWhileEditing
    {
        get => _editor.ShowFormatBarWhileEditing;
        set => _editor.ShowFormatBarWhileEditing = value;
    }

    /// <summary>ポインターで文字を選んでいる最中かどうか。</summary>
    private bool _isSelectingText;

    /// <summary>IME へ渡すクライアント（編集中だけ使う）。</summary>
    private StyledTextInputClient? _textInputClient;

    /// <summary>キャレットの点滅を始めた時刻。</summary>
    private DateTimeOffset _caretBlinkStart = DateTimeOffset.UtcNow;

    /// <summary>キャレットの点滅を進めるタイマー（編集中だけ動かす）。</summary>
    private DispatcherTimer? _caretTimer;

    /// <summary>キャレットの位置（文字数）を取得する。</summary>
    public int CaretIndex => _editor.CaretIndex;

    /// <summary>選択範囲の開始位置（文字数）を取得する。</summary>
    public int SelectionStart => _editor.SelectionStart;

    /// <summary>選択範囲の終了位置（文字数）を取得する。</summary>
    public int SelectionEnd => _editor.SelectionEnd;

    /// <summary>選択範囲の長さ（文字数）を取得する。</summary>
    public int SelectionLength => _editor.SelectionLength;

    /// <summary>本文の文字を選んでいるかどうかを取得する。</summary>
    public bool HasSelection => _editor.HasSelection;

    /// <summary>本文の編集を始める（キャレットを本文の末尾へ置く）。</summary>
    /// <remarks>フォーカスを移し、キャレットの点滅を点灯から始める。</remarks>
    public void BeginEditing()
    {
        if (!IsInputEnabled)
        {
            return;
        }

        if (_editor.Begin() == TextEditChange.None)
        {
            Focus();
            return;
        }

        Focus();
        RestartCaretBlink();
        ApplyChange(TextEditChange.Editing | TextEditChange.Selection);
    }

    /// <summary>本文の編集を終える（内容は既に <see cref="RichTextValue"/> へ入っている）。</summary>
    public void EndEditing()
    {
        if (_editor.End() == TextEditChange.None)
        {
            return;
        }

        StopCaretBlink();
        ApplyChange(TextEditChange.Editing | TextEditChange.Selection);
    }

    /// <summary>選択範囲を指定する。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（文字数。 0 は選択なし）。</param>
    public void Select(int start, int length) => ApplyChange(_editor.Select(start, length));

    /// <summary>本文をすべて選択する。</summary>
    public void SelectAll() => ApplyChange(_editor.SelectAll());

    /// <summary>キャレットを指定位置へ移す。</summary>
    /// <param name="index">移動先（文字数。 範囲外は本文の範囲へ丸める）。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    public void MoveCaret(int index, bool extend) => ApplyChange(_editor.MoveCaret(index, extend));

    /// <summary>キャレットを前後の 1 クラスタへ移す（選択があるときは選択の端へ畳む）。</summary>
    /// <param name="direction">移動する向き（負は左、正は右）。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    public void MoveCaretByCluster(int direction, bool extend)
        => ApplyChange(_editor.MoveCaretByCluster(direction, extend));

    /// <summary>キャレットを上下の行の同じ桁へ移す。</summary>
    /// <param name="delta">移動する行数。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    public void MoveCaretByLine(int delta, bool extend)
        => ApplyChange(_editor.MoveCaretByLine(ResolveLineIndex(), delta, extend));

    /// <summary>キャレットを行の先頭・末尾へ移す。</summary>
    /// <param name="first">行の先頭へ移す場合は <see langword="true"/>。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    public void MoveCaretToLineEdge(bool first, bool extend)
        => ApplyChange(_editor.MoveCaretToLineEdge(ResolveLineIndex(), first, extend));

    /// <summary>位置（元画像ピクセル）に最も近い文字位置へキャレットを移す。</summary>
    /// <param name="point">位置（元画像ピクセル）。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    public void SetCaretFromPoint(EsPoint point, bool extend)
    {
        if (ResolveLineIndex() is not { } index)
        {
            return;
        }

        ApplyChange(_editor.SetCaretFromPoint(index, point, extend, MeasureText));
    }

    /// <summary>選択範囲を文字列で置き換える（選択が無ければ挿入）。</summary>
    /// <param name="text">挿入する文字列（空なら削除）。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool ReplaceSelection(string text) => IsTextChanged(_editor.ReplaceSelection(text));

    /// <summary>指定した範囲を文字列で置き換える（ホストが範囲を指定する場合）。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="end">終了位置（文字数）。</param>
    /// <param name="text">置き換える文字列（空なら削除）。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool ReplaceRange(int start, int end, string text) => IsTextChanged(_editor.ReplaceRange(start, end, text));

    /// <summary>選択範囲、またはキャレットの前後の 1 クラスタを削除する。</summary>
    /// <param name="forward">キャレットの次の文字を削除する場合は <see langword="true"/>。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool DeleteAtCaret(bool forward) => IsTextChanged(_editor.DeleteAtCaret(forward));

    /// <summary>選択範囲の書式を丸ごと適用する（選択が無ければ以降の入力へ回す）。</summary>
    /// <param name="style">適用する書式。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool ApplyTextStyle(TextStyle style) => IsTextChanged(_editor.ApplyStyle(style));

    /// <summary>指定した範囲の書式を丸ごと適用する（ホストが範囲を指定する場合）。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（文字数。 0 以下は本文全体）。</param>
    /// <param name="style">適用する書式。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool ApplyTextStyle(int start, int length, TextStyle style)
        => IsTextChanged(_editor.ApplyStyle(start, length, style));

    /// <summary>選択範囲の書式を 1 属性だけ変換する（選択が無ければ以降の入力へ回す）。</summary>
    /// <param name="transform">現在の書式を受け取って新しい書式を返す関数。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool TransformTextStyle(Func<TextStyle, TextStyle> transform)
        => IsTextChanged(_editor.TransformStyle(transform));

    /// <summary>指定した範囲の書式を 1 属性だけ変換する（ホストが範囲を指定する場合）。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（文字数。 0 以下は本文全体）。</param>
    /// <param name="transform">現在の書式を受け取って新しい書式を返す関数。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool TransformTextStyle(int start, int length, Func<TextStyle, TextStyle> transform)
        => IsTextChanged(_editor.TransformStyle(start, length, transform));

    /// <summary>書式バーへ表示する書式を取得する（選択が無い間は覚えた書式）。</summary>
    public TextStyle SelectedTextStyle => _editor.SelectedTextStyle;

    /// <summary>選択範囲の文字がすべて条件を満たすかどうかを取得する。</summary>
    /// <param name="match">書式が条件を満たすかどうかを返す関数。</param>
    /// <returns>すべて満たす場合は <see langword="true"/>。</returns>
    public bool SelectedTextMatches(Func<TextStyle, bool> match) => _editor.SelectedTextMatches(match);

    /// <summary>選択中の文字列を取得する（クリップボードへ渡す）。</summary>
    /// <returns>選択中の文字列（選択が無い場合は空）。</returns>
    public string GetSelectedText() => _editor.GetSelectedText();

    /// <summary>選択中の文字列をクリップボードへコピーする。</summary>
    /// <returns>コピーの非同期処理。</returns>
    public async Task CopyAsync()
    {
        var text = GetSelectedText();
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
        if (GetSelectedText().Length == 0)
        {
            return;
        }

        await CopyAsync();
        ReplaceSelection(string.Empty);
    }

    /// <summary>クリップボードの文字列を挿入する。</summary>
    /// <returns>挿入した文字列（貼り付ける文字列が無い場合は <see langword="null"/>）。</returns>
    public async Task<string?> PasteAsync()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return null;
        }

        var text = await clipboard.TryGetTextAsync();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var normalized = EsUtil.Feature.StyledText.Core.Text.TextInput.Normalize(text);
        ReplaceSelection(normalized);
        return normalized;
    }

    /// <summary>キャレットの矩形（このコントロールの座標の DIP）を取得する。</summary>
    /// <returns>キャレットの矩形。 解決できない場合は空の矩形。</returns>
    public Rect CaretRect
    {
        get
        {
            if (_editor.ResolveCaretRect(ResolveBodySize(), MeasureText, LineHeightFactor) is not { } caret)
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
    internal StyledTextInputClient InputClient => _textInputClient ??= new StyledTextInputClient(this);

    /// <summary>本文の文字列（IME へ渡す）。</summary>
    internal string TextForInput => RichTextValue.PlainText;

    /// <summary>IME から選択範囲を設定する。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="end">終了位置（文字数）。</param>
    internal void SetSelectionFromInput(int start, int end) => ApplyChange(_editor.SelectRange(start, end));

    /// <summary>IME の変換中文字列を設定する（本文へは入れない）。</summary>
    /// <param name="preeditText">変換中の文字列（<see langword="null"/> は変換の終了）。</param>
    internal void SetPreeditTextForInput(string? preeditText) => SetPreeditText(preeditText);

    /// <summary>キーが押されたときの処理（割り当ては <see cref="TextEditKeyMap"/> が単一所有する）。</summary>
    /// <param name="e">キーの引数。</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!e.Handled && TryHandleEditingKey(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
        }
    }

    /// <summary>キーを本文の編集操作へ適用する（ホストの別要素がフォーカスを持つ場合の転送口）。</summary>
    /// <param name="key">キー。</param>
    /// <param name="modifiers">修飾キー。</param>
    /// <returns>処理した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>割り当ては <see cref="TextEditKeyMap"/> が単一所有する</b>: 本コントロールとホストの
    /// 書式バーが同じ規則で動くよう、キーの解釈を 2 か所に書かない。
    /// </remarks>
    internal bool TryHandleEditingKey(Key key, KeyModifiers modifiers)
    {
        if (!IsInputEnabled || !IsEditing)
        {
            return false;
        }

        var request = TextEditKeyMap.Resolve(ToTextEditKey(key), ToTextEditModifiers(modifiers));
        switch (request.Command)
        {
            case TextEditCommand.EndEditing:
                EndEditing();
                return true;
            case TextEditCommand.Copy:
                _ = CopyAsync();
                return true;
            case TextEditCommand.Cut:
                _ = CutAsync();
                return true;
            case TextEditCommand.Paste:
                _ = PasteAsync();
                return true;
        }

        var change = _editor.TryExecute(request, ResolveLineIndex);
        if (change == TextEditChange.None)
        {
            return false;
        }

        ApplyChange(change);
        return true;
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

        ReplaceSelection(e.Text);
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

    /// <summary>編集の結果を <see cref="RichTextValue"/> へ書き戻し、描画と購読者へ通知する。</summary>
    /// <param name="change">変わった内容。</param>
    private void ApplyChange(TextEditChange change)
    {
        if ((change & TextEditChange.Text) != 0)
        {
            RichTextValue = _editor.Text;
            RichTextChanged?.Invoke(this, EventArgs.Empty);

            // **本文が変わったことを IME へ知らせる**（周囲の文字列を渡し直し、変換の基準を最新にする）
            InputClient.NotifyTextChanged();
            RestartCaretBlink();
        }

        if ((change & TextEditChange.Editing) != 0)
        {
            RaiseEditingChanged();
        }

        if (change != TextEditChange.None)
        {
            AfterSelectionChanged();
        }
    }

    /// <summary>本文が変わったかを返す（変わった内容を通知する）。</summary>
    /// <param name="change">変わった内容。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    private bool IsTextChanged(TextEditChange change)
    {
        ApplyChange(change);
        return (change & TextEditChange.Text) != 0;
    }

    /// <summary>選択範囲が変わったことを通知して描画を更新する。</summary>
    private void AfterSelectionChanged()
    {
        InputClient.NotifySelectionChanged();
        RaiseSelectionChanged();
        InvalidateVisual();
    }

    /// <summary>IME の変換中の文字列を設定し、描画と IME へ知らせる。</summary>
    /// <param name="preeditText">変換中の文字列（<see langword="null"/> は変換の終了）。</param>
    private void SetPreeditText(string? preeditText)
    {
        var next = preeditText ?? string.Empty;
        if (string.Equals(_editor.PreeditText, next, StringComparison.Ordinal))
        {
            return;
        }

        _editor.PreeditText = next;
        InputClient.NotifySelectionChanged();
        InvalidateVisual();
    }

    /// <summary>位置（このコントロールの DIP）を元画像ピクセルへ写す。</summary>
    /// <param name="point">位置（DIP）。</param>
    /// <returns>位置（元画像ピクセル）。</returns>
    private EsPoint ToSource(Point point)
    {
        var scale = ResolveScale();
        return new EsPoint(point.X / scale, point.Y / scale);
    }

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
