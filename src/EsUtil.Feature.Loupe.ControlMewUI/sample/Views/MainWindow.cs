using System.Globalization;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Feature.Loupe.MewUI.Sample.Imaging;
using EsUtil.Feature.Loupe.MewUI.Sample.Logging;
using EsUtil.Feature.Loupe.MewUI.Sample.Presentation;
using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Loupe.MewUI.Sample.Views;

/// <summary>メイン ウィンドウ（設定パネル + テスト画像 + ルーペ）を組み立てる。</summary>
/// <remarks>
/// <b>示すこと</b>: <c>LoupeControl</c> の公開 API（<c>Source</c> / <c>CenterPoint</c> / <c>Options</c> /<br/>
/// <c>Appearance</c> / <c>ClientToPixelScaling</c> / <c>Log</c> / <c>DrawPlanResolver</c>）だけで<br/>
/// マウス追従の拡大表示・外観の変更・計画の差し替えができる。<br/>
/// <br/>
/// <b>配置</b>: ルーペの表示位置・表示サイズはコアが決めるため、ホスト（<see cref="LoupeHostPanel"/>）は<br/>
/// <c>ResolveLayout</c> の結果で子を配置するだけである。<br/>
/// <br/>
/// <b>拡張例</b>: 見本のルーペは外観と描画計画を差し替えて、十字線の代わりに格子線・枠線の代わりに四隅の鉤を描く<br/>
/// （提示層の派生は不要。 <see cref="GridLoupePlan"/>）。
/// </remarks>
internal static class MainWindow
{
    /// <summary>テスト画像の幅（ピクセル）。</summary>
    private const int ImageWidth = 800;

    /// <summary>テスト画像の高さ（ピクセル）。</summary>
    private const int ImageHeight = 600;

    /// <summary>設定パネルの幅（DIP）。</summary>
    private const double SettingsWidth = 380;

    /// <summary>スライダーの幅（DIP）。</summary>
    private const double SliderWidth = 150;

    /// <summary>数値入力の幅（DIP）。</summary>
    private const double NumberWidth = 110;

    /// <summary>ログの保持件数。</summary>
    private const int LogCapacity = 200;

    /// <summary>ログ表示の高さ（DIP）。</summary>
    private const double LogHeight = 180;

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

    /// <summary>メイン ウィンドウを生成する。</summary>
    /// <returns>生成したウィンドウ（未表示）。</returns>
    internal static Window Create()
    {
        // ── テスト画像 ──
        var source = TestImageFactory.Create(ImageWidth, ImageHeight);
        var imageSource = ImageSource.FromBgraPixels(source.Width, source.Height, source.ReadOnlyPixels, true);
        var image = new Image { Source = imageSource, Width = ImageWidth, Height = ImageHeight };

        // ── ログ表示（コントロールの Log へ設定する） ──
        var logLines = new List<string>();
        var logText = new TextBlock().FontSize(11);
        var logger = new InMemoryLogger(line =>
        {
            logLines.Add(line);
            if (logLines.Count > LogCapacity)
            {
                logLines.RemoveAt(0);
            }

            logText.Text = string.Join(Environment.NewLine, logLines);
        });

        // ── ルーペ（マウス追従） ──
        var appearance = LoupeAppearance.Default;
        var loupe = new LoupeControl
        {
            Source = source,
            PositionOffset = 24,
            Options = LoupeOptions.Default with { ZoomLevel = 4, RequestedSize = 240 },
            Appearance = appearance,
            Log = logger,
            LogPrefix = "[Loupe]",
            CenterPoint = new EsPoint(120, 120),
        };

        // ── 見本（外観と描画計画の差し替え） ──
        var previewSource = GridLoupePlan.CreateSource();
        var preview = new LoupeControl
        {
            Source = previewSource,
            PositionOffset = 0,
            Options = LoupeOptions.Default with { ZoomLevel = 6, RequestedSize = 150 },
            Appearance = GridLoupePlan.Appearance,
            CenterPoint = new EsPoint(70, 60),
            Log = logger,
            LogPrefix = "[Preview]",
            DrawPlanResolver = static plan => GridLoupePlan.Resolve(in plan),
        };

        // ── 画像とルーペを重ねるホスト ──
        var host = new LoupeHostPanel(image, new EsSize(ImageWidth, ImageHeight));
        host.AddLoupe(loupe);
        host.AddLoupe(preview);
        host.PointerMoved += (_, e) => loupe.CenterPoint = e.Position;

        // ── 選択矩形（ルーペのソースが元画像のどこにあるかを示す。 対象座標 = 元画像ピクセル） ──
        var selection = new SelectionState();

        void ApplySelection()
        {
            host.SetSelectionRect(selection.Rect, selection.IsVisible, selection.Label);
            // ルーペ内の破線も描き直す（プロパティに現れない入力のため InvalidateVisual で再描画を要求する）
            loupe.InvalidateVisual();
        }

        selection.Changed += (_, _) => ApplySelection();
        ApplySelection();

        // ルーペ画像上の破線は、描画完了直後に同一のキャンバスへ描く（Rendered の用途）
        loupe.Rendered += (_, e) =>
        {
            var content = e.Content;
            LoupeSelectionPainter.Paint(e.Canvas, in content, selection);
        };

        // ── 設定パネル ──
        var settings = new StackPanel().Vertical().Spacing(12).Children(
            Section(
                "表示",
                CheckRow(true, value => loupe.IsVisible = value, "ルーペを表示"),
                SliderRow("拡大率", 4, 1, 16, 1, true, value => loupe.Options = loupe.Options with { ZoomLevel = (int)value }),
                SliderRow("ルーペ サイズ", 240, 80, 480, 10, true, value => loupe.Options = loupe.Options with { RequestedSize = (int)value }),
                SliderRow("描画スケール", 1.0, 0.5, 2.0, 0.1, false, value => loupe.ClientToPixelScaling = value)),
            Section(
                "外観",
                ChoiceRow(s_colorChoices, 1, index =>
                {
                    appearance = appearance with
                    {
                        CrossColor = ResolveColor(index, appearance.CrossColor),
                        ShowCrosshair = s_colorChoices[index].Color is not null,
                    };
                    loupe.Appearance = appearance;
                }),
                ChoiceRow(s_colorChoices, 6, index =>
                {
                    appearance = appearance with
                    {
                        PasteboardColor = ResolveColor(index, appearance.PasteboardColor),
                        ShowPasteboard = s_colorChoices[index].Color is not null,
                    };
                    loupe.Appearance = appearance;
                }),
                ChoiceRow(s_colorChoices, 5, index =>
                {
                    appearance = appearance with
                    {
                        BorderColor = ResolveColor(index, appearance.BorderColor),
                        ShowBorder = s_colorChoices[index].Color is not null,
                    };
                    loupe.Appearance = appearance;
                }),
                SliderRow("枠線の太さ", 1, 0, 8, 1, true, value =>
                {
                    appearance = appearance with { BorderThickness = (int)value };
                    loupe.Appearance = appearance;
                }),
                CheckRow(true, value =>
                {
                    appearance = appearance with { ShowCoordLabel = value };
                    loupe.Appearance = appearance;
                }, "座標ラベルを表示")),
            Section(
                "操作",
                new StackPanel().Horizontal().Spacing(8).Children(
                    Button("再描画を要求", () => loupe.InvalidateVisual()),
                    Button("非表示にして解放", () =>
                    {
                        // Avalonia 版の Clear() と同じ結果（非表示にして描画リソースを解放する）
                        loupe.IsVisible = false;
                        loupe.Source = null;
                    }),
                    Button("ソースを再設定", () => loupe.Source = source))),
            Section(
                "選択矩形（破線オーバーレイ）",
                CheckRow(true, value => selection.IsVisible = value, "選択矩形を表示"),
                NumberRow("X", selection.X, 0, ImageWidth, 10, true, value => selection.X = value),
                NumberRow("Y", selection.Y, 0, ImageHeight, 10, true, value => selection.Y = value),
                NumberRow("W", selection.Width, 0, ImageWidth, 10, true, value => selection.Width = value),
                NumberRow("H", selection.Height, 0, ImageHeight, 10, true, value => selection.Height = value)),
            Section(
                "拡張例（見本）",
                CheckRow(true, value => preview.IsVisible = value, "格子線と四隅の鉤を描く見本を表示")),
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
        root.Add(new Border { Child = host, Padding = new Thickness(12) }.Column(1));

        var window = new Window()
            .Title("LoupeControl サンプル（MewUI）")
            .Resizable(1340, 820, minWidth: 1000, minHeight: 640);
        window.Content(root);

        window.Closed += () =>
        {
            previewSource.Dispose();
            source.Dispose();
        };

        return window;
    }

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
        var name = new Label().Text(label).Width(110).VerticalTextAlignment(TextAlignment.Center);

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

    /// <summary>数値入力の行を構築する（Avalonia 版サンプルの <c>NumericUpDown</c> に相当する）。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="initial">初期値。</param>
    /// <param name="minimum">最小値。</param>
    /// <param name="maximum">最大値。</param>
    /// <param name="step">増減の単位。</param>
    /// <param name="isInteger">整数のみを受け付けるかどうか。</param>
    /// <param name="apply">変更の反映先。</param>
    /// <returns>項目名と数値入力を横に並べた要素。</returns>
    private static Element NumberRow(string label, double initial, double minimum, double maximum, double step, bool isInteger, Action<double> apply)
    {
        var input = new NumericUpDown()
            .Minimum(minimum)
            .Maximum(maximum)
            .Step(step)
            .IsInteger(isInteger)
            .Width(NumberWidth);
        input.Value = initial;
        input.OnValueChanged(apply);
        return Row(label, input);
    }

    /// <summary>色などの選択肢の行を構築する。</summary>
    /// <param name="choices">選択肢（名前と色）。</param>
    /// <param name="selectedIndex">初期の選択インデックス。</param>
    /// <param name="apply">変更の反映先（選択インデックス）。</param>
    /// <returns>コンボボックス。</returns>
    private static Element ChoiceRow((string Name, DrawingColor? Color)[] choices, int selectedIndex, Action<int> apply)
    {
        var combo = new ComboBox().Items([.. choices.Select(static choice => choice.Name)]);
        combo.SelectedIndex = selectedIndex;
        combo.OnSelectionChanged(_ => apply(combo.SelectedIndex));
        return combo;
    }

    /// <summary>スライダーの現在値を書式化する。</summary>
    /// <param name="value">現在値。</param>
    /// <param name="isInteger">整数のみを受け付けるかどうか。</param>
    /// <returns>表示用の文字列。</returns>
    private static string Format(double value, bool isInteger)
        => isInteger
            ? ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.0", CultureInfo.InvariantCulture);
}
