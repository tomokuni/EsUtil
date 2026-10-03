using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using EsUtil.Feature.Loupe.AvaloniaUI.Sample.Logging;
using EsUtil.Feature.Loupe.AvaloniaUI.Sample.ViewModels;

namespace EsUtil.Feature.Loupe.AvaloniaUI.Sample.Views;

/// <summary>メインウィンドウ。 マウス追従とルーペ配置を担う。</summary>
/// <remarks>
/// マウス位置（View 固有の操作）の取得とルーペの配置のみをコードビハインドで行い、<br/>
/// 外観設定は <see cref="MainWindowViewModel"/> へバインドする。<br/>
/// </remarks>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm = new();

    /// <summary><see cref="MainWindow"/> の新しいインスタンスを初期化する。</summary>
    /// <remarks>
    /// ルーペの初期化と初期中心座標を設定する。<br/>
    /// </remarks>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

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
