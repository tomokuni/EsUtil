using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Ui.MewUI.Loupe.Sample.Imaging;
using EsUtil.Ui.MewUI.Loupe.Sample.Presentation;
using EsUtil.Ui.MewUI.SampleKit;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.MewUI.Loupe.Sample.Views;

/// <summary>メイン ウィンドウ（設定パネル + テスト画像 + ルーペ）。</summary>
/// <remarks>
/// <b>示すこと</b>: <c>LoupeControl</c> の公開 API（<c>Source</c> / <c>CenterPoint</c> / <c>Options</c> /
/// <c>Appearance</c> / <c>ClientToPixelScaling</c> / <c>PositionOffset</c> / <c>Log</c> / <c>LogPrefix</c> /
/// <c>DrawPlanResolver</c>）だけで、マウス追従の拡大表示・外観の変更・計画の差し替えができる。<br/>
/// <br/>
/// <b>配置</b>: サンプル共通の <see cref="SampleShell"/>（左: 設定 / 右上: 画像 / 右下: ログ）を使う。<br/>
/// <b>ログ</b>: Serilog の出力を非同期に表示する（<see cref="SampleLogging"/>）。<br/>
/// <b>項目の並び</b>: 同じ機能の他 UI のサンプルと同じ順に並べる。
/// </remarks>
internal static class MainWindow
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
        ("黒", DrawingColor.Black),
    ];

    /// <summary>ウィンドウを組み立てる。</summary>
    /// <returns>生成したウィンドウ。</returns>
    public static Window Create()
    {
        // ── テスト画像 ──
        var source = TestImageFactory.Create(ImageWidth, ImageHeight);
        var imageSource = ImageSource.FromBgraPixels(source.Width, source.Height, source.ReadOnlyPixels, true);
        var image = new Image { Source = imageSource, Width = ImageWidth, Height = ImageHeight };

        // ── ログ（Serilog。 非同期に表示する） ──
        var logging = new SampleLogging();
        var log = new SampleLogPanel(logging.Buffer);
        var logger = logging.Logger;
        logger.LogInformation("サンプルを起動しました。");

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
            // 接頭辞はプロセス内の全インスタンスで共有される（設定パネルの「ログ接頭辞」が全ルーペへ効く）
            DrawPlanResolver = static plan => GridLoupePlan.Resolve(in plan),
        };

        // ── 画像とルーペを重ねるホスト ──
        var host = new LoupeHostPanel(image, new EsSize(ImageWidth, ImageHeight));
        host.AddLoupe(loupe);
        host.AddLoupe(preview);
        host.PointerMoved += (_, e) => loupe.CenterPoint = e.Position;

        // ── 選択矩形（サンプル固有。 ルーペのソースが元画像のどこにあるかを示す） ──
        var selection = new SelectionState();

        void ApplySelection()
        {
            host.SetSelectionRect(selection.Rect, selection.IsVisible, selection.Label);
            loupe.InvalidateVisual();
        }

        selection.Changed += (_, _) => ApplySelection();
        ApplySelection();

        loupe.Rendered += (_, e) =>
        {
            var content = e.Content;
            LoupeSelectionPainter.Paint(e.Context, in content, selection);
        };

        // ── プロパティ設定（対象コントロールの公開プロパティ。 並びは他 UI と共通） ──
        var loupeVisible = new CheckBox().IsChecked(true).Content("ルーペを表示");
        loupeVisible.OnCheckedChanged(value => loupe.IsVisible = value);

        var properties = new StackPanel().Vertical().Spacing(6).Children(
            loupeVisible,
            SampleSettings.Slider("拡大率", 4, 1, 16, 1, true, value => loupe.Options = loupe.Options with { ZoomLevel = (int)value }),
            SampleSettings.Slider("ルーペ サイズ", 240, 80, 480, 10, true, value => loupe.Options = loupe.Options with { RequestedSize = (int)value }),
            SampleSettings.Slider("描画スケール", 1.0, 0.5, 2.0, 0.1, false, value => loupe.ClientToPixelScaling = value),
            SampleSettings.Slider("位置オフセット", 24, 0, 64, 4, true, value => loupe.PositionOffset = value),
            SampleSettings.Slider("中心 X", 120, 0, ImageWidth, 10, true, value => loupe.CenterPoint = new EsPoint(value, loupe.CenterPoint?.Y ?? 0)),
            SampleSettings.Slider("中心 Y", 120, 0, ImageHeight, 10, true, value => loupe.CenterPoint = new EsPoint(loupe.CenterPoint?.X ?? 0, value)),
            SampleSettings.Buttons(
                ("ソースを設定", () => loupe.Source = source),
                ("ソースを解除（描画資源を解放）", () =>
                {
                    loupe.IsVisible = false;
                    loupe.Source = null;
                    loupeVisible.IsChecked = false;
                }
        )),
            SampleSettings.Text("ログ接頭辞", "[Loupe]", value => loupe.LogPrefix = value),
            SampleSettings.Check("見本の描画計画（格子線・四隅の鉤）", false, value =>
                loupe.DrawPlanResolver = value ? static plan => GridLoupePlan.Resolve(in plan) : null),
            ColorChoice("十字線の色", 1, index =>
            {
                appearance = appearance with
                {
                    CrossColor = ResolveColor(index, appearance.CrossColor),
                    ShowCrosshair = s_colorChoices[index].Color is not null,
                };
                loupe.Appearance = appearance;
            }),
            ColorChoice("画像外の色", 6, index =>
            {
                appearance = appearance with
                {
                    PasteboardColor = ResolveColor(index, appearance.PasteboardColor),
                    ShowPasteboard = s_colorChoices[index].Color is not null,
                };
                loupe.Appearance = appearance;
            }),
            ColorChoice("枠線の色", 5, index =>
            {
                appearance = appearance with
                {
                    BorderColor = ResolveColor(index, appearance.BorderColor),
                    ShowBorder = s_colorChoices[index].Color is not null,
                };
                loupe.Appearance = appearance;
            }),
            SampleSettings.Slider("枠線の太さ", 1, 0, 8, 1, true, value =>
            {
                appearance = appearance with { BorderThickness = (int)value };
                loupe.Appearance = appearance;
            }),
            SampleSettings.Check("座標ラベルを表示", true, value =>
            {
                appearance = appearance with { ShowCoordLabel = value };
                loupe.Appearance = appearance;
            }),
            ColorChoice("座標ラベルの色", 3, index =>
            {
                appearance = appearance with { CoordLabelColor = ResolveColor(index, appearance.CoordLabelColor) };
                loupe.Appearance = appearance;
            }),
            ColorChoice("座標ラベルの背景色", 7, index =>
            {
                appearance = appearance with { CoordLabelBackgroundColor = ResolveColor(index, appearance.CoordLabelBackgroundColor) };
                loupe.Appearance = appearance;
            }),
            SampleSettings.Slider("座標ラベルの文字サイズ", 11, 6, 24, 1, true, value =>
            {
                appearance = appearance with { CoordLabelFontSize = value };
                loupe.Appearance = appearance;
            }),
            SampleSettings.Slider("座標ラベルの角丸", 2, 0, 12, 1, true, value =>
            {
                appearance = appearance with { CoordLabelCornerRadius = (int)value };
                loupe.Appearance = appearance;
            }),
            SampleSettings.Slider("座標ラベルの間隔", 4, 0, 24, 1, true, value =>
            {
                appearance = appearance with { CoordLabelOffset = value };
                loupe.Appearance = appearance;
            }));

        // ── サンプル設定（サンプル固有の操作。 並びは他 UI と共通） ──
        var samples = new StackPanel().Vertical().Spacing(6).Children(
            SampleSettings.LogLevel(logging),
            SampleSettings.Buttons(
                ("再描画を要求", () => loupe.InvalidateVisual()),
                ("ソースを設定して再表示", () =>
                {
                    loupe.Source = source;
                    loupeVisible.IsChecked = true;
                    loupe.IsVisible = true;
                }
        ),
                ("テスト ログを出力（非同期）", () => logger.LogInformation("テスト ログ（非同期）"))),
            SampleSettings.Check("選択矩形を表示", true, value => selection.IsVisible = value),
            SampleSettings.Number("選択矩形 X", selection.X, 0, ImageWidth, 10, true, value => selection.X = value),
            SampleSettings.Number("選択矩形 Y", selection.Y, 0, ImageHeight, 10, true, value => selection.Y = value),
            SampleSettings.Number("選択矩形 W", selection.Width, 0, ImageWidth, 10, true, value => selection.Width = value),
            SampleSettings.Number("選択矩形 H", selection.Height, 0, ImageHeight, 10, true, value => selection.Height = value),
            SampleSettings.Check("見本（格子線と四隅の鉤）を表示", true, value => preview.IsVisible = value));

        var window = SampleShell.Create(
            "LoupeControl サンプル（MewUI）", properties, samples, host, log);

        window.Closed += () =>
        {
            previewSource.Dispose();
            source.Dispose();
            logging.Dispose();
        };

        return window;
    }

    /// <summary>色の選択肢の行を作る。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="selectedIndex">初期の選択インデックス。</param>
    /// <param name="apply">変更の反映先（選択インデックス）。</param>
    /// <returns>項目名とコンボ ボックスを並べた行。</returns>
    private static FrameworkElement ColorChoice(string label, int selectedIndex, Action<int> apply)
        => SampleSettings.Choice(label, [.. s_colorChoices.Select(static choice => choice.Name)], selectedIndex, apply);

    /// <summary>「なし」を除いた色の選択肢から色を解決する。</summary>
    /// <param name="index">選択されたインデックス。</param>
    /// <param name="fallback">「なし」が選ばれたときに残す色（描画しないため見た目には影響しない）。</param>
    /// <returns>解決した色。</returns>
    private static DrawingColor ResolveColor(int index, DrawingColor fallback)
        => s_colorChoices[index].Color ?? fallback;
}
