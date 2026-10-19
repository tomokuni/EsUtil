using EsUtil.Ui.WinForms.Marquee.Sample.Imaging;
using EsUtil.Ui.WinForms.Marquee.Sample.Logging;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.WinForms.Marquee.Sample;

/// <summary>マーキー（選択矩形）コントロールの WinForms 版サンプル画面。</summary>
/// <remarks>
/// <b>構成</b>: 下地の画像（<see cref="PictureBox"/>）の子として本コントロールを重ねる。<br/>
/// コントロールの背景は透明であるため、親（画像）の描画が自分の背景として描かれる（画像が透けて見える）。<br/>
/// <b>注意</b>: 透明のコントロールは<b>親の描画</b>だけを背景に使う。 兄弟として重ねると画像が隠れるため、<br/>
/// オーバーレイするコントロールは画像を持つコントロールの子にする。<br/>
/// <b>本サンプルが示すこと</b>: 設定（<see cref="MarqueeOptions"/>）と外観（<see cref="MarqueeAppearance"/>）の指定、<br/>
/// 通知（選択・確定・キャンセル・追従位置）の購読、操作ログの表示、<c>Rendered</c> による付加情報の描画。
/// </remarks>
internal sealed class MainForm : Form
{
    /// <summary>下地の画像の幅（画素）。</summary>
    private const int ImageWidth = 1600;

    /// <summary>下地の画像の高さ（画素）。</summary>
    private const int ImageHeight = 1000;

    /// <summary>ログ欄の幅（画素）。</summary>
    private const int LogWidth = 420;

    /// <summary>ログを取り出す間隔（ミリ秒）。</summary>
    private const int LogDrainInterval = 200;

    /// <summary>表示中の選択矩形コントロール。</summary>
    private readonly MarqueeControl _marquee;

    /// <summary>下地の画像を表示する要素（本コントロールの親）。</summary>
    private readonly PictureBox _canvas;

    /// <summary>操作とログの表示欄。</summary>
    private readonly ListBox _log;

    /// <summary>状態を表示する帯。</summary>
    private readonly ToolStripStatusLabel _status;

    /// <summary>ライブラリが出力したログの受け皿。</summary>
    private readonly InMemoryLogger _logger = new();

    /// <summary>ログ欄へ未表示のログを流すタイマー。</summary>
    private readonly System.Windows.Forms.Timer _logTimer = new() { Interval = LogDrainInterval };

    /// <summary>付加情報の描画に使うフォント。</summary>
    private readonly Font _overlayFont;

    /// <summary>ハイライトの対象（固定の矩形。 対象座標）。</summary>
    private readonly EsRect _highlightRect = new(120, 120, 400, 300);

    /// <summary>サンプル画面を初期化する。</summary>
    public MainForm()
    {
        Text = "EsUtil.Ui.WinForms.Marquee サンプル";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1280, 820);
        MinimumSize = new Size(900, 600);

        _overlayFont = new Font(Font.FontFamily, 14.0f, FontStyle.Regular, GraphicsUnit.Pixel);

        _canvas = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = TestImageFactory.Create(ImageWidth, ImageHeight),
        };

        _marquee = new MarqueeControl
        {
            Dock = DockStyle.Fill,
            // 設定は 1 つの不変オブジェクトで与える（指定しない項目は既定値を保つ）
            Options = MarqueeOptions.Default with { HandleSize = 12.0, MinSelectionSize = 8.0 },
            // 外観（色・ラベル体裁・表示可否）は描画の設定値が単一所有する
            Appearance = MarqueeAppearance.Default with
            {
                OuterBorderColor = Color.FromArgb(255, 255, 214, 96),
                HandleColor = Color.FromArgb(255, 255, 255, 255),
                HandleBorderColor = Color.FromArgb(255, 32, 32, 32),
                InnerFillColor = Color.FromArgb(32, 255, 255, 255),
                MarkerColor = Color.FromArgb(255, 255, 96, 96),
            },
            Log = _logger,
        };

        _log = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true };

        _status = new ToolStripStatusLabel("左ドラッグで選択 / ハンドルでリサイズ / 矢印キーで微調整 / Enter 確定 / Escape キャンセル")
        {
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _marquee.SelectionStarted += (_, e) => AppendLog($"SelectionStarted: {e.Point}");
        _marquee.SelectionChanged += (_, e) => AppendLog($"SelectionChanged: {e.Rect}");
        _marquee.SelectionCompleted += (_, e) => AppendLog($"SelectionCompleted: {e.Rect}");
        _marquee.SelectionCancelled += (_, _) => AppendLog("SelectionCancelled");
        _marquee.ConfirmRequested += (_, e) => AppendLog($"ConfirmRequested: {e.Rect}");
        _marquee.CancelRequested += (_, _) => AppendLog("CancelRequested");
        _marquee.LoupePositionMoved += (_, e) => _status.Text = $"追従位置: X={e.Point.X:0} Y={e.Point.Y:0}";
        _marquee.Rendered += OnRendered;

        BuildLayout();

        // 子（画像の上）へ重ねる（透明のコントロールは親の描画を背景に使う）
        _canvas.Controls.Add(_marquee);

        _logTimer.Tick += (_, _) => DrainLogger();
        _logTimer.Start();
    }

    /// <summary>サンプル画面の配置を組み立てる。</summary>
    private void BuildLayout()
    {
        var side = new Panel { Dock = DockStyle.Right, Width = LogWidth, Padding = new Padding(8) };
        side.Controls.Add(_log);
        side.Controls.Add(BuildCommandBar());

        var status = new StatusStrip();
        status.Items.Add(_status);

        Controls.Add(_canvas);
        Controls.Add(side);
        Controls.Add(status);
    }

    /// <summary>操作ボタンを並べた帯を組み立てる。</summary>
    /// <returns>操作ボタンの帯（上端へ配置する）。</returns>
    private FlowLayoutPanel BuildCommandBar()
    {
        var reset = new Button { Text = "選択をクリア", AutoSize = true };
        reset.Click += (_, _) => _marquee.ResetSelection();

        var highlight = new Button { Text = "ハイライト切替", AutoSize = true };
        highlight.Click += (_, _) =>
        {
            _marquee.HighlightSourceRect = _marquee.HighlightSourceRect is null ? _highlightRect : null;
            AppendLog(_marquee.HighlightSourceRect is null ? "ハイライト解除" : $"ハイライト: {_highlightRect}");
        };

        var clear = new Button { Text = "ログを消去", AutoSize = true };
        clear.Click += (_, _) => _log.Items.Clear();

        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Height = 40 };
        bar.Controls.Add(reset);
        bar.Controls.Add(highlight);
        bar.Controls.Add(clear);
        return bar;
    }

    /// <summary>コントロールが描き終えた後に、ホスト固有の付加情報を描く。</summary>
    /// <param name="sender">イベントの発生元。</param>
    /// <param name="e">描画内容（描画面と計画）。</param>
    /// <remarks>
    /// 選択サイズと操作ガイドはアプリ固有の情報のため、コントロールは描かない（ホストが描く）。<br/>
    /// 描画面は DPI の拡大が適用済みであるため、DIP 座標で描ける。
    /// </remarks>
    private void OnRendered(object? sender, MarqueeRenderedEventArgs e)
    {
        var content = e.Content;
        var text = content.FocusDipRect is { } focus
            ? $"選択 {focus.Width:0} x {focus.Height:0}"
            : "ドラッグで範囲を選択してください";

        var origin = new PointF((float)content.ClientRect.X + 12.0f, (float)content.ClientRect.Y + 12.0f);

        using var background = new SolidBrush(Color.FromArgb(160, 0, 0, 0));
        var size = e.Graphics.MeasureString(text, _overlayFont);
        e.Graphics.FillRectangle(background, origin.X, origin.Y, size.Width + 8.0f, size.Height + 4.0f);
        e.Graphics.DrawString(text, _overlayFont, Brushes.White, origin);
    }

    /// <summary>ライブラリが出力したログを表示欄へ流す。</summary>
    private void DrainLogger()
    {
        while (_logger.TryDequeue(out var message))
        {
            AppendLog(message);
        }
    }

    /// <summary>表示欄へ 1 行を追加する（追記後は最新行へ移動する）。</summary>
    /// <param name="message">追加する行。</param>
    private void AppendLog(string message)
    {
        _log.Items.Add(message);
        _log.TopIndex = _log.Items.Count - 1;
    }

    /// <summary>保持している資源を破棄する。</summary>
    /// <param name="disposing">マネージド資源を破棄する場合は true。</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _logTimer.Stop();
            _logTimer.Dispose();
            _overlayFont.Dispose();
            _canvas.Image?.Dispose();
        }

        base.Dispose(disposing);
    }
}
