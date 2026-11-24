using Microsoft.Extensions.Logging;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Palette;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Annotation.Presenter.Panel;

namespace EsUtil.Ui.WinUI3.Annotation;

/// <summary>注釈の本文（表示とその場編集）と書式バーを 1 つにまとめたコントロール（WinUI 3 版）。</summary>
/// <remarks>
/// <b>セットで使う</b>: レイヤー（画像・注釈・入力）と書式バーを持つ。 2 つを別々に置くと、
/// 位置合わせと表示の切り替えをホストが書くことになる。<br/>
/// <b>位置と開閉はコアが決める</b>: <see cref="AnnotationPanelController"/> が「どこへ出すか・ドラッグで
/// どう動くか・いつ閉じるか」を単一所有する。 本クラスは <see cref="IAnnotationPanelHost"/> を実装し、
/// 重ねる先への追加・移動・削除だけを行う（4 つの UI 版で動作を揃えるため）。<br/>
/// <b>独立したウィンドウを作らない</b>: ウィンドウはタスク バーへ出る・アプリの後ろへ回る・フォーカスを
/// 奪うといった問題が版ごとに出るため、アプリの画面の最上位（<c>XamlRoot</c> の根）へ重ねる。<br/>
/// <b>全面を覆う要素を挟まない</b>: 挟むとパネルの外側の押下を吸ってしまい、注釈も書式バーも
/// 操作できなくなる。 位置は余白（<c>Margin</c>）で与える。
/// </remarks>
public sealed partial class AnnotationTextEditor : Grid, IAnnotationPanelHost
{
    /// <summary>注釈の表示と編集を担うレイヤー。</summary>
    private readonly AnnotationLayerControl _layer;

    /// <summary>本文の書式を操作するバー。</summary>
    private readonly AnnotationFormatBar _formatBar;

    /// <summary>書式バーを出すコントローラー。</summary>
    private readonly AnnotationPanelController _formatBarPanel;

    /// <summary>右クリックの外観フライアウトを出すコントローラー。</summary>
    private readonly AnnotationPanelController _stylePanel;

    /// <summary>ポップアップのウィンドウが中身のまわりに持つ影の余白（DIP）。</summary>
    /// <remarks>
    /// <b>見えている中身を基準に収める</b>: WinUI 3 のポップアップのウィンドウは中身のまわりに透明な
    /// 影の余白を含むため、ウィンドウの矩形だけで挟むと中身が画面の端を越えます。
    /// </remarks>
    private const double PopupShadowMargin = 20;

    /// <summary>表示中のパネルの根（種別ごとに 1 つ。 使い回す）。</summary>
    private readonly Dictionary<AnnotationPanelKind, Border> _roots = [];

    /// <summary>表示中のパネルの左上（DIP）。</summary>
    private readonly Dictionary<AnnotationPanelKind, EsPoint> _positions = [];

    /// <summary>パネルを重ねた先（アプリの画面の最上位）。</summary>
    private Panel? _overlay;


    /// <summary>パネルを載せるポップアップ（種別ごとに 1 つ。 使い回す）。</summary>
    private readonly Dictionary<AnnotationPanelKind, Popup> _popups = [];
    /// <summary>ドラッグ中のパネルの根（押下を受けた要素）。</summary>
    private Border? _draggingRoot;

    /// <summary>外観パネルを開いた押下で「外側を押したら閉じる」判定を飛ばすかどうか。</summary>
    private bool _swallowNextOutsidePress;

    /// <summary>外側の押下を監視している根（二重に繋がないための目印）。</summary>
    private Panel? _hookedRoot;

    /// <summary>注釈の本文編集と書式バーをまとめたコントロールを初期化する。</summary>
    public AnnotationTextEditor()
    {
        _layer = new AnnotationLayerControl();
        _formatBar = new AnnotationFormatBar(_layer);

        _formatBarPanel = new AnnotationPanelController(this, AnnotationPanelAppearance.FormatBar, () => _layer.Log);
        _stylePanel = new AnnotationPanelController(this, AnnotationPanelAppearance.StyleEditor, () => _layer.Log);

        Children.Add(_layer);

        _layer.EditingChanged += (_, _) =>
        {
            if (_layer.IsEditingText)
            {
                _formatBar.Refresh();
                ShowFormatBar();
                return;
            }

            _formatBarPanel.Close();
        };

        _layer.TextSelectionChanged += (_, _) =>
        {
            if (_formatBarPanel.IsOpen)
            {
                _formatBar.Refresh();
            }
        };

        _layer.SelectionChanged += (_, _) =>
        {
            if (_layer.IsEditingText)
            {
                _formatBar.Refresh();
                ShowFormatBar();
            }
        };

        _layer.StyleEditorRequested += (_, e) => ShowStylePanel(e.X, e.Y, e.IsFromPointer);
    }

    /// <summary>注釈の表示と編集を担うレイヤーを取得する。</summary>
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
    public bool IsEditingText => _layer.IsEditingText;

    /// <summary>書式バーの色の入力欄へ出す表記を取得または設定する。</summary>
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
    /// <value>注釈レイヤーが占める範囲を外側へ広げ、ウィンドウの内側との交わりを取ったもの（重ねた先の座標）。</value>
    /// <remarks>
    /// <b>ウィンドウの内側へ収める</b>: パネルはウィンドウの内側へ切れて見えなくなるため、レイヤーの
    /// 範囲へ挟まず、外側へ広げた範囲とウィンドウの内側との交わりを使う（規則は
    /// <see cref="AnnotationPanelPlacement.ResolveAvailableArea"/> が単一所有する）。<br/>
    /// <b>実寸が取れないときはレイヤーの範囲</b>: レイアウト前は <c>ActualWidth</c> が 0 のため、
    /// その場合はレイヤーの範囲をそのまま使う。
    /// </remarks>
    public EsRect AvailableArea
    {
        get
        {
            var width = _layer.ActualWidth > 0 ? _layer.ActualWidth : ActualWidth;
            var height = _layer.ActualHeight > 0 ? _layer.ActualHeight : ActualHeight;
            if (width <= 0 || height <= 0)
            {
                return default;
            }

            var origin = ToOverlayPoint(default);
            return Round(AnnotationPanelPlacement.ResolveAvailableArea(
                new EsRect(origin.X, origin.Y, width, height),
                VisibleBounds(),
                AnnotationPanelMetrics.OutsideAllowance));
        }
    }

    /// <summary>単精度の変換で生じた誤差を落とす。</summary>
    /// <param name="rect">変換した矩形。</param>
    /// <returns>1/100 DIP へ丸めた矩形。</returns>
    private static EsRect Round(EsRect rect)
        => new(Math.Round(rect.X, 2), Math.Round(rect.Y, 2), Math.Round(rect.Width, 2), Math.Round(rect.Height, 2));

    /// <summary>アプリのウィンドウの内側（重ねた先の座標）を取得する。</summary>
    /// <returns>見えている範囲。 取得できない場合は既定値。</returns>
    private EsRect VisibleBounds()
        => XamlRoot?.Content is Panel root && root.ActualWidth > 0 && root.ActualHeight > 0
            ? new EsRect(0, 0, root.ActualWidth, root.ActualHeight)
            : default;

    /// <summary>表示中のパネルの左上（DIP）を取得する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <returns>左上の位置。 表示していない場合は原点。</returns>
    public EsPoint GetPanelPosition(AnnotationPanelKind kind)
        => _positions.TryGetValue(kind, out var position) ? position : default;

    /// <summary>表示中のパネルの大きさ（DIP）を取得する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <returns>パネルの大きさ。 表示していない場合は原点。</returns>
    public EsSize GetPanelSize(AnnotationPanelKind kind) => MeasurePanel(kind);

    /// <summary>パネルを作って表示する。</summary>
    /// <param name="kind">パネルの種別（中身を決める）。</param>
    /// <param name="appearance">外観（枠・余白・幅）。</param>
    /// <param name="position">左上の位置（アプリの画面の左上を原点とする DIP）。</param>
    public void CreatePanel(
        AnnotationPanelKind kind, AnnotationPanelAppearance appearance, EsPoint position)
    {
        RemovePanel(kind);

        if (ResolveOverlay() is not { } overlay)
        {
            return;
        }

        // 根は種別ごとに 1 度だけ作って使い回す（中身を作り直すと購読が増える）
        if (!_roots.TryGetValue(kind, out var root))
        {
            root = new Border
            {
                Child = kind == AnnotationPanelKind.FormatBar
                    ? _formatBar
                    : new AnnotationStylePanel(_layer),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };

            _roots[kind] = root;
        }

        // **中身は表示のたびに作り直す**: 吹き出しの設定は「吹き出しを選んでいるとき」だけ出す。
        // 一度作った中身を使い回すと、最初に枠線を右クリックした後は吹き出しの設定が出ない。
        if (kind != AnnotationPanelKind.FormatBar)
        {
            root.Child = new AnnotationStylePanel(_layer);
        }
        else
        {
            // 高さはコアの固定値で確定する（見積りと実寸のずれで注釈に重なるのを防ぐ）
            root.Height = appearance.ResolvePanelHeight(appearance.FixedContentHeight);
        }

        root.Background = ToBrush(appearance.Background);
        root.BorderBrush = ToBrush(appearance.Border);
        root.BorderThickness = new Thickness(appearance.BorderThickness);
        root.CornerRadius = new CornerRadius(appearance.CornerRadius);
        root.Padding = new Thickness(
            appearance.Padding, appearance.VerticalPadding,
            appearance.Padding, appearance.VerticalPadding);

        root.PointerPressed += (_, e) => BeginPanelDrag(kind, root, e);
        root.PointerMoved += (_, e) => ContinuePanelDrag(kind, root, e);
        root.PointerReleased += (_, e) => EndPanelDrag(kind, root, e);

        // **ポップアップへ載せる**: ウィンドウの中へ重ねると、ウィンドウが小さいときに中身が切れる。
        // WinUI 3 の <see cref="Popup"/> は ShouldConstrainToRootBounds を false にすると
        // ウィンドウの外（画面の作業領域の内側）へも出せる。
        if (!_popups.TryGetValue(kind, out var popup))
        {
            popup = new Popup
            {
                XamlRoot = XamlRoot,
                ShouldConstrainToRootBounds = false,
                IsLightDismissEnabled = false,
                Child = root,
            };

            _popups[kind] = popup;
        }

        popup.Child = root;
        popup.IsOpen = true;

        ApplyPosition(kind, position);

        // **実寸を確定させる**: レイアウト前の測定は高さを正しく返さない。
        // ここでレイアウトを走らせると、ホストが実寸を報告でき、コアが位置を決め直せる。
        root.UpdateLayout();

        // **実寸が決まってから収め直す**: 出した直後の `ActualWidth` は 0 のため、最初の収め方では
        // 画面の外へはみ出します。
        if (DispatcherQueue is { } queue)
        {
            queue.TryEnqueue(() => ApplyPosition(kind, position));
        }
    }

    /// <summary>パネルを移動する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="position">左上の位置（アプリの画面の左上を原点とする DIP）。</param>
    public void MovePanel(AnnotationPanelKind kind, EsPoint position)
    {
        if (_popups.ContainsKey(kind))
        {
            ApplyPosition(kind, position);
        }
    }

    /// <summary>パネルを画面から外す（根と中身は残す）。</summary>
    /// <param name="kind">パネルの種別。</param>
    public void DestroyPanel(AnnotationPanelKind kind) => RemovePanel(kind);

    /// <summary>右クリック位置へ外観の編集パネルを表示する。</summary>
    /// <param name="x">位置（このコントロールの座標）。</param>
    /// <param name="y">位置（このコントロールの座標）。</param>
    /// <remarks>
    /// <b>位置と寿命はコアが決める</b>: 置ける領域（キャプチャ ページ）の内側へ収め、右クリックした位置へ出し、
    /// 外側をクリックすると閉じる。 余白を押したまま動かすと移動する。
    /// </remarks>
    /// <remarks>
    /// <b>位置はコアが決める</b>: 右クリックした位置へ出し、置ける領域の内側へ収める
    /// （<see cref="AnnotationPanelController.Constrain"/>）。 レイアウト前の測定ではなく、レイアウト後の
    /// 実寸で収める（<see cref="CreatePanel"/> がレイアウトを確定させる）。
    /// </remarks>
    public void ShowStylePanel(double x, double y) => ShowStylePanel(x, y, isFromPointer: false);

    /// <summary>右クリック位置へ外観の編集パネルを表示する。</summary>
    /// <param name="x">位置（このコントロールの座標）。</param>
    /// <param name="y">位置（このコントロールの座標）。</param>
    /// <param name="isFromPointer">ポインターの押下から出した要求かどうか（その押下では閉じない）。</param>
    private void ShowStylePanel(double x, double y, bool isFromPointer)
    {
        // **開いた押下では閉じない**: この押下は根の「外側を押したら閉じる」判定にも届くため、
        // 1 回だけ判定を飛ばす（飛ばさないとパネルが開いた直後に閉じる）。
        _swallowNextOutsidePress = isFromPointer;

        _stylePanel.Show(
            AnnotationPanelKind.StyleEditor, ToOverlayPoint(new Windows.Foundation.Point(x, y)));
        _stylePanel.Constrain(MeasurePanel(AnnotationPanelKind.StyleEditor));
    }

    /// <summary>外観の編集パネルが出ているかどうかを取得する。</summary>
    public bool IsStylePanelOpen => _stylePanel.IsOpen;

    /// <summary>外観の編集パネルを閉じる。</summary>
    public void CloseStylePanel() => _stylePanel.Close();

    /// <summary>書式バーを本文の外（本文に被さらない位置）へ出し直す。</summary>
    /// <remarks>
    /// <b>本文に被せない</b>: 編集中の文字が隠れて読めない。 位置の判断は
    /// 提示層（<see cref="AnnotationPanelPlacement.ResolveOutside"/>）が単一所有する。<br/>
    /// <b>実寸で置き直す</b>: レイアウト前の測定は高さを正しく返さないことがあるため、一度置いて
    /// レイアウトを確定させてからコアに実寸で決め直させる。
    /// </remarks>
    private void ShowFormatBar()
    {
        var body = ToOverlayRect(_layer.GetSelectedBoundsRect());
        _formatBarPanel.ShowOutside(
            AnnotationPanelKind.FormatBar, body, MeasureFormatBar(), AnnotationPanelMetrics.FormatBarGap);
        _formatBarPanel.RepositionOutside(body, AnnotationPanelMetrics.FormatBarGap);
    }

    /// <summary>書式バーの大きさ（DIP）を測る。</summary>
    /// <returns>実寸。 測れない場合は希望寸法。</returns>
    /// <remarks>
    /// <b>置ける幅を上限にして測る</b>: 幅を無限にして測ると中身（フォント名・色）が伸びきった寸法が返り、
    /// その寸法で位置を決めると置ける領域の外へ出る。
    /// </remarks>
    private EsSize MeasureFormatBar()
    {
        var measured = MeasurePanel(AnnotationPanelKind.FormatBar);
        if (measured.Width > 0 && measured.Height > 0)
        {
            return measured;
        }

        var appearance = AnnotationPanelAppearance.FormatBar;
        var area = AvailableArea;
        var widthLimit = area.Width > 0 ? area.Width : double.PositiveInfinity;
        _formatBar.Measure(new Windows.Foundation.Size(widthLimit, double.PositiveInfinity));
        var size = _formatBar.DesiredSize;
        return new EsSize(
            size.Width + (appearance.Padding * 2) + (appearance.BorderThickness * 2),
            size.Height + (appearance.VerticalPadding * 2) + (appearance.BorderThickness * 2));
    }

    /// <summary>パネルの大きさ（DIP）を測る。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <returns>実寸。 まだ置いていない場合は希望寸法。</returns>
    /// <remarks>
    /// <b>根ではなく中身を測る</b>: 根はレイアウトのたびに引き伸ばされることがあり、実寸が画面の
    /// 大きさになる。
    /// </remarks>
    private EsSize MeasurePanel(AnnotationPanelKind kind)
    {
        if (!_roots.TryGetValue(kind, out var root))
        {
            return default;
        }

        if (root.Child is FrameworkElement content)
        {
            content.Measure(new Windows.Foundation.Size(
                double.PositiveInfinity, double.PositiveInfinity));
            var desired = content.DesiredSize;
            var appearance = KindAppearance(kind);
            return new EsSize(
                desired.Width + (appearance.Padding * 2) + (appearance.BorderThickness * 2),
                appearance.ResolvePanelHeight(desired.Height));
        }

        return new EsSize(root.ActualWidth, root.ActualHeight);
    }

    /// <summary>種別の外観を取得する（パネルはどちらか 1 つしか持たない）。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <returns>外観。</returns>
    private static AnnotationPanelAppearance KindAppearance(AnnotationPanelKind kind)
        => kind == AnnotationPanelKind.FormatBar
            ? AnnotationPanelAppearance.FormatBar
            : AnnotationPanelAppearance.StyleEditor;

    /// <summary>種別のパネルを画面から外す（根と中身は残す）。</summary>
    /// <param name="kind">パネルの種別。</param>
    private void RemovePanel(AnnotationPanelKind kind)
    {
        if (_popups.TryGetValue(kind, out var popup))
        {
            popup.IsOpen = false;
        }
    }

    /// <summary>パネルの位置をポップアップへ適用する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="position">左上の位置（DIP）。</param>
    /// <remarks>
    /// <b>位置はポップアップのオフセットで与える</b>: 余白（<c>Margin</c>）で与えるとウィンドウの外へ出せません。
    /// <see cref="Popup.ShouldConstrainToRootBounds"/> を <see langword="false"/> にしてあるため、
    /// 中身がウィンドウより大きいときは外へはみ出して描けます。
    /// </remarks>
    private void ApplyPosition(AnnotationPanelKind kind, EsPoint position)
    {
        _positions[kind] = position;

        if (!_popups.TryGetValue(kind, out var popup))
        {
            return;
        }

        // **画面の外へは出さない**: パネルはポップアップとしてウィンドウの外へも描けるため、
        // 置く位置をアプリのウィンドウがあるモニターの作業領域で挟みます（画面外で見えなくなるのを
        // 防ぐ）
        // **単位を揃える**: ポップアップの位置（`HorizontalOffset`）はウィンドウの左上を原点とする
        // **DIP**、作業領域は**ピクセル**です。 表示倍率（125% など）を掛けずに挟むと、
        // 画面の外へはみ出します。
        // **ウィンドウのハンドルは XamlRoot から取る**: このコントロール自身を渡すと
        // `WinRT.Interop.WindowNative.GetWindowHandle` が InvalidCastException で落ちます。
        var x = position.X;
        var y = position.Y;
        if (MainWindowHandle() is { } handle && handle != IntPtr.Zero
            && XamlRoot is { } root)
        {
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
            var window = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            var work = Microsoft.UI.Windowing.DisplayArea
                .GetFromWindowId(windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest)
                .WorkArea;

            var scale = root.RasterizationScale > 0 ? root.RasterizationScale : 1;

            // **実寸が取れないときは中身を測る**: 出した直後の `ActualWidth` は 0 のため、それだけで
            // 挟むと画面の外へ出ます。
            var measured = GetPanelSize(kind);
            var width = popup.ActualWidth > 0 ? popup.ActualWidth : measured.Width;
            var height = popup.ActualHeight > 0 ? popup.ActualHeight : measured.Height;

            // **影の余白を差し引く**: WinUI 3 のポップアップのウィンドウは、中身のまわりに影の
            // 透明な余白を含みます。 余白を考えずに挟むと、見えている中身が
            // 画面の端を越えます。
            var margin = PopupShadowMargin;
            x = Math.Clamp(
                x,
                (work.X - window.Position.X) / scale,
                Math.Max(
                    (work.X - window.Position.X) / scale,
                    ((work.X + work.Width - window.Position.X) / scale) - width - margin));
            y = Math.Clamp(
                y,
                (work.Y - window.Position.Y) / scale,
                Math.Max(
                    (work.Y - window.Position.Y) / scale,
                    ((work.Y + work.Height - window.Position.Y) / scale) - height - margin));
        }

        popup.HorizontalOffset = x;
        popup.VerticalOffset = y;
        _positions[kind] = new EsPoint(x, y);
    }

    /// <summary>アプリのウィンドウのハンドルを取得する。</summary>
    /// <returns>ウィンドウのハンドル。 取得できない場合は <see cref="IntPtr.Zero"/>。</returns>
    private IntPtr MainWindowHandle()
    {
        if (XamlRoot?.ContentIslandEnvironment is not { } environment)
        {
            return IntPtr.Zero;
        }

        return Microsoft.UI.Win32Interop.GetWindowFromWindowId(environment.AppWindowId);
    }

    /// <summary>パネルを重ねる先（アプリの画面の最上位）を取得する。</summary>
    /// <returns>重ねる先。 取得できない場合は <see langword="null"/>。</returns>
    /// <remarks>
    /// <b>根の <c>Panel</c> へ直接足す</b>: 全面を覆う <c>Canvas</c> を挟むと、パネルの外側の押下を
    /// 吸ってしまい注釈も書式バーも操作できなくなる。 位置は余白で与える。
    /// </remarks>
    private Panel? ResolveOverlay()
    {
        if (_overlay is { } existing && existing.XamlRoot is not null)
        {
            return existing;
        }

        if (XamlRoot?.Content is not Panel rootPanel)
        {
            return null;
        }

        _overlay = rootPanel;

        // パネルの外側の押下で外観フライアウトを閉じる（根で拾う。 子は自分の分だけ処理する）。
        // 根は作り直されうるため、同じ根へ二重に繋がないよう目印を持つ
        if (!ReferenceEquals(_hookedRoot, rootPanel))
        {
            _hookedRoot = rootPanel;
            rootPanel.AddHandler(
                UIElement.PointerPressedEvent,
                new PointerEventHandler(OnRootPointerPressed),
                handledEventsToo: true);
        }

        return rootPanel;
    }

    /// <summary>根で押下を受けたときに、パネルの外側なら外観フライアウトを閉じる。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">押下の引数。</param>
    private void OnRootPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_swallowNextOutsidePress)
        {
            _swallowNextOutsidePress = false;
            return;
        }

        if (_overlay is not { } overlay)
        {
            return;
        }

        _ = sender;
        var position = e.GetCurrentPoint(overlay).Position;
        _stylePanel.CloseIfOutside(new EsPoint(position.X, position.Y));
    }

    /// <summary>このコントロールの座標（DIP）を重ねた先の座標（DIP）へ変換する。</summary>
    /// <param name="point">このコントロールの座標。</param>
    /// <returns>重ねた先の座標。 取得できない場合はそのまま返す。</returns>
    /// <remarks>
    /// <b>1/100 DIP までに丸める</b>: WinUI 3 の <c>Point</c> は単精度であり、変換すると
    /// <c>761.599975585938</c> のような誤差を含む。 そのままではログ・パネルの位置へ誤差が広がる。
    /// </remarks>
    private EsPoint ToOverlayPoint(Windows.Foundation.Point point)
    {
        // 置ける領域はパネルを置く前に問い合わせられるため、重ねる先は都度解決する
        if (_overlay is null)
        {
            _ = ResolveOverlay();
        }

        if (_overlay is not { } overlay)
        {
            return Round(new EsPoint(point.X, point.Y));
        }

        try
        {
            var local = TransformToVisual(overlay).TransformPoint(point);
            return Round(new EsPoint(local.X, local.Y));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // ツリーへ繋がっていない間は変換できない。 位置はそのまま使う（見えないより出すほうがよい）
            return Round(new EsPoint(point.X, point.Y));
        }
    }

    /// <summary>単精度の変換で生じた誤差を落とす。</summary>
    /// <param name="point">変換した座標。</param>
    /// <returns>1/100 DIP へ丸めた座標。</returns>
    private static EsPoint Round(EsPoint point)
        => new(Math.Round(point.X, 2), Math.Round(point.Y, 2));

    /// <summary>このコントロールの矩形（DIP）を重ねた先の座標（DIP）へ変換する。</summary>
    /// <param name="rect">このコントロールの矩形。</param>
    /// <returns>重ねた先の矩形。</returns>
    private EsRect ToOverlayRect(EsRect rect)
    {
        var origin = ToOverlayPoint(new Windows.Foundation.Point(rect.X, rect.Y));
        return new EsRect(origin.X, origin.Y, rect.Width, rect.Height);
    }

    /// <summary>パネルのドラッグを始める（余白を押したときだけ）。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="root">パネルの根。</param>
    /// <param name="e">押下の引数。</param>
    private void BeginPanelDrag(AnnotationPanelKind kind, Border root, PointerRoutedEventArgs e)
    {
        // 中身（ボタン・入力欄）の押下では動かさない（操作を奪わない）。
        // 余白（中身の無い面・ラベル）の押下は動かす（根そのものを押すとは限らない）。
        if (IsInteractiveSource(e.OriginalSource, root)
            || !e.GetCurrentPoint(root).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (_overlay is not { } overlay)
        {
            return;
        }

        _draggingRoot = root;
        var position = e.GetCurrentPoint(overlay).Position;
        Controller(kind).BeginDrag(new EsPoint(position.X, position.Y));
        root.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    /// <summary>押下の発生元が操作可能な要素かどうかを判定する（パネルのドラッグ可否）。</summary>
    /// <param name="source">押下の発生元。</param>
    /// <param name="root">パネルの根。</param>
    /// <returns>操作可能な要素（タブ移動の対象になる要素）から始まった場合は <see langword="true"/>。</returns>
    private static bool IsInteractiveSource(object? source, Border root)
    {
        for (var current = source as DependencyObject;
            current is not null && !ReferenceEquals(current, root);
            current = VisualTreeHelper.GetParent(current))
        {
            if (current is Control { IsTabStop: true })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>パネルをドラッグして動かす。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="root">パネルの根。</param>
    /// <param name="e">ポインターの引数。</param>
    private void ContinuePanelDrag(
        AnnotationPanelKind kind, Border root, PointerRoutedEventArgs e)
    {
        if (!ReferenceEquals(_draggingRoot, root) || _overlay is not { } overlay)
        {
            return;
        }

        var position = e.GetCurrentPoint(overlay).Position;
        Controller(kind).DragTo(new EsPoint(position.X, position.Y));
        e.Handled = true;
    }

    /// <summary>パネルのドラッグを終える。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="root">パネルの根。</param>
    /// <param name="e">ポインターの引数。</param>
    private void EndPanelDrag(
        AnnotationPanelKind kind, Border root, PointerRoutedEventArgs e)
    {
        _draggingRoot = null;
        Controller(kind).EndDrag();
        root.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    /// <summary>種別ごとのコントローラーを取得する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <returns>コントローラー。</returns>
    private AnnotationPanelController Controller(AnnotationPanelKind kind)
        => kind == AnnotationPanelKind.FormatBar ? _formatBarPanel : _stylePanel;

    /// <summary>コアの色を WinUI のブラシへ変換する。</summary>
    /// <param name="color">色。</param>
    /// <returns>不透明な塗りつぶしのブラシ。</returns>
    private static SolidColorBrush ToBrush(DrawingColor color)
        => new(Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B));
}
