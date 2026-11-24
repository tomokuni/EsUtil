using EsUtil.Feature.StyledText.Core.Text;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.StyledText.Presenter.Editing;

/// <summary>編集中の本文（値・キャレット・選択・書式・変換中の文字列）の状態と操作（単一所有）。</summary>
/// <remarks>
/// <b>編集の規則は提示層が単一所有する</b>: キャレット・選択・書式の適用は <see cref="TextEditState"/>、キーの割り当ては
/// <see cref="TextEditKeyMap"/>、折り返しと行の配置は <see cref="TextLayout"/> が決める。 本クラスは「どの本文を編集しているか」を
/// 1 か所に持ち、UI が同じ順序で同じ結果を得るための入口になる。<br/>
/// <b>UI は本クラスを操作し、描画と通知だけを行う</b>: 値・状態の写しを UI ごとに持たない（写しは片方だけ直したときに食い違う）。<br/>
/// <b>本文は不変</b>である: 編集は新しい <see cref="RichText"/> を <see cref="Text"/> へ書き戻し、変化が無ければ何もしない。<br/>
/// <b>クリップボードとキーの受理は UI が持つ</b>: 本クラスは要求（<see cref="TextEditCommand"/>）のうち本文の操作だけを実行する。
/// </remarks>
public sealed class TextEditor
{
    /// <summary>編集している本文（キャレットと選択の範囲は本値の文字数で数える）。</summary>
    private RichText _text = RichText.Empty;

    /// <summary>キャレット・選択・書式の状態。</summary>
    private TextEditState _state = new();

    /// <summary>IME の変換中の文字列（本文へは入れない）。</summary>
    private string _preeditText = string.Empty;

    /// <summary>編集している本文を取得または設定する。</summary>
    /// <value>表示・編集する本文。 設定すると編集位置を本文の範囲へ収める。</value>
    /// <exception cref="ArgumentNullException">設定する値が <see langword="null"/> の場合。</exception>
    public RichText Text
    {
        get => _text;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            _text = value;
            if (_state.IsActive)
            {
                _state.Clamp(_text.PlainText.Length);
            }
        }
    }

    /// <summary>キャレット・選択・書式の状態を取得または設定する。</summary>
    /// <value>既定は本クラスが作った状態。 ホストが既に状態を持っている場合はその実体を設定する。</value>
    /// <remarks><b>状態を 2 つ持たない</b>: ホストと共有する場合は同じ実体を設定する（別の状態を持つとキャレットと本文が食い違う）。</remarks>
    public TextEditState EditState
    {
        get => _state;
        set => _state = value ?? new TextEditState();
    }

    /// <summary>IME の変換中の文字列を取得または設定する（本文へは入れない）。</summary>
    /// <value>変換中の文字列。 空なら変換していない。</value>
    public string PreeditText
    {
        get => _preeditText;
        set => _preeditText = value ?? string.Empty;
    }

    /// <summary>編集中に書式バーを自動で表示するかどうかを取得または設定する。</summary>
    /// <value>自動で表示する場合は <see langword="true"/>（既定）。 ホストが独自にバーを出す場合は <see langword="false"/>。</value>
    public bool ShowFormatBarWhileEditing { get; set; } = true;

    /// <summary>いま書式バーを表示すべきかどうかを取得する。</summary>
    /// <value>編集中で、自動表示が有効で、1 文字以上を選んでいる場合は <see langword="true"/>。</value>
    public bool ShouldShowFormatBar => _state.ShouldShowFormatBar(ShowFormatBarWhileEditing);

    /// <summary>本文を編集中かどうかを取得する。</summary>
    public bool IsEditing => _state.IsActive;

    /// <summary>キャレットの位置（文字数）を取得する。</summary>
    public int CaretIndex => _state.CaretIndex;

    /// <summary>選択範囲の開始位置（文字数）を取得する。</summary>
    public int SelectionStart => _state.SelectionStart;

    /// <summary>選択範囲の終了位置（文字数）を取得する。</summary>
    public int SelectionEnd => _state.SelectionEnd;

    /// <summary>選択範囲の長さ（文字数）を取得する。</summary>
    public int SelectionLength => _state.SelectionLength;

    /// <summary>本文の文字を選んでいるかどうかを取得する。</summary>
    public bool HasSelection => _state.HasSelection;

    /// <summary>書式バーへ表示する書式を取得する（選択が無い間は覚えた書式）。</summary>
    public TextStyle SelectedTextStyle => _state.ResolveDisplayStyle(_text);

    /// <summary>描画に使う本文を取得する（変換中の文字列を含む）。</summary>
    /// <value>変換中でなければ <see cref="Text"/>。 変換中は変換中の文字列を入れた描画用の値。</value>
    public RichText DisplayText => TextCompositionPreview.Apply(_text, _state.CaretIndex, _preeditText);

    /// <summary>描画に使うキャレットの位置を取得する（変換中の文字列の分だけ進む）。</summary>
    public int DisplayCaretIndex => TextCompositionPreview.ResolveCaret(_state.CaretIndex, _preeditText);

    /// <summary>本文の編集を始める（キャレットを本文の末尾へ置く）。</summary>
    /// <returns>変わった内容。 既に編集中の場合は <see cref="TextEditChange.None"/>。</returns>
    public TextEditChange Begin()
    {
        if (_state.IsActive)
        {
            return TextEditChange.None;
        }

        _state.Begin(_text.PlainText.Length);
        _preeditText = string.Empty;
        return TextEditChange.Editing | TextEditChange.Selection;
    }

    /// <summary>本文の編集を終える（内容は既に <see cref="Text"/> へ入っている）。</summary>
    /// <returns>変わった内容。 編集中でない場合は <see cref="TextEditChange.None"/>。</returns>
    public TextEditChange End()
    {
        if (!_state.IsActive)
        {
            return TextEditChange.None;
        }

        _state.End();
        _preeditText = string.Empty;
        return TextEditChange.Editing | TextEditChange.Selection;
    }

    /// <summary>選択範囲を指定する（端は書記素クラスタの境界へ揃える）。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（文字数。 0 は選択なし）。</param>
    /// <returns>変わった内容（常に <see cref="TextEditChange.Selection"/>）。</returns>
    public TextEditChange Select(int start, int length)
    {
        _state.Select(_text, start, length);
        return TextEditChange.Selection;
    }

    /// <summary>本文全体を選択する。</summary>
    /// <returns>変わった内容（常に <see cref="TextEditChange.Selection"/>）。</returns>
    public TextEditChange SelectAll()
    {
        _state.SelectAll(_text);
        return TextEditChange.Selection;
    }

    /// <summary>選択範囲を指定する（IME が持つ選択位置を写す場合）。</summary>
    /// <param name="start">開始位置（文字数）。 範囲外は本文の範囲へ丸める。</param>
    /// <param name="end">終了位置（文字数）。 範囲外は本文の範囲へ丸める。</param>
    /// <returns>変わった内容（常に <see cref="TextEditChange.Selection"/>）。</returns>
    public TextEditChange SelectRange(int start, int end)
    {
        var length = _text.PlainText.Length;
        var from = Math.Clamp(start, 0, length);
        var to = Math.Clamp(end, from, length);
        _state.Select(_text, from, to - from);
        return TextEditChange.Selection;
    }

    /// <summary>キャレットを指定位置へ移す（書記素クラスタの境界へ揃える）。</summary>
    /// <param name="index">移動先（文字数）。 範囲外は本文の範囲へ丸める。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    /// <returns>変わった内容（常に <see cref="TextEditChange.Selection"/>）。</returns>
    public TextEditChange MoveCaret(int index, bool extend)
    {
        _state.MoveToCluster(_text, index, extend);
        return TextEditChange.Selection;
    }

    /// <summary>キャレットを前後の 1 クラスタへ移す（選択があるときは選択の端へ畳む）。</summary>
    /// <param name="direction">移動する向き（負は左、正は右）。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    /// <returns>変わった内容（常に <see cref="TextEditChange.Selection"/>）。</returns>
    public TextEditChange MoveCaretByCluster(int direction, bool extend)
        => MoveCaret(_state.ResolveClusterTarget(direction, extend), extend);

    /// <summary>キャレットを上下の行の同じ桁へ移す。</summary>
    /// <param name="lines">本文の行（無い場合は移動しない）。</param>
    /// <param name="delta">移動する行数。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    /// <returns>変わった内容（常に <see cref="TextEditChange.Selection"/>）。</returns>
    public TextEditChange MoveCaretByLine(TextLineIndex? lines, int delta, bool extend)
    {
        _state.MoveByLine(lines, _text.PlainText.Length, delta, extend);
        _state.SnapCaretToCluster(_text, extend);
        return TextEditChange.Selection;
    }

    /// <summary>キャレットを行の先頭・末尾へ移す。</summary>
    /// <param name="lines">本文の行（無い場合は本文の先頭・末尾へ移す）。</param>
    /// <param name="first">行の先頭へ移す場合は <see langword="true"/>。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    /// <returns>変わった内容（常に <see cref="TextEditChange.Selection"/>）。</returns>
    public TextEditChange MoveCaretToLineEdge(TextLineIndex? lines, bool first, bool extend)
    {
        _state.MoveToLineEdge(lines, _text.PlainText.Length, first, extend);
        _state.SnapCaretToCluster(_text, extend);
        return TextEditChange.Selection;
    }

    /// <summary>位置（元画像ピクセル）に最も近い文字位置へキャレットを移す。</summary>
    /// <param name="lines">本文の行。</param>
    /// <param name="point">位置（元画像ピクセル）。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    /// <param name="measureText">文字列と書式から実寸を測る関数。</param>
    /// <returns>変わった内容（常に <see cref="TextEditChange.Selection"/>）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> または <paramref name="measureText"/> が null の場合。</exception>
    public TextEditChange SetCaretFromPoint(
        TextLineIndex lines, EsPoint point, bool extend, Func<string, TextStyle, EsSize> measureText)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(measureText);

        _state.SetCaretFromPoint(lines, point, _text.PlainText.Length, extend, measureText);
        return TextEditChange.Selection;
    }

    /// <summary>キャレットと選択の起点を本文の範囲へ収める（履歴から戻したときなど）。</summary>
    /// <returns>変わった内容（常に <see cref="TextEditChange.Selection"/>）。</returns>
    public TextEditChange ClampCaret()
    {
        _state.Clamp(_text.PlainText.Length);
        _state.SnapCaretToCluster(_text, extend: true);
        return TextEditChange.Selection;
    }

    /// <summary>選択範囲を文字列で置き換える（選択が無ければ挿入）。</summary>
    /// <param name="inserted">挿入する文字列（空なら削除）。</param>
    /// <returns>変わった内容。 本文が変わらない場合は <see cref="TextEditChange.Selection"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="inserted"/> が null の場合。</exception>
    public TextEditChange ReplaceSelection(string inserted)
    {
        ArgumentNullException.ThrowIfNull(inserted);
        return Commit(_state.ReplaceSelection(_text, inserted));
    }

    /// <summary>指定した範囲を文字列で置き換える（ホストが範囲を指定する場合）。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="end">終了位置（文字数）。</param>
    /// <param name="inserted">置き換える文字列（空なら削除）。</param>
    /// <returns>変わった内容。 本文が変わらない場合は <see cref="TextEditChange.Selection"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="inserted"/> が null の場合。</exception>
    public TextEditChange ReplaceRange(int start, int end, string inserted)
    {
        ArgumentNullException.ThrowIfNull(inserted);
        return Commit(_state.ReplaceRange(_text, start, end, inserted));
    }

    /// <summary>選択範囲、またはキャレットの前後の 1 クラスタを削除する。</summary>
    /// <param name="forward">キャレットの次の文字を削除する場合は <see langword="true"/>。</param>
    /// <returns>変わった内容。 本文が変わらない場合は <see cref="TextEditChange.Selection"/>。</returns>
    public TextEditChange DeleteAtCaret(bool forward) => Commit(_state.Delete(_text, forward));

    /// <summary>選択範囲の書式を丸ごと適用する（選択が無ければ以降の入力へ回す）。</summary>
    /// <param name="style">適用する書式。</param>
    /// <returns>変わった内容。 本文が変わらない場合は <see cref="TextEditChange.Selection"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="style"/> が null の場合。</exception>
    public TextEditChange ApplyStyle(TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        return Commit(_state.ApplyStyle(_text, style));
    }

    /// <summary>指定した範囲の書式を丸ごと適用する（ホストが範囲を指定する場合）。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（文字数。 0 以下は本文全体）。</param>
    /// <param name="style">適用する書式。</param>
    /// <returns>変わった内容。 適用する範囲が無い場合は <see cref="TextEditChange.None"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="style"/> が null の場合。</exception>
    public TextEditChange ApplyStyle(int start, int length, TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);

        _state.Remember(style);
        return TextStyleEdit.ResolveRange(start, length) is { } range
            ? Commit(_text.ApplyStyle(range.Start, range.Length, style))
            : TextEditChange.None;
    }

    /// <summary>選択範囲の書式を 1 属性だけ変換する（選択が無ければ以降の入力へ回す）。</summary>
    /// <param name="transform">現在の書式を受け取って新しい書式を返す関数（範囲には 1 度だけ適用する）。</param>
    /// <returns>変わった内容。 本文が変わらない場合は <see cref="TextEditChange.Selection"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="transform"/> が null の場合。</exception>
    public TextEditChange TransformStyle(Func<TextStyle, TextStyle> transform)
        => Commit(_state.TransformStyle(_text, transform));

    /// <summary>指定した範囲の書式を 1 属性だけ変換する（ホストが範囲を指定する場合）。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（文字数。 0 以下は本文全体）。</param>
    /// <param name="transform">現在の書式を受け取って新しい書式を返す関数。</param>
    /// <returns>変わった内容。 本文が変わらない場合は <see cref="TextEditChange.Selection"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="transform"/> が null の場合。</exception>
    public TextEditChange TransformStyle(int start, int length, Func<TextStyle, TextStyle> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        var at = Math.Clamp(start, 0, _text.PlainText.Length);
        var next = transform(_text.StyleAt(at));
        _state.Remember(next);
        return Commit(_text.TransformStyle(start, length, _ => next));
    }

    /// <summary>選択中の文字列を取得する（クリップボードへ渡す）。</summary>
    /// <returns>選択中の文字列（選択が無い場合は空）。</returns>
    public string SelectedText() => _state.GetSelectedText(_text);

    /// <summary>選択範囲の文字がすべて条件を満たすかどうかを返す（書式ボタンの押下状態に使う）。</summary>
    /// <param name="match">書式が条件を満たすかどうかを返す関数。</param>
    /// <returns>すべて満たす場合は <see langword="true"/>。 選択が無い場合は表示中の書式で判定する。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="match"/> が null の場合。</exception>
    public bool SelectedTextMatches(Func<TextStyle, bool> match)
    {
        ArgumentNullException.ThrowIfNull(match);
        return _state.AllMatch(_text, match);
    }

    /// <summary>キーの割り当てが返した操作のうち、本文の操作だけを実行する。</summary>
    /// <param name="request">解決済みの操作。</param>
    /// <param name="resolveLines">本文の行を解決する関数（キャレットの上下移動だけが使う）。</param>
    /// <returns>変わった内容。 実行しない操作は <see cref="TextEditChange.None"/>。</returns>
    /// <remarks>
    /// <b>クリップボードと編集の終了は実行しない</b>: どちらも UI の仕組みを要するため <see cref="TextEditChange.None"/> を返し、
    /// UI が実行する（本クラスは UI のクリップボードもフォーカスも知らない）。
    /// </remarks>
    public TextEditChange TryExecute(TextEditRequest request, Func<TextLineIndex?>? resolveLines = null)
    {
        var extend = request.Extend;
        switch (request.Command)
        {
            case TextEditCommand.ToggleBold:
                return TransformStyle(static style => style with { IsBold = !style.IsBold });
            case TextEditCommand.ToggleItalic:
                return TransformStyle(static style => style with { IsItalic = !style.IsItalic });
            case TextEditCommand.ToggleUnderline:
                return TransformStyle(static style => style with { IsUnderline = !style.IsUnderline });
            case TextEditCommand.ToggleStrike:
                return TransformStyle(static style => style with { IsStrike = !style.IsStrike });
            case TextEditCommand.InsertLineBreak:
                return ReplaceSelection("\n");
            case TextEditCommand.DeleteBackward:
                return DeleteAtCaret(forward: false);
            case TextEditCommand.DeleteForward:
                return DeleteAtCaret(forward: true);
            case TextEditCommand.CaretLeft:
                return MoveCaretByCluster(-1, extend);
            case TextEditCommand.CaretRight:
                return MoveCaretByCluster(1, extend);
            case TextEditCommand.CaretUp:
                return MoveCaretByLine(resolveLines?.Invoke(), -1, extend);
            case TextEditCommand.CaretDown:
                return MoveCaretByLine(resolveLines?.Invoke(), 1, extend);
            case TextEditCommand.CaretLineStart:
                return MoveCaretToLineEdge(resolveLines?.Invoke(), first: true, extend);
            case TextEditCommand.CaretLineEnd:
                return MoveCaretToLineEdge(resolveLines?.Invoke(), first: false, extend);
            case TextEditCommand.SelectAll:
                return SelectAll();
            default:
                return TextEditChange.None;
        }
    }

    /// <summary>本文を折り返して行へ配置する（表示と編集で同じ結果を使う）。</summary>
    /// <param name="bodySize">本文の領域（元画像ピクセル）。</param>
    /// <param name="measureText">文字列と書式から実寸を測る関数。</param>
    /// <param name="lineHeightFactor">行間の倍率。</param>
    /// <returns>行の並び（領域が 0 以下の場合は空）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measureText"/> が null の場合。</exception>
    public IReadOnlyList<TextLinePlan> ResolveLines(
        EsSize bodySize,
        Func<string, TextStyle, EsSize> measureText,
        double lineHeightFactor)
    {
        ArgumentNullException.ThrowIfNull(measureText);

        if (bodySize.Width <= 0 || bodySize.Height <= 0)
        {
            return [];
        }

        return TextLayout.ResolveLines(
            DisplayText,
            new EsRect(0, 0, bodySize.Width, bodySize.Height),
            measureText,
            lineHeightFactor);
    }

    /// <summary>行と文字位置の対応を作る（表示と同じ本文で作る）。</summary>
    /// <param name="bodySize">本文の領域（元画像ピクセル）。</param>
    /// <param name="measureText">文字列と書式から実寸を測る関数。</param>
    /// <param name="lineHeightFactor">行間の倍率。</param>
    /// <returns>対応。 行が無い場合は <see langword="null"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measureText"/> が null の場合。</exception>
    public TextLineIndex? ResolveLineIndex(
        EsSize bodySize,
        Func<string, TextStyle, EsSize> measureText,
        double lineHeightFactor)
    {
        ArgumentNullException.ThrowIfNull(measureText);

        var displayed = DisplayText;
        return TextLineIndex.Create(ResolveLines(bodySize, measureText, lineHeightFactor), displayed.PlainText);
    }

    /// <summary>キャレットの矩形（元画像ピクセル）を求める。</summary>
    /// <param name="bodySize">本文の領域（元画像ピクセル）。</param>
    /// <param name="measureText">文字列と書式から実寸を測る関数。</param>
    /// <param name="lineHeightFactor">行間の倍率。</param>
    /// <returns>キャレットの矩形。 解決できない場合は <see langword="null"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measureText"/> が null の場合。</exception>
    /// <remarks><b>変換中の文字列の分だけ進めた位置を使う</b>: 変換候補は変換中の文字の末尾へ出す。</remarks>
    public TextCaretRect? ResolveCaretRect(
        EsSize bodySize,
        Func<string, TextStyle, EsSize> measureText,
        double lineHeightFactor)
        => ResolveLineIndex(bodySize, measureText, lineHeightFactor) is { } index
            ? index.ResolveCaret(DisplayCaretIndex, measureText)
            : null;

    /// <summary>変更後の本文を書き戻す。</summary>
    /// <param name="updated">変更後の本文。</param>
    /// <returns>変わった内容。</returns>
    private TextEditChange Commit(RichText updated)
    {
        if (ReferenceEquals(updated, _text))
        {
            return TextEditChange.Selection;
        }

        Text = updated;
        return TextEditChange.Text | TextEditChange.Selection;
    }
}
