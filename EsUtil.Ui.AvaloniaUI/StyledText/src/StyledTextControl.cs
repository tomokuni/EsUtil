using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.AvaloniaUI.StyledText;

/// <summary>本文を表示し、その場で編集するコントロール（表示と編集を 1 つの部品にまとめる）。</summary>
/// <remarks>
/// <b>表示と編集を分けない</b>: 入力欄を重ねる方法では、書式（run 単位の色・太字）が反映されず、
/// 書式バーを操作するたびにフォーカスと選択が外れる。 本コントロールは本文を自分で描き、キー入力のたびに
/// 本文を書き換える（書式はその場で反映される）。<br/>
/// <b>内容は <see cref="TextEditor"/> が単一所有する</b>: 編集中も <see cref="TextEditor.Text"/> が真の値であり、<see cref="RichTextValue"/> へ写して変更のたびに
/// <see cref="RichTextChanged"/> を発生させる（ホストは文書へ写すだけでよい）。<br/>
/// <b>規則は提示層が単一所有する</b>: 本文の値とキャレット・選択・書式の適用は <see cref="TextEditor"/>（状態は <see cref="TextEditState"/>）、
/// キーの割り当ては <see cref="TextEditKeyMap"/>、折り返しと行の配置は <see cref="TextLayout"/> が決める。
/// 本コントロールはフレームワーク固有の描画・入力・IME・クリップボードの配線だけを行う。<br/>
/// <b>座標は元画像ピクセル</b>である: <see cref="SourceToClientScaling"/> で画面の DIP へ写すため、拡大しても
/// 保存画像と同じ比率で文字が並ぶ。
/// </remarks>
public sealed partial class StyledTextControl : Control, Avalonia.Rendering.ICustomHitTest
{
    /// <summary>本文の編集と表示を担うコントロールを初期化する。</summary>
    public StyledTextControl()
    {
        // **入力を受けるにはフォーカスが要る**: Avalonia の <see cref="Control"/> は既定でフォーカスを
        // 持てず、キーと IME の宛先にならない（Focus() が失敗し、文字を入力できない）。
        Focusable = true;
        IsTabStop = true;

        // **IME の窓口を自分で出す**: 入力欄を重ねないため、変換候補の位置と変換中の文字列は
        // 本コントロールが IME へ答える（要求に応じてクライアントを返す）。
        TextInputMethodClientRequested += (_, e) => e.Client = InputClient;
    }

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

    /// <summary>本文の範囲を押下の対象にする。</summary>
    /// <param name="point">コントロールの座標。</param>
    /// <returns>押下を受け付ける場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>自前で描く面は押下の対象にならない</b>: Avalonia は背景を持つ面だけをヒットテストするため、
    /// 何も描いていない範囲を押しても本コントロールへ届かない（描いた文字の上でしか反応しない）。
    /// 範囲全体を押下の対象にして、本文の余白を押しても編集を始められるようにする。
    /// </remarks>
    public bool HitTest(Point point)
        => IsVisible && IsHitTestVisible && new Rect(Bounds.Size).Contains(point);
    /// <summary>表示する本文。</summary>
    public static readonly StyledProperty<RichText> RichTextValueProperty =
        AvaloniaProperty.Register<StyledTextControl, RichText>(
            nameof(RichTextValue), RichText.Empty, inherits: false);

    /// <summary>画面の DIP への倍率（1 以上で拡大）。</summary>
    public static readonly StyledProperty<double> SourceToClientScalingProperty =
        AvaloniaProperty.Register<StyledTextControl, double>(nameof(SourceToClientScaling), 1.0);

    /// <summary>行間の倍率。</summary>
    public static readonly StyledProperty<double> LineHeightFactorProperty =
        AvaloniaProperty.Register<StyledTextControl, double>(nameof(LineHeightFactor), 1.0);

    /// <summary>本文を編集中かどうか。</summary>
    public static readonly DirectProperty<StyledTextControl, bool> IsEditingProperty =
        AvaloniaProperty.RegisterDirect<StyledTextControl, bool>(
            nameof(IsEditing), control => control.IsEditing);

    /// <summary>表示する本文を取得または設定する。</summary>
    /// <value>表示・編集する本文。 <see langword="null"/> は空の本文として扱う。</value>
    public RichText RichTextValue
    {
        get => GetValue(RichTextValueProperty);
        set => SetValue(RichTextValueProperty, value ?? RichText.Empty);
    }

    /// <summary>画面の DIP への倍率を取得または設定する。</summary>
    /// <value>元画像ピクセルから画面の DIP への倍率。 0 以下・非有限は 1 として扱う。</value>
    public double SourceToClientScaling
    {
        get => GetValue(SourceToClientScalingProperty);
        set => SetValue(SourceToClientScalingProperty, value);
    }

    /// <summary>行間の倍率を取得または設定する。</summary>
    /// <value>行間の倍率。 0 以下・非有限は 1 として扱う。</value>
    public double LineHeightFactor
    {
        get => GetValue(LineHeightFactorProperty);
        set => SetValue(LineHeightFactorProperty, value);
    }

    /// <summary>本文を編集中かどうかを取得する。</summary>
    public bool IsEditing => _editor.IsEditing;

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
    /// 本コントロールが行う（<see cref="FormatBar"/>）。 本値が決めるのは画面要素をどこへ置くかだけである。<br/>
    /// <b><see cref="TextFormatBarHosting.Popup"/> は置く場所を用意しなくてよい</b>: 選択範囲の近くへ
    /// ポップアップで出す（位置は <see cref="FloatingPanelPlacement"/> が単一所有する）。
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
    /// <remarks>
    /// <b>ホストは <c>FormatBar.View</c> を置くだけでよい</b>: 出す項目の反映・キーとフォーカスの転送・
    /// 内容の更新は本コントロールが行う（ホストが同じ配線を書き写さない）。
    /// </remarks>
    public IStyledTextFormatBar FormatBar => _formatBar ??= CreateFormatBar();

    /// <summary>書式バーをポップアップで表示中かどうかを取得する。</summary>
    /// <value>表示中の場合は <see langword="true"/>（<see cref="TextFormatBarHosting.Custom"/> のときは常に <see langword="false"/>）。</value>
    public bool IsFormatBarOpen => _formatBarHost?.IsOpen ?? false;

    /// <summary>利用者が動かした後の書式バーの左上（DIP）を取得する（テスト用）。</summary>
    internal EsPoint? FormatBarManualPosition => _formatBarHost?.ManualPosition;

    /// <summary>書式バーを載せているウィンドウを取得する（テストが実入力の経路を確かめるために使う）。</summary>
    internal Window? FormatBarPopupWindow => _formatBarHost?.Window;

    /// <summary>文字列と書式から実寸を測る関数を取得または設定する。</summary>
    /// <value>既定は本コントロールの描画 API で測る関数。 描画と同じエンジンで測るため通常は変更しない。</value>
    /// <remarks>
    /// <b>差し替えられるようにする</b>: 折り返しとキャレットの位置は実寸に依存するため、ホストが
    /// 「描画に使うのと同じ計測」を渡せる必要がある（テストでは固定の計測を渡して位置だけを検証する）。
    /// </remarks>
    public Func<string, TextStyle, EsUtil.Platform.Core.Geometry.EsSize> MeasureText { get; set; }
        = RichTextPainter.Measure;

    /// <summary>本文を書き換えたときに発生する。</summary>
    /// <remarks>ホストは文書（または設定）へ写す。 写した結果を <see cref="RichTextValue"/> へ戻さないこと（往復する）。</remarks>
    public event EventHandler? RichTextChanged;

    /// <summary>キャレットまたは選択範囲が変わったときに発生する。</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>編集の開始・終了で発生する。</summary>
    public event EventHandler? EditingChanged;

    /// <summary>描画に使う本文を取得する（変換中の文字列を含む）。</summary>
    /// <value>変換中でなければ <see cref="RichTextValue"/>。 変換中は変換中の文字列を入れた描画用の値。</value>
    /// <remarks>
    /// <b>変換中の文字列は本文へ入れない</b>: 表示だけに入れる（確定したときだけ本文が変わる）。
    /// 入れないと、入力中の文字が表示されず「入力が反映されていない」ように見える。
    /// </remarks>
    internal RichText DisplayText => _editor.DisplayText;

    /// <summary>本文の領域（元画像ピクセル）を取得する。</summary>
    /// <returns>本文の領域。 まだ配置されていない場合は 0。</returns>
    private EsSize ResolveBodySize()
    {
        var scale = ResolveScale();
        return new EsSize(Bounds.Width / scale, Bounds.Height / scale);
    }

    /// <summary>書式バーを作る（キーとフォーカスの転送を配線する）。</summary>
    /// <returns>作ったバー。</returns>
    private IStyledTextFormatBar CreateFormatBar()
    {
        var bar = _formatBarFactory.Create(
            new StyledTextFormatBarContext(this), _formatBarOptions with { Items = _formatBarItems });

        // **キー操作とフォーカスは本コントロールが持つ**: バーは操作を転送し、適用後に本文へ戻すだけにする
        bar.SetKeyHandler(TryHandleEditingKey);
        bar.SetFocusRequest(() => Focus());
        return bar;
    }

    /// <summary>書式バーを作り直す（出す項目・生成・構成の変更を反映する）。</summary>
    /// <remarks>まだ誰もバーを要求していない場合は作らない（使わないバーを生成しない）。</remarks>
    private void RebuildFormatBar()
    {
        if (_formatBar is null)
        {
            return;
        }

        _formatBar = CreateFormatBar();
    }

    /// <summary>書式バーの内容と、ポップアップの出し入れを、いまの状態に合わせて更新する。</summary>
    /// <remarks>
    /// <b>出す条件は <see cref="ShouldShowFormatBar"/> が単一所有する</b>: ホストが置く場合と同じ判断を使う
    /// （ポップアップだけ別の条件にすると UI 間で挙動が食い違う）。<br/>
    /// <b>内容の更新は本コントロールの仕事</b>: ホストが置く場合も、選択のたびにバーの表示を追随させる
    /// （ホストに <c>Refresh</c> を呼ばせると、呼び忘れで古い書式が表示される）。
    /// </remarks>
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

    /// <summary>ポップアップで出す書式バーを閉じる（要素から外れたとき）。</summary>
    private void CloseFormatBarHost()
    {
        _formatBarHost?.Close();
        _formatBarHost = null;
    }

    /// <summary>選択範囲の矩形（このコントロールの座標の DIP）を取得する。</summary>
    /// <value>選択範囲の外接矩形。 選択が無い・解決できない場合は <see cref="CaretRect"/>。</value>
    /// <remarks>ポップアップの位置を決めるのに使う（選択の上へ出せば編集中の文字に重ならない）。</remarks>
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

    /// <summary>本文を折り返して行へ配置する（表示と編集で同じ結果を使う）。</summary>
    /// <returns>行の並び（本文が空・大きさが 0 の場合は空）。</returns>
    private IReadOnlyList<TextLinePlan> ResolveLines()
        => _editor.ResolveLines(ResolveBodySize(), MeasureText, LineHeightFactor);

    /// <summary>行と文字位置の対応を作る。</summary>
    /// <returns>対応。 行が無い場合は <see langword="null"/>。</returns>
    /// <remarks>
    /// <b>表示と同じ本文で作る</b>: 変換中の文字列を入れないと、キャレットと変換候補の位置が
    /// 確定後の位置と食い違う（入力位置から遠い場所に候補が出る）。
    /// </remarks>
    private TextLineIndex? ResolveLineIndex()
        => _editor.ResolveLineIndex(ResolveBodySize(), MeasureText, LineHeightFactor);

    /// <summary>表示倍率を取得する（無効な値は基準値へ正規化する）。</summary>
    /// <returns>表示倍率。</returns>
    /// <remarks>正規化の規則は <see cref="EsUtil.Platform.Core.Coord.DpiScaler.NormalizeScaling"/> が単一所有する。</remarks>
    private double ResolveScale() => EsUtil.Platform.Core.Coord.DpiScaler.NormalizeScaling(SourceToClientScaling);

    /// <summary>本文・選択範囲・キャレットを描く。</summary>
    /// <param name="context">描画先。</param>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var scale = ResolveScale();
        using var transform = context.PushTransform(Matrix.CreateScale(scale, scale));

        // **変換中の文字列も本文として描く**（重ねて描くと既存の文字と二重になる）
        var lines = ResolveLines();
        foreach (var line in lines)
        {
            RichTextPainter.DrawLine(context, line);
        }

        if (!IsEditing)
        {
            return;
        }

        var index = TextLineIndex.Create(lines, DisplayText.PlainText);
        if (index is null)
        {
            return;
        }

        foreach (var rect in index.ResolveSelectionRects(SelectionStart, SelectionEnd, MeasureText))
        {
            context.FillRectangle(
                RichTextPainter.ToBrush(TextEditorAppearance.SelectionColor),
                new Rect(rect.X, rect.Y, rect.Width, rect.Height));
        }

        // **変換中はキャレットを変換中の文字列の末尾へ置く**: 末尾へ置かないと、変換中の文字が
        // キャレットの位置へ重なって見える。
        if (index.ResolveCaret(_editor.DisplayCaretIndex, MeasureText) is not { } caret || !IsCaretVisible)
        {
            return;
        }

        var thickness = TextEditorAppearance.CaretThicknessDip / scale;
        context.FillRectangle(
            RichTextPainter.ToBrush(TextEditorAppearance.CaretColor),
            new Rect(caret.X, caret.Y, thickness, caret.Height));
    }

    /// <summary>本文が変わったときに、描画と編集の状態を更新する。</summary>
    /// <param name="change">プロパティの変更内容。</param>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == RichTextValueProperty || change.Property == SourceToClientScalingProperty
            || change.Property == LineHeightFactorProperty)
        {
            // **本文が外から変わったら編集位置を本文の範囲へ収める**（差し替えで範囲外に残さない）
            if (change.Property == RichTextValueProperty)
            {
                _editor.Text = RichTextValue ?? RichText.Empty;
            }

            InvalidateVisual();

            // **ポップアップは選択位置に追随する**: 本文・倍率・行間が変わると選択の位置も変わる
            UpdateFormatBarHost();
        }
    }

    /// <summary>要素へ繋がったときに、ポップアップの出し直しを行う。</summary>
    /// <param name="e">引数。</param>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateFormatBarHost();
    }

    /// <summary>要素から外れたときに、ポップアップを閉じる（取り残さない）。</summary>
    /// <param name="e">引数。</param>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CloseFormatBarHost();
        base.OnDetachedFromVisualTree(e);
    }
}
