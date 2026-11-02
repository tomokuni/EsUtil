using System.Runtime.InteropServices.WindowsRuntime;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Ui.WinUI3.Marquee.Sample.Imaging;
using EsUtil.Ui.WinUI3.Marquee.Sample.Presentation;
using EsUtil.Ui.WinUI3.SampleKit;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
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

    /// <summary>ズームの最小倍率。</summary>
    private const double MinZoom = 0.1;

    /// <summary>ズームの最大倍率。</summary>
    private const double MaxZoom = 5.0;

    /// <summary>ズームの刻み（スライダーの増減）。</summary>
    private const double ZoomStep = 0.1;

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

    /// <summary>ドラッグ選択コントロール。</summary>
    private readonly MarqueeControl _marquee = new();

    /// <summary>状態の表示。</summary>
    private readonly TextBlock _status = new() { Text = "選択なし", FontSize = 11, TextWrapping = TextWrapping.Wrap };

    /// <summary>現在の表示倍率（座標変換ポートが描画のたびに読む）。</summary>
    private double _zoom = 1.0;

    /// <summary>背景のテスト画像（4 UI 共通の画素）。 ウィンドウと同じ寿命で保持する。</summary>
    private readonly EsReadOnlyBitmap _backdrop;

    /// <summary>メイン ウィンドウを初期化する。</summary>
    public MainWindow()
    {
        var logger = _logging.Logger;
        logger.LogInformation("サンプルを起動しました。");
        _marquee.Log = logger;
        _marquee.LogPrefix = "[Marquee]";

        // 背景（選択対象）と選択オーバーレイを重ねる
        // 背景は 4 UI 共通のテスト画像を使う（同じ画素・同じ位置のマーカー・同じグリッド）
        _backdrop = TestImageFactory.Create(SourceWidth, SourceHeight);
        // 背景は対象座標の寸法のまま置き、拡大は描画の変換で行う（画素が画像と同じく拡大される）
        var background = new Image
        {
            Source = ToBitmap(_backdrop),
            Stretch = Stretch.Fill,
            IsHitTestVisible = false,
            Width = SourceWidth,
            Height = SourceHeight,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            RenderTransformOrigin = new Windows.Foundation.Point(0, 0),
        };

        var surface = new Grid
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x20, 0x20, 0x20)),
            MinHeight = 240,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        surface.Children.Add(background);
        surface.Children.Add(_marquee);

        // ズームで表示領域より大きくなったときにパンできるようスクロールできる入れ物へ置く
        var scroll = new ScrollViewer
        {
            Content = surface,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Auto,
            VerticalScrollMode = ScrollMode.Auto,
        };

        void Write(string line, LogLevel level = LogLevel.Debug)
        {
            // 文字列をそのまま記録する（テンプレート解析も引数の評価もしない）。
            // 設定の変更は Information、状態の変化は Debug、高頻度の値は Trace（ログ規約 §2）
            logger.Log(level, default, line, null, static (state, _) => state);
        }

        // ズームを反映する（背景の拡大率と、クライアント座標 → 対象座標の変換を揃える）
        void ApplyZoom(double value)
        {
            surface.Width = SourceWidth * value;
            surface.Height = SourceHeight * value;
            background.RenderTransform = new ScaleTransform { ScaleX = value, ScaleY = value };
            _marquee.ClientToSourceScaling = 1.0 / value;
            _marquee.InvalidateRender();
        }

        // 倍率は「対象座標 / ローカル DIP」。 ズームは背景を倍率ぶん大きく描くため、1 DIP が覆う対象座標は 1 / ズームになる
        _marquee.ClientToSourceScaling = 1.0 / _zoom;
        _marquee.SetCoordTransform(
            CoordTransform.ClientToSource(() => 1.0 / _zoom, () => new EsSize(SourceWidth, SourceHeight)));
        ApplyZoom(_zoom);

        // 選択の状態を画面へ出す（ログとは別に、常に見える形で残す）
        void UpdateStatus(EsRect rect)
            => _status.Text = string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"選択: x={rect.X:0.#}, y={rect.Y:0.#}, w={rect.Width:0.#}, h={rect.Height:0.#}");

        _marquee.SelectionStarted += (_, e) => Write($"開始: ({e.Point.X:0.#}, {e.Point.Y:0.#})");
        _marquee.SelectionChanged += (_, e) =>
        {
            // ドラッグ中は毎フレーム届くため Trace（既定の表示レベルでは出さない）
            Write($"選択中: {FormatRect(e.Rect)}", LogLevel.Trace);
            UpdateStatus(e.Rect);
        };
        _marquee.SelectionCompleted += (_, e) =>
        {
            Write($"確定可能: {FormatRect(e.Rect)}");
            UpdateStatus(e.Rect);
        };
        _marquee.SelectionCancelled += (_, _) => Write("キャンセル（選択が解除された）");
        _marquee.ConfirmRequested += (_, e) => Write($"確定要求: {FormatRect(e.Rect)}");
        _marquee.CancelRequested += (_, _) => Write("キャンセル要求");
        _marquee.LoupePositionMoved += (_, e) =>
        {
            // ポインター移動のたびに届く高頻度の値であるため Trace（画面の表示は状態の行が担う）
            Write($"ルーペ位置: ({e.Point.X:0.#}, {e.Point.Y:0.#})", LogLevel.Trace);
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
                    ApplyZoom(value);
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

        // ── サンプル設定（サンプル固有の操作。 並びは他 UI と共通） ──
        // ── メソッド操作（対象コントロールのメソッド実行） ──
        var methods = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                SampleSettings.Buttons(
                    ("選択をクリア", () => _marquee.ResetSelection()),
                    ("矩形を設定", () => _marquee.SetSelection(new EsRect(250, 180, 300, 200))),
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
                    ("ハイライト切替", ToggleHighlight),
                    ("テスト ログを出力（非同期）", () => logger.LogInformation("テスト ログ（非同期）"))),
                _status,
            },
        };

        var log = new SampleLogPanel(_logging.Buffer);
        Title = "MarqueeControl サンプル（WinUI 3）";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1400, 900));
        Content = SampleShell.CreateContent(properties, methods, samples, scroll, log);
        Closed += (_, _) =>
        {
            _backdrop.Dispose();
            _logging.Dispose();
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
            Write($"ハイライト（表示専用の注目）: {FormatRect(detected)} / 選択は {FormatRect(_marquee.SelectionSourceRect)} のまま（ハイライト中は操作できない）");
        }

        // 外観を更新してコントロールへ反映する
        void Apply(MarqueeAppearance value)
        {
            appearance = value;
            _marquee.Appearance = value;
            _marquee.InvalidateRender();
        }
    }

    /// <summary>コアの画素バッファを XAML の画像へ変換する。</summary>
    /// <param name="image">変換する画像。</param>
    /// <returns>変換した画像。</returns>
    /// <remarks>画素形式は <see cref="WriteableBitmap"/> が要求する BGRA8888 である（コアの内部表現と同じ）。</remarks>
    private static WriteableBitmap ToBitmap(EsReadOnlyBitmap image)
    {
        var bitmap = new WriteableBitmap(image.Width, image.Height);
        using (var target = bitmap.PixelBuffer.AsStream())
        {
            target.Write(image.ReadOnlyPixels);
        }

        bitmap.Invalidate();
        return bitmap;
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

    /// <summary>色の選択肢から色を解決する。</summary>
    /// <param name="index">選択されたインデックス。</param>
    /// <returns>解決した色。</returns>
    private static DrawingColor ResolveColor(int index) => s_colorChoices[index].Color;
}
