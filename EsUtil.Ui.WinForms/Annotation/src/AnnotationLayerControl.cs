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

    /// <summary>選択枠・ハンドルを解決する Marquee の提示層。</summary>
    private readonly MarqueePresenter _marquee;

    /// <summary>押下中に固定した変換（null は固定していない）。 ドラッグ中に表示矩形が変わっても 1:1 で動かす。</summary>
    private (double Scale, EsPoint Offset)? _frozenTransform;

    /// <summary>選択枠の外観（暗転・ラベル・マーカーは使わない）。</summary>
    private static readonly MarqueeAppearance s_marqueeAppearance = MarqueeAppearance.Default with
    {
        ShowOuterMask = false,
        ShowInnerMask = false,
        ShowMarker = false,
        ShowSizeLabel = false,
        ShowCoordLabel = false,
    };

    /// <summary>注釈の文書。</summary>
    private AnnotationDocument _document = new(1, 1);

    /// <summary>変更履歴（元に戻す・やり直す）。</summary>
    private readonly AnnotationHistory _history = new();

    /// <summary>以降の入力へ効かせる書式（選択が無いときの書式変更の行き先）。</summary>
    private readonly AnnotationTypingStyle _typingStyle = new();

    /// <summary>直近に描いた選択枠のハンドル（DIP。 ポインターの形を決めるのに使う）。</summary>
    private IReadOnlyList<EsRect>? _handleDipRects;

    /// <summary>これから記録する変更の直前の文書（変更が起きるまで保持する）。</summary>
    private AnnotationDocument? _undoCandidate;

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
    /// 注釈だけが画像からずれるため（実測 2026-11-21）、使うたびに取り直す。
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
    {
        var imageWidth = Math.Max(1, SourcePixelWidth);
        var imageHeight = Math.Max(1, SourcePixelHeight);
        var width = Math.Min(240, imageWidth);
        var height = Math.Min(160, imageHeight);
        return new EsRect(
            Math.Clamp(sourcePoint.X, 0, Math.Max(0, imageWidth - width)),
            Math.Clamp(sourcePoint.Y, 0, Math.Max(0, imageHeight - height)),
            width,
            height);
    }

    /// <summary>指定した位置（DIP）に注釈があるかどうかを判定する。</summary>
    /// <param name="dip">位置（ローカル DIP）。</param>
    /// <returns>注釈の近くにある場合は <see langword="true"/>。</returns>
    public bool HitTestAt(System.Drawing.Point dip)
        => AnnotationHitTest.HitTest(_document, ToSource(new EsPoint(dip.X, dip.Y))) is not null;

    /// <summary>元画像の幅（ピクセル）を取得または設定する。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SourcePixelWidth { get; set; } = 1;

    /// <summary>元画像の高さ（ピクセル）を取得または設定する。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SourcePixelHeight { get; set; } = 1;

    /// <summary>選択中の注釈の識別子を取得または設定する。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? SelectedId { get; set; }

    /// <summary>新規作成時の既定値を取得または設定する。</summary>
    /// <remarks><b>既存の注釈には影響しない</b>（各注釈が自分の外観・書式を持つ）。</remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public AnnotationDraftDefaults DraftDefaults { get; set; } = AnnotationDraftDefaults.Default;

    /// <summary>色の入力欄へ出す表記を取得または設定する。</summary>
    /// <value>色名 + 不透明度（既定）またはカラーコード（<c>#AARRGGBB</c>）。</value>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ColorTextFormat ColorTextFormat { get; set; } = ColorTextFormat.NameWithAlpha;

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
        get => _document;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            EndTextEditing();
            _document = value;
            _history.Clear();
            _undoCandidate = null;
            SelectedId = null;
            _marquee.ResetSelection();
            RaiseDocumentChanged();
            Invalidate();
        }
    }

    /// <summary>選択中の注釈を取得する。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public AnnotationItem? Selected => SelectedId is null ? null : _document.Find(SelectedId);

    /// <summary>選択中の本文の書式を取得または設定する（選択範囲の先頭の書式。 設定すると選択範囲へ適用する）。</summary>
    /// <remarks>選択が無い間は、書式バーで選んだ書式（以降の入力へ効く値）を返す。</remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public AnnotationTextStyle SelectedTextStyle
    {
        get => _typingStyle.ResolveDisplay(
            AnnotationSelectionPresenter.ResolveTextStyle(_document, SelectedId, TextSelectionRange),
            HasTextSelection);
        set
        {
            // 適用先の規則（選択が無ければ以降の入力へ回す）はコアが単一所有する
            _ = ApplyTextStyle(value);
        }
    }

    /// <summary>元に戻すことができるかどうかを取得する。</summary>
    public bool CanUndo => _history.CanUndo;

    /// <summary>やり直すことができるかどうかを取得する。</summary>
    public bool CanRedo => _history.CanRedo;

    /// <summary>選択と本文の編集を解除する（注釈の外をクリックしたとき）。</summary>
    public void ClearSelection()
    {
        EndTextEditing();
        Select(null);
    }
    /// <summary>枠線を追加する。</summary>
    /// <param name="bounds">位置と大きさ（元画像ピクセル）。</param>
    /// <returns>追加した注釈の識別子。</returns>
    /// <remarks>矩形はコア（<see cref="AnnotationCore.CreateFrame"/>）が画像の内側へ収める。</remarks>
    public string AddFrame(EsRect bounds)
    {
        PrepareUndo();
        var item = _core.CreateFrame(_document, bounds, DraftDefaults);
        _document = _document.Add(item);
        Select(item.Id);
        RaiseDocumentChanged();
        Invalidate();
        return item.Id;
    }

    /// <summary>吹き出しを追加する。</summary>
    /// <param name="bounds">位置と大きさ（元画像ピクセル）。</param>
    /// <param name="text">本文。</param>
    /// <returns>追加した注釈の識別子。</returns>
    /// <remarks>矩形もしっぽの先端もコア（<see cref="AnnotationCore.CreateBubble"/>）が画像の内側へ収める。</remarks>
    public string AddBubble(EsRect bounds, string text)
    {
        PrepareUndo();
        var defaults = DraftDefaults;
        var item = _core.CreateBubble(
            _document, bounds, RichText.Create(text, defaults.TextStyle), defaults);

        _document = _document.Add(item);
        Select(item.Id);
        RaiseDocumentChanged();
        Invalidate();
        return item.Id;
    }

    /// <summary>選択中の注釈を削除する。</summary>
    /// <returns>削除した場合は <see langword="true"/>。</returns>
    public bool DeleteSelected()
    {
        if (SelectedId is not { } id)
        {
            return false;
        }

        EndTextEditing();
        PrepareUndo();
        _document = _document.Remove(id);
        SelectedId = null;
        _marquee.ResetSelection();
        RaiseSelectionChanged();
        RaiseDocumentChanged();
        Invalidate();
        return true;
    }

    /// <summary>選択中の吹き出しの本文を置き換える。</summary>
    /// <param name="text">新しい本文。</param>
    /// <returns>置き換えた場合は <see langword="true"/>。</returns>
    public bool SetSelectedText(string text)
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return false;
        }

        PrepareUndo();
        _document = _document.WithText(bubble, bubble.Text.WithPlainText(text));
        RaiseDocumentChanged();
        Invalidate();
        return true;
    }

    /// <summary>選択中の本文へ書式を適用する（<see cref="TextSelectionRange"/> の範囲）。</summary>
    /// <param name="style">書式。</param>
    /// <returns>選択範囲へ適用した場合は <see langword="true"/>。 選択が無く以降の入力へ回した場合は <see langword="false"/>。</returns>
    /// <remarks>
    /// <b>選択が無ければ以降の入力へ回す</b>: 既存の文字は変えず、次に入力する文字へ適用する
    /// （規則は <see cref="AnnotationTextStyleEdit"/> と <see cref="AnnotationTypingStyle"/> が単一所有する）。
    /// </remarks>
    public bool ApplyTextStyle(AnnotationTextStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);

        _typingStyle.Remember(style);
        return AnnotationTextStyleEdit.ResolveRange(TextSelectionStart, TextSelectionLength) is { } selection
            && ApplyTextStyle(selection.Start, selection.Length, style);
    }

    /// <summary>選択中の本文へ書式を適用する。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（0 は全体）。</param>
    /// <param name="style">書式。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    public bool ApplyTextStyle(int start, int length, AnnotationTextStyle style)
        => SetTextStyle(text => text.ApplyStyle(start, length, style));

    /// <summary>選択中の本文の書式を変換する（1 つの属性だけ変える）。</summary>
    /// <param name="start">開始位置（文字数）。</param>
    /// <param name="length">長さ（0 は全体）。</param>
    /// <param name="transform">書式を変える処理。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    public bool TransformTextStyle(int start, int length, Func<AnnotationTextStyle, AnnotationTextStyle> transform)
        => SetTextStyle(text => text.TransformStyle(start, length, transform));

    /// <summary>選択中の本文の書式を変換する（選択範囲。 選択が無ければ以降の入力へ回す）。</summary>
    /// <param name="transform">書式を変える処理。</param>
    /// <returns>選択範囲へ適用した場合は <see langword="true"/>。 選択が無く以降の入力へ回した場合は <see langword="false"/>。</returns>
    /// <remarks>変換の元になる書式は <see cref="SelectedTextStyle"/>（直前に入力した文字、または選んだ書式）である。</remarks>
    public bool TransformTextStyle(Func<AnnotationTextStyle, AnnotationTextStyle> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        var next = transform(SelectedTextStyle);
        _typingStyle.Remember(next);

        return AnnotationTextStyleEdit.ResolveRange(TextSelectionStart, TextSelectionLength) is { } selection
            && TransformTextStyle(selection.Start, selection.Length, _ => next);
    }

    /// <summary>選択中の本文の書式を置き換える。</summary>
    /// <param name="change">本文を変える処理。</param>
    /// <returns>変えた場合は <see langword="true"/>。</returns>
    private bool SetTextStyle(Func<RichText, RichText> change)
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return false;
        }

        PrepareUndo();
        _document = _document.WithText(bubble, change(bubble.Text));
        RaiseDocumentChanged();
        Invalidate();
        return true;
    }

    /// <summary>選択中の注釈の外観（枠線・内側・角丸）を取得または設定する。</summary>
    /// <value>外観。 何も選んでいない場合は `null`。 設定するとその注釈の外観を差し替える。</value>
    /// <remarks>値の解決と適用の規則はコア（`AnnotationSelectionPresenter`）が単一所有する。</remarks>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
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
    /// <param name="transform">外観を変える処理。</param>
    /// <returns>変えた場合は <see langword="true"/>。</returns>
    public bool UpdateSelectedAppearance(Func<AnnotationAppearance, AnnotationAppearance> transform)
        => UpdateSelected(item => item.Appearance = transform(item.Appearance));

    /// <summary>選択中の吹き出しの本文の余白を取得または設定する（元画像ピクセル）。</summary>
    /// <value>余白。 吹き出しを選んでいない場合は ``null``。 設定するとその吹き出しの余白を変える。</value>
    /// <remarks>値の解決と補正はコアが単一所有する（<see cref="AnnotationSelectionPresenter"/> / <see cref="AnnotationLimits"/>）。</remarks>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
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
    /// <value>幅。 吹き出しを選んでいない場合は ``null``。 設定するとその吹き出しのしっぽの幅を変える。</value>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
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
    /// <value>辺。 吹き出しを選んでいない場合は ``null``。 設定するとその吹き出しのしっぽの辺を変える。</value>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
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
    /// <summary>選択中の吹き出しの本文の余白を設定する。</summary>
    /// <param name="padding">余白（元画像ピクセル）。</param>
    /// <returns>変えた場合は <see langword="true"/>。</returns>
    public bool SetSelectedBodyPadding(double padding)
        => UpdateSelected(item =>
        {
            if (item is BubbleAnnotation bubble)
            {
                bubble.BodyPadding = padding;
            }
        });

    /// <summary>選択中の吹き出しのしっぽを差し替える。</summary>
    /// <param name="transform">しっぽを変える処理。</param>
    /// <returns>変えた場合は <see langword="true"/>。</returns>
    /// <remarks>先端はコア（<see cref="AnnotationDocument.SetTail"/>）が画像の内側へ収める。</remarks>
    public bool UpdateSelectedTail(Func<BubbleTail, BubbleTail> transform)
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return false;
        }

        PrepareUndo();
        if (!_document.SetTail(bubble.Id, transform(bubble.Tail)))
        {
            _undoCandidate = null;
            return false;
        }

        RaiseDocumentChanged();
        Invalidate();
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
        Invalidate();
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
        Invalidate();
        return true;
    }

    /// <summary>変更を元に戻す。</summary>
    /// <returns>戻した場合は <see langword="true"/>。</returns>
    public bool Undo() => ApplyHistory(_history.Undo(_document));

    /// <summary>変更をやり直す。</summary>
    /// <returns>やり直した場合は <see langword="true"/>。</returns>
    public bool Redo() => ApplyHistory(_history.Redo(_document));

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

            var plan = _presenter.ResolveDrawPlan(_document, AnnotationTextMetrics.Measure, null);
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

    /// <summary>選択中の注釈を 1 か所だけ変更する。</summary>
    /// <param name="change">変更する処理。</param>
    /// <returns>変えた場合は <see langword="true"/>。</returns>
    private bool UpdateSelected(Action<AnnotationItem> change)
    {
        if (Selected is not { } item)
        {
            return false;
        }

        PrepareUndo();
        change(item);
        RaiseDocumentChanged();
        Invalidate();
        return true;
    }

    /// <summary>選択中の注釈を選択する。</summary>
    /// <param name="id">選択する注釈の識別子（解除は <see langword="null"/>）。</param>
    private void Select(string? id)
    {
        if (string.Equals(SelectedId, id, StringComparison.Ordinal))
        {
            return;
        }

        if (IsEditingText)
        {
            EndTextEditing();
        }

        SelectedId = id;
        if (Selected is { } item)
        {
            _marquee.SetSelection(item.Bounds);
        }
        else
        {
            _marquee.ResetSelection();
        }

        RaiseSelectionChanged();
        Invalidate();
    }

    /// <summary>選択の変更を通知する。</summary>
    private void RaiseSelectionChanged() => SelectionChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>これから変更することを履歴へ準備する（変更の直前に呼ぶ）。</summary>
    private void PrepareUndo() => _undoCandidate ??= _document.Clone();

    /// <summary>文書が変わったことを通知する（控えた文書があれば履歴へ積む）。</summary>
    private void RaiseDocumentChanged()
    {
        if (_undoCandidate is { } candidate)
        {
            _history.Record(candidate);
            _undoCandidate = null;
        }

        DocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>履歴から取り出した文書を適用する。</summary>
    /// <param name="document">適用する文書（無い場合は何もしない）。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    private bool ApplyHistory(AnnotationDocument? document)
    {
        if (document is null)
        {
            return false;
        }

        EndTextEditing();
        _document = document;
        if (SelectedId is { } id && _document.Find(id) is null)
        {
            SelectedId = null;
        }

        _marquee.ResetSelection();
        if (Selected is { } item)
        {
            _marquee.SetSelection(item.Bounds);
        }

        RaiseSelectionChanged();
        RaiseDocumentChanged();
        Invalidate();
        return true;
    }

    /// <summary>画像の表示倍率と位置を求める（縦横比を保って中央に置く）。</summary>
    /// <returns>倍率と左上の位置（DIP）。</returns>
    internal (double Scale, EsPoint Offset) ResolveImageTransform()
    {
        // 表示矩形の検証（縦横比・レイヤー内か）はコアが単一所有する（4 つの UI 版で同じ結果にする）
        return AnnotationImageLayout.Resolve(
            new EsSize(ClientSize.Width, ClientSize.Height),
            new EsSize(SourcePixelWidth, SourcePixelHeight),
            PreviewImageRect is { Width: > 0, Height: > 0 } preview ? preview : null);
    }

    /// <summary>ローカル DIP を元画像ピクセルへ変換する。</summary>
    /// <param name="dip">ローカル DIP の座標。</param>
    /// <returns>元画像ピクセルの座標。</returns>
    internal EsPoint ToSource(EsPoint dip)
    {
        var (scale, offset) = _frozenTransform ?? ResolveImageTransform();
        return new EsPoint((dip.X - offset.X) / scale, (dip.Y - offset.Y) / scale);
    }

    /// <summary>押下中の座標変換を固定する（ドラッグ中に倍率が変わってもカーソルと 1:1 で動かす）。</summary>
    private void FreezeTransform() => _frozenTransform = ResolveImageTransform();

    /// <summary>固定した変換を解除する（ポインターを離したら呼ぶ）。</summary>
    private void ReleaseTransform() => _frozenTransform = null;

    /// <summary>元画像ピクセルをローカル DIP へ変換する。</summary>
    /// <param name="source">元画像ピクセルの座標。</param>
    /// <returns>ローカル DIP の座標。</returns>
    internal EsPoint ToClientDip(EsPoint source)
    {
        var (scale, offset) = ResolveImageTransform();
        return new EsPoint(offset.X + (source.X * scale), offset.Y + (source.Y * scale));
    }

    /// <summary>選択中の注釈の矩形（DIP）を取得する。</summary>
    /// <returns>矩形（DIP）。 選択していない場合は空。</returns>
    /// <remarks>
    /// <b>書式バーを注釈に被せないための対象矩形</b>である。 本文の矩形ではなく枠線を含む矩形を使う
    /// （本文で判定するとバーが枠線の上へ数ピクセル重なる。 実測 2026-11-22）。
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
