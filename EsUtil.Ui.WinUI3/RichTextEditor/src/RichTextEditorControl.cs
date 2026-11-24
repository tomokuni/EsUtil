using EsUtil.Platform.Core.Geometry;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.System;
using Windows.UI.Core;

namespace EsUtil.Ui.WinUI3.RichTextEditor;

/// <summary>本文を表示し、その場で編集するコントロール（表示と編集を 1 つの部品にまとめる）。</summary>
/// <remarks>
/// <b>表示と編集を分けない</b>: 入力欄を重ねる方法では、書式（run 単位の色・太字）が反映されず、
/// 書式バーを操作するたびにフォーカスと選択が外れる。 本コントロールは本文を <see cref="TextBlock"/> として
/// 並べ、キー入力のたびに本文を書き換える。<br/>
/// <b>規則は提示層が単一所有する</b>: キャレット・選択・書式の適用は <see cref="TextEditState"/>、
/// キーの割り当ては <see cref="TextEditKeyMap"/>、折り返しと行の配置は <see cref="TextLayout"/> が決める。<br/>
/// <b>座標は元画像ピクセル</b>である: <see cref="RenderScale"/> で画面の DIP へ写す。
/// </remarks>
public sealed partial class RichTextEditorControl : Canvas
{
    /// <summary>キャレットの点滅を進めるタイマー（編集中だけ動かす）。</summary>
    private readonly DispatcherTimer _caretTimer;

    /// <summary>キャレットと選択の状態（規則は提示層が単一所有する）。</summary>
    private TextEditState _edit = new();

    /// <summary>IME の変換中の文字列（本文へは入れない）。</summary>
    private string _preeditText = string.Empty;

    /// <summary>表示する本文。</summary>
    private RichText _text = RichText.Empty;

    /// <summary>キャレットの点滅を始めた時刻。</summary>
    private DateTimeOffset _caretBlinkStart = DateTimeOffset.UtcNow;

    /// <summary>本文の編集と表示を担うコントロールを初期化する。</summary>
    public RichTextEditorControl()
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
    public RichText Text
    {
        get => _text;
        set
        {
            _text = value ?? RichText.Empty;

            // **本文が外から変わったら編集位置を本文の範囲へ収める**
            if (_edit.IsActive)
            {
                _edit.Clamp(_text.PlainText.Length);
            }

            Redraw();
        }
    }

    /// <summary>画面の DIP への倍率を取得または設定する。</summary>
    /// <value>1 以上で拡大。 1 未満・非有限は 1 として扱う。</value>
    public double RenderScale { get; set; } = 1.0;

    /// <summary>行間の倍率を取得または設定する。</summary>
    /// <value>1 未満・非有限は 1 として扱う。</value>
    public double LineHeightFactor { get; set; } = 1.0;

    /// <summary>文字列と書式から実寸を測る関数を取得または設定する。</summary>
    /// <value>既定は XAML のテキスト エンジンで測る関数。</value>
    public Func<string, TextStyle, EsSize> MeasureText { get; set; } = RichTextMetrics.Measure;

    /// <summary>キャレット・選択・書式の状態を取得または設定する。</summary>
    /// <value>既定は本コントロールが作った状態。 ホストが既に状態を持っている場合はその実体を設定する。</value>
    public TextEditState EditState
    {
        get => _edit;
        set
        {
            _edit = value ?? new TextEditState();
            Redraw();
        }
    }

    /// <summary>IME の変換中の文字列を取得または設定する（本文へは入れない）。</summary>
    public string PreeditText
    {
        get => _preeditText;
        set
        {
            var next = value ?? string.Empty;
            if (string.Equals(_preeditText, next, StringComparison.Ordinal))
            {
                return;
            }

            _preeditText = next;
            Redraw();
        }
    }

    /// <summary>本文を編集中かどうかを取得する。</summary>
    public bool IsEditing => _edit.IsActive;

    /// <summary>入力を受け付けるかどうかを取得または設定する。</summary>
    /// <value>入力を受け付ける場合は <see langword="true"/>（既定）。 表示専用にする場合は <see langword="false"/>。</value>
    /// <remarks>
    /// <b>ホストが入力を所有する場合に使う</b>: 注釈のレイヤーのように入力とフォーカスを自分で持つホストへ
    /// 重ねるとき、本コントロールがフォーカスを奪うとキーと IME の宛先が変わってしまう。
    /// </remarks>
    public bool IsInputEnabled { get; set; } = true;

    /// <summary>キャレットの位置（文字数）を取得する。</summary>
    public int CaretIndex => _edit.CaretIndex;

    /// <summary>選択範囲の開始位置（文字数）を取得する。</summary>
    public int SelectionStart => _edit.SelectionStart;

    /// <summary>選択範囲の終了位置（文字数）を取得する。</summary>
    public int SelectionEnd => _edit.SelectionEnd;

    /// <summary>選択範囲の長さ（文字数）を取得する。</summary>
    public int SelectionLength => _edit.SelectionLength;

    /// <summary>本文の文字を選んでいるかどうかを取得する。</summary>
    public bool HasSelection => _edit.HasSelection;

    /// <summary>本文を書き換えたときに発生する。</summary>
    public event EventHandler? TextChanged;

    /// <summary>キャレットまたは選択範囲が変わったときに発生する。</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>編集の開始・終了で発生する。</summary>
    public event EventHandler? EditingChanged;

    /// <summary>本文の編集を始める（キャレットを本文の末尾へ置く）。</summary>
    public void BeginEditing()
    {
        if (_edit.IsActive)
        {
            return;
        }

        _edit.Begin(Text.PlainText.Length);
        Focus(FocusState.Programmatic);
        RestartCaretBlink();
        EditingChanged?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        Redraw();
    }

    /// <summary>本文の編集を終える（内容は既に <see cref="Text"/> へ入っている）。</summary>
    public void EndEditing()
    {
        if (!_edit.IsActive)
        {
            return;
        }

        _edit.End();
        _preeditText = string.Empty;
        _caretTimer.Stop();
        EditingChanged?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        Redraw();
    }

    /// <summary>選択範囲を指定する。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（文字数。 0 は選択なし）。</param>
    public void Select(int start, int length)
    {
        _edit.Select(start, length, Text.PlainText.Length);
        AfterSelectionChanged();
    }

    /// <summary>本文をすべて選択する。</summary>
    public void SelectAll()
    {
        var length = Text.PlainText.Length;
        _edit.Select(0, length, length);
        AfterSelectionChanged();
    }

    /// <summary>キャレットを指定位置へ移す。</summary>
    /// <param name="index">移動先（文字数）。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    public void MoveCaret(int index, bool extend)
    {
        var text = Text.PlainText;
        var target = Math.Clamp(index, 0, text.Length);

        // **キャレットは書記素クラスタの境界にだけ置く**
        target = target < _edit.CaretIndex
            ? TextCursor.SnapBackward(text, target)
            : TextCursor.SnapForward(text, target);
        _edit.MoveTo(target, text.Length, extend);
        AfterSelectionChanged();
    }

    /// <summary>キャレットを前後の 1 クラスタへ移す（選択があるときは選択の端へ畳む）。</summary>
    /// <param name="direction">移動する向き（負は左、正は右）。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    public void MoveCaretByCluster(int direction, bool extend)
        => MoveCaret(_edit.ResolveClusterTarget(direction, extend), extend);

    /// <summary>キャレットを上下の行の同じ桁へ移す。</summary>
    /// <param name="delta">移動する行数。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    public void MoveCaretByLine(int delta, bool extend)
    {
        _edit.MoveByLine(ResolveLineIndex(), Text.PlainText.Length, delta, extend);
        SnapCaretToCluster(extend);
        AfterSelectionChanged();
    }

    /// <summary>キャレットを行の先頭・末尾へ移す。</summary>
    /// <param name="first">行の先頭へ移す場合は <see langword="true"/>。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    public void MoveCaretToLineEdge(bool first, bool extend)
    {
        _edit.MoveToLineEdge(ResolveLineIndex(), Text.PlainText.Length, first, extend);
        SnapCaretToCluster(extend);
        AfterSelectionChanged();
    }

    /// <summary>位置（元画像ピクセル）に最も近い文字位置へキャレットを移す。</summary>
    /// <param name="point">位置（元画像ピクセル）。</param>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    public void SetCaretFromPoint(EsPoint point, bool extend)
    {
        if (ResolveLineIndex() is not { } index)
        {
            return;
        }

        _edit.SetCaretFromPoint(index, point, Text.PlainText.Length, extend, MeasureText);
        AfterSelectionChanged();
    }

    /// <summary>選択範囲を文字列で置き換える（選択が無ければ挿入）。</summary>
    /// <param name="text">挿入する文字列（空なら削除）。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool ReplaceSelection(string text) => Commit(_edit.ReplaceSelection(Text, text));

    /// <summary>指定した範囲を文字列で置き換える。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="end">終了位置（文字数）。</param>
    /// <param name="text">置き換える文字列（空なら削除）。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool ReplaceRange(int start, int end, string text)
        => Commit(_edit.ReplaceRange(Text, start, end, text));

    /// <summary>選択範囲、またはキャレットの前後の 1 クラスタを削除する。</summary>
    /// <param name="forward">キャレットの次の文字を削除する場合は <see langword="true"/>。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool DeleteAtCaret(bool forward) => Commit(_edit.Delete(Text, forward));

    /// <summary>選択範囲の書式を丸ごと適用する（選択が無ければ以降の入力へ回す）。</summary>
    /// <param name="style">適用する書式。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool ApplyTextStyle(TextStyle style) => Commit(_edit.ApplyStyle(Text, style));

    /// <summary>指定した範囲の書式を丸ごと適用する。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（文字数。 0 以下は本文全体）。</param>
    /// <param name="style">適用する書式。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool ApplyTextStyle(int start, int length, TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);

        var updated = TextStyleEdit.ResolveRange(start, length) is { } range
            ? Text.ApplyStyle(range.Start, range.Length, style)
            : Text;
        _edit.Remember(style);
        return Commit(updated);
    }

    /// <summary>選択範囲の書式を 1 属性だけ変換する（選択が無ければ以降の入力へ回す）。</summary>
    /// <param name="transform">現在の書式を受け取って新しい書式を返す関数。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool TransformTextStyle(Func<TextStyle, TextStyle> transform)
        => Commit(_edit.TransformStyle(Text, transform));

    /// <summary>指定した範囲の書式を 1 属性だけ変換する。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（文字数。 0 以下は本文全体）。</param>
    /// <param name="transform">現在の書式を受け取って新しい書式を返す関数。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    public bool TransformTextStyle(int start, int length, Func<TextStyle, TextStyle> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        var at = Math.Clamp(start, 0, Text.PlainText.Length);
        var next = transform(Text.StyleAt(at));
        _edit.Remember(next);
        return Commit(Text.TransformStyle(start, length, _ => next));
    }

    /// <summary>書式バーへ表示する書式を取得する（選択が無い間は覚えた書式）。</summary>
    public TextStyle SelectedTextStyle => _edit.ResolveDisplayStyle(Text);

    /// <summary>選択範囲の文字がすべて条件を満たすかどうかを取得する。</summary>
    /// <param name="match">書式が条件を満たすかどうかを返す関数。</param>
    /// <returns>すべて満たす場合は <see langword="true"/>。</returns>
    public bool SelectedTextMatches(Func<TextStyle, bool> match) => _edit.AllMatch(Text, match);

    /// <summary>選択中の文字列を取得する。</summary>
    /// <returns>選択中の文字列（選択が無い場合は空）。</returns>
    public string SelectedText()
    {
        var text = Text.PlainText;
        var start = Math.Clamp(SelectionStart, 0, text.Length);
        var end = Math.Clamp(SelectionEnd, start, text.Length);
        return text[start..end];
    }

    /// <summary>選択中の文字列をクリップボードへコピーする。</summary>
    /// <returns>コピーの非同期処理。</returns>
    public async Task CopyAsync()
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

    /// <summary>選択中の文字列を切り取る。</summary>
    /// <returns>切り取りの非同期処理。</returns>
    public async Task CutAsync()
    {
        if (SelectedText().Length == 0)
        {
            return;
        }

        await CopyAsync();
        ReplaceSelection(string.Empty);
    }

    /// <summary>クリップボードの文字列を挿入する。</summary>
    /// <returns>貼り付けの非同期処理。</returns>
    public async Task PasteAsync()
    {
        var data = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
        if (!data.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
        {
            return;
        }

        var text = await data.GetTextAsync();
        if (!string.IsNullOrEmpty(text))
        {
            ReplaceSelection(NormalizeInput(text));
        }
    }

    /// <summary>キャレットの矩形（このコントロールの座標の DIP）を取得する。</summary>
    public Windows.Foundation.Rect CaretRect
    {
        get
        {
            if (ResolveLineIndex() is not { } index || index.ResolveCaret(_edit.CaretIndex, MeasureText) is not { } caret)
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
    public void Redraw()
    {
        Children.Clear();
        var scale = ResolveScale();

        // **変換中の文字列も本文として描く**（重ねて描くと既存の文字と二重になる）
        var displayed = DisplayText;
        var lines = ResolveLines(displayed);
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

        if (!IsEditing || TextLineIndex.Create(lines, displayed.PlainText) is not { } index)
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
        var caretIndex = TextCompositionPreview.ResolveCaret(_edit.CaretIndex, _preeditText);
        if (index.ResolveCaret(caretIndex, MeasureText) is not { } caret || !IsCaretVisible)
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

    /// <summary>本文を折り返して行へ配置する（表示と編集で同じ結果を使う）。</summary>
    /// <returns>行の並び（大きさが 0 の場合は空）。</returns>
    /// <summary>描画に使う本文を取得する（変換中の文字列を含む）。</summary>
    /// <value>変換中でなければ <see cref="Text"/>。 変換中は変換中の文字列を入れた描画用の値。</value>
    /// <remarks>
    /// <b>変換中の文字列は本文へ入れない</b>: 表示だけに入れる（確定したときだけ本文が変わる）。
    /// 入れないと、入力中の文字が表示されず「入力が反映されていない」ように見える。
    /// </remarks>
    internal RichText DisplayText => TextCompositionPreview.Apply(Text, _edit.CaretIndex, _preeditText);

    /// <summary>本文を折り返して行へ配置する（表示と編集で同じ結果を使う）。</summary>
    /// <param name="text">折り返す本文（変換中の文字列を含む表示用の値）。</param>
    /// <returns>行の並び。</returns>
    private IReadOnlyList<TextLinePlan> ResolveLines(RichText text)
    {
        var scale = ResolveScale();
        var width = ActualWidth / scale;
        var height = ActualHeight / scale;
        if (width <= 0 || height <= 0)
        {
            return [];
        }

        return TextLayout.ResolveLines(text, new EsRect(0, 0, width, height), MeasureText, LineHeightFactor);
    }

    /// <summary>行と文字位置の対応を作る。</summary>
    /// <returns>対応。 行が無い場合は <see langword="null"/>。</returns>
    /// <remarks>
    /// <b>表示と同じ本文で作る</b>: 変換中の文字列を入れないと、キャレットの位置が確定後の位置と食い違う。
    /// </remarks>
    private TextLineIndex? ResolveLineIndex()
    {
        var displayed = DisplayText;
        return TextLineIndex.Create(ResolveLines(displayed), displayed.PlainText);
    }

    /// <summary>表示倍率を取得する（1 未満・非有限は 1）。</summary>
    /// <returns>表示倍率。</returns>
    private double ResolveScale()
    {
        var scale = RenderScale;
        return double.IsFinite(scale) && scale > 0 ? scale : 1.0;
    }

    /// <summary>変更後の本文を書き戻し、通知する。</summary>
    /// <param name="updated">変更後の本文。</param>
    /// <returns>本文を変えた場合は <see langword="true"/>。</returns>
    private bool Commit(RichText updated)
    {
        if (ReferenceEquals(updated, Text))
        {
            AfterSelectionChanged();
            return false;
        }

        Text = updated;
        TextChanged?.Invoke(this, EventArgs.Empty);
        RestartCaretBlink();
        AfterSelectionChanged();
        return true;
    }

    /// <summary>選択範囲が変わったことを通知して描画を更新する。</summary>
    private void AfterSelectionChanged()
    {
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        Redraw();
    }

    /// <summary>キャレットを書記素クラスタの境界へ揃える。</summary>
    /// <param name="extend">選択を広げる場合は <see langword="true"/>。</param>
    private void SnapCaretToCluster(bool extend)
    {
        var text = Text.PlainText;
        var snapped = TextCursor.SnapBackward(text, _edit.CaretIndex);
        if (snapped != _edit.CaretIndex)
        {
            _edit.MoveTo(snapped, text.Length, extend);
        }
    }

    /// <summary>位置（このコントロールの DIP）を元画像ピクセルへ写す。</summary>
    /// <param name="point">位置（DIP）。</param>
    /// <returns>位置（元画像ピクセル）。</returns>
    private EsPoint ToSource(Windows.Foundation.Point point)
    {
        var scale = ResolveScale();
        return new EsPoint(point.X / scale, point.Y / scale);
    }

    /// <summary>入力を本文の改行の規則（<c>\n</c>）へ揃える。</summary>
    /// <param name="text">入力された文字列。</param>
    /// <returns>揃えた文字列。</returns>
    private static string NormalizeInput(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

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
        if (!IsInputEnabled || !IsEditing)
        {
            return;
        }

        var request = TextEditKeyMap.Resolve(ToTextEditKey(e.Key), CurrentModifiers());
        switch (request.Command)
        {
            case TextEditCommand.ToggleBold:
                e.Handled = TransformTextStyle(static style => style with { IsBold = !style.IsBold });
                return;
            case TextEditCommand.ToggleItalic:
                e.Handled = TransformTextStyle(static style => style with { IsItalic = !style.IsItalic });
                return;
            case TextEditCommand.ToggleUnderline:
                e.Handled = TransformTextStyle(static style => style with { IsUnderline = !style.IsUnderline });
                return;
            case TextEditCommand.ToggleStrike:
                e.Handled = TransformTextStyle(static style => style with { IsStrike = !style.IsStrike });
                return;
            case TextEditCommand.EndEditing:
                EndEditing();
                e.Handled = true;
                return;
            case TextEditCommand.InsertLineBreak:
                e.Handled = ReplaceSelection("\n");
                return;
            case TextEditCommand.DeleteBackward:
                e.Handled = DeleteAtCaret(forward: false);
                return;
            case TextEditCommand.DeleteForward:
                e.Handled = DeleteAtCaret(forward: true);
                return;
            case TextEditCommand.CaretLeft:
                MoveCaretByCluster(-1, request.Extend);
                e.Handled = true;
                return;
            case TextEditCommand.CaretRight:
                MoveCaretByCluster(1, request.Extend);
                e.Handled = true;
                return;
            case TextEditCommand.CaretUp:
                MoveCaretByLine(-1, request.Extend);
                e.Handled = true;
                return;
            case TextEditCommand.CaretDown:
                MoveCaretByLine(1, request.Extend);
                e.Handled = true;
                return;
            case TextEditCommand.CaretLineStart:
                MoveCaretToLineEdge(first: true, request.Extend);
                e.Handled = true;
                return;
            case TextEditCommand.CaretLineEnd:
                MoveCaretToLineEdge(first: false, request.Extend);
                e.Handled = true;
                return;
            case TextEditCommand.SelectAll:
                SelectAll();
                e.Handled = true;
                return;
            case TextEditCommand.Copy:
                _ = CopyAsync();
                e.Handled = true;
                return;
            case TextEditCommand.Cut:
                _ = CutAsync();
                e.Handled = true;
                return;
            case TextEditCommand.Paste:
                _ = PasteAsync();
                e.Handled = true;
                return;
        }
    }

    /// <summary>文字入力を処理する。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">入力の引数。</param>
    private void OnEditorCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs e)
    {
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
        if (IsInputEnabled && IsEditing && e.Pointer.IsInContact)
        {
            SetCaretFromPoint(ToSource(e.GetCurrentPoint(this).Position), extend: true);
        }
    }

    /// <summary>ポインターが離されたときの処理。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">ポインターの引数。</param>
    private void OnEditorPointerReleased(object sender, PointerRoutedEventArgs e)
        => ReleasePointerCapture(e.Pointer);

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
        VirtualKey.Number5 => TextEditKey.Digit5,
        (VirtualKey)0x65 => TextEditKey.Digit5,
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
