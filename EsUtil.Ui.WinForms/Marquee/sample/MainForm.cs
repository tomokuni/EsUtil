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

    /// <summary>色の選択肢（表示の可否は各項目のチェックが持つため、色だけを選ぶ）。</summary>
    private static readonly (string Name, Color Color)[] s_colorChoices =
    [
        ("白", Color.White),
        ("アンバー", Color.FromArgb(255, 255, 196, 0)),
        ("赤", Color.FromArgb(255, 255, 0, 0)),
        ("緑", Color.FromArgb(255, 0, 200, 83)),
        ("薄灰", Color.FromArgb(255, 0xD3, 0xD3, 0xD3)),
        ("黒 50%", Color.FromArgb(128, 0, 0, 0)),
        ("黒 60%", Color.FromArgb(153, 0, 0, 0)),
    ];

    /// <summary>サンプル画面を生成する。</summary>
    /// <returns>生成したフォーム。</returns>
    public static Form Create()
    {
        // ── ログ（Serilog。 非同期に表示する） ──
        var logging = new SampleLogging();
        var logger = logging.Logger;
        logger.LogInformation("サンプルを起動しました。");

        // 外観の初期値は設定パネルが持つ（パネルの初期値がそのまま反映されるため、ここでは既定を使う）
        var appearance = MarqueeAppearance.Default;

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

        marquee.SelectionStarted += (_, e) => Write($"開始: ({e.Point.X:0.#}, {e.Point.Y:0.#})");
        marquee.SelectionChanged += (_, e) =>
        {
            // ドラッグ中は毎フレーム届くため Trace（既定の表示レベルでは出さない）
            Write($"選択中: {e.Rect}", LogLevel.Trace);
            status.Text = $"選択: {e.Rect}";
        };
        marquee.SelectionCompleted += (_, e) =>
        {
            Write($"確定可能: {e.Rect}");
            status.Text = $"選択: {e.Rect}";
        };
        marquee.SelectionCancelled += (_, _) => Write("キャンセル（選択が解除された）");
        marquee.ConfirmRequested += (_, e) => Write($"確定要求: {e.Rect}");
        marquee.CancelRequested += (_, _) => Write("キャンセル要求");
        marquee.LoupePositionMoved += (_, e) =>
        {
            // ポインター移動のたびに届く高頻度の値であるため Trace（画面の表示は状態の行が担う）
            Write($"ルーペ位置: ({e.Point.X:0.#}, {e.Point.Y:0.#})", LogLevel.Trace);
            status.Text = $"追従位置: X={e.Point.X:0} Y={e.Point.Y:0}";
        };

        // 選択サイズと操作ガイドはアプリ固有の情報のため、コントロールの描画完了後にホストが描く
        marquee.Rendered += (_, e) =>
        {
            var content = e.Content;
            var text = content.FocusDipRect is { } focus
                ? $"選択 {focus.Width:0} x {focus.Height:0}"
                : "ドラッグで範囲を選択してください";

            // 下地は敷かない（他 UI と同じ見た目にする。 文字だけを左上へ置く）
            var origin = new PointF((float)content.ClientRect.X + 12.0f, (float)content.ClientRect.Y + 12.0f);
            e.Graphics.DrawString(text, overlayFont, Brushes.White, origin);
        };

        // ── プロパティ設定（対象コントロールの公開プロパティ。 並びは他 UI と共通） ──
        var properties = Column(
            // ハイライトはコントロールのプロパティ（HighlightSourceRect）を切り替える操作である
            SampleSettings.Buttons(("ハイライト矩形を設定/解除", ToggleHighlight)),
            SampleSettings.Slider("ズーム", zoom, MinZoom, MaxZoom, ZoomStep, false, value => ApplyZoom(value), 10),
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
            SampleSettings.Slider("サイズ ラベルの表示スケール", 0, 0, 2, 0.1, false, value => marquee.DisplayScaleScaling = value, 10),
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
        var methods = Column(
            SampleSettings.Buttons(
                ("選択をクリア", () => marquee.ResetSelection()),
                ("矩形を設定", () => marquee.SetSelection(new EsRect(250, 180, 300, 200))),
                ("確定を要求", () => marquee.RequestConfirm()),
                ("キャンセルを要求", () => marquee.RequestCancel())));

        // ── サンプル設定（サンプルアプリの外観と状態） ──
        var samples = Column(
            SampleSettings.LogLevel(logging),
            new Label
            {
                Text = "ドラッグ: 選択 / マウスを押したままの矢印キー: 位置の微調整 / Enter: 確定 / Escape: キャンセル",
                AutoSize = true,
                MaximumSize = new Size(SampleShell.SettingsWidth - 80, 0),
            },
            SampleSettings.Buttons(
                ("テスト ログを出力（非同期）", () => logger.LogInformation("テスト ログ（非同期）"))),
            status);

        var log = new SampleLogPanel(logging.Buffer);
        var form = SampleShell.Create("Marquee サンプル (WinForms)", properties, methods, samples, surface, log);
        form.FormClosed += (_, _) =>
        {
            canvas.Image?.Dispose();
            overlayFont.Dispose();
            logging.Dispose();
        };

        return form;

        // ログを 1 行出力する（書式化は Serilog に任せる）
        void Write(string line, LogLevel level = LogLevel.Debug)
        {
            // 文字列をそのまま記録する（テンプレート解析も引数の評価もしない）。
            // 設定の変更は Information、状態の変化は Debug、高頻度の値は Trace（ログ規約 §2）
            logger.Log(level, default, line, null, static (state, _) => state);
        }

        // 外観を更新してコントロールへ反映する
        void Apply(MarqueeAppearance value)
        {
            appearance = value;
            marquee.Appearance = value;
            marquee.InvalidateRender();
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
                Write("ハイライト解除（注目矩形は選択へ戻る）");
                return;
            }

            marquee.HighlightSourceRect = highlightRect;
            Write($"ハイライト（表示専用の注目）: {highlightRect} / 選択は {marquee.SelectionSourceRect} のまま（ハイライト中は操作できない）");
        }

        // ズームを反映する（画像の表示サイズと、クライアント座標 → 対象座標の変換を揃える）
        void ApplyZoom(double value)
        {
            zoom = value;
            canvas.Size = new Size(
                (int)Math.Round(ImageWidth * value, MidpointRounding.AwayFromZero),
                (int)Math.Round(ImageHeight * value, MidpointRounding.AwayFromZero));

            // 倍率は「対象座標 / ローカル DIP」。 ズームは画像を倍率ぶん大きく描くため、1 DIP が覆う対象座標は 1 / ズームになる
            marquee.ClientToSourceScaling = 1.0 / value;
            marquee.SetCoordTransform(
                CoordTransform.ClientToSource(() => 1.0 / zoom, () => new EsSize(ImageWidth, ImageHeight)));
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

    /// <summary>色の選択肢から色を解決する。</summary>
    /// <param name="index">選択されたインデックス。</param>
    /// <returns>解決した色。</returns>
    private static Color ResolveColor(int index) => s_colorChoices[index].Color;

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
