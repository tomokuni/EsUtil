namespace EsUtil.Ui.WinForms.SampleKit;

/// <summary>サンプル共通のウィンドウ構成（左: 設定 / 右上: 画像と操作 / 右下: ログ）。</summary>
/// <remarks>
/// <b>配置</b>: 左の設定パネルは固定幅、右は上下 2 段にする。 上段が画像と操作、下段がログであり、
/// <b>境界はドラッグで上下に動かせる</b>（<see cref="SplitContainer"/>）。 ログは下段の領域全体へ広がる。<br/>
/// <b>設定の分け方</b>: 左は「プロパティ設定」（対象コントロールの公開プロパティ）・「メソッド操作」
/// （対象コントロールのメソッド実行）・「サンプル設定」（サンプルアプリの外観と状態の変更）の 3 つの領域に分ける。<br/>
/// <b>ログ</b>: Serilog の出力を非同期に表示する（<see cref="SampleLogPanel"/>）。
/// </remarks>
public static class SampleShell
{
    /// <summary>設定パネルの幅（ピクセル）。</summary>
    public const int SettingsWidth = 420;

    /// <summary>ログの初期の高さ（ピクセル）。</summary>
    private const int LogInitialHeight = 220;

    /// <summary>ウィンドウを組み立てる。</summary>
    /// <param name="title">ウィンドウのタイトル。</param>
    /// <param name="propertySettings">プロパティ設定の行。</param>
    /// <param name="methodSettings">メソッド操作の行。 null の場合は領域を作らない。</param>
    /// <param name="sampleSettings">サンプル設定の行。</param>
    /// <param name="content">画像と操作（右上の領域）。</param>
    /// <param name="log">ログ（右下の領域）。</param>
    /// <param name="width">ウィンドウの初期幅（ピクセル）。</param>
    /// <param name="height">ウィンドウの初期高さ（ピクセル）。</param>
    /// <returns>組み立てたフォーム。</returns>
    /// <exception cref="ArgumentNullException">必須の引数が null の場合。</exception>
    public static Form Create(
        string title,
        Control propertySettings,
        Control? methodSettings,
        Control sampleSettings,
        Control content,
        SampleLogPanel log,
        int width = 1400,
        int height = 900)
    {
        ArgumentNullException.ThrowIfNull(propertySettings);
        ArgumentNullException.ThrowIfNull(sampleSettings);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(log);

        var settings = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
        };
        settings.Controls.Add(SampleSettings.Area("プロパティ設定", propertySettings));

        // メソッド操作は該当するメソッドを持つコントロールだけに出す（無いサンプルは領域ごと作らない）
        if (methodSettings is not null)
        {
            settings.Controls.Add(SampleSettings.Separator());
            settings.Controls.Add(SampleSettings.Area("メソッド操作", methodSettings));
        }

        settings.Controls.Add(SampleSettings.Separator());
        settings.Controls.Add(SampleSettings.Area("サンプル設定", sampleSettings));

        var settingsHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), AutoScroll = true };
        settingsHost.Controls.Add(settings);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            Panel1MinSize = 120,
            Panel2MinSize = 80,
            SplitterWidth = 6,
        };
        split.Panel1.Controls.Add(new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), Controls = { content } });
        split.Panel2.Controls.Add(new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), Controls = { log } });

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, SettingsWidth));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.Controls.Add(settingsHost, 0, 0);
        root.Controls.Add(split, 1, 0);

        var form = new Form
        {
            Text = title,
            ClientSize = new Size(width, height),
            MinimumSize = new Size(900, 600),
            StartPosition = FormStartPosition.CenterScreen,
        };
        form.Controls.Add(root);

        // 生成後に分割位置を決める（生成前はコントロールの大きさが確定していない）
        split.HandleCreated += (_, _) =>
        {
            if (split.Height > LogInitialHeight + split.Panel1MinSize)
            {
                split.SplitterDistance = split.Height - LogInitialHeight;
            }
        };

        return form;
    }
}
