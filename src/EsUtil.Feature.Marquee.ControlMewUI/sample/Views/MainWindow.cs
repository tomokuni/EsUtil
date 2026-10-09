using System.Globalization;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Feature.Marquee.MewUI.Sample.Imaging;
using EsUtil.Feature.Marquee.MewUI.Sample.Logging;
using EsUtil.Feature.Marquee.MewUI.Sample.Presentation;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Marquee.MewUI.Sample.Views;

/// <summary>メイン ウィンドウ（設定パネル + 背景画像 + ドラッグ選択）を組み立てる。</summary>
/// <remarks>
/// <b>示すこと</b>: <c>MarqueeControl</c> の公開 API（<c>SetCoordTransform</c> / <c>Options</c> / <c>Appearance</c> /<br/>
/// <c>SetSelection</c> / <c>ResetSelection</c> / <c>RequestConfirm</c> / <c>RequestCancel</c> / <c>Log</c>）だけで<br/>
/// ドラッグ選択・ズーム表示・外観の変更・確定/キャンセルの流れを組める。<br/>
/// <br/>
/// <b>座標系</b>: 選択矩形は対象座標（元画像ピクセル）で扱う。 表示のズームは座標変換ポート<br/>
/// （<c>CoordTransform.ClientToSource</c>）が担うため、コントロールはズームを知らない。<br/>
/// <br/>
/// <b>拡張例</b>: 見本のチェックを入れると、外観と描画計画を差し替えて外枠を破線・内側を三分割のガイド線にする<br/>
/// （提示層の派生は不要。 <see cref="GuideMarqueePlan"/>）。
/// </remarks>
internal static class MainWindow
{
    /// <summary>背景画像の幅（ピクセル）。</summary>
    private const int ImageWidth = 800;

    /// <summary>背景画像の高さ（ピクセル）。</summary>
    private const int ImageHeight = 600;

    /// <summary>設定パネルの幅（DIP）。</summary>
    private const double SettingsWidth = 400;

    /// <summary>スライダーの幅（DIP）。</summary>
    private const double SliderWidth = 150;

    /// <summary>ログ表示の高さ（DIP）。</summary>
    private const double LogHeight = 150;

    /// <summary>ズームの選択肢（倍率）。</summary>
    /// <summary>ズームの最小倍率（Avalonia 版サンプルと同じ）。</summary>
    private const double MinZoom = 0.1;

    /// <summary>ズームの最大倍率（Avalonia 版サンプルと同じ）。</summary>
    private const double MaxZoom = 5.0;

    /// <summary>ホイール 1 回あたりのズームの変化量。</summary>
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

    /// <summary>メイン ウィンドウを生成する。</summary>
    /// <returns>生成したウィンドウ（未表示）。</returns>
    internal static Window Create()
    {
        // ── 背景画像（選択対象） ──
        var backdrop = TestImageFactory.Create(ImageWidth, ImageHeight);
        var imageSource = ImageSource.FromBgraPixels(backdrop.Width, backdrop.Height, backdrop.Pixels, true);
        var image = new Image { Source = imageSource };

        // ── ログ表示（コントロールの Log と本サンプルの操作ログを同じ領域へ出す） ──
        var logLines = new List<string>();
        var logText = new TextBlock().FontSize(11);
        void WriteLog(string line)
        {
            logLines.Add(line);
            if (logLines.Count > 200)
            {
                logLines.RemoveAt(0);
            }

            logText.Text = string.Join(Environment.NewLine, logLines);
        }

        var logger = new InMemoryLogger(WriteLog);

        // ── 選択情報の表示 ──
        var selectionText = new TextBlock().FontSize(11).Text("選択なし");

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
        var surface = new Grid().Columns("*").Rows("*").Children(image, marquee);
        // ズームの現在値（ホイールとスライダーの両方から更新する）
        var zoomText = new TextBlock().Width(52).VerticalAlignment(VerticalAlignment.Center).Text(FormatZoom(zoom));

        // ズームのスライダー（ホイールと同じ範囲・刻み。 変更は ApplyZoom が倍率を確定する）
        var zoomSlider = new Slider
        {
            Minimum = MinZoom,
            Maximum = MaxZoom,
            Value = zoom,
            Width = SliderWidth,
            VerticalAlignment = VerticalAlignment.Center,
        };
        zoomSlider.SmallChange(ZoomStep);
        zoomSlider.ValueChanged += value =>
        {
            ApplyZoom(value);
            zoomText.Text = FormatZoom(zoom);
        };
        ApplyZoom(zoom);
        marquee.SetCoordTransform(CoordTransform.ClientToSource(() => zoom, () => new EsSize(ImageWidth, ImageHeight)));

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
        marquee.LoupePositionMoved += (_, e) => WriteLog($"ルーペ位置: ({e.Point.X:0.#}, {e.Point.Y:0.#})");
        marquee.ConfirmRequested += (_, e) => WriteLog($"確定要求: {FormatRect(e.Rect)}");
        marquee.CancelRequested += (_, _) => WriteLog("キャンセル要求");

        // 矢印キーの nudge はフォーカスを必要とするため、クリックでフォーカスを与える
        marquee.OnMouseDown(_ => marquee.Focus());

        // ホイールでズームする（Avalonia 版サンプルと同じ範囲・刻み。 表示倍率だけを変え、選択矩形は対象座標のまま）
        surface.OnMouseWheel(e =>
            zoomSlider.Value = Math.Clamp(zoom + (e.Delta.Y > 0 ? ZoomStep : -ZoomStep), MinZoom, MaxZoom));

        // ── 設定パネル ──
        var settings = new StackPanel().Vertical().Spacing(12).Children(
            Section(
                "表示",
                Row("ズーム", new StackPanel().Horizontal().Spacing(8).Children(zoomSlider, zoomText)),
                CheckRow(true, value => marquee.Options = marquee.Options with { ClampToSourceBounds = value }, "画像内にクリップ"),
                new StackPanel().Horizontal().Spacing(8).Children(
                    Button("選択をリセット", () => marquee.ResetSelection()),
                    Button("矩形を設定", () => marquee.SetSelection(new EsRect(250, 180, 300, 200)))),
                new StackPanel().Horizontal().Spacing(8).Children(
                    Button("確定を要求", () => marquee.RequestConfirm()),
                    Button("キャンセルを要求", () => marquee.RequestCancel()))),
            Section(
                "外観",
                ChoiceRow(s_colorChoices, 5, index =>
                {
                    appearance = appearance with
                    {
                        OuterBorderColor = ResolveColor(index, appearance.OuterBorderColor),
                        ShowOuterBorder = s_colorChoices[index].Color is not null,
                    };
                    marquee.Appearance = appearance;
                }),
                SliderRow("枠線の太さ", 2, 0.5, 8, 0.5, false, value =>
                {
                    appearance = appearance with { OuterBorderThickness = value };
                    marquee.Appearance = appearance;
                }),
                ChoiceRow(s_colorChoices, 1, index =>
                {
                    appearance = appearance with
                    {
                        HandleColor = ResolveColor(index, appearance.HandleColor),
                        ShowHandles = s_colorChoices[index].Color is not null,
                    };
                    marquee.Appearance = appearance;
                }),
                ChoiceRow(s_colorChoices, 1, index =>
                {
                    appearance = appearance with
                    {
                        MarkerColor = ResolveColor(index, appearance.MarkerColor),
                        ShowMarker = s_colorChoices[index].Color is not null,
                    };
                    marquee.Appearance = appearance;
                }),
                CheckRow(true, value =>
                {
                    appearance = appearance with { ShowOverlay = value };
                    marquee.Appearance = appearance;
                }, "暗転マスクを表示")),
            Section(
                "寸法・対話パラメータ",
                SliderRow("ハンドル サイズ", 10, 2, 30, 1, true, value => marquee.Options = marquee.Options with { HandleSize = value }),
                SliderRow("ハンドル枠の太さ", 1.5, 0.5, 8, 0.5, false, value => marquee.Options = marquee.Options with { HandleBorderThickness = value }),
                SliderRow("マーカーの長さ", 16, 8, 40, 2, true, value => marquee.Options = marquee.Options with { MarkerLength = value }),
                SliderRow("ヒット半径", 12, 2, 40, 1, true, value => marquee.Options = marquee.Options with { HandleHitRadius = value }),
                SliderRow("最小選択サイズ", 6, 1, 40, 1, true, value => marquee.Options = marquee.Options with { MinSelectionSize = value })),
            Section(
                "拡張例（見本）",
                CheckRow(false, value =>
                {
                    appearance = value ? GuideMarqueePlan.Appearance : MarqueeAppearance.Default;
                    marquee.Appearance = appearance;
                    marquee.DrawPlanResolver = value ? static plan => GuideMarqueePlan.Resolve(in plan) : null;
                }, "外枠を破線・内側を三分割のガイド線にする")),
            Section(
                "選択状態",
                selectionText),
            Section(
                "ログ",
                new Border { Child = new ScrollViewer().Content(logText), Height = LogHeight }));

        var settingsHost = new Border
        {
            Child = new ScrollViewer().Content(settings),
            Padding = new Thickness(12),
        };

        var root = new Grid().Columns($"{SettingsWidth},*").Rows("*");
        root.Add(settingsHost.Column(0));
        root.Add(new Border { Child = new ScrollViewer().Content(surface), Padding = new Thickness(12) }.Column(1));

        var window = new Window()
            .Title("MarqueeControl サンプル（MewUI）")
            .Resizable(1340, 860, minWidth: 1000, minHeight: 640);
        window.Content(root);

        window.Closed += () => backdrop.Dispose();

        return window;

        // ズームに合わせて表示領域（画像と選択の重なり）の大きさを変える
        void ApplyZoom(double value)
        {
            zoom = value;
            surface.Width = ImageWidth * value;
            surface.Height = ImageHeight * value;
        }
    }

    /// <summary>矩形をログ用の文字列へ整形する。</summary>
    /// <param name="rect">対象座標の矩形。</param>
    /// <returns>整形した文字列。</returns>
    private static string FormatRect(EsRect rect)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"({rect.X:0.#}, {rect.Y:0.#}) {rect.Width:0.#}x{rect.Height:0.#}");

    /// <summary>「なし」を除いた色の選択肢から色を解決する。</summary>
    /// <param name="index">選択されたインデックス。</param>
    /// <param name="fallback">「なし」が選ばれたときに残す色（描画しないため見た目には影響しない）。</param>
    /// <returns>解決した色。</returns>
    private static DrawingColor ResolveColor(int index, DrawingColor fallback)
        => s_colorChoices[index].Color ?? fallback;

    /// <summary>セクション（見出し + 項目行）を構築する。</summary>
    /// <param name="title">見出し。</param>
    /// <param name="rows">項目行。</param>
    /// <returns>見出しと項目行を縦に並べた要素。</returns>
    private static Element Section(string title, params Element[] rows)
    {
        var header = new Label().Text(title).Bold().FontSize(12);

        var body = new StackPanel().Vertical().Spacing(6);
        body.Children(rows);

        return new StackPanel().Vertical().Spacing(6).Children(header, body);
    }

    /// <summary>項目行（項目名 + 編集 UI）を構築する。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="control">編集 UI。</param>
    /// <returns>項目名と編集 UI を横に並べた要素。</returns>
    private static Element Row(string label, Element control)
    {
        var name = new Label().Text(label).Width(130).VerticalTextAlignment(TextAlignment.Center);

        return new StackPanel().Horizontal().Spacing(8).Children(name, control);
    }

    /// <summary>ボタンを構築する。</summary>
    /// <param name="text">ボタンの文言。</param>
    /// <param name="action">押下時の処理。</param>
    /// <returns>ボタン。</returns>
    private static FrameworkElement Button(string text, Action action)
    {
        var button = new Button().Content(text).OnClick(action);
        button.Cursor(CursorType.Hand);
        return button;
    }

    /// <summary>チェックボックスの行を構築する。</summary>
    /// <param name="initial">初期値。</param>
    /// <param name="apply">変更の反映先。</param>
    /// <param name="text">チェックボックスのラベル。</param>
    /// <returns>チェックボックス。</returns>
    private static FrameworkElement CheckRow(bool initial, Action<bool> apply, string text)
        => new CheckBox().IsChecked(initial).Content(text).OnCheckedChanged(apply);

    /// <summary>スライダーの行を構築する（現在値を数値で表示する）。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="initial">初期値。</param>
    /// <param name="minimum">最小値。</param>
    /// <param name="maximum">最大値。</param>
    /// <param name="step">増減の単位。</param>
    /// <param name="isInteger">整数のみを受け付けるかどうか。</param>
    /// <param name="apply">変更の反映先。</param>
    /// <returns>項目名・スライダー・現在値を並べた要素。</returns>
    private static Element SliderRow(string label, double initial, double minimum, double maximum, double step, bool isInteger, Action<double> apply)
    {
        var value = new TextBlock().Width(52).VerticalAlignment(VerticalAlignment.Center);
        value.Text = Format(initial, isInteger);

        var slider = new Slider
        {
            Minimum = minimum,
            Maximum = maximum,
            Value = initial,
            Width = SliderWidth,
            VerticalAlignment = VerticalAlignment.Center,
        };
        slider.SmallChange(step);
        slider.ValueChanged += changed =>
        {
            value.Text = Format(changed, isInteger);
            apply(changed);
        };

        return Row(label, new StackPanel().Horizontal().Spacing(8).Children(slider, value));
    }

    /// <summary>色などの選択肢の行を構築する。</summary>
    /// <param name="choices">選択肢（名前と色）。</param>
    /// <param name="selectedIndex">初期の選択インデックス。</param>
    /// <param name="apply">変更の反映先（選択インデックス）。</param>
    /// <returns>コンボボックス。</returns>
    private static Element ChoiceRow((string Name, DrawingColor? Color)[] choices, int selectedIndex, Action<int> apply)
        => ChoiceRow([.. choices.Select(static choice => choice.Name)], selectedIndex, apply);

    /// <summary>文字列の選択肢の行を構築する。</summary>
    /// <param name="choices">選択肢の表示名。</param>
    /// <param name="selectedIndex">初期の選択インデックス。</param>
    /// <param name="apply">変更の反映先（選択インデックス）。</param>
    /// <returns>コンボボックス。</returns>
    private static Element ChoiceRow(string[] choices, int selectedIndex, Action<int> apply)
    {
        var combo = new ComboBox().Items(choices);
        combo.SelectedIndex = selectedIndex;
        combo.OnSelectionChanged(_ => apply(combo.SelectedIndex));
        return combo;
    }

    /// <summary>ズームの倍率を書式化する。</summary>
    /// <param name="value">ズームの倍率。</param>
    /// <returns>表示用の文字列。</returns>
    private static string FormatZoom(double value) => value.ToString("0.0", CultureInfo.InvariantCulture) + "x";

    /// <summary>スライダーの現在値を書式化する。</summary>
    /// <param name="value">現在値。</param>
    /// <param name="isInteger">整数のみを受け付けるかどうか。</param>
    /// <returns>表示用の文字列。</returns>
    private static string Format(double value, bool isInteger)
        => isInteger
            ? ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.0", CultureInfo.InvariantCulture);
}
