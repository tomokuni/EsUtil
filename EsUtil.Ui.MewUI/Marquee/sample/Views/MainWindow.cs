using System.Globalization;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Ui.MewUI.Marquee.Sample.Imaging;
using EsUtil.Ui.MewUI.Marquee.Sample.Presentation;
using EsUtil.Ui.MewUI.SampleKit;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.MewUI.Marquee.Sample.Views;

/// <summary>メイン ウィンドウ（設定パネル + 背景画像 + ドラッグ選択）を組み立てる。</summary>
/// <remarks>
/// <b>示すこと</b>: <c>MarqueeControl</c> の公開 API（<c>SetCoordTransform</c> / <c>Options</c> / <c>Appearance</c> /
/// <c>SetSelection</c> / <c>ResetSelection</c> / <c>RequestConfirm</c> / <c>RequestCancel</c> / <c>Log</c>）だけで
/// ドラッグ選択・ズーム表示・外観の変更・確定/キャンセルの流れを組める。<br/>
/// <br/>
/// <b>座標系</b>: 選択矩形は対象座標（元画像ピクセル）で扱う。 表示のズームは座標変換ポート
/// （<c>CoordTransform.ClientToSource</c>）が担うため、コントロールはズームを知らない。<br/>
/// <br/>
/// <b>配置</b>: サンプル共通の <see cref="SampleShell"/>（左: 設定 / 右上: 画像 / 右下: ログ）を使う。<br/>
/// <b>ログ</b>: Serilog の出力を非同期に表示する（<see cref="SampleLogging"/>）。<br/>
/// <b>項目の並び</b>: 同じ機能の他 UI のサンプルと同じ順に並べる。
/// </remarks>
internal static class MainWindow
{
    /// <summary>背景画像の幅（ピクセル）。</summary>
    private const int ImageWidth = 800;

    /// <summary>背景画像の高さ（ピクセル）。</summary>
    private const int ImageHeight = 600;

    /// <summary>ズームの最小倍率（Avalonia 版サンプルと同じ）。</summary>
    private const double MinZoom = 0.1;

    /// <summary>ズームの最大倍率（Avalonia 版サンプルと同じ）。</summary>
    private const double MaxZoom = 5.0;

    /// <summary>ホイール 1 回あたりのズームの変化量。</summary>
    private const double ZoomStep = 0.1;

    /// <summary>通常ホイール 1 行あたりのパン量（DIP）。</summary>
    private const double WheelScrollSensitivity = 48.0;

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

    /// <summary>メイン ウィンドウを生成する。</summary>
    /// <returns>生成したウィンドウ（未表示）。</returns>
    internal static Window Create()
    {
        // ── 背景画像（選択対象） ──
        var backdrop = TestImageFactory.Create(ImageWidth, ImageHeight);
        var imageSource = ImageSource.FromBgraPixels(backdrop.Width, backdrop.Height, backdrop.ReadOnlyPixels, true);
        var image = new Image { Source = imageSource };

        // ── ログ（Serilog。 非同期に表示する） ──
        var logging = new SampleLogging();
        var log = new SampleLogPanel(logging.Buffer);
        var logger = logging.Logger;
        logger.LogInformation("サンプルを起動しました。");

        void WriteLog(string line, LogLevel level = LogLevel.Debug)
        {
            // 文字列をそのまま記録する（テンプレート解析も引数の評価もしない）。
            // 設定の変更は Information、状態の変化は Debug、高頻度の値は Trace（ログ規約 §2）
            logger.Log(level, default, line, null, static (state, _) => state);
        }

        // ── 選択情報の表示 ──
        var selectionText = new TextBlock().FontSize(11).Text("選択なし");
        var loupeText = new TextBlock().FontSize(11).Text("追従位置: -");

        // ── ドラッグ選択コントロール ──
        var zoom = 1.0;
        var appearance = MarqueeAppearance.Default;
        var marquee = new MarqueeControl
        {
            Options = MarqueeOptions.Default,
            Appearance = appearance,
            Log = logger,
            LogPrefix = "[Marquee]",
        };

        // 画像とコントロールを同じ領域へ重ねる（コントロールが選択矩形と装飾を描く）
        // 左上へ寄せて指定した大きさを保つ（引き伸ばされると画像と座標がずれる）
        var surface = new Grid().Columns("*").Rows("*").Children(image, marquee);
        surface.HorizontalAlignment(HorizontalAlignment.Left);
        surface.VerticalAlignment(VerticalAlignment.Top);

        // 表示領域より大きくなったとき（ズーム時）にパンできるようスクロールビューへ入れる
        var scroll = new ScrollViewer().Content(surface);
        scroll.HorizontalScroll = ScrollMode.Auto;
        scroll.VerticalScroll = ScrollMode.Auto;
        scroll.OnScrollChanged(() =>
            WriteLog($"スクロール: X={scroll.HorizontalOffset:0}, Y={scroll.VerticalOffset:0}", LogLevel.Trace));

        var zoomText = new TextBlock().Width(52).VerticalAlignment(VerticalAlignment.Center).Text(FormatZoom(zoom));

        // 倍率は「対象座標 / ローカル DIP」。 ズームは画像を倍率ぶん大きく描くため、1 DIP が覆う対象座標は 1 / ズームになる
        marquee.ClientToSourceScaling = 1.0 / zoom;
        marquee.SetCoordTransform(
            CoordTransform.ClientToSource(() => 1.0 / zoom, () => new EsSize(ImageWidth, ImageHeight)));

        // 起動直後に枠線・ハンドル・ラベルが見えるように、選択を 1 つ与えておく
        marquee.SetSelection(new EsRect(120, 80, 240, 180));

        void UpdateSelection(MarqueeSnapshot snapshot)
        {
            var rect = snapshot.SelectionSourceRect;
            selectionText.Text = snapshot.HasSelection
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"選択: x={rect.X:0.#}, y={rect.Y:0.#}, w={rect.Width:0.#}, h={rect.Height:0.#}")
                : "選択なし";
        }

        marquee.SelectionStarted += (_, e) => WriteLog($"開始: ({e.Point.X:0.#}, {e.Point.Y:0.#})");
        marquee.SelectionChanged += (_, e) => UpdateSelection(e.Snapshot);
        marquee.SelectionCompleted += (_, e) =>
        {
            WriteLog($"確定可能: {FormatRect(e.Rect)}");
            UpdateSelection(e.Snapshot);
        };
        marquee.SelectionCancelled += (_, _) => WriteLog("キャンセル（選択が解除された）");
        // ポインター移動のたびに届く高頻度の値であるため Trace（画面の表示は状態の行が担う）
        marquee.LoupePositionMoved += (_, e) =>
        {
            WriteLog($"ルーペ位置: ({e.Point.X:0.#}, {e.Point.Y:0.#})", LogLevel.Trace);
            loupeText.Text = $"追従位置: X={e.Point.X:0} Y={e.Point.Y:0}";
        };
        marquee.ConfirmRequested += (_, e) => WriteLog($"確定要求: {FormatRect(e.Rect)}");
        marquee.CancelRequested += (_, _) => WriteLog("キャンセル要求");

        // 選択サイズと操作ガイドはアプリ固有の情報のため、コントロールの描画完了後にホストが描く（他 UI と同じ文言・位置）
        marquee.Rendered += (_, e) =>
        {
            var content = e.Content;
            var text = content.FocusDipRect is { } focus
                ? $"選択 {focus.Width:0} x {focus.Height:0}"
                : "ドラッグで範囲を選択してください";
            e.DrawText(text, content.ClientRect.X + 12, content.ClientRect.Y + 12, 14, 0xFFFFFFFF);
        };

        // 矢印キーの nudge はフォーカスを必要とするため、クリックでフォーカスを与える
        marquee.OnMouseDown(_ => marquee.Focus());

        // Ctrl+ホイールでズーム（カーソル位置を固定）、通常ホイールで表示領域のパン
        marquee.OnMouseWheel(e =>
        {
            if (!e.ControlKey)
            {
                scroll.ScrollBy(-e.Delta.Y * WheelScrollSensitivity);
                e.Handled = true;
                return;
            }

            var anchor = e.GetPosition(surface);
            var oldZoom = zoom;
            ApplyZoom(Math.Clamp(zoom + (e.Delta.Y > 0 ? ZoomStep : -ZoomStep), MinZoom, MaxZoom));

            if (!zoom.Equals(oldZoom))
            {
                var ratio = zoom / oldZoom;
                scroll.SetScrollOffsets(
                    (anchor.X * ratio) - anchor.X + scroll.HorizontalOffset,
                    (anchor.Y * ratio) - anchor.Y + scroll.VerticalOffset);
            }

            e.Handled = true;
        });

        // ── プロパティ設定（対象コントロールの公開プロパティ。 並びは他 UI と共通） ──
        var properties = new StackPanel().Vertical().Spacing(6).Children(
            // ハイライトはコントロールのプロパティ（HighlightSourceRect）を切り替える操作である
            SampleSettings.Buttons(("ハイライト矩形を設定/解除", ToggleHighlight)),
            SampleSettings.Slider("ズーム", zoom, MinZoom, MaxZoom, ZoomStep, false, value => ApplyZoom(value)),
            SampleSettings.Check("ハンドルを表示", true, value => Apply(appearance with { ShowHandles = value })),
            ColorChoice("ハンドルの色", 0, index => Apply(appearance with { HandleColor = ResolveColor(index) })),
            ColorChoice("ハンドル枠の色", 4, index => Apply(appearance with { HandleBorderColor = ResolveColor(index) })),
            SampleSettings.Slider("ハンドル サイズ", 10, 2, 30, 1, true, value => marquee.Options = marquee.Options with { HandleSize = value }),
            SampleSettings.Slider("ハンドル枠の太さ", 1.5, 0.5, 8, 0.5, false, value => marquee.Options = marquee.Options with { HandleBorderThickness = value }),
            SampleSettings.Slider("ヒット半径", 12, 2, 40, 1, true, value => marquee.Options = marquee.Options with { HandleHitRadius = value }),
            SampleSettings.Slider("最小選択サイズ", 6, 1, 40, 1, true, value => marquee.Options = marquee.Options with { MinSelectionSize = value }),
            SampleSettings.Check("画像内にクリップ", true, value => marquee.Options = marquee.Options with { ClampToSourceBounds = value }),
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
            SampleSettings.Slider("マーカーの長さ", 16, 8, 40, 2, true, value => marquee.Options = marquee.Options with { MarkerLength = value }),
            SampleSettings.Check("サイズ ラベルを表示", true, value => Apply(appearance with { ShowSizeLabel = value })),
            ColorChoice("サイズ ラベルの色", 0, index => Apply(appearance with { SizeLabelColor = ResolveColor(index) })),
            ColorChoice("サイズ ラベルの背景色", 6, index => Apply(appearance with { SizeLabelBackgroundColor = ResolveColor(index) })),
            SampleSettings.Slider("サイズ ラベルの文字サイズ", 14, 6, 24, 1, true, value => Apply(appearance with { SizeLabelFontSize = value })),
            SampleSettings.Slider("サイズ ラベルの表示スケール", 0, 0, 2, 0.1, false, value => marquee.DisplayScaleScaling = value),
            SampleSettings.Check("座標ラベルを表示", true, value => Apply(appearance with { ShowCoordLabel = value })),
            ColorChoice("座標ラベルの色", 0, index => Apply(appearance with { CoordLabelColor = ResolveColor(index) })),
            ColorChoice("座標ラベルの背景色", 6, index => Apply(appearance with { CoordLabelBackgroundColor = ResolveColor(index) })),
            SampleSettings.Slider("座標ラベルの文字サイズ", 12, 6, 24, 1, true, value => Apply(appearance with { CoordLabelFontSize = value })),
            SampleSettings.Slider("座標ラベルの間隔", 8, 0, 24, 1, true, value => Apply(appearance with { CoordLabelOffset = value })),
            SampleSettings.Slider("ラベルの余白", 4, 0, 24, 1, true, value => Apply(appearance with { LabelPadding = value })),
            SampleSettings.Text("ログ接頭辞", "[Marquee]", value => marquee.LogPrefix = value),
            SampleSettings.Check("描画計画を差し替える（破線・ガイド線）", false, value =>
                marquee.DrawPlanResolver = value ? static plan => GuideMarqueePlan.Resolve(in plan) : null));

        // ── API操作（対象コントロールのメソッド実行） ──
        var methods = new StackPanel().Vertical().Spacing(6).Children(
            SampleSettings.Buttons(
                ("選択をクリア", () => marquee.ResetSelection()),
                ("矩形を設定", () => marquee.SetSelection(new EsRect(250, 180, 300, 200))),
                ("確定を要求", () => marquee.RequestConfirm()),
                ("キャンセルを要求", () => marquee.RequestCancel())));

        // ── サンプル設定（サンプルアプリの外観と状態） ──
        var samples = new StackPanel().Vertical().Spacing(6).Children(
            SampleSettings.LogLevel(logging),
            new TextBlock
            {
                Text = "ドラッグ: 選択 / マウスを押したままの矢印キー: 位置の微調整 / Enter: 確定 / Escape: キャンセル",
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
            },
            SampleSettings.Buttons(
                ("テスト ログを出力（非同期）", () => logger.LogInformation("テスト ログ（非同期）"))),
            selectionText,
            loupeText);

        var window = SampleShell.Create("Marquee サンプル (MewUI)", properties, methods, samples, scroll, log);
        window.Closed += () =>
        {
            backdrop.Dispose();
            logging.Dispose();
        };

        return window;

        // ズームに合わせて表示領域（画像と選択の重なり）の大きさを変える
        void ApplyZoom(double value)
        {
            zoom = value;
            zoomText.Text = FormatZoom(value);
            marquee.ClientToSourceScaling = 1.0 / value;
            surface.Width = ImageWidth * value;
            surface.Height = ImageHeight * value;
        }

        // 外観を更新してコントロールへ反映する（保持値と反映先を 1 か所にまとめる）
        void Apply(MarqueeAppearance value)
        {
            appearance = value;
            marquee.Appearance = value;
        }

        // 検出結果など、ホストが外から与える「表示専用の注目矩形」を模す。
        // 選択（SetSelection）とは別で、**ハンドルを出さず、確定・キャンセルの対象にもならない**（利用者に操作させたくない領域を示す）。
        // ハイライト中は**表示専用**になる（ハンドルを出さず、ドラッグもキー操作も受け付けない）。
        // これが「選択（SetSelection）との違い」である：選択は利用者が編集でき、ハイライトはホストが与える表示専用の注目である。
        void ToggleHighlight()
        {
            if (marquee.HighlightSourceRect is not null)
            {
                marquee.HighlightSourceRect = null;
                WriteLog("ハイライト解除（注目矩形は選択へ戻る）");
                return;
            }

            var detected = new EsRect(420, 140, 300, 200);
            marquee.HighlightSourceRect = detected;
            WriteLog($"ハイライト（表示専用の注目）: {FormatRect(detected)} / 選択は {FormatRect(marquee.SelectionSourceRect)} のまま（ハイライト中は操作できない）");
        }
    }

    /// <summary>矩形をログ用の文字列へ整形する。</summary>
    /// <param name="rect">対象座標の矩形。</param>
    /// <returns>整形した文字列。</returns>
    private static string FormatRect(EsRect rect)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"({rect.X:0.#}, {rect.Y:0.#}) {rect.Width:0.#}x{rect.Height:0.#}");

    /// <summary>色の選択肢の行を作る。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="selectedIndex">初期の選択インデックス。</param>
    /// <param name="apply">変更の反映先（選択インデックス）。</param>
    /// <returns>項目名とコンボ ボックスを並べた行。</returns>
    private static FrameworkElement ColorChoice(string label, int selectedIndex, Action<int> apply)
        => SampleSettings.Choice(label, [.. s_colorChoices.Select(static choice => choice.Name)], selectedIndex, apply);

    /// <summary>色の選択肢から色を解決する。</summary>
    /// <param name="index">選択されたインデックス。</param>
    /// <returns>解決した色。</returns>
    private static DrawingColor ResolveColor(int index) => s_colorChoices[index].Color;

    /// <summary>ズームの倍率を書式化する。</summary>
    /// <param name="value">ズームの倍率。</param>
    /// <returns>表示用の文字列。</returns>
    private static string FormatZoom(double value) => value.ToString("0.0", CultureInfo.InvariantCulture) + "x";
}
