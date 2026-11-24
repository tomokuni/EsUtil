using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core.Palette;
using Xunit;
using MewColor = Aprillz.MewUI.Color;

namespace EsUtil.Ui.MewUI.ColorSelector.Tests;

/// <summary>色選択コントロール（MewUI 版）の公開 API とログの単体テスト。</summary>
/// <remarks>
/// ウィンドウを開かずに生成・プロパティ設定だけで検証できる範囲に限定する（入力はサンプルで手動確認する）。
/// 色文字列の解釈そのものはコアのテストが検証する（本クラスは UI との同期を見る）。
/// </remarks>
[Collection(ColorSelectorLogPrefix.Name)]
public class ColorSelectorControlTests
{
    [Fact]
    internal void ColorSortMode_並び順を変えても色の変更として扱わない()
    {
        var logger = new RecordingLogger();
        var control = new ColorSelectorControl { Log = logger, SelectedColor = "Blue" };
        var raised = 0;
        control.SelectedColorChanged += (_, _) => raised++;

        // 一覧を作り直すと選択が動く。 これを「利用者の選択」として扱うと、
        // 並び順を変えただけで色の変更イベントが発火し、Setting の Information が同じ内容で出る
        logger.Messages.Clear();
        control.ColorSortMode = ColorSortMode.ByName;
        control.ColorSortMode = ColorSortMode.ByHue;

        Assert.Equal(0, raised);
        Assert.Equal("#FF0000FF", control.SelectedColor);
        Assert.DoesNotContain(logger.Messages, message => message.Contains("Setting:", StringComparison.Ordinal));
    }

    [Fact]
    internal void 既定の色は白である()
    {
        var control = new ColorSelectorControl();

        Assert.Equal("#FFFFFFFF", control.SelectedColor);
        Assert.False(control.HasValidationError);
        Assert.Null(control.ValidationErrorMessage);
    }

    [Fact]
    internal void SelectedColor_色名を設定すると保存値へ正規化される()
    {
        var control = new ColorSelectorControl { SelectedColor = "Red" };

        Assert.Equal("#FFFF0000", control.SelectedColor);
    }

    [Fact]
    internal void SelectedColor_不透明度付きの指定はアルファへ合成される()
    {
        var control = new ColorSelectorControl { SelectedColor = "Red+50%" };

        Assert.Equal("#80FF0000", control.SelectedColor);
    }

    [Fact]
    internal void SelectedColor_変更するとイベントが発火する()
    {
        var control = new ColorSelectorControl();
        var raised = 0;
        control.SelectedColorChanged += (_, _) => raised++;

        control.SelectedColor = "Blue";
        control.SelectedColor = "Blue";

        Assert.Equal(1, raised);
    }

    [Fact]
    internal void SelectedColor_無効な値はエラーになる()
    {
        var control = new ColorSelectorControl { SelectedColor = "NotAColor" };

        Assert.True(control.HasValidationError);
        Assert.Equal(ColorValidation.InvalidColorMessage, control.ValidationErrorMessage);
    }

    [Fact]
    internal void SelectedColor_空文字はエラーにならない()
    {
        var control = new ColorSelectorControl { SelectedColor = "" };

        Assert.False(control.HasValidationError);
        Assert.Null(control.ValidationErrorMessage);
    }

    [Fact]
    internal void ColorSortMode_未定義の値でも例外にならない()
    {
        var control = new ColorSelectorControl { ColorSortMode = ColorSortMode.ByName };

        Assert.Equal(ColorSortMode.ByName, control.ColorSortMode);
    }

    [Fact]
    internal void Log_設定するとログが記録される()
    {
        var logger = new RecordingLogger();
        var control = new ColorSelectorControl { Log = logger, SelectedColor = "Green" };

        Assert.Contains(logger.Messages, message => message.Contains("SelectedColor", StringComparison.Ordinal));
    }

    [Fact]
    internal void LogPrefix_接頭辞を変更するとログの接頭辞が変わる()
    {
        var logger = new RecordingLogger();
        var control = new ColorSelectorControl { Log = logger, LogPrefix = "[MyColor]", SelectedColor = "Green" };

        Assert.Contains(logger.Messages, message => message.StartsWith("[MyColor]", StringComparison.Ordinal));
    }

    [Fact]
    internal void Log_nullを設定しても例外にならない()
    {
        var control = new ColorSelectorControl { Log = null!, SelectedColor = "Green" };

        Assert.Equal("#FF008000", control.SelectedColor);
    }


    [Fact]
    internal void TextFontSize_設定すると入力欄の文字サイズが変わる()
    {
        var control = new ColorSelectorControl { TextFontSize = 18 };

        Assert.Equal(18d, control.TextFontSize);
    }

    [Fact]
    internal void HighlightBorderOnError_無効な入力でも例外にならない()
    {
        var control = new ColorSelectorControl { HighlightBorderOnError = true, SelectedColor = "not-a-color" };

        Assert.True(control.HasValidationError);
    }

    [Fact]
    internal void HighlightBorderOnError_設定した値を保持する()
    {
        var control = new ColorSelectorControl { HighlightBorderOnError = true };

        Assert.True(control.HighlightBorderOnError);
    }

    /// <summary>テーマの色と色名が分類で分かれることを検証する。</summary>
    /// <remarks>
    /// 一覧は上部のタブ（テーマカラー / 色名）で分類を切り替える。 テーマの色は数が多いため、<br/>
    /// 色名と同じ一覧へ混ぜると目的の色を探しにくい。 分類と表示名が同じ出所から決まることも確かめる。
    /// </remarks>
    [Fact]
    internal void 一覧の項目はテーマの色と色名に分かれる()
    {
        var control = new ColorSelectorControl();

        var items = control.CreateItems();

        Assert.All(
            items,
            item => Assert.Equal(
                item.Label.StartsWith("テーマ:", StringComparison.Ordinal),
                item.Category == ColorSelectorCategory.Theme));
        Assert.Contains(items, item => item.Category == ColorSelectorCategory.Named && item.Value == "Red");
    }

    /// <summary>「カスタム」と「ColorPicker」のタブを個別に隠せることを検証する。</summary>
    /// <remarks>色名の入力とテーマ・色名の一覧だけを使う場面では、余分なタブを出さない。</remarks>
    [Fact]
    internal void カスタムとColorPickerのタブは個別に隠せる()
    {
        var control = new ColorSelectorControl
        {
            ShowPickerTab = false,
            CustomTabs = [new ColorSelectorCustomTab("代表色", [new ColorSelectorCustomColor("赤", "#FFDC2626")])],
        };

        Assert.DoesNotContain(control.CreateTabs(), tab => tab.IsPicker);
        Assert.Contains(control.CreateTabs(), tab => tab.Title == "代表色");

        var withoutCustom = new ColorSelectorControl
        {
            ShowCustomTab = false,
            CustomTabs = [new ColorSelectorCustomTab("代表色", [new ColorSelectorCustomColor("赤", "#FFDC2626")])],
        };

        Assert.DoesNotContain(withoutCustom.CreateTabs(), tab => tab.Title == "代表色");
    }

    /// <summary>入力欄が標準のコンボボックスと文字入力の 2 つで成ることを検証する。</summary>
    /// <remarks>
    /// MewUI 0.22.1 の ComboBox は編集できる入力欄を持たないため、枠と展開ボタンはコンボボックスが担い、
    /// 文字の入力と表示はその上へ重ねたテキスト入力が担う（AvaloniaUI 版の <c>IsEditable</c> の代替）。
    /// </remarks>
    [Fact]
    internal void 構造_入力欄は標準のコンボボックスと編集用のテキスト入力()
    {
        var control = new ColorSelectorControl();

        // 見た目を似せて作らず、枠と展開ボタンは標準の ComboBox をそのまま使う
        Assert.NotNull(control.Combo);
        Assert.NotNull(control.TextEditor);

        // 文字はテキスト入力が表示する（コンボボックスは項目を持たない）
        Assert.Equal("White", control.TextEditor.Text);
        Assert.Equal(0, control.Combo.ItemsSource.Count);
    }

    /// <summary>テーマの色と色名のタブを隠すと、カスタム タブだけになることを検証する。</summary>
    [Fact]
    internal void 既存のタブは隠せる()
    {
        var control = new ColorSelectorControl
        {
            ShowThemeColorTab = false,
            ShowNamedColorTab = false,
            ShowPickerTab = false,
            CustomTabs =
            [
                new ColorSelectorCustomTab("代表色", [
                    new ColorSelectorCustomColor("赤", "#FFDC2626"),
                    new ColorSelectorCustomColor("青", "#FF2563EB"),
                ]),
            ],
        };

        var only = Assert.Single(control.CreateTabs());

        Assert.Equal("代表色", only.Title);
        Assert.Equal(["#FFDC2626", "#FF2563EB"], only.Items.Select(item => item.Value));
        Assert.All(only.Items, item => Assert.Equal(ColorSelectorCategory.Custom, item.Category));
    }

    /// <summary>カスタム タブを複数追加できることを検証する（題名と色を動的に決められる）。</summary>
    /// <remarks>
    /// <b>テーマの色は並ばない</b>: テーマの色は <c>Application.Run</c> 後にしか解決できないため、
    /// アプリを起動しない本テストでは「色名」だけが既存タブとして残る（タブの作り方は同じである）。
    /// </remarks>
    [Fact]
    internal void カスタムタブは複数追加できる()
    {
        var control = new ColorSelectorControl
        {
            CustomTabs =
            [
                new ColorSelectorCustomTab("枠線", [new ColorSelectorCustomColor("赤", "#FFDC2626")]),
                new ColorSelectorCustomTab("内側", [new ColorSelectorCustomColor("白", "#FFFFFFFF")]),
            ],
        };

        var tabs = control.CreateTabs();

        // 並びは「カスタム → テーマカラー → 色名 → ColorPicker」（利用者が用意した色を先に出す）。
        // テーマの色はアプリ実行後にしか解決できないため、ここではテーマカラーのタブは無い
        Assert.Equal(["枠線", "内側", "色名", "ColorPicker"], tabs.Select(tab => tab.Title));
    }

    /// <summary>中身の無いカスタム タブは表示しないことを検証する。</summary>
    [Fact]
    internal void 空のカスタムタブは表示しない()
    {
        var control = new ColorSelectorControl { CustomTabs = [new ColorSelectorCustomTab("空", [])] };

        Assert.DoesNotContain(control.CreateTabs(), tab => tab.Title == "空");
    }

    /// <summary>タブが 1 つだけなら題名の行を隠せることを検証する。</summary>
    [Fact]
    internal void タブが1つなら題名を隠せる()
    {
        var shown = new ColorSelectorControl
        {
            ShowThemeColorTab = false,
            ShowNamedColorTab = false,
            ShowPickerTab = false,
            CustomTabs = [new ColorSelectorCustomTab("代表色", [new ColorSelectorCustomColor("赤", "#FFDC2626")])],
        };

        shown.RefreshList();
        Assert.True(shown.IsTabStripVisible);

        var hidden = new ColorSelectorControl
        {
            ShowTabTitles = false,
            ShowThemeColorTab = false,
            ShowNamedColorTab = false,
            ShowPickerTab = false,
            CustomTabs = [new ColorSelectorCustomTab("代表色", [new ColorSelectorCustomColor("赤", "#FFDC2626")])],
        };

        hidden.RefreshList();
        Assert.False(hidden.IsTabStripVisible);
    }

    /// <summary>タブが 2 つ以上なら題名の設定に関わらずタブを出すことを検証する。</summary>
    /// <remarks>切り替える相手があるときはタブが要る（隠すと片方の一覧へ到達できない）。</remarks>
    [Fact]
    internal void タブが2つ以上なら題名を隠さない()
    {
        var control = new ColorSelectorControl
        {
            ShowTabTitles = false,
            CustomTabs = [new ColorSelectorCustomTab("代表色", [new ColorSelectorCustomColor("赤", "#FFDC2626")])],
        };

        control.RefreshList();

        Assert.True(control.IsTabStripVisible);
    }

    /// <summary>表示できるタブが 1 つも無ければ一覧を開かないことを検証する。</summary>
    [Fact]
    internal void タブが1つも無ければ一覧を開かない()
    {
        var control = new ColorSelectorControl
        {
            ShowThemeColors = false,
            ShowNamedColorTab = false,
            ShowPickerTab = false,
        };

        control.ToggleList();

        Assert.Empty(control.CreateTabs());
        Assert.False(control.IsListOpen);
    }

    /// <summary>内容が広いときも枠（コントロール）の幅が変わらないことを検証する。</summary>
    /// <remarks>
    /// 一覧を開くたびに枠が広がると、開閉のたびに周りのレイアウトが動く。 一覧だけを内容に合わせて広げる。<br/>
    /// 行の測定は描画のバックエンドを要求するためアプリ実行前は行わない（幅の計算だけを検証する）。
    /// </remarks>
    [Fact]
    internal void 内容が広いときも枠の幅は変わらない()
    {
        var control = new ColorSelectorControl
        {
            Width = 120,
            ShowThemeColorTab = false,
            ShowNamedColorTab = false,
            ShowPickerTab = false,
            CustomTabs =
            [
                new ColorSelectorCustomTab("長い名前のタブ", [
                    new ColorSelectorCustomColor("とても長い色の名前がここに入ります", "#FFDC2626"),
                ]),
            ],
        };
        var minWidth = control.MinWidth;
        var tabs = control.CreateTabs();

        control.RefreshList();

        // 枠が広いときは一覧も枠に揃い、枠が狭いときは内容（ここでは測れないため 0）に揃う
        Assert.Equal(400d, ColorSelectorControl.ResolveFlyoutWidth(400, tabs, double.PositiveInfinity, showTabTitles: false));
        Assert.Equal(120d, ColorSelectorControl.ResolveFlyoutWidth(120, tabs, double.PositiveInfinity, showTabTitles: false));
        Assert.Equal(minWidth, control.MinWidth);
    }

    /// <summary>一覧の幅にタブの行の幅を含めることを検証する。</summary>
    /// <remarks>
    /// タブの行だけが広いと、一覧（とスクロール バー）がポップアップの途中で切れる。<br/>
    /// 枠（入力欄）よりタブの行（2 つで 96 × 2 + 4）が広い場合は、タブの行の幅を一覧の幅にする。
    /// </remarks>
    [Fact]
    internal void 一覧の幅はタブの行の幅を含める()
    {
        var control = new ColorSelectorControl
        {
            Width = 120,
            ShowThemeColorTab = false,
            ShowNamedColorTab = true,
            ShowPickerTab = true,
        };

        var tabs = control.CreateTabs();
        Assert.Equal(2, tabs.Count);

        var width = ColorSelectorControl.ResolveFlyoutWidth(120, tabs, double.PositiveInfinity, showTabTitles: true);

        Assert.Equal((2 * 96) + 4, width);
    }

    /// <summary>一覧の幅が決めたポップアップの幅と等しいことを検証する。</summary>
    /// <remarks>一覧をポップアップの幅いっぱいに置かないと、スクロール バーが右端へ来ない。</remarks>
    [Fact]
    internal void 一覧の幅はポップアップの幅に揃う()
    {
        var control = new ColorSelectorControl
        {
            ShowThemeColorTab = false,
            ShowNamedColorTab = true,
            ShowPickerTab = true,
            CustomTabs = [new ColorSelectorCustomTab("代表色", [new ColorSelectorCustomColor("赤", "#FFDC2626")])],
        };

        control.RefreshList();

        // タブは 3 つ（代表色 / 色名 / ColorPicker）で、枠（入力欄）よりタブの行（96 × 3 + 4 × 2）が広い
        var width = control.ResolveFlyoutWidth(control.CreateTabs());
        Assert.Equal((3 * 96) + (2 * 4), width);

        Grid? slot = null;
        for (var i = 0; i < control.TabBody.Count; i++)
        {
            var candidate = Assert.IsType<Grid>(control.TabBody[i]);
            if (candidate.Count > 0)
            {
                slot = candidate;
                break;
            }
        }

        Assert.NotNull(slot);
        var list = Assert.IsType<ListBox>(slot[0]);
        Assert.Equal(width, list.Width);
        Assert.Equal(width, control.TabBody.Width);
    }

    /// <summary>タブが多いときは幅に収まるようタブの幅を詰めることを検証する。</summary>
    /// <remarks>
    /// タブの幅を 96 のまま並べると、数が多いときにポップアップの右端が切れる（AvaloniaUI 版の）。<br/>
    /// 幅は上限（ポップアップに使える幅）で止めるため、タブの幅を詰めて収める（本値は見積もりであり、
    /// 実際の幅はタブの行の等分した列が決める）。
    /// </remarks>
    [Fact]
    internal void タブの幅は数が多いほど詰める()
    {
        // 収まるなら既定の幅のまま
        Assert.Equal(96d, ColorSelectorControl.ResolveTabWidth((4 * 96) + (3 * 4), 4));

        // 収まらないときは詰める（(300 - 3 × 4) / 4 = 72）
        Assert.Equal(72d, ColorSelectorControl.ResolveTabWidth(300, 4));

        // 詰めても下限（56）を下回らない
        Assert.Equal(56d, ColorSelectorControl.ResolveTabWidth(100, 8));
    }

    /// <summary>タブが増えても横スクロールが出ないよう、タブの行を均等な列へ並べることを検証する。</summary>
    /// <remarks>
    /// 幅を固定したタブを並べると、端数や余白の分だけ行が広がってポップアップに横スクロールが出る。
    /// タブは**幅を持たせず**列に幅を決めさせ、列は等分にする。<br/>
    /// 文言の実寸が列より広くなり得る（MewUI の <see cref="TextBlock"/> は折り返さない文字を実寸で測る）ため、
    /// 列の上限は 1 つ分の幅にして行が広がらないようにする。
    /// </remarks>
    [Fact]
    internal void タブの行は均等な列へ並べる()
    {
        var control = new ColorSelectorControl
        {
            ShowThemeColorTab = false,
            ShowNamedColorTab = true,
            ShowPickerTab = true,
            CustomTabs =
            [
                new ColorSelectorCustomTab("代表色パレット", [new ColorSelectorCustomColor("赤", "#FFDC2626")], 8),
                new ColorSelectorCustomTab("代表色", [new ColorSelectorCustomColor("青", "#FF2563EB")]),
            ],
        };

        control.RefreshList();

        // 標準の 2 つ（色名・ColorPicker）+ カスタム 2 つで 4 つ
        Assert.Equal(4, control.Tabs.Count);

        var width = control.ResolveFlyoutWidth(control.CreateTabs());
        var strip = control.TabStrip;

        // タブの行はポップアップの幅に揃え、等分した列へ並べる
        Assert.Equal(width, strip.Width);
        Assert.Equal(4, strip.ColumnDefinitions.Count);
        Assert.All(strip.ColumnDefinitions, column => Assert.Equal(GridLength.Star, column.Width));
        Assert.All(strip.ColumnDefinitions, column => Assert.Equal((width - (3 * 4)) / 4, column.MaxWidth, 3));

        // タブは幅を持たない（幅は列が決める）。 列の位置も並びと一致する
        for (var i = 0; i < strip.Count; i++)
        {
            var button = Assert.IsType<ToggleButton>(strip[i]);
            Assert.True(double.IsNaN(button.Width));
            Assert.Equal(i, Grid.GetColumn(button));
        }

        // 列の上限の合計 + 間隔がポップアップの幅に収まる（横スクロールが出ない）
        var total = strip.ColumnDefinitions.Sum(column => column.MaxWidth) + (3 * 4);
        Assert.True(total <= width + 0.001, $"タブの行 {total} が幅 {width} を超える");
    }

    /// <summary>タブが多いときは最小の幅で並べた行までは広げることを検証する。</summary>
    /// <remarks>
    /// 上限で切ると、タブが多いときに最後のタブが欠ける。<br/>
    /// MewUI のポップアップは中身の幅で測って作業領域へ寄せるため、枠より広くても切れない。
    /// </remarks>
    [Fact]
    internal void タブが多いときは最小の幅で並べた行まで広げる()
    {
        var control = new ColorSelectorControl
        {
            ShowThemeColorTab = false,
            ShowNamedColorTab = true,
            ShowPickerTab = true,
            CustomTabs =
            [
                new ColorSelectorCustomTab("1", [new ColorSelectorCustomColor("赤", "#FFDC2626")]),
                new ColorSelectorCustomTab("2", [new ColorSelectorCustomColor("青", "#FF2563EB")]),
                new ColorSelectorCustomTab("3", [new ColorSelectorCustomColor("緑", "#FF16A34A")]),
                new ColorSelectorCustomTab("4", [new ColorSelectorCustomColor("黄", "#FFEAB308")]),
                new ColorSelectorCustomTab("5", [new ColorSelectorCustomColor("紫", "#FF7C3AED")]),
                new ColorSelectorCustomTab("6", [new ColorSelectorCustomColor("桃", "#FFDB2777")]),
            ],
        };

        var tabs = control.CreateTabs();
        Assert.Equal(8, tabs.Count);

        // 使える幅（300）ではタブが収まらないため、最小の幅（56）で並べた行（8 × 56 + 7 × 4 = 476）まで広げる
        var width = ColorSelectorControl.ResolveFlyoutWidth(0, tabs, maxWidth: 300, showTabTitles: true);

        Assert.Equal((8 * 56) + (7 * 4), width);
        Assert.Equal(56d, ColorSelectorControl.ResolveTabWidth(width, tabs.Count));
    }

    /// <summary>タブが多いときはポップアップに使える幅を超えないことを検証する。</summary>
    /// <remarks>上限を超えるとポップアップが右へはみ出して切れる（MewUI は配置できる領域の幅で測る）。</remarks>
    [Fact]
    internal void 一覧の幅はポップアップに使える幅を超えない()
    {
        var control = new ColorSelectorControl
        {
            ShowThemeColorTab = false,
            ShowNamedColorTab = true,
            ShowPickerTab = true,
            CustomTabs = [new ColorSelectorCustomTab("代表色", [new ColorSelectorCustomColor("赤", "#FFDC2626")])],
        };

        var tabs = control.CreateTabs();
        Assert.Equal(3, tabs.Count);

        // タブの行は 96 × 3 + 4 × 2 = 296 だが、使える幅（240）で止める
        var width = ColorSelectorControl.ResolveFlyoutWidth(0, tabs, maxWidth: 240, showTabTitles: true);

        Assert.Equal(240d, width);
        Assert.True(ColorSelectorControl.ResolveTabWidth(width, tabs.Count) < 96);
    }

    /// <summary>一覧のタブが分類（色名 / カスタム / ColorPicker）に分かれることを検証する。</summary>
    /// <remarks>
    /// テーマの色は数が多いため、色名と同じ一覧へ混ぜると目的の色を探しにくい。<br/>
    /// アプリ実行前はテーマの色を解決できないため、ここでは色名・カスタム・ColorPicker の並びを確かめる。
    /// </remarks>
    [Fact]
    internal void 一覧は色名とColorPickerのタブに分かれる()
    {
        var control = new ColorSelectorControl
        {
            CustomTabs = [new ColorSelectorCustomTab("代表色", [new ColorSelectorCustomColor("赤", "#FFDC2626")])],
        };

        control.RefreshList();

        Assert.Equal(["代表色", "色名", "ColorPicker"], control.Tabs.Select(tab => tab.Title));
        Assert.Contains(control.Tabs, tab => tab.IsPicker && tab.Items.Count == 0);
        Assert.All(
            control.Tabs.Where(tab => tab.Title == "色名").SelectMany(tab => tab.Items),
            item => Assert.Equal(ColorSelectorCategory.Named, item.Category));
    }

    /// <summary>■ の列数を指定できることを検証する（0 は一覧、1 以上はその列数の格子）。</summary>
    /// <remarks>40 色を 8 列に並べると 5 行に収まり、スクロールせずに選べる。</remarks>
    [Fact]
    internal void カスタムタブの列数を指定できる()
    {
        var colors = new List<ColorSelectorCustomColor>();
        for (var i = 0; i < 40; i++)
        {
            colors.Add(new ColorSelectorCustomColor(
                "色" + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "#FF0000" + i.ToString("X2", System.Globalization.CultureInfo.InvariantCulture)));
        }

        var control = new ColorSelectorControl
        {
            ShowThemeColorTab = false,
            ShowNamedColorTab = false,
            ShowPickerTab = false,
            CustomTabs = [new ColorSelectorCustomTab("代表色パレット", colors, 8)],
        };

        control.RefreshList();

        // 8 列の格子（セルは色の四角の一辺 32 DIP）
        const double CellSize = 32;
        var slot = Assert.IsType<Grid>(control.TabBody[0]);
        var grid = Assert.IsType<UniformGrid>(slot[0]);
        Assert.Equal(8, grid.Columns);
        Assert.Equal(40, grid.Count);
        Assert.Equal(8, grid.Width / CellSize);

        // 枠より狭い格子は、指定した幅のまま置く（一覧の幅いっぱいへ引き伸ばさない）
        slot.Measure(new Size(320, double.PositiveInfinity));
        slot.Arrange(new Rect(0, 0, 320, slot.DesiredSize.Height));
        Assert.Equal(8 * CellSize, grid.Bounds.Width);

        // 0 は一覧（色名付き）
        control.CustomTabs = [new ColorSelectorCustomTab("代表色", colors)];
        control.RefreshList();

        var list = Assert.IsType<ListBox>(Assert.IsType<Grid>(control.TabBody[0])[0]);
        Assert.Equal(40, Assert.IsAssignableFrom<IItemsView>(list.ItemsSource).Count);
    }

    /// <summary>タブの中身を、開いたとき（表示するとき）にだけ作ることを検証する。</summary>
    /// <remarks>
    /// 全タブの中身を先に作ると、カラーピッカーのタブを開いていないのにピッカーのログ
    /// （色の選択を開きました）まで出る。 表示するタブの入れ物だけを作り、中身は最初に表示するときに作る。
    /// </remarks>
    [Fact]
    internal void タブの中身は開いたときに作る()
    {
        var logger = new RecordingLogger();
        var control = new ColorSelectorControl
        {
            Log = logger,
            CustomTabs = [new ColorSelectorCustomTab("代表色", [new ColorSelectorCustomColor("赤", "#FFDC2626")])],
        };

        control.RefreshList();

        var pickerIndex = control.Tabs.ToList().FindIndex(tab => tab.IsPicker);
        Assert.True(pickerIndex >= 0);

        // 中身を持つのは表示中のタブだけ（開いていないタブは空のまま）
        for (var i = 0; i < control.TabBody.Count; i++)
        {
            var slot = Assert.IsType<Grid>(control.TabBody[i]);
            Assert.Equal(slot.IsVisible, slot.Count > 0);
        }

        // カラーピッカーのタブは開くまで空（ピッカーの生成もログ出力もしない）
        Assert.Equal(0, Assert.IsType<Grid>(control.TabBody[pickerIndex]).Count);
        Assert.DoesNotContain(logger.Messages, message => message.Contains("色の選択を開きました", StringComparison.Ordinal));

        // 表示したときに初めて中身が作られる
        control.ApplyTab(pickerIndex);
        Assert.Equal(1, Assert.IsType<Grid>(control.TabBody[pickerIndex]).Count);
    }

    /// <summary>カスタム タブの色名が入力欄に出ることを検証する。</summary>
    /// <remarks>色コードだけを見比べなくても、意図した色かどうかが分かる。 不透明度があるときは <c>+{n}%</c> を足す。</remarks>
    [Fact]
    internal void カスタムの色名は入力欄に出る()
    {
        var control = new ColorSelectorControl
        {
            ShowPickerTab = false,
            ShowThemeColorTab = false,
            ShowNamedColorTab = false,
            CustomTabs = [new ColorSelectorCustomTab("代表色", [new ColorSelectorCustomColor("枠線の赤", "#FFDC2626")])],
            SelectedColor = "#FFDC2626",
        };

        Assert.Equal("枠線の赤", control.TextEditor.Text);

        control.SelectedColor = "#80DC2626";
        Assert.Equal("枠線の赤+50%", control.TextEditor.Text);
    }

    /// <summary>入力欄の左に現在色の四角を出し、文字をその右から始めることを検証する。</summary>
    /// <remarks>
    /// 標準のコンボボックスは文字しか出せないため、四角を重ねて「■ + 色名」に見せる。<br/>
    /// 文字が四角に重ならないよう、テキスト入力の左の余白を空ける（他 UI と同じ 7 + 12 + 5 = 24 DIP）。
    /// </remarks>
    [Fact]
    internal void 入力欄の左に現在色の四角を出し文字を右へ寄せる()
    {
        var control = new ColorSelectorControl { SelectedColor = "#FFDC2626" };

        Assert.True(control.CurrentColorSwatch.IsVisible);
        Assert.Equal(24d, control.TextEditor.Padding.Left);
    }

    /// <summary>現在色の四角が選んだ色で塗られ、無効な入力の間は隠れることを検証する。</summary>
    /// <remarks>無効な入力の間も前の色を出すと、その色が選ばれていると誤解される。</remarks>
    [Fact]
    internal void 現在色の四角は選んだ色で塗る()
    {
        var control = new ColorSelectorControl();
        var swatch = control.CurrentColorSwatch;

        control.SelectedColor = "#FFDC2626";
        Assert.Equal(MewColor.FromArgb(0xFF, 0xDC, 0x26, 0x26), swatch.Background);
        Assert.True(swatch.IsVisible);

        control.SelectedColor = "not-a-color";
        Assert.False(swatch.IsVisible);
    }

    /// <summary>一覧で同じ色を選び直しても <see cref="ColorSelectorControl.ColorApplied"/> が発生することを検証する。</summary>
    /// <remarks>
    /// 文字単位で色が混ざった範囲を一律にそろえる用途では、値の変更（同じ値では通知されない）だけでは足りない。
    /// </remarks>
    [Fact]
    internal void 同じ色を選び直しても適用の通知が来る()
    {
        var control = new ColorSelectorControl
        {
            ShowThemeColorTab = false,
            ShowNamedColorTab = false,
            ShowPickerTab = false,
            CustomTabs = [new ColorSelectorCustomTab("代表色", [new ColorSelectorCustomColor("赤", "#FFDC2626")])],
            SelectedColor = "#FFDC2626",
        };
        control.RefreshList();
        control.ApplyTab(0);

        var list = Assert.IsType<ListBox>(Assert.IsType<Grid>(control.TabBody[0])[0]);
        var applied = 0;
        control.ColorApplied += (_, _) => applied++;

        // 一覧は既に同じ色を選んだ状態である。 選択を外してから選び直しても、色は変わらない
        list.SelectedIndex = -1;
        list.SelectedIndex = 0;

        Assert.Equal(1, applied);
        Assert.Equal("#FFDC2626", control.SelectedColor);
    }
}
