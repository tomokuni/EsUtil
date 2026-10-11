using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using EsUtil.Feature.Loupe.AvaloniaUI.Sample.Internal;
using EsUtil.Feature.Loupe.AvaloniaUI.Sample.Logging;
using EsUtil.Feature.Loupe.AvaloniaUI.Sample.Presentation;
using EsUtil.Feature.Loupe.AvaloniaUI.Sample.ViewModels;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Feature.Loupe.AvaloniaUI.Sample.Views;

/// <summary>メインウィンドウ。 マウス追従とルーペ配置を担う。</summary>
/// <remarks>
/// マウス位置（View 固有の操作）の取得とルーペの配置のみをコードビハインドで行い、<br/>
/// 外観設定は <see cref="MainWindowViewModel"/> へバインドする。<br/>
/// </remarks>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm = new();

    /// <summary>見本（<c>GridPreview</c>）の元画像。 本ウィンドウが生存する間保持する。</summary>
    private readonly EsBitmap _gridPreviewBitmap = GridLoupePlan.CreateSource();

    /// <summary>見本（<c>GridPreview</c>）の表示用画像（<see cref="_gridPreviewBitmap"/> を借りて包む非所有アダプタ）。</summary>
    private readonly EsBitmapAvaloniaAdapter _gridPreviewImage;

    /// <summary><see cref="MainWindow"/> の新しいインスタンスを初期化する。</summary>    /// <remarks>
    /// ルーペの初期化と初期中心座標を設定する。<br/>
    /// </remarks>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        // 見本の表示用画像を組み立てる（元画像は _gridPreviewBitmap が保持する）
        _gridPreviewImage = new EsBitmapAvaloniaAdapter(_gridPreviewBitmap);

        // ルーペの表示状態を明示的に反映（AXAML の初期値は非表示のため）
        Loupe.IsVisible = _vm.IsLoupeVisible;

        // 描画エラーの通知先として ILogger を設定する（デバッグ出力へ転送）
        Loupe.Log = new DebugLogger();

        // ルーペをカーソルの右下（+24 DIP）へ配置する。
        // 配置は LoupeControl.UpdatePosition（RenderTransform）が担い、ホスト側の Canvas.SetLeft/Top とは
        // 競合しない（Canvas.SetLeft/Top は LoupeControl の RenderTransform と加算され、ルーペが右下に
        // 大きくずれるため、配置はコントロール自身に任せる）。
        Loupe.PositionOffset = 24;

        // 初期状態でも拡大表示が見えるように中心を設定
        _vm.CenterPoint = new Point(120, 120);

        // 拡張例（計画の差し替え）: 外観と描画計画を差し替える（派生は不要）
        // 見本は DIP で書いた寸法を計画の倍率で換算するため、表示スケーリング（DPI）に関係なく同じ見た目になる
        GridPreview.Appearance = GridLoupePlan.Appearance;
        GridPreview.DrawPlanResolver = static plan => GridLoupePlan.Resolve(in plan);
        GridPreview.Options = LoupeOptions.Default with { RequestedSize = 150, ZoomLevel = 6 };
        GridPreview.Source = _gridPreviewImage;
        GridPreview.CenterPoint = new Point(120, 80);
        // 本見本は常に表示する（既定は非表示。 表示可否はホストが指示する）
        GridPreview.IsVisible = true;

        // ルーペ画像上の選択矩形の破線は、描画完了直後に書き込み可能な画像へ描く（Rendered の用途）
        Loupe.Rendered += OnLoupeRendered;

        // 選択矩形はルーペのプロパティに現れない入力のため、変更時は RequestRender() で再生成を要求する
        _vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <summary>ルーペの描画完了時に選択矩形の破線を描く。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">描画完了の通知引数（書き込み可能な画像と描画計画）。</param>
    /// <remarks>
    /// 破線の元データ（選択矩形）はルーペのプロパティに現れないため、描画完了のたびに描き直す。<br/>
    /// 変更の反映は <see cref="OnViewModelPropertyChanged"/> が <c>RequestRender()</c> で要求する。
    /// </remarks>
    private void OnLoupeRendered(object? sender, LoupeRenderedEventArgs e)
    {
        if (!_vm.SelectionRectVisible)
        {
            return;
        }

        var content = e.Content;
        SelectionRectPainter.Paint(e.Bitmap, in content, _vm.SelectionRect);
    }

    /// <summary>選択矩形の変更でルーペ画像の再生成を要求する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">プロパティ変更の通知引数。</param>
    /// <remarks>
    /// 選択矩形はルーペのプロパティに現れない（ホスト側のオーバーレイの元データ）ため、<br/>
    /// ルーペは自身のプロパティ変更では再生成されない。 <c>RequestRender()</c> が再生成の唯一の入口である。
    /// </remarks>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(MainWindowViewModel.SelectionRectVisible)
            or nameof(MainWindowViewModel.SelectionRectX)
            or nameof(MainWindowViewModel.SelectionRectY)
            or nameof(MainWindowViewModel.SelectionRectWidth)
            or nameof(MainWindowViewModel.SelectionRectHeight)))
        {
            return;
        }

        Loupe.RequestRender();
    }

    /// <summary>テスト画像上のマウス移動で中心座標を更新する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ポインタイベント引数。</param>
    /// <remarks>
    /// 中心座標は画像の外もそのまま許可する。 LoupeControl は画像外の領域を透明として切り出し、<br/>
    /// 画像外は無地の色（ペーストボード）で表示される（視野全体が画像外になった場合も全面がその色）。<br/>
    /// Stretch=None のため画像ローカル DIP ≒ 元画像ピクセル。<br/>
    /// ルーペの表示位置は LoupeControl.UpdatePosition（RenderTransform + PositionOffset）が計算するため、
    /// ホスト側では中心座標（CenterPoint）の設定のみを行う。
    /// </remarks>
    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(TestImage);

        // 画像範囲へクランプしない（画像外も中心として許容し、画像外の色を表示する）
        _vm.CenterPoint = new Point(pos.X, pos.Y);
    }

    /// <summary>Clear() の動作確認（非表示にして Bitmap を解放）。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnClearClicked(object? sender, RoutedEventArgs e) => Loupe.Clear();

    /// <summary>RequestRender() の動作確認（プロパティ変更を伴わない再生成要求）。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnRequestRenderClicked(object? sender, RoutedEventArgs e) => Loupe.RequestRender();
}
