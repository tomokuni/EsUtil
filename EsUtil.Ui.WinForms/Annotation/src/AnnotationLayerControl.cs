using Microsoft.Extensions.Logging;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using EsUtil.Feature.Marquee;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Palette;
using System.ComponentModel;
using EsUtil.Ui.WinForms.Interop.Imaging;
using EsUtil.Feature.Annotation.Core.Contract;
using EsUtil.Feature.Annotation.Core.Defaults;
using EsUtil.Feature.Annotation.Core.Geometry;
using EsUtil.Feature.Annotation.Core.Interaction;
using EsUtil.Feature.Annotation.Presenter.Selection;
using EsUtil.Feature.Text;
using EsUtil.Feature.Text.Core.Text;
using EsUtil.Feature.Text.Presenter.Editing;

namespace EsUtil.Ui.WinForms.Annotation;

/// <summary>画像の上へ注釈（枠線・吹き出し）を重ねるレイヤー コントロール（WinForms 版）。</summary>
/// <remarks>
/// <b>役割</b>: 画像の表示・注釈の描画・入力の受付を担う。 判断（当たり判定・折り返し・描画計画）は
/// <c>EsUtil.Feature.Annotation</c> が単一所有し、本クラスは GDI+ へ写すだけである。<br/>
/// <b>選択枠と 8 方向のハンドルは Marquee の提示層を再利用する</b>（枠・ハンドル・nudge を UI 版で揃えるため）。<br/>
/// <b>本文の編集はその場で行う</b>: 表示中のレイアウトのままキャレットと選択範囲を描き、入力欄を重ねない
/// （入力欄を重ねると折り返し・行の高さ・余白が入力欄の規則になり、表示と編集で見た目が変わる）。 IME のメッセージは自前で扱う。<br/>
/// <b>座標</b>: 注釈は元画像ピクセルで保持し、描画時は表示倍率で写す（画像へ焼き込まない）。
/// </remarks>
public sealed partial class AnnotationLayerControl : Control
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

    /// <summary>押下中に固定した変換（null は固定していない）。 ドラッグ中に表示矩形が変わっても 1:1 で動かす。</summary>
    private AnnotationViewTransform? _frozenTransform;

    /// <summary>選択枠の外観（暗転・ラベル・マーカーは使わない）。</summary>
    private static readonly MarqueeAppearance s_marqueeAppearance = MarqueeAppearance.OutlineOnly;

    /// <summary>直近に描いた選択枠のハンドル（DIP。 ポインターの形を決めるのに使う）。</summary>
    private IReadOnlyList<EsRect>? _handleDipRects;

    /// <summary>しっぽの先端をドラッグ中かどうか。</summary>
    private bool _isDraggingTail;

    /// <summary>注釈の無い場所でドラッグしている間かどうか（プレビューのパンへ渡す）。</summary>
    private bool _isPreviewDragging;

    /// <summary>外観フライアウトを出した位置（ローカル DIP）。</summary>
    private double _styleEditorX;

    /// <summary>外観フライアウトを出した位置（ローカル DIP）。</summary>
    private double _styleEditorY;

    /// <summary>Marquee からの通知で文書を更新している最中かどうか（再入防止）。</summary>
    private bool _isApplyingMarquee;

    /// <summary>最後に観測したポインター位置（元画像ピクセル。 nudge の起点に使う）。</summary>
    private EsPoint _lastPointerSource;

    /// <summary>表示用に変換した画像（差し替え時に解放する）。</summary>
    private Bitmap? _image;

    /// <summary>注釈レイヤーを初期化する。</summary>
    public AnnotationLayerControl()
    {
        _core = new AnnotationCore(_presenter);
        _session = new AnnotationEditSession(new AnnotationDocument(1, 1), () => _core.Log);
        _session.Subscribe(OnSessionChanged);
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.Selectable | ControlStyles.ResizeRedraw,
            true);

        TabStop = true;
        // 入力欄を重ねないため、IME のメッセージは自前で扱う（本文のその場編集）
        ImeMode = ImeMode.NoControl;
        _coordinateTransform = CreateCoordinateTransform(this);
        _marquee = new MarqueePresenter(new AnnotationMarqueeEffects(this))
        {
            Appearance = s_marqueeAppearance,
        };
        _marquee.SetCoordTransform(_coordinateTransform);
        _marquee.Subscribe(OnMarqueeNotice);
    }

    /// <summary>外観・書式の編集パネルを要求する（ホストが表示する）。</summary>
    public event EventHandler<AnnotationStyleEditorRequestedEventArgs>? StyleEditorRequested;

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
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EsReadOnlyBitmap? Source
    {
        get => _source;
        set
        {
            if (ReferenceEquals(_source, value))
            {
                return;
            }

            _source = value;
            _image?.Dispose();
            _image = value is { IsDisposed: false } bitmap ? EsBitmapDrawingInterop.ToDrawingBitmap(bitmap) : null;
            Invalidate();
        }
    }

    /// <summary>表示する画像（変換前）。</summary>
    private EsReadOnlyBitmap? _source;

    /// <summary>画像を自分で描くかどうかを取得または設定する。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowImage { get; set; } = true;

    /// <summary>プレビュー内の画像の表示矩形（DIP）を取得または設定する。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EsRect? PreviewImageRect => ImageRectProvider?.Invoke() ?? _previewImageRect;

    /// <summary>画像の表示矩形を取り直す関数（ホストが設定する）。</summary>
    /// <remarks>
    /// <b>毎回ホストへ問い合わせる</b>: レイアウトの途中で取得した値は古くなる（レイアウト確定前に
    /// 取得すると、その後ウィンドウやツール バーの寸法が変わって画像の位置がずれる）。 値を持ち回すと
    /// 注釈だけが画像からずれるため、使うたびに取り直す。
    /// </remarks>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<EsRect?>? ImageRectProvider { get; set; }

    /// <summary>画像の表示矩形を設定する（<see cref="ImageRectProvider"/> が無い場合だけ使う）。</summary>
    /// <param name="rect">矩形（このコントロールの座標）。</param>
    public void SetPreviewImageRect(EsRect? rect) => _previewImageRect = rect;

    /// <summary>ホストから渡された画像の表示矩形。</summary>
    private EsRect? _previewImageRect;

    /// <summary>指定した位置（DIP）を左上として枠線を追加する。</summary>
    /// <param name="dip">左上の位置（ローカル DIP）。</param>
    /// <returns>追加した注釈の識別子。</returns>
    public string AddFrameAt(System.Drawing.Point dip) => AddFrame(ResolveDefaultBounds(ToSource(new EsPoint(dip.X, dip.Y))));

    /// <summary>指定した位置（DIP）を左上として吹き出しを追加する。</summary>
    /// <param name="dip">左上の位置（ローカル DIP）。</param>
    /// <returns>追加した注釈の識別子。</returns>
    public string AddBubbleAt(System.Drawing.Point dip) => AddBubble(ResolveDefaultBounds(ToSource(new EsPoint(dip.X, dip.Y))), string.Empty);

    /// <summary>追加する注釈の既定の矩形を求める。</summary>
    /// <param name="sourcePoint">左上の位置（元画像ピクセル）。</param>
    /// <returns>画像の内側へ収めた矩形。</returns>
    private EsRect ResolveDefaultBounds(EsPoint sourcePoint)
        => AnnotationGeometry.ResolveDraftBounds(sourcePoint, new EsSize(SourcePixelWidth, SourcePixelHeight));

    /// <summary>指定した位置（DIP）に注釈があるかどうかを判定する。</summary>
    /// <param name="dip">位置（ローカル DIP）。</param>
    /// <returns>注釈の近くにある場合は <see langword="true"/>。</returns>
    public bool HitTestAt(System.Drawing.Point dip) => ResolvePointerKind(new EsPoint(dip.X, dip.Y)) != AnnotationPointerKind.None;

    /// <summary>元画像の幅（ピクセル）を取得または設定する。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SourcePixelWidth { get; set; } = 1;

    /// <summary>元画像の高さ（ピクセル）を取得または設定する。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SourcePixelHeight { get; set; } = 1;

    /// <summary>選択中の注釈の識別子を取得または設定する。</summary>
    /// <remarks>設定すると本文の編集を終え、選択枠と書式の適用先を切り替える（規則は編集セッションが単一所有する）。</remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? SelectedId
    {
        get => _session.SelectedId;
        set => _session.SelectedId = value;
    }

    /// <summary>新規作成時の既定値を取得または設定する。</summary>
    /// <remarks><b>既存の注釈には影響しない</b>（各注釈が自分の外観・書式を持つ）。</remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public AnnotationDraftDefaults DraftDefaults
    {
        get => _session.DraftDefaults;
        set => _session.DraftDefaults = value;
    }

    /// <summary>色の入力欄へ出す表記を取得または設定する。</summary>
    /// <value>色名 + 不透明度（既定）またはカラーコード（<c>#AARRGGBB</c>）。</value>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ILogger Log
    {
        get => _core.Log;
        set => _core.Log = value;
    }

    /// <summary>ログの接頭辞を取得または設定する（ロガーと一緒に設定する）。</summary>
    /// <value>接頭辞（例: <c>[Annotation]</c>）。 null または空白を設定すると既定値へ戻す。</value>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? LogPrefix
    {
        get => _core.LogPrefix;
        set => _core.LogPrefix = value;
    }
    /// <summary>行間の倍率を取得または設定する。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double LineHeightFactor
    {
        get => _presenter.LineHeightFactor;
        set
        {
            _presenter.LineHeightFactor = value;
            Invalidate();
        }
    }

    /// <summary>選択枠のハンドルの一辺の大きさ（DIP）を取得または設定する。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double HandleSize
    {
        get => _marquee.Options.HandleSize;
        set
        {
            _marquee.Options = _marquee.Options with { HandleSize = value };
            Invalidate();
        }
    }

    /// <summary>注釈の文書を取得または設定する。</summary>
    /// <remarks>差し替えると選択を解除し、変更履歴を空にする（別の画像の注釈を引き継がない）。</remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public AnnotationDocument Document
    {
        get => _session.Document;
        set
        {
            _session.Document = value;
            Invalidate();
        }
    }

    /// <summary>選択中の注釈を取得する。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public AnnotationItem? Selected => _session.Selected;

    /// <summary>選択中の本文の書式を取得または設定する（選択範囲の先頭の書式。 設定すると選択範囲へ適用する）。</summary>
    /// <remarks>選択が無い間は、書式バーで選んだ書式（以降の入力へ効く値）を返す。</remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextStyle SelectedTextStyle
    {
        get => _session.SelectedTextStyle;
        set
        {
            // 適用先の規則（選択が無ければ以降の入力へ回す）は編集セッションが単一所有する
            _session.SelectedTextStyle = value;
            Invalidate();
        }
    }

    /// <summary>元に戻すことができるかどうかを取得する。</summary>
    public bool CanUndo => _session.CanUndo;

    /// <summary>やり直すことができるかどうかを取得する。</summary>
    public bool CanRedo => _session.CanRedo;

    /// <summary>選択と本文の編集を解除する（注釈の外をクリックしたとき）。</summary>
    public void ClearSelection() => _session.Select(null);
    /// <summary>枠線を追加する。</summary>
    /// <param name="bounds">位置と大きさ（元画像ピクセル）。</param>
    /// <returns>追加した注釈の識別子。</returns>
    /// <remarks>矩形はコア（<see cref="AnnotationCore.CreateFrame"/>）が画像の内側へ収める。</remarks>
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

    /// <summary>選択中の本文へ書式を適用する（選択範囲）。</summary>
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
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
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
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
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
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
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
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
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

    /// <summary>本文の描画を別のコントロールへ任せる吹き出しの識別子を取得または設定する。</summary>
    /// <value>本文編集コントロールが描く吹き出しの識別子。 無い場合は <see langword="null"/>。</value>
    /// <remarks>
    /// <b>本文の描画を 2 か所に持たない</b>: 本文編集コントロールを重ねている間は、本レイヤーは本文を描かない。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? ExternalTextId
    {
        get => _externalTextId;
        set
        {
            if (string.Equals(_externalTextId, value, StringComparison.Ordinal))
            {
                return;
            }

            _externalTextId = value;
            Invalidate();
        }
    }

    /// <summary>本文の描画を別のコントロールへ任せる吹き出しの識別子（保持する値）。</summary>
    private string? _externalTextId;

    /// <summary>編集の状態と文書を所有する編集セッションを取得する（本文編集コントロールと共有する）。</summary>
    internal AnnotationEditSession Session => _session;

    /// <summary>IME の変換中の文字列を取得する。</summary>
    internal string PreeditText => _preeditText;

    /// <summary>画像の表示倍率（元画像ピクセル → DIP）を取得する。</summary>
    internal double ViewScale => ResolveImageTransform().Scale;

    /// <summary>元画像ピクセルの矩形を、このコントロールの座標（DIP）の矩形へ変換する。</summary>
    /// <param name="source">矩形（元画像ピクセル）。</param>
    /// <returns>矩形（このコントロールの座標の DIP）。</returns>
    public RectangleF GetClientRect(EsRect source)
    {
        var (scale, offset) = ResolveImageTransform();
        return new RectangleF(
            (float)(offset.X + (source.X * scale)),
            (float)(offset.Y + (source.Y * scale)),
            (float)(source.Width * scale),
            (float)(source.Height * scale));
    }

    /// <summary>画像と注釈を元画像ピクセルの 1:1 で合成した画像を作る（保存用）。</summary>
    /// <returns>合成した画像。 破棄は呼び出し側。 作れない場合は <see langword="null"/>。</returns>
    /// <remarks>選択枠・ハンドル・キャレットは描かない（編集中の一時的な表示であり、保存する絵ではない）。</remarks>
    public EsBitmap? RenderComposedImage()
    {
        var width = Math.Max(1, SourcePixelWidth);
        var height = Math.Max(1, SourcePixelHeight);

        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (_image is { } image)
            {
                graphics.DrawImage(image, new Rectangle(0, 0, width, height));
            }

            var plan = _presenter.ResolveDrawPlan(_session.Document, AnnotationTextMetrics.Measure, null);
            AnnotationPainter.Draw(graphics, plan, 1, 0, 0);
        }

        var data = bitmap.LockBits(
            new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
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

    /// <summary>編集セッションの変更をコントロールの状態とイベントへ反映する。</summary>
    /// <param name="change">変更の種類。</param>
    private void OnSessionChanged(AnnotationEditChange change)
    {
        if ((change & AnnotationEditChange.Editing) != 0)
        {
            if (!_session.IsEditingText)
            {
                SetPreedit(string.Empty);
                StopCaretBlink();
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

        Invalidate();
    }

    /// <summary>画像の表示倍率と位置を求める（縦横比を保って中央に置く）。</summary>
    /// <returns>倍率と左上の位置（DIP）。</returns>
    internal AnnotationViewTransform ResolveImageTransform()
        // 表示矩形の検証と変換の式はコアが単一所有する（4 つの UI 版で同じ結果にする）
        => AnnotationViewTransform.Resolve(
            new EsSize(ClientSize.Width, ClientSize.Height),
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

    /// <summary>選択中の注釈の矩形（DIP）を取得する。</summary>
    /// <returns>矩形（DIP）。 選択していない場合は空。</returns>
    /// <remarks>
    /// <b>書式バーを注釈に被せないための対象矩形</b>である。 本文の矩形ではなく枠線を含む矩形を使う
    /// （本文で判定するとバーが枠線の上へ数ピクセル重なる）
    /// </remarks>
    public RectangleF GetSelectedBoundsRect()
    {
        if (Selected is not { } item)
        {
            return default;
        }

        var topLeft = ToClientDip(item.Bounds.TopLeft);
        var bottomRight = ToClientDip(item.Bounds.BottomRight);
        return new RectangleF(
            (float)topLeft.X,
            (float)topLeft.Y,
            (float)(bottomRight.X - topLeft.X),
            (float)(bottomRight.Y - topLeft.Y));
    }

    /// <summary>Marquee へ渡す座標変換ポート（変換の規則が変わらないため 1 度だけ作る）。</summary>
    private readonly ICoordTransform _coordinateTransform;

    /// <summary>Marquee へ渡す座標変換ポートを生成する。</summary>
    /// <param name="owner">座標変換の持ち主。</param>
    /// <returns>座標変換ポート。</returns>
    private static ICoordTransform CreateCoordinateTransform(AnnotationLayerControl owner) => new CoordTransform(
        owner.ToSource,
        owner.ToClientDip,
        () => new EsSize(owner.SourcePixelWidth, owner.SourcePixelHeight));

    /// <summary>カーソル形状を Marquee の種別へ合わせる。</summary>
    /// <param name="kind">Marquee のカーソル種別。</param>
    internal void ApplyCursor(MarqueeCursorKind kind) => Cursor = kind switch
    {
        MarqueeCursorKind.Move => Cursors.SizeAll,
        MarqueeCursorKind.Horizontal => Cursors.SizeWE,
        MarqueeCursorKind.Vertical => Cursors.SizeNS,
        MarqueeCursorKind.DiagonalNWSE => Cursors.SizeNWSE,
        MarqueeCursorKind.DiagonalNESW => Cursors.SizeNESW,
        _ => Cursors.Default,
    };

    /// <summary>表示用の画像を破棄する。</summary>
    /// <param name="disposing">マネージド リソースを解放する場合は <see langword="true"/>。</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _image?.Dispose();
            _image = null;
        }

        base.Dispose(disposing);
    }
}

