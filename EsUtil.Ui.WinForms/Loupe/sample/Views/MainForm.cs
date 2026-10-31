using EsUtil.Ui.WinForms.Loupe.Sample.Imaging;
using EsUtil.Ui.WinForms.Loupe.Sample.Presentation;
using EsUtil.Ui.WinForms.SampleKit;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinForms.Loupe.Sample.Views;

/// <summary>メイン フォーム（設定パネル + テスト画像 + ルーペ）を組み立てる。</summary>
/// <remarks>
/// <b>示すこと</b>: <c>LoupeControl</c> の公開 API（<c>Source</c> / <c>CenterPoint</c> / <c>Options</c> /
/// <c>Appearance</c> / <c>ClientToPixelScaling</c> / <c>PositionOffset</c> / <c>Log</c> / <c>LogPrefix</c> /
/// <c>DrawPlanResolver</c>）だけで、マウス追従の拡大表示・外観の変更・計画の差し替えができる。<br/>
/// <br/>
/// <b>配置</b>: サンプル共通の <see cref="SampleShell"/>（左: 設定 / 右上: 画像 / 右下: ログ）を使う。<br/>
/// <b>ログ</b>: Serilog の出力を非同期に表示する（<see cref="SampleLogging"/>）。<br/>
/// <b>項目の並び</b>: 同じ機能の他 UI のサンプルと同じ順に並べる。
/// </remarks>
internal static class MainForm
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

    /// <summary>メイン フォームを生成する。</summary>
    /// <returns>生成したフォーム。</returns>
    public static Form Create()
    {
        // ── テスト画像と見本の元画像 ──
        var source = TestImageFactory.Create(ImageWidth, ImageHeight);
        var previewSource = GridLoupePlan.CreateSource();

        // ── ログ（Serilog。 非同期に表示する） ──
        var logging = new SampleLogging();
        var logger = logging.Logger;
        logger.LogInformation("サンプルを起動しました。");

        // ── ルーペ（マウス追従） ──
        var appearance = LoupeAppearance.Default;
        var zoomLevel = 4;
        var requestedSize = 240;
        var loupe = new LoupeControl
        {
            Source = source,
            PositionOffset = 24,
            Options = LoupeOptions.Default with { ZoomLevel = zoomLevel, RequestedSize = requestedSize },
            Appearance = appearance,
            CenterPoint = new EsPoint(120, 120),
            LogPrefix = "[Loupe]",
            Log = logger,
        };

        // ── 見本（外観と描画計画の差し替え） ──
        var preview = new LoupeControl
        {
            Source = previewSource,
            PositionOffset = 0,
            Options = LoupeOptions.Default with { ZoomLevel = 6, RequestedSize = 150 },
            Appearance = GridLoupePlan.Appearance,
            CenterPoint = new EsPoint(70, 60),
            DrawPlanResolver = static plan => GridLoupePlan.Resolve(in plan),
            // 接頭辞はプロセス内の全インスタンスで共有される（設定パネルの「ログ接頭辞」が全ルーペへ効く）
            Log = logger,
        };

        // ── 画像とルーペを重ねるホスト ──
        var host = new LoupeHostPanel(source) { Dock = DockStyle.Fill };
        host.AddLoupe(loupe);
        host.AddLoupe(preview);

        // ── 選択矩形（サンプル固有） ──
        var selection = new SelectionState();

        selection.Changed += (_, _) =>
        {
            host.SetSelectionRect(selection.Rect, selection.IsVisible, selection.Label);

            // ルーペ内の破線も描き直す（プロパティに現れない入力のため、再描画を明示的に要求する）
            loupe.Invalidate();
        };

        // 初期状態を反映する（変更イベントは初回に発火しない）
        host.SetSelectionRect(selection.Rect, selection.IsVisible, selection.Label);

        host.PointerMoved += (_, e) =>
        {
            loupe.CenterPoint = e.Position;
            host.UpdateLoupeBounds();
        };

        // ルーペの上でもマウス追従を続ける（ルーペは子であるため、位置を親の座標へ写して使う）
        loupe.MouseMove += (_, e) =>
        {
            var position = host.PointToClient(loupe.PointToScreen(e.Location));
            loupe.CenterPoint = new EsPoint(position.X, position.Y);
            host.UpdateLoupeBounds();
        };

        // ルーペ画像上の破線は、描画完了直後に同一の描画面へ描く（Rendered の用途）
        loupe.Rendered += (_, e) =>
        {
            var content = e.Content;
            LoupeSelectionPainter.Paint(e.Graphics, in content, selection);
        };

        // ── プロパティ設定（対象コントロールの公開プロパティ。 並びは他 UI と共通） ──
        var loupeVisible = (CheckBox)SampleSettings.Check("ルーペを表示", true, value =>
        {
            loupe.Visible = value;
            host.UpdateLoupeBounds();
        });

        var properties = Column(
            loupeVisible,
            SampleSettings.Slider("拡大率", zoomLevel, 1, 16, 1, true, value =>
            {
                zoomLevel = (int)value;
                ApplyOptions();
            }),
            SampleSettings.Slider("ルーペ サイズ", requestedSize, 80, 480, 10, true, value =>
            {
                requestedSize = (int)value;
                ApplyOptions();
            }),
            SampleSettings.Slider("描画スケール", 1.0, 0.5, 2.0, 0.1, false, value => loupe.ClientToPixelScaling = value, 10),
            SampleSettings.Slider("位置オフセット", 24, 0, 64, 4, true, value =>
            {
                loupe.PositionOffset = value;
                host.UpdateLoupeBounds();
            }),
            SampleSettings.Slider("中心 X", 120, 0, ImageWidth, 10, true, value => SetCenter(value, loupe.CenterPoint?.Y ?? 0)),
            SampleSettings.Slider("中心 Y", 120, 0, ImageHeight, 10, true, value => SetCenter(loupe.CenterPoint?.X ?? 0, value)),
            SampleSettings.Buttons(
                ("ソースを設定", () => loupe.Source = source),
                ("ソースを解除（描画資源を解放）", () =>
                {
                    loupe.Visible = false;
                    loupe.Source = null;
                    loupeVisible.Checked = false;
                }
        )),
            SampleSettings.Text("ログ接頭辞", "[Loupe]", value => loupe.LogPrefix = value),
            SampleSettings.Check("見本の描画計画（格子線・四隅の鉤）", false, value =>
                loupe.DrawPlanResolver = value ? static plan => GridLoupePlan.Resolve(in plan) : null),
            ColorChoice("十字線の色", 1, index => ApplyAppearance(appearance with
            {
                CrossColor = ResolveColor(index, appearance.CrossColor),
                ShowCrosshair = s_colorChoices[index].Color is not null,
            })),
            ColorChoice("画像外の色", 6, index => ApplyAppearance(appearance with
            {
                PasteboardColor = ResolveColor(index, appearance.PasteboardColor),
                ShowPasteboard = s_colorChoices[index].Color is not null,
            })),
            ColorChoice("枠線の色", 5, index => ApplyAppearance(appearance with
            {
                BorderColor = ResolveColor(index, appearance.BorderColor),
                ShowBorder = s_colorChoices[index].Color is not null,
            })),
            SampleSettings.Slider("枠線の太さ", 1, 0, 8, 1, true, value => ApplyAppearance(appearance with { BorderThickness = (int)value })),
            SampleSettings.Check("座標ラベルを表示", true, value => ApplyAppearance(appearance with { ShowCoordLabel = value })),
            ColorChoice("座標ラベルの色", 3, index => ApplyAppearance(appearance with { CoordLabelColor = ResolveColor(index, appearance.CoordLabelColor) })),
            ColorChoice("座標ラベルの背景色", 7, index => ApplyAppearance(appearance with { CoordLabelBackgroundColor = ResolveColor(index, appearance.CoordLabelBackgroundColor) })),
            SampleSettings.Slider("座標ラベルの文字サイズ", 11, 6, 24, 1, true, value => ApplyAppearance(appearance with { CoordLabelFontSize = value })),
            SampleSettings.Slider("座標ラベルの角丸", 2, 0, 12, 1, true, value => ApplyAppearance(appearance with { CoordLabelCornerRadius = (int)value })),
            SampleSettings.Slider("座標ラベルの間隔", 4, 0, 24, 1, true, value => ApplyAppearance(appearance with { CoordLabelOffset = value })));

        // ── サンプル設定（サンプル固有の操作。 並びは他 UI と共通） ──
        var samples = Column(
            SampleSettings.Buttons(
                ("再描画を要求", () => loupe.Invalidate()),
                ("ソースを設定して再表示", () =>
                {
                    loupe.Source = source;
                    loupeVisible.Checked = true;
                    loupe.Visible = true;
                }
        ),
                ("テスト ログを出力（非同期）", () => logger.LogInformation("テスト ログ（非同期）"))),
            SampleSettings.Check("選択矩形を表示", true, value => selection.IsVisible = value),
            SampleSettings.Number("選択矩形 X", selection.X, 0, ImageWidth, 0, value => selection.X = value, 10),
            SampleSettings.Number("選択矩形 Y", selection.Y, 0, ImageHeight, 0, value => selection.Y = value, 10),
            SampleSettings.Number("選択矩形 W", selection.Width, 0, ImageWidth, 0, value => selection.Width = value, 10),
            SampleSettings.Number("選択矩形 H", selection.Height, 0, ImageHeight, 0, value => selection.Height = value, 10),
            SampleSettings.Check("見本（格子線と四隅の鉤）を表示", true, value => preview.Visible = value));

        var log = new SampleLogPanel(logging.Buffer);
        var form = SampleShell.Create("LoupeControl サンプル（WinForms）", properties, samples, host, log);

        // 実効倍率はハンドル生成時に確定するため、表示後にもう一度配置を解決する
        form.Shown += (_, _) => host.UpdateLoupeBounds();
        form.FormClosed += (_, _) =>
        {
            source.Dispose();
            previewSource.Dispose();
            logging.Dispose();
        };

        return form;

        // 表示の設定（Options）を反映する
        void ApplyOptions()
        {
            loupe.Options = loupe.Options with { ZoomLevel = zoomLevel, RequestedSize = requestedSize };
            host.UpdateLoupeBounds();
        }

        // 拡大中心を設定し、配置を更新する
        void SetCenter(double x, double y)
        {
            loupe.CenterPoint = new EsPoint(x, y);
            host.UpdateLoupeBounds();
        }

        // 外観を更新してルーペへ反映する
        void ApplyAppearance(LoupeAppearance value)
        {
            appearance = value;
            loupe.Appearance = value;
        }
    }

    /// <summary>設定の行を縦に並べる。</summary>
    /// <param name="rows">行。</param>
    /// <returns>縦に並べた要素。</returns>
    private static Control Column(params Control[] rows)
    {
        var panel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };
        panel.Controls.AddRange(rows);
        return panel;
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
