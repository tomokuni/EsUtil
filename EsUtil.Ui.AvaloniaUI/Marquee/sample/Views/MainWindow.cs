using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Ui.AvaloniaUI.Marquee.Sample.Imaging;
using EsUtil.Ui.AvaloniaUI.Marquee.Sample.Internal;
using EsUtil.Ui.AvaloniaUI.Marquee.Sample.Presentation;
using EsUtil.Ui.AvaloniaUI.SampleKit;
using Microsoft.Extensions.Logging;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.AvaloniaUI.Marquee.Sample.Views;

/// <summary>メイン ウィンドウ（設定パネル + 背景画像 + ドラッグ選択）。</summary>
/// <remarks>
/// <b>示すこと</b>: <c>MarqueeControl</c> の公開 API（<c>SetCoordTransform</c> / <c>Options</c> / <c>Appearance</c> /
/// <c>SetSelection</c> / <c>ResetSelection</c> / <c>RequestConfirm</c> / <c>RequestCancel</c> / <c>Log</c>）だけで
/// ドラッグ選択・ズーム表示・外観の変更・確定/キャンセルの流れを組める。<br/>
/// <b>配置</b>: サンプル共通の <see cref="SampleShell"/>（左: 設定 / 右上: 画像 / 右下: ログ）を使う。<br/>
/// <b>ログ</b>: Serilog の出力を非同期に表示する（<see cref="SampleLogging"/>）。
/// </remarks>
public sealed class MainWindow : Window
{
    /// <summary>背景画像の幅（ピクセル）。</summary>
    private const int ImageWidth = 800;

    /// <summary>背景画像の高さ（ピクセル）。</summary>
    private const int ImageHeight = 600;

    /// <summary>ズームの最小倍率。</summary>
    private const double MinZoom = 0.1;

    /// <summary>ズームの最大倍率。</summary>
    private const double MaxZoom = 5.0;

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

    /// <summary>背景画像。</summary>
    private readonly Image _image;

    /// <summary>ドラッグ選択コントロール。</summary>
    private readonly MarqueeControl _marquee;

    /// <summary>状態の表示。</summary>
    private readonly TextBlock _status = new() { Text = "選択なし", FontSize = 11, TextWrapping = TextWrapping.Wrap };

    /// <summary>ウィンドウを初期化する。</summary>
    public MainWindow()
    {
        Title = "MarqueeControl サンプル（AvaloniaUI）";
        Width = 1400;
        Height = 900;
        MinWidth = 900;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var logger = _logging.Logger;
        logger.LogInformation("サンプルを起動しました。");

        var appearance = MarqueeAppearance.Default;
        var zoom = 1.0;

        var backdrop = TestImageFactory.Create(ImageWidth, ImageHeight);
        _image = new Image
        {
            Source = backdrop.ToAvaloniaBitmap(),
            Width = ImageWidth,
            Height = ImageHeight,
            Stretch = Stretch.Fill,
        };

        _marquee = new MarqueeControl
        {
            Log = logger,
            LogPrefix = "[Marquee]",
            Options = MarqueeOptions.Default with { ClampToSourceBounds = true },
        };

        // 画像とコントロールを同じ領域へ重ねる
        // 左上へ寄せて指定した大きさを保つ（引き伸ばされると画像と座標がずれる）
        var surface = new Grid
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };
        surface.Children.Add(_image);
        surface.Children.Add(_marquee);

        var scroll = new ScrollViewer
        {
            Content = surface,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };

        void Write(string line)
        {
            // 文字列をそのまま記録する（テンプレート解析も引数の評価もしない）
            logger.Log(LogLevel.Information, default, line, null, static (state, _) => state);
        }

        void UpdateStatus(MarqueeSnapshot snapshot)
        {
            var rect = snapshot.SelectionSourceRect;
            _status.Text = snapshot.HasSelection
                ? string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"選択: x={rect.X:0.#}, y={rect.Y:0.#}, w={rect.Width:0.#}, h={rect.Height:0.#}")
                : "選択なし";
        }

        _marquee.SelectionStarted += (_, e) => Write($"開始: ({e.Point.X:0.#}, {e.Point.Y:0.#})");
        _marquee.SelectionChanged += (_, e) => UpdateStatus(e.Snapshot);
        _marquee.SelectionCompleted += (_, e) =>
        {
            Write($"確定可能: ({e.Rect.X:0.#}, {e.Rect.Y:0.#}) {e.Rect.Width:0.#}x{e.Rect.Height:0.#}");
            UpdateStatus(e.Snapshot);
        };
        _marquee.SelectionCancelled += (_, _) => Write("キャンセル（選択が解除された）");
        _marquee.ConfirmRequested += (_, e) => Write($"確定要求: ({e.Rect.X:0.#}, {e.Rect.Y:0.#})");
        _marquee.CancelRequested += (_, _) => Write("キャンセル要求");
        _marquee.LoupePositionMoved += (_, e) => _status.Text = $"追従位置: X={e.Point.X:0} Y={e.Point.Y:0}";
        _marquee.Rendered += (_, e) =>
        {
            var content = e.Content;
            var text = content.FocusDipRect is { } focus
                ? $"選択 {focus.Width:0} x {focus.Height:0}"
                : "ドラッグで範囲を選択してください";
            var origin = new Point(content.ClientRect.X + 12, content.ClientRect.Y + 12);
            e.Context.DrawText(
                new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    Typeface.Default, 14, Brushes.White),
                origin);
        };

        // 初期の選択（枠線・ハンドル・ラベルが見える状態）
        _marquee.SetSelection(new Rect(120, 80, 240, 180));

        // ── プロパティ設定（対象コントロールの公開プロパティ。 並びは他 UI と共通） ──
        var properties = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                SampleSettings.Slider("ズーム", zoom, MinZoom, MaxZoom, 0.1, false, value =>
                {
                    zoom = value;
                    _marquee.ClientToSourceScaling = value;
                    surface.Width = ImageWidth * value;
                    surface.Height = ImageHeight * value;
                    _marquee.SetCoordTransform(CoordTransform.ClientToSource(() => zoom, () => new EsSize(ImageWidth, ImageHeight)));
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
                ColorChoice("マーカーの色", 4, index => Apply(appearance with { MarkerColor = ResolveColor(index, appearance.MarkerColor) })),
                SampleSettings.Slider("マーカーの太さ", 2, 0.5, 8, 0.5, false, value => Apply(appearance with { MarkerThickness = value })),
                SampleSettings.Check("外枠を表示", true, value => Apply(appearance with { ShowOuterBorder = value })),
                ColorChoice("外枠の色", 2, index => Apply(appearance with { OuterBorderColor = ResolveColor(index, appearance.OuterBorderColor) })),
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
                    ("矩形を設定", () => _marquee.SetSelection(new Rect(250, 180, 300, 200))),
                    ("ハイライト切替", ToggleHighlight),
                    ("確定を要求", () => _marquee.RequestConfirm()),
                    ("キャンセルを要求", () => _marquee.RequestCancel()),
                    ("テスト ログを出力（非同期）", () => logger.LogInformation("テスト ログ（非同期）"))),
                _status,
            },
        };

        var log = new SampleLogPanel(_logging.Buffer);
        Content = SampleShell.CreateContent(properties, samples, scroll, log);
        Closed += (_, _) =>
        {
            _logging.Dispose();
            backdrop.Dispose();
        };

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
