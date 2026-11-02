using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Ui.WinUI3.Loupe.Sample.Imaging;
using EsUtil.Ui.WinUI3.Loupe.Sample.Presentation;
using EsUtil.Ui.WinUI3.SampleKit;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinUI3.Loupe.Sample.Views;

/// <summary>メイン ウィンドウ（設定パネル + テスト画像 + ルーペ）を組み立てる。</summary>
/// <remarks>
/// <b>示すこと</b>: <c>LoupeControl</c> の公開 API（<c>Source</c> / <c>CenterPoint</c> / <c>Options</c> /
/// <c>Appearance</c> / <c>ClientToPixelScaling</c> / <c>PositionOffset</c> / <c>Log</c> / <c>LogPrefix</c> /
/// <c>DrawPlanResolver</c>）だけで、マウス追従の拡大表示・外観の変更・計画の差し替えができる。<br/>
/// <b>配置</b>: サンプル共通の <see cref="SampleShell"/>（左: 設定 / 右上: 画像 / 右下: ログ）を使う。<br/>
/// <b>ログ</b>: Serilog の出力を非同期に表示する（<see cref="SampleLogging"/>）。
/// </remarks>
public sealed class MainWindow : Window
{
    /// <summary>テスト画像の幅（ピクセル）。</summary>
    private const int ImageWidth = 800;

    /// <summary>テスト画像の高さ（ピクセル）。</summary>
    private const int ImageHeight = 600;

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
        ("半透明の赤", DrawingColor.FromArgb(102, 255, 0, 0)),
        ("黒 50%", DrawingColor.FromArgb(153, 0, 0, 0)),
        ("黒", DrawingColor.Black),
    ];

    /// <summary>テスト画像（拡大元）。</summary>
    private readonly EsReadOnlyBitmap _source = TestImageFactory.Create(ImageWidth, ImageHeight);

    /// <summary>見本の元画像。</summary>
    private readonly EsReadOnlyBitmap _previewSource = GridLoupePlan.CreateSource();

    /// <summary>ログ（Serilog + 非同期表示）。</summary>
    private readonly SampleLogging _logging = new();

    /// <summary>ルーペ（マウス追従）。</summary>
    private readonly LoupeControl _loupe = new();

    /// <summary>見本のルーペ。</summary>
    private readonly LoupeControl _preview = new();

    /// <summary>画像と選択矩形を表示する面。</summary>
    private readonly SourceCanvas _canvas = new() { Width = ImageWidth, Height = ImageHeight };

    /// <summary>選択矩形の座標ラベル。</summary>
    private readonly TextBlock _selectionLabel = new()
    {
        FontSize = 11,
        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0x8C, 0x00)),
        Visibility = Visibility.Collapsed,
    };

    /// <summary>選択矩形の状態（サンプル固有）。</summary>
    private readonly SelectionState _selection = new();

    /// <summary>ルーペの外観。</summary>
    private LoupeAppearance _appearance = LoupeAppearance.Default;

    /// <summary>メイン ウィンドウを初期化する。</summary>
    public MainWindow()
    {
        var logger = _logging.Logger;
        logger.LogInformation("サンプルを起動しました。");

        _canvas.SetSource(_source);

        _loupe.Source = _source;
        _loupe.PositionOffset = 24;
        _loupe.Options = LoupeOptions.Default with { ZoomLevel = 4, RequestedSize = 160 };
        _loupe.Appearance = _appearance;
        _loupe.CenterPoint = new EsPoint(120, 120);
        _loupe.Log = logger;
        _loupe.LogPrefix = "[Loupe]";

        _preview.Source = _previewSource;
        _preview.Options = LoupeOptions.Default with { ZoomLevel = 6, RequestedSize = 150 };
        _preview.Appearance = GridLoupePlan.Appearance;
        _preview.CenterPoint = new EsPoint(70, 60);
        _preview.DrawPlanResolver = static plan => GridLoupePlan.Resolve(in plan);
        _preview.Log = logger;
        // 接頭辞はプロセス内の全インスタンスで共有される（設定パネルの「ログ接頭辞」が全ルーペへ効く）

        var host = new Canvas
        {
            Width = ImageWidth,
            Height = ImageHeight,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x20, 0x20, 0x20)),
        };
        host.Children.Add(_canvas);
        host.Children.Add(_selectionLabel);
        host.Children.Add(_loupe);
        host.Children.Add(_preview);
        host.PointerMoved += (_, e) =>
        {
            var position = e.GetCurrentPoint(_canvas).Position;
            _loupe.CenterPoint = new EsPoint(position.X, position.Y);
            UpdatePlacement();
        };

        // 選択矩形の破線は、描画完了直後に同じキャンバスへ描く（Rendered の用途）
        _loupe.Rendered += (_, e) =>
        {
            var content = e.Content;
            if (content.CaptureTopLeft is not { } topLeft || !_selection.IsVisible)
            {
                return;
            }

            var rect = _selection.Rect;
            var scale = content.ZoomLevel;
            e.AddRect((rect.X - topLeft.X) * scale, (rect.Y - topLeft.Y) * scale, rect.Width * scale, 1, 0xFFFF0000);
            e.AddRect(
                (rect.X - topLeft.X) * scale,
                ((rect.Y + rect.Height) * scale) - (topLeft.Y * scale) - 1,
                rect.Width * scale,
                1,
                0xFFFF0000);
        };

        _selection.Changed += (_, _) => UpdateSelection();
        UpdateSelection();
        UpdatePlacement();

        // ── プロパティ設定（対象コントロールの公開プロパティ。 並びは他 UI と共通） ──
        var loupeVisible = (CheckBox)SampleSettings.Check("ルーペを表示", true, value =>
        {
            _loupe.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            UpdatePlacement();
        });

        var properties = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                loupeVisible,
                SampleSettings.Slider("拡大率", 4, 1, 16, 1, true, value => _loupe.Options = _loupe.Options with { ZoomLevel = (int)value }),
                SampleSettings.Slider("ルーペ サイズ", 160, 80, 480, 10, true, value => _loupe.Options = _loupe.Options with { RequestedSize = (int)value }),
                SampleSettings.Slider("位置オフセット", 0, 0, 64, 4, true, value =>
                {
                    _loupe.PositionOffset = value;
                    UpdatePlacement();
                }),
                SampleSettings.Slider("中心 X", 120, 0, ImageWidth, 10, true, value => SetCenter(value, _loupe.CenterPoint?.Y ?? 0)),
                SampleSettings.Slider("中心 Y", 120, 0, ImageHeight, 10, true, value => SetCenter(_loupe.CenterPoint?.X ?? 0, value)),
                SampleSettings.Buttons(
                    ("ソースを設定", () => _loupe.Source = _source),
                    ("ソースを解除（描画資源を解放）", () =>
                    {
                        _loupe.Visibility = Visibility.Collapsed;
                        _loupe.Source = null;
                        loupeVisible.IsChecked = false;
                        UpdatePlacement();
                    }),
                    ("ソースを設定して再表示", () =>
                    {
                        _loupe.Source = _source;
                        loupeVisible.IsChecked = true;
                        _loupe.Visibility = Visibility.Visible;
                        UpdatePlacement();
                    })),
                ColorChoice("十字線の色", 7, index => Apply(_appearance with
                {
                    CrossColor = ResolveColor(index, _appearance.CrossColor),
                    ShowCrosshair = s_colorChoices[index].Color is not null,
                })),
                ColorChoice("画像外の色", 6, index => Apply(_appearance with
                {
                    PasteboardColor = ResolveColor(index, _appearance.PasteboardColor),
                    ShowPasteboard = s_colorChoices[index].Color is not null,
                })),
                ColorChoice("枠線の色", 5, index => Apply(_appearance with
                {
                    BorderColor = ResolveColor(index, _appearance.BorderColor),
                    ShowBorder = s_colorChoices[index].Color is not null,
                })),
                SampleSettings.Slider("枠線の太さ", 2, 1, 8, 1, true, value => Apply(_appearance with { BorderThickness = (int)value })),
                SampleSettings.Check("座標ラベルを表示", true, value => Apply(_appearance with { ShowCoordLabel = value })),
                ColorChoice("座標ラベルの色", 4, index => Apply(_appearance with { CoordLabelColor = ResolveColor(index, _appearance.CoordLabelColor) })),
                ColorChoice("座標ラベルの背景色", 8, index => Apply(_appearance with { CoordLabelBackgroundColor = ResolveColor(index, _appearance.CoordLabelBackgroundColor) })),
                SampleSettings.Slider("座標ラベルの文字サイズ", 12, 6, 24, 1, true, value => Apply(_appearance with { CoordLabelFontSize = value })),
                SampleSettings.Slider("座標ラベルの角丸", 2, 0, 12, 1, true, value => Apply(_appearance with { CoordLabelCornerRadius = (int)value })),
                SampleSettings.Slider("座標ラベルの間隔", 2, 0, 24, 1, true, value => Apply(_appearance with { CoordLabelOffset = value })),
                SampleSettings.Text("ログ接頭辞", "[Loupe]", value => _loupe.LogPrefix = value),
                SampleSettings.Check("見本の描画計画（格子線・四隅の鉤）", false, value =>
                    _loupe.DrawPlanResolver = value ? static plan => GridLoupePlan.Resolve(in plan) : null),
            },
        };

        // ── サンプル設定（サンプル固有の操作。 並びは他 UI と共通） ──
        var samples = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                SampleSettings.LogLevel(_logging),
                SampleSettings.Buttons(("テスト ログを出力（非同期）", () => logger.LogInformation("テスト ログ（非同期）"))),
                SampleSettings.Check("選択矩形を表示", true, value => _selection.IsVisible = value),
                SampleSettings.Number("選択矩形 X", _selection.X, 0, ImageWidth, 10, true, value => _selection.X = value),
                SampleSettings.Number("選択矩形 Y", _selection.Y, 0, ImageHeight, 10, true, value => _selection.Y = value),
                SampleSettings.Number("選択矩形 W", _selection.Width, 0, ImageWidth, 10, true, value => _selection.Width = value),
                SampleSettings.Number("選択矩形 H", _selection.Height, 0, ImageHeight, 10, true, value => _selection.Height = value),
                SampleSettings.Check("見本（格子線と四隅の鉤）を表示", true, value =>
                    _preview.Visibility = value ? Visibility.Visible : Visibility.Collapsed),
            },
        };

        var scroll = new ScrollViewer
        {
            Content = host,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        var log = new SampleLogPanel(_logging.Buffer);
        Title = "LoupeControl サンプル（WinUI 3）";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1400, 900));
        // ── API操作（対象コントロールのメソッド実行） ──
        var methods = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                SampleSettings.Buttons(("再描画を要求", () => _loupe.RequestRedraw())),
            },
        };

        Content = SampleShell.CreateContent(properties, methods, samples, scroll, log);
        Closed += (_, _) =>
        {
            _logging.Dispose();
            _source.Dispose();
            _previewSource.Dispose();
        };

        return;

        // 拡大中心を設定し、配置を更新する
        void SetCenter(double x, double y)
        {
            _loupe.CenterPoint = new EsPoint(x, y);
            UpdatePlacement();
        }

        // 外観を更新してルーペへ反映する
        void Apply(LoupeAppearance value)
        {
            _appearance = value;
            _loupe.Appearance = value;
        }

        // 選択矩形の枠とラベルを更新する
        void UpdateSelection()
        {
            _canvas.SetSelection(_selection.Rect, _selection.IsVisible);

            Canvas.SetLeft(_selectionLabel, _selection.X + 4);
            Canvas.SetTop(_selectionLabel, _selection.Y - 20);
            _selectionLabel.Text = _selection.Label;
            _selectionLabel.Visibility = _selection.IsVisible ? Visibility.Visible : Visibility.Collapsed;

            _loupe.RequestRedraw();
        }

        // ルーペをコアが解決した位置・サイズへ配置する
        void UpdatePlacement()
        {
            var area = new EsSize(ImageWidth, ImageHeight);
            Arrange(_loupe, area);
            Arrange(_preview, area);
        }

        // 1 つのルーペをコアの解決結果へ配置する
        static void Arrange(LoupeControl loupe, EsSize area)
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
    }

    /// <summary>色の選択肢の行を作る。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="selectedIndex">初期の選択インデックス。</param>
    /// <param name="apply">変更の反映先（選択インデックス）。</param>
    /// <returns>項目名とコンボ ボックスを並べた行。</returns>
    private static UIElement ColorChoice(string label, int selectedIndex, Action<int> apply)
        => SampleSettings.Choice(label, [.. s_colorChoices.Select(static choice => choice.Name)], selectedIndex, apply);

    /// <summary>「なし」を除いた色の選択肢から色を解決する。</summary>
    /// <param name="index">選択されたインデックス。</param>
    /// <param name="fallback">「なし」が選ばれたときに残す色（描画しないため見た目には影響しない）。</param>
    /// <returns>解決した色。</returns>
    private static DrawingColor ResolveColor(int index, DrawingColor fallback)
        => s_colorChoices[index].Color ?? fallback;
}
