using System.Globalization;
using EsUtil.Feature.Loupe.WinForms.Sample.Imaging;
using EsUtil.Feature.Loupe.WinForms.Sample.Logging;
using EsUtil.Feature.Loupe.WinForms.Sample.Presentation;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Loupe.WinForms.Sample.Views;

/// <summary>メイン ウィンドウ（設定パネル + テスト画像 + ルーペ）を組み立てる。</summary>
/// <remarks>
/// <b>示すこと</b>: <c>LoupeControl</c> の公開 API（<c>Source</c> / <c>CenterPoint</c> / <c>Options</c> /<br/>
/// <c>Appearance</c> / <c>ClientToPixelScaling</c> / <c>Log</c> / <c>DrawPlanResolver</c>）だけで<br/>
/// マウス追従の拡大表示・外観の変更・計画の差し替えができる。<br/>
/// <br/>
/// <b>配置</b>: ルーペの表示位置・表示サイズはコアが決める（<c>ResolveLayout</c>）ため、ホスト（<see cref="LoupeHostPanel"/>）は<br/>
/// 結果の矩形で子を配置するだけである。<br/>
/// <br/>
/// <b>拡張例</b>: 見本のルーペは外観と描画計画を差し替えて、十字線の代わりに格子線・枠線の代わりに四隅の鉤を描く<br/>
/// （提示層の派生は不要。 <see cref="GridLoupePlan"/>）。
/// </remarks>
internal sealed class MainForm : Form
{
    /// <summary>テスト画像の幅（ピクセル）。</summary>
    private const int ImageWidth = 800;

    /// <summary>テスト画像の高さ（ピクセル）。</summary>
    private const int ImageHeight = 600;

    /// <summary>設定パネルの幅（ピクセル）。</summary>
    private const int SettingsWidth = 384;

    /// <summary>項目名の幅（ピクセル）。</summary>
    private const int RowLabelWidth = 110;

    /// <summary>編集 UI の幅（ピクセル）。</summary>
    private const int EditorWidth = 150;

    /// <summary>ログの保持件数。</summary>
    private const int LogCapacity = 200;

    /// <summary>ログ表示の高さ（ピクセル）。</summary>
    private const int LogHeight = 180;

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

    /// <summary>テスト画像（ルーペのソース。 ウィンドウが所有し、閉じるときに解放する）。</summary>
    private readonly EsReadOnlyBitmap _source;

    /// <summary>見本の元画像（グラデーション）。</summary>
    private readonly EsReadOnlyBitmap _previewSource;

    /// <summary>画像とルーペを重ねるホスト。</summary>
    private readonly LoupeHostPanel _host;

    /// <summary>マウス追従するルーペ。</summary>
    private readonly LoupeControl _loupe;

    /// <summary>外観と描画計画を差し替えた見本のルーペ。</summary>
    private readonly LoupeControl _preview;

    /// <summary>選択矩形の状態（サンプルの表示用）。</summary>
    private readonly SelectionState _selection = new();

    /// <summary>画面へ出力するログの文言（末尾の <see cref="LogCapacity"/> 件を保持する）。</summary>
    private readonly List<string> _logLines = [];

    /// <summary>ログ表示のテキスト ボックス。</summary>
    private readonly TextBox _logBox = new();

    /// <summary>ログ表示に使うフォント。</summary>
    private readonly Font _logFont = new(FontFamily.GenericMonospace, 8f);

    /// <summary>ルーペの外観（色コンボボックスの選択で更新する単一値）。</summary>
    private LoupeAppearance _appearance = LoupeAppearance.Default;

    /// <summary>メイン ウィンドウを初期化する。</summary>
    public MainForm()
    {
        Text = "LoupeControl サンプル（WinForms）";
        MinimumSize = new Size(1000, 640);
        ClientSize = new Size(1340, 820);

        _source = TestImageFactory.Create(ImageWidth, ImageHeight);
        _previewSource = GridLoupePlan.CreateSource();

        // ログ表示（コントロールの Log へ設定する）
        var logger = new InMemoryLogger(AppendLog);

        // ルーペ（マウス追従）
        _loupe = new LoupeControl
        {
            Source = _source,
            PositionOffset = 24,
            Options = LoupeOptions.Default with { ZoomLevel = 4, RequestedSize = 240 },
            Appearance = _appearance,
            CenterPoint = new EsPoint(120, 120),
            LogPrefix = "[Loupe]",
            Log = logger,
        };

        // 見本（外観と描画計画の差し替え）
        _preview = new LoupeControl
        {
            Source = _previewSource,
            PositionOffset = 0,
            Options = LoupeOptions.Default with { ZoomLevel = 6, RequestedSize = 150 },
            Appearance = GridLoupePlan.Appearance,
            CenterPoint = new EsPoint(70, 60),
            DrawPlanResolver = static plan => GridLoupePlan.Resolve(in plan),
            LogPrefix = "[Preview]",
            Log = logger,
        };

        // 画像とルーペを重ねるホスト
        _host = new LoupeHostPanel(_source);
        _host.AddLoupe(_loupe);
        _host.AddLoupe(_preview);
        _host.PointerMoved += OnPointerMoved;

        // ルーペの上でもマウス追従を続ける（ルーペは子であるため、位置を親の座標へ写して使う）
        _loupe.MouseMove += OnLoupeMouseMove;

        // 選択矩形（ルーペのソースが元画像のどこにあるかを示す。 対象座標 = 元画像ピクセル）
        _selection.Changed += OnSelectionChanged;
        ApplySelection();

        // ルーペ画像上の破線は、描画完了直後に同一の描画面へ描く（Rendered の用途）
        _loupe.Rendered += OnLoupeRendered;

        BuildLayout();
    }

    /// <summary>ウィンドウを閉じるときに画像を解放する。</summary>
    /// <param name="e">終了のイベント引数。</param>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);

        _source.Dispose();
        _previewSource.Dispose();
        _logFont.Dispose();
    }

    /// <summary>マウス位置をルーペの中心へ反映する。</summary>
    /// <param name="sender">イベントの発行元。</param>
    /// <param name="e">マウス位置（元画像ピクセル座標）。</param>
    private void OnPointerMoved(object? sender, PointerMovedEventArgs e)
    {
        _loupe.CenterPoint = e.Position;
        _host.UpdateLoupeBounds();
    }

    /// <summary>ルーペ上のマウス位置を親の座標へ写してルーペの中心へ反映する。</summary>
    /// <param name="sender">イベントの発行元。</param>
    /// <param name="e">マウス イベント引数（ルーペのクライアント座標）。</param>
    /// <remarks>
    /// ルーペはホストの子（画像の上）であるため、カーソルがルーペの上にある間は本ハンドラーが動かす<br/>
    /// （座標はスクリーン座標を経由して親のクライアント座標へ写す。 画像は 1:1 で描くため親の座標 = 画像の画素）。
    /// </remarks>
    private void OnLoupeMouseMove(object? sender, MouseEventArgs e)
    {
        var position = _host.PointToClient(_loupe.PointToScreen(e.Location));
        _loupe.CenterPoint = new EsPoint(position.X, position.Y);
        _host.UpdateLoupeBounds();
    }

    /// <summary>選択矩形の変更をホストとルーペへ反映する。</summary>
    /// <param name="sender">イベントの発行元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        ApplySelection();

        // ルーペ内の破線も描き直す（プロパティに現れない入力のため、再描画を明示的に要求する）
        _loupe.Invalidate();
    }

    /// <summary>ルーペの描画完了直後に、選択矩形の破線を同じ描画面へ描く。</summary>
    /// <param name="sender">イベントの発行元。</param>
    /// <param name="e">描画完了のイベント引数（描画面と描画計画を持つ）。</param>
    private void OnLoupeRendered(object? sender, LoupeRenderedEventArgs e)
    {
        var content = e.Content;
        LoupeSelectionPainter.Paint(e.Graphics, in content, _selection);
    }

    /// <summary>選択矩形をホストへ反映する。</summary>
    private void ApplySelection()
        => _host.SetSelectionRect(_selection.Rect, _selection.IsVisible, _selection.Label);

    /// <summary>ログの 1 行を画面へ追加する。</summary>
    /// <param name="line">追加する行。</param>
    /// <remarks>ログは UI スレッドから出力されるため、そのまま画面を更新できる。</remarks>
    private void AppendLog(string line)
    {
        _logLines.Add(line);
        if (_logLines.Count > LogCapacity)
        {
            _logLines.RemoveAt(0);
        }

        _logBox.Text = string.Join(Environment.NewLine, _logLines);
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.ScrollToCaret();
    }

    /// <summary>設定パネルと画像の領域を組み立てる。</summary>
    private void BuildLayout()
    {
        _logBox.Multiline = true;
        _logBox.ReadOnly = true;
        _logBox.ScrollBars = ScrollBars.Vertical;
        _logBox.WordWrap = false;
        _logBox.Font = _logFont;
        _logBox.Size = new Size(SettingsWidth - 60, LogHeight);

        var settings = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
        };
        var sections = new Control[]
        {
            CreateSection(
                "表示",
                CreateCheckRow(true, value => _loupe.Visible = value, "ルーペを表示"),
                CreateSliderRow("拡大率", 4, 1, 16, value => SetZoomLevel(value)),
                CreateSliderRow("ルーペ サイズ", 240, 80, 480, value => SetRequestedSize(value)),
                CreateSliderRow("描画スケール", 10, 5, 20, value => _loupe.ClientToPixelScaling = value / 10.0)),
            CreateSection(
                "外観",
                CreateChoiceRow(s_colorChoices, 1, index =>
                {
                    _appearance = _appearance with
                    {
                        CrossColor = ResolveColor(index, _appearance.CrossColor),
                        ShowCrosshair = s_colorChoices[index].Color is not null,
                    };
                    _loupe.Appearance = _appearance;
                }),
                CreateChoiceRow(s_colorChoices, 6, index =>
                {
                    _appearance = _appearance with
                    {
                        PasteboardColor = ResolveColor(index, _appearance.PasteboardColor),
                        ShowPasteboard = s_colorChoices[index].Color is not null,
                    };
                    _loupe.Appearance = _appearance;
                }),
                CreateChoiceRow(s_colorChoices, 5, index =>
                {
                    _appearance = _appearance with
                    {
                        BorderColor = ResolveColor(index, _appearance.BorderColor),
                        ShowBorder = s_colorChoices[index].Color is not null,
                    };
                    _loupe.Appearance = _appearance;
                }),
                CreateSliderRow("枠線の太さ", 2, 0, 8, value =>
                {
                    _appearance = _appearance with { BorderThickness = value };
                    _loupe.Appearance = _appearance;
                }),
                CreateCheckRow(true, value =>
                {
                    _appearance = _appearance with { ShowCoordLabel = value };
                    _loupe.Appearance = _appearance;
                }, "座標ラベルを表示")),
            CreateSection(
                "操作",
                CreateRow(
                    string.Empty,
                    CreateButton("再描画を要求", () => _loupe.Invalidate()),
                    CreateButton("非表示にして解放", () =>
                    {
                        // 表示を止めて描画資源（画像の参照）を手放す
                        _loupe.Visible = false;
                        _loupe.Source = null;
                    }),
                    CreateButton("ソースを再設定", () => _loupe.Source = _source))),
            CreateSection(
                "選択矩形（破線オーバーレイ）",
                CreateCheckRow(true, value => _selection.IsVisible = value, "選択矩形を表示"),
                CreateNumberRow("X", _selection.X, 0, ImageWidth, value => _selection.X = value),
                CreateNumberRow("Y", _selection.Y, 0, ImageHeight, value => _selection.Y = value),
                CreateNumberRow("W", _selection.Width, 0, ImageWidth, value => _selection.Width = value),
                CreateNumberRow("H", _selection.Height, 0, ImageHeight, value => _selection.Height = value)),
            CreateSection(
                "拡張例（見本）",
                CreateCheckRow(true, value => _preview.Visible = value, "格子線と四隅の鉤を描く見本を表示")),
            CreateSection("ログ", _logBox),
        };
        settings.Controls.AddRange(sections);

        var settingsHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), AutoScroll = true };
        settingsHost.Controls.Add(settings);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, SettingsWidth));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.Controls.Add(settingsHost, 0, 0);
        root.Controls.Add(_host, 1, 0);

        Controls.Add(root);
        _host.UpdateLoupeBounds();
    }

    /// <summary>拡大率を設定し、ルーペの配置を更新する。</summary>
    /// <param name="zoomLevel">拡大率。</param>
    private void SetZoomLevel(int zoomLevel)
        => _loupe.Options = _loupe.Options with { ZoomLevel = zoomLevel };

    /// <summary>表示サイズ（DIP）を設定し、ルーペの配置を更新する。</summary>
    /// <param name="requestedSize">一辺の表示サイズ（DIP）。</param>
    private void SetRequestedSize(int requestedSize)
    {
        _loupe.Options = _loupe.Options with { RequestedSize = requestedSize };
        _host.UpdateLoupeBounds();
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
    /// <returns>見出しと項目行を縦に並べたコンテナー。</returns>
    private static GroupBox CreateSection(string title, params Control[] rows)
    {
        var body = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Location = new Point(10, 22),
        };
        body.Controls.AddRange(rows);

        var group = new GroupBox
        {
            Text = title,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(SettingsWidth - 56, 0),
        };
        group.Controls.Add(body);
        return group;
    }

    /// <summary>項目行（項目名 + 編集 UI）を構築する。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="controls">編集 UI。</param>
    /// <returns>項目名と編集 UI を横に並べたコンテナー。</returns>
    private static FlowLayoutPanel CreateRow(string label, params Control[] controls)
    {
        var row = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0),
        };

        row.Controls.Add(new Label
        {
            Text = label,
            Width = RowLabelWidth,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
        });
        row.Controls.AddRange(controls);
        return row;
    }

    /// <summary>ボタンを構築する。</summary>
    /// <param name="text">ボタンの文言。</param>
    /// <param name="action">押下時の処理。</param>
    /// <returns>ボタン。</returns>
    private static Button CreateButton(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        button.Click += (_, _) => action();
        return button;
    }

    /// <summary>チェックボックスの行を構築する。</summary>
    /// <param name="initial">初期値。</param>
    /// <param name="apply">変更の反映先。</param>
    /// <param name="text">チェックボックスのラベル。</param>
    /// <returns>チェックボックス。</returns>
    private static FlowLayoutPanel CreateCheckRow(bool initial, Action<bool> apply, string text)
    {
        var check = new CheckBox { Text = text, Checked = initial, AutoSize = true };
        check.CheckedChanged += (_, _) => apply(check.Checked);
        return CreateRow(string.Empty, check);
    }

    /// <summary>スライダーの行を構築する（現在値を数値で表示する）。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="initial">初期値。</param>
    /// <param name="minimum">最小値。</param>
    /// <param name="maximum">最大値。</param>
    /// <param name="apply">変更の反映先（スライダーの値）。</param>
    /// <returns>項目名・スライダー・現在値を並べた行。</returns>
    /// <remarks>
    /// スライダーは整数のみを扱うため、小数を扱う項目は呼び出し側が倍率で換算する<br/>
    /// （描画スケールは 0.5〜2.0 を 5〜20 として扱う）。
    /// </remarks>
    private static FlowLayoutPanel CreateSliderRow(string label, int initial, int minimum, int maximum, Action<int> apply)
    {
        var value = new Label
        {
            Width = 46,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = initial.ToString(CultureInfo.InvariantCulture),
        };

        var slider = new TrackBar
        {
            Minimum = minimum,
            Maximum = maximum,
            Value = initial,
            SmallChange = 1,
            LargeChange = Math.Max(1, (maximum - minimum) / 10),
            TickFrequency = Math.Max(1, (maximum - minimum) / 10),
            Width = EditorWidth,
            Height = 28,
        };
        slider.ValueChanged += (_, _) =>
        {
            value.Text = slider.Value.ToString(CultureInfo.InvariantCulture);
            apply(slider.Value);
        };

        return CreateRow(label, slider, value);
    }

    /// <summary>数値入力の行を構築する。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="initial">初期値。</param>
    /// <param name="minimum">最小値。</param>
    /// <param name="maximum">最大値。</param>
    /// <param name="apply">変更の反映先。</param>
    /// <returns>項目名と数値入力を横に並べた行。</returns>
    private static FlowLayoutPanel CreateNumberRow(string label, double initial, double minimum, double maximum, Action<double> apply)
    {
        var input = new NumericUpDown
        {
            Minimum = (decimal)minimum,
            Maximum = (decimal)maximum,
            Value = (decimal)initial,
            Increment = 10m,
            Width = EditorWidth,
        };
        input.ValueChanged += (_, _) => apply((double)input.Value);
        return CreateRow(label, input);
    }

    /// <summary>色などの選択肢の行を構築する。</summary>
    /// <param name="choices">選択肢（名前と色）。</param>
    /// <param name="selectedIndex">初期の選択インデックス。</param>
    /// <param name="apply">変更の反映先（選択インデックス）。</param>
    /// <returns>項目名とコンボボックスを横に並べた行。</returns>
    private static FlowLayoutPanel CreateChoiceRow((string Name, DrawingColor? Color)[] choices, int selectedIndex, Action<int> apply)
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = EditorWidth };
        combo.Items.AddRange([.. choices.Select(static choice => (object)choice.Name)]);
        combo.SelectedIndex = selectedIndex;
        combo.SelectedIndexChanged += (_, _) => apply(combo.SelectedIndex);
        return CreateRow(string.Empty, combo);
    }
}
