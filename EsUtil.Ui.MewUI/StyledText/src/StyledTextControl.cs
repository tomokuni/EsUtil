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
/// <b>規則は提示層が単一所有する</b>: 本文の値とキャレット・選択・書式の適用は <see cref="TextEditor"/>（状態は <see cref="TextEditState"/>）、
/// キーの割り当ては <see cref="TextEditKeyMap"/>、折り返しと行の配置は <see cref="TextLayout"/> が決める。<br/>
/// <b>座標は元画像ピクセル</b>である: <see cref="RenderScale"/> で画面の DIP へ写す。
/// </remarks>
public sealed partial class StyledTextControl : Panel
{
    /// <summary>キャレットの点滅を進めるタイマー（編集中だけ動かす）。</summary>
    private readonly DispatcherTimer _caretTimer;

    /// <summary>本文の値と編集の状態（規則は提示層が単一所有する）。</summary>
    private TextEditor _editor = new();

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
        get => _editor.Text;
        set
        {
            _editor.Text = value ?? RichText.Empty;
            InvalidateVisual();
        }
    }

    /// <summary>画面の DIP への倍率を取得または設定する。</summary>
    /// <value>元画像ピクセルから画面の DIP への倍率。 0 以下・非有限は 1 として扱う。</value>
    public double RenderScale { get; set; } = 1.0;

    /// <summary>行間の倍率を取得または設定する。</summary>
    /// <value>行間の倍率。 0 以下・非有限は 1 として扱う。</value>
    public double LineHeightFactor { get; set; } = 1.0;

    /// <summary>文字列と書式から実寸を測る関数を取得または設定する。</summary>
    /// <value>既定は本コントロールの描画 API で測る関数。</value>
    public Func<string, TextStyle, EsSize> MeasureText { get; set; } = RichTextPainter.Measure;

    /// <summary>本文の値と編集の状態を取得または設定する。</summary>
    /// <value>既定は本コントロールが作った実体。 ホストが既に編集器を持っている場合はその実体を設定する。</value>
    /// <remarks>
    /// <b>編集器を 2 つ持たない</b>: ホスト（注釈の編集セッションなど）が本文と状態を持っている場合は、
    /// 同じ実体を設定する（別の実体を持つとキャレットと本文が食い違う）。 状態（<see cref="TextEditState"/>）は
    /// 本プロパティの実体が持つため、個別に設定しない。
    /// </remarks>
    /// <exception cref="ArgumentNullException">設定する値が <see langword="null"/> の場合。</exception>
    public TextEditor Editor
    {
        get => _editor;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (ReferenceEquals(_editor, value))
            {
                return;
            }

            _editor = value;
            InvalidateVisual();
        }
    }

    /// <summary>IME の変換中の文字列を取得または設定する（本文へは入れない）。</summary>
    public string PreeditText
    {
        get => _editor.PreeditText;
        set
        {
            var next = value ?? string.Empty;
            if (string.Equals(_editor.PreeditText, next, StringComparison.Ordinal))
            {
                return;
            }

            _editor.PreeditText = next;
            InvalidateVisual();
        }
    }

    /// <summary>本文を編集中かどうかを取得する。</summary>
    public bool IsEditing => _editor.IsEditing;

    /// <summary>編集中に書式バーを自動で表示するかどうかを取得または設定する。</summary>
    /// <value>自動で表示する場合は <see langword="true"/>（既定）。 ホストが独自にバーを出す場合は <see langword="false"/>。</value>
    /// <remarks>
    /// <b>表示の判断は <see cref="ShouldShowFormatBar"/> が持つ</b>: 自動表示が有効でも、1 文字以上を選ぶまでは
    /// 表示しない（適用先が決まらないため）。 ホスト（注釈のパネルなど）は本プロパティを読んでバーを出し入れする。
    /// </remarks>
    public bool ShowFormatBarWhileEditing
    {
        get => _showFormatBarWhileEditing;
        set
        {
            if (_showFormatBarWhileEditing == value)
            {
                return;
            }

            _showFormatBarWhileEditing = value;
            UpdateFormatBarHost();
        }
    }

    /// <summary>編集中に書式バーを自動で表示するか（保持する値）。</summary>
    private bool _showFormatBarWhileEditing = true;

    /// <summary>いま書式バーを表示すべきかどうかを取得する。</summary>
    /// <value>編集中で、自動表示が有効で、1 文字以上を選んでおり、出す項目がある場合は <see langword="true"/>。</value>
    /// <remarks>判定の規則は <see cref="TextFormatBarVisibility.ShouldShow"/> が単一所有する（4 つの UI 版で共有する）。</remarks>
    public bool ShouldShowFormatBar
        => TextFormatBarVisibility.ShouldShow(ShowFormatBarWhileEditing, IsEditing, SelectionLength, FormatBarItems);

    /// <summary>書式バーに出す項目の組み合わせを取得または設定する。</summary>
    /// <value>既定はすべての項目を出す（<see cref="TextFormatBarItemVisibility.Default"/>）。</value>
    /// <remarks>
    /// <b>すべて出さない場合はバーを表示しない</b>: 空のバーを出すと、操作できない枠だけが本文に重なる
    /// （判定は <see cref="ShouldShowFormatBar"/>）。<br/>
    /// <b>バーは本コントロールが作り直す</b>: ホストが置く場合（<see cref="TextFormatBarHosting.Custom"/>）は
    /// <see cref="FormatBar"/> の <c>View</c> を置き直せばよい。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><see langword="null"/> を設定した場合。</exception>
    public TextFormatBarItemVisibility FormatBarItems
    {
        get => _formatBarItems;
        set
        {
            var items = value ?? throw new ArgumentNullException(nameof(value));
            if (_formatBarItems == items)
            {
                return;
            }

            _formatBarItems = items;
            RebuildFormatBar();
            UpdateFormatBarHost();
        }
    }

    /// <summary>書式バーをどこへ出すかを取得または設定する。</summary>
    /// <value>既定はホストが置く <see cref="TextFormatBarHosting.Custom"/>。</value>
    /// <remarks>
    /// <b>バーはどちらの場合も本コントロールが持つ</b>: 生成・項目の反映・キーとフォーカスの転送・内容の更新は
    /// 本コントロールが行う（<see cref="FormatBar"/>）。 本値が決めるのは画面要素をどこへ置くかだけである。
    /// </remarks>
    public TextFormatBarHosting FormatBarHosting
    {
        get => _formatBarHosting;
        set
        {
            if (_formatBarHosting == value)
            {
                return;
            }

            _formatBarHosting = value;
            if (value != TextFormatBarHosting.Popup)
            {
                _formatBarHost?.Close();
            }

            UpdateFormatBarHost();
        }
    }

    /// <summary>書式バーを作る処理を取得または設定する（独自の GUI へ差し替える入口）。</summary>
    /// <value>既定は組み込みの書式バー（<see cref="StyledTextFormatBarFactory"/>）。</value>
    /// <remarks>差し替えるとバーを作り直す（表示中なら新しいバーへ差し替わる）。</remarks>
    /// <exception cref="ArgumentNullException"><see langword="null"/> を設定した場合。</exception>
    public IStyledTextFormatBarFactory FormatBarFactory
    {
        get => _formatBarFactory;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_formatBarFactory, value))
            {
                return;
            }

            _formatBarFactory = value;
            RebuildFormatBar();
            UpdateFormatBarHost();
        }
    }

    /// <summary>書式バーの構成（フォントの一覧・色の選択・色の表記）を取得または設定する。</summary>
    /// <value>既定の構成。</value>
    /// <remarks>差し替えるとバーを作り直す（表示中なら新しい構成のバーへ差し替わる）。</remarks>
    /// <exception cref="ArgumentNullException"><see langword="null"/> を設定した場合。</exception>
    public StyledTextFormatBarOptions FormatBarOptions
    {
        get => _formatBarOptions;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (_formatBarOptions == value)
            {
                return;
            }

            _formatBarOptions = value;
            RebuildFormatBar();
            UpdateFormatBarHost();
        }
    }

    /// <summary>書式バーを取得する（本コントロールが単一所有する実体）。</summary>
    /// <value>初回の取得で作り、<see cref="FormatBarItems"/> などの変更で作り直される。</value>
    /// <remarks>ホストは <c>FormatBar.View</c> を置くだけでよい（配線は本コントロールが行う）。</remarks>
    public IStyledTextFormatBar FormatBar => _formatBar ??= CreateFormatBar();

    /// <summary>書式バーをポップアップで表示中かどうかを取得する。</summary>
    /// <value>表示中の場合は <see langword="true"/>（<see cref="TextFormatBarHosting.Custom"/> のときは常に <see langword="false"/>）。</value>
    public bool IsFormatBarOpen => _formatBarHost?.IsOpen ?? false;

    /// <summary>書式バーに出す項目の組み合わせ（保持する値）。</summary>
    private TextFormatBarItemVisibility _formatBarItems = TextFormatBarItemVisibility.Default;

    /// <summary>書式バーをどこへ出すか（保持する値）。</summary>
    private TextFormatBarHosting _formatBarHosting = TextFormatBarHosting.Custom;

    /// <summary>書式バーを作る処理（保持する値）。</summary>
    private IStyledTextFormatBarFactory _formatBarFactory = new StyledTextFormatBarFactory();

    /// <summary>書式バーの構成（保持する値）。</summary>
    private StyledTextFormatBarOptions _formatBarOptions = new();

    /// <summary>本コントロールが所有する書式バー（初回の取得で作る）。</summary>
    private IStyledTextFormatBar? _formatBar;

    /// <summary>書式バーをポップアップで出す処理（<see cref="TextFormatBarHosting.Popup"/> のときだけ作る）。</summary>
    private StyledTextFormatBarPopup? _formatBarHost;

    /// <summary>本文の編集を始める前のフォーカスの与え方（ホストが入力を持つ場合は何もしない）。</summary>
    private void FocusForBar()
    {
        if (IsInputEnabled)
        {
            Focus();
        }
    }

    /// <summary>書式バーを作る（キーとフォーカスの転送を配線する）。</summary>
    /// <returns>作ったバー。</returns>
    private IStyledTextFormatBar CreateFormatBar()
    {
        var bar = _formatBarFactory.Create(
            new StyledTextFormatBarContext(this), _formatBarOptions with { Items = _formatBarItems });

        // **キー操作とフォーカスは本コントロールが持つ**: バーは操作を転送し、適用後に本文へ戻すだけにする
        bar.SetKeyHandler(TryHandleEditingKey);
        bar.SetFocusRequest(FocusForBar);
        return bar;
    }

    /// <summary>書式バーを作り直す（出す項目・生成・構成の変更を反映する）。</summary>
    private void RebuildFormatBar()
    {
        if (_formatBar is null)
        {
            return;
        }

        _formatBar = CreateFormatBar();
    }

    /// <summary>書式バーの内容と、ポップアップの出し入れを、いまの状態に合わせて更新する。</summary>
    private void UpdateFormatBarHost()
    {
        if (_formatBarHosting == TextFormatBarHosting.Popup)
        {
            _formatBarHost ??= new StyledTextFormatBarPopup(this);
            _formatBarHost.Update();
            return;
        }

        _formatBar?.Refresh();
    }


    /// <summary>このコントロールが載っているウィンドウを取得する（ポップアップの親に使う）。</summary>
    internal Window? HostWindow => FindVisualRoot() as Window;

    /// <summary>選択範囲の矩形（このコントロールの座標の DIP）を取得する。</summary>
    /// <value>選択範囲の外接矩形。 選択が無い・解決できない場合は <see cref="CaretRect"/>。</value>
    internal Rect SelectionBounds
    {
        get
        {
            if (SelectionLength <= 0 || ResolveLineIndex() is not { } index)
            {
                return CaretRect;
            }

            var rects = index.ResolveSelectionRects(SelectionStart, SelectionEnd, MeasureText);
            if (rects.Count == 0)
            {
                return CaretRect;
            }

            var scale = ResolveScale();
            var left = double.MaxValue;
            var top = double.MaxValue;
            var right = double.MinValue;
            var bottom = double.MinValue;
            foreach (var rect in rects)
            {
                left = Math.Min(left, rect.X);
                top = Math.Min(top, rect.Y);
                right = Math.Max(right, rect.X + rect.Width);
                bottom = Math.Max(bottom, rect.Y + rect.Height);
            }

            return new Rect(left * scale, top * scale, (right - left) * scale, (bottom - top) * scale);
        }
    }

    /// <summary>入力を受け付けるかどうかを取得または設定する。</summary>
    /// <value>入力を受け付ける場合は <see langword="true"/>（既定）。 表示専用にする場合は <see langword="false"/>。</value>
    /// <remarks>
    /// <b>ホストが入力を所有する場合に使う</b>: 注釈のレイヤーのように入力とフォーカスを自分で持つホストへ
    /// 重ねるとき、本コントロールがフォーカスを奪うとキーと IME の宛先が変わってしまう。
    /// </remarks>
    public bool IsInputEnabled { get; set; } = true;

    /// <summary>キャレットの位置（文字数）を取得する。</summary>
    public int CaretIndex => _editor.CaretIndex;

    /// <summary>選択範囲の開始位置（文字数）を取得する。</summary>
    public int SelectionStart => _editor.SelectionStart;

    /// <summary>選択範囲の終了位置（文字数）を取得する。</summary>
    public int SelectionEnd => _editor.SelectionEnd;

    /// <summary>選択範囲の長さ（文字数）を取得する。</summary>
    public int SelectionLength => _editor.SelectionLength;

    /// <summary>本文の文字を選んでいるかどうかを取得する。</summary>
    public bool HasSelection => _editor.HasSelection;

    /// <summary>書式バーへ表示する書式を取得する（選択が無い間は覚えた書式）。</summary>
    public TextStyle SelectedTextStyle => _editor.SelectedTextStyle;

    /// <summary>本文を書き換えたときに発生する。</summary>
    public event EventHandler? RichTextChanged;

    /// <summary>キャレットまたは選択範囲が変わったときに発生する。</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>編集の開始・終了で発生する。</summary>
    public event EventHandler? EditingChanged;

    /// <summary>本文の編集を始める（キャレットを本文の末尾へ置く）。</summary>
    public void BeginEditing()
    {
        if (!IsInputEnabled)
        {
            return;
        }

        if (_editor.BeginEdit() == TextEditChange.None)
        {
            Focus();
            return;
        }

        Focus();
        RestartCaretBlink();
        ApplyChange(TextEditChange.Editing | TextEditChange.Selection);
    }

    /// <summary>本文の編集を終える（内容は既に <see cref="RichTextValue"/> へ入っている）。</summary>
    public void EndEditing()
    {
        if (_editor.EndEdit() == TextEditChange.None)
        {
            return;
        }

        _caretTimer.Stop();
        ApplyChange(TextEditChange.Editing | TextEditChange.Selection);
    }

    /// <summary>選択範囲を指定する。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（文字数。 0 は選択なし）。</param>
    public void Select(int start, int length) => ApplyChange(_editor.Select(start, length));

    /// <summary>本文をすべて選択する。</summary>
    public void SelectAll() => ApplyChange(_editor.SelectAll());

    /// <summary>キャレットを指定位置へ移す。</summary>
    /// <param name="index">移動先（文字数）。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    public void MoveCaret(int index, bool extend) => ApplyChange(_editor.MoveCaret(index, extend));

    /// <summary>キャレットを前後の 1 クラスタへ移す（選択があるときは選択の端へ畳む）。</summary>
    /// <param name="direction">移動する向き（負は左、正は右）。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    public void MoveCaretByCluster(int direction, bool extend)
        => ApplyChange(_editor.MoveCaretByCluster(direction, extend));

    /// <summary>キャレットを上下の行の同じ桁へ移す。</summary>
    /// <param name="delta">移動する行数。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    public void MoveCaretByLine(int delta, bool extend)
        => ApplyChange(_editor.MoveCaretByLine(ResolveLineIndex(), delta, extend));

    /// <summary>キャレットを行の先頭・末尾へ移す。</summary>
    /// <param name="first">行の先頭へ移す場合は <see langword="true"/>。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    public void MoveCaretToLineEdge(bool first, bool extend)
        => ApplyChange(_editor.MoveCaretToLineEdge(ResolveLineIndex(), first, extend));

    /// <summary>位置（元画像ピクセル）に最も近い文字位置へキャレットを移す。</summary>
    /// <param name="point">位置（元画像ピクセル）。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    public void SetCaretFromPoint(EsPoint point, bool extend)
    {
        if (ResolveLineIndex() is not { } index)
        {
            return;
        }

        ApplyChange(_editor.SetCaretFromPoint(index, point, extend, MeasureText));
    }

    /// <summary>選択範囲を文字列で置き換える（選択が無ければ挿入）。</summary>
    /// <param name="text">挿入する文字列（空なら削除）。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool ReplaceSelection(string text) => IsTextChanged(_editor.ReplaceSelection(text));

    /// <summary>指定した範囲を文字列で置き換える。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="end">終了位置（文字数）。</param>
    /// <param name="text">置き換える文字列（空なら削除）。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool ReplaceRange(int start, int end, string text) => IsTextChanged(_editor.ReplaceRange(start, end, text));

    /// <summary>選択範囲、またはキャレットの前後の 1 クラスタを削除する。</summary>
    /// <param name="forward">キャレットの次の文字を削除する場合は <see langword="true"/>。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool DeleteAtCaret(bool forward) => IsTextChanged(_editor.DeleteAtCaret(forward));

    /// <summary>選択範囲の書式を丸ごと適用する（選択が無ければ以降の入力へ回す）。</summary>
    /// <param name="style">適用する書式。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool ApplyTextStyle(TextStyle style) => IsTextChanged(_editor.ApplyStyle(style));

    /// <summary>指定した範囲の書式を丸ごと適用する。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（文字数。 0 以下は本文全体）。</param>
    /// <param name="style">適用する書式。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool ApplyTextStyle(int start, int length, TextStyle style)
        => IsTextChanged(_editor.ApplyStyle(start, length, style));

    /// <summary>選択範囲の書式を 1 属性だけ変換する（選択が無ければ以降の入力へ回す）。</summary>
    /// <param name="transform">現在の書式を受け取って新しい書式を返す関数。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool TransformTextStyle(Func<TextStyle, TextStyle> transform)
        => IsTextChanged(_editor.TransformStyle(transform));

    /// <summary>指定した範囲の書式を 1 属性だけ変換する。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（文字数。 0 以下は本文全体）。</param>
    /// <param name="transform">現在の書式を受け取って新しい書式を返す関数。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool TransformTextStyle(int start, int length, Func<TextStyle, TextStyle> transform)
        => IsTextChanged(_editor.TransformStyle(start, length, transform));

    /// <summary>選択範囲の文字がすべて条件を満たすかどうかを取得する。</summary>
    /// <param name="match">書式が条件を満たすかどうかを返す関数。</param>
    /// <returns>すべて満たす場合は <see langword="true"/>。</returns>
    public bool SelectedTextMatches(Func<TextStyle, bool> match) => _editor.SelectedTextMatches(match);

    /// <summary>選択中の文字列を取得する。</summary>
    /// <returns>選択中の文字列（選択が無い場合は空）。</returns>
    public string GetSelectedText() => _editor.GetSelectedText();

    /// <summary>選択中の文字列をクリップボードへコピーする。</summary>
    /// <returns>コピーの非同期処理。</returns>
    public Task CopyAsync()
    {
        var text = GetSelectedText();
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
        if (GetSelectedText().Length == 0)
        {
            return;
        }

        await CopyAsync();
        ReplaceSelection(string.Empty);
    }

    /// <summary>クリップボードの文字列を挿入する。</summary>
    /// <returns>挿入した文字列（貼り付ける文字列が無い場合は <see langword="null"/>）。</returns>
    public Task<string?> PasteAsync()
    {
        var text = RichTextClipboard.GetText();
        if (text.Length == 0)
        {
            return Task.FromResult<string?>(null);
        }

        var normalized = EsUtil.Feature.StyledText.Core.Text.TextInput.Normalize(text);
        ReplaceSelection(normalized);
        return Task.FromResult<string?>(normalized);
    }

    /// <summary>キャレットの矩形（このコントロールの座標の DIP）を取得する。</summary>
    internal Rect CaretRect
    {
        get
        {
            if (_editor.ResolveCaretRect(ResolveBodySize(), MeasureText, LineHeightFactor) is not { } caret)
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
            var lines = ResolveLines();

            // **同じフレームでは同じレイアウト実体を使う**: 文字の描画とキャレット・選択範囲の
            // 位置計算でレイアウトを作り直すと、丸めやキャッシュの差で位置が食い違う。
            var layouts = new TextLayoutCache();
            foreach (var line in lines)
            {
                RichTextPainter.DrawLine(context, line, layouts.Create);
            }

            if (!IsEditing || TextLineIndex.Create(lines, DisplayText.PlainText) is not { } index)
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
            if (index.ResolveCaret(_editor.DisplayCaretIndex, MeasureText) is not { } caret || !IsCaretVisible)
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
            case TextEditCommand.EndEditing:
                EndEditing();
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
        }

        var change = _editor.TryExecute(request, ResolveLineIndex);
        if (change == TextEditChange.None)
        {
            return false;
        }

        ApplyChange(change);
        return true;
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
    internal RichText DisplayText => _editor.DisplayText;

    /// <summary>本文の領域（元画像ピクセル）を取得する。</summary>
    /// <returns>本文の領域。</returns>
    private EsSize ResolveBodySize()
    {
        var scale = ResolveScale();
        return new EsSize(RenderSize.Width / scale, RenderSize.Height / scale);
    }

    /// <summary>本文を折り返して行へ配置する（表示と編集で同じ結果を使う）。</summary>
    /// <returns>行の並び（大きさが 0 の場合は空）。</returns>
    private IReadOnlyList<TextLinePlan> ResolveLines()
        => _editor.ResolveLines(ResolveBodySize(), MeasureText, LineHeightFactor);

    /// <summary>行と文字位置の対応を作る。</summary>
    /// <returns>対応。 行が無い場合は <see langword="null"/>。</returns>
    /// <remarks>
    /// <b>表示と同じ本文で作る</b>: 変換中の文字列を入れないと、キャレットと変換候補の位置が
    /// 確定後の位置と食い違う（入力位置から遠い场所に候補が出る）。
    /// </remarks>
    private TextLineIndex? ResolveLineIndex()
        => _editor.ResolveLineIndex(ResolveBodySize(), MeasureText, LineHeightFactor);

    /// <summary>表示倍率を取得する（無効な値は基準値へ正規化する）。</summary>
    /// <returns>表示倍率。</returns>
    /// <remarks>正規化の規則は <see cref="EsUtil.Platform.Core.Coord.DpiScaler.NormalizeScaling"/> が単一所有する。</remarks>
    private double ResolveScale() => EsUtil.Platform.Core.Coord.DpiScaler.NormalizeScaling(RenderScale);

    /// <summary>編集の結果を通知して描画を更新する。</summary>
    /// <param name="change">変わった内容。</param>
    private void ApplyChange(TextEditChange change)
    {
        if ((change & TextEditChange.Text) != 0)
        {
            RichTextChanged?.Invoke(this, EventArgs.Empty);
            RestartCaretBlink();
        }

        if ((change & TextEditChange.Editing) != 0)
        {
            EditingChanged?.Invoke(this, EventArgs.Empty);
        }

        if (change != TextEditChange.None)
        {
            AfterSelectionChanged();
        }
    }

    /// <summary>本文が変わったかを返す（変わった内容を通知する）。</summary>
    /// <param name="change">変わった内容。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    private bool IsTextChanged(TextEditChange change)
    {
        ApplyChange(change);
        return (change & TextEditChange.Text) != 0;
    }

    /// <summary>選択範囲が変わったことを通知して描画を更新する。</summary>
    private void AfterSelectionChanged()
    {
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();

        // **ポップアップは選択に追随する**: 選択が無くなったら閉じ、位置が変わったら置き直す
        UpdateFormatBarHost();
    }

    /// <summary>位置（このコントロールの DIP）を元画像ピクセルへ写す。</summary>
    /// <param name="point">位置（DIP）。</param>
    /// <returns>位置（元画像ピクセル）。</returns>
    private EsPoint ToSource(Point point)
    {
        var scale = ResolveScale();
        return new EsPoint(point.X / scale, point.Y / scale);
    }

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
