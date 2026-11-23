using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Threading;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.AvaloniaUI.Annotation;

/// <summary>注釈レイヤーの本文の編集（表示中のレイアウトのまま、その場で編集する）。</summary>
/// <remarks>
/// <b>入力欄を重ねない</b>: 本文はコントロールが描いているため、入力欄を重ねると書式が反映されず、
/// 書式バーを押すたびにフォーカスが移って選択が分からなくなる。 本文を直接編集し、キー入力のたびに
/// 文書を書き換える（書式はその場で反映される）。<br/>
/// <b>座標の対応</b>: 折り返しは <see cref="AnnotationPresenter"/> が解決した行を使い、行の開始位置は
/// 本文の文字列から数え直す（改行は行の区切りとして 1 文字を消費する）。<br/>
/// <b>日本語入力</b>: <see cref="AnnotationTextInputClient"/> で IME の変換中文字列を受け取り、キャレットの位置へ
/// 下線付きで描く（変換中の文字列は文書へ入れない）。
/// </remarks>
public sealed partial class AnnotationLayerControl
{
    /// <summary>キャレットの太さ（DIP）。</summary>
    private const double CaretThicknessDip = 1.6;

    /// <summary>選択範囲の色（本文の上に薄く重ねる）。</summary>
    private static readonly IBrush s_textSelectionBrush = new SolidColorBrush(Color.FromArgb(72, 0, 120, 215));

    /// <summary>キャレットの色。</summary>
    private static readonly IBrush s_caretBrush = new SolidColorBrush(Color.FromArgb(255, 20, 20, 20));

    /// <summary>本文を編集中かどうか。</summary>
    private bool _isEditingText;

    /// <summary>キャレットの位置（文字数。 0 は先頭）。</summary>
    private int _caretIndex;

    /// <summary>選択の起点（<see cref="_caretIndex"/> と同じなら選択なし）。</summary>
    private int _selectionAnchor;

    /// <summary>IME の変換中文字列（文書へは入れない）。</summary>
    private string _preeditText = string.Empty;

    /// <summary>ポインターで文字を選んでいる最中かどうか。</summary>
    private bool _isSelectingText;

    /// <summary>IME へ渡すクライアント（編集中だけ使う）。</summary>
    private AnnotationTextInputClient? _textInputClient;

    /// <summary>本文の編集中に発生する（開始・終了の両方）。</summary>
    /// <remarks>ホストは書式バーの表示・非表示を切り替える。</remarks>
    public event EventHandler? EditingChanged;

    /// <summary>本文の選択範囲（キャレットの位置）が変わったときに発生する。</summary>
    /// <remarks>
    /// ホストは書式バーの現在値（太字・斜体・下線・取り消し線の押下状態）を更新する。<br/>
    /// 選択の変更では文書は変わらないため、<see cref="DocumentChanged"/> では代用できない。
    /// </remarks>
    public event EventHandler? TextSelectionChanged;

    /// <summary>本文を編集中かどうかを取得する。</summary>
    /// <value>編集中の場合は <see langword="true"/>。</value>
    public bool IsEditingText => _isEditingText;

    /// <summary>キャレットの位置（文字数）を取得する。</summary>
    public int CaretIndex => _caretIndex;

    /// <summary>本文の文字を選んでいるかどうかを取得する（書式の適用先があるか）。</summary>
    /// <value>編集中で、かつ選択範囲に 1 文字以上ある場合は <see langword="true"/>。</value>
    /// <remarks>書式バーはこの値で各操作の有効・無効を切り替える（選択が無いと適用先が決まらない）。</remarks>
    public bool HasTextSelection => _isEditingText && TextSelectionRange.Length > 0;

    /// <summary>選択範囲の開始位置（文字数）を取得する。</summary>
    public int SelectionStart => Math.Min(_selectionAnchor, _caretIndex);

    /// <summary>選択範囲の終了位置（文字数）を取得する。</summary>
    public int SelectionEnd => Math.Max(_selectionAnchor, _caretIndex);

    /// <summary>選択中の本文を編集する（表示中のレイアウトのまま、その場で入力する）。</summary>
    /// <returns>編集を始めた場合は <see langword="true"/>。 枠線を選んでいる場合は <see langword="false"/>。</returns>
    /// <remarks>キャレットは本文の末尾に置く。 選択枠のハンドルは編集中は隠す（本文の見た目を優先する）。</remarks>
    public bool BeginTextEditing()
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return false;
        }

        if (_isEditingText)
        {
            return true;
        }

        _isEditingText = true;
        _preeditText = string.Empty;
        _isSelectingText = false;
        _caretIndex = bubble.Text.PlainText.Length;
        _selectionAnchor = _caretIndex;
        _marquee.ResetSelection();
        SyncTextSelectionRange();
        Focus();
        RestartCaretBlink();
        RaiseEditingChanged();
        InvalidateVisual();
        return true;
    }

    /// <summary>本文の編集を終える（内容は既に文書へ入っている）。</summary>
    public void EndTextEditing()
    {
        if (!_isEditingText)
        {
            return;
        }

        _isEditingText = false;
        _preeditText = string.Empty;
        _isSelectingText = false;
        TextSelectionRange = default;
        StopCaretBlink();

        if (Selected is { } item)
        {
            _marquee.SetSelection(item.Bounds);
        }

        RaiseEditingChanged();
        InvalidateVisual();
    }

    /// <summary>キャレットの点滅を始めた時刻。</summary>
    private DateTimeOffset _caretBlinkStart = DateTimeOffset.UtcNow;

    /// <summary>キャレットの点滅を進めるタイマー（編集中だけ動かす）。</summary>
    private DispatcherTimer? _caretTimer;

    /// <summary>いまキャレットを点灯して描くかどうかを取得する。</summary>
    /// <value>点滅の位相が点灯の間は <see langword="true"/>。</value>
    private bool IsCaretVisible => AnnotationCaretBlink.IsVisible(DateTimeOffset.UtcNow - _caretBlinkStart);

    /// <summary>キャレットの点滅を点灯から始め直す（文字入力・キャレット移動の直後に呼ぶ）。</summary>
    /// <remarks>
    /// <b>点灯から始める</b>: 消えた瞬間に操作すると入力位置を見失うため、操作の直後は必ず点灯にします。
    /// </remarks>
    private void RestartCaretBlink()
    {
        _caretBlinkStart = DateTimeOffset.UtcNow;
        if (_caretTimer is null)
        {
            var timer = new DispatcherTimer { Interval = AnnotationCaretBlink.Interval };
            timer.Tick += (_, _) =>
            {
                if (_isEditingText)
                {
                    InvalidateVisual();
                }
                else
                {
                    timer.Stop();
                }
            };

            _caretTimer = timer;
        }

        if (!_caretTimer.IsEnabled)
        {
            _caretTimer.Start();
        }
    }

    /// <summary>キャレットの点滅を止める（編集を終えたとき）。</summary>
    private void StopCaretBlink() => _caretTimer?.Stop();
    /// <summary>本文の文字列（IME へ渡す）。</summary>
    internal string TextForInput => Selected is BubbleAnnotation bubble ? bubble.Text.PlainText : string.Empty;

    /// <summary>キャレットの矩形（コントロール座標の DIP。 IME の変換候補の位置に使う）。</summary>
    /// <returns>キャレットの矩形。 解決できない場合は空の矩形。</returns>
    internal Rect GetCaretRectForInput()
    {
        if (!TryResolveCaretSource(_caretIndex, out var caret))
        {
            return default;
        }

        var (scale, offset) = ResolveImageTransform();
        return new Rect(
            (caret.X * scale) + offset.X,
            (caret.Y * scale) + offset.Y,
            CaretThicknessDip,
            caret.Height * scale);
    }

    /// <summary>IME から選択範囲を設定する。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="end">終了位置（文字数）。</param>
    internal void SetSelectionFromInput(int start, int end)
    {
        var length = TextForInput.Length;
        _selectionAnchor = Math.Clamp(start, 0, length);
        _caretIndex = Math.Clamp(end, 0, length);
        SyncTextSelectionRange();
        InvalidateVisual();
    }

    /// <summary>IME の変換中文字列を設定する（文書へは入れない）。</summary>
    /// <param name="preeditText">変換中の文字列（<see langword="null"/> は変換の終了）。</param>
    internal void SetPreeditTextForInput(string? preeditText)
    {
        _preeditText = preeditText ?? string.Empty;

        // **変換中の文字列の長さの分だけキャレットを進めて見せる**: 進めないと、変換中の文字が
        // キャレットの位置へ重なって描かれ、確定した文字と位置が食い違って見える（実測 2026-11-22）
        TextInputClient.NotifySelectionChanged();
        InvalidateVisual();
    }

    /// <summary>IME へ渡すクライアントを取得する。</summary>
    internal AnnotationTextInputClient TextInputClient => _textInputClient ??= new AnnotationTextInputClient(this);

    /// <summary>本文の編集キーを、引数の組み立てなしで適用する（書式バーからの転送に使う）。</summary>
    /// <param name="key">キー。</param>
    /// <param name="modifiers">修飾キー。</param>
    /// <returns>処理した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>フォーカスが書式バーへ移っても本文のキー操作を保つ</b>: 書式バーへフォーカスが移ることがあり、
    /// 環境によってはキー入力がバー側へ届く。 その場合もカーソル移動・選択の変更を本文へ適用する。
    /// </remarks>
    internal bool TryHandleEditingKey(Key key, KeyModifiers modifiers)
    {
        if (!_isEditingText)
        {
            return false;
        }

        var args = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
            KeyModifiers = modifiers,
        };
        return HandleTextEditingKey(args);
    }

    /// <summary>本文の編集でキーが押されたときの処理。</summary>
    /// <param name="e">キーの引数。</param>
    /// <returns>処理した場合は <see langword="true"/>。</returns>
    private bool HandleTextEditingKey(KeyEventArgs e)
    {
        var control = (e.KeyModifiers & KeyModifiers.Control) != 0;
        var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        var alt = (e.KeyModifiers & KeyModifiers.Alt) != 0;

        // 装飾のショートカット（一般的な表記に合わせる。 取り消し線は Alt+Shift+5）
        if (control && !shift && !alt)
        {
            switch (e.Key)
            {
                case Key.B:
                    return ToggleDecoration(style => style.IsBold, (style, value) => style with { IsBold = value });
                case Key.I:
                    return ToggleDecoration(style => style.IsItalic, (style, value) => style with { IsItalic = value });
                case Key.U:
                    return ToggleDecoration(style => style.IsUnderline, (style, value) => style with { IsUnderline = value });
            }
        }

        if (alt && shift && !control && e.Key is Key.D5 or Key.NumPad5)
        {
            return ToggleDecoration(style => style.IsStrike, (style, value) => style with { IsStrike = value });
        }

        switch (e.Key)
        {
            case Key.Escape:
                EndTextEditing();
                return true;
            case Key.Enter when control:
                EndTextEditing();
                return true;
            case Key.Enter:
                ReplaceSelection("\n");
                return true;
            case Key.Back:
                DeleteAtCaret(forward: false);
                return true;
            case Key.Delete:
                DeleteAtCaret(forward: true);
                return true;
            case Key.Left:
                MoveCaret(_caretIndex - 1, shift);
                return true;
            case Key.Right:
                MoveCaret(_caretIndex + 1, shift);
                return true;
            case Key.Up:
                MoveCaretToNeighbourLine(-1, shift);
                return true;
            case Key.Down:
                MoveCaretToNeighbourLine(1, shift);
                return true;
            case Key.Home:
                MoveCaretToLineEdge(first: true, shift);
                return true;
            case Key.End:
                MoveCaretToLineEdge(first: false, shift);
                return true;
            case Key.A when control:
                _selectionAnchor = 0;
                _caretIndex = TextForInput.Length;
                SyncTextSelectionRange();
                InvalidateVisual();
                return true;
            case Key.C when control:
                _ = CopySelectionAsync();
                return true;
            case Key.X when control:
                _ = CutSelectionAsync();
                return true;
            case Key.V when control:
                _ = PasteAsync();
                return true;
            default:
                return false;
        }
    }

    /// <summary>文字入力を処理する（IME の確定文字列もここへ来る）。</summary>
    /// <param name="e">入力の引数。</param>
    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);

        if (!_isEditingText || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        ReplaceSelection(e.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", string.Empty, StringComparison.Ordinal));
        e.Handled = true;
    }

    /// <summary>選択範囲を指定された文字列で置き換える。</summary>
    /// <param name="text">置き換える文字列（空なら削除）。</param>
    /// <returns>文書を変えた場合は <see langword="true"/>。</returns>
    private bool ReplaceSelection(string text)
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return false;
        }

        var start = SelectionStart;
        var length = SelectionEnd - SelectionStart;
        if (length == 0 && text.Length == 0)
        {
            return false;
        }

        PrepareUndo();

        // 挿入した文字へ保留している書式を反映する（規則はコアが単一所有する）
        _document = _document.WithText(
            bubble, AnnotationTextStyleEdit.Insert(bubble.Text, start, length, text, _typingStyle.Value));
        RaiseDocumentChanged();

        _caretIndex = start + text.Length;
        _selectionAnchor = _caretIndex;
        SyncTextSelectionRange();

        // **本文が変わったことを IME へ知らせる**（周囲の文字列を渡し直し、変換の基準を最新にする）
        TextInputClient.NotifyTextChanged();
        RestartCaretBlink();
        InvalidateVisual();
        return true;
    }

    /// <summary>選択範囲、またはキャレットの前後の 1 文字を削除する。</summary>
    /// <param name="forward">キャレットの次の文字を削除する場合は <see langword="true"/>。</param>
    private void DeleteAtCaret(bool forward)
    {
        if (SelectionEnd > SelectionStart)
        {
            ReplaceSelection(string.Empty);
            return;
        }

        var index = forward ? _caretIndex : _caretIndex - 1;
        if (index < 0 || index >= TextForInput.Length)
        {
            return;
        }

        _selectionAnchor = index;
        _caretIndex = index + 1;
        ReplaceSelection(string.Empty);
    }

    /// <summary>キャレットを移動する（<paramref name="extend"/> なら選択を広げる）。</summary>
    /// <param name="index">移動先（文字数）。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    private void MoveCaret(int index, bool extend)
    {
        _caretIndex = Math.Clamp(index, 0, TextForInput.Length);
        if (!extend)
        {
            _selectionAnchor = _caretIndex;
        }

        SyncTextSelectionRange();
        InvalidateVisual();
    }

    /// <summary>キャレットを上下の行の同じ桁へ移す。</summary>
    /// <param name="delta">移動する行数。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    private void MoveCaretToNeighbourLine(int delta, bool extend)
    {
        if (ResolveTextLayout() is not { } layout || layout.Lines.Count == 0)
        {
            return;
        }

        var lineIndex = LineIndexOf(layout, _caretIndex);
        var target = Math.Clamp(lineIndex + delta, 0, layout.Lines.Count - 1);
        var column = _caretIndex - layout.Starts[lineIndex];
        MoveCaret(layout.Starts[target] + Math.Min(column, LineTextLength(layout.Lines[target])), extend);
    }

    /// <summary>キャレットを行の先頭・末尾へ移す。</summary>
    /// <param name="first">行の先頭へ移す場合は <see langword="true"/>。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    private void MoveCaretToLineEdge(bool first, bool extend)
    {
        if (ResolveTextLayout() is not { } layout || layout.Lines.Count == 0)
        {
            MoveCaret(first ? 0 : TextForInput.Length, extend);
            return;
        }

        var lineIndex = LineIndexOf(layout, _caretIndex);
        var line = layout.Lines[lineIndex];
        MoveCaret(first ? layout.Starts[lineIndex] : layout.Starts[lineIndex] + LineTextLength(line), extend);
    }

    /// <summary>選択範囲の装飾（太字・斜体・下線・取り消し線）を切り替える。</summary>
    /// <param name="read">属性を読む関数。</param>
    /// <param name="write">属性を書く関数。</param>
    /// <returns>処理した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>切り替えの向きは 1 回だけ決める</b>: 範囲の中に太字と通常が混ざっていても、選んだ文字の先頭の状態で
    /// 「付ける／外す」を決め、範囲の全 run へ同じ値を書く（run ごとに反転すると混ざったままになる）。
    /// </remarks>
    private bool ToggleDecoration(
        Func<AnnotationTextStyle, bool> read,
        Func<AnnotationTextStyle, bool, AnnotationTextStyle> write)
        // 適用先（選択範囲・選択が無ければ以降の入力）の決定は Apply/Transform が単一所有する
        => TransformTextStyle(style => write(style, !read(style)));

    /// <summary>選択範囲を <see cref="TextSelectionRange"/> へ写す（書式の適用先に使う）。</summary>
    private void SyncTextSelectionRange()
    {
        var current = (SelectionStart, SelectionEnd - SelectionStart);
        if (TextSelectionRange == current)
        {
            return;
        }

        TextSelectionRange = current;
        RaiseTextSelectionChanged();
        RestartCaretBlink();

        // **IME へ知らせる**: 知らせないと変換候補が古いキャレット位置に出て、選択範囲も取り残される
        // （実測 2026-11-22: 日本語入力でキャレットと既存の文字が動かない、と見えました）
        if (_isEditingText)
        {
            TextInputClient.NotifySelectionChanged();
        }
    }

    /// <summary><see cref="TextSelectionChanged"/> を発生させる。</summary>
    private void RaiseTextSelectionChanged() => TextSelectionChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>選択範囲の文字がすべて条件を満たすかどうかを取得する（書式ボタンの押下状態に使う）。</summary>
    /// <param name="match">書式が条件を満たすかどうかを返す関数。</param>
    /// <returns>すべて満たす場合は <see langword="true"/>。 吹き出しを選んでいない場合は <see langword="false"/>。</returns>
    /// <remarks>
    /// <b>範囲のすべてで判定する</b>: 一部だけ太字の範囲でボタンが押されて見えないよう、先頭の文字ではなく
    /// 範囲のすべてが同じ属性かで決める。 選択が無い（キャレットだけ）ときは書式バーで選んだ書式で判定する。
    /// </remarks>
    public bool SelectedTextMatches(Func<AnnotationTextStyle, bool> match)
    {
        ArgumentNullException.ThrowIfNull(match);

        if (Selected is not BubbleAnnotation bubble)
        {
            return false;
        }

        var range = TextSelectionRange;
        return range.Length <= 0
            ? match(SelectedTextStyle)
            : bubble.Text.AllMatch(range.Start, range.Length, match);
    }

    /// <summary>履歴から戻した後にキャレットを本文の範囲へ収める。</summary>
    private void ClampCaretToText()
    {
        if (!_isEditingText)
        {
            return;
        }

        var length = TextForInput.Length;
        _caretIndex = Math.Clamp(_caretIndex, 0, length);
        _selectionAnchor = Math.Clamp(_selectionAnchor, 0, length);
        SyncTextSelectionRange();
    }

    /// <summary>編集の開始・終了を通知する。</summary>
    private void RaiseEditingChanged() => EditingChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>本文の編集（選択範囲・キャレット・変換中の文字列）を描く（元画像座標）。</summary>
    /// <param name="context">描画先。</param>
    /// <param name="scale">表示倍率（キャレットの太さを DIP 一定にする）。</param>
    private void DrawTextEditing(DrawingContext context, double scale)
    {
        if (!_isEditingText || ResolveTextLayout() is not { } layout || layout.Lines.Count == 0)
        {
            return;
        }

        var start = SelectionStart;
        var end = SelectionEnd;
        for (var i = 0; i < layout.Lines.Count; i++)
        {
            var line = layout.Lines[i];
            var lineStart = layout.Starts[i];
            var from = Math.Max(start, lineStart);
            var to = Math.Min(end, lineStart + AnnotationTextLineLayout.Length(line));
            if (to <= from)
            {
                continue;
            }

            // 選択範囲も描画と同じ規則で求める（区間の原点からの距離。 コアが単一所有する）
            var x1 = AnnotationTextLineLayout.ResolveX(line, from - lineStart, MeasureSegmentPrefix);
            var x2 = AnnotationTextLineLayout.ResolveX(line, to - lineStart, MeasureSegmentPrefix);
            context.FillRectangle(
                s_textSelectionBrush, new Rect(x1, line.Bounds.Y, x2 - x1, line.Bounds.Height));
        }

        // **変換中はキャレットを変換中の文字列の末尾へ置く**: 末尾へ置かないと、変換中の文字が
        // キャレットの位置に重なって見える（実測 2026-11-22）
        if (!TryResolveCaretSource(
            AnnotationCompositionPreview.ResolveCaret(_caretIndex, _preeditText), out var caret))
        {
            return;
        }

        // **キャレットは一般的な速さで点滅させる**（規則はコアの AnnotationCaretBlink が単一所有する）。
        // 文字入力・キャレット移動の直後は点灯から始める（消えた瞬間に操作すると入力位置を見失う）。
        if (IsCaretVisible)
        {
            var thickness = CaretThicknessDip / Math.Max(scale, 0.01);
            context.FillRectangle(s_caretBrush, new Rect(caret.X, caret.Y, thickness, caret.Height));
        }

        // **変換中の文字列は本文として描く**（RenderDocument が入れている）ため、ここでは重ねて描かない。
        // 重ねて描くと既存の文字と二重になり、確定した文字と位置が食い違って見える（実測 2026-11-22）。
    }

    /// <summary>キャレットを点（元画像ピクセル）の位置へ移す（選択の起点は現在の位置に保つ）。</summary>
    /// <param name="point">位置（元画像ピクセル）。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    private void SetCaretFromPoint(EsPoint point, bool extend)
    {
        _caretIndex = Math.Clamp(ResolveIndexAt(point), 0, TextForInput.Length);
        if (!extend)
        {
            _selectionAnchor = _caretIndex;
        }

        SyncTextSelectionRange();
        InvalidateVisual();
    }

    /// <summary>キャレットの位置（元画像ピクセル）を求める。</summary>
    /// <param name="index">文字位置。</param>
    /// <param name="caret">キャレットの左上と行の高さ。</param>
    /// <returns>求めた場合は <see langword="true"/>。</returns>
    private bool TryResolveCaretSource(int index, out (double X, double Y, double Height) caret)
    {
        caret = default;
        if (ResolveTextLayout() is not { } layout || layout.Lines.Count == 0)
        {
            return false;
        }

        var lineIndex = LineIndexOf(layout, index);
        var line = layout.Lines[lineIndex];
        var offset = Math.Clamp(index - layout.Starts[lineIndex], 0, LineTextLength(line));

        // 位置の決め方はコアが単一所有する（区間の原点からの距離。 行頭から積み上げない）
        caret = (
            AnnotationTextLineLayout.ResolveX(line, offset, MeasureSegmentPrefix),
            line.Bounds.Y,
            line.Bounds.Height);
        return true;
    }

    /// <summary>点（元画像ピクセル）に最も近い文字位置を求める。</summary>
    /// <param name="point">位置（元画像ピクセル）。</param>
    /// <returns>文字位置。</returns>
    private int ResolveIndexAt(EsPoint point)
    {
        if (ResolveTextLayout() is not { } layout || layout.Lines.Count == 0)
        {
            return 0;
        }

        var lineIndex = 0;
        for (var i = 0; i < layout.Lines.Count; i++)
        {
            if (point.Y < layout.Lines[i].Bounds.Y + layout.Lines[i].Bounds.Height || i == layout.Lines.Count - 1)
            {
                lineIndex = i;
                break;
            }
        }

        var line = layout.Lines[lineIndex];
        return layout.Starts[lineIndex]
            + AnnotationTextLineLayout.ResolveOffset(line, point.X, MeasureSegmentPrefix);
    }

    /// <summary>区間の先頭から指定した文字数までの幅を求める（元画像ピクセル）。</summary>
    /// <param name="segment">区間。</param>
    /// <param name="count">文字数。</param>
    /// <returns>幅（元画像ピクセル）。</returns>
    private static double MeasureSegmentPrefix(AnnotationTextSegmentPlan segment, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        var text = count >= segment.Text.Length ? segment.Text : segment.Text[..count];
        return MeasureText(text, segment.Style).Width;
    }

    /// <summary>行の文字数を求める（区間の文字数の合計）。</summary>
    /// <param name="line">行。</param>
    /// <returns>文字数。</returns>
    /// <remarks>数え方はコアが単一所有する（<see cref="AnnotationTextLineLayout.Length"/>）。</remarks>
    internal static int LineTextLength(AnnotationTextLinePlan line) => AnnotationTextLineLayout.Length(line);

    /// <summary>指定位置の文字が入る行を求める。</summary>
    /// <param name="layout">本文の行。</param>
    /// <param name="index">文字位置。</param>
    /// <returns>行の位置。</returns>
    private static int LineIndexOf(TextLayout layout, int index)
    {
        for (var i = 0; i < layout.Lines.Count; i++)
        {
            if (index <= layout.Starts[i] + LineTextLength(layout.Lines[i]))
            {
                return i;
            }
        }

        return layout.Lines.Count - 1;
    }

    /// <summary>選択中の文字をクリップボードへコピーする（ホストのキー処理から呼ぶ入口）。</summary>
    public void CopyTextSelection() => _ = CopySelectionAsync();

    /// <summary>選択中の文字を切り取る（ホストのキー処理から呼ぶ入口）。</summary>
    public void CutTextSelection() => _ = CutSelectionAsync();

    /// <summary>クリップボードの文字を挿入する（ホストのキー処理から呼ぶ入口）。</summary>
    public void PasteText() => _ = PasteAsync();

    /// <summary>選択中の文字をクリップボードへコピーする。</summary>
    /// <returns>コピーの非同期処理。</returns>
    private async Task CopySelectionAsync()
    {
        var text = SelectedText();
        if (text.Length == 0 || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        try
        {
            await clipboard.SetTextAsync(text);
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // クリップボードを掴めない環境ではコピーを諦める（編集は続けられる）
        }
    }

    /// <summary>選択中の文字をクリップボードへ切り取る。</summary>
    /// <returns>切り取りの非同期処理。</returns>
    private async Task CutSelectionAsync()
    {
        var text = SelectedText();
        if (text.Length == 0)
        {
            return;
        }

        await CopySelectionAsync();
        ReplaceSelection(string.Empty);
    }

    /// <summary>クリップボードの文字を挿入する（選択範囲は置き換える）。</summary>
    /// <returns>貼り付けの非同期処理。</returns>
    private async Task PasteAsync()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        string? text;
        try
        {
            text = await clipboard.TryGetTextAsync();
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // クリップボードを掴めない環境では貼り付けを諦める（編集は続けられる）
            return;
        }

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        ReplaceSelection(text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", string.Empty, StringComparison.Ordinal));
    }

    /// <summary>選択中の文字を取得する。</summary>
    /// <returns>選択中の文字列（選択が無い場合は空）。</returns>
    private string SelectedText()
    {
        var text = TextForInput;
        var start = Math.Clamp(SelectionStart, 0, text.Length);
        var end = Math.Clamp(SelectionEnd, 0, text.Length);
        return end <= start ? string.Empty : text[start..end];
    }

    /// <summary>選択中の吹き出しの本文の行を解決する。</summary>
    /// <returns>行と、各行の開始位置（文字数）。 解決できない場合は <see langword="null"/>。</returns>
    /// <remarks>
    /// <b>行の開始位置は本文から数え直す</b>: 折り返しは明示改行を区切りとして扱うため、行の文字数を足した後に
    /// 改行 1 文字を進める（この規則を 1 か所に閉じ込める）。
    /// </remarks>
    private TextLayout? ResolveTextLayout()
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return null;
        }

        var plan = _presenter.ResolveDrawPlan(RenderDocument, MeasureText, SelectedId);
        var shape = plan.Shapes.FirstOrDefault(candidate => string.Equals(candidate.Id, bubble.Id, StringComparison.Ordinal));
        if (shape is null || shape.Lines.Count == 0)
        {
            return null;
        }

        var plain = bubble.Text.PlainText;
        var starts = new List<int>(shape.Lines.Count);
        var index = 0;
        foreach (var line in shape.Lines)
        {
            starts.Add(index);
            index += LineTextLength(line);
            if (index < plain.Length && plain[index] == '\n')
            {
                index++;
            }
        }

        return new TextLayout(shape.Lines, starts);
    }

    /// <summary>本文の行と、各行の開始位置。</summary>
    /// <param name="Lines">行の並び。</param>
    /// <param name="Starts">各行の開始位置（文字数）。</param>
    private sealed record TextLayout(IReadOnlyList<AnnotationTextLinePlan> Lines, IReadOnlyList<int> Starts);
}
