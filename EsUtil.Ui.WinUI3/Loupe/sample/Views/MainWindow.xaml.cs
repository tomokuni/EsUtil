using EsUtil.Ui.WinUI3.Loupe.Sample.Imaging;
using EsUtil.Ui.WinUI3.Loupe.Sample.Logging;
using EsUtil.Ui.WinUI3.Loupe.Sample.Presentation;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinUI3.Loupe.Sample.Views;

/// <summary>メイン ウィンドウ（設定パネル + テスト画像 + ルーペ）を組み立てる。</summary>
/// <remarks>
/// <b>示すこと</b>: <c>LoupeControl</c> の公開 API（<c>Source</c> / <c>CenterPoint</c> / <c>Options</c> /<br/>
/// <c>Appearance</c> / <c>ClientToPixelScaling</c> / <c>Log</c> / <c>DrawPlanResolver</c>）だけで<br/>
/// マウス追従の拡大表示・外観の変更・計画の差し替えができる。<br/>
/// <br/>
/// <b>配置</b>: ルーペの表示位置・表示サイズはコアが決めるため、ホスト（本ウィンドウ）は<br/>
/// <c>ResolveLayout</c> の結果で子を配置するだけである（位置の計算もクランプも持たない）。<br/>
/// <br/>
/// <b>プロパティは CLR プロパティである</b>: 本体の公開プロパティは XAML で設定できないため、<br/>
/// すべてコードビハインドから設定する（構成は XAML、結線はコードビハインドという分担にする）。<br/>
/// <br/>
/// <b>拡張例</b>: 見本のルーペは外観と描画計画を差し替えて、十字線の代わりに格子線・枠線の代わりに四隅の鉤を描く<br/>
/// （描画層の派生は不要。 <see cref="GridLoupePlan"/>）。
/// </remarks>
public sealed partial class MainWindow : Window
{
    /// <summary>テスト画像の幅（ピクセル）。</summary>
    private const int ImageWidth = 800;

    /// <summary>テスト画像の高さ（ピクセル）。</summary>
    private const int ImageHeight = 600;

    /// <summary>ルーペ中心からの右下オフセット（DIP）。</summary>
    private const double PositionOffset = 24;

    /// <summary>ログの保持件数。</summary>
    private const int LogCapacity = 200;

    /// <summary>色の選択肢（「なし」は描画しないことを表す）。</summary>
    private static readonly (string Name, DrawingColor? Color)[] s_colorChoices =
    [
        ("なし", null),
        ("赤", DrawingColor.FromArgb(255, 255, 0, 0)),
        ("黄", DrawingColor.FromArgb(255, 255, 196, 0)),
        ("緑", DrawingColor.FromArgb(255, 0, 200, 83)),
        ("白", DrawingColor.White),
        ("薄灰", DrawingColor.FromArgb(255, 0xD3, 0xD3, 0xD3)),
        ("濃灰", DrawingColor.FromArgb(255, 0x53, 0x53, 0x53)),
        ("黒", DrawingColor.Black),
    ];

    /// <summary>画面へ出力したログ（末尾が新しい）。</summary>
    private readonly List<string> _logLines = [];

    /// <summary>選択矩形の状態。</summary>
    private readonly SelectionState _selection = new();

    /// <summary>テスト画像（ルーペのソース）。</summary>
    private EsReadOnlyBitmap? _source;

    /// <summary>見本の元画像（ルーペのソース）。</summary>
    private EsReadOnlyBitmap? _previewSource;

    /// <summary>ルーペの外観（設定は <c>with</c> 式で部分変更する）。</summary>
    private LoupeAppearance _appearance = LoupeAppearance.Default;

    /// <summary>メイン ウィンドウを初期化して結線する。</summary>
    public MainWindow()
    {
        InitializeComponent();

        var logger = new InMemoryLogger(AppendLog);

        _source = TestImageFactory.Create(ImageWidth, ImageHeight);
        _previewSource = GridLoupePlan.CreateSource();

        SourceCanvas.SetSource(_source);

        AddColorOptions(CrosshairColorBox, selectedIndex: 1);
        AddColorOptions(PasteboardColorBox, selectedIndex: 6);
        AddColorOptions(BorderColorBox, selectedIndex: 5);

        // ── ルーペ（マウス追従） ──
        Loupe.Source = _source;
        Loupe.PositionOffset = PositionOffset;
        Loupe.Options = LoupeOptions.Default with { ZoomLevel = 4, RequestedSize = 240 };
        Loupe.Appearance = _appearance;
        Loupe.Log = logger;
        Loupe.LogPrefix = "[Loupe]";
        Loupe.CenterPoint = new EsPoint(120, 120);
        Loupe.IsHitTestVisible = false;

        // ── 見本（外観と描画計画の差し替え） ──
        Preview.Source = _previewSource;
        Preview.PositionOffset = 0;
        Preview.Options = LoupeOptions.Default with { ZoomLevel = 6, RequestedSize = 150 };
        Preview.Appearance = GridLoupePlan.Appearance;
        Preview.CenterPoint = new EsPoint(70, 60);
        Preview.Log = logger;
        Preview.LogPrefix = "[Preview]";
        Preview.DrawPlanResolver = static plan => GridLoupePlan.Resolve(in plan);
        Preview.IsHitTestVisible = false;

        // ルーペ画像の中の破線は、描画完了直後に同一のキャンバスへ描く（Rendered の用途）
        Loupe.Rendered += (_, e) =>
        {
            var content = e.Content;
            // 選択矩形の破線は、ルーペ画像のピクセル座標で追加する（描画 API は公開しない）
            if (content.CaptureTopLeft is not { } topLeft || !_selection.IsVisible)
            {
                return;
            }

            var rect = _selection.Rect;
            var scale = content.ZoomLevel;
            e.AddRect(
                (rect.X - topLeft.X) * scale,
                (rect.Y - topLeft.Y) * scale,
                rect.Width * scale,
                1,
                0xFFFF0000);
            e.AddRect(
                (rect.X - topLeft.X) * scale,
                ((rect.Y + rect.Height) * scale) - (topLeft.Y * scale) - 1,
                rect.Width * scale,
                1,
                0xFFFF0000);
        };

        // ── 入力と変更の結線 ──
        HostCanvas.PointerMoved += OnHostPointerMoved;
        HostCanvas.SizeChanged += OnHostSizeChanged;
        ShowLoupeCheck.Checked += OnShowLoupeToggled;
        ShowLoupeCheck.Unchecked += OnShowLoupeToggled;
        ShowPreviewCheck.Checked += OnShowPreviewToggled;
        ShowPreviewCheck.Unchecked += OnShowPreviewToggled;
        ShowLabelCheck.Checked += OnShowLabelToggled;
        ShowLabelCheck.Unchecked += OnShowLabelToggled;
        ShowSelectionCheck.Checked += OnShowSelectionToggled;
        ShowSelectionCheck.Unchecked += OnShowSelectionToggled;
        ZoomSlider.ValueChanged += OnZoomChanged;
        SizeSlider.ValueChanged += OnSizeChanged;
        ScaleSlider.ValueChanged += OnScaleChanged;
        BorderThicknessSlider.ValueChanged += OnBorderThicknessChanged;
        CrosshairColorBox.SelectionChanged += OnColorChanged;
        PasteboardColorBox.SelectionChanged += OnColorChanged;
        BorderColorBox.SelectionChanged += OnColorChanged;
        SelectionXBox.ValueChanged += OnSelectionNumberChanged;
        SelectionYBox.ValueChanged += OnSelectionNumberChanged;
        SelectionWidthBox.ValueChanged += OnSelectionNumberChanged;
        SelectionHeightBox.ValueChanged += OnSelectionNumberChanged;
        RequestRenderButton.Click += OnRequestRender;
        HideAndReleaseButton.Click += OnHideAndRelease;
        ResetSourceButton.Click += OnResetSource;
        _selection.Changed += (_, _) => ApplySelection();

        Closed += OnWindowClosed;

        ApplySelection();
        ApplyAppearance();
        UpdateLoupePlacement();
    }

    /// <summary>テスト画像の上をマウスが移動したときに、ルーペの拡大中心を追従させる。</summary>
    /// <param name="sender">イベントの送信元（ホストのキャンバス）。</param>
    /// <param name="e">ポインターのイベント引数。</param>
    /// <remarks>
    /// ホストのキャンバスはテスト画像と同じ DIP サイズで置くため、そのままの座標が元画像ピクセル座標になる。<br/>
    /// 位置はルーペが持たないため、中心の更新後にホストが配置をやり直す。
    /// </remarks>
    private void OnHostPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(HostCanvas).Position;
        Loupe.CenterPoint = new EsPoint(position.X, position.Y);
        UpdateLoupePlacement();
    }

    /// <summary>ホストの大きさが変わったときにルーペを配置し直す。</summary>
    /// <param name="sender">イベントの送信元（ホストのキャンバス）。</param>
    /// <param name="e">サイズのイベント引数。</param>
    private void OnHostSizeChanged(object sender, SizeChangedEventArgs e) => UpdateLoupePlacement();

    /// <summary>ルーペの表示を切り替える。</summary>
    /// <param name="sender">イベントの送信元（チェック ボックス）。</param>
    /// <param name="e">イベント引数。</param>
    /// <remarks>
    /// WinUI 3 の <c>IsVisible</c> は読み取り専用であるため、表示の切り替えは <c>Visibility</c> で行う<br/>
    /// （非表示の間はコアの表示レイアウトが位置を返さないため、ルーペは描画されない）。
    /// </remarks>
    private void OnShowLoupeToggled(object sender, RoutedEventArgs e)
    {
        Loupe.Visibility = ShowLoupeCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        UpdateLoupePlacement();
    }

    /// <summary>見本のルーペの表示を切り替える。</summary>
    /// <param name="sender">イベントの送信元（チェック ボックス）。</param>
    /// <param name="e">イベント引数。</param>
    private void OnShowPreviewToggled(object sender, RoutedEventArgs e)
    {
        Preview.Visibility = ShowPreviewCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        UpdateLoupePlacement();
    }

    /// <summary>座標ラベルの表示を切り替える（外観の部分変更）。</summary>
    /// <param name="sender">イベントの送信元（チェック ボックス）。</param>
    /// <param name="e">イベント引数。</param>
    private void OnShowLabelToggled(object sender, RoutedEventArgs e)
    {
        _appearance = _appearance with { ShowCoordLabel = ShowLabelCheck.IsChecked == true };
        ApplyAppearance();
    }

    /// <summary>選択矩形の表示を切り替える。</summary>
    /// <param name="sender">イベントの送信元（チェック ボックス）。</param>
    /// <param name="e">イベント引数。</param>
    private void OnShowSelectionToggled(object sender, RoutedEventArgs e)
        => _selection.IsVisible = ShowSelectionCheck.IsChecked == true;

    /// <summary>拡大率の変更をルーペへ反映する。</summary>
    /// <param name="sender">イベントの送信元（スライダー）。</param>
    /// <param name="e">イベント引数。</param>
    private void OnZoomChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        Loupe.Options = Loupe.Options with { ZoomLevel = (int)e.NewValue };
        UpdateLoupePlacement();
    }

    /// <summary>ルーペのサイズの変更をルーペへ反映する。</summary>
    /// <param name="sender">イベントの送信元（スライダー）。</param>
    /// <param name="e">イベント引数。</param>
    private void OnSizeChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        Loupe.Options = Loupe.Options with { RequestedSize = (int)e.NewValue };
        UpdateLoupePlacement();
    }

    /// <summary>描画スケール（環境値）の変更をルーペへ反映する。</summary>
    /// <param name="sender">イベントの送信元（スライダー）。</param>
    /// <param name="e">イベント引数。</param>
    /// <remarks>
    /// 通常は描画のたびに描画面と割り当て領域の比で上書きされるため、<br/>
    /// 本スライダーは「環境値の補正と再描画」を確認するためのものである。
    /// </remarks>
    private void OnScaleChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        Loupe.ClientToPixelScaling = e.NewValue;
        UpdateLoupePlacement();
    }

    /// <summary>枠線の太さの変更を外観へ反映する。</summary>
    /// <param name="sender">イベントの送信元（スライダー）。</param>
    /// <param name="e">イベント引数。</param>
    private void OnBorderThicknessChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        _appearance = _appearance with { BorderThickness = (int)e.NewValue };
        ApplyAppearance();
    }

    /// <summary>色の選択の変更を外観へ反映する（「なし」は表示の停止を意味する）。</summary>
    /// <param name="sender">イベントの送信元（コンボ ボックス）。</param>
    /// <param name="e">イベント引数。</param>
    /// <remarks>
    /// 透明色を「描かない」の代用にせず、色と表示可否（<c>Show*</c>）を別々に設定する。
    /// </remarks>
    private void OnColorChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox box || box.SelectedIndex < 0)
        {
            return;
        }

        var index = box.SelectedIndex;
        var choice = s_colorChoices[index];
        var isVisible = choice.Color is not null;

        if (ReferenceEquals(box, CrosshairColorBox))
        {
            _appearance = _appearance with
            {
                CrossColor = choice.Color ?? _appearance.CrossColor,
                ShowCrosshair = isVisible,
            };
        }
        else if (ReferenceEquals(box, PasteboardColorBox))
        {
            _appearance = _appearance with
            {
                PasteboardColor = choice.Color ?? _appearance.PasteboardColor,
                ShowPasteboard = isVisible,
            };
        }
        else if (ReferenceEquals(box, BorderColorBox))
        {
            _appearance = _appearance with
            {
                BorderColor = choice.Color ?? _appearance.BorderColor,
                ShowBorder = isVisible,
            };
        }

        ApplyAppearance();
    }

    /// <summary>選択矩形の数値入力の変更を状態へ反映する。</summary>
    /// <param name="sender">イベントの送信元（数値入力）。</param>
    /// <param name="e">イベント引数。</param>
    private void OnSelectionNumberChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        // 数値入力は空欄（NaN）になりうるため、確定している値だけを反映する
        if (double.IsNaN(e.NewValue))
        {
            return;
        }

        if (ReferenceEquals(sender, SelectionXBox))
        {
            _selection.X = e.NewValue;
        }
        else if (ReferenceEquals(sender, SelectionYBox))
        {
            _selection.Y = e.NewValue;
        }
        else if (ReferenceEquals(sender, SelectionWidthBox))
        {
            _selection.Width = e.NewValue;
        }
        else if (ReferenceEquals(sender, SelectionHeightBox))
        {
            _selection.Height = e.NewValue;
        }
    }

    /// <summary>ルーペへ再描画を要求する（プロパティ変更を伴わない再描画の確認）。</summary>
    /// <param name="sender">イベントの送信元（ボタン）。</param>
    /// <param name="e">イベント引数。</param>
    private void OnRequestRender(object sender, RoutedEventArgs e) => Loupe.RequestRedraw();

    /// <summary>ルーペを非表示にしてソースを解放する。</summary>
    /// <param name="sender">イベントの送信元（ボタン）。</param>
    /// <param name="e">イベント引数。</param>
    /// <remarks>画像の所有権はホストにあるため、ここではルーペの参照だけを外す（画像は解放しない）。</remarks>
    private void OnHideAndRelease(object sender, RoutedEventArgs e)
    {
        Loupe.Visibility = Visibility.Collapsed;
        ShowLoupeCheck.IsChecked = false;
        Loupe.Source = null;
        UpdateLoupePlacement();
    }

    /// <summary>ルーペのソースを再設定する（未設定時の下地の確認）。</summary>
    /// <param name="sender">イベントの送信元（ボタン）。</param>
    /// <param name="e">イベント引数。</param>
    private void OnResetSource(object sender, RoutedEventArgs e)
    {
        Loupe.Source = _source;
        Loupe.Visibility = Visibility.Visible;
        ShowLoupeCheck.IsChecked = true;
        UpdateLoupePlacement();
    }

    /// <summary>ウィンドウを閉じるときにテスト画像を解放する。</summary>
    /// <param name="sender">イベントの送信元（ウィンドウ）。</param>
    /// <param name="e">イベント引数。</param>
    private void OnWindowClosed(object sender, WindowEventArgs e)
    {
        _previewSource?.Dispose();
        _previewSource = null;
        _source?.Dispose();
        _source = null;
    }

    /// <summary>ルーペをコアが解決した位置・サイズへ配置する。</summary>
    /// <remarks>
    /// 位置の計算・位置の補正はコア（<c>ResolveLayout</c>）が単一所有するため、<br/>
    /// ホストは結果の矩形を要素へ写すだけである。 位置が返らない場合（拡大中心が未設定・親領域が収まらない）は<br/>
    /// サイズ 0 で置く（「配置しない」の判断もコアが持つ）。
    /// </remarks>
    private void UpdateLoupePlacement()
    {
        var area = new EsSize(HostCanvas.ActualWidth, HostCanvas.ActualHeight);

        ArrangeLoupe(Loupe, area);
        ArrangeLoupe(Preview, area);
        UpdateSelectionLabel();
    }

    /// <summary>1 つのルーペをコアの解決結果へ配置する。</summary>
    /// <param name="loupe">配置するルーペ。</param>
    /// <param name="area">親領域のサイズ（DIP）。</param>
    private static void ArrangeLoupe(LoupeControl loupe, EsSize area)
    {
        var layout = loupe.Visibility == Visibility.Visible ? loupe.ResolveLayout(area) : default;

        if (layout.Position is not { } position)
        {
            loupe.Width = 0;
            loupe.Height = 0;
            return;
        }

        loupe.Width = layout.ClientSize;
        loupe.Height = layout.ClientSize;
        Canvas.SetLeft(loupe, position.X);
        Canvas.SetTop(loupe, position.Y);
    }

    /// <summary>現在の外観をルーペへ反映する。</summary>
    private void ApplyAppearance() => Loupe.Appearance = _appearance;

    /// <summary>選択矩形の状態をテスト画像の破線枠とラベルへ反映する。</summary>
    /// <remarks>
    /// ルーペ内の破線は描画完了イベントが描画するため、入力の反映には再描画の要求が要る<br/>
    /// （プロパティに現れない入力であるため <c>Invalidate</c> で要求する）。
    /// </remarks>
    private void ApplySelection()
    {
        var rect = _selection.Rect;
        var isVisible = _selection.IsVisible && rect.Width > 0 && rect.Height > 0;

        SourceCanvas.SetSelection(rect, isVisible);
        UpdateSelectionLabel();
        Loupe.RequestRedraw();
    }

    /// <summary>選択矩形の座標ラベルを選択矩形の左上へ配置する。</summary>
    /// <remarks>上端を超える場合は枠の内側へ落とす（画像の外へ出さない）。</remarks>
    private void UpdateSelectionLabel()
    {
        var rect = _selection.Rect;
        if (!_selection.IsVisible || rect.Width <= 0 || rect.Height <= 0)
        {
            SelectionLabel.Visibility = Visibility.Collapsed;
            return;
        }

        SelectionLabel.Text = _selection.Label;
        SelectionLabel.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionLabel, rect.X + 4);
        Canvas.SetTop(SelectionLabel, Math.Max(0, rect.Y - 20));
    }

    /// <summary>色の選択肢をコンボ ボックスへ設定する。</summary>
    /// <param name="box">設定するコンボ ボックス。</param>
    /// <param name="selectedIndex">最初に選択する選択肢のインデックス。</param>
    /// <remarks>選択肢の設定は選択の変更イベントより先に行う（設定中の通知で外観を壊さない）。</remarks>
    private static void AddColorOptions(ComboBox box, int selectedIndex)
    {
        foreach (var choice in s_colorChoices)
        {
            box.Items.Add(choice.Name);
        }

        box.SelectedIndex = selectedIndex;
    }

    /// <summary>ログの 1 行を画面のログ表示へ追加する。</summary>
    /// <param name="line">整形済みの 1 行。</param>
    private void AppendLog(string line)
    {
        _logLines.Add(line);
        if (_logLines.Count > LogCapacity)
        {
            _logLines.RemoveAt(0);
        }

        LogText.Text = string.Join(Environment.NewLine, _logLines);
    }
}
