using EsUtil.Ui.WinForms.Marquee.Sample.Imaging;
using EsUtil.Ui.WinForms.Marquee.Sample.Presentation;
using EsUtil.Ui.WinForms.SampleKit;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;

namespace EsUtil.Ui.WinForms.Marquee.Sample;

/// <summary>マーキー（選択矩形）コントロールの WinForms 版サンプル画面。</summary>
/// <remarks>
/// <b>構成</b>: 下地の画像（<see cref="PictureBox"/>）の子として本コントロールを重ねる。<br/>
/// コントロールの背景は透明であるため、親（画像）の描画が自分の背景として描かれる（画像が透けて見える）。<br/>
/// <b>注意</b>: 透明のコントロールは<b>親の描画</b>だけを背景に使う。 兄弟として重ねると画像が隠れるため、<br/>
/// オーバーレイするコントロールは画像を持つコントロールの子にする。<br/>
/// <b>配置</b>: サンプル共通の <see cref="SampleShell"/>（左: 設定 / 右上: 画像 / 右下: ログ）を使う。<br/>
/// <b>ログ</b>: Serilog の出力を非同期に表示する（<see cref="SampleLogging"/>）。
/// </remarks>
internal static class MainForm
{
    /// <summary>下地の画像の幅（画素）。</summary>
    private const int ImageWidth = 800;

    /// <summary>下地の画像の高さ（画素）。</summary>
    private const int ImageHeight = 600;

    /// <summary>ズームの最小倍率。</summary>
    private const double MinZoom = 0.1;

    /// <summary>ズームの最大倍率。</summary>
    private const double MaxZoom = 5.0;

    /// <summary>ズームの刻み（スライダーの増減）。</summary>
    private const double ZoomStep = 0.1;

    /// <summary>色の選択肢（「なし」は描画しないことを表す）。</summary>
    private static readonly (string Name, Color? Color)[] s_colorChoices =
    [
        ("なし", null),
        ("白", Color.White),
        ("アンバー", Color.FromArgb(255, 255, 196, 0)),
        ("赤", Color.FromArgb(255, 255, 0, 0)),
        ("緑", Color.FromArgb(255, 0, 200, 83)),
        ("薄灰", Color.FromArgb(255, 0xD3, 0xD3, 0xD3)),
    ];

    /// <summary>サンプル画面を生成する。</summary>
    /// <returns>生成したフォーム。</returns>
    public static Form Create()
    {
        // ── ログ（Serilog。 非同期に表示する） ──
        var logging = new SampleLogging();
        var logger = logging.Logger;
        logger.LogInformation("サンプルを起動しました。");

        var appearance = MarqueeAppearance.Default with
        {
            OuterBorderColor = Color.FromArgb(255, 255, 214, 96),
            HandleColor = Color.FromArgb(255, 255, 255, 255),
            HandleBorderColor = Color.FromArgb(255, 32, 32, 32),
            InnerFillColor = Color.FromArgb(32, 255, 255, 255),
            MarkerColor = Color.FromArgb(255, 255, 96, 96),
        };

        // ── 下地の画像とコントロール ──
        // 画像はズーム倍率を掛けた画素サイズで置く（PictureBox の Zoom は画像をその大きさへ拡大する）
        var canvas = new PictureBox
        {
            Location = Point.Empty,
            Size = new Size(ImageWidth, ImageHeight),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = TestImageFactory.Create(ImageWidth, ImageHeight),
        };

        var marquee = new MarqueeControl
        {
            Dock = DockStyle.Fill,
            Options = MarqueeOptions.Default,
            Appearance = appearance,
            Log = logger,
            LogPrefix = "[Marquee]",
        };

        // 子（画像の上）へ重ねる（透明のコントロールは親の描画を背景に使う）
        canvas.Controls.Add(marquee);

        // ズームで表示領域より大きくなったときにパンできるよう、スクロールできる入れ物へ置く
        var surface = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        surface.Controls.Add(canvas);

        var zoom = 1.0;
        ApplyZoom(zoom);

        // 起動直後に枠線・ハンドル・ラベルが見えるように、選択を 1 つ与えておく
        marquee.SetSelection(new EsRect(120, 80, 240, 180));

        var status = new TextBlockLike();
        var highlightRect = new EsRect(120, 120, 400, 300);
        var overlayFont = new Font(FontFamily.GenericSansSerif, 14.0f, FontStyle.Regular, GraphicsUnit.Pixel);

        marquee.SelectionStarted += (_, e) => Write($"SelectionStarted: {e.Point}");
        marquee.SelectionChanged += (_, e) => Write($"SelectionChanged: {e.Rect}");
        marquee.SelectionCompleted += (_, e) => Write($"SelectionCompleted: {e.Rect}");
        marquee.SelectionCancelled += (_, _) => Write("SelectionCancelled");
        marquee.ConfirmRequested += (_, e) => Write($"ConfirmRequested: {e.Rect}");
        marquee.CancelRequested += (_, _) => Write("CancelRequested");
        marquee.LoupePositionMoved += (_, e) => status.Text = $"追従位置: X={e.Point.X:0} Y={e.Point.Y:0}";

        // 選択サイズと操作ガイドはアプリ固有の情報のため、コントロールの描画完了後にホストが描く
        marquee.Rendered += (_, e) =>
        {
            var content = e.Content;
            var text = content.FocusDipRect is { } focus
                ? $"選択 {focus.Width:0} x {focus.Height:0}"
                : "ドラッグで範囲を選択してください";

            var origin = new PointF((float)content.ClientRect.X + 12.0f, (float)content.ClientRect.Y + 12.0f);
            using var background = new SolidBrush(Color.FromArgb(160, 0, 0, 0));
            var size = e.Graphics.MeasureString(text, overlayFont);
            e.Graphics.FillRectangle(background, origin.X, origin.Y, size.Width + 8.0f, size.Height + 4.0f);
            e.Graphics.DrawString(text, overlayFont, Brushes.White, origin);
        };

        // ── プロパティ設定（対象コントロールの公開プロパティ。 並びは他 UI と共通） ──
        var properties = Column(
            SampleSettings.Slider("ズーム", zoom, MinZoom, MaxZoom, ZoomStep, false, value => ApplyZoom(value), 10),
            SampleSettings.Slider("表示スケール", 1.0, 0.5, 2.0, 0.1, false, value => marquee.DisplayScaleScaling = value, 10),
            SampleSettings.Slider("ハンドル サイズ", 10, 2, 30, 1, true, value => marquee.Options = marquee.Options with { HandleSize = value }),
            SampleSettings.Slider("ハンドル枠の太さ", 1.5, 0.5, 8, 0.5, false, value => marquee.Options = marquee.Options with { HandleBorderThickness = value }),
            SampleSettings.Slider("マーカーの長さ", 16, 8, 40, 2, true, value => marquee.Options = marquee.Options with { MarkerLength = value }),
            SampleSettings.Slider("ヒット半径", 12, 2, 40, 1, true, value => marquee.Options = marquee.Options with { HandleHitRadius = value }),
            SampleSettings.Slider("最小選択サイズ", 6, 1, 40, 1, true, value => marquee.Options = marquee.Options with { MinSelectionSize = value }),
            SampleSettings.Check("画像内にクリップ", true, value => marquee.Options = marquee.Options with { ClampToSourceBounds = value }),
            SampleSettings.Text("ログ接頭辞", "[Marquee]", value => marquee.LogPrefix = value),
            SampleSettings.Check("描画計画を差し替える（破線・ガイド線）", false, value =>
                marquee.DrawPlanResolver = value ? static plan => GuideMarqueePlan.Resolve(in plan) : null),
            SampleSettings.Check("ハンドルを表示", true, value => Apply(appearance with { ShowHandles = value })),
            ColorChoice("ハンドルの色", 1, index => Apply(appearance with { HandleColor = ResolveColor(index, appearance.HandleColor) })),
            ColorChoice("ハンドル枠の色", 2, index => Apply(appearance with { HandleBorderColor = ResolveColor(index, appearance.HandleBorderColor) })),
            SampleSettings.Check("マーカーを表示", true, value => Apply(appearance with { ShowMarker = value })),
            ColorChoice("マーカーの色", 3, index => Apply(appearance with { MarkerColor = ResolveColor(index, appearance.MarkerColor) })),
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
            SampleSettings.Slider("ラベルの余白", 4, 0, 24, 1, true, value => Apply(appearance with { LabelPadding = value })));

        // ── サンプル設定（サンプル固有の操作。 並びは他 UI と共通） ──
        var samples = Column(
            new Label
            {
                Text = "ドラッグ: 選択 / 矢印キー: nudge / Enter: 確定 / Escape: キャンセル",
                AutoSize = true,
                MaximumSize = new Size(SampleShell.SettingsWidth - 80, 0),
            },
            SampleSettings.Buttons(
                ("選択をクリア", () => marquee.ResetSelection()),
                ("矩形を設定", () => marquee.SetSelection(new EsRect(250, 180, 300, 200))),
                ("ハイライト切替", () =>
                {
                    marquee.HighlightSourceRect = marquee.HighlightSourceRect is null ? highlightRect : null;
                    Write(marquee.HighlightSourceRect is null ? "ハイライト解除" : $"ハイライト: {highlightRect}");
                }
        ),
                ("確定を要求", () => marquee.RequestConfirm()),
                ("キャンセルを要求", () => marquee.RequestCancel()),
                ("テスト ログを出力（非同期）", () => logger.LogInformation("テスト ログ（非同期）"))),
            status);

        var log = new SampleLogPanel(logging.Buffer);
        var form = SampleShell.Create("MarqueeControl サンプル（WinForms）", properties, samples, surface, log);
        form.FormClosed += (_, _) =>
        {
            canvas.Image?.Dispose();
            overlayFont.Dispose();
            logging.Dispose();
        };

        return form;

        // ログを 1 行出力する（書式化は Serilog に任せる）
        void Write(string line)
        {
            // 文字列をそのまま記録する（テンプレート解析も引数の評価もしない）
            logger.Log(LogLevel.Information, default, line, null, static (state, _) => state);
        }

        // 外観を更新してコントロールへ反映する
        void Apply(MarqueeAppearance value)
        {
            appearance = value;
            marquee.Appearance = value;
            marquee.InvalidateRender();
        }

        // ズームを反映する（画像の表示サイズと、クライアント座標 → 対象座標の変換を揃える）
        void ApplyZoom(double value)
        {
            zoom = value;
            canvas.Size = new Size(
                (int)Math.Round(ImageWidth * value, MidpointRounding.AwayFromZero),
                (int)Math.Round(ImageHeight * value, MidpointRounding.AwayFromZero));

            marquee.ClientToSourceScaling = value;
            marquee.SetCoordTransform(CoordTransform.ClientToSource(() => zoom, () => new EsSize(ImageWidth, ImageHeight)));
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
    private static Color ResolveColor(int index, Color fallback)
        => s_colorChoices[index].Color ?? fallback;

    /// <summary>状態の 1 行を表示するラベル（読み取り専用）。</summary>
    private sealed class TextBlockLike : Label
    {
        /// <summary>ラベルを初期化する。</summary>
        public TextBlockLike()
        {
            AutoSize = true;
            MaximumSize = new Size(SampleShell.SettingsWidth - 80, 0);
            Text = "選択なし";
        }
    }
}
