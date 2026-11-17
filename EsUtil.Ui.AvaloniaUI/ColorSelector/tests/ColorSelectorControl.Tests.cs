using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using EsUtil.Platform.Core.Palette;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EsUtil.Ui.AvaloniaUI.ColorSelector.Tests;

/// <summary><see cref="ColorSelectorControl"/> の単体テスト（公開プロパティ・バリデーション・入力の反映）。</summary>
/// <remarks>
/// コントロール生成と入力欄の操作を含むため、Avalonia ヘッドレス実行（<c>[AvaloniaFact]</c>）で直列検証する。<br/>
/// Flyout の構築（<see cref="ColorSelectorFlyoutFactory"/>）は専用のテストファイルで検証する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class ColorSelectorControlTests
{
    /// <summary>一覧の先頭にテーマの色が並ぶことを検証する。</summary>
    /// <remarks>
    /// テーマの色は UI ごとに異なるためコアの色名の表には含めず、一覧を作るたびに UI から解決する。<br/>
    /// 表示名（「テーマ: 背景」等）と適用値（そのときの色の <c>#AARRGGBB</c>）を分けて持つ。<br/>
    /// 並べる順序は UI 層が違っても同じ（背景 → 文字 → 副次テキスト → 枠線 → アクセント → 選択背景）。
    /// </remarks>
    [AvaloniaFact]
    internal void 一覧の先頭にテーマの色が並ぶ()
    {
        var control = CreateControl();

        var items = control.CreateItems();

        Assert.True(items.Count > 3);
        Assert.Contains(items, item => item.Label == "テーマ: 文字" && item.Value.StartsWith('#'));
        Assert.Contains(items, item => item.Label == "テーマ: アクセント" && item.Value.StartsWith('#'));

        // 地色はウィンドウの背景から取るため、ウィンドウを持たないヘッドレスでは並ばない（並ばなくても壊れない）

        // 色名の一覧は表示名と適用値が同じ（テーマの色だけが両者で異なる）
        Assert.Contains(items, item => item.Label == item.Value && item.Value == "Red");
    }

    /// <summary>テーマの色の並び順が決まっていることを検証する。</summary>
    /// <remarks>
    /// 並べる順序は 3 UI で共通である（同じ機能の一覧は UI 層が違っても同じ順に並べる）。<br/>
    /// 解決できない色は並ばないため、並んだ項目だけで順序を確かめる。 ヘッドレス テストはテーマ
    /// （Fluent テーマ）を読み込まないため資源の大半は解決できず、実際にすべての色が並ぶことは
    /// サンプル アプリの実行で確かめる（実測 2026-11-02: 26 色）。
    /// </remarks>
    [AvaloniaFact]
    internal void テーマの色は決まった順に並ぶ()
    {
        var control = CreateControl();

        var labels = control.CreateItems()
            .Select(item => item.Label)
            .Where(label => label.StartsWith("テーマ:", StringComparison.Ordinal))
            .ToArray();

        string[] expected =
        [
            "テーマ: 背景",
            "テーマ: レイヤー",
            "テーマ: カード",
            "テーマ: コントロールの背景",
            "テーマ: コントロールの枠線",
            "テーマ: 区切り線",
            "テーマ: 文字",
            "テーマ: 副次テキスト",
            "テーマ: 無効の文字",
            "テーマ: 無効の背景",
            "テーマ: アクセント",
            "テーマ: アクセント（淡）",
            "テーマ: アクセント（濃）",
            "テーマ: アクセント文字",
            "テーマ: アクセント上の文字",
            "テーマ: 選択の背景",
            "テーマ: ホバーの背景",
            "テーマ: 押下の背景",
            "テーマ: フォーカス",
            "テーマ: エラー",
        ];

        // 3 UI で共通の意味を持つ色は同じ順に並ぶ（解決できない色は飛ばす）
        Assert.Equal(expected.Where(labels.Contains), labels.Take(expected.Length));

    }

    /// <summary>テーマの色を並べない設定にできることを検証する。</summary>
    /// <remarks>テーマの色を必要としない利用者（コアの色名だけを並べたい）のために、並べるかどうかを選べる。</remarks>
    [AvaloniaFact]
    internal void テーマの色を並べない設定にできる()
    {
        var control = CreateControl();
        control.ShowThemeColors = false;

        var items = control.CreateItems();

        Assert.DoesNotContain(items, item => item.Label.StartsWith("テーマ:", StringComparison.Ordinal));
        Assert.Contains(items, item => item.Label == item.Value && item.Value == "Red");
    }

    /// <summary>テーマを読み込んだウィンドウへ置くと、テーマの色がすべて解決されることを検証する。</summary>
    /// <remarks>
    /// ヘッドレスのテスト アプリはテーマを読み込まないため、資源の大半は解決できない（並ばない）。<br/>
    /// 実際のアプリと同じ条件（Fluent テーマを読み込んだウィンドウの下）で、共通の色がすべて並ぶことを確かめる。<br/>
    /// 3 UI で共通の意味を持つ色は同じ名前で並ぶため、この一覧が 3 UI の下限になる。
    /// </remarks>
    [AvaloniaFact]
    internal void テーマを読み込むと共通の色がすべて並ぶ()
    {
        var control = CreateControl();
        var window = new Window { Content = control };
        window.Styles.Add(new FluentTheme());

        var labels = control.CreateItems()
            .Select(item => item.Label)
            .Where(label => label.StartsWith("テーマ:", StringComparison.Ordinal))
            .ToArray();

        // 3 UI で共通の意味を持つ 20 色（ヘッドレスでも地色は解決できないため 19 色以上）
        Assert.True(labels.Length >= 19, $"{labels.Length} 色しか並んでいない: {string.Join(", ", labels)}");
        Assert.All(
            control.CreateItems().Where(item => item.Label.StartsWith("テーマ:", StringComparison.Ordinal)),
            item => Assert.Matches("^#[0-9A-F]{8}$", item.Value));
    }

    /// <summary>一覧がテーマカラーと色名のタブに分かれることを検証する。</summary>
    /// <remarks>
    /// テーマの色は数が多いため、色名と同じ一覧へ混ぜると目的の色を探しにくい。<br/>
    /// タブで分類を切り替え、一覧はその分類の項目だけを持つ。
    /// </remarks>
    [AvaloniaFact]
    internal void 一覧はテーマカラーと色名のタブに分かれる()
    {
        var control = CreateControl();
        control.ShowPickerTab = false;
        var flyout = Assert.IsType<Flyout>(control.CreateListFlyout());

        var (tabs, body) = GetTabsAndBody(flyout);
        Assert.Equal(2, tabs.Children.Count);
        Assert.Equal("テーマカラー", Assert.IsType<ToggleButton>(tabs.Children[0]).Content);
        Assert.Equal("色名", Assert.IsType<ToggleButton>(tabs.Children[1]).Content);

        // 開いた直後はテーマカラー（テーマの色だけが並ぶ）
        var themeItems = Assert.IsAssignableFrom<IEnumerable<ColorSelectorItem>>(GetVisibleView<ListBox>(body).ItemsSource).ToArray();
        Assert.NotEmpty(themeItems);
        Assert.All(themeItems, item => Assert.Equal(ColorSelectorCategory.Theme, item.Category));

        // 色名のタブへ切り替えると色名だけが並ぶ
        Assert.IsType<ToggleButton>(tabs.Children[1]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var namedItems = Assert.IsAssignableFrom<IEnumerable<ColorSelectorItem>>(GetVisibleView<ListBox>(body).ItemsSource).ToArray();
        Assert.NotEmpty(namedItems);
        Assert.All(namedItems, item => Assert.Equal(ColorSelectorCategory.Named, item.Category));
        Assert.Contains(namedItems, item => item.Value == "Red");
    }

    /// <summary>カスタム タブは格子（色の四角を 6 列）でも並べられることを検証する。</summary>
    /// <remarks>16 色なら 6 列 × 3 行に収まり、スクロールせずに選べる。</remarks>
    [AvaloniaFact]
    internal void カスタムタブは色の四角を6列に並べられる()
    {
        var control = CreateControl();
        var colors = new List<ColorSelectorCustomColor>();
        for (var i = 0; i < 16; i++)
        {
            colors.Add(new ColorSelectorCustomColor(
                "色" + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "#FF0000" + i.ToString("X2", System.Globalization.CultureInfo.InvariantCulture)));
        }

        control.CustomTabs = [new ColorSelectorCustomTab("代表色", colors, ColorSelectorTabLayout.Grid)];
        var flyout = Assert.IsType<Flyout>(control.CreateListFlyout());

        var (_, body) = GetTabsAndBody(flyout);
        var grid = Assert.IsType<WrapPanel>(GetVisibleView<WrapPanel>(body));

        Assert.Equal(16, grid.Children.Count);
        Assert.Equal(6, grid.Width / grid.ItemWidth);
    }

    /// <summary>カスタム タブの色名が入力欄（テキスト部）に出ることを検証する。</summary>
    /// <remarks>色コードだけを見比べなくても、意図した色かどうかが分かる。</remarks>
    [AvaloniaFact]
    internal void カスタムの色名は入力欄に出る()
    {
        var control = CreateControl();
        control.ShowPickerTab = false;
        control.ShowThemeColorTab = false;
        control.ShowNamedColorTab = false;
        control.CustomTabs =
        [
            new ColorSelectorCustomTab("代表色", [new ColorSelectorCustomColor("枠線の赤", "#FFDC2626")]),
        ];

        control.SelectedColor = "#FFDC2626";

        Assert.Equal("枠線の赤", control.FindControl<ComboBox>("PART_Combo")!.Text);
    }

    /// <summary>入力欄の左に現在色の四角を出し、文字をその右から始めることを検証する。</summary>
    /// <remarks>
    /// 標準のコンボボックスは文字しか出せないため、四角を重ねて「■ + 色名」に見せる。<br/>
    /// 文字が四角に重ならないよう、内側の入力欄の左の余白を空ける。
    /// </remarks>
    [AvaloniaFact]
    internal void 入力欄の左に現在色の四角を出し文字を右へ寄せる()
    {
        var control = CreateControl();
        control.SelectedColor = "#FFDC2626";

        var swatch = Assert.IsType<Border>(control.FindControl<Border>("PART_CurrentColorSwatch"));
        var combo = control.FindControl<ComboBox>("PART_Combo")!;

        Assert.True(
            swatch.IsVisible,
            $"visible={swatch.IsVisible} bg={(swatch.Background?.ToString() ?? "null")} color={control.SelectedColor}");
        Assert.Equal(24.0, combo.Padding.Left);
    }

    /// <summary>現在色の四角が選んだ色で塗られ、無効な入力の間は隠れることを検証する。</summary>
    /// <remarks>無効な入力の間も前の色を出すと、その色が選ばれていると誤解される。</remarks>
    [AvaloniaFact]
    internal void 現在色の四角は選んだ色で塗る()
    {
        var control = CreateControl();
        var swatch = Assert.IsType<Border>(control.FindControl<Border>("PART_CurrentColorSwatch"));

        control.SelectedColor = "#FFDC2626";
        var brush = Assert.IsType<SolidColorBrush>(swatch.Background);
        Assert.Equal(Color.FromArgb(255, 0xDC, 0x26, 0x26), brush.Color);
        Assert.True(swatch.IsVisible);

        control.SelectedColor = "not-a-color";
        Assert.False(swatch.IsVisible);
    }

    /// <summary>一覧で同じ色を選び直しても <see cref="ColorSelectorControl.ColorApplied"/> が発生することを検証する。</summary>
    /// <remarks>
    /// 文字単位で色が混ざった範囲を一律にそろえる用途では、値の変更（同じ値では通知されない）だけでは足りない。
    /// </remarks>
    [AvaloniaFact]
    internal void 同じ色を選び直しても適用の通知が来る()
    {
        var control = CreateControl();
        control.ShowThemeColorTab = false;
        control.ShowNamedColorTab = false;
        control.ShowPickerTab = false;
        control.CustomTabs =
        [
            new ColorSelectorCustomTab("代表色", [new ColorSelectorCustomColor("赤", "#FFDC2626")]),
        ];
        control.SelectedColor = "#FFDC2626";

        var applied = 0;
        control.ColorApplied += (_, _) => applied++;

        var flyout = Assert.IsType<Flyout>(control.CreateListFlyout());
        var (_, body) = GetTabsAndBody(flyout);
        var list = GetVisibleView<ListBox>(body);
        list.SelectedItem = Assert.Single(Assert.IsAssignableFrom<IEnumerable<ColorSelectorItem>>(list.ItemsSource));

        Assert.Equal(1, applied);
        Assert.Equal("#FFDC2626", control.SelectedColor);
    }

    /// <summary>項目の分類が名前と食い違わないことを検証する。</summary>
    /// <remarks>テーマの色だけが「テーマ:」で始まる（タブの分類と表示名が同じ出所から決まる）。</remarks>
    [AvaloniaFact]
    internal void テーマの色だけがテーマの接頭辞を持つ()
    {
        var control = CreateControl();

        foreach (var item in control.CreateItems())
        {
            var isThemeLabel = item.Label.StartsWith("テーマ:", StringComparison.Ordinal);
            Assert.Equal(isThemeLabel, item.Category == ColorSelectorCategory.Theme);
        }
    }

    /// <summary>一覧は入力欄（コンボボックス）の左端を揃えて開くことを検証する。</summary>
    /// <remarks>右端を揃えると、幅の広い一覧が入力欄より左へ大きくずれて見える。</remarks>
    [AvaloniaFact]
    internal void 一覧は左端を揃えて開く()
    {
        var control = CreateControl();
        var flyout = control.CreateListFlyout();

        Assert.Equal(PlacementMode.BottomEdgeAlignedLeft, Assert.IsType<Flyout>(flyout).Placement);
    }

    /// <summary>テーマの色を選ぶと識別子を保持し、そのときのテーマの色へ解決されることを検証する。</summary>
    /// <remarks>
    /// 選んだ時点の色を保存すると明暗の切替へ追随できない。 識別子を保持し、必要になるたびに解決する。
    /// </remarks>
    [AvaloniaFact]
    internal void テーマの色の識別子はテーマの色へ解決される()
    {
        var control = CreateControl();
        var window = new Window { Content = control };
        window.Styles.Add(new FluentTheme());

        control.SelectedThemeColor = "文字";

        Assert.Equal("文字", control.SelectedThemeColor);
        Assert.True(control.TryResolveThemeColor("文字", out var expected));
        Assert.Equal(expected, control.SelectedColor);
        Assert.Matches("^#[0-9A-F]{8}$", control.SelectedColor);

        // 入力欄は識別子を出す（テーマに追随していることが分かる）
        Assert.Equal("テーマ: 文字", control.FindControl<ComboBox>("PART_Combo")!.Text);
    }

    /// <summary>色を直接設定したらテーマの識別子が外れることを検証する。</summary>
    /// <remarks>テーマの色を選んだあとに色名を選び直したら、テーマの追随をやめる（識別子が残ると上書きされる）。</remarks>
    [AvaloniaFact]
    internal void 色を直接設定するとテーマの識別子が外れる()
    {
        var control = CreateControl();
        control.SelectedThemeColor = "文字";

        control.SelectedColor = "Red";

        Assert.Null(control.SelectedThemeColor);
        Assert.Equal("#FFFF0000", control.SelectedColor);
    }

    /// <summary>テーマの色の識別子から一覧と同じ色が解決されることを検証する。</summary>
    /// <remarks>一覧の表示と解決した色が同じ出所であること（別の表を持つと食い違う）。</remarks>
    [AvaloniaFact]
    internal void テーマの色の識別子は一覧と同じ色に解決される()
    {
        var control = CreateControl();

        foreach (var item in control.CreateItems().Where(item => item.Category == ColorSelectorCategory.Theme))
        {
            Assert.NotNull(item.ThemeKey);
            Assert.True(control.TryResolveThemeColor(item.ThemeKey!, out var hex));
            Assert.Equal(item.Value, hex);
        }
    }

    /// <summary>テーマの色の識別子の一覧が一覧の項目と一致することを検証する。</summary>
    /// <remarks>
    /// 利用側は <see cref="ColorSelectorControl.ThemeColorKeys"/> を設定画面の選択肢や保存値の検証に使う。<br/>
    /// 一覧に並ぶ色と識別子の一覧が食い違うと、保存した識別子が解決できなくなる。
    /// </remarks>
    [AvaloniaFact]
    internal void テーマの色の識別子の一覧は一覧の項目と一致する()
    {
        var control = CreateControl();

        var keys = control.CreateItems()
            .Where(item => item.Category == ColorSelectorCategory.Theme)
            .Select(item => item.ThemeKey)
            .ToArray();

        Assert.NotEmpty(keys);
        Assert.All(keys, key => Assert.Contains(key, ColorSelectorControl.ThemeColorKeys));

        // ヘッドレスではテーマを読み込まないため解決できる色は一部である（一覧 ⊂ 識別子の一覧）。
        // 識別子の一覧に重複が無いことと、代表的な識別子を含むことはここで確かめる
        Assert.Equal(ColorSelectorControl.ThemeColorKeys.Count, ColorSelectorControl.ThemeColorKeys.Distinct().Count());
        Assert.Contains("背景", ColorSelectorControl.ThemeColorKeys);
        Assert.Contains("アクセント", ColorSelectorControl.ThemeColorKeys);
    }

    /// <summary>一覧の幅が枠（入力欄）の幅と揃うことを検証する。</summary>
    /// <remarks>
    /// 一覧だけが枠より広い（狭い）と、開いたときの見た目が崩れる。 幅は「枠の幅」「内容＋余白」「タブ 2 つ」の最大になる。
    /// </remarks>
    [AvaloniaFact]
    internal void 一覧の幅は枠の幅と揃う()
    {
        var control = CreateControl();
        var window = new Window { Width = 480, Height = 200, Content = control };
        window.Show();
        var flyout = Assert.IsType<Flyout>(control.CreateListFlyout());
        var (_, body) = GetTabsAndBody(flyout);
        var list = GetVisibleView<ListBox>(body);

        // 枠（入力欄）より内容が狭いときは、一覧も枠と同じ幅になる
        Assert.Equal(control.Bounds.Width, list.Width);
        window.Close();
    }

    /// <summary>枠より内容が広いときも枠（コントロール）の幅が変わらないことを検証する。</summary>
    /// <remarks>
    /// 一覧を開くたびに枠が広がると、開閉のたびに周りのレイアウトが動く。 一覧だけを内容に合わせて広げる。
    /// </remarks>
    [AvaloniaFact]
    internal void 内容が広いときも枠の幅は変わらない()
    {
        var control = CreateControl();
        control.ShowThemeColorTab = false;
        control.ShowNamedColorTab = false;
        control.CustomTabs =
        [
            new ColorSelectorCustomTab("長い名前のタブ", [
                new ColorSelectorCustomColor("とても長い色の名前がここに入ります", "#FFDC2626"),
            ]),
        ];
        var window = new Window { Width = 120, Height = 200, Content = control };
        window.Show();
        var frameWidth = control.Bounds.Width;
        var minWidth = control.MinWidth;
        var flyout = Assert.IsType<Flyout>(control.CreateListFlyout());
        var (_, body) = GetTabsAndBody(flyout);
        var list = GetVisibleView<ListBox>(body);

        Assert.True(list.Width >= frameWidth, "一覧が枠より狭い");
        Assert.Equal(minWidth, control.MinWidth);
        Assert.Equal(frameWidth, control.Bounds.Width);
        window.Close();
    }

    /// <summary>「カスタム」と「ColorPicker」のタブを個別に隠せることを検証する。</summary>
    /// <remarks>色名の入力とテーマ・色名の一覧だけを使う場面では、余分なタブを出さない。</remarks>
    [AvaloniaFact]
    internal void カスタムとColorPickerのタブは個別に隠せる()
    {
        var control = CreateControl();
        control.CustomTabs = [new ColorSelectorCustomTab("代表色", [new ColorSelectorCustomColor("赤", "#FFDC2626")])];

        control.ShowPickerTab = false;
        Assert.DoesNotContain(control.CreateTabs(), tab => tab.IsPicker);
        Assert.Contains(control.CreateTabs(), tab => tab.Title == "代表色");

        control.ShowCustomTab = false;
        Assert.DoesNotContain(control.CreateTabs(), tab => tab.Title == "代表色");
    }

    /// <summary>テーマの色と色名のタブを隠すと、カスタム タブだけになることを検証する。</summary>
    [AvaloniaFact]
    internal void 既存のタブは隠せる()
    {
        var control = CreateControl();
        control.ShowThemeColorTab = false;
        control.ShowNamedColorTab = false;
        control.ShowPickerTab = false;
        control.CustomTabs =
        [
            new ColorSelectorCustomTab("代表色", [
                new ColorSelectorCustomColor("赤", "#FFDC2626"),
                new ColorSelectorCustomColor("青", "#FF2563EB"),
            ]),
        ];

        var only = Assert.Single(control.CreateTabs());

        Assert.Equal("代表色", only.Title);
        Assert.Equal(["#FFDC2626", "#FF2563EB"], only.Items.Select(item => item.Value));
        Assert.All(only.Items, item => Assert.Equal(ColorSelectorCategory.Custom, item.Category));
    }

    /// <summary>カスタム タブを複数追加できることを検証する（題名と色を動的に決められる）。</summary>
    [AvaloniaFact]
    internal void カスタムタブは複数追加できる()
    {
        var control = CreateControl();
        control.CustomTabs =
        [
            new ColorSelectorCustomTab("枠線", [new ColorSelectorCustomColor("赤", "#FFDC2626")]),
            new ColorSelectorCustomTab("内側", [new ColorSelectorCustomColor("白", "#FFFFFFFF")]),
        ];

        var tabs = control.CreateTabs();

        // 並びは「カスタム → テーマカラー → 色名 → ColorPicker」（利用者が用意した色を先に出す）
        Assert.Equal(5, tabs.Count);
        Assert.Equal(["枠線", "内側", "テーマカラー", "色名", "ColorPicker"], tabs.Select(tab => tab.Title));
    }

    /// <summary>中身の無いカスタム タブは表示しないことを検証する。</summary>
    [AvaloniaFact]
    internal void 空のカスタムタブは表示しない()
    {
        var control = CreateControl();
        control.CustomTabs = [new ColorSelectorCustomTab("空", [])];

        Assert.DoesNotContain(control.CreateTabs(), tab => tab.Title == "空");
    }

    /// <summary>タブが 1 つだけなら題名を隠せることを検証する。</summary>
    [AvaloniaFact]
    internal void タブが1つなら題名を隠せる()
    {
        var control = CreateControl();
        control.ShowThemeColorTab = false;
        control.ShowNamedColorTab = false;
        control.CustomTabs = [new ColorSelectorCustomTab("代表色", [new ColorSelectorCustomColor("赤", "#FFDC2626")])];

        control.ShowPickerTab = false;
        control.ShowTabTitles = true;
        var shown = Assert.IsType<StackPanel>(Assert.IsType<Flyout>(control.CreateListFlyout()).Content);
        Assert.IsType<StackPanel>(shown.Children[0]);

        control.ShowTabTitles = false;
        var hidden = Assert.IsType<StackPanel>(Assert.IsType<Flyout>(control.CreateListFlyout()).Content);
        Assert.IsType<Panel>(hidden.Children[0]);
    }

    /// <summary>タブが 2 つ以上なら題名の設定に関わらずタブを出すことを検証する。</summary>
    /// <remarks>切り替える相手があるときはタブが要る（隠すと片方の一覧へ到達できない）。</remarks>
    [AvaloniaFact]
    internal void タブが2つ以上なら題名を隠さない()
    {
        var control = CreateControl();
        control.ShowTabTitles = false;

        var root = Assert.IsType<StackPanel>(Assert.IsType<Flyout>(control.CreateListFlyout()).Content);

        // 1 段目がタブ、2 段目が中身
        Assert.IsType<StackPanel>(root.Children[0]);
        Assert.IsType<Panel>(root.Children[1]);
    }

    /// <summary>表示できるタブが 1 つも無ければ一覧を開かないことを検証する。</summary>
    [AvaloniaFact]
    internal void タブが1つも無ければ一覧を開かない()
    {
        var control = CreateControl();
        control.ShowThemeColors = false;
        control.ShowNamedColorTab = false;
        control.ShowPickerTab = false;

        Assert.Null(control.CreateListFlyout());
    }

    /// <summary>一覧のタブ ストリップと中身（タブごとの表示を切り替える入れ物）を取り出す。</summary>
    /// <param name="flyout">対象の一覧。</param>
    /// <returns>タブ ストリップと中身。</returns>
    private static (StackPanel Tabs, Panel Body) GetTabsAndBody(Flyout flyout)
    {
        var root = Assert.IsType<StackPanel>(flyout.Content);
        return (Assert.IsType<StackPanel>(root.Children[0]), Assert.IsType<Panel>(root.Children[1]));
    }

    /// <summary>表示中のタブの中身を取り出す。</summary>
    /// <typeparam name="T">中身の型。</typeparam>
    /// <param name="body">タブの中身を入れた入れ物。</param>
    /// <returns>表示中のタブの中身。</returns>
    private static T GetVisibleView<T>(Panel body)
        where T : Control
    {
        // 中身はタブごとの入れ物（Panel）へ遅延作成されるため、表示中の入れ物の中から取り出す
        var slot = Assert.IsType<Panel>(body.Children.First(child => child.IsVisible));
        return Assert.IsType<T>(slot.Children[0]);
    }

    /// <summary>テスト用の <see cref="ColorSelectorControl"/> を生成する。</summary>
    /// <returns>生成したコントロール（入力欄の名前解決が済んでいること）。</returns>
    private static ColorSelectorControl CreateControl()
    {
        var control = new ColorSelectorControl();
        control.ApplyTemplate();
        return control;
    }

    /// <summary>入力欄（<c>PART_Combo</c>。 編集できるコンボボックス）を取り出す。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <returns>入力欄。</returns>
    private static ComboBox TextOf(ColorSelectorControl control)
        => control.FindControl<ComboBox>("PART_Combo")!;

    // ====================================================================
    // ログ（Log プロパティ）
    // ====================================================================

    [AvaloniaFact]
    internal void Log_デフォルトはNullLoggerインスタンス()
    {
        var control = CreateControl();

        Assert.Same(NullLogger.Instance, control.Log);
    }

    [AvaloniaFact]
    internal void Log_設定したロガーを返す()
    {
        var control = CreateControl();
        var logger = NullLogger.Instance;

        control.Log = logger;

        Assert.Same(logger, control.Log);
    }

    [AvaloniaFact]
    internal void Log_nullを設定するとNullLoggerにフォールバックする()
    {
        var control = CreateControl();

        control.Log = null!;

        Assert.Same(NullLogger.Instance, control.Log);
    }

    [AvaloniaFact]
    internal void LogPrefix_nullは既定の接頭辞へ戻る()
    {
        var control = CreateControl();

        control.LogPrefix = "[Test]";
        control.LogPrefix = null;

        Assert.Null(control.LogPrefix);
    }

    // ====================================================================
    // 依存プロパティのデフォルト値
    // ====================================================================

    [AvaloniaFact]
    internal void 依存プロパティのデフォルト値が正しい()
    {
        var control = CreateControl();

        Assert.Equal(ColorSortMode.ByHue, control.ColorSortMode);
        Assert.Equal(ColorTextFormat.NameWithAlpha, control.TextFormat);
        Assert.False(control.HasValidationError);
        Assert.Null(control.ValidationErrorMessage);
        Assert.False(control.HighlightBorderOnError);
        Assert.Equal(double.NaN, control.TextFontSize);
    }

    // ====================================================================
    // 1 つの入力欄としての構造
    // ====================================================================

    [AvaloniaFact]
    internal void 構造_入力欄は編集できるコンボボックス()
    {
        var control = CreateControl();

        var combo = TextOf(control);

        // 見た目を似せて作らず、標準のコンボボックスをそのまま使う
        Assert.NotNull(combo);
        Assert.True(combo.IsEditable);
    }

    [AvaloniaFact]
    internal void 構造_展開ボタン以外のボタンを持たない()
    {
        var control = CreateControl();

        // ▼ / … の自前ボタンは無い（展開はコンボボックスの標準のボタンが担う）
        Assert.Null(control.FindControl<Button>("PART_ListButton"));
        Assert.Null(control.FindControl<Button>("PART_PickerButton"));
    }

    // ====================================================================
    // SelectedColor / バリデーション
    // ====================================================================

    [AvaloniaFact]
    internal void SelectedColor_有効な色を設定するとエラーにならない()
    {
        var control = CreateControl();

        control.SelectedColor = "Red";

        // 入力欄は表記（色名+不透明度）で表示する
        Assert.Equal("Red", TextOf(control).Text);
        Assert.False(control.HasValidationError);
        Assert.Null(control.ValidationErrorMessage);
    }

    [AvaloniaFact]
    internal void SelectedColor_16進コードを設定するとエラーにならない()
    {
        var control = CreateControl();

        control.SelectedColor = "#FF0000";

        Assert.False(control.HasValidationError);
    }

    [AvaloniaFact]
    internal void SelectedColor_無効な色を設定するとエラーになる()
    {
        var control = CreateControl();

        control.SelectedColor = "NotAColor";

        Assert.True(control.HasValidationError);
        Assert.Equal("無効な色コードです。", control.ValidationErrorMessage);
    }

    [AvaloniaFact]
    internal void SelectedColor_空文字を設定するとエラーにならない()
    {
        var control = CreateControl();

        control.SelectedColor = "";

        Assert.False(control.HasValidationError);
        Assert.Null(control.ValidationErrorMessage);
    }

    [AvaloniaFact]
    internal void SelectedColor_無効後_有効な値を設定するとエラーが解除される()
    {
        var control = CreateControl();
        control.SelectedColor = "NotAColor";

        control.SelectedColor = "Blue";

        Assert.False(control.HasValidationError);
        Assert.Null(control.ValidationErrorMessage);
    }

    [AvaloniaFact]
    internal void SelectedColor_nullを設定すると例外にならない()
    {
        var control = CreateControl();

        var exception = Record.Exception(() => control.SelectedColor = null!);

        Assert.Null(exception);
    }

    // ====================================================================
    // 表記（TextFormat）
    // ====================================================================

    [AvaloniaFact]
    internal void TextFormat_色名と不透明度で表示する()
    {
        var control = CreateControl();
        control.SelectedColor = "#80FF0000";

        control.TextFormat = ColorTextFormat.NameWithAlpha;

        Assert.Equal("Red+50%", TextOf(control).Text);
    }

    [AvaloniaFact]
    internal void TextFormat_カラーコードで表示する()
    {
        var control = CreateControl();
        control.SelectedColor = "#80FF0000";

        control.TextFormat = ColorTextFormat.HexCode;

        Assert.Equal("#80FF0000", TextOf(control).Text);
    }

    // ====================================================================
    // ツールチップ
    // ====================================================================

    [AvaloniaFact]
    internal void ツールチップ_色名とカラーコードの2行を出す()
    {
        var control = CreateControl();

        control.SelectedColor = "#80FF0000";

        var text = ToolTip.GetTip(TextOf(control)) as string;
        Assert.NotNull(text);
        var lines = text.Split(Environment.NewLine);
        Assert.Equal("Red+50%", lines[0]);
        Assert.Equal("#80FF0000", lines[1]);
    }

    [AvaloniaFact]
    internal void ツールチップ_入力欄に色名とカラーコードを出す()
    {
        var control = CreateControl();

        control.SelectedColor = "#80FF0000";

        var tip = ToolTip.GetTip(TextOf(control)) as string;
        Assert.NotNull(tip);
        Assert.Contains("#80FF0000", tip, StringComparison.Ordinal);
        Assert.Contains("Red+50%", tip, StringComparison.Ordinal);
    }

    // ====================================================================
    // 入力欄の編集
    // ====================================================================

    [AvaloniaFact]
    internal void 入力_有効な色を入力すると保存値へ反映する()
    {
        var control = CreateControl();
        var text = TextOf(control);

        text.Text = "Lime";

        Assert.Equal("#FF00FF00", control.SelectedColor);
    }

    [AvaloniaFact]
    internal void 入力_無効な色を入力するとエラーになる()
    {
        var control = CreateControl();

        TextOf(control).Text = "NotAColor";

        Assert.True(control.HasValidationError);
    }

    [AvaloniaFact]
    internal void LostFocus_無効な色が最終有効値に戻る()
    {
        var control = CreateControl();
        control.SelectedColor = "Red";
        var text = TextOf(control);
        text.Text = "NotAColor";

        text.RaiseEvent(new FocusChangedEventArgs(InputElement.LostFocusEvent));

        Assert.Equal("#FFFF0000", control.SelectedColor);
        Assert.Equal("Red", text.Text);
        Assert.False(control.HasValidationError);
    }

    [AvaloniaFact]
    internal void LostFocus_有効な色の場合はそのまま維持される()
    {
        var control = CreateControl();
        control.SelectedColor = "Red";
        var text = TextOf(control);
        text.Text = "Blue";

        text.RaiseEvent(new FocusChangedEventArgs(InputElement.LostFocusEvent));

        Assert.Equal("#FF0000FF", control.SelectedColor);
    }

    // ====================================================================
    // バリデーションの強調
    // ====================================================================

    [AvaloniaFact]
    internal void HighlightBorderOnError_エラー時にvalidationErrorクラスが設定される()
    {
        var control = CreateControl();
        control.HighlightBorderOnError = true;

        control.SelectedColor = "NotAColor";

        Assert.Contains(":error", TextOf(control).Classes);
    }

    [AvaloniaFact]
    internal void HighlightBorderOnError_エラーでなければvalidationErrorクラスは設定されない()
    {
        var control = CreateControl();
        control.HighlightBorderOnError = true;

        control.SelectedColor = "Red";

        Assert.DoesNotContain(":error", TextOf(control).Classes);
    }

    [AvaloniaFact]
    internal void HighlightBorderOnError_falseではエラーでもvalidationErrorクラスは設定されない()
    {
        var control = CreateControl();
        control.HighlightBorderOnError = false;

        control.SelectedColor = "NotAColor";

        Assert.DoesNotContain(":error", TextOf(control).Classes);
    }


    [AvaloniaFact]
    internal void TextFontSize変更で入力欄の文字サイズが変わる()
    {
        var control = CreateControl();

        control.TextFontSize = 18;

        Assert.Equal(18d, TextOf(control).FontSize);
    }

    [AvaloniaFact]
    internal void TextFontSize_NaNで親の文字サイズを継承する()
    {
        var control = CreateControl();
        control.TextFontSize = 18;

        control.TextFontSize = double.NaN;

        Assert.False(TextOf(control).IsSet(Avalonia.Controls.Primitives.TemplatedControl.FontSizeProperty));
    }

    // ====================================================================
    // カラーピッカー ボタン
    // ====================================================================

}
