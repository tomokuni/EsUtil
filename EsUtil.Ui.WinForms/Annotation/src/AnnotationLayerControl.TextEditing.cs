using System.Runtime.InteropServices;
using System.Text;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.Annotation.Presenter.Drawing;
using EsUtil.Feature.StyledText;
using EsUtil.Feature.StyledText.Core.Text;
using EsUtil.Feature.StyledText.Presenter.Editing;

namespace EsUtil.Ui.WinForms.Annotation;

/// <summary>注釈レイヤーの本文の編集（WinForms 版）。</summary>
/// <remarks>
/// <b>入力欄を重ねない</b>: 本文はコントロールが GDI+ で描いているため、入力欄を重ねると折り返し・行の高さ・
/// 余白が入力欄の規則になり、<b>表示と編集で見た目が変わる</b>。 表示中のレイアウトのままキャレットと
/// 選択範囲を描いて入力する（Avalonia UI 版・MewUI 版と同じ規則）。<br/>
/// <b>日本語入力（IME）</b>: ウィンドウ メッセージ（<c>WM_IME_COMPOSITION</c>）から変換中の文字列と確定した
/// 文字列を受け取る。 変換中の文字列は文書へ入れず、キャレットの位置へ下線付きで描く。<br/>
/// <b>座標の対応</b>: 折り返しは <see cref="AnnotationPresenter"/> が解決した行を
/// <see cref="TextLineIndex"/> で文字位置へ対応させる。
/// </remarks>
public sealed partial class AnnotationLayerControl
{
    /// <summary>本文の編集状態（キャレット・選択・入力の規則は提示層が単一所有する）。</summary>
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

    /// <summary>IME の変換中文字列（文書へは入れない）。</summary>
    private string _preeditText = string.Empty;

    /// <summary>IME の変換中の文字列が変わったときに発生する（本文の描画を受け持つコントロールを追随させる）。</summary>
    internal event EventHandler? PreeditChanged;

    /// <summary>変換中の文字列を設定し、変わっていれば通知する。</summary>
    /// <param name="value">変換中の文字列（空で変換の終了）。</param>
    private void SetPreedit(string value)
    {
        if (string.Equals(_preeditText, value, StringComparison.Ordinal))
        {
            return;
        }

        _preeditText = value;
        PreeditChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>IME の変換中かどうか。</summary>
    private bool _isComposing;

    /// <summary>本文の編集中に発生する（開始・終了の両方）。</summary>
    public event EventHandler? EditingChanged;

    /// <summary>本文の選択範囲（キャレットの位置）が変わったときに発生する。</summary>
    public event EventHandler? TextSelectionChanged;

    /// <summary>本文を編集中かどうかを取得する。</summary>
    public bool IsEditingText => _session.IsEditingText;

    /// <summary>キャレットの位置（文字数）を取得する。</summary>
    public int CaretIndex => _session.CaretIndex;

    /// <summary>本文の文字を選んでいるかどうかを取得する（書式の適用先があるか）。</summary>
    public bool HasTextSelection => IsEditingText && TextSelectionLength > 0;

    /// <summary>選択範囲の開始位置（文字数）を取得する。</summary>
    public int TextSelectionStart => _session.SelectionStart;

    /// <summary>選択範囲の終了位置（文字数）を取得する。</summary>
    public int TextSelectionEnd => _session.SelectionEnd;

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
            _session.TextSelectionRange = (value.Start, value.Length);
            Invalidate();
        }
    }

    /// <summary>キャレットの点滅を始めた時刻。</summary>
    private DateTimeOffset _caretBlinkStart = DateTimeOffset.UtcNow;

    /// <summary>キャレットの点滅を進めるタイマー（編集中だけ動かす）。</summary>
    private System.Windows.Forms.Timer? _caretTimer;

    /// <summary>いまキャレットを点灯して描くかどうかを取得する。</summary>
    /// <value>点滅の位相が点灯の間は <see langword="true"/>。</value>
    private bool IsCaretVisible => TextCaretBlink.IsVisible(DateTimeOffset.UtcNow - _caretBlinkStart);

    /// <summary>キャレットの点滅を点灯から始め直す（文字入力・キャレット移動の直後に呼ぶ）。</summary>
    private void RestartCaretBlink()
    {
        _caretBlinkStart = DateTimeOffset.UtcNow;
        if (_caretTimer is null)
        {
            var timer = new System.Windows.Forms.Timer
            {
                Interval = TextCaretBlink.IntervalMilliseconds,
            };
            timer.Tick += (_, _) =>
            {
                if (IsEditingText)
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
    internal string TextForInput => _session.SelectedPlainText;

    /// <summary>本文の編集を始める（吹き出しを選んでいるときだけ）。</summary>
    /// <returns>編集を始めた場合は <see langword="true"/>。 枠線を選んでいる場合は <see langword="false"/>。</returns>
    public bool BeginTextEditing()
    {
        if (!_session.BeginTextEditing())
        {
            return false;
        }

        Focus();
        RestartCaretBlink();
        return true;
    }

    /// <summary>本文の編集を終える（内容は既に文書へ入っている）。</summary>
    public void EndTextEditing()
    {
        if (_session.IsEditingText)
        {
            _session.EndTextEditing();
        }
    }

    /// <summary>ウィンドウ メッセージから IME の変換内容を受け取る。</summary>
    /// <param name="m">メッセージ。</param>
    /// <remarks>
    /// <b>当たり判定を透過しない</b>: 注釈レイヤーはプレビュー領域の入力を単一所有し、注釈の無い場所の操作は
    /// <see cref="AnnotationLayerControl.PreviewGestureRequested"/> でホストへ渡す（透過させると、透過した先の
    /// 要素が入力を受け取り、注釈を選び直せなくなる）
    /// </remarks>
    protected override void WndProc(ref Message m)
    {
        if (IsEditingText)
        {
            switch (m.Msg)
            {
                case WmImeStartComposition:
                    _isComposing = true;
                    SetPreedit(string.Empty);
                    Invalidate();
                    break;
                case WmImeEndComposition:
                    _isComposing = false;
                    SetPreedit(string.Empty);
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
            SetPreedit(string.Empty);
            if (committed.Length > 0)
            {
                ReplaceSelection(committed);
                return;
            }
        }

        if ((flags & GcsCompStr) != 0)
        {
            _isComposing = true;
            SetPreedit(ReadCompositionString(GcsCompStr));
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

        var index = forward ? _session.CaretIndex : _session.CaretIndex - 1;
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
        if (Selected is not BubbleAnnotation)
        {
            return false;
        }

        _isComposing = false;
        SetPreedit(string.Empty);
        // 挿入した文字へ保留している書式を反映する（規則はコアと提示層が単一所有する）
        if (!_session.ReplaceRange(start, end, text))
        {
            return false;
        }

        Invalidate();
        return true;
    }

    /// <summary>選択範囲の文字がすべて条件を満たすかどうかを取得する（書式ボタンの押下状態に使う）。</summary>
    /// <param name="match">書式が条件を満たすかどうかを返す関数。</param>
    /// <returns>すべて満たす場合は <see langword="true"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="match"/> が null の場合。</exception>
    public bool SelectedTextMatches(Func<TextStyle, bool> match) => _session.SelectedTextMatches(match);

    /// <summary>選択範囲の書式を変換する（1 つの属性だけ変える）。</summary>
    /// <param name="transform">書式を変える処理。</param>
    /// <remarks>選択が無ければ以降の入力へ回す（判定は <see cref="TransformTextStyle(Func{TextStyle, TextStyle})"/> が持つ）。</remarks>
    internal void TransformSelectionStyle(Func<TextStyle, TextStyle> transform)
        => _ = TransformTextStyle(transform);

    /// <summary>選択範囲へ書式を丸ごと適用する（色・大きさ・フォント）。</summary>
    /// <param name="style">書式。</param>
    internal void ApplyFullStyle(TextStyle style) => ApplyTextStyle(style);

    /// <summary>本文の編集（選択範囲・キャレット・変換中の文字列）を描く（DIP）。</summary>
    /// <param name="graphics">描画先（DIP）。</param>
    private void DrawEditing(Graphics graphics)
    {
        if (!IsEditingText || ResolveLineIndex() is not { } index)
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
        // 二重になり、キャレットが動かないため「入力が反映されていない」ように見える。
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

        // **キャレットは一般的な速さで点滅させる**（規則は提示層の TextCaretBlink が単一所有する）
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
            || index.ResolveCaret(_session.CaretIndex, AnnotationTextMetrics.Measure) is not { } caret)
        {
            return default;
        }

        var (scale, offset) = ResolveImageTransform();
        return ((caret.X * scale) + offset.X, (caret.Y * scale) + offset.Y, Math.Max(1, caret.Height * scale));
    }

    /// <summary>選択中の吹き出しの行と文字位置の対応を解決する。</summary>
    /// <returns>対応。 解決できない場合は <see langword="null"/>。</returns>
    private TextLineIndex? ResolveLineIndex()
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return null;
        }

        // **行と文字数を同じ本文から作る**: 行は変換中の文字列を含む表示用の本文から作られるため、
        // 文字数の基準も同じ本文でなければ、変換中のキャレットが本文の範囲外になって位置を解決できない
        // （変換候補が画面の隅に飛ぶ）。
        var plan = _presenter.ResolveDrawPlan(_session.Document, AnnotationTextMetrics.Measure, _session.SelectedId);
        var shape = plan.Shapes.FirstOrDefault(
            candidate => string.Equals(candidate.Id, bubble.Id, StringComparison.Ordinal));
        return shape is null ? null : TextLineIndex.Create(shape.Lines, bubble.Text.PlainText);
    }

    /// <summary>本文の編集でキーが押されたときの処理。</summary>
    /// <param name="e">キーの引数。</param>
    /// <returns>処理した場合は <see langword="true"/>。</returns>
    /// <remarks>キーの割り当ては <see cref="TextEditKeyMap"/> が単一所有する（本クラスは操作の実行だけを行う）。</remarks>
    private bool HandleTextEditingKey(KeyEventArgs e)
    {
        var request = TextEditKeyMap.Resolve(ToTextEditKey(e.KeyCode), ToTextEditModifiers(e));
        switch (request.Command)
        {
            case TextEditCommand.ToggleBold:
                return ToggleDecoration(style => style.IsBold, static (style, value) => style with { IsBold = value });
            case TextEditCommand.ToggleItalic:
                return ToggleDecoration(style => style.IsItalic, static (style, value) => style with { IsItalic = value });
            case TextEditCommand.ToggleUnderline:
                return ToggleDecoration(style => style.IsUnderline, static (style, value) => style with { IsUnderline = value });
            case TextEditCommand.ToggleStrike:
                return ToggleDecoration(style => style.IsStrike, static (style, value) => style with { IsStrike = value });
            case TextEditCommand.EndEditing:
                EndTextEditing();
                return true;
            case TextEditCommand.InsertLineBreak:
                ReplaceSelection("\n");
                return true;
            case TextEditCommand.DeleteBackward:
                DeleteAtCaret(forward: false);
                return true;
            case TextEditCommand.DeleteForward:
                DeleteAtCaret(forward: true);
                return true;
            case TextEditCommand.CaretLeft:
                MoveCaretByCluster(-1, request.Extend);
                return true;
            case TextEditCommand.CaretRight:
                MoveCaretByCluster(1, request.Extend);
                return true;
            case TextEditCommand.CaretUp:
                MoveCaretToNeighbourLine(-1, request.Extend);
                return true;
            case TextEditCommand.CaretDown:
                MoveCaretToNeighbourLine(1, request.Extend);
                return true;
            case TextEditCommand.CaretLineStart:
                MoveCaretToLineEdge(first: true, request.Extend);
                return true;
            case TextEditCommand.CaretLineEnd:
                MoveCaretToLineEdge(first: false, request.Extend);
                return true;
            case TextEditCommand.SelectAll:
                _session.SelectAllText();
                Invalidate();
                return true;
            case TextEditCommand.Copy:
            case TextEditCommand.Cut:
            case TextEditCommand.Paste:
                HandleClipboard(request.Command);
                return true;
            default:
                return false;
        }
    }

    /// <summary>UI の修飾キーを本層の修飾キーへ写す。</summary>
    /// <param name="e">キーの引数。</param>
    /// <returns>本層の修飾キー。</returns>
    private static TextEditModifiers ToTextEditModifiers(KeyEventArgs e)
    {
        var result = TextEditModifiers.None;
        if (e.Control)
        {
            result |= TextEditModifiers.Control;
        }

        if (e.Shift)
        {
            result |= TextEditModifiers.Shift;
        }

        if (e.Alt)
        {
            result |= TextEditModifiers.Alt;
        }

        return result;
    }

    /// <summary>UI のキーを本層のキーへ写す（本層が扱うキーだけ）。</summary>
    /// <param name="key">UI のキー。</param>
    /// <returns>本層のキー。 扱わないキーは <see cref="TextEditKey.None"/>。</returns>
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

    /// <summary>クリップボードの操作（コピー・切り取り・貼り付け）を行う。</summary>
    /// <param name="command">実行する操作。</param>
    private void HandleClipboard(TextEditCommand command)
    {
        var start = TextSelectionStart;
        var length = TextSelectionLength;
        if (command == TextEditCommand.Paste)
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
        if (command == TextEditCommand.Cut)
        {
            ReplaceSelection(string.Empty);
        }
    }

    /// <summary>選択範囲の装飾を切り替える。</summary>
    /// <param name="read">属性を読む関数。</param>
    /// <param name="write">属性を書く関数。</param>
    /// <returns>処理した場合は <see langword="true"/>。</returns>
    private bool ToggleDecoration(
        Func<TextStyle, bool> read, Func<TextStyle, bool, TextStyle> write)
        // 適用先（選択範囲・選択が無ければ以降の入力）の決定は Apply/Transform が単一所有する
        => TransformTextStyle(style => write(style, !read(style)));

    /// <summary>キャレットを前後の 1 クラスタへ移す。</summary>
    /// <param name="direction">移動する向き（負は左、正は右）。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    private void MoveCaretByCluster(int direction, bool extend)
    {
        _session.MoveCaretByCluster(direction, extend);
        Invalidate();
    }

    /// <summary>キャレットを上下の行の同じ桁へ移す。</summary>
    /// <param name="delta">移動する行数。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    private void MoveCaretToNeighbourLine(int delta, bool extend)
    {
        _session.MoveCaretByLine(ResolveLineIndex(), delta, extend);
        Invalidate();
    }

    /// <summary>キャレットを行の先頭・末尾へ移す。</summary>
    /// <param name="first">行の先頭へ移す場合は <see langword="true"/>。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    private void MoveCaretToLineEdge(bool first, bool extend)
    {
        _session.MoveCaretToLineEdge(ResolveLineIndex(), first, extend);
        Invalidate();
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

        _session.SetCaretFromPoint(index, point, extend, AnnotationTextMetrics.Measure);
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

