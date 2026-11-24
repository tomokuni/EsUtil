using Microsoft.Extensions.Logging;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Palette;
using EsUtil.Feature.Annotation.Logging;
using EsUtil.Ui.AvaloniaUI.Interop;
using EsUtil.Feature.Annotation.Presenter.Panel;
using EsUtil.Feature.StyledText;
using EsUtil.Ui.AvaloniaUI.StyledText;
using EsUtil.Feature.StyledText.Presenter.Formatting;

namespace EsUtil.Ui.AvaloniaUI.Annotation;

/// <summary>注釈の本文（表示とその場編集）と書式バーを 1 つにまとめたコントロール。</summary>
/// <remarks>
/// <b>セットで使う</b>: 本文の表示・キャレット・選択範囲は <see cref="AnnotationLayerControl"/> が描き、
/// 書式バーはアプリの画面へ重ねて出す。 2 つを別々に置くと、位置合わせと表示の切り替えを
/// ホストが書くことになる（サンプルごとにずれる）。<br/>
/// <b>位置と開閉はコアが決める</b>: <see cref="AnnotationPanelController"/> が「どこへ出すか・ドラッグで
/// どう動くか・いつ閉じるか」を単一所有する。 本クラスは <see cref="IAnnotationPanelHost"/> を実装し、
/// パネルの作成・移動・削除だけを行う（4 つの UI 版で動作を揃えるため）。<br/>
/// <b>パネルは枠なしのウィンドウへ載せる</b>: アプリの画面へ重ねるとウィンドウの外へ出られない。
/// ウィンドウなら画面の内側まで自由に置け、はみ出さないことは <see cref="ApplyPosition"/> が保証する。<br/>
/// <b>状態は Panel ではなくこの型が持つ</b>: <see cref="AnnotationLayerControl"/> は画像と注釈だけを扱う。
/// </remarks>
public sealed class AnnotationEditorPanel : Panel, IAnnotationPanelHost
{
    /// <summary>書式バー（編集中だけ表示する）。 独自の GUI へ差し替えられる。</summary>
    private IStyledTextFormatBar _formatBar;

    /// <summary>書式バーを作る処理（差し替えの入口）。</summary>
    private IStyledTextFormatBarFactory _formatBarFactory = new StyledTextFormatBarFactory();

    /// <summary>注釈の表示と編集を担うレイヤー。</summary>
    private readonly AnnotationLayerControl _layer;

    /// <summary>書式バーを出すコントローラー。</summary>
    private readonly AnnotationPanelController _formatBarPanel;

    /// <summary>右クリックの外観フライアウトを出すコントローラー。</summary>
    private readonly AnnotationPanelController _stylePanel;

    /// <summary>表示中のパネルの根（種別ごとに 1 つ。 使い回す）。</summary>
    private readonly Dictionary<AnnotationPanelKind, Border> _roots = [];

    /// <summary>パネルを載せる別ウィンドウ（種別ごとに 1 つ。 アプリのウィンドウの内側へ置く）。</summary>
    private readonly Dictionary<AnnotationPanelKind, Window> _windows = [];

    /// <summary>パネルを置いた位置（種別ごと。 アプリの画面の左上を原点とする DIP）。</summary>
    /// <remarks>
    /// <b>置いた位置を控える</b>: パネルの根はウィンドウの中にあり、位置は <see cref="Canvas.GetLeft"/>
    /// から読めません。 外側の押下の判定はこの値を使います（<see cref="AnnotationPanelController.CloseIfOutside"/>）。
    /// </remarks>
    private readonly Dictionary<AnnotationPanelKind, EsPoint> _positions = [];


    /// <summary>ドラッグ中のパネルの根（押下を受けた要素）。</summary>
    private Border? _draggingRoot;

    /// <summary>外側の押下を監視している最上位（繋ぎ直しの判定に使う）。</summary>
    private TopLevel? _hookedTop;

    /// <summary>移動を監視しているアプリのウィンドウ（繋ぎ直しの判定に使う）。</summary>
    private Window? _hookedWindow;

    /// <summary>本文の表示と編集を担うコントロール（選択中の吹き出しの本文の上へ重ねる）。</summary>
    private readonly StyledText.StyledTextControl _textEditor;

    /// <summary>本文コントロールを合わせている吹き出しの識別子（無い場合は <see langword="null"/>）。</summary>
    private string? _syncedTextId;

    /// <summary>本文コントロールを最後に置いた矩形（移動の検出に使う）。</summary>
    private Rect _syncedTextRect;

    /// <summary>レイアウトの処理中かどうか（処理中は無効化を要求しない）。</summary>
    private bool _isInLayout;

    /// <summary>注釈の本文編集と書式バーをまとめたコントロールを初期化する。</summary>
    public AnnotationEditorPanel()
    {
        _layer = new AnnotationLayerControl();

        // 下地を持たないとパネルが入力の対象にならない（Avalonia は背景が無い面をヒットテストしない）
        Background = Brushes.Transparent;

        _formatBarPanel = new AnnotationPanelController(this, AnnotationPanelAppearance.FormatBar, () => _layer.Log);
        _stylePanel = new AnnotationPanelController(this, AnnotationPanelAppearance.StyleEditor, () => _layer.Log);

        // **本文の表示と編集は専用のコントロールが担う**: 選択中の吹き出しの本文の上へ重ね、
        // 本文の文字・選択範囲・キャレット・変換中の文字列を描く（レイヤーは本文を描かない）。
        _textEditor = new StyledText.StyledTextControl
        {
            IsInputEnabled = false,
            IsHitTestVisible = false,
            Focusable = false,
            IsVisible = false,
            MeasureText = AnnotationPainter.Measure,
            LineHeightFactor = _layer.LineHeightFactor,
            Editor = _layer.Session.TextEditor,
        };

        Children.Add(_layer);
        Children.Add(_textEditor);

        // **書式バーは本文コントロールの後に作る**: バーは本文へのフォーカスとキー転送を受け取る
        _formatBar = CreateFormatBar();

        // 右クリックの外観フライアウト（位置はレイヤーが知らせる）
        _layer.StyleEditorRequested += (_, e) => ShowStylePanel(e.X, e.Y);

        // 編集の開始・終了でバーを出し入れする（本文の位置は編集のたびに変わりうる）
        _layer.EditingChanged += (_, _) =>
        {
            if (_layer.IsEditingText)
            {
                // **バーは選択ができてから出す**: 編集中でも 1 文字以上を選ぶまでは出さない
                UpdateFormatBarVisibility();

                // **フォーカスは編集の開始後に移す**: レイアウトの途中で移すと取り消される環境がある
                Dispatcher.UIThread.Post(
                    () =>
                    {
                        if (_layer.IsEditingText)
                        {
                            _textEditor.Focus();
                        }
                    },
                    DispatcherPriority.Input);
                return;
            }

            _formatBarPanel.Close();
        };

        // 文字・書式の変更で表示を追随させる（バーの現在値が古くならないようにする）
        _layer.DocumentChanged += (_, _) =>
        {
            if (_formatBarPanel.IsOpen)
            {
                _formatBar.Refresh();
            }

            SyncTextEditor();
        };

        // 移動・サイズ変更で本文の位置が変わるため、バーの位置を追随させる
        _layer.SelectionChanged += (_, _) =>
        {
            UpdateFormatBarVisibility();
            SyncTextEditor();

            // 外観パネルは「選んでいる注釈」を編集するため、選択が変わったら閉じる
            if (!_layer.IsEditingText)
            {
                _stylePanel.Close();
            }
        };

        // 選択範囲の変更で、書式バーの表示と押下状態（太字・斜体・下線・取り消し線）を追随させる
        _layer.TextSelectionChanged += (_, _) =>
        {
            UpdateFormatBarVisibility();
            SyncTextEditor();
        };

        // 編集の開始・終了でも本文の描画先を切り替える
        _layer.EditingChanged += (_, _) => SyncTextEditor();

        // **入力・編集・IME は本文コントロールが単一所有する**: 編集中は本コントロールへフォーカスを移し、
        // キー・IME・キャレット・選択・クリップボードをすべてコントロールに任せる（レイヤーは図形だけを持つ）。
        _textEditor.RichTextChanged += (_, _) => OnTextEditorChanged();
        _textEditor.EditingChanged += (_, _) => OnTextEditorEditingChanged();

        // **選択の変更は 2 か所へ伝える**: 書式バーの表示と、レイヤーの購読者（ホスト）への通知。
        _textEditor.SelectionChanged += (_, _) =>
        {
            _layer.NotifyTextSelectionChanged();
            UpdateFormatBarVisibility();
        };
    }

    /// <summary>本文の表示と編集を担うコントロールを取得する（テストと差し替えの入口）。</summary>
    internal StyledText.StyledTextControl TextEditor => _textEditor;

    /// <summary>選択中の本文をクリップボードへコピーする。</summary>
    /// <remarks>
    /// <b>クリップボードの処理は本文コントロールが単一所有する</b>: 注釈のレイヤーは持ちません
    /// （キーと API の両方から同じ処理を呼べるよう、入口だけをここに置きます）。
    /// </remarks>
    public void CopyTextSelection()
    {
        _layer.Log.LogTextOperation(
            "コピー", _textEditor.CaretIndex, _textEditor.SelectionStart, _textEditor.SelectionLength, _textEditor.SelectedText());
        _ = _textEditor.CopyAsync();
    }

    /// <summary>選択中の本文をクリップボードへ切り取る。</summary>
    public void CutTextSelection()
    {
        _layer.Log.LogTextOperation(
            "切り取り", _textEditor.CaretIndex, _textEditor.SelectionStart, _textEditor.SelectionLength, _textEditor.SelectedText());
        _ = _textEditor.CutAsync();
    }

    /// <summary>クリップボードの文字列をキャレットの位置へ貼り付ける。</summary>
    public void PasteText() => _ = PasteAndLogAsync();

    /// <summary>クリップボードの文字列を貼り付け、操作の内容と値を記録する。</summary>
    /// <returns>貼り付けの非同期処理。</returns>
    private async Task PasteAndLogAsync()
    {
        var inserted = await _textEditor.PasteAsync();
        if (inserted is null)
        {
            return;
        }

        _layer.Log.LogTextOperation(
            "貼り付け", _textEditor.CaretIndex, _textEditor.SelectionStart, _textEditor.SelectionLength, inserted);
    }
    /// <summary>注釈の表示と編集を担うレイヤーを取得する。</summary>
    /// <value>画像・文書・選択・元に戻すなどを操作する対象。</value>
    public AnnotationLayerControl Layer => _layer;

    /// <summary>ログの出力先を取得または設定する（レイヤーへ委譲する）。</summary>
    /// <value>ロガー。 null を設定すると出力しない。</value>
    public ILogger Log
    {
        get => _layer.Log;
        set => _layer.Log = value;
    }

    /// <summary>ログの接頭辞を取得または設定する（レイヤーへ委譲する）。</summary>
    /// <value>接頭辞（例: <c>[Annotation]</c>）。 null または空白を設定すると既定値へ戻す。</value>
    public string? LogPrefix
    {
        get => _layer.LogPrefix;
        set => _layer.LogPrefix = value;
    }
    /// <summary>本文を編集中かどうかを取得する。</summary>
    /// <value>編集中の場合は <see langword="true"/>。 書式バーは 1 文字以上を選んでいる間だけ出る。</value>
    public bool IsEditingText => _layer.IsEditingText;

    /// <summary>編集中に書式バーを自動で表示するかどうかを取得または設定する。</summary>
    /// <value>自動で表示する場合は <see langword="true"/>（既定）。 ホストが独自にバーを出す場合は <see langword="false"/>。</value>
    /// <remarks>
    /// <b>表示の条件は本文コントロールが単一所有する</b>: 編集中であること・自動表示が有効であること・
    /// 1 文字以上を選んでいることの 3 つを満たす間だけ表示する（<c>StyledTextControl.ShouldShowFormatBar</c>）。
    /// </remarks>
    public bool ShowFormatBarWhileEditing
    {
        get => _textEditor.ShowFormatBarWhileEditing;
        set
        {
            var previous = _textEditor.ShowFormatBarWhileEditing;
            _textEditor.ShowFormatBarWhileEditing = value;
            _layer.Log.LogSettingChanged(nameof(ShowFormatBarWhileEditing), previous, value);
            UpdateFormatBarVisibility();
        }
    }

    /// <summary>書式バーを作る処理を取得または設定する（独自の GUI へ差し替える入口）。</summary>
    /// <value>既定は組み込みの書式バー（<see cref="StyledTextFormatBarFactory"/>）。</value>
    /// <remarks>
    /// <b>差し替えはバーを出す前に行う</b>: 表示中のバーを入れ替えると、位置と操作の対象が食い違うため、
    /// 表示中は <see cref="InvalidOperationException"/> になる。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><see langword="null"/> を設定した場合。</exception>
    /// <exception cref="InvalidOperationException">書式バーを表示中に設定した場合。</exception>
    public IStyledTextFormatBarFactory FormatBarFactory
    {
        get => _formatBarFactory;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (_formatBarPanel.IsOpen)
            {
                throw new InvalidOperationException("書式バーを表示中は差し替えできません。");
            }

            _formatBarFactory = value;
            _roots.Remove(AnnotationPanelKind.FormatBar);
            _formatBar = CreateFormatBar();
        }
    }

    /// <summary>書式バーの構成（フォントの一覧・色のタブ・色の表記）を取得する。</summary>
    /// <value>注釈の代表色を書式バーの色の選択へ渡す構成。</value>
    public StyledTextFormatBarOptions FormatBarOptions { get; } = new()
    {
        ConfigureColorSelector = (selector, columns) => AnnotationPalette.Configure(selector, columns, showPicker: true),
        PaletteColumns = AnnotationPalette.DefaultPaletteColumns,
        MeasureText = AnnotationPainter.Measure,
    };

    /// <summary>書式バーの代表色（■ を並べる格子）の列数を取得または設定する。</summary>
    /// <value>1 以上の列数。 既定は <see cref="AnnotationPalette.DefaultPaletteColumns"/>（8 列）。</value>
    /// <remarks>組み込みの書式バーだけに効く（差し替えたバーは独自の設定に従う）。</remarks>
    public int PaletteColumns
    {
        get => (_formatBar as StyledTextFormatBar)?.PaletteColumns ?? FormatBarOptions.PaletteColumns;
        set
        {
            if (_formatBar is StyledTextFormatBar bar)
            {
                bar.PaletteColumns = value;
            }
        }
    }

    /// <summary>書式バーの色の選択の幅（DIP）を取得する。</summary>
    /// <value>「■ + 全角 4 文字 + 100%」と「■ + <c>#FFFFFFFF</c>」の広いほうの幅。</value>
    public double ColorWidth => (_formatBar as StyledTextFormatBar)?.ColorWidth ?? _formatBar.PreferredWidth;

    /// <summary>書式バーの色の入力欄へ出す表記を取得または設定する。</summary>
    /// <value>色名 + 不透明度（既定）またはカラーコード（<c>#AARRGGBB</c>）。</value>
    /// <remarks>
    /// <b>右クリックのフライアウトと揃える</b>: 同じ値を <see cref="AnnotationLayerControl.ColorTextFormat"/> へも写す
    /// （書式バーとフライアウトで色の見え方が食い違わない）。
    /// </remarks>
    public ColorTextFormat TextFormat
    {
        get => _formatBar.TextFormat;
        set
        {
            _formatBar.TextFormat = value;
            _layer.ColorTextFormat = value;
        }
    }

    /// <summary>パネルを置ける領域（DIP）を取得する。</summary>
    /// <value>注釈レイヤーが占める範囲を外側へ広げ、重ねる先の見えている範囲との交わりを取ったもの。</value>
    /// <remarks>
    /// <b>ウィンドウの内側へ収める</b>: 重ねる先（<see cref="OverlayLayer"/>）の外は切れて見えなくなる。
    /// レイヤーの外側へも置けるよう、領域は <see cref="AnnotationPanelMetrics.OutsideAllowance"/> だけ
    /// 広げてから交わりを取る（規則は <see cref="AnnotationPanelPlacement.ResolveAvailableArea"/> が
    /// 単一所有する）。
    /// </remarks>
    public EsRect AvailableArea
    {
        get
        {
            if (Bounds.Width <= 0 || Bounds.Height <= 0)
            {
                return default;
            }

            var origin = ToOverlayPoint(default);
            var layer = new EsRect(origin.X, origin.Y, Bounds.Width, Bounds.Height);
            return AnnotationPanelPlacement.ResolveAvailableArea(
                layer, VisibleBounds(), AnnotationPanelMetrics.OutsideAllowance);
        }
    }

    /// <summary>重ねる先の見えている範囲（DIP）を取得する。</summary>
    /// <returns>重ねる先の範囲。 取得できない場合は既定値。</returns>
    private EsRect VisibleBounds()
        => OverlayLayer.GetOverlayLayer(this) is { } overlay
            && overlay.Bounds.Width > 0 && overlay.Bounds.Height > 0
                ? new EsRect(0, 0, overlay.Bounds.Width, overlay.Bounds.Height)
                : default;

    /// <summary>表示中のパネルの左上（DIP）を取得する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <returns>左上の位置。 表示していない場合は原点。</returns>
    /// <remarks>
    /// <b>置いた位置を控えて返す</b>: パネルの根は <see cref="Canvas"/> ではなくウィンドウの中にあり、
    /// <see cref="Canvas.GetLeft"/> は常に <see cref="double.NaN"/> です。 根から読むと常に原点になり、
    /// 外側の押下の判定が「内側」と誤って閉じなくなります。
    /// </remarks>
    public EsPoint GetPanelPosition(AnnotationPanelKind kind)
        => _positions.TryGetValue(kind, out var position) ? position : default;

    /// <summary>表示中のパネルの大きさ（DIP）を取得する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <returns>パネルの大きさ。 表示していない場合は原点。</returns>
    public EsSize GetPanelSize(AnnotationPanelKind kind)
        => _roots.TryGetValue(kind, out var root) && root.Bounds.Width > 0
            ? new EsSize(root.Bounds.Width, root.Bounds.Height)
            : MeasurePanel(kind);

    /// <summary>パネルを作って表示する。</summary>
    /// <param name="kind">パネルの種別（中身を決める）。</param>
    /// <param name="appearance">外観（枠・余白・幅）。</param>
    /// <param name="position">左上の位置（アプリの画面の左上を原点とする DIP）。</param>
    public void CreatePanel(
        AnnotationPanelKind kind, AnnotationPanelAppearance appearance, EsPoint position)
    {
        RemovePanel(kind);

        if (TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        // **根は種別ごとに 1 度だけ作って使い回す**: 書式バーは作り直さない（購読が増えるため）。
        // 根を作り直すと、前の根が書式バーを子として保持したままになり、付け替えに失敗する。
        if (!_roots.TryGetValue(kind, out var root))
        {
            root = new Border
            {
                Child = kind == AnnotationPanelKind.FormatBar
                    ? _formatBar.View
                    : AnnotationStyleFlyout.BuildContent(_layer, appearance),
            };

            _roots[kind] = root;
        }

        // **中身は表示のたびに作り直す**: 吹き出しの設定は「吹き出しを選んでいるとき」だけ出す。
        // 一度作った中身を使い回すと、最初に枠線を右クリックした後は吹き出しの設定が出ない。
        if (kind != AnnotationPanelKind.FormatBar)
        {
            root.Child = AnnotationStyleFlyout.BuildContent(_layer, appearance);
        }
        else
        {
            // 高さはコアの固定値で確定する（見積りと実寸のずれで注釈に重なるのを防ぐ）
            root.Height = appearance.ResolvePanelHeight(appearance.FixedContentHeight);
            root.Width = MeasureFormatBar().Width;
        }

        root.Background = AnnotationPainter.ToBrush(appearance.Background);
        root.BorderBrush = AnnotationPainter.ToBrush(appearance.Border);
        root.BorderThickness = new Thickness(appearance.BorderThickness);
        root.CornerRadius = new CornerRadius(appearance.CornerRadius);
        root.Padding = new Thickness(
            appearance.Padding, appearance.VerticalPadding,
            appearance.Padding, appearance.VerticalPadding);

        // 余白（中身の無いところ）を押したまま動かすとパネルが動く
        root.PointerPressed += (_, e) => BeginPanelDrag(kind, root, e);
        root.PointerMoved += (_, e) => ContinuePanelDrag(kind, root, e);
        root.PointerReleased += (_, e) => EndPanelDrag(kind, e);

        // **別ウィンドウへ載せる**: ポップアップ（<see cref="Popup"/>）の子にすると、書式バーの列が
        // 潰れて中身（色・大きさ・フォント）が欠ける。 枠なしのウィンドウなら
        // レイアウトは普通のウィンドウと同じで、ウィンドウの外（画面の内側）へも出せます。
        if (!_windows.TryGetValue(kind, out var window))
        {
            window = new Window
            {
                WindowDecorations = Avalonia.Controls.WindowDecorations.None,
                ShowInTaskbar = false,
                CanResize = false,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Topmost = false,
                Content = root,
            };

            _windows[kind] = window;

            // **フォーカスが外れたら閉じる**: アプリの外（他のアプリ）や別ウィンドウを押した場合、
            // アプリの画面の押下は届かないため、外側の押下の監視だけでは閉じられません。
            window.Deactivated += (_, _) => CloseOnFocusLost(kind);
            window.Show(top as Window ?? (Window)top);
        }

        // **隠したウィンドウは出し直す**: いったん閉じた（<c>Hide</c> した）ウィンドウは、
        // そのままでは二度と表示されない（外側を押して書式バーを消した後、注釈を
        // ダブルクリックしても出なくなった）
        if (!window.IsVisible)
        {
            window.Show(top as Window ?? (Window)top);
        }

        ApplyPosition(kind, position);

        // **レイアウトが確定してから置き直す**: 出した直後の位置指定は、ウィンドウが自分の寸法を
        // 確定する際に捨てられることがある。 次の描画で置き直す。
        Dispatcher.UIThread.Post(
            () => ApplyPosition(kind, position), DispatcherPriority.Loaded);
    }

    /// <summary>パネルを移動する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="position">左上の位置（アプリの画面の左上を原点とする DIP）。</param>
    public void MovePanel(AnnotationPanelKind kind, EsPoint position) => ApplyPosition(kind, position);
    /// <summary>表示中のパネルを画面から外す（根と中身は残す）。</summary>
    /// <param name="kind">パネルの種別。</param>
    public void DestroyPanel(AnnotationPanelKind kind) => RemovePanel(kind);

    /// <summary>右クリック位置へ外観フライアウトを出す（既に出ていれば閉じる）。</summary>
    /// <param name="x">位置（このコントロールの座標）。</param>
    /// <param name="y">位置（このコントロールの座標）。</param>
    /// <remarks>
    /// <b>位置と寿命はコアが決める</b>: 置ける領域（キャプチャ ページ）の内側へ収め、右クリックした位置へ出し、
    /// 外側をクリックすると閉じる。 余白を押したまま動かすと移動する。
    /// </remarks>
    public void ShowStylePanel(double x, double y)
    {
        EnsureOutsidePressHook();
        _stylePanel.Show(AnnotationPanelKind.StyleEditor, ToOverlayPoint(new Point(x, y)));
        _stylePanel.Constrain(MeasurePanel(AnnotationPanelKind.StyleEditor));
    }

    /// <summary>外観フライアウトを閉じる（出ていなければ何もしない）。</summary>
    public void CloseStylePanel() => _stylePanel.Close();

    /// <summary>外観フライアウトが出ているかどうかを取得する。</summary>
    public bool IsStylePanelOpen => _stylePanel.IsOpen;

    /// <summary>書式バーを表示中かどうかを取得する。</summary>
    /// <value>編集中で 1 文字以上を選んでいる間は <see langword="true"/>。</value>
    public bool IsFormatBarOpen => _formatBarPanel.IsOpen;

    /// <summary>現在の編集状態に合わせて書式バーを出すか閉じるかを決める。</summary>
    /// <remarks>
    /// <b>条件は本文コントロールが持つ</b>: 編集中で、自動表示が有効で、1 文字以上を選んでいる間だけ出す。
    /// 選択が無い（適用先が決まらない）間は出さない。 位置は <see cref="ShowFormatBar"/> が決める。
    /// </remarks>
    private void UpdateFormatBarVisibility()
    {
        if (!_textEditor.ShouldShowFormatBar)
        {
            _formatBarPanel.Close();
            return;
        }

        _formatBar.Refresh();
        ShowFormatBar();
    }

    /// <summary>書式バーを本文の外（本文に被さらない位置）へ出し直す。</summary>
    /// <remarks>
    /// <b>本文に被せない</b>: 編集中の文字が隠れて読めない。 位置の判断は
    /// 提示層（<see cref="AnnotationPanelPlacement.ResolveOutside"/>）が単一所有する。
    /// <b>大きさは実寸を使う</b>: 中身（フォント名・大きさ・色）で幅が決まるため、固定値だと
    /// 枠からはみ出す。 一度置いてから実寸で出し直す。
    /// </remarks>
    private void ShowFormatBar()
    {
        EnsureOutsidePressHook();
        var body = ToOverlayRect(_layer.GetSelectedBoundsRect().ToEsRect());
        _formatBarPanel.ShowOutside(
            AnnotationPanelKind.FormatBar, body, MeasureFormatBar(), AnnotationPanelMetrics.FormatBarGap);

        // レイアウト後の実寸で置き直す（レイアウト前の測定は高さを正しく返さないことがある）
        _formatBarPanel.RepositionOutside(body, AnnotationPanelMetrics.FormatBarGap);
    }

    /// <summary>書式バーを作り、キーとフォーカスの転送先を配線する。</summary>
    /// <returns>書式バー。</returns>
    private IStyledTextFormatBar CreateFormatBar()
    {
        var bar = _formatBarFactory.Create(new LayerFormatBarContext(_layer), FormatBarOptions);

        // **本文のキー操作とフォーカスは本文コントロールが持つ**: 書式バーは操作を転送するだけにする
        bar.SetKeyHandler(_textEditor.TryHandleEditingKey);
        bar.SetFocusRequest(() =>
        {
            if (_layer.IsEditingText)
            {
                _textEditor.Focus();
            }
        });
        bar.TextFormat = FormatBarOptions.TextFormat;
        return bar;
    }

    /// <summary>書式バーの大きさ（DIP）を測る。</summary>
    /// <returns>実寸。 測れない場合は希望寸法。</returns>
    private EsSize MeasureFormatBar()
    {
        // **中身の希望寸法から求める**: パネルの実寸（Bounds）は置いた幅へ切り詰められているため、
        // それを使うと右端が毎回わずかに切れる（測定と配置が互いを参照して縮む）。
        _formatBar.View.Measure(Size.Infinity);
        var desired = _formatBar.View.DesiredSize;
        return AnnotationPanelAppearance.FormatBar.ResolvePanelSize(new EsSize(desired.Width, desired.Height));
    }

    /// <summary>パネルの大きさ（DIP）を測る。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <returns>実寸。 まだ置いていない場合は希望寸法。</returns>
    private EsSize MeasurePanel(AnnotationPanelKind kind)
    {
        if (!_roots.TryGetValue(kind, out var root))
        {
            return default;
        }

        if (root.Bounds.Width > 0 && root.Bounds.Height > 0)
        {
            return new EsSize(root.Bounds.Width, root.Bounds.Height);
        }

        root.Measure(Size.Infinity);
        var desired = root.DesiredSize;
        return new EsSize(desired.Width, desired.Height);
    }

    private void RemovePanel(AnnotationPanelKind kind)
    {
        if (_windows.TryGetValue(kind, out var window))
        {
            window.Hide();
        }
    }

    /// <summary>パネルのウィンドウからフォーカスが外れたときに、外観フライアウトを閉じる。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <remarks>
    /// <b>書式バーは閉じない</b>: 書式バーは本文の編集中だけ出すもので、押すたびにフォーカスが
    /// 移るためです（閉じると書式を変えられません）。<br/>
    /// <b>1 度待ってから決める</b>: パネルの中のポップアップ（色の一覧）が開くと、一瞬だけ
    /// ウィンドウが非アクティブになることがあります。 すぐに閉じると色を選べません。
    /// </remarks>
    private void CloseOnFocusLost(AnnotationPanelKind kind)
    {
        if (kind != AnnotationPanelKind.StyleEditor
            || !_stylePanel.IsOpen
            || !_windows.TryGetValue(kind, out var window))
        {
            return;
        }

        Dispatcher.UIThread.Post(
            () =>
            {
                if (_stylePanel.IsOpen && !window.IsActive)
                {
                    _stylePanel.Close();
                }
            },
            DispatcherPriority.Background);
    }

    /// <summary>パネルの位置を別ウィンドウへ適用する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="position">左上の位置（アプリの画面の左上を原点とする DIP）。</param>
    /// <remarks>
    /// <b>ウィンドウの位置は画面座標（ピクセル）</b>であるため、位置（画面の DIP）を表示倍率で写します
    /// （125% の画面では DIP のまま置くと 25% ずれます）。<br/>
    /// <b>画面の外へは出さない</b>: 実寸を表示倍率で写し、画面の作業領域へ収めます。 収めた後の位置を
    /// 控えるため、外側の押下の判定（<see cref="GetPanelPosition"/>）と実際の位置が食い違いません。
    /// </remarks>
    private void ApplyPosition(AnnotationPanelKind kind, EsPoint position)
    {
        if (!_windows.TryGetValue(kind, out var window) || TopLevel.GetTopLevel(this) is not Window main)
        {
            return;
        }

        _positions[kind] = position;

        // **位置はアプリの画面（ウィンドウの中）の DIP**: 画面のピクセルへ写すときに、ウィンドウの
        // 位置（ピクセル）を足します。 足さないと、ウィンドウを動かした分だけパネルがずれます。
        var scale = main.RenderScaling > 0 ? main.RenderScaling : 1;
        var size = ResolvePanelSize(kind);
        var x = main.Position.X + (int)Math.Round(position.X * scale);
        var y = main.Position.Y + (int)Math.Round(position.Y * scale);
        if (main.Screens.ScreenFromWindow(main) is { } screen)
        {
            // **作業領域はピクセル**であるため、パネルの実寸もピクセルへ写してから挟む
            // （収める規則は提示層の AnnotationPanelPlacement.Clamp が単一所有する）
            var area = screen.WorkingArea;
            var clamped = AnnotationPanelPlacement.Clamp(
                new EsPoint(x, y),
                Math.Round(size.Width * scale),
                Math.Round(size.Height * scale),
                new EsRect(area.X, area.Y, area.Width, area.Height));
            x = (int)Math.Round(clamped.X);
            y = (int)Math.Round(clamped.Y);

            // 実際に置いた位置を控える（外側の押下の判定に使う）
            _positions[kind] = new EsPoint(
                (x - main.Position.X) / scale,
                (y - main.Position.Y) / scale);
        }

        window.Position = new PixelPoint(x, y);
    }

    /// <summary>パネルの実寸（DIP）を求める。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <returns>実寸。 測れない場合は <see cref="GetPanelSize"/> の見積り。</returns>
    /// <remarks>
    /// <b><c>Width</c> は使わない</b>: ウィンドウは中身に合わせて大きさを決めるため
    /// （<see cref="SizeToContent.WidthAndHeight"/>）、<c>Width</c> は <see cref="double.NaN"/> であり、
    /// ピクセルへ写すと 0 になって画面の外への判定が効きません。
    /// </remarks>
    private EsSize ResolvePanelSize(AnnotationPanelKind kind)
    {
        if (_windows.TryGetValue(kind, out var window)
            && window.Bounds is { Width: > 0, Height: > 0 } bounds)
        {
            return new EsSize(bounds.Width, bounds.Height);
        }

        return GetPanelSize(kind);
    }
    /// <summary>このコントロールの座標（DIP）を重ねた先の座標（DIP）へ変換する。</summary>
    /// <param name="point">このコントロールの座標。</param>
    /// <returns>重ねた先の座標。 取得できない場合はそのまま返す。</returns>
    /// <remarks>
    /// <b>変換は視覚ツリーに任せる</b>: 画面座標を経由すると点の基準が呼び出し元とずれ、
    /// フライアウトが右クリック位置から離れた場所へ出る。
    /// </remarks>
    private EsPoint ToOverlayPoint(Point point)
    {
        if (OverlayLayer.GetOverlayLayer(this) is { } overlay
            && this.TranslatePoint(point, overlay) is { } local)
        {
            return new EsPoint(local.X, local.Y);
        }

        return new EsPoint(point.X, point.Y);
    }

    /// <summary>このコントロールの矩形（DIP）を重ねた先の座標（DIP）へ変換する。</summary>
    /// <param name="rect">このコントロールの矩形。</param>
    /// <returns>重ねた先の矩形。</returns>
    private EsRect ToOverlayRect(EsRect rect)
    {
        var origin = ToOverlayPoint(new Point(rect.X, rect.Y));
        return new EsRect(origin.X, origin.Y, rect.Width, rect.Height);
    }


    /// <summary>パネルのドラッグを始める（余白を押したときだけ）。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="root">パネルの根。</param>
    /// <param name="e">押下の引数。</param>
    private void BeginPanelDrag(AnnotationPanelKind kind, Border root, PointerPressedEventArgs e)
    {
        // 中身（ボタン・入力欄）の押下では動かさない（操作を奪わない）。
        // 余白（中身の無い面・ラベル）の押下は動かす（根そのものを押すとは限らない）。
        if (IsInteractiveSource(e.Source, root)
            || !e.GetCurrentPoint(root).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _draggingRoot = root;
        Controller(kind).BeginDrag(ToOverlayPoint(e.GetPosition(this)));
        e.Pointer.Capture(root);
        e.Handled = true;
    }

    /// <summary>押下の発生元が操作可能な要素かどうかを判定する（パネルのドラッグ可否）。</summary>
    /// <param name="source">押下の発生元。</param>
    /// <param name="root">パネルの根。</param>
    /// <returns>操作可能な要素（フォーカスを持つ要素）から始まった場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>根そのものを押すとは限らない</b>: 書式バーの余白やラベルを押すと発生元は子要素になる。
    /// 発生元がフォーカスを持つ要素（ボタン・入力欄・一覧など）のときだけドラッグを止める。
    /// </remarks>
    private static bool IsInteractiveSource(object? source, Border root)
    {
        for (var current = source as Visual; current is not null; current = current.GetVisualParent())
        {
            if (ReferenceEquals(current, root))
            {
                return false;
            }

            if (current is InputElement { Focusable: true })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>パネルの外側の押下を監視する（まだ監視していない場合だけ繋ぐ）。</summary>
    /// <remarks>
    /// <b>外側の押下で閉じる</b>: 重ねた先はアプリの画面の最上位のため、外側の押下は他の要素へ届く。
    /// ここで拾ってコアへ知らせないと、フライアウトが開いたまま残る。
    /// </remarks>
    private void EnsureOutsidePressHook()
    {
        if (TopLevel.GetTopLevel(this) is not { } top || ReferenceEquals(top, _hookedTop))
        {
            return;
        }

        UnhookOutsidePress();
        _hookedTop = top;
        top.AddHandler(
            PointerPressedEvent, OnOutsidePointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>外側の押下の監視を外す。</summary>
    private void UnhookOutsidePress()
    {
        if (_hookedTop is { } top)
        {
            top.RemoveHandler(PointerPressedEvent, OnOutsidePointerPressed);
            _hookedTop = null;
        }
    }

    /// <summary>アプリのウィンドウが動いたらパネルを閉じる（位置は画面の絶対座標で決めるため追随しない）。</summary>
    /// <remarks>
    /// <b>追随せず閉じる</b>: 書式バーと外観フライアウトは枠なしウィンドウとして画面の絶対座標へ置く。
    /// アプリのウィンドウを動かすと、置いた位置だけが取り残されて注釈から離れる。
    /// 動いたら閉じる（右クリックのフライアウトと同じ扱い）。
    /// </remarks>
    private void EnsureWindowMoveHook()
    {
        if (TopLevel.GetTopLevel(this) is not Window window || ReferenceEquals(window, _hookedWindow))
        {
            return;
        }

        UnhookWindowMove();
        _hookedWindow = window;
        window.PositionChanged += OnWindowPositionChanged;
    }

    /// <summary>ウィンドウの移動の監視を外す。</summary>
    private void UnhookWindowMove()
    {
        if (_hookedWindow is { } window)
        {
            window.PositionChanged -= OnWindowPositionChanged;
            _hookedWindow = null;
        }
    }

    /// <summary>アプリのウィンドウが動いたときにパネルを閉じる。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">引数。</param>
    private void OnWindowPositionChanged(object? sender, PixelPointEventArgs e)
    {
        _ = sender;
        _ = e;
        _formatBarPanel.Close();
        _stylePanel.Close();
    }

    /// <summary>アプリの画面の押下をコアへ知らせ、パネルの外側なら閉じる。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">押下の引数。</param>
    private void OnOutsidePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _ = sender;
        _stylePanel.CloseIfOutside(ToOverlayPoint(e.GetPosition(this)));
    }

    /// <summary>視覚ツリーへ繋がったときに外側の押下の監視を始める。</summary>
    /// <param name="e">引数。</param>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        EnsureOutsidePressHook();
        EnsureWindowMoveHook();
    }

    /// <summary>視覚ツリーから外れたときに外側の押下の監視を止める。</summary>
    /// <param name="e">引数。</param>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        UnhookOutsidePress();
        UnhookWindowMove();
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>書式バーが書式を読み書きする相手（注釈の編集セッションへつなぐ）。</summary>
    /// <param name="layer">注釈のレイヤー。</param>
    private sealed class LayerFormatBarContext(AnnotationLayerControl layer) : ITextFormatBarContext
    {
        /// <inheritdoc/>
        public TextStyle SelectedTextStyle => layer.Session.SelectedTextStyle;

        /// <inheritdoc/>
        public bool HasTextSelection => layer.HasTextSelection;

        /// <inheritdoc/>
        public bool SelectedTextMatches(Func<TextStyle, bool> match) => layer.Session.SelectedTextMatches(match);

        /// <inheritdoc/>
        public bool Transform(Func<TextStyle, TextStyle> transform) => layer.Session.TransformTextStyle(transform);

        /// <inheritdoc/>
        public bool Apply(TextStyle style) => layer.Session.ApplyTextStyle(style);
    }

    /// <summary>パネルをドラッグして動かす。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="root">パネルの根。</param>
    /// <param name="e">ポインターの引数。</param>
    private void ContinuePanelDrag(AnnotationPanelKind kind, Border root, PointerEventArgs e)
    {
        if (!ReferenceEquals(_draggingRoot, root))
        {
            return;
        }

        Controller(kind).DragTo(ToOverlayPoint(e.GetPosition(this)));
        e.Handled = true;
    }

    /// <summary>パネルのドラッグを終える。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="e">ポインターの引数。</param>
    private void EndPanelDrag(AnnotationPanelKind kind, PointerReleasedEventArgs e)
    {
        _draggingRoot = null;
        Controller(kind).EndDrag();
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /// <summary>種別ごとのコントローラーを取得する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <returns>コントローラー。</returns>
    private AnnotationPanelController Controller(AnnotationPanelKind kind)
        => kind == AnnotationPanelKind.FormatBar ? _formatBarPanel : _stylePanel;


    /// <summary>子要素の寸法を測る（レイヤーは全面、本文コントロールは本文の矩形）。</summary>
    /// <param name="availableSize">使える領域。</param>
    /// <returns>必要な寸法（レイヤーの希望寸法）。</returns>
    protected override Size MeasureOverride(Size availableSize)
    {
        _isInLayout = true;
        try
        {
            _layer.Measure(availableSize);
            _layer.Arrange(new Rect(availableSize));
            SyncTextEditor();
            _textEditor.Measure(ResolveTextEditorRect().Size);
        }
        finally
        {
            _isInLayout = false;
        }

        return _layer.DesiredSize;
    }

    /// <summary>子要素を配置する（レイヤーは全面、本文コントロールは本文の矩形）。</summary>
    /// <param name="finalSize">最終的な領域。</param>
    /// <returns>配置した寸法。</returns>
    protected override Size ArrangeOverride(Size finalSize)
    {
        _isInLayout = true;
        try
        {
            _layer.Arrange(new Rect(finalSize));
            SyncTextEditor();

            var rect = ResolveTextEditorRect();
            _syncedTextRect = rect;
            _textEditor.Arrange(rect);
        }
        finally
        {
            _isInLayout = false;
        }

        return finalSize;
    }

    /// <summary>本文コントロールの内容（本文・変換中の文字列・倍率）と位置を追随させる。</summary>
    /// <remarks>
    /// <b>本文はレイヤーが単一所有する</b>: コントロールは描画と入力の入口であり、本文の値は
    /// 編集セッションから写す（同じ本文を 2 か所に持たない）。<br/>
    /// <b>位置は本文の矩形ごとに追随させる</b>: 同じ吹き出しを動かしたときも置き直す（識別子だけで
    /// 比べると、動かした本文が元の位置に取り残される）。
    /// </remarks>
    private void SyncTextEditor()
    {
        _textEditor.Editor = _layer.Session.TextEditor;
        _textEditor.LineHeightFactor = _layer.LineHeightFactor;

        if (_layer.Session.Selected is not BubbleAnnotation bubble)
        {
            HideTextEditor();
            return;
        }

        // **変換中の文字列は本文コントロールが描く**: 本文へ混ぜて渡すと二重になるため、
        // 本文は文書の値のまま渡し、変換中の文字列は PreeditText で別に渡す。
        _textEditor.RichTextValue = bubble.Text;
        _textEditor.RenderScale = _layer.ViewScale;
        _textEditor.IsVisible = true;
        _textEditor.RefreshCaretBlink();

        // **編集中だけ入力を受ける**: 入力・IME・キャレットはコントロールが持ち、編集していない間は
        // レイヤーが本文を描き、押下もレイヤー（選択・移動・作成）へ渡す。
        var editing = _layer.IsEditingText;
        _textEditor.IsInputEnabled = editing;
        _textEditor.Focusable = editing;
        _textEditor.IsHitTestVisible = editing;
        if (editing && !_textEditor.IsFocused)
        {
            _textEditor.Focus();
        }

        _textEditor.InvalidateVisual();

        // レイヤーは同じ本文を描かない（二重に描くと縁が太り、位置も二重に見える）
        _layer.ExternalTextId = bubble.Id;
        _syncedTextId = bubble.Id;

        // **動いたら測り直す**: レイアウトの途中で呼ばれた場合はその回の配置が正しい矩形を使うため、
        // ここでは要求しない（要求すると配置と要求が互いを呼び続ける）。
        if (!_isInLayout && ResolveTextEditorRect() != _syncedTextRect)
        {
            InvalidateMeasure();
        }
    }

    /// <summary>本文コントロールで本文が変わったら、編集セッション（文書）へ書き戻す。</summary>
    /// <remarks>
    /// <b>文書は編集セッションが単一所有する</b>: コントロールは入力の入口であり、確定した本文は
    /// セッションへ渡す（元に戻す・保存・他の表示は文書だけを見る）。 同じ本文なら何もしない
    /// （<see cref="SyncTextEditor"/> が文書から写した値を書き戻して往復しない）。
    /// </remarks>
    private void OnTextEditorChanged()
    {
        if (!_layer.IsEditingText || _layer.Session.Selected is not BubbleAnnotation bubble)
        {
            return;
        }

        if (string.Equals(bubble.Text.PlainText, _textEditor.RichTextValue.PlainText, StringComparison.Ordinal)
            && bubble.Text.Equals(_textEditor.RichTextValue))
        {
            return;
        }

        _layer.Session.ApplySelectedRichText(_textEditor.RichTextValue);
    }

    /// <summary>本文コントロールが編集を終えたら、編集セッションも終える。</summary>
    private void OnTextEditorEditingChanged()
    {
        if (_layer.IsEditingText && !_textEditor.IsEditing)
        {
            _layer.Session.EndTextEditing();
        }
    }

    /// <summary>本文コントロールを隠し、レイヤーが本文を描く状態へ戻す。</summary>
    private void HideTextEditor()
    {
        if (_syncedTextId is null && !_textEditor.IsVisible)
        {
            _layer.ExternalTextId = null;
            return;
        }

        _syncedTextId = null;
        _textEditor.IsVisible = false;
        _textEditor.IsInputEnabled = false;
        _textEditor.Focusable = false;
        _textEditor.IsHitTestVisible = false;
        _layer.ExternalTextId = null;

        if (!_isInLayout)
        {
            InvalidateMeasure();
        }
    }

    /// <summary>本文コントロールを置く矩形（このコントロールの座標）を求める。</summary>
    /// <returns>本文の矩形。 選択中の吹き出しが無い場合は空。</returns>
    private Rect ResolveTextEditorRect()
    {
        if (_layer.Session.Selected is not BubbleAnnotation bubble)
        {
            return default;
        }

        return _layer.GetClientRect(bubble.ResolveBodyRect());
    }
}
