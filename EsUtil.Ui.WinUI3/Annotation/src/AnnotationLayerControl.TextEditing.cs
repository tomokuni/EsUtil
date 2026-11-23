using EsUtil.Platform.Core.Geometry;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using Windows.System;
using EsUtil.Feature.Annotation.Core.Text;
using EsUtil.Feature.Annotation.Presenter.Drawing;
using EsUtil.Feature.Annotation.Logging;

namespace EsUtil.Ui.WinUI3.Annotation;

/// <summary>注釈レイヤーの本文の編集（WinUI 3 版）。</summary>
/// <remarks>
/// <b>入力欄を重ねない</b>: 本文はコントロールが <see cref="TextBlock"/> で描いているため、入力欄を重ねると
/// 折り返し・行の高さ・余白が入力欄の規則になり、<b>表示と編集で見た目が変わる</b>。 表示中のレイアウトのまま
/// キャレットと選択範囲を描いて入力する（Avalonia UI 版・MewUI 版と同じ規則）。<br/>
/// <b>日本語入力（IME）</b>: OS の入力欄（<see cref="TextBox"/>）を透明に重ねて変換を任せ、
/// 変換中の文字列は文書へ入れず、キャレットの位置へ下線付きで描く（確定した文字列だけを入れる）。
/// 変換中の文字列は文書へ入れず、キャレットの位置へ下線付きで描く（確定した文字列だけを入れる）。<br/>
/// <b>座標の対応</b>: 折り返しは <see cref="AnnotationPresenter"/> が解決した行を
/// <see cref="AnnotationTextLineIndex"/> で文字位置へ対応させる。
/// </remarks>
public sealed partial class AnnotationLayerControl
{
    /// <summary>編集の表示（選択範囲・キャレット・変換中の文字列）を並べる層。</summary>
    private readonly Canvas _editing = new() { IsHitTestVisible = false };

    /// <summary>本文を編集中かどうか。</summary>
    private bool _isEditingText;

    /// <summary>キャレットの位置（文字数。 0 は先頭）。</summary>
    private int _caretIndex;

    /// <summary>選択の起点（<see cref="_caretIndex"/> と同じなら選択なし）。</summary>
    private int _selectionAnchor;

    /// <summary>IME の変換中文字列（文書へは入れない）。</summary>
    private string _preeditText = string.Empty;

    /// <summary>IME の変換中かどうか。</summary>
    private bool _isComposing;



    /// <summary>本文の編集中に発生する（開始・終了の両方）。</summary>
    public event EventHandler? EditingChanged;

    /// <summary>本文の選択範囲（キャレットの位置）が変わったときに発生する。</summary>
    public event EventHandler? TextSelectionChanged;

    /// <summary>本文を編集中かどうかを取得する。</summary>
    public bool IsEditingText => _isEditingText;

    /// <summary>キャレットの位置（文字数）を取得する。</summary>
    public int CaretIndex => _caretIndex;

    /// <summary>本文の文字を選んでいるかどうかを取得する（書式の適用先があるか）。</summary>
    public bool HasTextSelection => _isEditingText && TextSelectionLength > 0;

    /// <summary>選択範囲の開始位置（文字数）を取得する。</summary>
    public int TextSelectionStart => Math.Min(_selectionAnchor, _caretIndex);

    /// <summary>選択範囲の終了位置（文字数）を取得する。</summary>
    public int TextSelectionEnd => Math.Max(_selectionAnchor, _caretIndex);

    /// <summary>選択範囲の長さ（文字数）を取得する。</summary>
    public int TextSelectionLength => TextSelectionEnd - TextSelectionStart;

    /// <summary>選択範囲（開始位置と長さ）を取得する。</summary>
    public (int Start, int Length) TextSelectionRange
    {
        get => (TextSelectionStart, TextSelectionLength);
        set
        {
            // 範囲はキャレットと選択の起点で表す（範囲から位置を戻す規則はここ 1 か所）
            var length = TextForInput.Length;
            var start = Math.Clamp(value.Start, 0, length);
            var end = Math.Clamp(start + Math.Max(0, value.Length), start, length);
            _selectionAnchor = start;
            _caretIndex = end;
            SyncTextSelectionRange();
            Redraw();
        }
    }

    /// <summary>キャレットの点滅を始めた時刻。</summary>
    private DateTimeOffset _caretBlinkStart = DateTimeOffset.UtcNow;

    /// <summary>キャレットの点滅を進めるタイマー（編集中だけ動かす）。</summary>
    private Microsoft.UI.Xaml.DispatcherTimer? _caretTimer;

    /// <summary>いまキャレットを点灯して描くかどうかを取得する。</summary>
    /// <value>点滅の位相が点灯の間は <see langword="true"/>。</value>
    private bool IsCaretVisible => AnnotationCaretBlink.IsVisible(DateTimeOffset.UtcNow - _caretBlinkStart);

    /// <summary>キャレットの点滅を点灯から始め直す（文字入力・キャレット移動の直後に呼ぶ）。</summary>
    private void RestartCaretBlink()
    {
        _caretBlinkStart = DateTimeOffset.UtcNow;
        if (_caretTimer is null)
        {
            var timer = new Microsoft.UI.Xaml.DispatcherTimer { Interval = AnnotationCaretBlink.Interval };
            timer.Tick += (_, _) =>
            {
                if (_isEditingText)
                {
                    Redraw();
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

    /// <summary>本文の編集を始める（吹き出しを選んでいるときだけ）。</summary>
    /// <returns>編集を始めた場合は <see langword="true"/>。 枠線を選んでいる場合は <see langword="false"/>。</returns>
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
        _core.Log.LogEditingChanged(true);
        _preeditText = string.Empty;
        _isComposing = false;
        _caretIndex = bubble.Text.PlainText.Length;
        _selectionAnchor = _caretIndex;
        _marquee.ResetSelection();
        _ = Focus(FocusState.Programmatic);
        FocusImeSink();
        RestartCaretBlink();
        RaiseEditingChanged();
        Redraw();
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
        _core.Log.LogEditingChanged(false);
        _preeditText = string.Empty;
        _isComposing = false;
        StopCaretBlink();
        ReleaseImeSink();

        if (Selected is { } item)
        {
            _marquee.SetSelection(item.Bounds);
        }

        RaiseEditingChanged();
        Redraw();
    }

    /// <summary>選択範囲、またはキャレットの前後の 1 文字を削除する。</summary>
    /// <param name="forward">キャレットの次の文字を削除する場合は <see langword="true"/>。</param>
    private void DeleteAtCaret(bool forward)
    {
        if (TextSelectionEnd > TextSelectionStart)
        {
            ReplaceSelection(string.Empty);
            return;
        }

        var index = forward ? _caretIndex : _caretIndex - 1;
        if (index < 0 || index >= TextForInput.Length)
        {
            return;
        }

        ReplaceRange(index, index + 1, string.Empty);
    }

    /// <summary>選択範囲を指定された文字列で置き換える。</summary>
    /// <param name="text">置き換える文字列（空なら削除）。</param>
    /// <returns>文書を変えた場合は <see langword="true"/>。</returns>
    private bool ReplaceSelection(string text)
    {
        var start = TextSelectionStart;
        var end = TextSelectionEnd;
        if (start == end && text.Length == 0)
        {
            return false;
        }

        var changed = ReplaceRange(start, end, text);
        return changed;
    }

    /// <summary>指定した範囲を文字列で置き換えて文書へ反映する。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="end">終了位置（文字数）。</param>
    /// <param name="text">置き換える文字列。</param>
    /// <returns>文書を変えた場合は <see langword="true"/>。</returns>
    private bool ReplaceRange(int start, int end, string text)
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return false;
        }

        // **変換中かどうかを先に落とす**: 落とさずに本文を書き換えると、変換の途中で「本文が変わった」と
        // IME へ伝えてしまい、変換が中断されて **1 キーごとに確定**する（実測 2026-11-23: 代入の順序が逆になる）。
        _isComposing = false;
        _preeditText = string.Empty;
        PrepareUndo();
        // 挿入した文字へ保留している書式を反映する（規則はコアが単一所有する）
        _document = _document.WithText(
            bubble,
            AnnotationTextStyleEdit.Insert(bubble.Text, start, end - start, text, _typingStyle.Value));
        RaiseDocumentChanged();

        _caretIndex = start + text.Length;
        _selectionAnchor = _caretIndex;
        SyncTextSelectionRange();
        Redraw();
        return true;
    }

    /// <summary>選択範囲の装飾（太字・斜体・下線・取り消し線）を切り替える。</summary>
    /// <param name="read">属性を読む関数。</param>
    /// <param name="write">属性を書く関数。</param>
    /// <returns>処理した場合は <see langword="true"/>。</returns>
    internal bool ToggleDecoration(
        Func<AnnotationTextStyle, bool> read,
        Func<AnnotationTextStyle, bool, AnnotationTextStyle> write)
        // 適用先（選択範囲・選択が無ければ以降の入力）の決定は Apply/Transform が単一所有する
        => TransformTextStyle(style => write(style, !read(style)));

    /// <summary>選択範囲の文字がすべて条件を満たすかどうかを取得する（書式ボタンの押下状態に使う）。</summary>
    /// <param name="match">書式が条件を満たすかどうかを返す関数。</param>
    /// <returns>すべて満たす場合は <see langword="true"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="match"/> が null の場合。</exception>
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

    /// <summary>選択範囲の書式を変換する（1 つの属性だけ変える）。</summary>
    /// <param name="transform">書式を変える処理。</param>
    /// <remarks>選択が無ければ以降の入力へ回す（判定は <see cref="TransformTextStyle(Func{AnnotationTextStyle, AnnotationTextStyle})"/> が持つ）。</remarks>
    internal void TransformSelectionStyle(Func<AnnotationTextStyle, AnnotationTextStyle> transform)
        => _ = TransformTextStyle(transform);

    /// <summary>選択範囲へ書式を丸ごと適用する（色・大きさ・フォント）。</summary>
    /// <param name="style">書式。</param>
    internal void ApplyFullStyle(AnnotationTextStyle style) => ApplyTextStyle(style);

    /// <summary>選択範囲を <see cref="TextSelectionRange"/> へ写す（書式の適用先に使う）。</summary>
    private void SyncTextSelectionRange()
    {
        TextSelectionChanged?.Invoke(this, EventArgs.Empty);
        RestartCaretBlink();
    }

    /// <summary>編集の開始・終了を通知する。</summary>
    private void RaiseEditingChanged() => EditingChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>本文の編集（選択範囲・キャレット・変換中の文字列）を描く。</summary>
    private void DrawEditing()
    {
        _editing.Children.Clear();
        if (!_isEditingText || ResolveLineIndex() is not { } index)
        {
            return;
        }

        var (scale, offset) = ResolveImageTransform();

        foreach (var rect in index.ResolveSelectionRects(TextSelectionStart, TextSelectionEnd, MeasureText))
        {
            var shape = new Rectangle
            {
                Width = Math.Max(1, rect.Width * scale),
                Height = Math.Max(1, rect.Height * scale),
                Fill = AnnotationTextMetrics.ToBrush(AnnotationEditorAppearance.SelectionColor),
            };

            Canvas.SetLeft(shape, (rect.X * scale) + offset.X);
            Canvas.SetTop(shape, (rect.Y * scale) + offset.Y);
            _editing.Children.Add(shape);
        }

        var caret = ResolveCaretDip();
        var caretX = caret.X;

        // **変換中の文字列はここで描かない**: 描画に使う文書（<see cref="RenderDocument"/>）へ
        // 入れているため、重ねて描くと既存の文字と二重になります（実測 2026-11-23）。

        // **キャレットは一般的な速さで点滅させる**（規則はコアの AnnotationCaretBlink が単一所有する）
        PlaceImeSink();
        if (!IsCaretVisible)
        {
            return;
        }

        var caretShape = new Rectangle
        {
            Width = AnnotationEditorAppearance.CaretThicknessDip,
            Height = Math.Max(1, caret.Height),
            Fill = AnnotationTextMetrics.ToBrush(AnnotationEditorAppearance.CaretColor),
        };

        Canvas.SetLeft(caretShape, caretX);
        Canvas.SetTop(caretShape, caret.Y);
        _editing.Children.Add(caretShape);
    }

    /// <summary>文字を置く上端（DIP）を求める（本文の run と同じく行の高さの中央へ寄せる）。</summary>
    /// <param name="caret">キャレットの位置と行の高さ（DIP）。</param>
    /// <param name="style">文字の書式。</param>
    /// <returns>上端（DIP）。</returns>
    /// <remarks>
    /// <b>本文と揃える</b>: <see cref="AnnotationPainter"/> は行の実寸と書式の実寸の差の半分だけ下げて置きます。
    /// 変換中の文字だけ上端へ置くと上へずれて見えます（実測 2026-11-23）。
    /// </remarks>
    private double TextTopDip((double X, double Y, double Height) caret, AnnotationTextStyle style)
    {
        var (scale, _) = ResolveImageTransform();
        var runHeight = MeasureText("Ag", style with { IsItalic = false }).Height;
        return caret.Y + Math.Max(0, (caret.Height - (runHeight * scale)) / 2);
    }

    /// <summary>キャレットの位置（DIP）を求める。</summary>
    /// <returns>キャレットの左上と行の高さ。</returns>
    /// <remarks>
    /// <b>変換中はキャレットを変換中の文字列の末尾へ置く</b>: 規則はコアの
    /// <see cref="AnnotationCompositionPreview.ResolveCaret"/> が単一所有します（4 つの UI 版で同じ）。
    /// </remarks>
    private (double X, double Y, double Height) ResolveCaretDip()
    {
        var index = ResolveLineIndex();
        var caretIndex = AnnotationCompositionPreview.ResolveCaret(_caretIndex, _preeditText);
        if (index is null || index.ResolveCaret(caretIndex, MeasureText) is not { } caret)
        {
            return default;
        }

        var (scale, offset) = ResolveImageTransform();
        return (
            (caret.X * scale) + offset.X,
            (caret.Y * scale) + offset.Y,
            Math.Max(1, caret.Height * scale));
    }

    /// <summary>選択中の吹き出しの行と文字位置の対応を解決する。</summary>
    /// <returns>対応。 解決できない場合は <see langword="null"/>。</returns>
    private AnnotationTextLineIndex? ResolveLineIndex()
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return null;
        }

        var document = RenderDocument;
        var plan = _presenter.ResolveDrawPlan(document, MeasureText, SelectedId);
        var shape = plan.Shapes.FirstOrDefault(
            candidate => string.Equals(candidate.Id, bubble.Id, StringComparison.Ordinal));
        return shape is null
            ? null
            : AnnotationTextLineIndex.Create(shape, document.Find(bubble.Id) is BubbleAnnotation drawn
                ? drawn.Text.PlainText
                : bubble.Text.PlainText);
    }

    /// <summary>本文の編集でキーが押されたときの処理。</summary>
    /// <param name="e">キーの引数。</param>
    /// <returns>処理した場合は <see langword="true"/>。</returns>
    private bool HandleTextEditingKey(KeyRoutedEventArgs e)
    {
        var control = IsDown(VirtualKey.Control);
        var shift = IsDown(VirtualKey.Shift);

        if (control && !shift)
        {
            switch (e.Key)
            {
                case VirtualKey.B:
                    return ToggleDecoration(style => style.IsBold, static (style, value) => style with { IsBold = value });
                case VirtualKey.I:
                    return ToggleDecoration(style => style.IsItalic, static (style, value) => style with { IsItalic = value });
                case VirtualKey.U:
                    return ToggleDecoration(style => style.IsUnderline, static (style, value) => style with { IsUnderline = value });
                case VirtualKey.C:
                    _ = CopySelectionAsync();
                    return true;
                case VirtualKey.X:
                    _ = CutSelectionAsync();
                    return true;
                case VirtualKey.V:
                    _ = PasteAsync();
                    return true;
                case VirtualKey.A:
                    _selectionAnchor = 0;
                    _caretIndex = TextForInput.Length;
                    SyncTextSelectionRange();
                    Redraw();
                    return true;
            }
        }

        switch (e.Key)
        {
            case VirtualKey.Escape:
                EndTextEditing();
                return true;
            case VirtualKey.Enter:
                ReplaceSelection("\n");
                return true;
            case VirtualKey.Back:
                DeleteAtCaret(forward: false);
                return true;
            case VirtualKey.Delete:
                DeleteAtCaret(forward: true);
                return true;
            case VirtualKey.Left:
                MoveCaret(shift ? _caretIndex - 1 : TextSelectionStart - 1, shift);
                return true;
            case VirtualKey.Right:
                MoveCaret(shift ? _caretIndex + 1 : TextSelectionEnd + 1, shift);
                return true;
            case VirtualKey.Up:
                MoveCaretToNeighbourLine(-1, shift);
                return true;
            case VirtualKey.Down:
                MoveCaretToNeighbourLine(1, shift);
                return true;
            case VirtualKey.Home:
                MoveCaretToLineEdge(first: true, shift);
                return true;
            case VirtualKey.End:
                MoveCaretToLineEdge(first: false, shift);
                return true;
            default:
                return false;
        }
    }

    /// <summary>選択中の文字をクリップボードへコピーする。</summary>
    /// <remarks>キー入力が注釈レイヤーへ届かない環境向けの入口（ホストのキー処理から呼ぶ）。</remarks>
    public void CopyTextSelection() => _ = CopySelectionAsync();

    /// <summary>選択中の文字を切り取る。</summary>
    /// <remarks>キー入力が注釈レイヤーへ届かない環境向けの入口（ホストのキー処理から呼ぶ）。</remarks>
    public void CutTextSelection() => _ = CutSelectionAsync();

    /// <summary>クリップボードの文字を挿入する。</summary>
    /// <remarks>キー入力が注釈レイヤーへ届かない環境向けの入口（ホストのキー処理から呼ぶ）。</remarks>
    public void PasteText() => _ = PasteAsync();

    /// <summary>選択中の文字をクリップボードへコピーする。</summary>
    /// <returns>コピーの非同期処理。</returns>
    private async Task CopySelectionAsync()
    {
        var text = SelectedText();
        if (text.Length == 0)
        {
            return;
        }

        var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
        package.SetText(text);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        await Task.CompletedTask;
    }

    /// <summary>選択中の文字を切り取る。</summary>
    /// <returns>切り取りの非同期処理。</returns>
    private async Task CutSelectionAsync()
    {
        if (SelectedText().Length == 0)
        {
            return;
        }

        await CopySelectionAsync();
        ReplaceSelection(string.Empty);
    }


    /// <summary>クリップボードの文字を挿入する。</summary>
    /// <returns>貼り付けの非同期処理。</returns>
    private async Task PasteAsync()
    {
        var content = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
        if (!content.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
        {
            return;
        }

        var text = await content.GetTextAsync();
        if (!string.IsNullOrEmpty(text))
        {
            ReplaceSelection(text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n'));
        }
    }

    /// <summary>選択中の文字を取得する。</summary>
    /// <returns>選択中の文字列（選択が無い場合は空）。</returns>
    private string SelectedText()
    {
        var text = TextForInput;
        var start = Math.Clamp(TextSelectionStart, 0, text.Length);
        var end = Math.Clamp(TextSelectionEnd, 0, text.Length);
        return end <= start ? string.Empty : text[start..end];
    }

    /// <summary>確定した文字列を受け取る（IME の変換中は受け取らない）。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">文字入力の引数。</param>
    private void OnCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs e)
    {
        _ = sender;

        // **入力は受け口（_imeSink）が担う**: 受け口がある間はここへ入れない（二重に入る）。
        // 受け口を用意できない環境（ウィンドウを持たない検証）だけ、そのまま入れる。
        if (!_isEditingText || _isComposing || _imeSink is not null)
        {
            return;
        }

        var text = e.Character.ToString();
        if (string.IsNullOrEmpty(text) || char.IsControl(e.Character))
        {
            return;
        }

        ReplaceSelection(text);
        e.Handled = true;
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
        Redraw();
    }

    /// <summary>キャレットを上下の行の同じ桁へ移す。</summary>
    /// <param name="delta">移動する行数。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    private void MoveCaretToNeighbourLine(int delta, bool extend)
    {
        if (ResolveLineIndex() is not { } index || index.Lines.Count == 0)
        {
            return;
        }

        var lineIndex = index.LineIndexOf(_caretIndex);
        var target = Math.Clamp(lineIndex + delta, 0, index.Lines.Count - 1);
        var column = _caretIndex - index.LineStart(lineIndex);
        var length = AnnotationTextLineIndex.LineTextLength(index.Lines[target]);
        MoveCaret(index.LineStart(target) + Math.Min(column, length), extend);
    }

    /// <summary>キャレットを行の先頭・末尾へ移す。</summary>
    /// <param name="first">行の先頭へ移す場合は <see langword="true"/>。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    private void MoveCaretToLineEdge(bool first, bool extend)
    {
        if (ResolveLineIndex() is not { } index || index.Lines.Count == 0)
        {
            MoveCaret(first ? 0 : TextForInput.Length, extend);
            return;
        }

        var lineIndex = index.LineIndexOf(_caretIndex);
        var length = AnnotationTextLineIndex.LineTextLength(index.Lines[lineIndex]);
        MoveCaret(first ? index.LineStart(lineIndex) : index.LineStart(lineIndex) + length, extend);
    }

    /// <summary>点（元画像ピクセル）の位置へキャレットを移す。</summary>
    /// <param name="point">位置（元画像ピクセル）。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    private void SetCaretFromPoint(EsPoint point, bool extend)
    {
        if (ResolveLineIndex() is not { } index)
        {
            return;
        }

        _caretIndex = Math.Clamp(index.IndexAt(point, MeasureText), 0, TextForInput.Length);
        if (!extend)
        {
            _selectionAnchor = _caretIndex;
        }

        SyncTextSelectionRange();
        Redraw();
    }
}
