using Microsoft.Extensions.Logging;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Rendering;
using EsUtil.Feature.Marquee;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Native;
using EsUtil.Platform.Core.Native.Win;
using EsUtil.Platform.Core.Palette;
using EsUtil.Feature.Annotation.Core.Contract;
using EsUtil.Feature.Annotation.Core.Defaults;
using EsUtil.Feature.Annotation.Core.Geometry;
using EsUtil.Feature.Annotation.Core.Interaction;
using EsUtil.Feature.Annotation.Presenter.Drawing;
using EsUtil.Feature.Annotation.Presenter.Selection;
using EsUtil.Feature.Text;
using EsUtil.Feature.Text.Core.Text;
using EsUtil.Feature.Text.Presenter.Editing;

namespace EsUtil.Ui.MewUI.Annotation;

/// <summary>画像の上へ注釈（枠線・吹き出し）を重ねるレイヤー コントロール（MewUI 版）。</summary>
/// <remarks>
/// <b>役割</b>: 画像の表示・注釈の描画・入力の受付を担う。 判断（当たり判定・折り返し・描画計画）は
/// <c>EsUtil.Feature.Annotation</c> が単一所有し、本クラスは自 UI の描画 API へ写すだけである。<br/>
/// <b>選択枠と 8 方向のハンドルは Marquee の提示層を再利用する</b>（枠・ハンドル・nudge・カーソルを 4 つの UI 版で揃えるため）。<br/>
/// <b>本文の編集はその場で行う</b>: 表示中のレイアウトのままキャレット・選択範囲を描いて入力する（入力欄を重ねない）。
/// 日本語入力（IME）の変換中文字列は文書へ入れず、キャレットの位置へ下線付きで描く。<br/>
/// <b>座標</b>: 注釈は元画像ピクセルで保持し、描画時は表示倍率で写す（画像へ焼き込まない）。
/// </remarks>
public sealed partial class AnnotationLayerControl : Panel
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

    /// <summary>カーソル操作（nudge のカーソル移動・非表示）。</summary>
    private readonly ICursorService _cursor = new WinCursorService();

    /// <summary>選択枠の外観（暗転・ラベル・マーカーは使わない）。</summary>
    private static readonly MarqueeAppearance s_marqueeAppearance = MarqueeAppearance.OutlineOnly;

    /// <summary>直近に描いた選択枠のハンドル（DIP。 ポインターの形を決めるのに使う）。</summary>
    private IReadOnlyList<EsRect>? _handleDipRects;

    /// <summary>しっぽの先端をドラッグ中かどうか。</summary>
    private bool _isDraggingTail;

    /// <summary>Marquee からの通知で文書を更新している最中かどうか（再入防止）。</summary>
    private bool _isApplyingMarquee;

    /// <summary>押下中に固定した変換（null は固定していない）。</summary>
    private AnnotationViewTransform? _frozenTransform;

    /// <summary>最後に観測したポインター位置（元画像ピクセル。 nudge の起点に使う）。</summary>
    private EsPoint _lastPointerSource;

    /// <summary>表示する画像（変換済み。 差し替え時に解放する）。</summary>
    private EsReadOnlyBitmap? _imageSource;

    /// <summary>変換した画像の元（画素を保持する）。</summary>
    private ImageSource? _decoded;

    /// <summary>描画に渡す画像。</summary>
    private IImage? _image;

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

    /// <summary>注釈レイヤーを初期化する。</summary>
    public AnnotationLayerControl()
    {
        _core = new AnnotationCore(_presenter);
        _session = new AnnotationEditSession(new AnnotationDocument(1, 1), () => _core.Log);
        _session.Subscribe(OnSessionChanged);
        Focusable = true;
        ClipToBounds = true;
        _marquee = new MarqueePresenter(new MarqueeEffects(this))
        {
            Appearance = s_marqueeAppearance,
        };
        _marquee.SetCoordTransform(CoordinateTransform);
        _marquee.Subscribe(OnMarqueeNotice);
    }

    /// <summary>表示する画像を取得または設定する。</summary>
    /// <value>背景に表示する画像。 差し替えると変換を作り直す（破棄は本コントロールが行う）。</value>
    public EsReadOnlyBitmap? Source
    {
        get => _imageSource;
        set
        {
            if (ReferenceEquals(_imageSource, value))
            {
                return;
            }

            ReleaseImage();
            _imageSource = value;
            InvalidateVisual();
        }
    }

    /// <summary>画像を自分で描くかどうかを取得または設定する。</summary>
    /// <value>プレビューへ重ねるときは <see langword="false"/>（画像と影はプレビューが描く）。</value>
    public bool ShowImage { get; set; } = true;

    /// <summary>ホストから渡された画像の表示矩形（<see cref="ImageRectProvider"/> が無い場合だけ使う）。</summary>
    private EsRect? _previewImageRect;

    /// <summary>プレビュー内の画像の表示矩形（DIP）を取得する。</summary>
    /// <value>プレビューが画像を表示している矩形。 未設定の場合は自分の領域いっぱいへ中央寄せで表示する。</value>
    /// <remarks>
    /// <b>毎回ホストへ問い合わせる</b>: レイアウトの途中で取得した値は古くなる（レイアウト確定前に
    /// 取得すると、その後ウィンドウやツール バーの寸法が変わって画像の位置がずれる）。 値を持ち回すと
    /// 注釈だけが画像からずれるため、使うたびに取り直す。
    /// </remarks>
    public EsRect? PreviewImageRect => ImageRectProvider?.Invoke() ?? _previewImageRect;

    /// <summary>画像の表示矩形を取り直す関数（ホストが設定する）。</summary>
    /// <remarks>null の場合は <see cref="SetPreviewImageRect"/> で与えた値を使う。</remarks>
    public Func<EsRect?>? ImageRectProvider { get; set; }

    /// <summary>画像の表示矩形を設定する（<see cref="ImageRectProvider"/> が無い場合だけ使う）。</summary>
    /// <param name="rect">矩形（このコントロールの座標）。</param>
    public void SetPreviewImageRect(EsRect? rect) => _previewImageRect = rect;

    /// <summary>指定した位置（DIP）を左上として枠線を追加する。</summary>
    /// <param name="dip">左上の位置（ローカル DIP）。</param>
    /// <returns>追加した注釈の識別子。</returns>
    public string AddFrameAt(EsPoint dip) => AddFrameAt(new Point(dip.X, dip.Y));

    /// <summary>指定した位置（DIP）を左上として枠線を追加する。</summary>
    /// <param name="dip">左上の位置（ローカル DIP）。</param>
    /// <returns>追加した注釈の識別子。</returns>
    public string AddFrameAt(Point dip) => AddFrame(ResolveDefaultBounds(ToSource(dip)));

    /// <summary>指定した位置（DIP）を左上として吹き出しを追加する。</summary>
    /// <param name="dip">左上の位置（ローカル DIP）。</param>
    /// <returns>追加した注釈の識別子。</returns>
    public string AddBubbleAt(EsPoint dip) => AddBubbleAt(new Point(dip.X, dip.Y));

    /// <summary>指定した位置（DIP）を左上として吹き出しを追加する。</summary>
    /// <param name="dip">左上の位置（ローカル DIP）。</param>
    /// <returns>追加した注釈の識別子。</returns>
    public string AddBubbleAt(Point dip) => AddBubble(ResolveDefaultBounds(ToSource(dip)), string.Empty);

    /// <summary>追加する注釈の既定の矩形を求める（指定した位置を左上にし、画像の内側へ収める）。</summary>
    /// <param name="sourcePoint">左上の位置（元画像ピクセル）。</param>
    /// <returns>画像の内側へ収めた矩形。</returns>
    private EsRect ResolveDefaultBounds(EsPoint sourcePoint)
        => AnnotationGeometry.ResolveDraftBounds(sourcePoint, new EsSize(SourcePixelWidth, SourcePixelHeight));

    /// <summary>注釈の無い場所でドラッグしている間かどうか（プレビューのパンへ渡す）。</summary>
    private bool _isPreviewDragging;

    /// <summary>指定した位置（DIP）に注釈があるかどうかを判定する（プレビューの入力を受け取るかの判断に使う）。</summary>
    /// <param name="dip">位置（ローカル DIP）。</param>
    /// <returns>注釈・ハンドル・しっぽの近くにある場合は <see langword="true"/>。</returns>
    public bool HitTestAt(Point dip) => ResolvePointerKind(dip) != AnnotationPointerKind.None;

    /// <summary>元画像の幅（ピクセル）を取得または設定する。</summary>
    public int SourcePixelWidth { get; set; } = 1;

    /// <summary>元画像の高さ（ピクセル）を取得または設定する。</summary>
    public int SourcePixelHeight { get; set; } = 1;

    /// <summary>選択中の注釈の識別子を取得または設定する。</summary>
    /// <remarks>設定すると本文の編集を終え、選択枠と書式の適用先を切り替える（規則は編集セッションが単一所有する）。</remarks>
    public string? SelectedId
    {
        get => _session.SelectedId;
        set => _session.SelectedId = value;
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
    /// <remarks><b>保存値は表記に依らず <c>#AARRGGBB</c></b>である（表記は画面に出す文字列だけを決める）。</remarks>
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
    public (int Start, int Length) TextSelectionRange
    {
        get => _session.TextSelectionRange;
        set
        {
            _session.TextSelectionRange = value;
            InvalidateVisual();
        }
    }

    /// <summary>選択枠のハンドルを掴む半径（DIP）を取得または設定する。</summary>
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
    public double HandleSize
    {
        get => _marquee.Options.HandleSize;
        set
        {
            _marquee.Options = _marquee.Options with { HandleSize = value };
            InvalidateVisual();
        }
    }

    /// <summary>注釈の文書を取得または設定する。</summary>
    /// <remarks>差し替えると選択を解除し、変更履歴を空にする（別の画像の注釈を引き継がない）。</remarks>
    public AnnotationDocument Document
    {
        get => _session.Document;
        set
        {
            _session.Document = value;
            InvalidateVisual();
        }
    }

    /// <summary>選択中の注釈を取得する。</summary>
    public AnnotationItem? Selected => _session.Selected;

    /// <summary>選択中の本文の書式を取得する（選択範囲の先頭の書式）。</summary>
    /// <remarks>選択が無い間は、書式バーで選んだ書式（以降の入力へ効く値）を返す。</remarks>
    public TextStyle SelectedTextStyle
    {
        get => _session.SelectedTextStyle;
        set
        {
            // 適用先の規則（選択が無ければ以降の入力へ回す）は編集セッションが単一所有する
            _session.SelectedTextStyle = value;
            InvalidateVisual();
        }
    }

    /// <summary>消しゴム（元に戻す）ができるかどうかを取得する。</summary>
    public bool CanUndo => _session.CanUndo;

    /// <summary>やり直しができるかどうかを取得する。</summary>
    public bool CanRedo => _session.CanRedo;

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

    /// <summary>選択中の本文へ書式を適用する（<see cref="TextSelectionRange"/> の範囲）。</summary>
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

    /// <summary>選択中の本文の書式を変換する（<see cref="TextSelectionRange"/> の範囲）。</summary>
    /// <param name="transform">書式を変える処理。</param>
    /// <returns>選択範囲へ適用した場合は <see langword="true"/>。 選択が無く以降の入力へ回した場合は <see langword="false"/>。</returns>
    /// <remarks>
    /// <b>選択が無ければ以降の入力へ回す</b>: 変換の元になる書式は <see cref="SelectedTextStyle"/>（直前に入力した
    /// 文字の書式、または書式バーで選んだ書式）である。
    /// </remarks>
    public bool TransformTextStyle(Func<TextStyle, TextStyle> transform)
        => _session.TransformTextStyle(transform);

    /// <summary>選択中の本文の書式を変換する（指定した属性だけ変える）。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（0 は全体）。</param>
    /// <param name="transform">書式を変える処理。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    public bool TransformTextStyle(int start, int length, Func<TextStyle, TextStyle> transform)
        => _session.TransformTextStyle(start, length, transform);

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
                _ = UpdateSelectedAppearance(_ => appearance);
            }
        }
    }
    /// <summary>選択中の注釈の外観を差し替える。</summary>
    /// <param name="transform">外観を変える処理。</param>
    /// <returns>変えた場合は <see langword="true"/>。</returns>
    public bool UpdateSelectedAppearance(Func<AnnotationAppearance, AnnotationAppearance> transform)
        => _session.UpdateSelectedAppearance(transform);

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
                _ = UpdateSelectedTail(tail => tail with { Width = width });
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
                _ = UpdateSelectedTail(tail => tail with { BaseEdge = edge });
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
    public bool UpdateSelectedTail(Func<BubbleTail, BubbleTail> transform) => _session.UpdateSelectedTail(transform);

    /// <summary>選択中の注釈を最前面へ移す。</summary>
    /// <returns>移した場合は <see langword="true"/>。</returns>
    public bool MoveSelectedToFront() => _session.MoveSelectedToFront();

    /// <summary>選択中の注釈を最背面へ移す。</summary>
    /// <returns>移した場合は <see langword="true"/>。</returns>
    public bool MoveSelectedToBack() => _session.MoveSelectedToBack();

    /// <summary>変更を元に戻す。</summary>
    /// <returns>戻した場合は <see langword="true"/>。</returns>
    public bool Undo() => _session.Undo();

    /// <summary>変更をやり直す。</summary>
    /// <returns>やり直した場合は <see langword="true"/>。</returns>
    public bool Redo() => _session.Redo();

    /// <summary>外観・書式の編集パネルを表示する（右クリックと同じ）。</summary>
    public void ShowStyleEditor()
    {
        if (Selected is null)
        {
            return;
        }

        Focus();
        StyleEditorRequested?.Invoke(this, new AnnotationStyleEditorRequestedEventArgs(_styleEditorX, _styleEditorY));
    }

    /// <summary>選択中の本文の矩形（DIP）を取得する。</summary>
    /// <returns>本文の矩形。 吹き出しを選んでいない場合は空の矩形。</returns>
    public EsRect GetSelectedBodyRect()
    {
        if (_selectedBodyRectSource is { } body)
        {
            var (scale, offset) = ResolveImageTransform();
            return new EsRect(
                (body.X * scale) + offset.X,
                (body.Y * scale) + offset.Y,
                body.Width * scale,
                body.Height * scale);
        }

        return default;
    }

    /// <summary>選択中の注釈の矩形（このコントロールの座標）を取得する。</summary>
    /// <returns>矩形。 選択していない場合は空。</returns>
    /// <remarks>
    /// <b>書式バーを注釈に被せないための対象矩形</b>である。 本文の矩形ではなく枠線を含む矩形を使う
    /// （本文で判定するとバーが枠線の上へ数ピクセル重なる）
    /// </remarks>
    public EsRect GetSelectedBoundsRect()
    {
        if (Selected is not { } item)
        {
            return default;
        }

        var (scale, offset) = ResolveImageTransform();
        return new EsRect(
            (item.Bounds.X * scale) + offset.X,
            (item.Bounds.Y * scale) + offset.Y,
            item.Bounds.Width * scale,
            item.Bounds.Height * scale);
    }

    /// <summary>選択中の吹き出しの本文の矩形（元画像ピクセル。 描画のたびに更新する）。</summary>
    private EsRect? _selectedBodyRectSource;

    /// <summary>画像と注釈を元画像ピクセルの 1:1 で合成した画像を作る（保存用）。</summary>
    /// <returns>合成した画像。 破棄は呼び出し側。 描けない場合は <see langword="null"/>。</returns>
    /// <remarks>
    /// <b>選択枠・ハンドル・キャレットは描かない</b>（編集中の一時的な表示であり、保存する絵ではない）。<br/>
    /// 形式は PNG になり、拡張子は呼び出し側が決める（コアの保存は拡張子が形式を決める）。
    /// </remarks>
    public EsBitmap? RenderComposedImage()
    {
        var width = Math.Max(1, SourcePixelWidth);
        var height = Math.Max(1, SourcePixelHeight);

        var factory = Application.DefaultGraphicsFactory;
        using var surface = factory.CreateSurface(RenderSurfaceDescriptor.CpuPixels(width, height, 1.0, true, "annotation"));
        using (var context = factory.CreateContext(surface))
        {
            context.BeginFrame(surface);
            DrawAll(context, includeSelection: false, scale: 1.0, offset: default);
            context.EndFrame();
        }

        var stride = width * 4;
        var pixels = new byte[stride * height];
        if (!factory.TryReadPixels(surface, pixels, stride))
        {
            return null;
        }

        return EsBitmap.FromPixelCopy(width, height, pixels);
    }

    /// <summary>選択と本文の編集を解除する（注釈の外をクリックしたとき）。</summary>
    public void ClearSelection() => _session.Select(null);

    /// <summary>描画に使う文書を取得する（変換中の文字列を入れた複製を含む）。</summary>
    /// <value>変換中でなければ本文の文書。 変換中は変換中の文字列を入れた描画用の複製。</value>
    private AnnotationDocument RenderDocument
        => IsEditingText
            ? AnnotationCompositionPreview.Apply(_session.Document, _session.SelectedId, _session.CaretIndex, _preeditText)
            : _session.Document;

    /// <summary>編集セッションの変更をコントロールの状態とイベントへ反映する。</summary>
    /// <param name="change">変更の種類。</param>
    private void OnSessionChanged(AnnotationEditChange change)
    {
        if ((change & AnnotationEditChange.Editing) != 0)
        {
            if (!_session.IsEditingText)
            {
                _preeditText = string.Empty;
                _isSelectingText = false;
                StopCaretBlink();
                DetachInputWindow();
            }
            else
            {
                _marquee.ResetSelection();
            }

            EditingChanged?.Invoke(this, EventArgs.Empty);
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

            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        if ((change & AnnotationEditChange.TextSelection) != 0)
        {
            TextSelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        if ((change & AnnotationEditChange.Document) != 0)
        {
            DocumentChanged?.Invoke(this, EventArgs.Empty);
        }

        InvalidateVisual();
    }

    /// <summary>変換した画像を解放する。</summary>
    private void ReleaseImage()
    {
        _image?.Dispose();
        _image = null;
        _decoded?.Dispose();
        _decoded = null;
        _imageSource = null;
    }

    /// <summary>表示する画像を MewUI の画像として解決する（同じ画像なら作り直さない）。</summary>
    /// <returns>描画に渡す画像。 無い場合は <see langword="null"/>。</returns>
    private IImage? ResolveImage()
    {
        if (_image is not null)
        {
            return _imageSource is { IsDisposed: false } ? _image : null;
        }

        if (_imageSource is not { IsDisposed: false } bitmap)
        {
            return null;
        }

        _decoded = ImageSource.FromBgraPixels(bitmap.Width, bitmap.Height, bitmap.ReadOnlyPixels, hasAlpha: true);
        _image = _decoded.CreateImage(Application.DefaultGraphicsFactory);
        return _image;
    }

    /// <summary>画像の表示倍率と位置を求める（縦横比を保って中央に置く）。</summary>
    /// <returns>倍率と左上の位置（DIP）。</returns>
    private AnnotationViewTransform ResolveImageTransform()
        // 表示矩形の検証と変換の式はコアが単一所有する（4 つの UI 版で同じ結果にする）
        => AnnotationViewTransform.Resolve(
            new EsSize(RenderSize.Width, RenderSize.Height),
            new EsSize(SourcePixelWidth, SourcePixelHeight),
            PreviewImageRect is { Width: > 0, Height: > 0 } preview
                ? new EsRect(preview.X, preview.Y, preview.Width, preview.Height)
                : null);
    /// <summary>ローカル DIP を元画像ピクセルへ変換する。</summary>
    /// <param name="dip">ローカル DIP の座標。</param>
    /// <returns>元画像ピクセルの座標。</returns>
    /// <remarks>
    /// <b>押下中は変換を固定する</b>: ドラッグ中にホストが表示矩形を更新すると倍率が変わり、
    /// 注釈がカーソルからずれる。
    /// 押下時に決めた変換を解放まで使い続ける。
    /// </remarks>
    private EsPoint ToSource(Point dip)
        => (_frozenTransform ?? ResolveImageTransform()).ToSource(new EsPoint(dip.X, dip.Y));

    /// <summary>押下中に使う変換を固定する（押下のたびに呼ぶ）。</summary>
    private void FreezeTransform() => _frozenTransform = ResolveImageTransform();

    /// <summary>固定した変換を解除する（ポインターを離したら呼ぶ）。</summary>
    private void ReleaseTransform() => _frozenTransform = null;

    /// <summary>元画像ピクセルをローカル DIP へ変換する。</summary>
    /// <param name="source">元画像ピクセルの座標。</param>
    /// <returns>ローカル DIP の座標。</returns>
    private EsPoint ToClientDip(EsPoint source)
    {
        // **押下中は描画も固定した変換を使う**: 変換が途中で変わると、枠と注釈がカーソルからずれ、
        // ヒット判定とも食い違う。
        return (_frozenTransform ?? ResolveImageTransform()).ToClient(source);
    }

    /// <summary>Marquee へ渡す座標変換ポートを生成する。</summary>
    private ICoordTransform CoordinateTransform => new CoordTransform(
        dip => ToSource(new Point(dip.X, dip.Y)),
        ToClientDip,
        () => new EsSize(SourcePixelWidth, SourcePixelHeight));

    /// <summary>行の文字数を求める（区間の文字数の合計）。</summary>
    /// <param name="line">行。</param>
    /// <returns>文字数。</returns>
    /// <remarks>数え方はコアが単一所有する（<see cref="TextLineLayout.Length"/>）。</remarks>
    internal static int LineTextLength(TextLinePlan line) => TextLineLayout.Length(line);
}

