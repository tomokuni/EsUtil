using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.Annotation.Core.Text;
using EsUtil.Feature.Annotation.Presenter.Drawing;

namespace EsUtil.Feature.Annotation.Presenter.Selection;

/// <summary>本文の編集（キャレット・選択・入力・削除・書式）の状態と規則（単一所有）。</summary>
/// <remarks>
/// <b>状態の所有者は提示層</b>: 「いまどこを編集しているか（キャレット・選択）」は提示の状態であり、
/// 4 つの UI 版が同じ規則で扱う。 UI は入力イベントを本クラスへ渡し、通知と描画だけを行う。<br/>
/// <b>文字の操作は Core が持つ</b>: 文字列の挿入・削除・書式の適用は <see cref="RichText"/> と
/// <see cref="AnnotationTextStyleEdit"/> が単一所有する。 本クラスは「どの範囲へ・どこへキャレットを置くか」だけを決める。<br/>
/// <b>文書は不変</b>であるため、編集は新しい文書を返す（変化が無い場合は同じ実体を返す）。<br/>
/// <b>選択が無いときの書式変更は以降の入力へ回す</b>（既存の文字は変えない。 入力する文字へだけ適用する）。
/// </remarks>
public sealed class AnnotationTextEdit
{
    /// <summary>本文の編集中かどうか（キャレットと選択が有効か）。</summary>
    private bool _isActive;

    /// <summary>書式の適用先（選択中の吹き出し。 選択していない場合は <see langword="null"/>）。</summary>
    private string? _bubbleId;

    /// <summary>キャレットの位置（文字数）。</summary>
    private int _caret;

    /// <summary>選択の起点（キャレットと同じなら選択なし）。</summary>
    private int _anchor;

    /// <summary>以降の入力へ効かせる書式（無ければ直前の文字の書式を引き継ぐ）。</summary>
    private AnnotationTextStyle? _typingStyle;

    /// <summary>編集しているかどうかを取得する。</summary>
    /// <value>編集中の場合は <see langword="true"/>。</value>
    public bool IsActive => _isActive;

    /// <summary>編集している吹き出しの識別子を取得する。</summary>
    /// <value>編集していない場合は <see langword="null"/>。</value>
    public string? BubbleId => _bubbleId;

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

    /// <summary>以降の入力へ効かせる書式を取得する。</summary>
    /// <value>まだ書式バーを操作していない場合は <see langword="null"/>。</value>
    public AnnotationTextStyle? TypingStyle => _typingStyle;

    /// <summary>本文の編集を始める（キャレットを本文の末尾へ置く）。</summary>
    /// <param name="bubbleId">編集する吹き出しの識別子。</param>
    /// <param name="textLength">本文の文字数。</param>
    /// <exception cref="ArgumentException"><paramref name="bubbleId"/> が空白の場合。</exception>
    public void Begin(string bubbleId, int textLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bubbleId);

        Bind(bubbleId);
        _isActive = true;
        _caret = Math.Max(0, textLength);
        _anchor = _caret;
    }

    /// <summary>本文の編集を終える（選択の対象は保つ）。</summary>
    /// <remarks>編集を終えても選択中の吹き出しの書式は変えられるため、対象は解除しない。</remarks>
    public void End()
    {
        _isActive = false;
        _caret = 0;
        _anchor = 0;
        _typingStyle = null;
    }

    /// <summary>書式・本文の適用先を設定する（UI の選択の変更に追随する）。</summary>
    /// <param name="bubbleId">選択中の吹き出しの識別子（選択を解除する場合は <see langword="null"/>）。</param>
    /// <remarks>編集を始めていなくても、選択中の吹き出しへ書式を適用できるようにする。</remarks>
    public void Bind(string? bubbleId)
    {
        if (string.Equals(_bubbleId, bubbleId, StringComparison.Ordinal))
        {
            return;
        }

        _bubbleId = bubbleId;
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

    /// <summary>キャレットを上下の行の同じ桁へ移す。</summary>
    /// <param name="lines">本文の行（無い場合は行の移動をしない）。</param>
    /// <param name="textLength">本文の文字数。</param>
    /// <param name="delta">移動する行数。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    /// <returns>移動した場合は <see langword="true"/>。</returns>
    public bool MoveByLine(AnnotationTextLineIndex? lines, int textLength, int delta, bool extend)
    {
        if (lines is null || lines.Lines.Count == 0)
        {
            return false;
        }

        var lineIndex = lines.LineIndexOf(_caret);
        var target = Math.Clamp(lineIndex + delta, 0, lines.Lines.Count - 1);
        var column = _caret - lines.LineStart(lineIndex);
        var length = AnnotationTextLineIndex.LineTextLength(lines.Lines[target]);
        MoveTo(lines.LineStart(target) + Math.Min(column, length), textLength, extend);
        return true;
    }

    /// <summary>キャレットを行の先頭・末尾へ移す（行が無い場合は本文の先頭・末尾へ移す）。</summary>
    /// <param name="lines">本文の行（無い場合は行の移動をしない）。</param>
    /// <param name="textLength">本文の文字数。</param>
    /// <param name="first">行の先頭へ移す場合は <see langword="true"/>。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    public void MoveToLineEdge(AnnotationTextLineIndex? lines, int textLength, bool first, bool extend)
    {
        if (lines is null || lines.Lines.Count == 0)
        {
            MoveTo(first ? 0 : textLength, textLength, extend);
            return;
        }

        var lineIndex = lines.LineIndexOf(_caret);
        var length = AnnotationTextLineIndex.LineTextLength(lines.Lines[lineIndex]);
        MoveTo(first ? lines.LineStart(lineIndex) : lines.LineStart(lineIndex) + length, textLength, extend);
    }

    /// <summary>位置（元画像ピクセル）に最も近い文字位置へキャレットを移す。</summary>
    /// <param name="lines">本文の行。</param>
    /// <param name="point">位置（元画像ピクセル）。</param>
    /// <param name="textLength">本文の文字数。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    /// <param name="measureText">文字列と書式から実寸を測る関数。</param>
    public void SetCaretFromPoint(
        AnnotationTextLineIndex lines,
        EsPoint point,
        int textLength,
        bool extend,
        Func<string, AnnotationTextStyle, EsSize> measureText)
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

    /// <summary>以降の入力へ効かせる書式を覚える。</summary>
    /// <param name="style">書式。</param>
    /// <exception cref="ArgumentNullException"><paramref name="style"/> が <see langword="null"/> の場合。</exception>
    public void Remember(AnnotationTextStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        _typingStyle = style;
    }

    /// <summary>選択範囲を文字列で置き換えた文書を返す（選択が無ければ挿入）。</summary>
    /// <param name="document">文書。</param>
    /// <param name="text">挿入する文字列（空なら選択・1 文字の削除）。</param>
    /// <returns>置き換えた文書（変化が無い場合は同じ実体）。 キャレットは挿入した文字列の末尾へ移る。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> または <paramref name="text"/> が null の場合。</exception>
    public AnnotationDocument ReplaceSelection(AnnotationDocument document, string text)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(text);

        if (FindText(document) is not { } bubble)
        {
            return document;
        }

        var start = SelectionStart;
        var length = SelectionLength;
        if (length == 0 && text.Length == 0)
        {
            return document;
        }

        var replaced = document.WithText(
            bubble.Id, AnnotationTextStyleEdit.Insert(bubble.Text, start, length, text, _typingStyle));
        if (ReferenceEquals(replaced, document))
        {
            return document;
        }

        _caret = start + text.Length;
        _anchor = _caret;
        return replaced;
    }

    /// <summary>指定した範囲を文字列で置き換えた文書を返す（ホストが範囲を指定する場合）。</summary>
    /// <param name="document">文書。</param>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="end">終了位置（文字数）。</param>
    /// <param name="text">置き換える文字列（空なら削除）。</param>
    /// <returns>置き換えた文書（変化が無い場合は同じ実体）。 キャレットは挿入した文字列の末尾へ移る。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> または <paramref name="text"/> が null の場合。</exception>
    public AnnotationDocument ReplaceRange(AnnotationDocument document, int start, int end, string text)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(text);

        if (FindText(document) is not { } bubble)
        {
            return document;
        }

        var from = Math.Clamp(start, 0, bubble.Text.PlainText.Length);
        var to = Math.Clamp(end, from, bubble.Text.PlainText.Length);
        var replaced = document.WithText(
            bubble.Id, AnnotationTextStyleEdit.Insert(bubble.Text, from, to - from, text, _typingStyle));
        if (ReferenceEquals(replaced, document))
        {
            return document;
        }

        _caret = from + text.Length;
        _anchor = _caret;
        return replaced;
    }

    /// <summary>選択範囲、またはキャレットの前後の 1 文字を削除した文書を返す。</summary>
    /// <param name="document">文書。</param>
    /// <param name="forward">キャレットの次の文字を削除する場合は <see langword="true"/>。</param>
    /// <returns>削除した文書（変化が無い場合は同じ実体）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> が null の場合。</exception>
    public AnnotationDocument Delete(AnnotationDocument document, bool forward)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (HasSelection)
        {
            return ReplaceSelection(document, string.Empty);
        }

        if (FindText(document) is not { } bubble)
        {
            return document;
        }

        // **削除の単位は書記素クラスタ**である: サロゲート ペア・結合文字を半分に割ると文字列が壊れる。
        var (start, length) = AnnotationTextCursor.ResolveDelete(bubble.Text.PlainText, _caret, forward);
        if (length <= 0)
        {
            return document;
        }

        _anchor = start;
        _caret = start + length;
        return ReplaceSelection(document, string.Empty);
    }

    /// <summary>選択範囲へ書式を丸ごと適用した文書を返す（選択が無ければ以降の入力へ回す）。</summary>
    /// <param name="document">文書。</param>
    /// <param name="style">適用する書式。</param>
    /// <returns>適用した文書（選択が無い・同じ値の場合は同じ実体）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> または <paramref name="style"/> が null の場合。</exception>
    public AnnotationDocument ApplyStyle(AnnotationDocument document, AnnotationTextStyle style)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(style);

        Remember(style);
        return AnnotationSelectionPresenter.ApplyTextStyle(document, _bubbleId, SelectionRange, style) ?? document;
    }

    /// <summary>選択範囲の書式を 1 属性だけ変換した文書を返す（変えない属性を保つ）。</summary>
    /// <param name="document">文書。</param>
    /// <param name="transform">現在の書式を受け取って新しい書式を返す関数（範囲には 1 度だけ適用する）。</param>
    /// <returns>変換した文書（選択が無い・同じ値の場合は同じ実体）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> または <paramref name="transform"/> が null の場合。</exception>
    /// <remarks>
    /// <b>切り替えの判断は 1 回だけ行う</b>: 範囲の先頭の書式で「太字にする／戻す」を決め、その値を範囲の全 run へ
    /// 書く（run ごとに反転すると、範囲の中で太字と通常が入れ替わる）。
    /// </remarks>
    public AnnotationDocument TransformStyle(
        AnnotationDocument document, Func<AnnotationTextStyle, AnnotationTextStyle> transform)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(transform);

        var next = transform(ResolveDisplayStyle(document));
        Remember(next);
        if (FindText(document) is not { } bubble || !HasSelection)
        {
            return document;
        }

        var start = SelectionStart;
        var length = SelectionLength;
        return document.WithText(bubble.Id, bubble.Text.TransformStyle(start, length, _ => next));
    }

    /// <summary>指定した範囲の書式を 1 属性だけ変換した文書を返す（ホストが範囲を指定する場合）。</summary>
    /// <param name="document">文書。</param>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（文字数。 0 以下は本文全体）。</param>
    /// <param name="transform">現在の書式を受け取って新しい書式を返す関数。</param>
    /// <returns>変換した文書（変化が無い場合は同じ実体）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> または <paramref name="transform"/> が null の場合。</exception>
    public AnnotationDocument TransformStyleRange(
        AnnotationDocument document, int start, int length, Func<AnnotationTextStyle, AnnotationTextStyle> transform)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(transform);

        return FindText(document) is { } bubble
            ? document.WithText(bubble.Id, bubble.Text.TransformStyle(start, length, transform))
            : document;
    }

    /// <summary>選択範囲の文字がすべて条件を満たすかどうかを返す（書式ボタンの押下状態に使う）。</summary>
    /// <param name="document">文書。</param>
    /// <param name="match">書式が条件を満たすかどうかを返す関数。</param>
    /// <returns>すべて満たす場合は <see langword="true"/>。 選択が無い場合は表示中の書式で判定する。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> または <paramref name="match"/> が null の場合。</exception>
    public bool AllMatch(AnnotationDocument document, Func<AnnotationTextStyle, bool> match)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(match);

        return FindText(document) is not { } bubble
            ? false
            : HasSelection
                ? bubble.Text.AllMatch(SelectionStart, SelectionLength, match)
                : match(ResolveDisplayStyle(document));
    }

    /// <summary>書式バーへ表示する書式を返す（選択が無い間は覚えた書式）。</summary>
    /// <param name="document">文書。</param>
    /// <returns>表示する書式。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> が null の場合。</exception>
    /// <remarks>
    /// <b>選んだ書式が表示に残る</b>: 選択が無いときに書式を変えると本文は変わらないため、そのまま
    /// キャレットの位置の書式を表示すると「操作が効いていない」ように見える。
    /// </remarks>
    public AnnotationTextStyle ResolveDisplayStyle(AnnotationDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var resolved = AnnotationSelectionPresenter.ResolveTextStyle(document, _bubbleId, SelectionRange);
        return !HasSelection && _typingStyle is { } pending ? pending : resolved;
    }

    /// <summary>編集している吹き出しの本文を取得する。</summary>
    /// <param name="document">文書。</param>
    /// <returns>吹き出し。 編集していない・吹き出しが無い場合は <see langword="null"/>。</returns>
    private BubbleAnnotation? FindText(AnnotationDocument document)
        => _bubbleId is null ? null : document.Find(_bubbleId) as BubbleAnnotation;
}
