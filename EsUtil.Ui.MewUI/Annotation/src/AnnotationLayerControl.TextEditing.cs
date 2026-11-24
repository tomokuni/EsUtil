using Aprillz.MewUI;
using Aprillz.MewUI.Text;
using Aprillz.MewUI.Rendering;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.Annotation.Presenter.Drawing;
using EsUtil.Feature.Text;
using EsUtil.Feature.Text.Core.Text;
using EsUtil.Feature.Text.Presenter.Editing;

namespace EsUtil.Ui.MewUI.Annotation;

/// <summary>注釈レイヤーの本文の編集（表示中のレイアウトのまま、その場で編集する）。</summary>
/// <remarks>
/// <b>入力欄を重ねない</b>: 本文はコントロールが描いているため、入力欄を重ねると書式が反映されず、
/// 書式バーを押すたびにフォーカスが移って選択が分からなくなる。 本文を直接編集し、キー入力のたびに
/// 文書を書き換える（書式はその場で反映される）。<br/>
/// <b>座標の対応</b>: 折り返しは <see cref="AnnotationPresenter"/> が解決した行を使い、行の開始位置は
/// 本文の文字列から数え直す（改行は行の区切りとして 1 文字を消費する）。<br/>
/// <b>日本語入力</b>: IME の変換中文字列は文書へ入れず、キャレットの位置へ下線付きで描く
/// （確定した文字列だけを文書へ入れる）。
/// </remarks>
public sealed partial class AnnotationLayerControl
{
    /// <summary>本文の編集状態（キャレット・選択・入力の規則は提示層が単一所有する）。</summary>
    /// <summary>直近に通知した選択範囲（同じ範囲で通知を繰り返さない）。</summary>


    /// <summary>IME の変換中文字列（文書へは入れない）。</summary>
    private string _preeditText = string.Empty;

    /// <summary>変換中の文字列の先頭の位置（文字数。 IME へ位置を答えるのに使う）。</summary>
    private int _compositionStart;


    /// <summary>ポインターで文字を選んでいる最中かどうか。</summary>
    private bool _isSelectingText;

    /// <summary>入力イベントを購読しているウィンドウ（購読していない間は <see langword="null"/>）。</summary>
    private Window? _inputWindow;

    /// <summary>本文の編集中に発生する（開始・終了の両方）。</summary>
    /// <remarks>ホストは書式バーの表示・非表示を切り替える。</remarks>
    public event EventHandler? EditingChanged;

    /// <summary>本文の選択範囲（キャレットの位置）が変わったときに発生する。</summary>
    public event EventHandler? TextSelectionChanged;

    /// <summary>本文を編集中かどうかを取得する。</summary>
    public bool IsEditingText => _session.IsEditingText;

    /// <summary>キャレットの位置（文字数）を取得する。</summary>
    public int CaretIndex => _session.CaretIndex;

    /// <summary>本文の文字を選んでいるかどうかを取得する（書式の適用先があるか）。</summary>
    public bool HasTextSelection => IsEditingText && _session.SelectionLength > 0;

    /// <summary>選択範囲の開始位置（文字数）を取得する。</summary>
    public int SelectionStart => _session.SelectionStart;

    /// <summary>選択範囲の終了位置（文字数）を取得する。</summary>
    public int SelectionEnd => _session.SelectionEnd;

    /// <summary>選択中の本文を編集する（表示中のレイアウトのまま、その場で入力する）。</summary>
    /// <returns>編集を始めた場合は <see langword="true"/>。 枠線を選んでいる場合は <see langword="false"/>。</returns>
    public bool BeginTextEditing()
    {
        if (!_session.BeginTextEditing())
        {
            return false;
        }

        Focus();
        AttachInputWindow();
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

    /// <summary>キャレットの点滅を始めた時刻。</summary>
    private DateTimeOffset _caretBlinkStart = DateTimeOffset.UtcNow;

    /// <summary>キャレットの点滅を進めるタイマー（編集中だけ動かす）。</summary>
    private DispatcherTimer? _caretTimer;

    /// <summary>いまキャレットを点灯して描くかどうかを取得する。</summary>
    /// <value>点滅の位相が点灯の間は <see langword="true"/>。</value>
    private bool IsCaretVisible => TextCaretBlink.IsVisible(DateTimeOffset.UtcNow - _caretBlinkStart);

    /// <summary>キャレットの点滅を点灯から始め直す（文字入力・キャレット移動の直後に呼ぶ）。</summary>
    private void RestartCaretBlink()
    {
        _caretBlinkStart = DateTimeOffset.UtcNow;
        if (_caretTimer is null)
        {
            var timer = new DispatcherTimer(TextCaretBlink.Interval);
            timer.Tick += () =>
            {
                if (IsEditingText)
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
    internal string TextForInput => _session.SelectedPlainText;

    /// <summary>本文の選択範囲を IME から設定する。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="end">終了位置（文字数）。</param>
    internal void SetSelectionFromInput(int start, int end)
    {
        var length = TextForInput.Length;
        var from = Math.Clamp(start, 0, length);
        var to = Math.Clamp(end, from, length);
        _session.SelectText(from, to - from);
        InvalidateVisual();
    }

    /// <summary>IME の変換中文字列を設定する（文書へは入れない）。</summary>
    /// <param name="preeditText">変換中の文字列（<see langword="null"/> は変換の終了）。</param>
    /// <remarks>
    /// <b>候補の位置は自前で指定しない</b>: MewUI のバックエンドがフォーカス中の要素
    /// （<c>Aprillz.MewUI.Input.ITextCompositionClient</c>）から変換中の文字の矩形を得て位置を決めます
    /// （本レイヤーは <c>AnnotationLayerControl.Ime.cs</c> で実装）。
    /// 自前で <c>ImmSetCandidateWindow</c> を呼ぶと、後からバックエンドが上書きするため効果がありません。
    /// </remarks>
    internal void SetPreeditTextForInput(string? preeditText)
    {
        _preeditText = preeditText ?? string.Empty;
        InvalidateVisual();
    }

    /// <summary>本文の編集でキーが押されたときの処理。</summary>
    /// <param name="e">キーの引数。</param>
    /// <returns>処理した場合は <see langword="true"/>。</returns>
    /// <remarks>キーの割り当ては <see cref="TextEditKeyMap"/> が単一所有する（本クラスは操作の実行だけを行う）。</remarks>
    private bool HandleTextEditingKey(KeyEventArgs e)
    {
        var request = TextEditKeyMap.Resolve(ToTextEditKey(e.Key), ToTextEditModifiers(e));
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
                InvalidateVisual();
                return true;
            case TextEditCommand.Copy:
                CopySelection();
                return true;
            case TextEditCommand.Cut:
                if (CopySelection())
                {
                    ReplaceSelection(string.Empty);
                }

                return true;
            case TextEditCommand.Paste:
                var pasted = AnnotationClipboard.GetText();
                if (pasted.Length > 0)
                {
                    ReplaceSelection(NormalizeInput(pasted));
                }

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

    /// <summary>ウィンドウの入力イベントを購読する（フォーカスを失ったときの後片付けだけ）。</summary>
    /// <remarks>
    /// <b>文字と変換はウィンドウのプレビューで受け取らない</b>: プレビューで <c>Handled</c> を立てると、
    /// MewUI のバックエンドが <b>変換候補の位置を設定する処理を行わなくなります</b>
    /// （<c>RaisePreviewTextCompositionStart</c> の後、<c>Handled</c> が立っていると
    /// <c>ITextCompositionClient</c> の呼び出しと <c>PositionImeWindow</c> を飛ばす）
    /// そのため候補が画面の隅（右下）に出ます。 文字と変換は
    /// <see cref="Aprillz.MewUI.Input.ITextInputClient"/> / <see cref="Aprillz.MewUI.Input.ITextCompositionClient"/>
    /// （<c>AnnotationLayerControl.Ime.cs</c>）で受け取り、バックエンドに位置を決めさせます。
    /// </remarks>
    private void AttachInputWindow()
    {
        if (_inputWindow is not null || FindVisualRoot() is not Window window)
        {
            return;
        }

        _inputWindow = window;
        window.LostFocus += OnWindowLostFocus;
    }

    /// <summary>ウィンドウの入力イベントの購読を解除する。</summary>
    private void DetachInputWindow()
    {
        if (_inputWindow is { } window)
        {
            window.LostFocus -= OnWindowLostFocus;
            _inputWindow = null;
        }
    }

    /// <summary>確定した文字列を本文へ入れる。</summary>
    /// <param name="e">入力の引数。</param>
    private void ApplyTextInput(TextInputEventArgs e)
    {
        if (!IsEditingText || !IsFocused || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        // **変換中に届いた確定文字列を取りこぼさない**: IME によっては確定した文字列が
        // 変換の終了より先にここへ届く。 変換中だからと捨てると、**確定した瞬間に文字が消える**。
        // ここで変換を終えたものとして本文へ入れる。
        _preeditText = string.Empty;
        _compositionStart = _session.CaretIndex;
        ReplaceSelection(NormalizeInput(e.Text));
        e.Handled = true;
    }

    /// <summary>IME の変換の開始を反映する。</summary>
    /// <param name="e">変換の引数。</param>
    private void ApplyCompositionStart(TextCompositionEventArgs e)
    {
        if (!IsEditingText || !IsFocused)
        {
            return;
        }

        // **変換の先頭を覚える**: 候補の位置は「変換中の文字列の先頭の文字の矩形」から決まる
        // （MewUI は本値と GetCharRectInWindow で IME の位置を決める）
        _compositionStart = SelectionStart;
        SetPreeditTextForInput(e.Text);
        e.Handled = true;
    }

    /// <summary>IME の変換中の文字列の更新を反映する。</summary>
    /// <param name="e">変換の引数。</param>
    private void ApplyCompositionUpdate(TextCompositionEventArgs e)
    {
        if (!IsEditingText || !IsFocused)
        {
            return;
        }

        SetPreeditTextForInput(e.Text);
        e.Handled = true;
    }

    /// <summary>IME の変換の確定を反映する。</summary>
    /// <param name="e">変換の引数。</param>
    private void ApplyCompositionEnd(TextCompositionEventArgs e)
    {
        if (!IsEditingText || !IsFocused)
        {
            return;
        }

        _preeditText = string.Empty;
        _compositionStart = _session.CaretIndex;
        if (!string.IsNullOrEmpty(e.Text))
        {
            ReplaceSelection(NormalizeInput(e.Text));
        }
        else
        {
            InvalidateVisual();
        }

        e.Handled = true;
    }

    /// <summary>ウィンドウがフォーカスを失ったら入力の購読を解除する（別ウィンドウへ入力を漏らさない）。</summary>
    private void OnWindowLostFocus() => DetachInputWindow();

    /// <summary>入力文字列の改行を本文の規則（<c>\n</c>）へ揃える。</summary>
    /// <param name="text">入力文字列。</param>
    /// <returns>揃えた文字列。</returns>
    private static string NormalizeInput(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", string.Empty, StringComparison.Ordinal);

    /// <summary>入力した文字列で選択範囲を置き換える（以降の入力へ効かせる書式も同時に反映する）。</summary>
    /// <param name="text">置き換える文字列（空なら削除）。</param>
    /// <returns>文書を変えた場合は <see langword="true"/>。</returns>
    private bool ReplaceSelection(string text)
    {
        if (Selected is not BubbleAnnotation)
        {
            return false;
        }

        _preeditText = string.Empty;

        // 挿入した文字へ保留している書式を反映する（規則はコアと提示層が単一所有する）
        if (!_session.ReplaceSelection(text))
        {
            return false;
        }

        InvalidateVisual();
        return true;
    }

    /// <summary>選択範囲、またはキャレットの前後の 1 文字を削除する。</summary>
    /// <param name="forward">キャレットの次の文字を削除する場合は <see langword="true"/>。</param>
    private void DeleteAtCaret(bool forward)
    {
        if (_session.DeleteAtCaret(forward))
        {
            InvalidateVisual();
        }
    }

    /// <summary>キャレットを前後の 1 クラスタへ移す。</summary>
    /// <param name="direction">移動する向き（負は左、正は右）。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    private void MoveCaretByCluster(int direction, bool extend)
    {
        _session.MoveCaretByCluster(direction, extend);
        InvalidateVisual();
    }

    /// <summary>キャレットを上下の行の同じ桁へ移す。</summary>
    /// <param name="delta">移動する行数。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    private void MoveCaretToNeighbourLine(int delta, bool extend)
    {
        _session.MoveCaretByLine(ResolveLineIndex(), delta, extend);
        InvalidateVisual();
    }

    /// <summary>キャレットを行の先頭・末尾へ移す。</summary>
    /// <param name="first">行の先頭へ移す場合は <see langword="true"/>。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    private void MoveCaretToLineEdge(bool first, bool extend)
    {
        _session.MoveCaretToLineEdge(ResolveLineIndex(), first, extend);
        InvalidateVisual();
    }

    /// <summary>選択範囲の装飾（太字・斜体・下線・取り消し線）を切り替える。</summary>
    /// <param name="read">属性を読む関数。</param>
    /// <param name="write">属性を書く関数。</param>
    /// <returns>処理した場合は <see langword="true"/>。</returns>
    private bool ToggleDecoration(
        Func<TextStyle, bool> read,
        Func<TextStyle, bool, TextStyle> write)
        // 適用先（選択範囲・選択が無ければ以降の入力）の決定は Apply/Transform が単一所有する
        => TransformTextStyle(style => write(style, !read(style)));

    /// <summary>選択範囲の文字がすべて条件を満たすかどうかを取得する（書式ボタンの押下状態に使う）。</summary>
    /// <param name="match">書式が条件を満たすかどうかを返す関数。</param>
    /// <returns>すべて満たす場合は <see langword="true"/>。</returns>
    /// <remarks>選択が無い間は、書式バーで選んだ書式（以降の入力へ効く値）で判定する。</remarks>
    public bool SelectedTextMatches(Func<TextStyle, bool> match) => _session.SelectedTextMatches(match);

    /// <summary>選択中の文字をクリップボードへコピーする。</summary>
    /// <returns>コピーした場合は <see langword="true"/>。</returns>
    private bool CopySelection() => AnnotationClipboard.TrySetText(_session.SelectedText());

    /// <summary>本文の編集（選択範囲・キャレット・変換中の文字列）を描く（元画像座標）。</summary>
    /// <param name="context">描画先。</param>
    /// <param name="scale">表示倍率（キャレットの太さを DIP 一定にする）。</param>
    /// <param name="layouts">文字のレイアウトを取得する関数（描画と同じ実体を返す）。</param>
    private void DrawTextEditing(
        IGraphicsContext context, double scale, Func<string, TextStyle, ITextLayout> layouts)
    {
        if (!IsEditingText || ResolveLineIndex() is not { } index)
        {
            return;
        }

        var start = SelectionStart;
        var end = SelectionEnd;
        for (var i = 0; i < index.Lines.Count; i++)
        {
            var line = index.Lines[i];
            var lineStart = index.LineStart(i);
            var from = Math.Max(start, lineStart);
            var to = Math.Min(end, lineStart + TextLineLayout.Length(line));
            if (to <= from)
            {
                continue;
            }

            // **選択範囲も描画と同じ規則で求める**（区間の原点からの距離。 Core が単一所有する）
            var x1 = TextLineLayout.ResolveX(line, from - lineStart, PrefixMeasure(layouts));
            var x2 = TextLineLayout.ResolveX(line, to - lineStart, PrefixMeasure(layouts));
            context.FillRectangle(new Rect(x1, line.Bounds.Y, x2 - x1, line.Bounds.Height), AnnotationPainter.ToColor(AnnotationEditorAppearance.SelectionColor));
        }

        // **変換中はキャレットを変換中の文字列の末尾へ置く**（規則は提示層の AnnotationCompositionPreview）
        if (!TryResolveCaretSource(
            AnnotationCompositionPreview.ResolveCaret(_session.CaretIndex, _preeditText), layouts, out var caret))
        {
            return;
        }

        // **キャレットは一般的な速さで点滅させる**（規則は提示層の TextCaretBlink が単一所有する）
        if (IsCaretVisible)
        {
            var thickness = AnnotationEditorAppearance.CaretThicknessDip / Math.Max(scale, 0.01);
            context.FillRectangle(new Rect(caret.X, caret.Y, thickness, caret.Height), AnnotationPainter.ToColor(AnnotationEditorAppearance.CaretColor));
        }

        // **変換中の文字列は本文として描く**（RenderDocument が入れている）ため、ここでは重ねて描かない。
        // 重ねて描くと既存の文字と二重になる。
    }

    /// <summary>キャレットを点（元画像ピクセル）の位置へ移す（選択の起点は現在の位置に保つ）。</summary>
    /// <param name="point">位置（元画像ピクセル）。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    private void SetCaretFromPoint(EsPoint point, bool extend)
    {
        if (ResolveLineIndex() is not { } lines)
        {
            return;
        }

        _session.SetCaretFromPoint(lines, point, extend, AnnotationTextMetrics.Measure);
        InvalidateVisual();
    }

    /// <summary>キャレットの位置（元画像ピクセル）を求める。</summary>
    /// <param name="index">文字位置。</param>
    /// <param name="layouts">文字のレイアウトを取得する関数（描画と同じ実体を返す）。</param>
    /// <param name="caret">キャレットの左上と高さ。</param>
    /// <returns>求めた場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>文字と同じベースラインで上下を決める</b>: 行の上端をそのまま使うと、実際に描かれる文字より
    /// キャレットが上へずれる。 文字の描画（<c>AnnotationPainter</c>）と同じ
    /// ベースラインから上端と高さを求め、文字の範囲に揃える。
    /// </remarks>
    internal bool TryResolveCaretSource(
        int index, Func<string, TextStyle, ITextLayout> layouts, out (double X, double Y, double Height) caret)
    {
        ArgumentNullException.ThrowIfNull(layouts);
        caret = default;
        if (ResolveLineIndex() is not { } source)
        {
            return false;
        }

        var lineIndex = source.LineIndexOf(index);
        var line = source.Lines[lineIndex];
        var offset = Math.Clamp(index - source.LineStart(lineIndex), 0, TextLineLayout.Length(line));

        // **位置は描画と同じ規則で求める**: 区間の原点（描画に使う Bounds.X）からの距離で決める
        // （規則の所有者は Core の TextLineLayout。行頭から積み上げると文字からずれる）
        var prefix = PrefixMeasure(layouts);
        var x = TextLineLayout.ResolveX(line, offset, prefix);

        // **縦位置も描画と同じ区間の書式で決める**: キャレットの位置の書式で測ると、太字や大きさの違う
        // 区間へキャレットが入ったときに上下へずれる。 描画は区間ごとのレイアウトで
        // 上端を決めているため、同じレイアウトのベースライン・高さを使う。
        var (top, height) = ResolveCaretVertical(line, offset, layouts);
        caret = (x, top, height);
        return true;
    }

    /// <summary>キャレットの上端と高さ（元画像ピクセル）を、その位置を描く区間から求める。</summary>
    /// <param name="line">行。</param>
    /// <param name="offset">行の先頭からの文字位置。</param>
    /// <param name="layouts">文字のレイアウトを取得する関数（描画と同じ実体を返す）。</param>
    /// <returns>上端と高さ。</returns>
    private static (double Top, double Height) ResolveCaretVertical(
        TextLinePlan line, int offset, Func<string, TextStyle, ITextLayout> layouts)
    {
        if (line.Segments.Count == 0)
        {
            return (line.Bounds.Y, line.Bounds.Height);
        }

        var (segmentIndex, _) = TextLineLayout.Locate(line, offset);
        var segment = line.Segments[segmentIndex];
        var segmentLayout = layouts(segment.Text, segment.Style);
        var baseline = TextLineLayout.ResolveBaseline(line, AnnotationTextMetrics.MeasureLineMetrics);
        var segmentBaseline = segmentLayout.Lines.Count > 0 ? segmentLayout.Lines[0].Baseline : segmentLayout.MeasuredSize.Height;
        return (baseline - segmentBaseline, segmentLayout.MeasuredSize.Height);
    }

    /// <summary>1 度だけ使うレイアウトの生成器（当たり判定など、描画と同じフレームに居ない処理で使う）。</summary>
    private static readonly Func<string, TextStyle, ITextLayout> DefaultLayouts =
        static (text, style) => AnnotationTextMetrics.CreateLayout(text, style, ignoreItalic: false);

    /// <summary>区間の先頭から指定した文字数までの幅を測る関数を作る。</summary>
    /// <param name="layouts">文字のレイアウトを取得する関数（描画と同じ実体を返す）。</param>
    /// <returns>区間と文字数から幅を返す関数（元画像ピクセル）。</returns>
    /// <remarks>
    /// <b>描画と同じレイアウト実体で測る</b>: 描画（`AnnotationPainter`）は区間の文字列から作った
    /// レイアウトを区間の原点へ置く。 位置を同じ実体の中で測れば、カーニング・字形・末尾の空白の扱いが
    /// 描画と必ず一致する。
    /// </remarks>
    private static TextLineLayout.MeasurePrefix PrefixMeasure(
        Func<string, TextStyle, ITextLayout> layouts)
        => (segment, count) =>
        {
            var layout = layouts(segment.Text, segment.Style);
            if (count <= 0)
            {
                return 0;
            }

            // **末尾は実測値（MeasuredSize）を使う**: `GetCaretBounds(全文字数)` は
            // 「最後の文字の開始位置」を返すため、末尾のキャレットが最後の 1 文字の内側へ入る。
            // 途中の位置は「k 文字送った位置」を
            // 返す `GetCaretBounds(k)` を使う（描画が次に文字を置く位置と一致する）。
            return count >= segment.Text.Length
                ? layout.MeasuredSize.Width
                : layout.GetCaretBounds(new CharacterHit(count, 0)).X;
        };
    /// <summary>選択中の吹き出しの本文の行を解決する。</summary>
    /// <returns>行と文字位置の対応。 解決できない場合は <see langword="null"/>。</returns>
    private TextLineIndex? ResolveLineIndex()
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return null;
        }

        var plan = _presenter.ResolveDrawPlan(RenderDocument, AnnotationTextMetrics.Measure, SelectedId);
        var shape = plan.Shapes.FirstOrDefault(
            candidate => string.Equals(candidate.Id, bubble.Id, StringComparison.Ordinal));
        return shape is null ? null : TextLineIndex.Create(shape.Lines, bubble.Text.PlainText);
    }
}

