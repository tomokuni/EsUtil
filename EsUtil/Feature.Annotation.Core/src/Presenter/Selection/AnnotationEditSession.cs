using EsUtil.Feature.Annotation.Core.Geometry;
using EsUtil.Feature.Annotation.Core.History;
using EsUtil.Feature.Annotation.Logging;
using EsUtil.Platform.Core.Common;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using EsUtil.Feature.StyledText;
using EsUtil.Feature.StyledText.Core.Text;
using EsUtil.Feature.StyledText.Core.Editing;

namespace EsUtil.Feature.Annotation.Presenter.Selection;

/// <summary>注釈の編集セッション（文書・選択・変更履歴・本文の編集状態を単一所有する）。</summary>
/// <remarks>
/// <b>役割</b>: 4 つの UI 版が同じ順序で文書を変更し、同じ通知を出すための状態と操作を持つ。 UI は入力の受付と描画だけを行う。<br/>
/// <b>状態の所有者は提示層</b>: 「何を選んでいるか」「どこを編集しているか」「戻せるか」は提示の状態であり、Core の計算ではない。
/// 文書の値の規則（画像の内側へ収める・安全域へ補正する）は Core が単一所有し、本クラスはその入口を呼ぶ。<br/>
/// <b>本文の操作は書式付き文字列の編集層へ委譲する</b>: キャレット・選択・入力・書式の適用は <see cref="TextEditor"/> が単一所有し、
/// 本クラスは編集対象の本文を選択中の注釈へ揃え、結果を文書へ書き戻す（操作の規則を 2 か所に持たない）。<br/>
/// <b>1 操作 = 1 手</b>: 変更の直前に履歴へ控え、変更後の文書を積む（<see cref="AnnotationHistory"/>）。
/// ドラッグのように何度も書き換える操作は <see cref="BeginChange"/> から <see cref="SetBounds"/> の繰り返しで 1 手にまとまる。<br/>
/// <b>通知は購読で受ける</b>: 選択・編集・文書は値の通知であるため <c>event</c> を使わず、<see cref="Subscribe"/> が返す
/// <see cref="IDisposable"/> で解除する（UI は自分のイベントへ転送する）。<br/>
/// <b>ログ</b>: 選択・本文の編集・書式の適用・履歴の移動を記録する。 毎フレームの描画計画の解決は記録しない。
/// </remarks>
public sealed class AnnotationEditSession
{
    /// <summary>変更履歴（元に戻す・やり直す）。</summary>
    private readonly AnnotationHistory _history;

    /// <summary>選択中の本文の値と編集状態（キャレット・選択・入力・書式）。</summary>
    private readonly TextEditor _editor = new();

    /// <summary>変更の購読（通知する側と購読する側を分ける）。</summary>
    private readonly CallbackList<AnnotationEditChange> _notices = new();

    /// <summary>ログの出力先を返す関数（未設定は出力しない。 ホストのロガーを設定のたびに読む）。</summary>
    private readonly Func<ILogger>? _logger;

    /// <summary>注釈の文書。</summary>
    private AnnotationDocument _document;

    /// <summary>選択中の注釈の識別子。</summary>
    private string? _selectedId;

    /// <summary>直近に通知した本文の選択範囲（同じ範囲で通知を繰り返さない）。</summary>
    private (int Start, int Length) _lastTextSelection;

    /// <summary>編集セッションを初期化する。</summary>
    /// <param name="document">最初の文書。</param>
    /// <param name="logger">ログの出力先を返す関数（省略すると出力しない）。 ホストのロガーを設定のたびに読む。</param>
    /// <param name="historyCapacity">変更履歴の保持数（1 未満は 1）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> が <see langword="null"/> の場合。</exception>
    public AnnotationEditSession(AnnotationDocument document, Func<ILogger>? logger = null, int historyCapacity = AnnotationHistory.DefaultCapacity)
    {
        ArgumentNullException.ThrowIfNull(document);

        _document = document;
        _logger = logger;
        _history = new AnnotationHistory(historyCapacity);
    }

    /// <summary>文書を取得または設定する。</summary>
    /// <value>表示・編集する文書。 差し替えると選択と変更履歴を解除する。</value>
    /// <remarks><b>別の画像の注釈を編集しない</b>: 差し替えは「別の画像を開いた」ことを意味するため、選択・編集・履歴を捨てる。</remarks>
    /// <exception cref="ArgumentNullException">設定する値が <see langword="null"/> の場合。</exception>
    public AnnotationDocument Document
    {
        get => _document;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            EndTextEditing();
            _document = value;
            _history.Clear();
            _selectedId = null;
            ResetEditor();
            _lastTextSelection = default;
            Notify(AnnotationEditChange.Document | AnnotationEditChange.Selection | AnnotationEditChange.TextSelection);
        }
    }

    /// <summary>選択中の注釈の識別子を取得または設定する。</summary>
    /// <value>選択中の注釈の識別子。 解除は <see langword="null"/>。</value>
    /// <remarks>設定すると本文の編集を終え、書式の適用先を新しい注釈へ切り替える。</remarks>
    public string? SelectedId
    {
        get => _selectedId;
        set => Select(value);
    }

    /// <summary>選択中の注釈を取得する。</summary>
    /// <value>選択が無い場合は <see langword="null"/>。</value>
    public AnnotationItem? Selected => _selectedId is null ? null : _document.Find(_selectedId);

    /// <summary>新規作成時の既定値を取得または設定する。</summary>
    /// <value>枠線・吹き出し・本文の既定。 既定は <see cref="AnnotationDraftDefaults.Default"/>。</value>
    /// <remarks><b>既存の注釈には影響しない</b>（各注釈が自分の外観・書式を持つ）。</remarks>
    public AnnotationDraftDefaults DraftDefaults { get; set; } = AnnotationDraftDefaults.Default;

    /// <summary>元に戻せるかどうかを取得する。</summary>
    public bool CanUndo => _history.CanUndo;

    /// <summary>やり直せるかどうかを取得する。</summary>
    public bool CanRedo => _history.CanRedo;

    /// <summary>本文を編集中かどうかを取得する。</summary>
    public bool IsEditingText => _editor.IsEditing;

    /// <summary>キャレットの位置（文字数）を取得する。</summary>
    public int CaretIndex => _editor.CaretIndex;

    /// <summary>本文の選択範囲の開始位置（文字数）を取得する。</summary>
    public int SelectionStart => _editor.SelectionStart;

    /// <summary>本文の選択範囲の終了位置（文字数）を取得する。</summary>
    public int SelectionEnd => _editor.SelectionEnd;

    /// <summary>本文の選択範囲の長さ（文字数）を取得する。</summary>
    public int SelectionLength => _editor.SelectionLength;

    /// <summary>本文の文字を選んでいるかどうかを取得する。</summary>
    public bool HasTextSelection => _editor.HasSelection;

    /// <summary>選択中の吹き出しの本文（書式を除く）を取得する。</summary>
    /// <value>本文。 吹き出しを選んでいない場合は空。</value>
    public string SelectedPlainText => (_document.Find(_selectedId) as BubbleAnnotation)?.Text.PlainText ?? string.Empty;

    /// <summary>本文の値と編集の状態を取得する（UI の本文編集コントロールと共有する）。</summary>
    /// <value>編集している本文と、キャレット・選択・入力・書式の状態。 所有者は本セッションであり、UI は同じ実体を描画に使う。</value>
    /// <remarks>
    /// <b>編集器を 2 つ持たない</b>: UI の編集コントロールが自前の編集器を持つと、本文・キャレット・選択が
    /// 文書と食い違う。 UI は本プロパティの実体を渡し（<c>StyledTextControl.Editor</c>）、描画と入力の入口としてだけ使う。
    /// </remarks>
    public TextEditor Editor => _editor;

    /// <summary>編集コントロールが書き換えた本文を文書へ書き戻す（1 手として履歴へ積む）。</summary>
    /// <returns>文書が変わった場合は <see langword="true"/>。</returns>
    /// <remarks>本文の操作は編集コントロールが <see cref="Editor"/> へ行うため、本メソッドはその結果を文書へ移すだけである。</remarks>
    public bool CommitTextEdit() => ReplaceEdit();

    /// <summary>書式の適用先の範囲（文字数）を取得する。</summary>
    /// <value>開始位置と長さ（0 起点）。 長さ 0 は選択なし。</value>
    public (int Start, int Length) TextSelectionRange
        => (_editor.SelectionStart, _editor.SelectionLength);

    /// <summary>本文の選択範囲を指定する（ホストが入力欄の選択位置を写すときに使う）。</summary>
    /// <param name="start">開始位置（文字数。 範囲外は本文の範囲へ丸める）。</param>
    /// <param name="length">長さ（文字数。 0 は選択なし）。</param>
    /// <remarks>選択の変更は通知する（書式バーの表示条件が変わるため）。</remarks>
    public void SelectText(int start, int length)
    {
        _editor.Select(start, length);
        NotifyTextSelectionChanged();
    }

    /// <summary>書式バーへ表示する書式を取得する。</summary>
    /// <value>選択範囲の先頭の書式。 選択が無い間は最後に選んだ書式。</value>
    public TextStyle SelectedTextStyle => _editor.SelectedTextStyle;

    /// <summary>変更の購読を追加する。</summary>
    /// <param name="notify">変更を受け取る処理。 null 不可。</param>
    /// <returns>購読を解除する <see cref="IDisposable"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="notify"/> が <see langword="null"/> の場合。</exception>
    public IDisposable Subscribe(Action<AnnotationEditChange> notify) => _notices.Add(notify);

    /// <summary>注釈を選択する（解除は <see langword="null"/>）。</summary>
    /// <param name="id">選択する注釈の識別子。</param>
    /// <remarks>
    /// <b>別の注釈を選んだら本文の編集を終える</b>: 編集していた本文を見失わないようにする。
    /// 同じ注釈を選び直した場合は何もしない（選択のたびに編集状態を捨てない）。
    /// </remarks>
    public void Select(string? id)
    {
        if (string.Equals(_selectedId, id, StringComparison.Ordinal))
        {
            return;
        }

        EndTextEditing();
        _selectedId = id;
        ResetEditor();
        _lastTextSelection = default;
        (_logger?.Invoke() ?? NullLogger.Instance).LogSelectionChanged(ResolveKind(id), id);
        Notify(AnnotationEditChange.Selection | AnnotationEditChange.TextSelection);
    }

    /// <summary>注釈の種別を求める（選択の記録に使う。 見つからない場合は <see langword="null"/>）。</summary>
    /// <param name="id">注釈の識別子（無い場合は <see langword="null"/>）。</param>
    /// <returns>注釈の種別。 識別子が無い・見つからない場合は <see langword="null"/>。</returns>
    private AnnotationKind? ResolveKind(string? id)
        => _document.Find(id) switch
        {
            FrameAnnotation => AnnotationKind.Frame,
            BubbleAnnotation => AnnotationKind.Bubble,
            _ => null,
        };

    /// <summary>枠線を追加する。</summary>
    /// <param name="bounds">矩形（元画像ピクセル）。</param>
    /// <returns>追加した注釈の識別子。</returns>
    /// <remarks>矩形は Core（<see cref="AnnotationGeometry.CreateFrame"/>）が画像の内側へ収める。</remarks>
    public string AddFrame(EsRect bounds)
    {
        var frame = AnnotationGeometry.CreateFrame(bounds, DraftDefaults.FrameAppearance, ImageSize);
        (_logger?.Invoke() ?? NullLogger.Instance).LogAnnotationCreated(AnnotationKind.Frame, frame.Id, frame.Bounds);
        ApplyChange(document => document.Add(frame));
        Select(frame.Id);
        return frame.Id;
    }

    /// <summary>吹き出しを追加する。</summary>
    /// <param name="bounds">本体の矩形（元画像ピクセル）。</param>
    /// <param name="text">本文（改行は <c>\n</c> へ揃える）。</param>
    /// <returns>追加した注釈の識別子。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> が <see langword="null"/> の場合。</exception>
    /// <remarks>矩形もしっぽの先端も Core（<see cref="AnnotationGeometry.CreateBubble"/>）が画像の内側へ収める。</remarks>
    public string AddBubble(EsRect bounds, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var defaults = DraftDefaults;
        var bubble = AnnotationGeometry.CreateBubble(
            bounds, RichText.Create(TextInput.Normalize(text), defaults.TextStyle), defaults, ImageSize);
        (_logger?.Invoke() ?? NullLogger.Instance).LogAnnotationCreated(AnnotationKind.Bubble, bubble.Id, bubble.Bounds);
        ApplyChange(document => document.Add(bubble));
        Select(bubble.Id);
        return bubble.Id;
    }

    /// <summary>選択中の注釈を削除する。</summary>
    /// <returns>削除した場合は <see langword="true"/>。</returns>
    public bool DeleteSelected()
    {
        if (_selectedId is not { } id)
        {
            return false;
        }

        var kind = _document.Find(id) is FrameAnnotation ? AnnotationKind.Frame : AnnotationKind.Bubble;
        EndTextEditing();
        if (!ApplyChange(document => document.Remove(id)))
        {
            return false;
        }

        (_logger?.Invoke() ?? NullLogger.Instance).LogAnnotationRemoved(kind, id);
        Select(null);
        return true;
    }

    /// <summary>選択中の吹き出しの本文を置き換える。</summary>
    /// <param name="text">新しい本文（改行は <c>\n</c> へ揃える）。</param>
    /// <returns>置き換えた場合は <see langword="true"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> が <see langword="null"/> の場合。</exception>
    /// <remarks><b>書式は文字位置で引き継ぐ</b>（<see cref="RichText.WithPlainText"/>）。 同じ文字列なら何もしない。</remarks>
    public bool SetSelectedText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (_document.Find(_selectedId) is not BubbleAnnotation bubble)
        {
            return false;
        }

        if (!ApplyChange(document => document.WithText(bubble.Id, bubble.Text.WithPlainText(TextInput.Normalize(text)))))
        {
            return false;
        }

        // 差し替えた本文の範囲へキャレットを収める（書記素クラスタの境界へ揃える規則は編集器が持つ）
        _ = _editor.ClampCaret();

        // **操作の内容と値を残す**（差し替え後の本文の先頭。 本文は大きくなりうるため整形はログ定義が持つ）
        var updated = SelectedPlainText;
        (_logger?.Invoke() ?? NullLogger.Instance).LogTextChanged(updated.Length, updated);

        NotifyTextSelectionChanged();
        return true;
    }

    /// <summary>選択範囲の書式を丸ごと適用する（選択が無ければ以降の入力へ回す）。</summary>
    /// <param name="style">適用する書式。</param>
    /// <returns>既存の文字へ適用した場合は <see langword="true"/>。 以降の入力へ回した場合は <see langword="false"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="style"/> が <see langword="null"/> の場合。</exception>
    public bool ApplyTextStyle(TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);

        // 本文の操作は書式付き文字列の編集層が担い、文書への書き戻しだけを本クラスが行う
        var range = TextSelectionRange;
        _editor.ApplyStyle(style);
        return ReplaceEdit(range);
    }

    /// <summary>指定した範囲の書式を丸ごと適用する（ホストが範囲を指定する場合）。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（文字数。 0 以下は本文全体）。</param>
    /// <param name="style">適用する書式。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="style"/> が <see langword="null"/> の場合。</exception>
    public bool ApplyTextStyle(int start, int length, TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);

        _editor.ApplyStyle(start, length, style);
        return ReplaceEdit((start, length));
    }

    /// <summary>選択範囲の書式を 1 属性だけ変換する（選択が無ければ以降の入力へ回す）。</summary>
    /// <param name="transform">現在の書式を受け取って新しい書式を返す関数（範囲には 1 度だけ適用する）。</param>
    /// <returns>既存の文字へ適用した場合は <see langword="true"/>。 以降の入力へ回した場合は <see langword="false"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="transform"/> が <see langword="null"/> の場合。</exception>
    public bool TransformTextStyle(Func<TextStyle, TextStyle> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        var range = TextSelectionRange;
        _editor.TransformStyle(transform);
        return ReplaceEdit(range);
    }

    /// <summary>指定した範囲の書式を 1 属性だけ変換する（ホストが範囲を指定する場合）。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（文字数。 0 以下は本文全体）。</param>
    /// <param name="transform">現在の書式を受け取って新しい書式を返す関数。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="transform"/> が <see langword="null"/> の場合。</exception>
    public bool TransformTextStyle(int start, int length, Func<TextStyle, TextStyle> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        _editor.TransformStyle(start, length, transform);
        return ReplaceEdit((start, length));
    }

    /// <summary>選択中の注釈の外観を差し替える。</summary>
    /// <param name="transform">現在の外観を受け取って新しい外観を返す関数。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="transform"/> が <see langword="null"/> の場合。</exception>
    public bool UpdateSelectedAppearance(Func<AnnotationAppearance, AnnotationAppearance> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        if (Selected is not { } item
            || !ApplyChange(document => AnnotationSelectionPresenter.ApplyAppearance(
                document, item.Id, transform(item.Appearance))))
        {
            return false;
        }

        if (_document.Find(item.Id) is { } updated)
        {
            (_logger?.Invoke() ?? NullLogger.Instance).LogAnnotationAppearanceChanged(updated.Appearance);
        }

        return true;
    }

    /// <summary>選択中の吹き出しの本文の余白を差し替える。</summary>
    /// <param name="padding">本文の余白（元画像ピクセル）。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    public bool SetSelectedBodyPadding(double padding)
    {
        if (_document.Find(_selectedId) is not BubbleAnnotation bubble
            || !ApplyChange(document => AnnotationSelectionPresenter.SetBodyPadding(document, bubble.Id, padding)))
        {
            return false;
        }

        if (_document.Find(bubble.Id) is BubbleAnnotation updated)
        {
            (_logger?.Invoke() ?? NullLogger.Instance).LogAnnotationBodyPaddingChanged(updated.BodyPadding);
        }

        return true;
    }

    /// <summary>選択中の吹き出しのしっぽを差し替える。</summary>
    /// <param name="transform">現在のしっぽを受け取って新しいしっぽを返す関数。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="transform"/> が <see langword="null"/> の場合。</exception>
    public bool UpdateSelectedTail(Func<BubbleTail, BubbleTail> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        if (_document.Find(_selectedId) is not BubbleAnnotation bubble
            || !ApplyChange(document => document.WithTail(bubble.Id, transform(bubble.Tail))))
        {
            return false;
        }

        if (_document.Find(bubble.Id) is BubbleAnnotation updated)
        {
            (_logger?.Invoke() ?? NullLogger.Instance).LogAnnotationTailChanged(updated.Tail);
        }

        return true;
    }

    /// <summary>選択中の注釈を最前面へ移す。</summary>
    /// <returns>移した場合は <see langword="true"/>。</returns>
    public bool BringSelectedToFront() => MoveSelectedInOrder(toFront: true);

    /// <summary>選択中の注釈を最背面へ移す。</summary>
    /// <returns>移した場合は <see langword="true"/>。</returns>
    public bool SendSelectedToBack() => MoveSelectedInOrder(toFront: false);

    /// <summary>選択中の注釈の矩形を差し替える（ドラッグ中の更新）。</summary>
    /// <param name="bounds">新しい矩形（元画像ピクセル）。</param>
    /// <returns>文書が変わった場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>履歴への控えは <see cref="BeginChange"/> が持つ</b>: ドラッグは押下から解放までが 1 手である。
    /// 本メソッドは変更を積むだけであり、控えが無ければ何も積まれない（単独で呼ぶと履歴に残らない）。
    /// </remarks>
    public bool SetBounds(EsRect bounds)
    {
        if (_document.Find(_selectedId) is not { } item
            || !SetDocumentWithoutHistory(_document.WithBounds(item.Id, bounds)))
        {
            return false;
        }

        if (_document.Find(item.Id) is { } updated)
        {
            (_logger?.Invoke() ?? NullLogger.Instance).LogAnnotationBoundsChanged(updated.Bounds);
        }

        return true;
    }

    /// <summary>選択中の吹き出しのしっぽの先端を差し替える（ドラッグ中の更新）。</summary>
    /// <param name="tip">新しい先端（元画像ピクセル）。</param>
    /// <param name="margin">画像の端から残す距離（元画像ピクセル。 UI が表示倍率で DIP の距離を換算して渡す）。</param>
    /// <returns>文書が変わった場合は <see langword="true"/>。</returns>
    /// <remarks>履歴への控えは <see cref="BeginChange"/> が持つ（<see cref="SetBounds"/> と同じ）。</remarks>
    public bool SetTailTip(EsPoint tip, double margin = 0)
    {
        if (_document.Find(_selectedId) is not BubbleAnnotation bubble
            || !SetDocumentWithoutHistory(
                _document.WithTailTip(bubble.Id, AnnotationGeometry.ClampTailTip(tip, ImageSize, margin))))
        {
            return false;
        }

        if (_document.Find(bubble.Id) is BubbleAnnotation updated)
        {
            (_logger?.Invoke() ?? NullLogger.Instance).LogAnnotationTailChanged(updated.Tail);
        }

        return true;
    }

    /// <summary>これから変更することを履歴へ控える（押下の直前に呼ぶ）。</summary>
    /// <remarks>控え中に再度呼んでも最初の文書が 1 手の基準になる。</remarks>
    public void BeginChange() => _history.BeginChange(_document);

    /// <summary>控えた変更を 1 手として履歴へ積む（変更が無ければ積まない）。</summary>
    public void CommitChange() => _history.CommitChange(_document);

    /// <summary>控えを捨てる（操作を取り消したとき）。</summary>
    public void CancelChange() => _history.CancelChange();

    /// <summary>直前の変更を元に戻す。</summary>
    /// <returns>戻した場合は <see langword="true"/>。</returns>
    /// <remarks>本文を編集中は編集を続けたままキャレットを本文の範囲へ収める（戻した結果をその場で直せるようにする）。</remarks>
    public bool Undo() => MoveHistory(redo: false);

    /// <summary>元に戻した変更をやり直す。</summary>
    /// <returns>やり直した場合は <see langword="true"/>。</returns>
    public bool Redo() => MoveHistory(redo: true);

    /// <summary>本文の編集を始める（キャレットを本文の末尾へ置く）。</summary>
    /// <returns>編集を始めた場合は <see langword="true"/>。 吹き出しを選んでいない場合は <see langword="false"/>。</returns>
    public bool BeginTextEditing()
    {
        if (_document.Find(_selectedId) is not BubbleAnnotation)
        {
            return false;
        }

        if (_editor.IsEditing)
        {
            return true;
        }

        SyncEditorText();
        _editor.Begin();
        NotifyEditStarted();
        return true;
    }

    /// <summary>本文の編集を終える（内容は既に文書へ入っている）。</summary>
    public void EndTextEditing()
    {
        if (_editor.End() == TextEditChange.None)
        {
            return;
        }

        NotifyEditEnded();
    }

    /// <summary>選択範囲の文字がすべて条件を満たすかどうかを返す（書式ボタンの押下状態に使う）。</summary>
    /// <param name="match">書式が条件を満たすかどうかを返す関数。</param>
    /// <returns>すべて満たす場合は <see langword="true"/>。 選択が無い場合は表示中の書式で判定する。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="match"/> が <see langword="null"/> の場合。</exception>
    public bool SelectedTextMatches(Func<TextStyle, bool> match)
    {
        ArgumentNullException.ThrowIfNull(match);
        return _document.Find(_selectedId) is BubbleAnnotation && _editor.SelectedTextMatches(match);
    }

    /// <summary>キャレットと選択の起点を本文の範囲へ収める（履歴から戻したときなど）。</summary>
    private void ClampCaret() => _editor.ClampCaret();

    /// <summary>このセッションが対象とする画像の大きさを取得する。</summary>
    private EsSize ImageSize => AnnotationGeometry.ResolveImageSize(_document);

    /// <summary>選択中の注釈の重なり順を移し、操作を記録する。</summary>
    /// <param name="toFront">最前面へ移す場合は <see langword="true"/>。</param>
    /// <returns>移した場合は <see langword="true"/>。</returns>
    private bool MoveSelectedInOrder(bool toFront)
    {
        if (_selectedId is not { } id
            || !ApplyChange(document => toFront ? document.BringToFront(id) : document.SendToBack(id)))
        {
            return false;
        }

        (_logger?.Invoke() ?? NullLogger.Instance).LogOrderChanged(toFront, id);
        return true;
    }

    /// <summary>1 回の変更として文書を差し替える（履歴に 1 手として積む）。</summary>
    /// <param name="change">変更前の文書を受け取り、変更後の文書を返す関数（変更なしは同じ実体を返す）。</param>
    /// <returns>文書が変わった場合は <see langword="true"/>。</returns>
    private bool ApplyChange(Func<AnnotationDocument, AnnotationDocument?> change)
    {
        var before = _document;
        _history.BeginChange(before);
        var after = change(before);
        if (after is null || ReferenceEquals(before, after))
        {
            _history.CancelChange();
            return false;
        }

        _document = after;
        SyncEditorText();
        _history.CommitChange(after);
        LogDocumentChanged();
        Notify(AnnotationEditChange.Document);
        return true;
    }

    /// <summary>ドラッグ中の変更を文書へ反映する（押下で控えた 1 手へ積む）。</summary>
    /// <param name="after">変更後の文書。</param>
    /// <returns>文書が変わった場合は <see langword="true"/>。</returns>
    private bool SetDocumentWithoutHistory(AnnotationDocument after)
    {
        if (ReferenceEquals(_document, after))
        {
            return false;
        }

        _document = after;
        SyncEditorText();
        _history.CommitChange(after);
        LogDocumentChanged();
        Notify(AnnotationEditChange.Document);
        return true;
    }

    /// <summary>編集した本文を文書へ書き戻す（変わらない場合は履歴を残さない）。</summary>
    /// <param name="styleRange">書式を適用した範囲（指定した場合だけログへ記録する）。</param>
    /// <returns>文書が変わった場合は <see langword="true"/>。</returns>
    private bool ReplaceEdit((int Start, int Length)? styleRange = null)
    {
        if (_document.Find(_selectedId) is not BubbleAnnotation bubble)
        {
            return false;
        }

        var updated = _editor.Text;
        if (ReferenceEquals(updated, bubble.Text))
        {
            return false;
        }

        var before = _document;
        _history.BeginChange(before);
        var after = before.WithText(bubble.Id, updated);
        _document = after;
        _history.CommitChange(after);
        LogDocumentChanged();
        var logger = _logger?.Invoke() ?? NullLogger.Instance;
        if (!string.Equals(bubble.Text.PlainText, updated.PlainText, StringComparison.Ordinal))
        {
            // **操作の内容と値を残す**（入力・削除・貼り付けのどれか、変更後の本文の先頭）
            logger.LogTextChanged(updated.PlainText.Length, updated.PlainText);
        }

        if (styleRange is { } range)
        {
            logger.LogTextStyleApplied(updated.PlainText, range.Start, range.Length);
        }

        _lastTextSelection = (_editor.SelectionStart, _editor.SelectionLength);
        Notify(AnnotationEditChange.Document | AnnotationEditChange.TextSelection);
        return true;
    }

    /// <summary>履歴を 1 手動かす。</summary>
    /// <param name="redo">やり直す場合は <see langword="true"/>。</param>
    /// <returns>動かした場合は <see langword="true"/>。</returns>
    private bool MoveHistory(bool redo)
    {
        _history.CancelChange();
        var target = redo ? _history.Redo(_document) : _history.Undo(_document);
        if (target is null)
        {
            return false;
        }

        _document = target;
        SyncEditorText();
        if (_selectedId is not null && _document.Find(_selectedId) is null)
        {
            EndTextEditing();
            _selectedId = null;
            ResetEditor();
            Notify(AnnotationEditChange.Selection);
        }
        else if (_editor.IsEditing)
        {
            ClampCaret();
        }

        (_logger?.Invoke() ?? NullLogger.Instance).LogHistoryMoved(redo, _history.UndoCount, _history.RedoCount);
        _lastTextSelection = (_editor.SelectionStart, _editor.SelectionLength);
        Notify(AnnotationEditChange.Document | AnnotationEditChange.TextSelection);
        return true;
    }

    /// <summary>変更を購読者へ通知する。</summary>
    /// <param name="change">変更の種類。</param>
    private void Notify(AnnotationEditChange change)
    {
        if (change == AnnotationEditChange.None)
        {
            return;
        }

        _notices.Raise(change);
    }

    /// <summary>選択中の吹き出しの本文を取得する（編集の入口に渡す）。</summary>
    private RichText SelectedRichText
        => _document.Find(_selectedId) is BubbleAnnotation bubble ? bubble.Text : RichText.Empty;

    /// <summary>編集対象の本文を選択中の吹き出しの本文へ揃える（文書が変わったときに呼ぶ）。</summary>
    private void SyncEditorText()
    {
        var text = SelectedRichText;
        if (!ReferenceEquals(_editor.Text, text))
        {
            _editor.Text = text;
        }
    }

    /// <summary>選択の解除に合わせて編集状態を捨て、編集対象の本文を選択中の吹き出しへ揃える。</summary>
    private void ResetEditor()
    {
        _editor.EditState.Reset();
        SyncEditorText();
    }

    /// <summary>本文の選択範囲が変わったときだけ記録して通知する。</summary>
    /// <remarks>本文コントロールの入力からも呼ぶ（同じ範囲で通知を繰り返さない判定と <c>Trace</c> の記録をここへ集約する）。</remarks>
    public void NotifyTextSelectionChanged()
    {
        var current = (_editor.SelectionStart, _editor.SelectionLength);
        if (current == _lastTextSelection)
        {
            return;
        }

        _lastTextSelection = current;
        (_logger?.Invoke() ?? NullLogger.Instance).LogTextSelectionChanged(
            _editor.CaretIndex, _editor.SelectionStart, _editor.SelectionLength, SelectedPlainText);
        Notify(AnnotationEditChange.TextSelection);
    }

    /// <summary>本文の編集の開始を記録して通知する。</summary>
    private void NotifyEditStarted()
    {
        _lastTextSelection = (_editor.SelectionStart, _editor.SelectionLength);
        (_logger?.Invoke() ?? NullLogger.Instance).LogEditingChanged(
            editing: true, _editor.CaretIndex, _editor.SelectionStart, _editor.SelectionLength, SelectedPlainText);
        Notify(AnnotationEditChange.Editing | AnnotationEditChange.TextSelection);
    }

    /// <summary>本文の編集の終了を記録して通知する。</summary>
    private void NotifyEditEnded()
    {
        _lastTextSelection = (_editor.SelectionStart, _editor.SelectionLength);
        (_logger?.Invoke() ?? NullLogger.Instance).LogEditingChanged(
            editing: false, _editor.CaretIndex, _editor.SelectionStart, _editor.SelectionLength, SelectedPlainText);
        Notify(AnnotationEditChange.Editing | AnnotationEditChange.TextSelection);
    }

    /// <summary>文書の変更を Trace で記録する（ドラッグの 1 歩ごとに届くため）。</summary>
    private void LogDocumentChanged()
        => (_logger?.Invoke() ?? NullLogger.Instance).LogDocumentChanged(_document.Items.Length);
}
