using Microsoft.Extensions.Logging;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EsUtil.Feature.Marquee;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Palette;
using EsUtil.Feature.Annotation.Core.Contract;
using EsUtil.Feature.Annotation.Core.Defaults;
using EsUtil.Feature.Annotation.Core.Geometry;
using EsUtil.Feature.Annotation.Core.History;
using EsUtil.Feature.Annotation.Core.Interaction;
using EsUtil.Feature.Annotation.Presenter.Drawing;
using EsUtil.Feature.Annotation.Presenter.Selection;
using EsUtil.Feature.Annotation.Logging;

namespace EsUtil.Ui.AvaloniaUI.Annotation;

/// <summary>画像の上へ注釈（枠線・吹き出し）を重ねるレイヤー コントロール（Avalonia UI 版）。</summary>
/// <remarks>
/// <b>役割</b>: 画像の表示・注釈の描画・入力の受付・外観と書式の編集を担う。 判断（当たり判定・折り返し・
/// 描画計画）は <c>EsUtil.Feature.Annotation</c> が単一所有し、本クラスは自 UI の描画 API へ写すだけである。<br/>
/// <b>選択枠と 8 方向のハンドルは Marquee の提示層を再利用する</b>（枠・ハンドル・nudge・カーソルを 4 つの UI 版で揃えるため）。
/// ハンドルの当たり判定も <b>同じ解決結果（<c>MarqueeDrawPlan</c>）と <c>HandleHitRadius</c> を使う</b>ため、
/// 「カーソルが変わる範囲」と「押して操作できる範囲」が一致する。<br/>
/// <b>本文の編集はその場で行う</b>: ダブルクリックまたは Enter で編集を始め、表示中のレイアウトのまま
/// キャレット・選択範囲を描いて入力する（<see cref="BeginTextEditing"/>）。 書式は <see cref="TextSelectionRange"/> と
/// <c>TransformTextStyle</c> で文字単位に適用し、入力中も反映される。<br/>
/// <b>外観と書式の編集は右クリックのフライアウト</b>（<see cref="ShowStyleEditor"/>）で行う。 サンプルは同じ編集 UI を
/// 設定ペインから使える（既定値の編集）。<br/>
/// <b>座標</b>: 注釈は元画像ピクセルで保持し、描画時は表示倍率で写す（画像へ焼き込まない）。
/// </remarks>
public sealed partial class AnnotationLayerControl : Control, Avalonia.Rendering.ICustomHitTest
{
    /// <summary>画像を取得または設定する。</summary>
    /// <value>背景に表示する画像。 null の場合は注釈だけを描く。</value>
    public static readonly StyledProperty<IImage?> SourceProperty =
        AvaloniaProperty.Register<AnnotationLayerControl, IImage?>(nameof(Source));

    /// <summary>画像を自分で描くかどうかを取得または設定する。</summary>
    /// <value>プレビューへ重ねるときは <see langword="false"/>（画像と影はプレビューが描く）。</value>
    public bool ShowImage { get; set; } = true;

    /// <summary>プレビュー内の画像の表示矩形（DIP）を取得または設定する。</summary>
    /// <value>プレビューが画像を表示している矩形。 未設定の場合は自分の領域いっぱいへ中央寄せで表示する。</value>
    /// <remarks><b>プレビューのズーム・パンへ追従する</b>: ホストがズームの変化のたびに設定する。</remarks>
    public EsRect? PreviewImageRect => ImageRectProvider?.Invoke() ?? _previewImageRect;

    /// <summary>画像の表示矩形を取り直す関数（ホストが設定する）。</summary>
    /// <remarks>
    /// <b>毎回ホストへ問い合わせる</b>: レイアウトの途中で取得した値は古くなる（レイアウト確定前に
    /// 取得すると、その後ウィンドウやツール バーの寸法が変わって画像の位置がずれる）。 値を持ち回すと
    /// 注釈だけが画像からずれるため（実測 2026-11-21）、使うたびに取り直す。
    /// </remarks>
    public Func<EsRect?>? ImageRectProvider { get; set; }

    /// <summary>画像の表示矩形を設定する（<see cref="ImageRectProvider"/> が無い場合だけ使う）。</summary>
    /// <param name="rect">矩形（このコントロールの座標）。</param>
    public void SetPreviewImageRect(EsRect? rect) => _previewImageRect = rect;

    /// <summary>ホストから渡された画像の表示矩形。</summary>
    private EsRect? _previewImageRect;
    /// <summary>元画像の幅（ピクセル）を取得または設定する。</summary>
    public static readonly StyledProperty<int> SourcePixelWidthProperty =
        AvaloniaProperty.Register<AnnotationLayerControl, int>(nameof(SourcePixelWidth), 1);

    /// <summary>元画像の高さ（ピクセル）を取得または設定する。</summary>
    public static readonly StyledProperty<int> SourcePixelHeightProperty =
        AvaloniaProperty.Register<AnnotationLayerControl, int>(nameof(SourcePixelHeight), 1);

    /// <summary>選択中の注釈の識別子を取得または設定する。</summary>
    public static readonly StyledProperty<string?> SelectedIdProperty =
        AvaloniaProperty.Register<AnnotationLayerControl, string?>(nameof(SelectedId));

    /// <summary>しっぽのハンドルの半径（DIP。 描画と当たり判定で同じ値を使う）。</summary>
    private const double TailHandleRadiusDip = AnnotationHitTest.TailHandleRadiusDefault;

    /// <summary>注釈の描画計画を解決する提示層。</summary>
    private readonly AnnotationPresenter _presenter = new();

    /// <summary>文書・保存形式・ログの入口（提示層を共有する）。</summary>
    private readonly AnnotationCore _core;

    /// <summary>選択枠・ハンドルを解決する Marquee の提示層。</summary>
    private readonly MarqueePresenter _marquee;

    /// <summary>押下中に固定した変換（null は固定していない）。 ドラッグ中に表示矩形が変わっても 1:1 で動かす。</summary>
    private AnnotationViewTransform? _frozenTransform;

    /// <summary>選択枠の外観（暗転・ラベル・マーカーは使わない）。</summary>
    private static readonly MarqueeAppearance s_marqueeAppearance = MarqueeAppearance.OutlineOnly;

    /// <summary>注釈の文書。</summary>
    private AnnotationDocument _document = new(1, 1);

    /// <summary>変更履歴（元に戻す・やり直す）。</summary>
    private readonly AnnotationHistory _history = new();

    /// <summary>直近に描いた選択枠のハンドル（DIP。 ポインターの形を決めるのに使う）。</summary>
    private IReadOnlyList<EsRect>? _handleDipRects;

    /// <summary>しっぽの先端をドラッグ中かどうか。</summary>
    private bool _isDraggingTail;

    /// <summary>Marquee からの通知で文書を更新している最中かどうか（再入防止）。</summary>
    private bool _isApplyingMarquee;

    /// <summary>最後に観測したポインター位置（元画像ピクセル。 nudge の起点に使う）。</summary>
    private EsPoint _lastPointerSource;

    /// <summary>外観・書式の編集フライアウト（開いている間だけ保持する）。</summary>
    /// <summary>注釈レイヤーを初期化する。</summary>
    public AnnotationLayerControl()
    {
        _core = new AnnotationCore(_presenter);
        Focusable = true;
        ClipToBounds = true;
        _marquee = new MarqueePresenter(new AnnotationMarqueeEffects(this))
        {
            Appearance = s_marqueeAppearance,
        };
        _marquee.SetCoordTransform(CoordinateTransform);
        _marquee.Subscribe(OnMarqueeNotice);

        // 本文をその場で編集するため、IME（日本語入力）の窓口を渡す。
        // フォーカスを得た時点で問い合わせが来るため、編集中かどうかで分けずに常に同じクライアントを返す
        // （編集の開始時に問い合わせ直せないため。 編集中でなければ変換は受け取らない）。
        TextInputMethodClientRequested += (_, e) => e.Client = TextInputClient;

        // 方向キーはフォーカス移動より先に受け取る（Avalonia は方向キーを XY ナビゲーションで処理済みにする）。
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>画像を取得または設定する。</summary>
    public IImage? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>元画像の幅（ピクセル）を取得または設定する。</summary>
    public int SourcePixelWidth
    {
        get => GetValue(SourcePixelWidthProperty);
        set => SetValue(SourcePixelWidthProperty, value);
    }

    /// <summary>元画像の高さ（ピクセル）を取得または設定する。</summary>
    public int SourcePixelHeight
    {
        get => GetValue(SourcePixelHeightProperty);
        set => SetValue(SourcePixelHeightProperty, value);
    }

    /// <summary>選択中の注釈の識別子を取得または設定する。</summary>
    public string? SelectedId
    {
        get => GetValue(SelectedIdProperty);
        set => SetValue(SelectedIdProperty, value);
    }

    /// <summary>注釈の文書を取得または設定する。</summary>
    /// <value>表示・編集する文書。 null は空の文書として扱う。</value>
    /// <remarks>差し替えると選択を解除する（別の画像の注釈を編集しないため）。</remarks>
    public AnnotationDocument Document
    {
        get => _document;
        set
        {
            if (IsEditingText)
            {
                EndTextEditing();
            }

            _document = value ?? new AnnotationDocument(SourcePixelWidth, SourcePixelHeight);
            _history.Clear();
            SelectedId = null;
            _edit.Bind(null);
            _marquee.ResetSelection();
            RaiseDocumentChanged();
            InvalidateVisual();
        }
    }

    /// <summary>新規作成時の既定値を取得または設定する。</summary>
    /// <value>枠線・吹き出し・本文の既定。 既定は <see cref="AnnotationDraftDefaults.Default"/>。</value>
    /// <remarks>
    /// <b>既存の注釈には影響しない</b>（各注釈が自分の外観・書式を持つ）。 設定ページやサンプルの設定ペインから変える。
    /// </remarks>
    public AnnotationDraftDefaults DraftDefaults { get; set; } = AnnotationDraftDefaults.Default;

    /// <summary>色の入力欄へ出す表記を取得または設定する。</summary>
    /// <value>色名 + 不透明度（既定）またはカラーコード（<c>#AARRGGBB</c>）。</value>
    /// <remarks>
    /// <b>保存値は表記に依らず <c>#AARRGGBB</c></b>である。 右クリックのフライアウトの色は本表記で表示する
    /// （書式バーの色は <c>AnnotationFormatBar.TextFormat</c> が同じ値を受ける）。
    /// </remarks>
    public ColorTextFormat ColorTextFormat { get; set; } = ColorTextFormat.NameWithAlpha;

    /// <summary>文書・保存形式・ログの入口（本コントロールが使う唯一のファサード）を取得する。</summary>
    /// <value>本コントロールとロガーを共有するファサード。</value>
    public AnnotationCore Core => _core;
    /// <summary>描画計画を解決する提示層を取得する。</summary>
    /// <value>本コントロールが使用する提示層（ロガーと接頭辞を共有する）。</value>
    public AnnotationPresenter Presenter => _presenter;
    /// <summary>ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。 null を設定すると出力しない。</value>
    /// <remarks>
    /// ログの定義はコアが単一所有する（<c>AnnotationLogMessage</c>）。 本コントロールは同じロガーを<br/>
    /// コアと提示層へ渡す（生成の記録・文書の読み書き・設定の変更が同じ出力先へ流れる）。
    /// </remarks>
    public ILogger Log
    {
        get => _core.Log;
        set => _core.Log = value;
    }

    /// <summary>ログの接頭辞を取得または設定する（ロガーと一緒に設定する）。</summary>
    /// <value>接頭辞（例: <c>[Annotation]</c>）。 null または空白を設定すると既定値へ戻す。</value>
    public string? LogPrefix
    {
        get => _core.LogPrefix;
        set => _core.LogPrefix = value;
    }
    /// <summary>行間の倍率を取得または設定する。</summary>
    /// <value>文字の実寸の高さに対する行の高さの比。 既定は 1.25。</value>
    public double LineHeightFactor
    {
        get => _presenter.LineHeightFactor;
        set
        {
            _presenter.LineHeightFactor = value;
            InvalidateVisual();
        }
    }

    /// <summary>本文の書式を適用する範囲（文字数）を取得または設定する。</summary>
    /// <value>開始位置と長さ（0 起点）。 長さ 0 は「全体」を表す。</value>
    /// <remarks>
    /// 本文の入力欄はホストが持つため、入力欄の選択範囲をホストがここへ写す。
    /// 右クリックのフライアウトと <see cref="ApplyTextStyle(AnnotationTextStyle)"/> は本値の範囲へ適用する。
    /// </remarks>
    public (int Start, int Length) TextSelectionRange
    {
        get => _edit.SelectionRange;
        set
        {
            _edit.Select(value.Start, value.Length, TextForInput.Length);
            SyncTextSelectionRange();
            InvalidateVisual();
        }
    }

    /// <summary>選択枠のハンドルを掴む半径（DIP）を取得または設定する。</summary>
    /// <value>ハンドルの中心からの距離。 既定は 12。</value>
    /// <remarks>
    /// <b>描画（カーソルの変化）と押下の当たり判定が同じ値を使う</b>（Marquee の <c>HandleHitRadius</c>）。
    /// 見えているハンドルの大きさは <see cref="HandleSize"/> で別に決める。
    /// </remarks>
    public double HandleHitRadius
    {
        get => _marquee.Options.HandleHitRadius;
        set
        {
            _marquee.Options = _marquee.Options with { HandleHitRadius = value };
            InvalidateVisual();
        }
    }

    /// <summary>選択枠のハンドルの一辺の大きさ（DIP）を取得または設定する。</summary>
    /// <value>既定は 10。</value>
    public double HandleSize
    {
        get => _marquee.Options.HandleSize;
        set
        {
            _marquee.Options = _marquee.Options with { HandleSize = value };
            InvalidateVisual();
        }
    }

    /// <summary>文書が変わったときに発生する。</summary>
    /// <remarks>保存（注釈ファイルの書き出し）はホストが行う（本クラスはファイルへ書かない）。</remarks>
    public event EventHandler? DocumentChanged;

    /// <summary>選択が変わったときに発生する。</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>注釈の無い場所の操作をプレビューへ渡すときに発生する。</summary>
    /// <remarks>
    /// <b>入力の所有者は本レイヤー</b>である: 注釈の無い場所の押下・ドラッグ・ホイールは、本イベントで
    /// ホストへ知らせ、ホストがプレビュー（ズーム・パン・自動切り出し）へ渡す。 入力を「どの要素が受け取るか」で
    /// 切り替えると、受け取らなかった側が次の操作を受け取れなくなる（実測 2026-11-20）。
    /// </remarks>
    public event EventHandler<AnnotationPreviewGestureEventArgs>? PreviewGestureRequested;

    /// <summary>注釈の無い場所が右クリックされたときに発生する（追加メニューを出す）。</summary>
    /// <remarks>メニューの見た目は UI ごとに異なるため、位置だけを知らせてホストが自分のメニューを出す。</remarks>
    public event EventHandler<AnnotationAddMenuRequestedEventArgs>? AddMenuRequested;

    /// <summary>外観・書式の編集パネルを要求する（ホストが右クリック位置へ表示する）。</summary>
    public event EventHandler<AnnotationStyleEditorRequestedEventArgs>? StyleEditorRequested;


    /// <summary>選択中の注釈を取得する。</summary>
    /// <value>選択が無い場合は <see langword="null"/>。</value>
    public AnnotationItem? Selected => SelectedId is null ? null : _document.Find(SelectedId);

    /// <summary>選択中の吹き出しの本文の書式（選択範囲の先頭の文字）を取得する。</summary>
    /// <value>選択が吹き出しでない場合は <see cref="AnnotationDefaults.TextStyle"/>。</value>
    /// <remarks>フライアウトの初期表示に使う。 選択が無い間は書式バーで選んだ書式（以降の入力へ効く値）を返す。</remarks>
    public AnnotationTextStyle SelectedTextStyle
    {
        get => _edit.ResolveDisplayStyle(_document);
        set
        {
            // 適用先の規則（選択が無ければ以降の入力へ回す）はコアが単一所有する
            _ = ApplyTextStyle(value);
        }
    }

    /// <summary>指定した位置を左上として枠線を追加する（右クリックのメニューから追加する）。</summary>
    /// <param name="sourcePoint">左上の位置（元画像ピクセル）。</param>
    /// <returns>追加した注釈の識別子。</returns>
    public string AddFrameAt(EsPoint sourcePoint) => AddFrame(ResolveDefaultBounds(sourcePoint));

    /// <summary>指定した位置（DIP）を左上として枠線を追加する。</summary>
    /// <param name="dip">左上の位置（ローカル DIP）。</param>
    /// <returns>追加した注釈の識別子。</returns>
    public string AddFrameAt(Point dip) => AddFrameAt(ToSource(dip));

    /// <summary>指定した位置を左上として吹き出しを追加する（右クリックのメニューから追加する）。</summary>
    /// <param name="sourcePoint">左上の位置（元画像ピクセル）。</param>
    /// <returns>追加した注釈の識別子。</returns>
    public string AddBubbleAt(EsPoint sourcePoint) => AddBubble(ResolveDefaultBounds(sourcePoint), string.Empty);

    /// <summary>指定した位置（DIP）を左上として吹き出しを追加する。</summary>
    /// <param name="dip">左上の位置（ローカル DIP）。</param>
    /// <returns>追加した注釈の識別子。</returns>
    public string AddBubbleAt(Point dip) => AddBubbleAt(ToSource(dip));

    /// <summary>追加する注釈の既定の矩形を求める（指定した位置を左上にし、画像の内側へ収める）。</summary>
    /// <param name="sourcePoint">左上の位置（元画像ピクセル）。</param>
    /// <returns>画像の内側へ収めた矩形。</returns>
    private EsRect ResolveDefaultBounds(EsPoint sourcePoint)
        => AnnotationGeometry.ResolveDraftBounds(sourcePoint, new EsSize(SourcePixelWidth, SourcePixelHeight));

    /// <summary>指定した位置（DIP）に注釈があるかどうかを判定する（プレビューの入力を受け取るかの判断に使う）。</summary>
    /// <param name="dip">位置（ローカル DIP）。</param>
    /// <returns>注釈・ハンドル・しっぽの近くにある場合は <see langword="true"/>。</returns>
    public bool HitTestAt(Point dip)
    {
        var source = ToSource(dip);
        if (AnnotationHitTest.HitTest(_document, source) is not null)
        {
            return true;
        }

        if (Selected is BubbleAnnotation bubble && IsNearTailTip(bubble, source))
        {
            return true;
        }

        return HitsSelectionHandle(dip);
    }

    /// <summary>選択と本文の編集を解除する（注釈の外をクリックしたとき）。</summary>
    public void ClearSelection()
    {
        EndTextEditing();
        Select(null);
    }
    /// <summary>枠線の注釈を追加する。</summary>
    /// <param name="bounds">矩形（元画像ピクセル）。</param>
    /// <returns>追加した注釈の識別子。</returns>
    /// <remarks>矩形はコア（<see cref="AnnotationCore.CreateFrame"/>）が画像の内側へ収める。</remarks>
    public string AddFrame(EsRect bounds)
    {
        PrepareUndo();
        var frame = _core.CreateFrame(_document, bounds, DraftDefaults);
        _document = _document.Add(frame);
        RaiseDocumentChanged();
        Select(frame.Id);
        return frame.Id;
    }

    /// <summary>吹き出しの注釈を追加する。</summary>
    /// <param name="bounds">本体の矩形（元画像ピクセル）。</param>
    /// <param name="text">本文。</param>
    /// <returns>追加した注釈の識別子。</returns>
    /// <remarks>
    /// 矩形もしっぽの先端もコア（<see cref="AnnotationCore.CreateBubble"/>）が画像の内側へ収める
    /// （しっぽが画像の外へ出ると掴めなくなる）。
    /// </remarks>
    public string AddBubble(EsRect bounds, string text)
    {
        PrepareUndo();
        var defaults = DraftDefaults;
        var bubble = _core.CreateBubble(
            _document, bounds, RichText.Create(text, defaults.TextStyle), defaults);

        _document = _document.Add(bubble);
        RaiseDocumentChanged();
        Select(bubble.Id);
        return bubble.Id;
    }

    /// <summary>選択中の注釈を削除する。</summary>
    /// <returns>削除した場合は <see langword="true"/>。</returns>
    public bool DeleteSelected()
    {
        if (SelectedId is not { } id)
        {
            return false;
        }

        PrepareUndo();
        _document = _document.Remove(id);
        Select(null);
        RaiseDocumentChanged();
        return true;
    }

    /// <summary>選択中の吹き出しの本文を置き換える。</summary>
    /// <param name="text">新しい本文（明示改行は <c>\n</c>）。</param>
    /// <returns>置き換えた場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>書式は文字位置で引き継ぐ</b>: 先頭から同じ長さの範囲は元の run の書式を保ち、増えた部分は先頭 run の書式にする
    /// （編集のたびにすべての書式が消えないようにする）。
    /// </remarks>
    public bool SetSelectedText(string text)
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return false;
        }

        // 同じ文字列なら何もしない（履歴に同じ内容の手が増えないようにする）
        if (string.Equals(bubble.Text.PlainText, text, StringComparison.Ordinal))
        {
            return false;
        }

        PrepareUndo();
        _document = _document.WithText(bubble.Id, bubble.Text.WithPlainText(text));
        RaiseDocumentChanged();
        InvalidateVisual();
        return true;
    }

    /// <summary>選択中の吹き出しの本文へ書式を適用する（<see cref="TextSelectionRange"/> の範囲）。</summary>
    /// <param name="style">適用する書式。</param>
    /// <returns>選択範囲へ適用した場合は <see langword="true"/>。 選択が無く以降の入力へ回した場合は <see langword="false"/>。</returns>
    /// <remarks>
    /// 長さ 0 は「全体」へ適用する。 文字単位の書式は run の分割で表す（コアが単一所有する）。
    /// <b>選択が無ければ以降の入力へ回す</b>: 既存の文字は変えず、次に入力する文字へ適用する。
    /// </remarks>
    public bool ApplyTextStyle(AnnotationTextStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);

        PrepareUndo();
        var next = _edit.ApplyStyle(_document, style);
        if (ReferenceEquals(next, _document))
        {
            _history.CancelChange();
            return false;
        }

        _document = next;
        _core.Log.LogTextStyleApplied(TextSelectionRange.Start, TextSelectionRange.Length);
        RaiseDocumentChanged();
        InvalidateVisual();
        return true;
    }

    /// <summary>選択中の吹き出しの本文へ、指定した範囲の書式を適用する。</summary>
    /// <param name="start">開始位置（文字数。 0 起点）。</param>
    /// <param name="length">長さ（文字数）。 0 以下は本文全体。</param>
    /// <param name="style">適用する書式。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>範囲は本文の文字数で数える</b>（入力欄の選択位置をそのまま渡せる）。 範囲の解決と run の分割は
    /// コアの <c>RichText.ApplyStyle</c> が単一所有する。
    /// </remarks>
    public bool ApplyTextStyle(int start, int length, AnnotationTextStyle style)
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return false;
        }

        PrepareUndo();
        var next = AnnotationSelectionPresenter.ApplyTextStyle(
            _document, bubble.Id, (start, length), style);
        if (next is null)
        {
            _history.CancelChange();
            return false;
        }

        _document = next;
        _core.Log.LogTextStyleApplied(start, length);
        RaiseDocumentChanged();
        InvalidateVisual();
        return true;
    }

    /// <summary>選択中の吹き出しの本文の書式を 1 文字ずつ変換する（変えない属性を保つ）。</summary>
    /// <param name="transform">現在の書式を受け取って新しい書式を返す関数（run ごとに呼ぶ）。</param>
    /// <returns>選択範囲へ適用した場合は <see langword="true"/>。 選択が無く以降の入力へ回した場合は <see langword="false"/>。</returns>
    /// <remarks>
    /// <b>「太字にする」「色だけ変える」は本メソッドを使う</b>: <see cref="ApplyTextStyle(AnnotationTextStyle)"/> は
    /// 範囲の書式を丸ごと差し替えるため、指定しなかった属性（色・大きさなど）が既定へ戻る。
    /// 切り替えの判断（太字にする／戻す）は呼び出し側で 1 回だけ行い、その値を transform で書く。<br/>
    /// <b>選択が無ければ以降の入力へ回す</b>: 変換の元になる書式は <see cref="SelectedTextStyle"/> である。
    /// </remarks>
    public bool TransformTextStyle(Func<AnnotationTextStyle, AnnotationTextStyle> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        PrepareUndo();
        var next = _edit.TransformStyle(_document, transform);
        if (ReferenceEquals(next, _document))
        {
            _history.CancelChange();
            return false;
        }

        _document = next;
        _core.Log.LogTextStyleApplied(TextSelectionRange.Start, TextSelectionRange.Length);
        RaiseDocumentChanged();
        InvalidateVisual();
        return true;
    }

    /// <summary>選択中の吹き出しの本文の、指定した範囲の書式を 1 文字ずつ変換する。</summary>
    /// <param name="start">開始位置（文字数。 0 起点）。</param>
    /// <param name="length">長さ（文字数）。 0 以下は本文全体。</param>
    /// <param name="transform">現在の書式を受け取って新しい書式を返す関数（run ごとに呼ぶ）。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    public bool TransformTextStyle(int start, int length, Func<AnnotationTextStyle, AnnotationTextStyle> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        if (Selected is not BubbleAnnotation bubble)
        {
            return false;
        }

        PrepareUndo();
        var next = _edit.TransformStyleRange(_document, start, length, transform);
        if (ReferenceEquals(next, _document))
        {
            _history.CancelChange();
            return false;
        }

        _document = next;
        _core.Log.LogTextStyleApplied(start, length);
        RaiseDocumentChanged();
        InvalidateVisual();
        return true;
    }

    /// <summary>選択中の注釈の外観（枠線・内側・角丸）を取得または設定する。</summary>
    /// <value>外観。 何も選んでいない場合は `null`。 設定するとその注釈の外観を差し替える。</value>
    /// <remarks>値の解決と適用の規則はコア（`AnnotationSelectionPresenter`）が単一所有する。</remarks>
    public AnnotationAppearance? SelectedAppearance
    {
        get => AnnotationSelectionPresenter.ResolveAppearance(_document, SelectedId);
        set
        {
            if (value is { } appearance)
            {
                _ = UpdateSelectedAppearance(_ => appearance);
            }
        }
    }
    /// <summary>選択中の注釈の外観を差し替える。</summary>
    /// <param name="transform">現在の外観を受け取って新しい外観を返す関数。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    public bool UpdateSelectedAppearance(Func<AnnotationAppearance, AnnotationAppearance> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        if (Selected is not { } item)
        {
            return false;
        }

        PrepareUndo();
        var next = AnnotationSelectionPresenter.ApplyAppearance(_document, item.Id, transform(item.Appearance));
        if (next is null)
        {
            _history.CancelChange();
            return false;
        }

        _document = next;

        RaiseDocumentChanged();
        InvalidateVisual();
        return true;
    }

    /// <summary>選択中の吹き出しの本文の余白を取得または設定する（元画像ピクセル）。</summary>
    /// <value>余白。 吹き出しを選んでいない場合は <see langword="null"/>。 設定するとその吹き出しの余白を変える。</value>
    public double? SelectedBodyPadding
    {
        get => AnnotationSelectionPresenter.ResolveBubble(_document, SelectedId)?.BodyPadding;
        set
        {
            if (value is { } padding)
            {
                _ = SetSelectedBodyPadding(padding);
            }
        }
    }

    /// <summary>選択中の吹き出しのしっぽの幅を取得または設定する（元画像ピクセル）。</summary>
    /// <value>幅。 吹き出しを選んでいない場合は <see langword="null"/>。 設定するとその吹き出しのしっぽの幅を変える。</value>
    public double? SelectedTailWidth
    {
        get => AnnotationSelectionPresenter.ResolveBubble(_document, SelectedId)?.Tail.Width;
        set
        {
            if (value is { } width)
            {
                _ = UpdateSelectedTail(tail => tail with { Width = width });
            }
        }
    }

    /// <summary>選択中の吹き出しのしっぽの辺を取得または設定する。</summary>
    /// <value>辺。 吹き出しを選んでいない場合は <see langword="null"/>。 設定するとその吹き出しのしっぽの辺を変える。</value>
    public BubbleEdge? SelectedTailBaseEdge
    {
        get => AnnotationSelectionPresenter.ResolveBubble(_document, SelectedId)?.Tail.BaseEdge;
        set
        {
            if (value is { } edge)
            {
                _ = UpdateSelectedTail(tail => tail with { BaseEdge = edge });
            }
        }
    }

    /// <summary>選択中の吹き出しの本文の余白を差し替える。</summary>
    /// <param name="padding">本文の余白（元画像ピクセル）。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    public bool SetSelectedBodyPadding(double padding)
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return false;
        }

        PrepareUndo();
        var next = AnnotationSelectionPresenter.SetBodyPadding(_document, bubble.Id, padding);
        if (next is null)
        {
            _history.CancelChange();
            return false;
        }

        _document = next;

        RaiseDocumentChanged();
        InvalidateVisual();
        return true;
    }

    /// <summary>選択中の吹き出しのしっぽの太さ・辺を差し替える。</summary>
    /// <param name="transform">現在のしっぽを受け取って新しいしっぽを返す関数。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    public bool UpdateSelectedTail(Func<BubbleTail, BubbleTail> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        if (Selected is not BubbleAnnotation bubble)
        {
            return false;
        }

        PrepareUndo();
        var next = _document.WithTail(bubble.Id, transform(bubble.Tail));
        if (ReferenceEquals(next, _document))
        {
            _history.CancelChange();
            return false;
        }

        _document = next;

        RaiseDocumentChanged();
        InvalidateVisual();
        return true;
    }

    /// <summary>選択中の注釈を最前面へ移す。</summary>
    /// <returns>移した場合は <see langword="true"/>。</returns>
    public bool MoveSelectedToFront()
    {
        if (SelectedId is not { } id)
        {
            return false;
        }

        PrepareUndo();
        _document = _document.BringToFront(id);
        RaiseDocumentChanged();
        InvalidateVisual();
        return true;
    }

    /// <summary>選択中の注釈を最背面へ移す。</summary>
    /// <returns>移した場合は <see langword="true"/>。</returns>
    public bool MoveSelectedToBack()
    {
        if (SelectedId is not { } id)
        {
            return false;
        }

        PrepareUndo();
        _document = _document.SendToBack(id);
        RaiseDocumentChanged();
        InvalidateVisual();
        return true;
    }

    /// <summary>元に戻せるかどうかを取得する。</summary>
    /// <value>変更履歴に戻せる状態がある場合は <see langword="true"/>。</value>
    public bool CanUndo => _history.CanUndo;

    /// <summary>やり直せるかどうかを取得する。</summary>
    /// <value>元に戻した変更が残っている場合は <see langword="true"/>。</value>
    public bool CanRedo => _history.CanRedo;

    /// <summary>直前の変更を元に戻す。</summary>
    /// <returns>戻した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>選択は識別子で引き継ぐ</b>（戻した後も同じ注釈を選んだまま、続けて操作できるようにする）。
    /// 変更の単位は「追加・削除・移動・サイズ変更・しっぽの移動・外観・書式・重なり順」である
    /// （ドラッグは押下から離すまでで 1 手）。
    /// </remarks>
    public bool Undo() => ApplyHistory(_history.Undo(_document));

    /// <summary>元に戻した変更をやり直す。</summary>
    /// <returns>やり直した場合は <see langword="true"/>。</returns>
    public bool Redo() => ApplyHistory(_history.Redo(_document));

    /// <summary>履歴から取り出した文書を現在の文書にする（選択を引き継ぐ）。</summary>
    /// <param name="document">適用する文書（<see langword="null"/> は何もしない）。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    private bool ApplyHistory(AnnotationDocument? document)
    {
        _history.CancelChange();
        if (document is null)
        {
            return false;
        }

        var id = SelectedId;
        _document = document;
        ClampCaretToText();

        if (id is not null && _document.Find(id) is { } item)
        {
            SelectedId = id;
            _edit.Bind(id);
            SyncTextSelectionRange();
            if (IsEditingText)
            {
                // 編集中は選択枠を出さない（本文の見た目を優先する）
                _marquee.ResetSelection();
            }
            else
            {
                _marquee.SetSelection(item.Bounds);
            }
        }
        else
        {
            SelectedId = null;
            _edit.Bind(null);
            _marquee.ResetSelection();
        }

        // 履歴の操作自体は記録しない（RaiseDocumentChanged を経由しない）
        DocumentChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
        return true;
    }

    /// <summary>外観と書式の編集フライアウトを表示する（右クリック）。</summary>
    /// <remarks>選択中の注釈を編集する。 項目は枠線・吹き出しの外観、本文の書式、重なり順である。</remarks>
    public void ShowStyleEditor()
    {
        StyleEditorRequested?.Invoke(this, new AnnotationStyleEditorRequestedEventArgs(_styleEditorX, _styleEditorY));
    }

    /// <summary>選択中の吹き出しの本文の矩形（DIP）を取得する。</summary>
    /// <returns>本文の矩形（DIP）。 吹き出しを選んでいない場合は空の矩形。</returns>
    /// <remarks>ホストが入力欄を重ねる位置に使う（コントロールは子要素を持たない）。</remarks>
    public Rect GetSelectedBodyRect()
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return default;
        }

        var body = bubble.ResolveBodyRect();
        var topLeft = ToClientDip(body.TopLeft);
        var bottomRight = ToClientDip(body.BottomRight);
        return new Rect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
    }

    /// <summary>選択中の注釈の矩形（DIP）を取得する。</summary>
    /// <returns>矩形（DIP）。 選択していない場合は空。</returns>
    /// <remarks>
    /// <b>書式バーを注釈に被せないための対象矩形</b>である。 本文の矩形ではなく枠線を含む矩形を使う
    /// （本文で判定するとバーが枠線の上へ数ピクセル重なる。 実測 2026-11-22）。
    /// </remarks>
    public Rect GetSelectedBoundsRect()
    {
        if (Selected is not { } item)
        {
            return default;
        }

        var topLeft = ToClientDip(item.Bounds.TopLeft);
        var bottomRight = ToClientDip(item.Bounds.BottomRight);
        return new Rect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
    }

    /// <summary>しっぽのハンドルの中心（DIP）を取得する（描画と当たり判定で共有する）。</summary>
    /// <returns>中心（DIP）。 吹き出しを選んでいない場合は <see langword="null"/>。</returns>
    internal Point? GetTailHandleCenterDip()
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return null;
        }

        var tip = ToClientDip(bubble.Tail.Tip);
        return new Point(tip.X, tip.Y);
    }

    /// <summary>Marquee のカーソル形状を適用する。</summary>
    /// <param name="kind">カーソル種別。</param>
    internal void ApplyCursor(MarqueeCursorKind kind)
        => Cursor = new Cursor(AnnotationMarqueeEffects.ToStandardCursor(kind));

    /// <summary>ポインターのキャプチャを取得する（注釈の操作を始めるときだけ呼ぶ）。</summary>
    /// <param name="e">押下の引数。</param>
    /// <remarks>
    /// <b>注釈の無い場所ではキャプチャしない</b>: プレビューのパンはキャプチャ不要であり、
    /// キャプチャが残るとカーソルがウィンドウへ掴まれたままになる（実測 2026-11-20）。
    /// </remarks>
    private void CapturePointer(PointerPressedEventArgs e) => e.Pointer.Capture(this);

    /// <summary>Marquee の要求でポインター キャプチャを取得する（押下時に取得済みのため何もしない）。</summary>
    internal void CapturePointerForMarquee() => _ = this;

    /// <summary>文書が変わったことを通知する（フライアウトの編集結果もホストへ伝える）。</summary>
    internal void NotifyDocumentChanged()
    {
        RaiseDocumentChanged();
        InvalidateVisual();
    }

    /// <summary>コントロールの寸法が変わったら再描画する。</summary>
    /// <param name="e">サイズ変更の引数。</param>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        InvalidateVisual();
    }

    /// <summary>画像と注釈を元画像ピクセルの 1:1 で合成したビットマップを作る（保存用）。</summary>
    /// <returns>合成したビットマップ（<see cref="SourcePixelWidth"/> × <see cref="SourcePixelHeight"/>）。</returns>
    /// <remarks>
    /// <b>選択枠・ハンドル・キャレットは描かない</b>（編集中の一時的な表示であり、保存する絵ではない）。<br/>
    /// <b>座標は元画像ピクセル</b>である: 表示倍率を掛けず、注釈を 1:1 で描く（どの表示倍率でも同じ絵になる）。
    /// 呼び出し側は返したビットマップを破棄する。
    /// </remarks>
    public RenderTargetBitmap RenderComposedImage()
    {
        var width = Math.Max(1, SourcePixelWidth);
        var height = Math.Max(1, SourcePixelHeight);

        // DIP を 1:1 で描く（基準 DPI は共通基盤が単一所有する）
        var bitmap = new RenderTargetBitmap(
            new PixelSize(width, height),
            new Vector(
                EsUtil.Platform.Core.Coord.DpiScaler.StandardDpi,
                EsUtil.Platform.Core.Coord.DpiScaler.StandardDpi));
        using (var context = bitmap.CreateDrawingContext())
        {
            if (Source is { } image)
            {
                context.DrawImage(image, new Rect(0, 0, width, height));
            }

            if (_document.Items.Length > 0)
            {
                var plan = _presenter.ResolveDrawPlan(_document, MeasureText, selectedId: null);
                foreach (var shape in plan.Shapes)
                {
                    AnnotationPainter.DrawShape(context, shape);
                }
            }
        }

        return bitmap;
    }

    /// <summary>描画に使う文書を取得する（変換中の文字列を入れた複製を含む）。</summary>
    /// <value>変換中でなければ本文の文書。 変換中は変換中の文字列を入れた描画用の複製。</value>
    private AnnotationDocument RenderDocument
        => IsEditingText
            ? AnnotationCompositionPreview.Apply(_document, SelectedId, _edit.CaretIndex, _preeditText)
            : _document;
    /// <summary>描画する（画像 → 注釈 → 選択枠 → しっぽのハンドルの順）。</summary>
    /// <param name="context">描画先。</param>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var (scale, offset) = ResolveImageTransform();
        if (ShowImage && Source is { } image)
        {
            context.DrawImage(image, new Rect(offset.X, offset.Y, SourcePixelWidth * scale, SourcePixelHeight * scale));
        }

        if (_document.Items.Length > 0)
        {
            // **変換中の文字列も本文として描く**: キャレットの位置へ重ねて描くと既存の文字と重なり、
            // キャレットも動かないため「入力が反映されていない」ように見える（実測 2026-11-22）。
            // 文書は変えず、描画用の複製にだけ入れる（規則は提示層の AnnotationCompositionPreview が単一所有する）。
            var plan = _presenter.ResolveDrawPlan(RenderDocument, MeasureText, SelectedId);

            // 注釈は元画像座標で描く（枠線の太さ・文字の大きさを保存画像と同じ比率にする）
            using (context.PushTransform(
                Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offset.X, offset.Y)))
            {
                foreach (var shape in plan.Shapes)
                {
                    AnnotationPainter.DrawShape(context, shape);
                }

                // 本文の編集中は、選択範囲とキャレットを注釈と同じ座標系で描く（表示中のレイアウトのまま編集する）
                DrawTextEditing(context, scale);
            }
        }

        DrawSelection(context);

        // 本文の編集中はハンドルを出さない（動かせないため。 破線だけが「編集中の枠」を示す）
        if (!IsEditingText)
        {
            DrawTailHandle(context);
        }
    }

    /// <summary>注釈の無い場所でドラッグしている間かどうか（プレビューのパンへ渡す）。</summary>
    private bool _isPreviewDragging;

    /// <summary>外観フライアウトを出した位置（ローカル DIP）。</summary>
    private double _styleEditorX;

    /// <summary>外観フライアウトを出した位置（ローカル DIP）。</summary>
    private double _styleEditorY;

    /// <summary>この位置が入力の対象かどうかを判定する。</summary>
    /// <param name="point">位置（このコントロールの座標）。</param>
    /// <returns>領域内の場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>下地を描かない面でも入力を受ける</b>: 画像の上へ重ねるため背景を塗らないが、Avalonia の既定の
    /// ヒットテストは背景の無い面を対象にしない。 注釈レイヤーはプレビュー領域の入力を単一所有するため、
    /// 常に対象とする（実測 2026-11-20）。
    /// </remarks>
    public bool HitTest(Point point)
        => IsVisible && IsHitTestVisible && new Rect(Bounds.Size).Contains(point);
    /// <summary>ポインターが押されたときの処理（左: 選択・移動・サイズ変更。 右: 編集メニュー）。</summary>
    /// <param name="e">ポインターの引数。</param>
    /// <remarks>
    /// <b>注釈の無い場所も本レイヤーが受け取る</b>: 選択の解除と、プレビューへの操作の引き渡しを行う。
    /// 入力を要素の切り替えで振り分けると、切り替えた側が次の押下を受け取れない（実測 2026-11-20）。
    /// </remarks>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetCurrentPoint(this);
        _core.Log.LogPointerPressed(ToSource(point.Position), 1);

        // 押下をホストへ知らせる（表示中のメニューを外側の押下で閉じるために使う）
        RaisePreviewGesture(
            AnnotationPreviewGesture.Press, point.Position, 1, 0, e.KeyModifiers);

        if (point.Properties.IsRightButtonPressed)
        {
            HandleRightButton(point.Position);
            e.Handled = true;
            return;
        }

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus();

        // 押下中の座標変換を固定する（ドラッグ中に倍率が変わってもカーソルと 1:1 で動かす）
        FreezeTransform();

        // 押下から始まる操作（移動・サイズ変更・しっぽの移動）を 1 手として記録する。
        // 実際に文書が変わらなければ履歴へは積まれない（選択だけのクリックで手数が増えない）。
        PrepareUndo();

        var source = ToSource(point.Position);

        // 本文の編集中は、本文の内側の押下をキャレットの移動・選択に使う（外側なら編集を終える）
        if (IsEditingText)
        {
            if (Selected is BubbleAnnotation editingBubble && editingBubble.Bounds.Contains(source))
            {
                CapturePointer(e);
                _history.CancelChange();
                _isSelectingText = true;
                SetCaretFromPoint(source, (e.KeyModifiers & KeyModifiers.Shift) != 0);
                InvalidateVisual();
                return;
            }

            EndTextEditing();
        }

        if (Selected is not null)
        {
            if (Selected is BubbleAnnotation selectedBubble && IsNearTailTip(selectedBubble, source))
            {
                CapturePointer(e);
                _isDraggingTail = true;
                InvalidateVisual();
                return;
            }

            // ハンドルの当たり判定（カーソルが変わる範囲と同じ判定）を注釈の当たり判定より先に見る。
            // ここを飛ばすと、ハンドルの外周を押したときに「注釈の外側」と判定されて選択が解除される。
            if (HitsSelectionHandle(point.Position))
            {
                CapturePointer(e);
                _marquee.Press(source, e.ClickCount);
                InvalidateVisual();
                return;
            }
        }

        if (AnnotationHitTest.HitTest(_document, source) is { } hit)
        {
            if (!string.Equals(SelectedId, hit.Id, StringComparison.Ordinal))
            {
                Select(hit.Id);
            }

            if (e.ClickCount >= 2 && hit is BubbleAnnotation)
            {
                CapturePointer(e);
                BeginTextEditing();
                e.Handled = true;
                return;
            }

            CapturePointer(e);
            _marquee.Press(source, e.ClickCount);
        }
        else
        {
            Select(null);

            // 注釈の無い場所はプレビューの操作（パン）へ渡す（ダブルクリックは自動切り出しの確定に使う）
            if (e.ClickCount >= 2)
            {
                RaisePreviewGesture(
                    AnnotationPreviewGesture.DoubleClick, point.Position, e.ClickCount, 0, e.KeyModifiers);
                return;
            }

            _isPreviewDragging = true;
            RaisePreviewGesture(AnnotationPreviewGesture.DragStart, point.Position, 1, 0, e.KeyModifiers);
        }

        InvalidateVisual();
    }

    /// <summary>ポインターが動いたときの処理（ドラッグとカーソル形状）。</summary>
    /// <param name="e">ポインターの引数。</param>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var position = e.GetPosition(this);
        var source = ToSource(position);
        _lastPointerSource = source;

        // **ボタンを押していない移動では何も動かさない**: 捕捉の解放や Up の取りこぼしで
        // 「押していないのにドラッグ中」の状態が残ると、ホバーだけで枠線や注釈が動き続ける
        // （実測 2026-11-22: MewUI 版・WinUI 3 版で再現）。 押下中だけ動かす（4 つの UI 版で同じ規則）。
        var isPressed = e.GetCurrentPoint(this).Properties.IsLeftButtonPressed;

        // 注釈の無い場所を押したまま動かしたら、プレビューのパンへ渡す
        if (_isPreviewDragging)
        {
            RaisePreviewGesture(AnnotationPreviewGesture.DragMove, position, 1, 0, e.KeyModifiers);
            return;
        }

        // 注釈の上に無い間は、プレビューのホバー表示（座標・自動切り出しの強調）へ位置を渡す
        if (!HitTestAt(position))
        {
            RaisePreviewGesture(AnnotationPreviewGesture.Hover, position, 1, 0, e.KeyModifiers);
        }

        // 本文の編集中にドラッグしている間は、キャレットを伸ばして選択する
        if (IsEditingText && _isSelectingText)
        {
            if (!isPressed)
            {
                _isSelectingText = false;
                InvalidateVisual();
                return;
            }

            SetCaretFromPoint(source, extend: true);
            InvalidateVisual();
            return;
        }

        if (_isDraggingTail)
        {
            if (!isPressed)
            {
                _isDraggingTail = false;
                RaiseDocumentChanged();
                InvalidateVisual();
                return;
            }

            if (Selected is BubbleAnnotation bubble)
            {
                var next = _document.WithTailTip(bubble.Id, source);
                if (!ReferenceEquals(next, _document))
                {
                    _document = next;
                    RaiseDocumentChanged();
                    InvalidateVisual();
                }
            }

            return;
        }

        if (isPressed)
        {
            _marquee.Move(source);
        }
        else if (_marquee.CreateSnapshot(source).State is MarqueeInteractionState.Dragging
            or MarqueeInteractionState.DraggingNudging)
        {
            _marquee.Release(source);
        }

        // **ポインターの形で「何ができるか」を示す**（規則はコアの AnnotationPointerPolicy が単一所有する）。
        // 押下中は Marquee が操作に合った形を設定するため、ホバーのときだけ決める。
        if (!isPressed)
        {
            ApplyHoverCursor(source, position);
        }

        InvalidateVisual();
    }

    /// <summary>ホバー中のポインターの形を決めて適用する。</summary>
    /// <param name="source">位置（元画像ピクセル）。</param>
    /// <param name="position">位置（ローカル DIP）。</param>
    private void ApplyHoverCursor(EsPoint source, Point position)
    {
        var (scale, _) = ResolveImageTransform();
        var kind = AnnotationPointerPolicy.Resolve(
            _document,
            SelectedId,
            source,
            new EsPoint(position.X, position.Y),
            _handleDipRects,
            TailHandleRadiusDip / (scale > 0 ? scale : 1.0));

        Cursor = kind switch
        {
            AnnotationPointerKind.Move => new Cursor(StandardCursorType.SizeAll),
            AnnotationPointerKind.ResizeTop or AnnotationPointerKind.ResizeBottom
                => new Cursor(StandardCursorType.SizeNorthSouth),
            AnnotationPointerKind.ResizeLeft or AnnotationPointerKind.ResizeRight
                => new Cursor(StandardCursorType.SizeWestEast),
            AnnotationPointerKind.ResizeTopLeft => new Cursor(StandardCursorType.TopLeftCorner),
            AnnotationPointerKind.ResizeTopRight => new Cursor(StandardCursorType.TopRightCorner),
            AnnotationPointerKind.ResizeBottomLeft => new Cursor(StandardCursorType.BottomLeftCorner),
            AnnotationPointerKind.ResizeBottomRight => new Cursor(StandardCursorType.BottomRightCorner),
            AnnotationPointerKind.Tail => new Cursor(StandardCursorType.Hand),
            _ => new Cursor(StandardCursorType.Arrow),
        };
    }

    /// <summary>ポインターが離されたときの処理。</summary>
    /// <param name="e">ポインターの引数。</param>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        // 押下中に固定した変換を解除する
        ReleaseTransform();
        _core.Log.LogPointerReleased(ToSource(e.GetPosition(this)), SelectedId is not null);

        if (_isSelectingText)
        {
            _isSelectingText = false;
            e.Pointer.Capture(null);
            InvalidateVisual();
            return;
        }

        if (_isDraggingTail)
        {
            _isDraggingTail = false;
            return;
        }

        if (_isPreviewDragging)
        {
            _isPreviewDragging = false;
            RaisePreviewGesture(
                AnnotationPreviewGesture.DragEnd, e.GetPosition(this), 1, 0, e.KeyModifiers);
            e.Pointer.Capture(null);
            return;
        }

        _marquee.Release(ToSource(e.GetPosition(this)));
        e.Pointer.Capture(null);
        InvalidateVisual();
    }

    /// <summary>ホイールが回されたときの処理（ズーム・スクロールはプレビューへ渡す）。</summary>
    /// <param name="e">ホイールの引数。</param>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        RaisePreviewGesture(
            AnnotationPreviewGesture.Wheel, e.GetPosition(this), 1, (int)e.Delta.Y, e.KeyModifiers);
        e.Handled = true;
    }

    /// <summary>ポインターが領域から外れたときの処理（プレビューのホバー表示を消す）。</summary>
    /// <param name="e">ポインターの引数。</param>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);

        // 外へ出たら既定の形へ戻す（戻さないと移動の合図が残る）
        Cursor = new Cursor(StandardCursorType.Arrow);

        if (!_isPreviewDragging && !IsEditingText)
        {
            RaisePreviewGesture(AnnotationPreviewGesture.Leave, e.GetPosition(this), 1, 0, e.KeyModifiers);
        }
    }

    /// <summary>プレビューへ操作を渡す。</summary>
    /// <param name="gesture">操作の種類。</param>
    /// <param name="position">位置（ローカル DIP）。</param>
    /// <param name="clickCount">クリック回数。</param>
    /// <param name="wheelDelta">ホイールの移動量。</param>
    /// <param name="modifiers">修飾キー。</param>
    private void RaisePreviewGesture(
        AnnotationPreviewGesture gesture,
        Point position,
        int clickCount,
        int wheelDelta,
        KeyModifiers modifiers)
        => PreviewGestureRequested?.Invoke(
            this,
            new AnnotationPreviewGestureEventArgs(
                gesture,
                position.X,
                position.Y,
                clickCount,
                wheelDelta,
                (modifiers & KeyModifiers.Shift) != 0,
                (modifiers & KeyModifiers.Control) != 0));

    /// <summary>キーが押されたときの処理（Delete・Enter・方向キーの nudge）。</summary>
    /// <param name="e">キーの引数。</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (TryHandleKeyDown(e))
        {
            e.Handled = true;
        }
    }

    /// <summary>キー入力をこのコントロールの操作へ適用する。</summary>
    /// <param name="e">キーの引数。</param>
    /// <returns>処理した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>方向キーはトンネル段階でも渡す</b>: Avalonia は方向キーをフォーカス移動（XY ナビゲーション）へ使い、
    /// トンネル段階でイベントを処理済みにする。 そのままだと本文のキャレット移動・Shift+矢印の選択・
    /// 枠線の nudge が効かない（実測 2026-11-21: 修飾キーだけが届き、方向キーは届かなかった）。
    /// </remarks>
    private bool TryHandleKeyDown(KeyEventArgs e)
    {
        // 本文の編集中は、キー入力を本文の編集へ渡す（Delete で注釈を消さない）
        if (IsEditingText && HandleTextEditingKey(e))
        {
            return true;
        }

        // Ctrl+Z / Ctrl+Y（元に戻す・やり直す）。 本文の編集中はホストの入力欄が受け取る。
        if ((e.KeyModifiers & KeyModifiers.Control) != 0 && e.Key is Key.Z or Key.Y)
        {
            return e.Key == Key.Z ? Undo() : Redo();
        }

        if (e.Key == Key.Delete)
        {
            return DeleteSelected();
        }

        if (e.Key == Key.Enter && Selected is BubbleAnnotation)
        {
            BeginTextEditing();
            return true;
        }

        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            PrepareUndo();
            var handled = _marquee.KeyDown(ToEsKey(e.Key), _lastPointerSource);
            InvalidateVisual();
            return handled;
        }

        return false;
    }

    /// <summary>フォーカス移動より先に方向キーを受け取る（処理済みでも受け取る）。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">キーの引数。</param>
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        _ = sender;

        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End
            && TryHandleKeyDown(e))
        {
            e.Handled = true;
        }
    }

    /// <summary>右クリックを処理する（注釈の上なら書式を出す。 注釈の外なら追加メニューを要求する）。</summary>
    /// <param name="position">位置（DIP）。</param>
    private void HandleRightButton(Point position)
    {
        var source = ToSource(position);

        // 外観フライアウトは注釈（枠線・吹き出し）の上を右クリックしたときだけ出す。
        // 注釈の外は「枠線・吹き出しを追加」のメニューにする（選択も解除する）。
        // 追加できるのは画像の内側だけである（規則はコアが単一所有する）
        if (AnnotationHitTest.HitTest(_document, source) is not { } hit)
        {
            Select(null);
            if (AnnotationGeometry.Contains(source, _document.ImageSize))
            {
                AddMenuRequested?.Invoke(
                    this, new AnnotationAddMenuRequestedEventArgs(position.X, position.Y));
            }

            return;
        }

        if (!string.Equals(SelectedId, hit.Id, StringComparison.Ordinal))
        {
            Select(hit.Id);
        }

        _styleEditorX = position.X;
        _styleEditorY = position.Y;
        Focus();
        ShowStyleEditor();
    }

    /// <summary>選択中の注釈のいずれかのハンドル（8 方向）の当たり判定に含まれるかどうかを判定する。</summary>
    /// <param name="dip">位置（DIP）。</param>
    /// <returns>ハンドルの範囲内の場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>Marquee と同じ判定にする</b>: ハンドルの中心は <c>MarqueeDrawPlan.Handles</c> の矩形の中心、半径は
    /// <c>MarqueeOptions.HandleHitRadius</c>（DIP）を使う。 中心や半径を自前で計算すると、カーソルが変わる範囲と
    /// 押して操作できる範囲がずれる（実測: ハンドルの外周を押すと選択が解除される）。
    /// </remarks>
    private bool HitsSelectionHandle(Point dip)
    {
        var plan = _marquee.ResolveDrawPlan(
            new EsSize(Bounds.Width, Bounds.Height), AnnotationMarqueeMetrics.MeasureText);
        if (plan.Handles is not { } handles)
        {
            return false;
        }

        var radius = _marquee.Options.HandleHitRadius;
        var squared = radius * radius;
        for (var i = 0; i < MarqueeHandleRects.Count; i++)
        {
            var rect = handles.HandleDipRects[i];
            var centerX = rect.X + (rect.Width / 2);
            var centerY = rect.Y + (rect.Height / 2);
            var dx = centerX - dip.X;
            var dy = centerY - dip.Y;
            if ((dx * dx) + (dy * dy) <= squared)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>本文の実寸を測る（フォントの実寸に依存する部分だけを UI が担う）。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <returns>実寸（元画像ピクセル）。</returns>
    /// <remarks>
    /// <b>行の高さは斜体で変えない</b>: 幅（折り返しの判定）は指定どおりの書式で測り、高さだけは斜体を外した
    /// 書式で測る。 斜体は字を傾けるだけであり、行の高さを変えると 1 行ごとに間隔が変わる（実測 2026-11-18）。
    /// </remarks>
    private static EsSize MeasureText(string text, AnnotationTextStyle style)
    {
        var formatted = AnnotationPainter.CreateText(text, style);
        var lineHeight = AnnotationPainter.CreateLineMetrics(style).Height;
        return new EsSize(formatted.Width, lineHeight);
    }

    /// <summary>画像の表示倍率と位置を求める（プレビューの表示矩形に従う）。</summary>
    /// <returns>倍率と左上の位置（DIP）。</returns>
    /// <remarks>
    /// <b>倍率と位置はプレビューが単一所有する</b>: <see cref="PreviewImageRect"/>（プレビュー内の画像の表示矩形）を
    /// そのまま使う。 ズーム・パンで画像の位置が変わっても注釈の座標がずれない。<br/>
    /// 未設定（サンプルが単体で置く場合）だけ、自分の領域へ画像を収めた倍率で代用する。
    /// </remarks>
    private AnnotationViewTransform ResolveImageTransform()
        // 表示矩形の検証と変換の式はコアが単一所有する（4 つの UI 版で同じ結果にする）
        => AnnotationViewTransform.Resolve(
            new EsSize(Bounds.Width, Bounds.Height),
            new EsSize(SourcePixelWidth, SourcePixelHeight),
            PreviewImageRect is { Width: > 0, Height: > 0 } preview
                ? new EsRect(preview.X, preview.Y, preview.Width, preview.Height)
                : null);

    /// <summary>ローカル DIP を元画像ピクセルへ変換する。</summary>
    /// <param name="dip">ローカル DIP の座標。</param>
    /// <returns>元画像ピクセルの座標。</returns>
    private EsPoint ToSource(Point dip)
        => (_frozenTransform ?? ResolveImageTransform()).ToSource(new EsPoint(dip.X, dip.Y));

    /// <summary>押下中の座標変換を固定する（ドラッグ中に倍率が変わってもカーソルと 1:1 で動かす）。</summary>
    private void FreezeTransform() => _frozenTransform = ResolveImageTransform();

    /// <summary>固定した変換を解除する（ポインターを離したら呼ぶ）。</summary>
    private void ReleaseTransform() => _frozenTransform = null;

    /// <summary>元画像ピクセルをローカル DIP へ変換する。</summary>
    /// <param name="source">元画像ピクセルの座標。</param>
    /// <returns>ローカル DIP の座標。</returns>
    private EsPoint ToClientDip(EsPoint source) => ResolveImageTransform().ToClient(source);

    /// <summary>Marquee へ渡す座標変換ポートを生成する。</summary>
    /// <returns>双方向の変換ポート。</returns>
    private ICoordTransform CoordinateTransform => new CoordTransform(
        dip => ToSource(new Point(dip.X, dip.Y)),
        ToClientDip,
        () => new EsSize(SourcePixelWidth, SourcePixelHeight));

    /// <summary>注釈を選択する。</summary>
    /// <param name="id">選択する注釈の識別子（解除は null）。</param>
    private void Select(string? id)
    {
        // 別の注釈を選んだら本文の編集を終える（編集中の本文を見失わないようにする）
        if (IsEditingText && !string.Equals(SelectedId, id, StringComparison.Ordinal))
        {
            EndTextEditing();
        }

        SelectedId = id;
        _edit.Bind(id);
        SyncTextSelectionRange();
        if (id is null || _document.Find(id) is not { } item)
        {
            _marquee.ResetSelection();
        }
        else
        {
            _marquee.SetSelection(item.Bounds);
        }

        _core.Log.LogSelectionChanged(id);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    /// <summary>Marquee からの通知を文書へ反映する（移動・サイズ変更）。</summary>
    /// <param name="notice">通知。</param>
    private void OnMarqueeNotice(MarqueeNotice notice)
    {
        if (_isApplyingMarquee || SelectedId is null)
        {
            return;
        }

        if (notice.Kind is not (MarqueeNoticeKind.SelectionChanged or MarqueeNoticeKind.SelectionCompleted))
        {
            return;
        }

        var rect = notice.Snapshot.SelectionSourceRect;
        if (rect.IsEmpty || _document.Find(SelectedId) is not { } item)
        {
            return;
        }

        _isApplyingMarquee = true;
        try
        {
            // 画像の外へは出さない（収める規則はコアが単一所有する）
            var next = _document.WithBounds(item.Id, rect);
            if (!ReferenceEquals(next, _document))
            {
                _document = next;
                RaiseDocumentChanged();
            }
        }
        finally
        {
            _isApplyingMarquee = false;
        }

        InvalidateVisual();
    }

    /// <summary>選択枠（枠・8 方向のハンドル）を描く。</summary>
    /// <param name="context">描画先。</param>
    private void DrawSelection(DrawingContext context)
    {
        if (SelectedId is null)
        {
            return;
        }

        var plan = _marquee.ResolveDrawPlan(
            new EsSize(Bounds.Width, Bounds.Height), AnnotationMarqueeMetrics.MeasureText);

        // **本文を編集中は破線だけを描く**: ハンドルは動かせないため出さない。 代わりに「どの注釈を
        // 編集中か」を破線で示す（規則は提示層の AnnotationEditingOutline が単一所有する）。
        // 編集中はマーキーの選択を解除している（枠を出さない）ため、矩形は文書と表示倍率から求める。
        if (IsEditingText && Selected is { } editingItem)
        {
            var (editScale, editOffset) = ResolveImageTransform();
            DrawEditingOutline(
                context,
                AnnotationEditingOutline.Resolve(
                    AnnotationEditingOutline.ResolveSourceRect(editingItem),
                    editScale,
                    new EsPoint(editOffset.X, editOffset.Y)),
                _marquee.Appearance.BorderColor);
            return;
        }

        if (plan.Appearance.ShowBorder)
        {
            var brush = AnnotationPainter.ToBrush(plan.Appearance.BorderColor);
            foreach (var rect in plan.BorderDipRects)
            {
                context.FillRectangle(brush, new Rect(rect.X, rect.Y, rect.Width, rect.Height));
            }
        }

        if (plan.Handles is not { } handles)
        {
            return;
        }

        // ポインターの形を決めるのに使う（描いた値と同じ値を使い、判定と見た目をずらさない）
        var handleRects = new EsRect[MarqueeHandleRects.Count];
        for (var i = 0; i < handleRects.Length; i++)
        {
            handleRects[i] = handles.HandleDipRects[i];
        }

        _handleDipRects = handleRects;

        var fill = AnnotationPainter.ToBrush(plan.Appearance.HandleColor);
        var border = new Pen(AnnotationPainter.ToBrush(plan.Appearance.HandleBorderColor), handles.BorderThickness);
        for (var i = 0; i < MarqueeHandleRects.Count; i++)
        {
            var rect = handles.HandleDipRects[i];
            context.DrawRectangle(fill, border, new Rect(rect.X, rect.Y, rect.Width, rect.Height));
        }
    }

    /// <summary>本文を編集中の注釈を破線で囲む。</summary>
    /// <param name="context">描画先。</param>
    /// <param name="rect">破線を描く矩形（DIP）。</param>
    /// <param name="color">線の色。</param>
    /// <remarks>
    /// <b>見た目はコアが単一所有する</b>（<see cref="AnnotationEditingOutline"/>）: 移動できない状態で
    /// ハンドルを出すと「掴めるのに動かない枠」に見えるため、破線で「いま対象の枠」だけを示す。
    /// </remarks>
    private static void DrawEditingOutline(DrawingContext context, EsRect rect, System.Drawing.Color color)
    {
        var pen = new Pen(
            AnnotationPainter.ToBrush(color),
            AnnotationEditingOutline.Thickness,
            new DashStyle(AnnotationEditingOutline.DashPattern, 0));
        context.DrawRectangle(null, pen, new Rect(rect.X, rect.Y, rect.Width, rect.Height));
    }

    /// <summary>しっぽのハンドル（円）を描く。</summary>
    /// <param name="context">描画先。</param>
    /// <remarks>
    /// <b>当たり判定と同じ半径で描く</b>（<see cref="TailHandleRadiusDip"/>）。 表示倍率の影響を受けないよう、
    /// 注釈の座標変換の外（DIP）で描く。
    /// </remarks>
    private void DrawTailHandle(DrawingContext context)
    {
        if (GetTailHandleCenterDip() is not { } center)
        {
            return;
        }

        // 本体のハンドルと同じ外観にする（白の塗り + 灰色の枠。 色は Marquee の既定値を共有する）
        var size = HandleSize;
        var fill = AnnotationPainter.ToBrush(MarqueeAppearance.Default.HandleColor);
        var pen = new Pen(AnnotationPainter.ToBrush(MarqueeAppearance.Default.HandleBorderColor), 1);
        var rect = new Rect(center.X - (size / 2), center.Y - (size / 2), size, size);
        context.DrawRectangle(fill, pen, rect);
    }

    /// <summary>Avalonia のキーをコアのキーへ写す（nudge に使う 4 方向だけ）。</summary>
    /// <param name="key">Avalonia のキー。</param>
    /// <returns>コアのキー。 対象外は <see cref="EsUtil.Platform.Core.Input.EsKey.None"/>。</returns>
    private static EsUtil.Platform.Core.Input.EsKey ToEsKey(Key key) => key switch
    {
        Key.Left => EsUtil.Platform.Core.Input.EsKey.Left,
        Key.Right => EsUtil.Platform.Core.Input.EsKey.Right,
        Key.Up => EsUtil.Platform.Core.Input.EsKey.Up,
        Key.Down => EsUtil.Platform.Core.Input.EsKey.Down,
        _ => EsUtil.Platform.Core.Input.EsKey.None,
    };

    /// <summary>しっぽの先端の近くかどうかを判定する（描画したハンドルと同じ半径）。</summary>
    /// <param name="bubble">対象の吹き出し。</param>
    /// <param name="source">位置（元画像ピクセル）。</param>
    /// <returns>近くの場合は <see langword="true"/>。</returns>
    /// <remarks>半径は画面上で一定（DIP）にするため、表示倍率で割った値を対象座標の半径として使う。</remarks>
    private bool IsNearTailTip(BubbleAnnotation bubble, EsPoint source)
    {
        var (scale, _) = ResolveImageTransform();
        var radius = TailHandleRadiusDip / Math.Max(scale, 0.0001);
        var dx = bubble.Tail.Tip.X - source.X;
        var dy = bubble.Tail.Tip.Y - source.Y;
        return (dx * dx) + (dy * dy) <= radius * radius;
    }

    /// <summary>これから変更することを履歴へ準備する（変更の直前に呼ぶ）。</summary>
    /// <remarks>
    /// <b>1 操作 = 1 手</b>: 押しただけで動かなかった操作（選択だけのクリック）で元に戻す手数が増えない
    /// よう、控えと積みの判定は <see cref="AnnotationHistory.BeginChange"/> / <see cref="AnnotationHistory.CommitChange"/>
    /// が行う（本コントロールは呼ぶだけ）。
    /// </remarks>
    private void PrepareUndo() => _history.BeginChange(_document);

    /// <summary>文書が変わったことを通知する（履歴への積みは <see cref="AnnotationHistory.CommitChange"/> が行う）。</summary>
    private void RaiseDocumentChanged()
    {
        _history.CommitChange(_document);

        _core.Log.LogDocumentChanged(_document.Items.Length);
        DocumentChanged?.Invoke(this, EventArgs.Empty);
    }
}

