using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Rendering;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.MewUI.StyledText;

/// <summary>本文を表示し、その場で編集するコントロール（表示と編集を 1 つの部品にまとめる）。</summary>
/// <remarks>
/// <b>表示と編集を分けない</b>: 入力欄を重ねる方法では、書式（run 単位の色・太字）が反映されず、
/// 書式バーを操作するたびにフォーカスと選択が外れる。 本コントロールは本文を自分で描き、キー入力のたびに
/// 本文を書き換える（書式はその場で反映される）。<br/>
/// <b>規則は提示層が単一所有する</b>: キャレット・選択・書式の適用は <see cref="TextEditState"/>、
/// キーの割り当ては <see cref="TextEditKeyMap"/>、折り返しと行の配置は <see cref="TextLayout"/> が決める。<br/>
/// <b>座標は元画像ピクセル</b>である: <see cref="RenderScale"/> で画面の DIP へ写す。
/// </remarks>
public sealed partial class StyledTextControl : Panel
{
    /// <summary>キャレットの点滅を進めるタイマー（編集中だけ動かす）。</summary>
    private readonly DispatcherTimer _caretTimer;

    /// <summary>キャレットと選択の状態（規則は提示層が単一所有する）。</summary>
    private TextEditState _edit = new();

    /// <summary>IME の変換中の文字列（本文へは入れない）。</summary>
    private string _preeditText = string.Empty;

    /// <summary>表示する本文。</summary>
    private RichText _text = RichText.Empty;

    /// <summary>キャレットの点滅を始めた時刻。</summary>
    private DateTimeOffset _caretBlinkStart = DateTimeOffset.UtcNow;

    /// <summary>ポインターで文字を選んでいる最中かどうか。</summary>
    private bool _isSelectingText;

    /// <summary>本文の編集と表示を担うコントロールを初期化する。</summary>
    public StyledTextControl()
    {
        _caretTimer = new DispatcherTimer(TextCaretBlink.Interval);
        _caretTimer.Tick += () =>
        {
            if (IsEditing)
            {
                InvalidateVisual();
            }
            else
            {
                _caretTimer.Stop();
            }
        };
    }

    /// <summary>表示する本文を取得または設定する。</summary>
    public RichText RichTextValue
    {
        get => _text;
        set
        {
            _text = value ?? RichText.Empty;

            // **本文が外から変わったら編集位置を本文の範囲へ収める**
            if (_edit.IsActive)
            {
                _edit.Clamp(_text.PlainText.Length);
            }

            InvalidateVisual();
        }
    }

    /// <summary>画面の DIP への倍率を取得または設定する。</summary>
    /// <value>1 以上で拡大。 1 未満・非有限は 1 として扱う。</value>
    public double RenderScale { get; set; } = 1.0;

    /// <summary>行間の倍率を取得または設定する。</summary>
    /// <value>1 未満・非有限は 1 として扱う。</value>
    public double LineHeightFactor { get; set; } = 1.0;

    /// <summary>文字列と書式から実寸を測る関数を取得または設定する。</summary>
    /// <value>既定は本コントロールの描画 API で測る関数。</value>
    public Func<string, TextStyle, EsSize> MeasureText { get; set; } = RichTextPainter.Measure;

    /// <summary>キャレット・選択・書式の状態を取得または設定する。</summary>
    /// <value>既定は本コントロールが作った状態。 ホストが既に状態を持っている場合はその実体を設定する。</value>
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

    /// <summary>本文を編集中かどうかを取得する。</summary>
    public bool IsEditing => _edit.IsActive;

    /// <summary>編集中に書式バーを自動で表示するかどうかを取得または設定する。</summary>
    /// <value>自動で表示する場合は <see langword="true"/>（既定）。 ホストが独自にバーを出す場合は <see langword="false"/>。</value>
    /// <remarks>
    /// <b>表示の判断は <see cref="ShouldShowFormatBar"/> が持つ</b>: 自動表示が有効でも、1 文字以上を選ぶまでは
    /// 表示しない（適用先が決まらないため）。 ホスト（注釈のパネルなど）は本プロパティを読んでバーを出し入れする。
    /// </remarks>
    public bool ShowFormatBarWhileEditing { get; set; } = true;

    /// <summary>いま書式バーを表示すべきかどうかを取得する。</summary>
    /// <value>編集中で、自動表示が有効で、1 文字以上を選んでいる場合は <see langword="true"/>。</value>
    public bool ShouldShowFormatBar => _edit.ShouldShowFormatBar(ShowFormatBarWhileEditing);

    /// <summary>入力を受け付けるかどうかを取得または設定する。</summary>
    /// <value>入力を受け付ける場合は <see langword="true"/>（既定）。 表示専用にする場合は <see langword="false"/>。</value>
    /// <remarks>
    /// <b>ホストが入力を所有する場合に使う</b>: 注釈のレイヤーのように入力とフォーカスを自分で持つホストへ
    /// 重ねるとき、本コントロールがフォーカスを奪うとキーと IME の宛先が変わってしまう。
    /// </remarks>
    public bool IsInputEnabled { get; set; } = true;

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
        InvalidateVisual();
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
        InvalidateVisual();
    }

    /// <summary>選択範囲を指定する。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（文字数。 0 は選択なし）。</param>
    public void Select(int start, int length)
    {
        _edit.Select(RichTextValue, start, length);
        AfterSelectionChanged();
    }

    /// <summary>本文をすべて選択する。</summary>
    public void SelectAll()
    {
        _edit.SelectAll(RichTextValue);
        AfterSelectionChanged();
    }

    /// <summary>キャレットを指定位置へ移す。</summary>
    /// <param name="index">移動先（文字数）。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    public void MoveCaret(int index, bool extend)
    {
        _edit.MoveToCluster(RichTextValue, index, extend);
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
        _edit.SnapCaretToCluster(RichTextValue, extend);
        AfterSelectionChanged();
    }

    /// <summary>キャレットを行の先頭・末尾へ移す。</summary>
    /// <param name="first">行の先頭へ移す場合は <see langword="true"/>。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    public void MoveCaretToLineEdge(bool first, bool extend)
    {
        _edit.MoveToLineEdge(ResolveLineIndex(), RichTextValue.PlainText.Length, first, extend);
        _edit.SnapCaretToCluster(RichTextValue, extend);
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
    public string SelectedText() => _edit.GetSelectedText(RichTextValue);

    /// <summary>選択中の文字列をクリップボードへコピーする。</summary>
    /// <returns>コピーの非同期処理。</returns>
    public Task CopyAsync()
    {
        var text = SelectedText();
        if (text.Length > 0)
        {
            _ = RichTextClipboard.TrySetText(text);
        }

        return Task.CompletedTask;
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
    public Task PasteAsync()
    {
        var text = RichTextClipboard.GetText();
        if (text.Length > 0)
        {
            ReplaceSelection(NormalizeInput(text));
        }

        return Task.CompletedTask;
    }

    /// <summary>キャレットの矩形（このコントロールの座標の DIP）を取得する。</summary>
    public Rect CaretRect
    {
        get
        {
            if (ResolveLineIndex() is not { } index || index.ResolveCaret(_edit.CaretIndex, MeasureText) is not { } caret)
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

    /// <summary>キャレットの点滅を点灯から始め直す（ホストが入力を持つ場合に呼ぶ）。</summary>
    public void RefreshCaretBlink()
    {
        if (IsEditing)
        {
            RestartCaretBlink();
            InvalidateVisual();
        }
    }

    /// <summary>本文・選択範囲・キャレットを描く。</summary>
    /// <param name="context">描画先。</param>
    protected override void OnRender(IGraphicsContext context)
    {
        base.OnRender(context);

        RichTextPainter.DpiScale = context.DpiScale;

        var scale = ResolveScale();
        context.Save();
        try
        {
            // **描画の座標はウィンドウ基準である**: 自分の左上を足さないと画像から左上の分だけずれる
            context.Translate(Bounds.X, Bounds.Y);
            context.Scale(scale, scale);
            context.TextPixelSnap = false;

            // **変換中の文字列も本文として描く**（重ねて描くと既存の文字と二重になる）
            var displayed = DisplayText;
            var lines = ResolveLines(displayed);

            // **同じフレームでは同じレイアウト実体を使う**: 文字の描画とキャレット・選択範囲の
            // 位置計算でレイアウトを作り直すと、丸めやキャッシュの差で位置が食い違う。
            var layouts = new TextLayoutCache();
            foreach (var line in lines)
            {
                RichTextPainter.DrawLine(context, line, layouts.Create);
            }

            if (!IsEditing || TextLineIndex.Create(lines, displayed.PlainText) is not { } index)
            {
                return;
            }

            foreach (var rect in index.ResolveSelectionRects(SelectionStart, SelectionEnd, MeasureText))
            {
                var color = TextEditorAppearance.SelectionColor;
                context.FillRectangle(
                    new Rect(rect.X, rect.Y, rect.Width, rect.Height),
                    RichTextPainter.ToMewColor(color));
            }

            // **変換中はキャレットを変換中の文字列の末尾へ置く**
            var caretIndex = TextCompositionPreview.ResolveCaret(_edit.CaretIndex, _preeditText);
            if (index.ResolveCaret(caretIndex, MeasureText) is not { } caret || !IsCaretVisible)
            {
                return;
            }

            var thickness = TextEditorAppearance.CaretThicknessDip / scale;
            context.FillRectangle(
                new Rect(caret.X, caret.Y, thickness, caret.Height),
                RichTextPainter.ToMewColor(TextEditorAppearance.CaretColor));
        }
        finally
        {
            context.Restore();
        }
    }

    /// <summary>同じ矩形の中だけ入力を受ける。</summary>
    /// <param name="point">位置（ウィンドウ座標）。</param>
    /// <returns>自分の場合は自分。</returns>
    protected override UIElement OnHitTest(Point point)
        => IsInputEnabled && IsVisible && IsHitTestVisible && Bounds.Contains(point) ? this : null!;

    /// <summary>キーが押されたときの処理（割り当ては <see cref="TextEditKeyMap"/> が単一所有する）。</summary>
    /// <param name="e">キーの引数。</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (TryHandleEditingKey(e))
        {
            e.Handled = true;
        }
    }

    /// <summary>キーを本文の編集操作へ適用する（ホストの別要素がフォーカスを持つ場合の転送口）。</summary>
    /// <param name="e">キーの引数。</param>
    /// <returns>処理した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>割り当ては <see cref="TextEditKeyMap"/> が単一所有する</b>: 本コントロールとホストの
    /// 書式バーが同じ規則で動くよう、キーの解釈を 2 か所に書かない。
    /// </remarks>
    internal bool TryHandleEditingKey(KeyEventArgs e)
    {
        if (!IsInputEnabled || !IsEditing)
        {
            return false;
        }

        var request = TextEditKeyMap.Resolve(ToTextEditKey(e.Key), ToTextEditModifiers(e));
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
                _ = CopyAsync();
                return true;
            case TextEditCommand.Cut:
                _ = CutAsync();
                return true;
            case TextEditCommand.Paste:
                _ = PasteAsync();
                return true;
            default:
                return false;
        }
    }

    /// <summary>ポインターが押されたときの処理（キャレットの移動・選択の開始）。</summary>
    /// <param name="e">ポインターの引数。</param>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (!IsInputEnabled || !e.LeftButton)
        {
            return;
        }

        // **押下で編集を始める**: 利用者は「編集を開始」を押さずに本文へ書き始められる
        BeginEditing();
        Focus();
        SetCaretFromPoint(ToSource(e.GetPosition(this)), e.ShiftKey);
        _isSelectingText = true;
        CapturePointer();
        e.Handled = true;
    }

    /// <summary>ポインターが動いたときの処理（選択の拡張）。</summary>
    /// <param name="e">ポインターの引数。</param>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_isSelectingText)
        {
            SetCaretFromPoint(ToSource(e.GetPosition(this)), extend: true);
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
            ReleasePointer();
        }
    }

    /// <summary>ポインターのキャプチャを取得する（コントロールの外へ出ても移動を受け取る）。</summary>
    private void CapturePointer()
    {
        if (FindVisualRoot() is Window window)
        {
            window.CaptureMouse(this);
        }
    }

    /// <summary>ポインターのキャプチャを解放する。</summary>
    private void ReleasePointer()
    {
        if (FindVisualRoot() is Window window)
        {
            window.ReleaseMouseCapture();
        }
    }

    /// <summary>描画に使う本文を取得する（変換中の文字列を含む）。</summary>
    /// <value>変換中でなければ <see cref="RichTextValue"/>。 変換中は変換中の文字列を入れた描画用の値。</value>
    /// <remarks>
    /// <b>変換中の文字列は本文へ入れない</b>: 表示だけに入れる（確定したときだけ本文が変わる）。
    /// 入れないと、入力中の文字が表示されず「入力が反映されていない」ように見える。
    /// </remarks>
    internal RichText DisplayText => TextCompositionPreview.Apply(RichTextValue, _edit.CaretIndex, _preeditText);

    /// <summary>本文を折り返して行へ配置する（表示と編集で同じ結果を使う）。</summary>
    /// <param name="text">折り返す本文（変換中の文字列を含む表示用の値）。</param>
    /// <returns>行の並び（大きさが 0 の場合は空）。</returns>
    private IReadOnlyList<TextLinePlan> ResolveLines(RichText text)
    {
        var scale = ResolveScale();
        var width = RenderSize.Width / scale;
        var height = RenderSize.Height / scale;
        if (width <= 0 || height <= 0)
        {
            return [];
        }

        return TextLayout.ResolveLines(text, new EsRect(0, 0, width, height), MeasureText, LineHeightFactor);
    }

    /// <summary>行と文字位置の対応を作る。</summary>
    /// <returns>対応。 行が無い場合は <see langword="null"/>。</returns>
    /// <remarks>
    /// <b>表示と同じ本文で作る</b>: 変換中の文字列を入れないと、キャレットと変換候補の位置が
    /// 確定後の位置と食い違う（入力位置から遠い場所に候補が出る）。
    /// </remarks>
    private TextLineIndex? ResolveLineIndex()
    {
        var displayed = DisplayText;
        return TextLineIndex.Create(ResolveLines(displayed), displayed.PlainText);
    }

    /// <summary>表示倍率を取得する（無効な値は基準値へ正規化する）。</summary>
    /// <returns>表示倍率。</returns>
    /// <remarks>正規化の規則は <see cref="EsUtil.Platform.Core.Coord.DpiScaler.NormalizeScaling"/> が単一所有する。</remarks>
    private double ResolveScale() => EsUtil.Platform.Core.Coord.DpiScaler.NormalizeScaling(RenderScale);

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
        if (!_caretTimer.IsEnabled)
        {
            _caretTimer.Start();
        }
    }

    /// <summary>UI の修飾キーを本層の修飾キーへ写す。</summary>
    /// <param name="e">キーの引数。</param>
    /// <returns>本層の修飾キー。</returns>
    private static TextEditModifiers ToTextEditModifiers(KeyEventArgs e)
    {
        var result = TextEditModifiers.None;
        if (e.ControlKey)
        {
            result |= TextEditModifiers.Control;
        }

        if (e.ShiftKey)
        {
            result |= TextEditModifiers.Shift;
        }

        if (e.AltKey)
        {
            result |= TextEditModifiers.Alt;
        }

        return result;
    }

    /// <summary>UI のキーを本層のキーへ写す（本層が扱うキーだけ）。</summary>
    /// <param name="key">UI のキー。</param>
    /// <returns>本層のキー。</returns>
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
        Key.Backspace => TextEditKey.Back,
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
