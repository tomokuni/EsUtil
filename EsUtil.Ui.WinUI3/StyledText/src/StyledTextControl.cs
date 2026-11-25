using EsUtil.Platform.Core.Geometry;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.System;
using Windows.UI.Core;

namespace EsUtil.Ui.WinUI3.StyledText;

/// <summary>本文を表示し、その場で編集するコントロール（表示と編集を 1 つの部品にまとめる）。</summary>
/// <remarks>
/// <b>表示と編集を分けない</b>: 入力欄を重ねる方法では、書式（run 単位の色・太字）が反映されず、
/// 書式バーを操作するたびにフォーカスと選択が外れる。 本コントロールは本文を <see cref="TextBlock"/> として
/// 並べ、キー入力のたびに本文を書き換える。<br/>
/// <b>規則は提示層が単一所有する</b>: 本文の値とキャレット・選択・書式の適用は <see cref="TextEditor"/>（状態は <see cref="TextEditState"/>）、
/// キーの割り当ては <see cref="TextEditKeyMap"/>、折り返しと行の配置は <see cref="TextLayout"/> が決める。<br/>
/// <b>座標は元画像ピクセル</b>である: <see cref="RenderScale"/> で画面の DIP へ写す。
/// </remarks>
public sealed partial class StyledTextControl : Canvas
{
    /// <summary>テンキーの 5（<c>VK_NUMPAD5</c>）のキー値。</summary>
    /// <remarks><see cref="VirtualKey"/> にテンキーの名前が無いため値をそのまま持つ（<c>VirtualKey.Number5</c> は上段の 5）。</remarks>
    private const VirtualKey Numpad5 = (VirtualKey)0x65;

    /// <summary>キャレットの点滅を進めるタイマー（編集中だけ動かす）。</summary>
    private readonly DispatcherTimer _caretTimer;

    /// <summary>本文の値と編集の状態（規則は提示層が単一所有する）。</summary>
    private TextEditor _editor = new();

    /// <summary>キャレットの点滅を始めた時刻。</summary>
    private DateTimeOffset _caretBlinkStart = DateTimeOffset.UtcNow;

    /// <summary>本文の編集と表示を担うコントロールを初期化する。</summary>
    public StyledTextControl()
    {
        IsTabStop = true;
        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        _caretTimer = new Microsoft.UI.Xaml.DispatcherTimer { Interval = TextCaretBlink.Interval };
        _caretTimer.Tick += (_, _) =>
        {
            if (IsEditing)
            {
                Redraw();
            }
            else
            {
                _caretTimer.Stop();
            }
        };

        KeyDown += OnEditorKeyDown;
        PointerPressed += OnEditorPointerPressed;
        PointerMoved += OnEditorPointerMoved;
        PointerReleased += OnEditorPointerReleased;
        CharacterReceived += OnEditorCharacterReceived;
        SizeChanged += (_, _) => Redraw();
    }

    /// <summary>表示する本文を取得または設定する。</summary>
    public RichText RichTextValue
    {
        get => _editor.Text;
        set
        {
            _editor.Text = value ?? RichText.Empty;
            Redraw();
        }
    }

    /// <summary>画面の DIP への倍率を取得または設定する。</summary>
    /// <value>元画像ピクセルから画面の DIP への倍率。 0 以下・非有限は 1 として扱う。</value>
    public double RenderScale { get; set; } = 1.0;

    /// <summary>行間の倍率を取得または設定する。</summary>
    /// <value>行間の倍率。 0 以下・非有限は 1 として扱う。</value>
    public double LineHeightFactor { get; set; } = 1.0;

    /// <summary>文字列と書式から実寸を測る関数を取得または設定する。</summary>
    /// <value>既定は XAML のテキスト エンジンで測る関数。</value>
    public Func<string, TextStyle, EsSize> MeasureText { get; set; } = RichTextMetrics.Measure;

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
            Redraw();
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
            Redraw();
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
    public bool ShowFormatBarWhileEditing { get; set; } = true;

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
    /// <b>ホストがバーを置く場合に使う</b>: ホストは本値と同じ組み合わせを書式バーの構成（<c>Items</c>）へ渡す。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><see langword="null"/> を設定した場合。</exception>
    public TextFormatBarItemVisibility FormatBarItems
    {
        get => _formatBarItems;
        set => _formatBarItems = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>書式バーに出す項目の組み合わせ（保持する値）。</summary>
    private TextFormatBarItemVisibility _formatBarItems = TextFormatBarItemVisibility.Default;

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
            FocusImeSink();
            return;
        }

        FocusImeSink();
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
        ReleaseImeSink();
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
    public async Task CopyAsync()
    {
        var text = GetSelectedText();
        if (text.Length == 0)
        {
            return;
        }

        var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
        package.SetText(text);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        await Task.CompletedTask;
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
    public async Task<string?> PasteAsync()
    {
        var data = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
        if (!data.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
        {
            return null;
        }

        var text = await data.GetTextAsync();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var normalized = EsUtil.Feature.StyledText.Core.Text.TextInput.Normalize(text);
        ReplaceSelection(normalized);
        return normalized;
    }

    /// <summary>キャレットの矩形（このコントロールの座標の DIP）を取得する。</summary>
    internal Windows.Foundation.Rect CaretRect
    {
        get
        {
            if (_editor.ResolveCaretRect(ResolveBodySize(), MeasureText, LineHeightFactor) is not { } caret)
            {
                return default;
            }

            var scale = ResolveScale();
            return new Windows.Foundation.Rect(
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
            Redraw();
        }
    }

    /// <summary>本文・選択範囲・キャレットを並べ直す（子要素はすべて作り直す）。</summary>
    private void Redraw()
    {
        Children.Clear();
        var scale = ResolveScale();

        // **変換中の文字列も本文として描く**（重ねて描くと既存の文字と二重になる）
        var lines = ResolveLines();
        foreach (var line in lines)
        {
            foreach (var segment in line.Segments)
            {
                var text = RichTextMetrics.CreateText(segment, scale);
                SetLeft(text, segment.Bounds.X * scale);
                SetTop(text, RichTextMetrics.ResolveTextTop(line, segment) * scale);
                Children.Add(text);
            }
        }

        if (!IsEditing || TextLineIndex.Create(lines, DisplayText.PlainText) is not { } index)
        {
            return;
        }

        var selectionBrush = RichTextMetrics.ToBrush(TextEditorAppearance.SelectionColor);
        foreach (var rect in index.ResolveSelectionRects(SelectionStart, SelectionEnd, MeasureText))
        {
            var selection = new Rectangle
            {
                Width = rect.Width * scale,
                Height = rect.Height * scale,
                Fill = selectionBrush,
                IsHitTestVisible = false,
            };
            SetLeft(selection, rect.X * scale);
            SetTop(selection, rect.Y * scale);
            Children.Add(selection);
        }

        // **変換中はキャレットを変換中の文字列の末尾へ置く**
        if (index.ResolveCaret(_editor.DisplayCaretIndex, MeasureText) is not { } caret || !IsCaretVisible)
        {
            return;
        }

        var caretShape = new Rectangle
        {
            Width = TextEditorAppearance.CaretThicknessDip,
            Height = caret.Height * scale,
            Fill = RichTextMetrics.ToBrush(TextEditorAppearance.CaretColor),
            IsHitTestVisible = false,
        };
        SetLeft(caretShape, caret.X * scale);
        SetTop(caretShape, caret.Y * scale);
        Children.Add(caretShape);
    }

    /// <summary>描画に使う本文を取得する（変換中の文字列を含む）。</summary>
    /// <value>変換中でなければ <see cref="RichTextValue"/>。 変換中は変換中の文字列を入れた描画用の値。</value>
    /// <remarks>
    /// <b>変換中の文字列は本文へ入れない</b>: 表示だけに入れる（確定したときだけ本文が変わる）。
    /// 入れないと、入力中の文字が表示されず「入力が反映されていない」ように見える。
    /// </remarks>
    internal RichText DisplayText => _editor.DisplayText;

    /// <summary>本文の領域（元画像ピクセル）を取得する。</summary>
    /// <returns>本文の領域。</returns>
    private EsSize ResolveBodySize()
    {
        var scale = ResolveScale();
        return new EsSize(ActualWidth / scale, ActualHeight / scale);
    }

    /// <summary>本文を折り返して行へ配置する（表示と編集で同じ結果を使う）。</summary>
    /// <returns>行の並び（大きさが 0 の場合は空）。</returns>
    private IReadOnlyList<TextLinePlan> ResolveLines()
        => _editor.ResolveLines(ResolveBodySize(), MeasureText, LineHeightFactor);

    /// <summary>行と文字位置の対応を作る。</summary>
    /// <returns>対応。 行が無い場合は <see langword="null"/>。</returns>
    /// <remarks>
    /// <b>表示と同じ本文で作る</b>: 変換中の文字列を入れないと、キャレットの位置が確定後の位置と食い違う。
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
        Redraw();
    }

    /// <summary>位置（このコントロールの DIP）を元画像ピクセルへ写す。</summary>
    /// <param name="point">位置（DIP）。</param>
    /// <returns>位置（元画像ピクセル）。</returns>
    private EsPoint ToSource(Windows.Foundation.Point point)
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

    /// <summary>キーが押されたときの処理（割り当ては <see cref="TextEditKeyMap"/> が単一所有する）。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">キーの引数。</param>
    private void OnEditorKeyDown(object sender, KeyRoutedEventArgs e)
    {
        _ = sender;
        if (TryHandleEditingKey(e))
        {
            e.Handled = true;
        }
    }

    /// <summary>キーを本文の編集操作へ適用する（IME の受け口がフォーカスを持つ場合の転送口）。</summary>
    /// <param name="e">キーの引数。</param>
    /// <returns>処理した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>割り当ては <see cref="TextEditKeyMap"/> が単一所有する</b>: 本コントロールと IME の受け口が
    /// 同じ規則で動くよう、キーの解釈を 2 か所に書かない。
    /// </remarks>
    internal bool TryHandleEditingKey(KeyRoutedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (!IsInputEnabled || !IsEditing)
        {
            return false;
        }

        var request = TextEditKeyMap.Resolve(ToTextEditKey(e.Key), CurrentModifiers());
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

    /// <summary>文字入力を処理する。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">入力の引数。</param>
    private void OnEditorCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs e)
    {
        _ = sender;
        if (!IsInputEnabled || !IsEditing)
        {
            return;
        }

        var character = e.Character;
        if (character < ' ')
        {
            return;
        }

        ReplaceSelection(character.ToString());
        e.Handled = true;
    }

    /// <summary>ポインターが押されたときの処理（キャレットの移動・選択の開始）。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">ポインターの引数。</param>
    private void OnEditorPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _ = sender;
        if (!IsInputEnabled)
        {
            return;
        }

        // **押下で編集を始める**: 利用者は「編集を開始」を押さずに本文へ書き始められる
        BeginEditing();
        Focus(FocusState.Pointer);
        SetCaretFromPoint(ToSource(e.GetCurrentPoint(this).Position), (CurrentModifiers() & TextEditModifiers.Shift) != 0);
        CapturePointer(e.Pointer);
        e.Handled = true;
    }

    /// <summary>ポインターが動いたときの処理（選択の拡張）。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">ポインターの引数。</param>
    private void OnEditorPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        _ = sender;
        if (IsInputEnabled && IsEditing && e.Pointer.IsInContact)
        {
            SetCaretFromPoint(ToSource(e.GetCurrentPoint(this).Position), extend: true);
        }
    }

    /// <summary>ポインターが離されたときの処理。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">ポインターの引数。</param>
    private void OnEditorPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _ = sender;
        ReleasePointerCapture(e.Pointer);
    }

    /// <summary>押されている修飾キーを本層の修飾キーへ写す。</summary>
    /// <returns>本層の修飾キー。</returns>
    private static TextEditModifiers CurrentModifiers()
    {
        var result = TextEditModifiers.None;
        if (IsDown(VirtualKey.Control))
        {
            result |= TextEditModifiers.Control;
        }

        if (IsDown(VirtualKey.Shift))
        {
            result |= TextEditModifiers.Shift;
        }

        if (IsDown(VirtualKey.Menu))
        {
            result |= TextEditModifiers.Alt;
        }

        return result;
    }

    /// <summary>キーが押されているかどうかを取得する。</summary>
    /// <param name="key">キー。</param>
    /// <returns>押されている場合は <see langword="true"/>。</returns>
    private static bool IsDown(VirtualKey key)
        => (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

    /// <summary>UI のキーを本層のキーへ写す（本層が扱うキーだけ）。</summary>
    /// <param name="key">UI のキー。</param>
    /// <returns>本層のキー。</returns>
    private static TextEditKey ToTextEditKey(VirtualKey key) => key switch
    {
        VirtualKey.A => TextEditKey.A,
        VirtualKey.B => TextEditKey.B,
        VirtualKey.C => TextEditKey.C,
        VirtualKey.I => TextEditKey.I,
        VirtualKey.U => TextEditKey.U,
        VirtualKey.V => TextEditKey.V,
        VirtualKey.X => TextEditKey.X,
        VirtualKey.Number5 or Numpad5 => TextEditKey.Digit5,
        VirtualKey.Escape => TextEditKey.Escape,
        VirtualKey.Enter => TextEditKey.Enter,
        VirtualKey.Back => TextEditKey.Back,
        VirtualKey.Delete => TextEditKey.Delete,
        VirtualKey.Left => TextEditKey.Left,
        VirtualKey.Right => TextEditKey.Right,
        VirtualKey.Up => TextEditKey.Up,
        VirtualKey.Down => TextEditKey.Down,
        VirtualKey.Home => TextEditKey.Home,
        VirtualKey.End => TextEditKey.End,
        _ => TextEditKey.None,
    };
}
