using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.StyledText.Core.Text;

namespace EsUtil.Feature.StyledText.Core.Editing;

/// <summary>本文の編集（キャレット・選択・入力・削除・書式）の状態と規則（単一所有）。</summary>
/// <remarks>
/// <b>状態の所有者は提示層</b>: 「いまどこを編集しているか（キャレット・選択）」は提示の状態であり、
/// UI 版が同じ規則で扱う。 UI は入力イベントを本クラスへ渡し、通知と描画だけを行う。<br/>
/// <b>文字の操作は Core が持つ</b>: 文字列の挿入・削除・書式の適用は <see cref="RichText"/> と
/// <see cref="TextStyleEdit"/> が単一所有する。 本クラスは「どの範囲へ・どこへキャレットを置くか」だけを決める。<br/>
/// <b>編集する対象（文書・識別子）を知らない</b>: 対象の切替は呼び出し側が <see cref="Reset"/> で伝える
/// （対象の識別子は本クラスの状態ではない）。<br/>
/// <b>本文は不変</b>であるため、編集は新しい本文を返す（変化が無い場合は同じ実体を返す）。<br/>
/// <b>選択が無いときの書式変更は以降の入力へ回す</b>（既存の文字は変えない。 入力する文字へだけ適用する）。
/// </remarks>
public sealed class TextEditState
{
    /// <summary>書式バーを表示する最小の選択文字数。</summary>
    /// <value>1（1 文字以上を選んだときだけ表示する）。</value>
    public const int FormatBarMinimumSelectionLength = 1;

    /// <summary>本文の編集中かどうか（キャレットと選択が有効か）。</summary>
    private bool _isActive;

    /// <summary>キャレットの位置（文字数）。</summary>
    private int _caret;

    /// <summary>選択の起点（キャレットと同じなら選択なし）。</summary>
    private int _anchor;

    /// <summary>以降の入力へ効かせる書式（無ければ直前の文字の書式を引き継ぐ）。</summary>
    private TextStyle? _typingStyle;

    /// <summary>編集しているかどうかを取得する。</summary>
    /// <value>編集中の場合は <see langword="true"/>。</value>
    public bool IsActive => _isActive;

    /// <summary>キャレットの位置（文字数）を取得する。</summary>
    public int CaretIndex => _caret;

    /// <summary>選択の起点（文字数）を取得する。</summary>
    public int AnchorIndex => _anchor;

    /// <summary>選択範囲の開始位置（文字数）を取得する。</summary>
    public int SelectionStart => Math.Min(_anchor, _caret);

    /// <summary>選択範囲の終了位置（文字数）を取得する。</summary>
    public int SelectionEnd => Math.Max(_anchor, _caret);

    /// <summary>選択範囲の長さ（文字数）を取得する。</summary>
    public int SelectionLength => SelectionEnd - SelectionStart;

    /// <summary>選択範囲（開始位置と長さ）を取得する。</summary>
    public (int Start, int Length) SelectionRange => (SelectionStart, SelectionLength);

    /// <summary>本文の文字を選んでいるかどうかを取得する。</summary>
    public bool HasSelection => SelectionLength > 0;

    /// <summary>書式バーを表示すべきかどうかを返す。</summary>
    /// <param name="autoShow">編集中に書式バーを自動で表示する設定かどうか（ホストが決める）。</param>
    /// <returns>表示する場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>表示の条件は 3 つ</b>である: 編集中であること・自動表示が有効であること・
    /// <see cref="FormatBarMinimumSelectionLength"/> 文字以上を選んでいること。
    /// 適用先が決まらない（選択が無い）状態でバーを出さないための規則であり、本クラスが単一所有する。
    /// </remarks>
    public bool ShouldShowFormatBar(bool autoShow)
        => autoShow && IsActive && SelectionLength >= FormatBarMinimumSelectionLength;

    /// <summary>以降の入力へ効かせる書式を取得する。</summary>
    /// <value>まだ書式を変えていない場合は <see langword="null"/>。</value>
    public TextStyle? TypingStyle => _typingStyle;

    /// <summary>本文の編集を始める（キャレットを本文の末尾へ置く）。</summary>
    /// <param name="textLength">本文の文字数。</param>
    public void Begin(int textLength)
    {
        _isActive = true;
        _caret = Math.Max(0, textLength);
        _anchor = _caret;
    }

    /// <summary>本文の編集を終える（選択の対象は保つ）。</summary>
    /// <remarks>編集を終えても選択中の対象の書式は変えられるため、対象は解除しない。</remarks>
    public void End()
    {
        _isActive = false;
        _caret = 0;
        _anchor = 0;
        _typingStyle = null;
    }

    /// <summary>編集の状態を捨てる（編集する対象が変わったとき）。</summary>
    /// <remarks>対象の識別子は本クラスが持たないため、切替の判断は呼び出し側が行う。</remarks>
    public void Reset()
    {
        _isActive = false;
        _caret = 0;
        _anchor = 0;
        _typingStyle = null;
    }

    /// <summary>キャレットを指定位置へ移す。</summary>
    /// <param name="index">移動先（文字数。 範囲外は本文の範囲へ丸める）。</param>
    /// <param name="textLength">本文の文字数。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    public void MoveTo(int index, int textLength, bool extend)
    {
        _caret = Math.Clamp(index, 0, Math.Max(0, textLength));
        if (!extend)
        {
            _anchor = _caret;
        }
    }

    /// <summary>選択範囲を指定する（ホストが入力欄の選択位置を写すときに使う）。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（文字数。 0 は選択なし）。</param>
    /// <param name="textLength">本文の文字数。</param>
    public void Select(int start, int length, int textLength)
    {
        var from = Math.Clamp(start, 0, Math.Max(0, textLength));
        var to = Math.Clamp(from + Math.Max(0, length), from, Math.Max(0, textLength));
        _anchor = from;
        _caret = to;
    }

    /// <summary>選択範囲を書記素クラスタの境界へ揃えて指定する。</summary>
    /// <param name="text">本文。</param>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（文字数。 0 は選択なし）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> が null の場合。</exception>
    /// <remarks>
    /// <b>端はクラスタの境界へ揃える</b>: サロゲート ペア・結合文字の内側を選択すると、
    /// 書式の適用や削除で文字列が壊れる。 規則は <see cref="TextCursor"/> が単一所有する。
    /// </remarks>
    public void Select(RichText text, int start, int length)
    {
        ArgumentNullException.ThrowIfNull(text);

        var plain = text.PlainText;
        var from = TextCursor.SnapBackward(plain, Math.Clamp(start, 0, plain.Length));
        var to = TextCursor.SnapForward(
            plain, Math.Clamp(from + Math.Max(0, length), from, plain.Length));
        _anchor = from;
        _caret = to;
    }

    /// <summary>本文全体を選択する。</summary>
    /// <param name="text">本文。</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> が null の場合。</exception>
    public void SelectAll(RichText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Select(text, 0, text.PlainText.Length);
    }

    /// <summary>キャレットを指定位置へ移す（書記素クラスタの境界へ揃える）。</summary>
    /// <param name="text">本文。</param>
    /// <param name="index">移動先（文字数。 範囲外は本文の範囲へ丸める）。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> が null の場合。</exception>
    /// <remarks>
    /// <b>向きで揃える側を変える</b>: 前方（キャレットより前）へは手前の境界へ、後方へは次の境界へ揃える
    /// （どちらもキャレットがクラスタを飛び越さない）。
    /// </remarks>
    public void MoveToCluster(RichText text, int index, bool extend)
    {
        ArgumentNullException.ThrowIfNull(text);

        var plain = text.PlainText;
        var target = Math.Clamp(index, 0, plain.Length);
        target = target < _caret
            ? TextCursor.SnapBackward(plain, target)
            : TextCursor.SnapForward(plain, target);
        MoveTo(target, plain.Length, extend);
    }

    /// <summary>キャレットを書記素クラスタの境界へ揃える（行の移動で境界の内側に落ちたとき）。</summary>
    /// <param name="text">本文。</param>
    /// <param name="extend">選択の起点を保つ場合は <see langword="true"/>。</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> が null の場合。</exception>
    public void SnapCaretToCluster(RichText text, bool extend)
    {
        ArgumentNullException.ThrowIfNull(text);

        var plain = text.PlainText;
        var snapped = TextCursor.SnapBackward(plain, _caret);
        if (snapped != _caret)
        {
            MoveTo(snapped, plain.Length, extend);
        }
    }

    /// <summary>選択中の文字列を取得する（選択が無い場合は空）。</summary>
    /// <param name="text">本文。</param>
    /// <returns>選択中の文字列。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> が null の場合。</exception>
    public string GetSelectedText(RichText text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var plain = text.PlainText;
        var from = Math.Clamp(SelectionStart, 0, plain.Length);
        var to = Math.Clamp(SelectionEnd, from, plain.Length);
        return plain[from..to];
    }

    /// <summary>1 クラスタ分の移動でキャレットが向かう位置を求める。</summary>
    /// <param name="direction">移動する向き（負は左・上、正は右・下）。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    /// <returns>向かう位置（本文の範囲へ丸めるのは呼び出し側）。</returns>
    /// <remarks>
    /// <b>選択を広げない移動は選択を畳む</b>: 選択があるときに矢印キーを押すと、キャレットは選択の端へ移る
    /// （文字を 1 つ飛び越さない）。 標準的な入力の作法に合わせる。
    /// </remarks>
    public int ResolveClusterTarget(int direction, bool extend)
    {
        if (direction == 0)
        {
            return _caret;
        }

        if (extend || !HasSelection)
        {
            return _caret + direction;
        }

        return direction < 0 ? SelectionStart : SelectionEnd;
    }

    /// <summary>キャレットを上下の行の同じ桁へ移す。</summary>    /// <param name="lines">本文の行（無い場合は行の移動をしない）。</param>
    /// <param name="textLength">本文の文字数。</param>
    /// <param name="delta">移動する行数。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    /// <returns>移動した場合は <see langword="true"/>。</returns>
    public bool MoveByLine(TextLineIndex? lines, int textLength, int delta, bool extend)
    {
        if (lines is null || lines.Lines.Count == 0)
        {
            return false;
        }

        var lineIndex = lines.LineIndexOf(_caret);
        var target = Math.Clamp(lineIndex + delta, 0, lines.Lines.Count - 1);
        var column = _caret - lines.LineStart(lineIndex);
        var length = TextLineIndex.LineTextLength(lines.Lines[target]);
        MoveTo(lines.LineStart(target) + Math.Min(column, length), textLength, extend);
        return true;
    }

    /// <summary>キャレットを行の先頭・末尾へ移す（行が無い場合は本文の先頭・末尾へ移す）。</summary>
    /// <param name="lines">本文の行（無い場合は行の移動をしない）。</param>
    /// <param name="textLength">本文の文字数。</param>
    /// <param name="first">行の先頭へ移す場合は <see langword="true"/>。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    public void MoveToLineEdge(TextLineIndex? lines, int textLength, bool first, bool extend)
    {
        if (lines is null || lines.Lines.Count == 0)
        {
            MoveTo(first ? 0 : textLength, textLength, extend);
            return;
        }

        var lineIndex = lines.LineIndexOf(_caret);
        var length = TextLineIndex.LineTextLength(lines.Lines[lineIndex]);
        MoveTo(first ? lines.LineStart(lineIndex) : lines.LineStart(lineIndex) + length, textLength, extend);
    }

    /// <summary>位置（元画像ピクセル）に最も近い文字位置へキャレットを移す。</summary>
    /// <param name="lines">本文の行。</param>
    /// <param name="point">位置（元画像ピクセル）。</param>
    /// <param name="textLength">本文の文字数。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    /// <param name="measureText">文字列と書式から実寸を測る関数。</param>
    public void SetCaretFromPoint(
        TextLineIndex lines,
        EsPoint point,
        int textLength,
        bool extend,
        Func<string, TextStyle, EsSize> measureText)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(measureText);

        MoveTo(lines.IndexAt(point, measureText), textLength, extend);
    }

    /// <summary>キャレットと選択の起点を本文の範囲へ収める（履歴から戻したときなど）。</summary>
    /// <param name="textLength">本文の文字数。</param>
    public void Clamp(int textLength)
    {
        var length = Math.Max(0, textLength);
        _caret = Math.Clamp(_caret, 0, length);
        _anchor = Math.Clamp(_anchor, 0, length);
    }

    /// <summary>以降の入力へ効かせる書式を設定する。</summary>
    /// <param name="style">書式。</param>
    /// <exception cref="ArgumentNullException"><paramref name="style"/> が <see langword="null"/> の場合。</exception>
    public void SetTypingStyle(TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        _typingStyle = style;
    }

    /// <summary>選択範囲を文字列で置き換えた本文を返す（選択が無ければ挿入）。</summary>
    /// <param name="text">本文。</param>
    /// <param name="inserted">挿入する文字列（空なら選択・1 文字の削除）。</param>
    /// <returns>置き換えた本文（変化が無い場合は同じ実体）。 キャレットは挿入した文字列の末尾へ移る。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> または <paramref name="inserted"/> が null の場合。</exception>
    public RichText ReplaceSelection(RichText text, string inserted)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(inserted);

        var start = SelectionStart;
        var length = SelectionLength;
        if (length == 0 && inserted.Length == 0)
        {
            return text;
        }

        var replaced = TextStyleEdit.Insert(text, start, length, inserted, _typingStyle);
        if (ReferenceEquals(replaced, text))
        {
            return text;
        }

        _caret = start + inserted.Length;
        _anchor = _caret;
        return replaced;
    }

    /// <summary>指定した範囲を文字列で置き換えた本文を返す（ホストが範囲を指定する場合）。</summary>
    /// <param name="text">本文。</param>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="end">終了位置（文字数）。</param>
    /// <param name="inserted">置き換える文字列（空なら削除）。</param>
    /// <returns>置き換えた本文（変化が無い場合は同じ実体）。 キャレットは挿入した文字列の末尾へ移る。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> または <paramref name="inserted"/> が null の場合。</exception>
    public RichText ReplaceRange(RichText text, int start, int end, string inserted)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(inserted);

        var total = text.PlainText.Length;
        var from = Math.Clamp(start, 0, total);
        var to = Math.Clamp(end, from, total);
        var replaced = TextStyleEdit.Insert(text, from, to - from, inserted, _typingStyle);
        if (ReferenceEquals(replaced, text))
        {
            return text;
        }

        _caret = from + inserted.Length;
        _anchor = _caret;
        return replaced;
    }

    /// <summary>選択範囲、またはキャレットの前後の 1 クラスタを削除した本文を返す。</summary>
    /// <param name="text">本文。</param>
    /// <param name="forward">キャレットの次の文字を削除する場合は <see langword="true"/>。</param>
    /// <returns>削除した本文（変化が無い場合は同じ実体）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> が null の場合。</exception>
    public RichText Delete(RichText text, bool forward)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (HasSelection)
        {
            return ReplaceSelection(text, string.Empty);
        }

        // **削除の単位は書記素クラスタ**である: サロゲート ペア・結合文字を半分に割ると文字列が壊れる。
        var (start, length) = TextCursor.ResolveDelete(text.PlainText, _caret, forward);
        if (length <= 0)
        {
            return text;
        }

        _anchor = start;
        _caret = start + length;
        return ReplaceSelection(text, string.Empty);
    }

    /// <summary>選択範囲へ書式を丸ごと適用した本文を返す（選択が無ければ以降の入力へ回す）。</summary>
    /// <param name="text">本文。</param>
    /// <param name="style">適用する書式。</param>
    /// <returns>適用した本文（選択が無い・同じ値の場合は同じ実体）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> または <paramref name="style"/> が null の場合。</exception>
    public RichText ApplyStyle(RichText text, TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(style);

        SetTypingStyle(style);
        return TextStyleEdit.ResolveRange(SelectionStart, SelectionLength) is { } selection
            ? text.ApplyStyle(selection.Start, selection.Length, style)
            : text;
    }

    /// <summary>選択範囲の書式を 1 属性だけ変換した本文を返す（変えない属性を保つ）。</summary>
    /// <param name="text">本文。</param>
    /// <param name="transform">現在の書式を受け取って新しい書式を返す関数（範囲には 1 度だけ適用する）。</param>
    /// <returns>変換した本文（選択が無い・同じ値の場合は同じ実体）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> または <paramref name="transform"/> が null の場合。</exception>
    /// <remarks>
    /// <b>切り替えの判断は 1 回だけ行う</b>: 範囲の先頭の書式で「太字にする／戻す」を決め、その値を範囲の全 run へ
    /// 書く（run ごとに反転すると、範囲の中で太字と通常が入れ替わる）。
    /// </remarks>
    public RichText TransformStyle(RichText text, Func<TextStyle, TextStyle> transform)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(transform);

        var next = transform(ResolveDisplayStyle(text));
        SetTypingStyle(next);
        return HasSelection ? text.TransformStyle(SelectionStart, SelectionLength, _ => next) : text;
    }

    /// <summary>選択範囲の文字がすべて条件を満たすかどうかを返す（書式ボタンの押下状態に使う）。</summary>
    /// <param name="text">本文。</param>
    /// <param name="match">書式が条件を満たすかどうかを返す関数。</param>
    /// <returns>すべて満たす場合は <see langword="true"/>。 選択が無い場合は表示中の書式で判定する。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> または <paramref name="match"/> が null の場合。</exception>
    public bool AllMatch(RichText text, Func<TextStyle, bool> match)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(match);

        return HasSelection
            ? text.AllMatch(SelectionStart, SelectionLength, match)
            : match(ResolveDisplayStyle(text));
    }

    /// <summary>書式バーへ表示する書式を返す（選択が無い間は覚えた書式）。</summary>
    /// <param name="text">本文。</param>
    /// <returns>表示する書式。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> が null の場合。</exception>
    /// <remarks>
    /// <b>選んだ書式が表示に残る</b>: 選択が無いときに書式を変えると本文は変わらないため、そのまま
    /// キャレットの位置の書式を表示すると「操作が効いていない」ように見える。
    /// </remarks>
    public TextStyle ResolveDisplayStyle(RichText text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var resolved = text.StyleAt(Math.Clamp(SelectionStart, 0, text.PlainText.Length));
        return !HasSelection && _typingStyle is { } pending ? pending : resolved;
    }
}
