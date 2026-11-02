using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Globalization;
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

    /// <summary>色の選択肢（表示の可否は各項目のチェックが持つため、色だけを選ぶ）。</summary>
    private static readonly (string Name, DrawingColor Color)[] s_colorChoices =
    [
        ("白", DrawingColor.White),
        ("アンバー", DrawingColor.FromArgb(255, 255, 196, 0)),
        ("赤", DrawingColor.FromArgb(255, 255, 0, 0)),
        ("緑", DrawingColor.FromArgb(255, 0, 200, 83)),
        ("薄灰", DrawingColor.FromArgb(255, 0xD3, 0xD3, 0xD3)),
        ("黒 50%", DrawingColor.FromArgb(128, 0, 0, 0)),
        ("黒 60%", DrawingColor.FromArgb(153, 0, 0, 0)),
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
        Title = "Marquee サンプル (AvaloniaUI)";
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
        // 大きさは親（surface）に合わせる（ズームで surface を広げると画像も同じ倍率で広がる）
        _image = new Image
        {
            Source = backdrop.ToAvaloniaBitmap(),
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

        void Write(string line, LogLevel level = LogLevel.Debug)
        {
            // 文字列をそのまま記録する（テンプレート解析も引数の評価もしない）。
            // 設定の変更は Information、状態の変化は Debug、高頻度の値は Trace（ログ規約 §2）
            logger.Log(level, default, line, null, static (state, _) => state);
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
        _marquee.SelectionChanged += (_, e) =>
        {
            // ドラッグ中は毎フレーム届くため Trace（既定の表示レベルでは出さない）
            Write($"選択中: ({e.Rect.X:0.#}, {e.Rect.Y:0.#}) {e.Rect.Width:0.#}x{e.Rect.Height:0.#}", LogLevel.Trace);
            UpdateStatus(e.Snapshot);
        };
        _marquee.SelectionCompleted += (_, e) =>
        {
            Write($"確定可能: ({e.Rect.X:0.#}, {e.Rect.Y:0.#}) {e.Rect.Width:0.#}x{e.Rect.Height:0.#}");
            UpdateStatus(e.Snapshot);
        };
        _marquee.SelectionCancelled += (_, _) => Write("キャンセル（選択が解除された）");
        _marquee.ConfirmRequested += (_, e) => Write($"確定要求: ({e.Rect.X:0.#}, {e.Rect.Y:0.#})");
        _marquee.CancelRequested += (_, _) => Write("キャンセル要求");
        _marquee.LoupePositionMoved += (_, e) =>
        {
            // ポインター移動のたびに届く高頻度の値であるため Trace（トレース有効時だけ記録する）
            Write($"ルーペ位置: ({e.Point.X:0.#}, {e.Point.Y:0.#})", LogLevel.Trace);
            _status.Text = $"追従位置: X={e.Point.X:0} Y={e.Point.Y:0}";
        };
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
                // ハイライトはコントロールのプロパティ（HighlightSourceRect）を切り替える操作である
                SampleSettings.Buttons(("ハイライト矩形を設定/解除", ToggleHighlight)),
                SampleSettings.Slider("ズーム", zoom, MinZoom, MaxZoom, 0.1, false, value =>
                {
                    zoom = value;
                    _marquee.ClientToSourceScaling = 1.0 / value;
                    surface.Width = ImageWidth * value;
                    surface.Height = ImageHeight * value;

                    // 倍率は「対象座標 / ローカル DIP」。 ズームは画像を倍率ぶん大きく描くため、1 DIP が覆う対象座標は 1 / ズームになる
                    _marquee.SetCoordTransform(CoordTransform.ClientToSource(() => 1.0 / zoom, () => new EsSize(ImageWidth, ImageHeight)));
                }),
                SampleSettings.Check("ハンドルを表示", true, value => Apply(appearance with { ShowHandles = value })),
                ColorChoice("ハンドルの色", 0, index => Apply(appearance with { HandleColor = ResolveColor(index) })),
                ColorChoice("ハンドル枠の色", 4, index => Apply(appearance with { HandleBorderColor = ResolveColor(index) })),
                SampleSettings.Slider("ハンドル サイズ", 10, 2, 30, 1, true, value => _marquee.Options = _marquee.Options with { HandleSize = value }),
                SampleSettings.Slider("ハンドル枠の太さ", 1.5, 0.5, 8, 0.5, false, value => _marquee.Options = _marquee.Options with { HandleBorderThickness = value }),
                SampleSettings.Slider("ヒット半径", 12, 2, 40, 1, true, value => _marquee.Options = _marquee.Options with { HandleHitRadius = value }),
                SampleSettings.Slider("最小選択サイズ", 6, 1, 40, 1, true, value => _marquee.Options = _marquee.Options with { MinSelectionSize = value }),
                SampleSettings.Check("画像内にクリップ", true, value => _marquee.Options = _marquee.Options with { ClampToSourceBounds = value }),
                SampleSettings.Check("枠線を表示", true, value => Apply(appearance with { ShowBorder = value })),
                ColorChoice("枠線の色", 4, index => Apply(appearance with { BorderColor = ResolveColor(index) })),
                SampleSettings.Slider("枠線の太さ", 2, 0.5, 8, 0.5, false, value => Apply(appearance with { BorderThickness = value })),
                SampleSettings.Check("内側マスクを表示", false, value => Apply(appearance with { ShowInnerMask = value })),
                ColorChoice("内側マスクの色", 0, index => Apply(appearance with { InnerMaskColor = ResolveColor(index) })),
                SampleSettings.Check("外側マスクを表示", true, value => Apply(appearance with { ShowOuterMask = value })),
                ColorChoice("外側マスクの色", 5, index => Apply(appearance with { OuterMaskColor = ResolveColor(index) })),
                SampleSettings.Check("マーカーを表示", true, value => Apply(appearance with { ShowMarker = value })),
                ColorChoice("マーカーの色", 0, index => Apply(appearance with { MarkerColor = ResolveColor(index) })),
                SampleSettings.Slider("マーカーの太さ", 2, 0.5, 8, 0.5, false, value => Apply(appearance with { MarkerThickness = value })),
                SampleSettings.Slider("マーカーの長さ", 16, 8, 40, 2, true, value => _marquee.Options = _marquee.Options with { MarkerLength = value }),
                SampleSettings.Check("サイズ ラベルを表示", true, value => Apply(appearance with { ShowSizeLabel = value })),
                ColorChoice("サイズ ラベルの色", 0, index => Apply(appearance with { SizeLabelColor = ResolveColor(index) })),
                ColorChoice("サイズ ラベルの背景色", 6, index => Apply(appearance with { SizeLabelBackgroundColor = ResolveColor(index) })),
                SampleSettings.Slider("サイズ ラベルの文字サイズ", 14, 6, 24, 1, true, value => Apply(appearance with { SizeLabelFontSize = value })),
                SampleSettings.Slider("サイズ ラベルの表示スケール", 0, 0, 2, 0.1, false, value => _marquee.DisplayScaleScaling = value),
                SampleSettings.Check("座標ラベルを表示", true, value => Apply(appearance with { ShowCoordLabel = value })),
                ColorChoice("座標ラベルの色", 0, index => Apply(appearance with { CoordLabelColor = ResolveColor(index) })),
                ColorChoice("座標ラベルの背景色", 6, index => Apply(appearance with { CoordLabelBackgroundColor = ResolveColor(index) })),
                SampleSettings.Slider("座標ラベルの文字サイズ", 12, 6, 24, 1, true, value => Apply(appearance with { CoordLabelFontSize = value })),
                SampleSettings.Slider("座標ラベルの間隔", 8, 0, 24, 1, true, value => Apply(appearance with { CoordLabelOffset = value })),
                SampleSettings.Slider("ラベルの余白", 4, 0, 24, 1, true, value => Apply(appearance with { LabelPadding = value })),
                SampleSettings.Text("ログ接頭辞", "[Marquee]", value => _marquee.LogPrefix = value),
                SampleSettings.Check("描画計画を差し替える（破線・ガイド線）", false, value =>
                    _marquee.DrawPlanResolver = value ? static plan => GuideMarqueePlan.Resolve(in plan) : null),
            },
        };

        // ── API操作（対象コントロールのメソッド実行） ──
        var methods = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                SampleSettings.Buttons(
                    ("選択をクリア", () => _marquee.ResetSelection()),
                    ("矩形を設定", () => _marquee.SetSelection(new Rect(250, 180, 300, 200))),
                    ("確定を要求", () => _marquee.RequestConfirm()),
                    ("キャンセルを要求", () => _marquee.RequestCancel())),
            },
        };

        // ── サンプル設定（サンプルアプリの外観と状態） ──
        var samples = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                SampleSettings.LogLevel(_logging),
                new TextBlock
                {
                    Text = "ドラッグ: 選択 / マウスを押したままの矢印キー: 位置の微調整 / Enter: 確定 / Escape: キャンセル",
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap,
                },
                SampleSettings.Buttons(
                    ("テスト ログを出力（非同期）", () => logger.LogInformation("テスト ログ（非同期）"))),
                _status,
            },
        };

        var log = new SampleLogPanel(_logging.Buffer);
        Content = SampleShell.CreateContent(properties, methods, samples, scroll, log);
        Closed += (_, _) =>
        {
            _logging.Dispose();
            backdrop.Dispose();
        };

        return;

        // 検出結果など、ホストが外から与える「表示専用の注目矩形」を模す。
        // 選択（SetSelection）とは別で、**ハンドルを出さず、確定・キャンセルの対象にもならない**（利用者に操作させたくない領域を示す）。
        // ハイライト中は**表示専用**になる（ハンドルを出さず、ドラッグもキー操作も受け付けない）。
        // これが「選択（SetSelection）との違い」である：選択は利用者が編集でき、ハイライトはホストが与える表示専用の注目である。
        void ToggleHighlight()
        {
            if (_marquee.HighlightSourceRect is not null)
            {
                _marquee.HighlightSourceRect = null;
                Write("ハイライト解除（注目矩形は選択へ戻る）");
                return;
            }

            var detected = new EsRect(420, 140, 300, 200);
            _marquee.HighlightSourceRect = detected;
            Write(string.Create(
                CultureInfo.InvariantCulture,
                $"ハイライト（表示専用の注目）: ({detected.X:0.#}, {detected.Y:0.#}) {detected.Width:0.#}x{detected.Height:0.#} / 選択は {_marquee.SelectionSourceRect.X:0.#}, {_marquee.SelectionSourceRect.Y:0.#} {_marquee.SelectionSourceRect.Width:0.#}x{_marquee.SelectionSourceRect.Height:0.#} のまま（ハイライト中は操作できない）"));
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

    /// <summary>色の選択肢から色を解決する。</summary>
    /// <param name="index">選択されたインデックス。</param>
    /// <returns>解決した色。</returns>
    private static DrawingColor ResolveColor(int index) => s_colorChoices[index].Color;
}
