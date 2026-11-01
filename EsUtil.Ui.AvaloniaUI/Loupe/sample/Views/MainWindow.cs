using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using EsUtil.Ui.AvaloniaUI.Interop.Imaging;
using EsUtil.Ui.AvaloniaUI.Loupe.Sample.Imaging;
using EsUtil.Ui.AvaloniaUI.Loupe.Sample.Presentation;
using EsUtil.Ui.AvaloniaUI.SampleKit;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Microsoft.Extensions.Logging;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.AvaloniaUI.Loupe.Sample.Views;

/// <summary>メイン ウィンドウ（設定パネル + テスト画像 + ルーペ）。</summary>
/// <remarks>
/// <b>示すこと</b>: <c>LoupeControl</c> の公開 API（<c>Source</c> / <c>DisplaySource</c> / <c>CenterPoint</c> /
/// <c>Options</c> / <c>Appearance</c> / <c>ClientToPixelScaling</c> / <c>PositionOffset</c> / <c>Log</c> /
/// <c>LogPrefix</c> / <c>DrawPlanResolver</c>）だけで、マウス追従の拡大表示・外観の変更・計画の差し替えができる。<br/>
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

    /// <summary>ログ（Serilog + 非同期表示）。</summary>
    private readonly SampleLogging _logging = new();

    /// <summary>ルーペ（マウス追従）。</summary>
    private readonly LoupeControl _loupe;

    /// <summary>見本のルーペ（外観と描画計画の差し替え）。</summary>
    private readonly LoupeControl _preview;

    /// <summary>見本の元画像。</summary>
    private readonly EsReadOnlyBitmap _previewSource = GridLoupePlan.CreateSource();

    /// <summary>選択矩形の破線枠。</summary>
    private readonly Avalonia.Controls.Shapes.Rectangle _selectionRect = new()
    {
        Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00)),
        StrokeThickness = 2,
        StrokeDashArray = [4, 2],
        Fill = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0x8C, 0x00)),
        IsHitTestVisible = false,
    };

    /// <summary>選択矩形の座標ラベル。</summary>
    private readonly TextBlock _selectionLabel = new()
    {
        FontSize = 11,
        FontWeight = FontWeight.SemiBold,
        Foreground = new SolidColorBrush(Colors.Orange),
        Background = new SolidColorBrush(Color.FromArgb(0xDD, 0xFF, 0xFF, 0xFF)),
        Padding = new Thickness(3, 1),
        IsHitTestVisible = false,
    };

    /// <summary>選択矩形の表示可否。</summary>
    private bool _selectionVisible = true;

    /// <summary>選択矩形（対象座標）。</summary>
    private EsRect _selection = new(250, 180, 300, 200);

    /// <summary>ルーペの外観。</summary>
    private LoupeAppearance _appearance = LoupeAppearance.Default;

    /// <summary>ウィンドウを初期化する。</summary>
    public MainWindow()
    {
        Title = "LoupeControl サンプル（AvaloniaUI）";
        Width = 1400;
        Height = 900;
        MinWidth = 900;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var logger = _logging.Logger;
        logger.LogInformation("サンプルを起動しました。");

        // ── 画像とルーペを重ねるキャンバス ──
        var canvas = new Canvas { Background = Brushes.White, ClipToBounds = true };
        var image = new Image
        {
            Source = _source.ToAvaloniaBitmap(),
            Width = ImageWidth,
            Height = ImageHeight,
            Stretch = Stretch.None,
        };
        canvas.Children.Add(image);
        canvas.Children.Add(_selectionRect);
        canvas.Children.Add(_selectionLabel);

        _loupe = new LoupeControl
        {
            Source = _source,
            DisplaySource = image.Source,
            PositionOffset = 24,
            Options = LoupeOptions.Default with { ZoomLevel = 4, RequestedSize = 240 },
            Appearance = _appearance,
            CenterPoint = new Point(120, 120),
            Log = logger,
            LogPrefix = "[Loupe]",
        };
        _preview = new LoupeControl
        {
            Source = _previewSource,
            DisplaySource = _previewSource.ToAvaloniaBitmap(),
            PositionOffset = 0,
            Options = LoupeOptions.Default with { ZoomLevel = 6, RequestedSize = 150 },
            Appearance = GridLoupePlan.Appearance,
            CenterPoint = new Point(70, 60),
            DrawPlanResolver = static plan => GridLoupePlan.Resolve(in plan),
            Log = logger,
            // 接頭辞はプロセス内の全インスタンスで共有される（設定パネルの「ログ接頭辞」が全ルーペへ効く）
        };
        canvas.Children.Add(_loupe);
        canvas.Children.Add(_preview);

        canvas.PointerMoved += (_, e) => _loupe.CenterPoint = e.GetPosition(image);

        // ルーペ画像上の破線は、描画完了直後に同じ描画コンテキストへ描く（Rendered の用途）
        _loupe.Rendered += (_, e) =>
        {
            if (!_selectionVisible)
            {
                return;
            }

            var content = e.Content;
            SelectionRectPainter.Paint(e.Context, in content, _selection);
        };

        UpdateSelection();

        // ── プロパティ設定（対象コントロールの公開プロパティ。 並びは他 UI と共通） ──
        var loupeVisible = (CheckBox)SampleSettings.Check("ルーペを表示", true, value => _loupe.IsVisible = value);

        var properties = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                loupeVisible,
                SampleSettings.Slider("拡大率", 4, 1, 16, 1, true, value => _loupe.Options = _loupe.Options with { ZoomLevel = (int)value }),
                SampleSettings.Slider("ルーペ サイズ", 160, 80, 480, 10, true, value => _loupe.Options = _loupe.Options with { RequestedSize = (int)value }),
                SampleSettings.Slider("位置オフセット", 0, 0, 64, 4, true, value => _loupe.PositionOffset = value),
                SampleSettings.Slider("中心 X", 120, 0, ImageWidth, 10, true, value => _loupe.CenterPoint = new Point(value, _loupe.CenterPoint?.Y ?? 0)),
                SampleSettings.Slider("中心 Y", 120, 0, ImageHeight, 10, true, value => _loupe.CenterPoint = new Point(_loupe.CenterPoint?.X ?? 0, value)),
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
                SampleSettings.Buttons(
                    ("ソースを設定", () => _loupe.Source = _source),
                    ("ソースを解除（描画資源を解放）", () =>
                    {
                        _loupe.Clear();
                        _loupe.Source = null;
                        loupeVisible.IsChecked = false;
                    })),
                SampleSettings.Buttons(
                    ("再描画を要求", () => _loupe.RequestRender()),
                    ("ソースを設定して再表示", () =>
                    {
                        _loupe.Source = _source;
                        loupeVisible.IsChecked = true;
                        _loupe.IsVisible = true;
                    }),
                    ("テスト ログを出力（非同期）", () => logger.LogInformation("テスト ログ（非同期）"))),
                SampleSettings.Check("選択矩形を表示", true, value =>
                {
                    _selectionVisible = value;
                    UpdateSelection();
                }),
                SampleSettings.Number("選択矩形 X", _selection.X, 0, ImageWidth, 10, true, value =>
                {
                    _selection = _selection with { X = value };
                    UpdateSelection();
                }),
                SampleSettings.Number("選択矩形 Y", _selection.Y, 0, ImageHeight, 10, true, value =>
                {
                    _selection = _selection with { Y = value };
                    UpdateSelection();
                }),
                SampleSettings.Number("選択矩形 W", _selection.Width, 0, ImageWidth, 10, true, value =>
                {
                    _selection = _selection with { Width = value };
                    UpdateSelection();
                }),
                SampleSettings.Number("選択矩形 H", _selection.Height, 0, ImageHeight, 10, true, value =>
                {
                    _selection = _selection with { Height = value };
                    UpdateSelection();
                }),
                SampleSettings.Check("見本（格子線と四隅の鉤）を表示", true, value => _preview.IsVisible = value),
            },
        };

        var log = new SampleLogPanel(_logging.Buffer);
        Content = SampleShell.CreateContent(properties, samples, canvas, log);

        Closed += (_, _) =>
        {
            _logging.Dispose();
            _source.Dispose();
            _previewSource.Dispose();
        };

        return;

        // 外観を更新してルーペへ反映し、再描画を要求する（計画に効く入力のため）
        void Apply(LoupeAppearance value)
        {
            _appearance = value;
            _loupe.Appearance = value;
            _loupe.RequestRender();
        }

        // 選択矩形の枠とラベルを更新する
        void UpdateSelection()
        {
            Canvas.SetLeft(_selectionRect, _selection.X);
            Canvas.SetTop(_selectionRect, _selection.Y);
            _selectionRect.Width = _selection.Width;
            _selectionRect.Height = _selection.Height;
            _selectionRect.IsVisible = _selectionVisible;

            Canvas.SetLeft(_selectionLabel, _selection.X + 4);
            Canvas.SetTop(_selectionLabel, _selection.Y - 20);
            _selectionLabel.Text = string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"({_selection.X:0}, {_selection.Y:0}) {_selection.Width:0}×{_selection.Height:0}");
            _selectionLabel.IsVisible = _selectionVisible;

            _loupe.RequestRender();
        }
    }

    /// <summary>色の選択肢の行を作る。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="selectedIndex">初期の選択インデックス。</param>
    /// <param name="apply">変更の反映先（選択インデックス）。</param>
    /// <returns>項目名とコンボ ボックスを並べた行。</returns>
    private static Control ColorChoice(string label, int selectedIndex, Action<int> apply)
        => SampleSettings.Choice(label, [.. s_colorChoices.Select(static choice => choice.Name)], selectedIndex, apply);

    /// <summary>「なし」を除いた色の選択肢から色を解決する。</summary>
    /// <param name="index">選択されたインデックス。</param>
    /// <param name="fallback">「なし」が選ばれたときに残す色（描画しないため見た目には影響しない）。</param>
    /// <returns>解決した色。</returns>
    private static DrawingColor ResolveColor(int index, DrawingColor fallback)
        => s_colorChoices[index].Color ?? fallback;
}
