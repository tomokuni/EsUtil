using System.Runtime.InteropServices;
using System.Text;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.Annotation.Core.Text;
using EsUtil.Feature.Annotation.Presenter.Drawing;
using EsUtil.Feature.Annotation.Logging;

namespace EsUtil.Ui.WinForms.Annotation;

/// <summary>注釈レイヤーの本文の編集（WinForms 版）。</summary>
/// <remarks>
/// <b>入力欄を重ねない</b>: 本文はコントロールが GDI+ で描いているため、入力欄を重ねると折り返し・行の高さ・
/// 余白が入力欄の規則になり、<b>表示と編集で見た目が変わる</b>。 表示中のレイアウトのままキャレットと
/// 選択範囲を描いて入力する（Avalonia UI 版・MewUI 版と同じ規則）。<br/>
/// <b>日本語入力（IME）</b>: ウィンドウ メッセージ（<c>WM_IME_COMPOSITION</c>）から変換中の文字列と確定した
/// 文字列を受け取る。 変換中の文字列は文書へ入れず、キャレットの位置へ下線付きで描く。<br/>
/// <b>座標の対応</b>: 折り返しは <see cref="AnnotationPresenter"/> が解決した行を
/// <see cref="AnnotationTextLineIndex"/> で文字位置へ対応させる。
/// </remarks>
public sealed partial class AnnotationLayerControl
{
    /// <summary>IME の変換を開始するメッセージ。</summary>
    private const int WmImeStartComposition = 0x0105;

    /// <summary>IME の変換を終了するメッセージ。</summary>
    private const int WmImeEndComposition = 0x0106;

    /// <summary>IME の変換内容が変わったメッセージ。</summary>
    private const int WmImeComposition = 0x010F;

    /// <summary>変換中の文字列を求める指定。</summary>
    private const int GcsCompStr = 0x0008;

    /// <summary>確定した文字列を求める指定。</summary>
    private const int GcsResultStr = 0x0800;

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

    /// <summary>選択範囲（開始位置と長さ）を取得または設定する。</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
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
            Invalidate();
        }
    }

    /// <summary>キャレットの点滅を始めた時刻。</summary>
    private DateTimeOffset _caretBlinkStart = DateTimeOffset.UtcNow;

    /// <summary>キャレットの点滅を進めるタイマー（編集中だけ動かす）。</summary>
    private System.Windows.Forms.Timer? _caretTimer;

    /// <summary>いまキャレットを点灯して描くかどうかを取得する。</summary>
    /// <value>点滅の位相が点灯の間は <see langword="true"/>。</value>
    private bool IsCaretVisible => AnnotationCaretBlink.IsVisible(DateTimeOffset.UtcNow - _caretBlinkStart);

    /// <summary>キャレットの点滅を点灯から始め直す（文字入力・キャレット移動の直後に呼ぶ）。</summary>
    private void RestartCaretBlink()
    {
        _caretBlinkStart = DateTimeOffset.UtcNow;
        if (_caretTimer is null)
        {
            var timer = new System.Windows.Forms.Timer
            {
                Interval = AnnotationCaretBlink.IntervalMilliseconds,
            };
            timer.Tick += (_, _) =>
            {
                if (_isEditingText)
                {
                    Invalidate();
                }
                else
                {
                    timer.Stop();
                }
            };

            _caretTimer = timer;
        }

        if (!_caretTimer.Enabled)
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
        Focus();
        RestartCaretBlink();
        RaiseEditingChanged();
        Invalidate();
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

        if (Selected is { } item)
        {
            _marquee.SetSelection(item.Bounds);
        }

        RaiseEditingChanged();
        Invalidate();
    }

    /// <summary>ウィンドウ メッセージから IME の変換内容を受け取る。</summary>
    /// <param name="m">メッセージ。</param>
    /// <remarks>
    /// <b>当たり判定を透過しない</b>: 注釈レイヤーはプレビュー領域の入力を単一所有し、注釈の無い場所の操作は
    /// <see cref="AnnotationLayerControl.PreviewGestureRequested"/> でホストへ渡す（透過させると、透過した先の
    /// 要素が入力を受け取り、注釈を選び直せなくなる。 実測 2026-11-20）。
    /// </remarks>
    protected override void WndProc(ref Message m)
    {
        if (_isEditingText)
        {
            switch (m.Msg)
            {
                case WmImeStartComposition:
                    _isComposing = true;
                    _preeditText = string.Empty;
                    Invalidate();
                    break;
                case WmImeEndComposition:
                    _isComposing = false;
                    _preeditText = string.Empty;
                    Invalidate();
                    break;
                case WmImeComposition:
                    HandleImeComposition((int)m.LParam);
                    break;
                default:
                    break;
            }
        }

        base.WndProc(ref m);
    }

    /// <summary>IME の変換内容を本文へ反映する。</summary>
    /// <param name="flags">変換の指定（<c>GCS_*</c> の組み合わせ）。</param>
    private void HandleImeComposition(int flags)
    {
        if ((flags & GcsResultStr) != 0)
        {
            var committed = ReadCompositionString(GcsResultStr);
            _isComposing = false;
            _preeditText = string.Empty;
            if (committed.Length > 0)
            {
                ReplaceSelection(committed);
                return;
            }
        }

        if ((flags & GcsCompStr) != 0)
        {
            _isComposing = true;
            _preeditText = ReadCompositionString(GcsCompStr);
        }

        Invalidate();
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

        return ReplaceRange(start, end, text);
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
        Invalidate();
        return true;
    }

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
    private void SyncTextSelectionRange() => TextSelectionChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>編集の開始・終了を通知する。</summary>
    private void RaiseEditingChanged() => EditingChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>本文の編集（選択範囲・キャレット・変換中の文字列）を描く（DIP）。</summary>
    /// <param name="graphics">描画先（DIP）。</param>
    private void DrawEditing(Graphics graphics)
    {
        if (!_isEditingText || ResolveLineIndex() is not { } index)
        {
            return;
        }

        var (scale, offset) = ResolveImageTransform();
        using var selectionBrush = new SolidBrush(AnnotationEditorAppearance.SelectionColor);
        foreach (var rect in index.ResolveSelectionRects(TextSelectionStart, TextSelectionEnd, AnnotationTextMetrics.Measure))
        {
            graphics.FillRectangle(
                selectionBrush,
                (float)((rect.X * scale) + offset.X),
                (float)((rect.Y * scale) + offset.Y),
                Math.Max(1F, (float)(rect.Width * scale)),
                Math.Max(1F, (float)(rect.Height * scale)));
        }

        var caret = ResolveCaretDip();
        var caretX = (float)caret.X;

        // **変換中の文字列はキャレットの位置へ置き、キャレットはその末尾へ移す**: 重ねると既存の文字と
        // 二重になり、キャレットが動かないため「入力が反映されていない」ように見える（実測 2026-11-22）。
        // 変換中の文字列には下線を引く（未確定であることを示す）。
        if (_preeditText.Length > 0)
        {
            var style = SelectedTextStyle with { IsUnderline = true };
            using var font = AnnotationTextMetrics.CreateFont(style with { IsUnderline = false, IsStrike = false });
            using var brush = new SolidBrush(style.Color);
            using var format = new StringFormat(StringFormat.GenericTypographic) { FormatFlags = StringFormatFlags.NoWrap };
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            graphics.DrawString(
                _preeditText, font, brush, new PointF(caretX, (float)caret.Y), format);

            var width = (float)graphics.MeasureString(_preeditText, font, int.MaxValue, format).Width;
            using var pen = new Pen(style.Color, Math.Max(1F, (float)(style.FontSize * 0.06)));
            var underlineY = (float)(caret.Y + caret.Height - 1);
            graphics.DrawLine(pen, caretX, underlineY, caretX + width, underlineY);
            caretX += width;
        }

        // **キャレットは一般的な速さで点滅させる**（規則はコアの AnnotationCaretBlink が単一所有する）
        if (IsCaretVisible)
        {
            using var caretBrush = new SolidBrush(AnnotationEditorAppearance.CaretColor);
            graphics.FillRectangle(
                caretBrush, caretX, (float)caret.Y, (float)AnnotationEditorAppearance.CaretThicknessDip, Math.Max(1F, (float)caret.Height));
        }
    }

    /// <summary>キャレットの位置（DIP）を求める。</summary>
    /// <returns>キャレットの左上と行の高さ。</returns>
    private (double X, double Y, double Height) ResolveCaretDip()
    {
        if (ResolveLineIndex() is not { } index
            || index.ResolveCaret(_caretIndex, AnnotationTextMetrics.Measure) is not { } caret)
        {
            return default;
        }

        var (scale, offset) = ResolveImageTransform();
        return ((caret.X * scale) + offset.X, (caret.Y * scale) + offset.Y, Math.Max(1, caret.Height * scale));
    }

    /// <summary>選択中の吹き出しの行と文字位置の対応を解決する。</summary>
    /// <returns>対応。 解決できない場合は <see langword="null"/>。</returns>
    private AnnotationTextLineIndex? ResolveLineIndex()
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return null;
        }

        var plan = _presenter.ResolveDrawPlan(_document, AnnotationTextMetrics.Measure, SelectedId);
        var shape = plan.Shapes.FirstOrDefault(
            candidate => string.Equals(candidate.Id, bubble.Id, StringComparison.Ordinal));
        return shape is null ? null : AnnotationTextLineIndex.Create(shape, bubble.Text.PlainText);
    }

    /// <summary>本文の編集でキーが押されたときの処理。</summary>
    /// <param name="e">キーの引数。</param>
    /// <returns>処理した場合は <see langword="true"/>。</returns>
    private bool HandleTextEditingKey(KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.A)
        {
            _selectionAnchor = 0;
            _caretIndex = TextForInput.Length;
            SyncTextSelectionRange();
            Invalidate();
            return true;
        }

        if (e.Control && e.KeyCode is Keys.C or Keys.X or Keys.V)
        {
            HandleClipboard(e.KeyCode);
            return true;
        }

        if (e.Control && e.KeyCode is Keys.B or Keys.I or Keys.U)
        {
            return e.KeyCode switch
            {
                Keys.B => ToggleDecoration(style => style.IsBold, static (style, value) => style with { IsBold = value }),
                Keys.I => ToggleDecoration(style => style.IsItalic, static (style, value) => style with { IsItalic = value }),
                _ => ToggleDecoration(style => style.IsUnderline, static (style, value) => style with { IsUnderline = value }),
            };
        }

        switch (e.KeyCode)
        {
            case Keys.Escape:
                EndTextEditing();
                return true;
            case Keys.Enter:
                ReplaceSelection("\n");
                return true;
            case Keys.Back:
                DeleteAtCaret(forward: false);
                return true;
            case Keys.Delete:
                DeleteAtCaret(forward: true);
                return true;
            case Keys.Left:
                MoveCaret(e.Shift ? _caretIndex - 1 : TextSelectionStart - 1, e.Shift);
                return true;
            case Keys.Right:
                MoveCaret(e.Shift ? _caretIndex + 1 : TextSelectionEnd + 1, e.Shift);
                return true;
            case Keys.Up:
                MoveCaretToNeighbourLine(-1, e.Shift);
                return true;
            case Keys.Down:
                MoveCaretToNeighbourLine(1, e.Shift);
                return true;
            case Keys.Home:
                MoveCaretToLineEdge(first: true, e.Shift);
                return true;
            case Keys.End:
                MoveCaretToLineEdge(first: false, e.Shift);
                return true;
            default:
                return false;
        }
    }

    /// <summary>クリップボードの操作（コピー・切り取り・貼り付け）を行う。</summary>
    /// <param name="key">押されたキー。</param>
    private void HandleClipboard(Keys key)
    {
        var start = TextSelectionStart;
        var length = TextSelectionLength;
        if (key == Keys.V)
        {
            if (Clipboard.ContainsText())
            {
                ReplaceSelection(Clipboard.GetText().Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n'));
            }

        }

        if (length == 0)
        {
            return;
        }

        Clipboard.SetText(TextForInput.Substring(start, length));
        if (key == Keys.X)
        {
            ReplaceSelection(string.Empty);
        }
    }

    /// <summary>選択範囲の装飾を切り替える。</summary>
    /// <param name="read">属性を読む関数。</param>
    /// <param name="write">属性を書く関数。</param>
    /// <returns>処理した場合は <see langword="true"/>。</returns>
    private bool ToggleDecoration(
        Func<AnnotationTextStyle, bool> read, Func<AnnotationTextStyle, bool, AnnotationTextStyle> write)
        // 適用先（選択範囲・選択が無ければ以降の入力）の決定は Apply/Transform が単一所有する
        => TransformTextStyle(style => write(style, !read(style)));

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
        Invalidate();
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

        _caretIndex = Math.Clamp(index.IndexAt(point, AnnotationTextMetrics.Measure), 0, TextForInput.Length);
        if (!extend)
        {
            _selectionAnchor = _caretIndex;
        }

        SyncTextSelectionRange();
        Invalidate();
    }

    [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr ImmGetContext(IntPtr hWnd);

    [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);

    [DllImport("imm32.dll", CharSet = CharSet.Unicode, EntryPoint = "ImmGetCompositionStringW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int ImmGetCompositionStringW(IntPtr hIMC, int dwIndex, byte[]? lpBuf, int dwBufLen);
}
