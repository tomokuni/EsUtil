using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Point = Avalonia.Point;
using Rect = Avalonia.Rect;
using EsUtil.Feature.Marquee.AvaloniaUI.Sample.Logging;
using EsUtil.Feature.Marquee.AvaloniaUI.Sample.ViewModels;
using EsUtil.Feature.Marquee.AvaloniaUI;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using System.Collections.Specialized;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Interop;

namespace EsUtil.Feature.Marquee.AvaloniaUI.Sample.Views;

/// <summary>メインウィンドウ。 MarqueeControl のイベント購読・設定反映・座標ポート設定を担う。</summary>
/// <remarks>
/// 座標ポートは「対象 = コントロールローカル DIP」の等価変換（DPI 100% 相当）で動作確認する。<br/>
/// 選択状態は <see cref="MainWindowViewModel"/> へ同期し、コントロール設定は PropertyChanged を購読して反映する。<br/>
/// </remarks>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm = new();

    /// <summary>背景に敷くダミー画像（対象面のサイズ感を示す）。 Skia 直描画（EsBitmapAvaloniaImage）で表示する。</summary>
    private static readonly EsBitmapAvaloniaImage Backdrop = CreateBackdrop();

    /// <summary>画像の論理幅（対象ピクセル）。</summary>
    private const double ImageWidth = 1200;

    /// <summary>画像の論理高さ（対象ピクセル）。</summary>
    private const double ImageHeight = 800;

    /// <summary>ズーム倍率（1.0 = 等倍）。</summary>
    private double _zoom = 1.0;

    /// <summary>ズーム倍率の下限（10%）。</summary>
    private const double MinZoom = 0.1;

    /// <summary>ズーム倍率の上限（500%）。</summary>
    private const double MaxZoom = 5.0;

    /// <summary><see cref="MainWindow"/> の新しいインスタンスを初期化する。</summary>
    /// <remarks>
    /// MarqueeControl の公開イベントを購読し、選択状態を ViewModel へ同期する。<br/>
    /// ViewModel のコントロール設定（外観・動作）を MarqueeControl へ反映する。<br/>
    /// </remarks>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        // 背景のダミー画像（対象面のサイズ感）
        BackdropImage.Source = Backdrop;

        // コントロール内部の操作ログを画面のログ表示領域へ出力する
        Marquee.Log = new InMemoryLogger(line => Dispatcher.UIThread.Post(() => _vm.AddLog(line)));

        // 連携 ScrollViewer のスクロール・ズーム変化を購読する（選択面は ScrollViewer 内にあり、祖先探索は行わない）
        ImageScroll.ScrollChanged += OnImageScrollChanged;

        // ズーム倍率に応じたサイズ・座標ポートを適用する
        ApplyZoom(null, 1.0);

        // MarqueeControl のイベント購読（上位APはマウス・キーを直接処理しない）
        Marquee.SelectionChanged += OnSelectionChanged;
        Marquee.SelectionStarted += OnSelectionStarted;
        Marquee.SelectionCompleted += OnSelectionCompleted;
        Marquee.SelectionCancelled += OnSelectionCancelled;
        Marquee.LoupePositionMoved += OnLoupePositionMoved;
        // Escape / Enter の自己処理（確定・キャンセル要求）を購読する
        Marquee.ConfirmRequested += OnConfirmRequested;
        Marquee.CancelRequested += OnCancelRequested;

        // 文字（サイズラベル・座標ラベル）はコントロールが描かないため、描画完了イベントでホストが描く
        Marquee.Rendered += OnMarqueeRendered;

        // ViewModel のコントロール設定を MarqueeControl へ反映する
        _vm.PropertyChanged += OnViewModelPropertyChanged;
        ApplyControlSettings();

        // ログ追加時に最下部（最新行）へ自動スクロールする
        _vm.LogLines.CollectionChanged += OnLogLinesChanged;

        _vm.StatusText = "Ctrl+ホイールでズーム / ホイールでスクロール / ドラッグで選択";
        _vm.AddLog("[App] サンプル起動。ドラッグで領域選択してください。");
    }

    /// <summary>ズーム倍率を Canvas・画像・MarqueeControl のサイズと座標ポートへ適用する。</summary>
    /// <param name="anchor">ズームの中心（Canvasローカル座標）。 null の場合は初期化のみ。</param>
    /// <param name="oldZoom">適用前のズーム倍率（スクロール位置の調整用）。</param>
    /// <remarks>
    /// スクロールオフセットの調整は<b>同期的</b>に行う（<c>Dispatcher.Post</c> を使わない）。<br/>
    /// 非同期にすると、ズーム → サイズ変更 → レイアウト（オフセットがクランプされた状態で描画）→<br/>
    /// オフセット変更 → 再レイアウト、と 2 段階の描画が発生し、選択矩形・背景がちらつく。<br/>
    /// Canvas サイズ変更とオフセット変更を同じフレームで行うことで、描画を 1 回にまとめる。
    /// </remarks>
    private void ApplyZoom(Point? anchor, double oldZoom)
    {
        var ratio = _zoom / oldZoom;

        // ズーム中心（カーソル位置）が同じ表示位置に残るようスクロールオフセットを先に計算する
        // （ScrollViewer のオフセットはスクロール範囲内にクランプされるため、Canvas サイズ変更前に
        // 目標値を計算し、サイズ変更と同じフレームで設定する）。
        var offset = ImageScroll.Offset;
        var newOffset = anchor is { } a
            ? new Vector(a.X * ratio - a.X + offset.X, a.Y * ratio - a.Y + offset.Y)
            : offset;

        // Canvas・背景画像・MarqueeControl の表示サイズをズーム倍率に合わせる
        CanvasHost.Width = ImageWidth * _zoom;
        CanvasHost.Height = ImageHeight * _zoom;
        BackdropImage.Width = ImageWidth * _zoom;
        BackdropImage.Height = ImageHeight * _zoom;
        Marquee.Width = ImageWidth * _zoom;
        Marquee.Height = ImageHeight * _zoom;

        // 座標ポート: 対象 = 画像ピクセル（800x500）、DIP = 表示座標（ズーム倍率で変換）
        // SetCoordTransform は内部で InvalidateVisual を呼び、ズーム時のちらつきを防ぐ
        // ToSource = DIP→対象（_zoom で除算 = 画像ピクセルへ）、ToDip = 対象→DIP（_zoom で乗算）
        Marquee.SetCoordTransform(new CoordTransform(
            p => new EsPoint(p.X / _zoom, p.Y / _zoom),
            p => new EsPoint(p.X * _zoom, p.Y * _zoom),
            () => new EsSize(ImageWidth, ImageHeight)));

        // スクロールオフセットを同期的に設定する（Canvas サイズ変更と同じフレームで反映される）
        ImageScroll.Offset = newOffset;

        _vm.ZoomPercent = (int)Math.Round(_zoom * 100);

        _vm.AddLog($"[App] ズーム: {_zoom:F2}x");
    }

    /// <summary>選択面のマウスホイール処理。 Ctrl+ホイールでズーム、通常ホイールはスクロール。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ホイールイベント引数。</param>
    private void OnCanvasWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        // Ctrl なしの通常ホイールは ScrollViewer にスクロールさせる
        if ((e.KeyModifiers & KeyModifiers.Control) == 0)
        {
            return;
        }

        var anchor = e.GetPosition(CanvasHost);
        var oldZoom = _zoom;
        _zoom = Math.Clamp(_zoom + (e.Delta.Y > 0 ? 0.1 : -0.1), MinZoom, MaxZoom);
        ApplyZoom(anchor, oldZoom);
        e.Handled = true;
    }

    /// <summary>ログ行が追加されたときに最下部（最新行）へ自動スクロールする。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">コレクション変更イベント引数。</param>
    private void OnLogLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // 追加（またはリセット）後に最新行が表示されるよう、レイアウト完了を待って最下部へスクロールする
        Dispatcher.UIThread.Post(() => LogScroll.ScrollToEnd());
    }

    /// <summary>ViewModel の設定プロパティを MarqueeControl へ反映する。</summary>
    private void ApplyControlSettings()
    {
        // 選択枠線は装飾（暗転マスクの外枠）が描くため、色は外観へ、太さも外観（枠線の太さ）へ与える
        // （ハンドルの枠線も同じ色に揃える。 両者は同じ矩形に重なる）
        var borderColor = _vm.BorderColorOption.Color.ToDrawingColor();
        Marquee.Appearance = Marquee.Appearance with
        {
            OuterBorderColor = borderColor,
            OuterBorderThickness = _vm.BorderThickness,
            HandleColor = _vm.HandleColorOption.Color.ToDrawingColor(),
            HandleBorderColor = borderColor,
            MarkerColor = _vm.MarkerColorOption.Color.ToDrawingColor(),
        };
        Marquee.Options = new MarqueeOptions
        {
            HandleSize = _vm.HandleSize,
            HandleHitRadius = _vm.HandleHitRadius,
            MinSelectionSize = _vm.MinSelectionSize,
            MarkerLength = _vm.MarkerLength,
            ClampToSourceBounds = _vm.ClampToSourceBounds,
        };
        // マウスが描画域にない場合でも、設定変更を即時に再描画へ反映する
        Marquee.InvalidateVisual();
    }

    /// <summary>ViewModel の設定プロパティ変更を MarqueeControl へ反映する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">プロパティ変更イベント引数。</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.BorderThickness):
            case nameof(MainWindowViewModel.HandleSize):
            case nameof(MainWindowViewModel.HandleHitRadius):
            case nameof(MainWindowViewModel.MinSelectionSize):
            case nameof(MainWindowViewModel.BorderColorOption):
            case nameof(MainWindowViewModel.HandleColorOption):
            case nameof(MainWindowViewModel.MarkerColorOption):
            case nameof(MainWindowViewModel.MarkerLength):
                ApplyControlSettings();
                break;
            case nameof(MainWindowViewModel.ClampToSourceBounds):
                ApplyControlSettings();
                _vm.AddLog($"[App] ClampToSourceBounds: {(_vm.ClampToSourceBounds ? "ON（画像内にクリップ）" : "OFF（画像外も選択可能）")}");
                break;
            default:
                return;
        }
    }

    /// <summary>選択矩形が変更されたときの処理。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">選択矩形イベント引数。</param>
    private void OnSelectionChanged(object? sender, SelectionRectEventArgs e)
    {
        _vm.UpdateSelection(true, e.Rect);
        _vm.AddLog($"[Event] SelectionChanged: X={e.Rect.X:F0}, Y={e.Rect.Y:F0}, W={e.Rect.Width:F0}, H={e.Rect.Height:F0}");
    }

    /// <summary>ドラッグの始点が決定されたときの処理。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">座標イベント引数。</param>
    private void OnSelectionStarted(object? sender, MarqueePointEventArgs e)
        => _vm.AddLog($"[Event] SelectionStarted: ({e.Point.X:F0}, {e.Point.Y:F0})");

    /// <summary>選択が確定したときの処理。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">選択矩形イベント引数。</param>
    private void OnSelectionCompleted(object? sender, SelectionRectEventArgs e)
    {
        _vm.UpdateSelection(true, e.Rect);
        _vm.AddLog($"[Event] SelectionCompleted: X={e.Rect.X:F0}, Y={e.Rect.Y:F0}, W={e.Rect.Width:F0}, H={e.Rect.Height:F0}");
    }

    /// <summary>領域がキャンセルされたときの処理。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnSelectionCancelled(object? sender, EventArgs e)
    {
        _vm.UpdateSelection(false, default);
        _vm.AddLog("[Event] SelectionCancelled");
    }

    /// <summary>nudge 後のルーペ中心位置要求の処理。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">座標イベント引数。</param>
    private void OnLoupePositionMoved(object? sender, MarqueePointEventArgs e)
        => _vm.AddLog($"[Event] LoupePositionMoved: ({e.Point.X:F0}, {e.Point.Y:F0})");

    /// <summary>Enter キーによる確定要求の処理（選択破棄はコントロールが実施済み）。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">選択矩形イベント引数。</param>
    private void OnConfirmRequested(object? sender, SelectionRectEventArgs e)
    {
        _vm.StatusText = "Enter: 確定要求を受信しました。";
        _vm.AddLog($"[Event] ConfirmRequested: X={e.Rect.X:F0}, Y={e.Rect.Y:F0}, W={e.Rect.Width:F0}, H={e.Rect.Height:F0}");
    }

    /// <summary>Escape キーによるキャンセル要求の処理（選択破棄はコントロールが実施済み）。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnCancelRequested(object? sender, EventArgs e)
    {
        _vm.UpdateSelection(false, default);
        _vm.StatusText = "Escape: キャンセル要求を受信しました。";
        _vm.AddLog("[Event] CancelRequested");
    }

    /// <summary>連携 ScrollViewer のスクロール・ズーム変化を検知したときの処理。</summary>
    /// <param name="sender">イベント送信元（ScrollViewer）。</param>
    /// <param name="e">スクロール変化イベント。</param>
    /// <remarks>
    /// 選択矩形（対象座標）は表示の変化で不変であるため、再描画のみを要求する<br/>
    /// （選択面は座標変換ポートを都度評価するため、再描画だけで追従する）。
    /// </remarks>
    private void OnImageScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        Marquee.InvalidateVisual();
        _vm.AddLog($"[Event] ScrollChanged: X={ImageScroll.Offset.X:F0}, Y={ImageScroll.Offset.Y:F0}");
    }

    /// <summary>描画完了時にホスト固有の付加情報を描画する。</summary>
    /// <param name="sender">イベント送信元（MarqueeControl）。</param>
    /// <param name="e">描画コンテキストとコアが解決した描画内容。</param>
    /// <remarks>
    /// サイズラベルとカーソル座標ラベルはコントロールがテキスト要素として配置するため、本ハンドラは何もしない。<br/>
    /// ホスト固有の付加情報（操作ラベル等）を描く場合は本ハンドラで <see cref="MarqueeRenderedEventArgs.Content"/> の<br/>
    /// <see cref="MarqueeDrawPlan.FocusDipRect"/> を基準に描く。<br/>
    /// ログは描画パスの外（ディスパッチャー）へ投げる: 本ハンドラは描画中に呼ばれるため、<br/>
    /// ここで表示中の要素（ログの一覧）を変更すると Avalonia が描画中の視覚ツリー変更として例外にする。
    /// </remarks>
    private void OnMarqueeRendered(object? sender, MarqueeRenderedEventArgs e)
        => Dispatcher.UIThread.Post(() => _vm.AddLog($"[Event] Rendered: HasText={e.Content.HasText}, Layers={e.Content.Layers.Count}"));

    /// <summary>選択面の押下時に MarqueeControl へフォーカスを渡す。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ポインターイベント引数。</param>
    /// <remarks>矢印キー nudge は MarqueeControl のフォーカスが必要なため、面の押下でフォーカスを移動する。</remarks>
    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Marquee.Focus();
    }

    /// <summary>選択をクリアする。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnClearClicked(object? sender, RoutedEventArgs e)
    {
        Marquee.ResetSelection();
        _vm.UpdateSelection(false, default);
        _vm.AddLog("[App] ResetSelection() を呼び出しました。");
    }

    /// <summary>固定の選択矩形を設定する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    /// <remarks>外部からの復元（<see cref="MarqueeControl.SetSelection"/>）の動作確認。</remarks>
    private void OnSetFixedClicked(object? sender, RoutedEventArgs e)
    {
        Marquee.SetSelection(new Rect(250, 180, 500, 360));
        _vm.UpdateSelection(true, new Rect(250, 180, 500, 360));
        _vm.AddLog("[App] SetSelection(250,180,500,360) を呼び出しました。");
    }

    /// <summary>確定要求（Enter キーと同じ条件）を発行する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    /// <remarks>プログラムからの確定要求（<see cref="MarqueeControl.RequestConfirm"/>）の動作確認。</remarks>
    private void OnConfirmClicked(object? sender, RoutedEventArgs e)
    {
        Marquee.RequestConfirm();
        _vm.AddLog("[App] RequestConfirm() を呼び出しました。");
    }

    /// <summary>キャンセル要求（Escape キーと同じ条件）を発行する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    /// <remarks>プログラムからのキャンセル要求（<see cref="MarqueeControl.RequestCancel"/>）の動作確認。</remarks>
    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Marquee.RequestCancel();
        _vm.AddLog("[App] RequestCancel() を呼び出しました。");
    }

    /// <summary>操作ログをクリアする。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnClearLogClicked(object? sender, RoutedEventArgs e)
        => _vm.ClearLog();

    /// <summary>背景用のダミー画像を生成する（グリッド付き、サイズ感の確認用）。</summary>
    /// <returns>生成した WriteableBitmap。</returns>
    /// <remarks>
    /// ビットマップサイズは <see cref="ImageWidth"/> / <see cref="ImageHeight"/> に合わせる
    /// （const のためリテラルで指定。 グリッド間隔は 100px で視認性を確保する）。
    /// </remarks>
    private static EsBitmapAvaloniaImage CreateBackdrop()
    {
        const int width = 1200;
        const int height = 800;
        var bitmap = EsBitmap.Create(width, height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                // 淡いグリッド（100px 間隔。 画像サイズ拡大に合わせて間隔も広げる）
                var isGrid = x % 100 == 0 || y % 100 == 0;
                byte v = isGrid ? (byte)200 : (byte)235;
                bitmap.SetPixel(x, y, v, v, v, 255);
            }
        }

        return new EsBitmapAvaloniaImage(bitmap);
    }
}
