using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using EsUtil.Feature.Marquee;
using EsUtil.Feature.Annotation.Logging;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Palette;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using EsUtil.Feature.Annotation.Core.Contract;
using EsUtil.Feature.Annotation.Core.Defaults;
using EsUtil.Feature.Annotation.Core.Geometry;
using EsUtil.Feature.Annotation.Core.Interaction;
using EsUtil.Feature.Annotation.Presenter.Selection;
using EsUtil.Feature.StyledText;
using EsUtil.Feature.StyledText.Core.Text;
using EsUtil.Feature.StyledText.Core.Editing;

namespace EsUtil.Ui.WinUI3.Annotation;

/// <summary>画像の上へ注釈（枠線・吹き出し）を重ねるレイヤー コントロール（WinUI 3 版）。</summary>
/// <remarks>
/// <b>役割</b>: 画像の表示・注釈の描画・入力の受付を担う。 判断（当たり判定・折り返し・描画計画）は
/// <c>EsUtil.Feature.Annotation</c> が単一所有し、本クラスは XAML の図形と文字へ写すだけである。<br/>
/// <b>選択枠と 8 方向のハンドルは Marquee の提示層を再利用する</b>（枠・ハンドル・nudge を UI 版で揃えるため）。<br/>
/// <b>本文の編集はその場で行う</b>: 表示中のレイアウトのままキャレットと選択範囲を描いて入力する（入力欄を重ねない）。
/// 日本語入力（IME）の変換中文字列は文書へ入れず、キャレットの位置へ下線付きで描く。<br/>
/// <b>座標</b>: 注釈は元画像ピクセルで保持し、描画時は表示倍率で写す（画像へ焼き込まない）。
/// </remarks>
public sealed partial class AnnotationLayerControl : Grid
{
    /// <summary>しっぽのハンドルの半径（DIP。 描画と当たり判定で同じ値を使う）。</summary>
    private const double TailHandleRadiusDip = AnnotationHitTest.TailHandleRadiusDefault;

    /// <summary>注釈の描画計画を解決する提示層。</summary>
    private readonly AnnotationPresenter _presenter = new();

    /// <summary>文書・保存形式・ログの入口（提示層を共有する）。</summary>
    private readonly AnnotationCore _core;

    /// <summary>文書・選択・変更履歴・本文の編集状態を単一所有する編集セッション。</summary>
    private readonly AnnotationEditSession _session;

    /// <summary>選択枠・ハンドルを解決する Marquee の提示層。</summary>
    private readonly MarqueePresenter _marquee;

    /// <summary>画像を表示する面（XAML の画像。 注釈と同じ描画順に載せる）。</summary>
    private readonly Image _image = new()
    {
        IsHitTestVisible = false,
        Stretch = Microsoft.UI.Xaml.Media.Stretch.Fill,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
    };

    /// <summary>表示中の画像（差し替え時に作り直す）。</summary>
    private WriteableBitmap? _imageBitmap;
    /// <summary>画像を作り直した元（同じ画像なら作り直さない）。</summary>
    private EsReadOnlyBitmap? _imageSource;

    /// <summary>注釈を並べる層。</summary>
    private readonly Canvas _shapes = new() { IsHitTestVisible = false };

    /// <summary>選択枠とハンドルを並べる層。</summary>
    private readonly Canvas _selection = new() { IsHitTestVisible = false };

    /// <summary>入力の受け口（IME）を置く層。 レイアウトへ影響させないため専用の Canvas に載せる。</summary>

    /// <summary>押下中に固定した変換（null は固定していない）。 ドラッグ中に表示矩形が変わっても 1:1 で動かす。</summary>
    private AnnotationViewTransform? _frozenTransform;

    /// <summary>選択枠の外観（暗転・ラベル・マーカーは使わない）。</summary>
    private static readonly MarqueeAppearance s_marqueeAppearance = MarqueeAppearance.OutlineOnly;

    /// <summary>直近に描いた選択枠のハンドル（DIP。 ポインターの形を決めるのに使う）。</summary>
    private IReadOnlyList<EsRect>? _handleDipRects;

    /// <summary>しっぽの先端をドラッグ中かどうか。</summary>
    private bool _isDraggingTail;

    /// <summary>本文の選択範囲をマウスのドラッグで広げている最中かどうか。</summary>
    private bool _isSelectingText;

    /// <summary>Marquee からの通知で文書を更新している最中かどうか（再入防止）。</summary>
    private bool _isApplyingMarquee;

    /// <summary>最後に観測したポインター位置（元画像ピクセル。 nudge の起点に使う）。</summary>
    private EsPoint _lastPointerSource;

    /// <summary>注釈レイヤーを初期化する。</summary>
    public AnnotationLayerControl()
    {
        _core = new AnnotationCore(_presenter);
        _session = new AnnotationEditSession(new AnnotationDocument(1, 1), () => _core.Log);
        _session.Subscribe(OnSessionChanged);
        _marquee = new MarqueePresenter(new AnnotationMarqueeEffects(this))
        {
            Appearance = s_marqueeAppearance,
        };
        _marquee.SetCoordTransform(CoordinateTransform);
        _marquee.Subscribe(OnMarqueeNotice);

        IsTabStop = true;

        // XAML の Panel は下地が無いとヒットテストの対象にならない（押下が届かない）ため、透明で塗る
        Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);

        Children.Add(_image);
        Children.Add(_shapes);
        Children.Add(_selection);

        PointerPressed += OnPointerPressed;
        PointerWheelChanged += OnPointerWheelChanged;
        PointerExited += OnPointerExited;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        KeyDown += OnKeyDown;

        SizeChanged += (_, _) => InvalidateRender();
        Loaded += (_, _) => InvalidateRender();
    }

    /// <summary>外観・書式の編集パネルを要求する（ホストが表示する）。</summary>
    public event EventHandler<AnnotationStyleEditorRequestedEventArgs>? StyleEditorRequested;

    /// <summary>外観フライアウトを出した位置（ローカル DIP）。</summary>
    private double _styleEditorX;

    /// <summary>外観フライアウトを出した位置（ローカル DIP）。</summary>
    private double _styleEditorY;

    /// <summary>文書が変わったときに発生する。</summary>
    public event EventHandler? DocumentChanged;

    /// <summary>選択が変わったときに発生する。</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>注釈の無い場所の操作をプレビューへ渡すときに発生する。</summary>
    /// <remarks>
    /// <b>入力の所有者は本レイヤー</b>である: 注釈の無い場所の押下・ドラッグ・ホイールは、本イベントで
    /// ホストへ知らせ、ホストがプレビュー（ズーム・パン・自動切り出し）へ渡す。
    /// </remarks>
    public event EventHandler<AnnotationPreviewGestureEventArgs>? PreviewGestureRequested;

    /// <summary>注釈の無い場所が右クリックされたときに発生する（追加メニューを出す）。</summary>
    /// <remarks>メニューの見た目は UI ごとに異なるため、位置だけを知らせてホストが自分のメニューを出す。</remarks>
    public event EventHandler<AnnotationAddMenuRequestedEventArgs>? AddMenuRequested;

    /// <summary>表示する画像を取得または設定する。</summary>
    /// <value>背景に表示する画像。</value>
    /// <remarks>差し替えると表示用のビットマップを作り直す（作り直しは次の描画で行う）。</remarks>
    public EsReadOnlyBitmap? Source
    {
        get => _imageSource;
        set
        {
            if (ReferenceEquals(_imageSource, value))
            {
                return;
            }

            _imageSource = value;
            _imageBitmap = null;
            InvalidateRender();
        }
    }

    /// <summary>画像を自分で描くかどうかを取得または設定する。</summary>
    public bool ShowImage { get; set; } = true;

    /// <summary>プレビュー内の画像の表示矩形（DIP）を取得または設定する。</summary>
    public EsRect? PreviewImageRect
    {
        get => ImageRectProvider?.Invoke() ?? _previewImageRect;
        set => _previewImageRect = value;
    }

    /// <summary>画像の表示矩形を取り直す関数（ホストが設定する）。</summary>
    /// <remarks>
    /// <b>毎回ホストへ問い合わせる</b>: レイアウトの途中で取得した値は古くなる（レイアウト確定前に
    /// 取得すると、その後ウィンドウやツール バーの寸法が変わって画像の位置がずれる）。 値を持ち回すと
    /// 注釈だけが画像からずれるため、使うたびに取り直す。
    /// </remarks>
    public Func<EsRect?>? ImageRectProvider { get; set; }

    /// <summary>ホストから渡された画像の表示矩形。</summary>
    private EsRect? _previewImageRect;

    /// <summary>指定した位置（DIP）を左上として枠線を追加する。</summary>
    /// <param name="dip">左上の位置（ローカル DIP）。</param>
    /// <returns>追加した注釈の識別子。</returns>
    public string AddFrameAt(Windows.Foundation.Point dip) => AddFrame(ResolveDefaultBounds(ToSource(new EsPoint(dip.X, dip.Y))));

    /// <summary>指定した位置（DIP）を左上として吹き出しを追加する。</summary>
    /// <param name="dip">左上の位置（ローカル DIP）。</param>
    /// <returns>追加した注釈の識別子。</returns>
    public string AddBubbleAt(Windows.Foundation.Point dip) => AddBubble(ResolveDefaultBounds(ToSource(new EsPoint(dip.X, dip.Y))), string.Empty);

    /// <summary>追加する注釈の既定の矩形を求める。</summary>
    /// <param name="sourcePoint">左上の位置（元画像ピクセル）。</param>
    /// <returns>画像の内側へ収めた矩形。</returns>
    private EsRect ResolveDefaultBounds(EsPoint sourcePoint)
        => AnnotationGeometry.ResolveDraftBounds(sourcePoint, new EsSize(SourcePixelWidth, SourcePixelHeight));

    /// <summary>指定した位置（DIP）に注釈があるかどうかを判定する。</summary>
    /// <param name="dip">位置（ローカル DIP）。</param>
    /// <returns>注釈の近くにある場合は <see langword="true"/>。</returns>
    public bool HitTestAt(Windows.Foundation.Point dip) => ResolvePointerKind(dip) != AnnotationPointerKind.None;

    /// <summary>元画像の幅（ピクセル）を取得または設定する。</summary>
    public int SourcePixelWidth { get; set; } = 1;

    /// <summary>元画像の高さ（ピクセル）を取得または設定する。</summary>
    public int SourcePixelHeight { get; set; } = 1;

    /// <summary>選択中の注釈の識別子を取得または設定する。</summary>
    /// <remarks>設定すると本文の編集を終え、選択枠と書式の適用先を切り替える（規則は編集セッションが単一所有する）。</remarks>
    public string? SelectedId
    {
        get => _session.SelectedId;
        set => _session.Select(value);
    }

    /// <summary>新規作成時の既定値を取得または設定する。</summary>
    /// <remarks><b>既存の注釈には影響しない</b>（各注釈が自分の外観・書式を持つ）。</remarks>
    public AnnotationDraftDefaults DraftDefaults
    {
        get => _session.DraftDefaults;
        set => _session.DraftDefaults = value;
    }

    /// <summary>色の入力欄へ出す表記を取得または設定する。</summary>
    /// <value>色名 + 不透明度（既定）またはカラーコード（<c>#AARRGGBB</c>）。</value>
    public ColorTextFormat ColorTextFormat { get; set; } = ColorTextFormat.NameWithAlpha;

    /// <summary>本文の実寸を測る（描画に使うテキスト エンジンに揃える）。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <returns>実寸（元画像ピクセル）。</returns>
    /// <remarks>文字を置く要素と同じエンジンで測る（<c>StyledText.RichTextMetrics.Measure</c>）。</remarks>
    private static EsSize MeasureText(string text, TextStyle style) => StyledText.RichTextMetrics.Measure(text, style);

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
    public double LineHeightFactor
    {
        get => _presenter.LineHeightFactor;
        set
        {
            _presenter.LineHeightFactor = value;
            InvalidateRender();
        }
    }

    /// <summary>選択枠のハンドルの一辺の大きさ（DIP）を取得または設定する。</summary>
    public double HandleSize
    {
        get => _marquee.Options.HandleSize;
        set
        {
            _marquee.Options = _marquee.Options with { HandleSize = value };
            InvalidateRender();
        }
    }

    /// <summary>選択枠のハンドルを掴む半径（DIP）を取得または設定する。</summary>
    public double HandleHitRadius
    {
        get => _marquee.Options.HandleHitRadius;
        set => _marquee.Options = _marquee.Options with { HandleHitRadius = value };
    }

    /// <summary>注釈の文書を取得または設定する。</summary>
    /// <remarks>差し替えると選択を解除し、変更履歴を空にする（別の画像の注釈を引き継がない）。</remarks>
    public AnnotationDocument Document
    {
        get => _session.Document;
        set
        {
            _session.Document = value;
            InvalidateRender();
        }
    }

    /// <summary>選択中の注釈を取得する。</summary>
    public AnnotationItem? Selected => _session.Selected;

    /// <summary>選択中の本文の書式を取得する（選択範囲の先頭の書式）。</summary>
    /// <remarks>選択が無い間は、書式バーで選んだ書式（以降の入力へ効く値）を返す。</remarks>
    public TextStyle SelectedTextStyle => _session.SelectedTextStyle;

    /// <summary>元に戻すことができるかどうかを取得する。</summary>
    public bool CanUndo => _session.CanUndo;

    /// <summary>やり直すことができるかどうかを取得する。</summary>
    public bool CanRedo => _session.CanRedo;

    /// <summary>選択と本文の編集を解除する（注釈の外をクリックしたとき）。</summary>
    public void ClearSelection() => _session.Select(null);

    /// <summary>枠線を追加する。</summary>
    /// <param name="bounds">位置と大きさ（元画像ピクセル）。</param>
    /// <returns>追加した注釈の識別子。</returns>
    /// <remarks>矩形はコア（<see cref="AnnotationGeometry.CreateFrame"/>）が画像の内側へ収める。</remarks>
    public string AddFrame(EsRect bounds) => _session.AddFrame(bounds);

    /// <summary>吹き出しを追加する。</summary>
    /// <param name="bounds">位置と大きさ（元画像ピクセル）。</param>
    /// <param name="text">本文。</param>
    /// <returns>追加した注釈の識別子。</returns>
    /// <remarks>矩形もしっぽの先端もコア（<see cref="AnnotationGeometry.CreateBubble"/>）が画像の内側へ収める。</remarks>
    public string AddBubble(EsRect bounds, string text) => _session.AddBubble(bounds, text);

    /// <summary>選択中の注釈を削除する。</summary>
    /// <returns>削除した場合は <see langword="true"/>。</returns>
    public bool DeleteSelected() => _session.DeleteSelected();

    /// <summary>選択中の吹き出しの本文を置き換える。</summary>
    /// <param name="text">新しい本文。</param>
    /// <returns>置き換えた場合は <see langword="true"/>。</returns>
    public bool SetSelectedText(string text) => _session.SetSelectedText(text);

    /// <summary>選択中の本文へ書式を適用する（<see cref="SelectionStart"/> と <see cref="SelectionLength"/> の範囲）。</summary>
    /// <param name="style">書式。</param>
    /// <returns>選択範囲へ適用した場合は <see langword="true"/>。 選択が無く以降の入力へ回した場合は <see langword="false"/>。</returns>
    /// <remarks>
    /// <b>選択が無ければ以降の入力へ回す</b>: 既存の文字は変えず、次に入力する文字へ適用する
    /// （規則は <see cref="TextStyleEdit"/> と <see cref="TextEditState"/> が単一所有する）。
    /// </remarks>
    public bool ApplyTextStyle(TextStyle style) => _session.ApplyTextStyle(style);

    /// <summary>選択中の本文へ書式を適用する。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（0 は全体）。</param>
    /// <param name="style">書式。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    public bool ApplyTextStyle(int start, int length, TextStyle style)
        => _session.ApplyTextStyle(start, length, style);

    /// <summary>選択中の本文の書式を変換する（1 つの属性だけ変える）。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（0 は全体）。</param>
    /// <param name="transform">書式を変える処理。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    public bool TransformTextStyle(int start, int length, Func<TextStyle, TextStyle> transform)
        => _session.TransformTextStyle(start, length, transform);

    /// <summary>選択中の本文の書式を変換する（選択範囲。 選択が無ければ以降の入力へ回す）。</summary>
    /// <param name="transform">書式を変える処理。</param>
    /// <returns>選択範囲へ適用した場合は <see langword="true"/>。 選択が無く以降の入力へ回した場合は <see langword="false"/>。</returns>
    /// <remarks>変換の元になる書式は <see cref="SelectedTextStyle"/>（直前に入力した文字、または選んだ書式）である。</remarks>
    public bool TransformTextStyle(Func<TextStyle, TextStyle> transform)
        => _session.TransformTextStyle(transform);

    /// <summary>選択中の注釈の外観（枠線・内側・角丸）を取得または設定する。</summary>
    /// <value>外観。 何も選んでいない場合は `null`。 設定するとその注釈の外観を差し替える。</value>
    /// <remarks>値の解決と適用の規則はコア（`AnnotationSelectionPresenter`）が単一所有する。</remarks>
    public AnnotationAppearance? SelectedAppearance
    {
        get => AnnotationSelectionPresenter.ResolveAppearance(_session.Document, _session.SelectedId);
        set
        {
            if (value is { } appearance)
            {
                _ = TransformSelectedAppearance(_ => appearance);
            }
        }
    }
    /// <summary>選択中の注釈の外観を差し替える。</summary>
    /// <param name="transform">外観を変える処理。</param>
    /// <returns>変えた場合は <see langword="true"/>。</returns>
    public bool TransformSelectedAppearance(Func<AnnotationAppearance, AnnotationAppearance> transform)
        => _session.TransformSelectedAppearance(transform);

    /// <summary>選択中の吹き出しの本文の余白を取得または設定する（元画像ピクセル）。</summary>
    /// <value>余白。 吹き出しを選んでいない場合は ``null``。 設定するとその吹き出しの余白を変える。</value>
    /// <remarks>値の解決と補正はコアが単一所有する（<see cref="AnnotationSelectionPresenter"/> / <see cref="AnnotationLimits"/>）。</remarks>
    public double? SelectedBodyPadding
    {
        get => AnnotationSelectionPresenter.ResolveBubble(_session.Document, _session.SelectedId)?.BodyPadding;
        set
        {
            if (value is { } padding)
            {
                _ = SetSelectedBodyPadding(padding);
            }
        }
    }

    /// <summary>選択中の吹き出しのしっぽの幅を取得または設定する（元画像ピクセル）。</summary>
    /// <value>幅。 吹き出しを選んでいない場合は ``null``。 設定するとその吹き出しのしっぽの幅を変える。</value>
    public double? SelectedTailWidth
    {
        get => AnnotationSelectionPresenter.ResolveBubble(_session.Document, _session.SelectedId)?.Tail.Width;
        set
        {
            if (value is { } width)
            {
                _ = TransformSelectedTail(tail => tail with { Width = width });
            }
        }
    }

    /// <summary>選択中の吹き出しのしっぽの辺を取得または設定する。</summary>
    /// <value>辺。 吹き出しを選んでいない場合は ``null``。 設定するとその吹き出しのしっぽの辺を変える。</value>
    public BubbleEdge? SelectedTailBaseEdge
    {
        get => AnnotationSelectionPresenter.ResolveBubble(_session.Document, _session.SelectedId)?.Tail.BaseEdge;
        set
        {
            if (value is { } edge)
            {
                _ = TransformSelectedTail(tail => tail with { BaseEdge = edge });
            }
        }
    }
    /// <summary>選択中の吹き出しの本文の余白を設定する。</summary>
    /// <param name="padding">余白（元画像ピクセル）。</param>
    /// <returns>変えた場合は <see langword="true"/>。</returns>
    public bool SetSelectedBodyPadding(double padding) => _session.SetSelectedBodyPadding(padding);

    /// <summary>選択中の吹き出しのしっぽを差し替える。</summary>
    /// <param name="transform">しっぽを変える処理。</param>
    /// <returns>変えた場合は <see langword="true"/>。</returns>
    /// <remarks>先端はコア（<see cref="AnnotationDocument.WithTail"/>）が画像の内側へ収める。</remarks>
    public bool TransformSelectedTail(Func<BubbleTail, BubbleTail> transform) => _session.TransformSelectedTail(transform);

    /// <summary>選択中の注釈を最前面へ移す。</summary>
    /// <returns>移した場合は <see langword="true"/>。</returns>
    public bool BringSelectedToFront() => _session.BringSelectedToFront();

    /// <summary>選択中の注釈を最背面へ移す。</summary>
    /// <returns>移した場合は <see langword="true"/>。</returns>
    public bool SendSelectedToBack() => _session.SendSelectedToBack();

    /// <summary>変更を元に戻す。</summary>
    /// <returns>戻した場合は <see langword="true"/>。</returns>
    public bool Undo() => _session.Undo();

    /// <summary>変更をやり直す。</summary>
    /// <returns>やり直した場合は <see langword="true"/>。</returns>
    public bool Redo() => _session.Redo();

    /// <summary>外観・書式の編集パネルを表示する（右クリックと同じ）。</summary>
    public void ShowStyleEditor() => ShowStyleEditor(isFromPointer: false);

    /// <summary>外観・書式の編集パネルを表示する。</summary>
    /// <param name="isFromPointer">ポインターの押下から出した要求かどうか。</param>
    private void ShowStyleEditor(bool isFromPointer)
    {
        if (Selected is null)
        {
            return;
        }

        _ = Focus(FocusState.Programmatic);
        RaiseStyleEditorRequested(isFromPointer);
    }

    /// <summary>選択中の注釈の矩形（このコントロールの座標）を取得する。</summary>
    /// <returns>矩形。 選択していない場合は空。</returns>
    /// <remarks>
    /// <b>書式バーを注釈に被せないための対象矩形</b>である。 本文の矩形ではなく枠線を含む矩形を使う
    /// （本文で判定するとバーが枠線の上へ数ピクセル重なる）
    /// </remarks>
    public EsRect GetSelectedBoundsRect()
        => Selected is { } item ? ResolveImageTransform().ToClient(item.Bounds) : default;

    /// <summary>選択中の本文の矩形（DIP）を取得する。</summary>
    /// <returns>本文の矩形。 吹き出しを選んでいない場合は空の矩形。</returns>
    public EsRect GetSelectedBodyRect()
        => Selected is BubbleAnnotation bubble
            ? ResolveImageTransform().ToClient(bubble.ResolveBodyRect())
            : default;

    /// <summary>本文の描画を別のコントロールへ任せる吹き出しの識別子を取得または設定する。</summary>
    /// <value>本文編集コントロールが描く吹き出しの識別子。 無い場合は <see langword="null"/>。</value>
    /// <remarks>
    /// <b>本文の描画を 2 か所に持たない</b>: 本文編集コントロールを重ねている間は、本レイヤーは本文を描かない。
    /// </remarks>
    internal string? ExternalTextId
    {
        get => _externalTextId;
        set
        {
            if (string.Equals(_externalTextId, value, StringComparison.Ordinal))
            {
                return;
            }

            _externalTextId = value;
            InvalidateRender();
        }
    }

    /// <summary>本文の描画を別のコントロールへ任せる吹き出しの識別子（保持する値）。</summary>
    private string? _externalTextId;

    /// <summary>編集の状態と文書を所有する編集セッションを取得する（本文編集コントロールと共有する）。</summary>
    internal AnnotationEditSession Session => _session;

    /// <summary>画像の表示倍率（元画像ピクセル → DIP）を取得する。</summary>
    internal double SourceToClientScaling => ResolveImageTransform().Scale;

    /// <summary>元画像ピクセルの矩形を、このコントロールの座標（DIP）の矩形へ変換する。</summary>
    /// <param name="source">矩形（元画像ピクセル）。</param>
    /// <returns>矩形（このコントロールの座標の DIP）。</returns>
    internal Windows.Foundation.Rect GetClientRect(EsRect source)
    {
        var rect = ResolveImageTransform().ToClient(source);
        return new Windows.Foundation.Rect(rect.X, rect.Y, rect.Width, rect.Height);
    }

    /// <summary>画像と注釈を元画像ピクセルの 1:1 で合成した画像を作る（保存用）。</summary>
    /// <returns>合成した画像。 破棄は呼び出し側。</returns>
    /// <remarks>
    /// <b>選択枠・ハンドル・キャレットは描かない</b>（編集中の一時的な表示であり、保存する絵ではない）。<br/>
    /// <b>GDI+ で焼き込む</b>: XAML の要素は表示倍率の影響を受け、1:1 の画素を得られないため、
    /// 保存用の絵は元画像ピクセルの座標系で描く（形式は PNG）。<br/>
    /// <b>生成できない場合は例外</b>である（合成画像を書けなかったことを黙って握りつぶさない）。
    /// </remarks>
    /// <exception cref="InvalidOperationException">合成した画像を生成できなかった場合。</exception>
    public EsBitmap RenderComposedImage()
    {
        try
        {
            var width = Math.Max(1, SourcePixelWidth);
            var height = Math.Max(1, SourcePixelHeight);

            using var bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                if (Source is { } source)
                {
                    using var image = ToDrawingBitmap(source);
                    graphics.DrawImage(image, new System.Drawing.Rectangle(0, 0, width, height));
                }

                var plan = _presenter.ResolveDrawPlan(_session.Document, MeasureText, null);
                foreach (var shape in plan.Shapes)
                {
                    AnnotationComposer.DrawShape(graphics, shape, MeasureText);
                }
            }

            var data = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, width, height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                var pixels = new byte[data.Stride * height];
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                return EsBitmap.FromPixelCopy(width, height, pixels);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            Log.LogOperationError(ex, nameof(RenderComposedImage));
            throw;
        }
    }

    /// <summary>表示用の画像を GDI+ のビットマップへ写す。</summary>
    /// <param name="source">元画像。</param>
    /// <returns>写したビットマップ。 破棄は呼び出し側。</returns>
    private static System.Drawing.Bitmap ToDrawingBitmap(EsReadOnlyBitmap source)
    {
        var width = source.Width;
        var height = source.Height;
        var pixels = source.ReadOnlyPixels;
        var bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(
            new System.Drawing.Rectangle(0, 0, width, height),
            System.Drawing.Imaging.ImageLockMode.WriteOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            var bytes = pixels.ToArray();
            var stride = width * 4;
            for (var y = 0; y < height; y++)
            {
                Marshal.Copy(bytes, y * stride, data.Scan0 + (y * data.Stride), stride);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }

    /// <summary>編集セッションの変更をコントロールの状態とイベントへ反映する。</summary>
    /// <param name="change">変更の種類。</param>
    private void OnSessionChanged(AnnotationEditChange change)
    {
        if ((change & AnnotationEditChange.Editing) != 0)
        {
            if (!_session.IsEditingText)
            {
                _isSelectingText = false;
                InvalidateRender();
            }
            else
            {
                _marquee.ResetSelection();
            }

            RaiseEditingChanged();
        }

        if ((change & AnnotationEditChange.Selection) != 0)
        {
            if (_session.IsEditingText)
            {
                _marquee.ResetSelection();
            }
            else if (_session.Selected is { } item)
            {
                _marquee.SetSelection(item.Bounds);
            }
            else
            {
                _marquee.ResetSelection();
            }

            RaiseSelectionChanged();
        }

        if ((change & AnnotationEditChange.TextSelection) != 0)
        {
            RaiseTextSelectionChanged();
        }

        if ((change & AnnotationEditChange.Document) != 0)
        {
            RaiseDocumentChanged();
        }

        InvalidateRender();
    }

    /// <summary>画像の表示倍率と位置を求める（縦横比を保って中央に置く）。</summary>
    /// <returns>倍率と左上の位置（DIP）。</returns>
    /// <remarks>表示矩形の検証と変換の式は <see cref="AnnotationViewTransform"/> が単一所有する（UI ごとに書き写さない）。</remarks>
    internal AnnotationViewTransform ResolveImageTransform()
        => AnnotationViewTransform.Resolve(
            new EsSize(ActualWidth, ActualHeight),
            new EsSize(SourcePixelWidth, SourcePixelHeight),
            PreviewImageRect is { Width: > 0, Height: > 0 } preview ? preview : null);

    /// <summary>ローカル DIP を元画像ピクセルへ変換する。</summary>
    /// <param name="dip">ローカル DIP の座標。</param>
    /// <returns>元画像ピクセルの座標。</returns>
    internal EsPoint ToSource(EsPoint dip)
        => (_frozenTransform ?? ResolveImageTransform()).ToSource(dip);

    /// <summary>押下中の座標変換を固定する（ドラッグ中に倍率が変わってもカーソルと 1:1 で動かす）。</summary>
    private void FreezeTransform() => _frozenTransform = ResolveImageTransform();

    /// <summary>固定した変換を解除する（ポインターを離したら呼ぶ）。</summary>
    private void ReleaseTransform() => _frozenTransform = null;

    /// <summary>元画像ピクセルをローカル DIP へ変換する。</summary>
    /// <param name="source">元画像ピクセルの座標。</param>
    /// <returns>ローカル DIP の座標。</returns>
    internal EsPoint ToClientDip(EsPoint source) => ResolveImageTransform().ToClient(source);

    /// <summary>Marquee へ渡す座標変換ポートを生成する。</summary>
    private ICoordTransform CoordinateTransform => new CoordTransform(
        ToSource,
        ToClientDip,
        () => new EsSize(SourcePixelWidth, SourcePixelHeight));

    /// <summary><see cref="StyleEditorRequested"/> を発生させる。</summary>
    /// <param name="isFromPointer">ポインターの押下から出した要求かどうか。</param>
    private void RaiseStyleEditorRequested(bool isFromPointer)
        => StyleEditorRequested?.Invoke(
            this, new AnnotationStyleEditorRequestedEventArgs(_styleEditorX, _styleEditorY, isFromPointer));

    /// <summary><see cref="AddMenuRequested"/> を発生させる。</summary>
    /// <param name="x">右クリックした位置（ローカル DIP）。</param>
    /// <param name="y">右クリックした位置（ローカル DIP）。</param>
    private void RaiseAddMenuRequested(double x, double y)
        => AddMenuRequested?.Invoke(this, new AnnotationAddMenuRequestedEventArgs(x, y));

    /// <summary><see cref="EditingChanged"/> を発生させる。</summary>
    private void RaiseEditingChanged() => EditingChanged?.Invoke(this, EventArgs.Empty);

    /// <summary><see cref="SelectionChanged"/> を発生させる。</summary>
    private void RaiseSelectionChanged() => SelectionChanged?.Invoke(this, EventArgs.Empty);

    /// <summary><see cref="TextSelectionChanged"/> を発生させる。</summary>
    private void RaiseTextSelectionChanged() => TextSelectionChanged?.Invoke(this, EventArgs.Empty);

    /// <summary><see cref="DocumentChanged"/> を発生させる。</summary>
    private void RaiseDocumentChanged() => DocumentChanged?.Invoke(this, EventArgs.Empty);
}
