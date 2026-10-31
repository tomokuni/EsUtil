using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Ui.WinUI3.Marquee.Sample.Presentation;
using EsUtil.Ui.WinUI3.Marquee.Sample.Views;
using EsUtil.Ui.WinUI3.SampleKit;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinUI3.Marquee.Sample;

/// <summary>サンプルのメイン ウィンドウ（背景 + ドラッグ選択 + ログ表示）。</summary>
/// <remarks>
/// <b>示すこと</b>: コントロールの公開 API（<c>SetCoordTransform</c> / <c>Options</c> / <c>Appearance</c> /
/// <c>SetSelection</c> / <c>ResetSelection</c> / <c>RequestConfirm</c> / <c>RequestCancel</c> / <c>Log</c>）だけで
/// ドラッグ選択・表示倍率・外観の変更・確定/キャンセルの流れを組める。<br/>
/// <b>配置</b>: サンプル共通の <see cref="SampleShell"/>（左: 設定 / 右上: 画像 / 右下: ログ）を使う。<br/>
/// <b>ログ</b>: Serilog の出力を非同期に表示する（<see cref="SampleLogging"/>）。
/// </remarks>
public sealed class MainWindow : Window
{
    /// <summary>背景（選択対象）の幅（ピクセル。 座標変換ポートの境界として使う）。</summary>
    private const int SourceWidth = 800;

    /// <summary>背景（選択対象）の高さ（ピクセル。 座標変換ポートの境界として使う）。</summary>
    private const int SourceHeight = 600;

    /// <summary>ルーペ位置の目印の一辺（DIP）。</summary>
    private const double LoupeMarkerSize = 24.0;

    /// <summary>ズームの最小倍率。</summary>
    private const double MinZoom = 0.1;

    /// <summary>ズームの最大倍率。</summary>
    private const double MaxZoom = 5.0;

    /// <summary>ズームの刻み（スライダーの増減）。</summary>
    private const double ZoomStep = 0.1;

    /// <summary>色の選択肢（「なし」は描画しないことを表す）。</summary>
    private static readonly (string Name, DrawingColor? Color)[] s_colorChoices =
    [
        ("なし", null),
        ("白", DrawingColor.White),
        ("アンバー", DrawingColor.FromArgb(255, 255, 196, 0)),
        ("赤", DrawingColor.FromArgb(255, 255, 0, 0)),
        ("緑", DrawingColor.FromArgb(255, 0, 200, 83)),
        ("薄灰", DrawingColor.FromArgb(255, 0xD3, 0xD3, 0xD3)),
    ];

    /// <summary>ログ（Serilog + 非同期表示）。</summary>
    private readonly SampleLogging _logging = new();

    /// <summary>ドラッグ選択コントロール。</summary>
    private readonly MarqueeControl _marquee = new();

    /// <summary>状態の表示。</summary>
    private readonly TextBlock _status = new() { Text = "選択なし", FontSize = 11, TextWrapping = TextWrapping.Wrap };

    /// <summary>ルーペ位置の目印。</summary>
    private readonly Ellipse _loupeMarker = new()
    {
        Width = LoupeMarkerSize,
        Height = LoupeMarkerSize,
        Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xC4, 0x00)),
        StrokeThickness = 2,
        Visibility = Visibility.Collapsed,
    };

    /// <summary>現在の表示倍率（座標変換ポートが描画のたびに読む）。</summary>
    private double _zoom = 1.0;

    /// <summary>メイン ウィンドウを初期化する。</summary>
    public MainWindow()
    {
        var logger = _logging.Logger;
        logger.LogInformation("サンプルを起動しました。");
        _marquee.Log = logger;
        _marquee.LogPrefix = "[Marquee]";

        // 背景（選択対象）と選択オーバーレイを重ねる
        var background = new TestPatternCanvas();
        var markerCanvas = new Canvas { IsHitTestVisible = false };
        markerCanvas.Children.Add(_loupeMarker);

        var surface = new Grid
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x20, 0x20, 0x20)),
            MinHeight = 240,
        };
        surface.Children.Add(background);
        surface.Children.Add(_marquee);
        surface.Children.Add(markerCanvas);

        void Write(string line) => logger.Log(LogLevel.Information, default, line, null, static (state, _) => state);

        _marquee.SetCoordTransform(CoordTransform.ClientToSource(() => _zoom, () => new EsSize(SourceWidth, SourceHeight)));

        _marquee.SelectionStarted += (_, e) => Write($"SelectionStarted: ({e.Point.X:0}, {e.Point.Y:0})");
        _marquee.SelectionChanged += (_, e) => Write($"SelectionChanged: {FormatRect(e.Rect)}");
        _marquee.SelectionCompleted += (_, e) => Write($"SelectionCompleted: {FormatRect(e.Rect)}");
        _marquee.SelectionCancelled += (_, _) => Write("SelectionCancelled");
        _marquee.ConfirmRequested += (_, e) => Write($"ConfirmRequested: {FormatRect(e.Rect)}");
        _marquee.CancelRequested += (_, _) => Write("CancelRequested");
        _marquee.LoupePositionMoved += (_, e) =>
        {
            // 通知は対象座標で届くため、表示 DIP へ戻して目印を置く
            Canvas.SetLeft(_loupeMarker, e.Point.X / _zoom);
            Canvas.SetTop(_loupeMarker, e.Point.Y / _zoom);
            _loupeMarker.Visibility = Visibility.Visible;
            _status.Text = $"追従位置: X={e.Point.X:0} Y={e.Point.Y:0}";
        };

        // 起動直後に枠線・ハンドル・ラベルが見えるように、選択を 1 つ与えておく
        _marquee.SetSelection(new EsRect(120, 80, 240, 180));

        var appearance = MarqueeAppearance.Default;

        // ── プロパティ設定（対象コントロールの公開プロパティ。 並びは他 UI と共通） ──
        var properties = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                SampleSettings.Slider("ズーム", _zoom, MinZoom, MaxZoom, ZoomStep, false, value =>
                {
                    _zoom = value;
                    _marquee.InvalidateRender();
                }),
                SampleSettings.Slider("表示スケール", 1.0, 0.5, 2.0, 0.1, false, value => _marquee.DisplayScaleScaling = value),
                SampleSettings.Slider("ハンドル サイズ", 10, 2, 30, 1, true, value => _marquee.Options = _marquee.Options with { HandleSize = value }),
                SampleSettings.Slider("ハンドル枠の太さ", 1.5, 0.5, 8, 0.5, false, value => _marquee.Options = _marquee.Options with { HandleBorderThickness = value }),
                SampleSettings.Slider("マーカーの長さ", 16, 8, 40, 2, true, value => _marquee.Options = _marquee.Options with { MarkerLength = value }),
                SampleSettings.Slider("ヒット半径", 12, 2, 40, 1, true, value => _marquee.Options = _marquee.Options with { HandleHitRadius = value }),
                SampleSettings.Slider("最小選択サイズ", 6, 1, 40, 1, true, value => _marquee.Options = _marquee.Options with { MinSelectionSize = value }),
                SampleSettings.Check("画像内にクリップ", true, value => _marquee.Options = _marquee.Options with { ClampToSourceBounds = value }),
                SampleSettings.Text("ログ接頭辞", "[Marquee]", value => _marquee.LogPrefix = value),
                SampleSettings.Check("描画計画を差し替える（破線・ガイド線）", false, value =>
                    _marquee.DrawPlanResolver = value ? static plan => GuideMarqueePlan.Resolve(in plan) : null),
                SampleSettings.Check("ハンドルを表示", true, value => Apply(appearance with { ShowHandles = value })),
                ColorChoice("ハンドルの色", 1, index => Apply(appearance with { HandleColor = ResolveColor(index, appearance.HandleColor) })),
                ColorChoice("ハンドル枠の色", 2, index => Apply(appearance with { HandleBorderColor = ResolveColor(index, appearance.HandleBorderColor) })),
                SampleSettings.Check("マーカーを表示", true, value => Apply(appearance with { ShowMarker = value })),
                ColorChoice("マーカーの色", 3, index => Apply(appearance with { MarkerColor = ResolveColor(index, appearance.MarkerColor) })),
                SampleSettings.Slider("マーカーの太さ", 2, 0.5, 8, 0.5, false, value => Apply(appearance with { MarkerThickness = value })),
                SampleSettings.Check("外枠を表示", true, value => Apply(appearance with { ShowOuterBorder = value })),
                ColorChoice("外枠の色", 5, index => Apply(appearance with { OuterBorderColor = ResolveColor(index, appearance.OuterBorderColor) })),
                SampleSettings.Slider("外枠の太さ", 2, 0.5, 8, 0.5, false, value => Apply(appearance with { OuterBorderThickness = value })),
                SampleSettings.Check("内側の塗りを表示", true, value => Apply(appearance with { ShowInnerFill = value })),
                ColorChoice("内側の塗りの色", 1, index => Apply(appearance with { InnerFillColor = ResolveColor(index, appearance.InnerFillColor) })),
                SampleSettings.Check("内枠を表示", true, value => Apply(appearance with { ShowInnerBorder = value })),
                ColorChoice("内枠の色", 5, index => Apply(appearance with { InnerBorderColor = ResolveColor(index, appearance.InnerBorderColor) })),
                SampleSettings.Slider("内枠の太さ", 1, 0.5, 8, 0.5, false, value => Apply(appearance with { InnerBorderThickness = value })),
                SampleSettings.Check("暗転マスクを表示", true, value => Apply(appearance with { ShowOverlay = value })),
                ColorChoice("暗転マスクの色", 5, index => Apply(appearance with { OverlayColor = ResolveColor(index, appearance.OverlayColor) })),
                SampleSettings.Check("マスクを全体に敷く", false, value => Apply(appearance with { ShowFullOverlay = value })),
                SampleSettings.Check("サイズ ラベルを表示", true, value => Apply(appearance with { ShowSizeLabel = value })),
                ColorChoice("サイズ ラベルの色", 1, index => Apply(appearance with { SizeLabelColor = ResolveColor(index, appearance.SizeLabelColor) })),
                ColorChoice("サイズ ラベルの背景色", 5, index => Apply(appearance with { SizeLabelBackgroundColor = ResolveColor(index, appearance.SizeLabelBackgroundColor) })),
                SampleSettings.Slider("サイズ ラベルの文字サイズ", 11, 6, 24, 1, true, value => Apply(appearance with { SizeLabelFontSize = value })),
                SampleSettings.Check("座標ラベルを表示", true, value => Apply(appearance with { ShowCoordLabel = value })),
                ColorChoice("座標ラベルの色", 1, index => Apply(appearance with { CoordLabelColor = ResolveColor(index, appearance.CoordLabelColor) })),
                ColorChoice("座標ラベルの背景色", 5, index => Apply(appearance with { CoordLabelBackgroundColor = ResolveColor(index, appearance.CoordLabelBackgroundColor) })),
                SampleSettings.Slider("座標ラベルの文字サイズ", 11, 6, 24, 1, true, value => Apply(appearance with { CoordLabelFontSize = value })),
                SampleSettings.Slider("座標ラベルの間隔", 4, 0, 24, 1, true, value => Apply(appearance with { CoordLabelOffset = value })),
                SampleSettings.Slider("ラベルの余白", 4, 0, 24, 1, true, value => Apply(appearance with { LabelPadding = value })),
            },
        };

        // ── サンプル設定（サンプル固有の操作。 並びは他 UI と共通） ──
        var samples = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = "ドラッグ: 選択 / 矢印キー: nudge / Enter: 確定 / Escape: キャンセル",
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap,
                },
                SampleSettings.Buttons(
                    ("選択をクリア", () => _marquee.ResetSelection()),
                    ("矩形を設定", () => _marquee.SetSelection(new EsRect(250, 180, 300, 200))),
                    ("ハイライト切替", ToggleHighlight),
                    ("確定を要求", () => _marquee.RequestConfirm()),
                    ("キャンセルを要求", () => _marquee.RequestCancel()),
                    ("テスト ログを出力（非同期）", () => logger.LogInformation("テスト ログ（非同期）"))),
                _status,
            },
        };

        var log = new SampleLogPanel(_logging.Buffer);
        Title = "MarqueeControl サンプル（WinUI 3）";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1400, 900));
        Content = SampleShell.CreateContent(properties, samples, surface, log);
        Closed += (_, _) => _logging.Dispose();

        return;

        // ハイライトの対象を切り替える
        void ToggleHighlight()
        {
            _marquee.HighlightSourceRect = _marquee.HighlightSourceRect is null ? new EsRect(120, 120, 400, 300) : null;
            Write(_marquee.HighlightSourceRect is null ? "ハイライト解除" : "ハイライト設定");
        }

        // 外観を更新してコントロールへ反映する
        void Apply(MarqueeAppearance value)
        {
            appearance = value;
            _marquee.Appearance = value;
            _marquee.InvalidateRender();
        }
    }

    /// <summary>矩形をログ用の文字列へ整形する。</summary>
    /// <param name="rect">対象座標の矩形。</param>
    /// <returns>整形した文字列。</returns>
    private static string FormatRect(EsRect rect)
        => string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"({rect.X:0.#}, {rect.Y:0.#}) {rect.Width:0.#}x{rect.Height:0.#}");

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
