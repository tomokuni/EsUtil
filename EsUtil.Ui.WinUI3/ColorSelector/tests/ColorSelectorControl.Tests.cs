using EsUtil.Platform.Core.Palette;
using Xunit;

namespace EsUtil.Ui.WinUI3.ColorSelector.Tests;

/// <summary>色選択コントロール（WinUI 3 版）のテスト。</summary>
/// <remarks>
/// WinUI のコントロールは XAML ランタイム（<c>Application</c>）を必要とするため、コントロールを生成せずに
/// 検証できる範囲（色文字列の規則がコアと一致すること・一覧のタブの決め方・格子の行数・入力欄の表示・
/// 一覧の幅の決め方・ログ定義）を対象にする。
/// タブ・格子・表示・幅はコントロールが内部で使う純粋関数（<c>internal static</c>）を直接呼ぶ。
/// 実際の入力と表示はサンプル アプリと利用側アプリで確認する。
/// </remarks>
[Collection(ColorSelectorLogPrefix.Name)]
public class ColorSelectorControlTests
{
    [Fact]
    internal void 保存値の形式は色名から作れる()
        => Assert.Equal("#FFFF0000", ColorString.ToArgbHex("Red"));

    [Fact]
    internal void 不透明度はアルファへ合成される()
        => Assert.Equal("#80FF0000", ColorString.ToArgbHex("Red+50%"));

    [Fact]
    internal void 並び順の指定はコアの列挙を使う()
    {
        Assert.NotEmpty(ColorEntry.GetSorted(ColorSortMode.ByName));
        Assert.NotEmpty(ColorEntry.GetSorted(ColorSortMode.ByHue));
    }

    [Fact]
    internal void 無効な入力は検証で検出できる()
    {
        Assert.True(ColorValidation.IsValid("Red"));
        Assert.False(ColorValidation.IsValid("NotAColor"));
        Assert.Equal(ColorValidation.InvalidColorMessage, ColorValidation.Determine("NotAColor", false).Message);
    }

    // ====================================================================
    // 一覧のタブ（コントロールを作らずに検証する範囲）
    // ====================================================================

    /// <summary>一覧がテーマカラーと色名のタブに分かれることを検証する。</summary>
    /// <remarks>
    /// テーマの色は数が多いため、色名と同じ一覧へ混ぜると目的の色を探しにくい。<br/>
    /// タブで分類を切り替え、一覧はその分類の項目だけを持つ。
    /// </remarks>
    [Fact]
    internal void 一覧はテーマカラーと色名のタブに分かれる()
    {
        var tabs = ColorSelectorControl.CreateTabs(
            showCustomTab: false,
            showThemeColorTab: true,
            showNamedColorTab: true,
            showPickerTab: false,
            SampleItems(),
            customTabs: (ColorSelectorCustomTab[])[]);

        Assert.Equal((string[])["テーマカラー", "色名"], tabs.Select(tab => tab.Title));
        Assert.All(tabs, tab => Assert.Equal(ColorSelectorTabLayout.List, tab.Layout));
        Assert.All(tabs[0].Items, item => Assert.Equal(ColorSelectorCategory.Theme, item.Category));
        Assert.All(tabs[1].Items, item => Assert.Equal(ColorSelectorCategory.Named, item.Category));
        Assert.Contains(tabs[1].Items, item => item.Value == "Red");
    }

    /// <summary>タブの並びが「カスタム → テーマカラー → 色名 → ColorPicker」であることを検証する。</summary>
    /// <remarks>利用者が用意した色を先に出す（よく使う色へ素早く届かせる）。</remarks>
    [Fact]
    internal void タブの並びはカスタムが先()
    {
        var tabs = ColorSelectorControl.CreateTabs(
            showCustomTab: true,
            showThemeColorTab: true,
            showNamedColorTab: true,
            showPickerTab: true,
            SampleItems(),
            (ColorSelectorCustomTab[])
            [
                new ColorSelectorCustomTab("枠線", (ColorSelectorCustomColor[])[new ColorSelectorCustomColor("赤", "#FFDC2626")]),
                new ColorSelectorCustomTab("内側", (ColorSelectorCustomColor[])[new ColorSelectorCustomColor("白", "#FFFFFFFF")]),
            ]);

        Assert.Equal((string[])["枠線", "内側", "テーマカラー", "色名", "ColorPicker"], tabs.Select(tab => tab.Title));
        Assert.True(tabs[^1].IsPicker);
        Assert.Empty(tabs[^1].Items);
    }

    /// <summary>「カスタム」と「ColorPicker」のタブを個別に隠せることを検証する。</summary>
    /// <remarks>色名の入力とテーマ・色名の一覧だけを使う場面では、余分なタブを出さない。</remarks>
    [Fact]
    internal void カスタムとColorPickerのタブは個別に隠せる()
    {
        ColorSelectorCustomTab[] customTabs =
        [
            new ColorSelectorCustomTab("代表色", (ColorSelectorCustomColor[])[new ColorSelectorCustomColor("赤", "#FFDC2626")]),
        ];

        var withoutPicker = ColorSelectorControl.CreateTabs(
            showCustomTab: true,
            showThemeColorTab: false,
            showNamedColorTab: false,
            showPickerTab: false,
            SampleItems(),
            customTabs);

        Assert.DoesNotContain(withoutPicker, tab => tab.IsPicker);
        Assert.Contains(withoutPicker, tab => tab.Title == "代表色");

        var withoutCustom = ColorSelectorControl.CreateTabs(
            showCustomTab: false,
            showThemeColorTab: false,
            showNamedColorTab: false,
            showPickerTab: true,
            SampleItems(),
            customTabs);

        Assert.DoesNotContain(withoutCustom, tab => tab.Title == "代表色");
        Assert.Contains(withoutCustom, tab => tab.IsPicker);
    }

    /// <summary>テーマの色と色名のタブを隠すと、カスタム タブだけになることを検証する。</summary>
    [Fact]
    internal void 既存のタブは隠せる()
    {
        var tabs = ColorSelectorControl.CreateTabs(
            showCustomTab: true,
            showThemeColorTab: false,
            showNamedColorTab: false,
            showPickerTab: false,
            SampleItems(),
            (ColorSelectorCustomTab[])
            [
                new ColorSelectorCustomTab("代表色", (ColorSelectorCustomColor[])
                [
                    new ColorSelectorCustomColor("赤", "#FFDC2626"),
                    new ColorSelectorCustomColor("青", "#FF2563EB"),
                ]),
            ]);

        var only = Assert.Single(tabs);

        Assert.Equal("代表色", only.Title);
        Assert.Equal((string[])["#FFDC2626", "#FF2563EB"], only.Items.Select(item => item.Value));
        Assert.All(only.Items, item => Assert.Equal(ColorSelectorCategory.Custom, item.Category));
    }

    /// <summary>中身の無いカスタム タブは表示しないことを検証する。</summary>
    /// <remarks>色の値が空文字のものだけのタブも、選んでも色が変わらないため表示しない。</remarks>
    [Fact]
    internal void 空のカスタムタブは表示しない()
    {
        var tabs = ColorSelectorControl.CreateTabs(
            showCustomTab: true,
            showThemeColorTab: true,
            showNamedColorTab: true,
            showPickerTab: false,
            SampleItems(),
            (ColorSelectorCustomTab[])
            [
                new ColorSelectorCustomTab("空", (ColorSelectorCustomColor[])[]),
                new ColorSelectorCustomTab("値が空", (ColorSelectorCustomColor[])[new ColorSelectorCustomColor("未設定", " ")]),
            ]);

        Assert.DoesNotContain(tabs, tab => string.Equals(tab.Title, "空", StringComparison.Ordinal));
        Assert.DoesNotContain(tabs, tab => string.Equals(tab.Title, "値が空", StringComparison.Ordinal));
        Assert.Equal(2, tabs.Count);
    }

    /// <summary>タブが 1 つだけなら題名を隠せることを検証する。</summary>
    /// <remarks>切り替える相手が無いため、題名の行を出さずに一覧を狭くできる。</remarks>
    [Fact]
    internal void タブが1つなら題名を隠せる()
    {
        var tabs = ColorSelectorControl.CreateTabs(
            showCustomTab: true,
            showThemeColorTab: false,
            showNamedColorTab: false,
            showPickerTab: false,
            SampleItems(),
            (ColorSelectorCustomTab[])
            [
                new ColorSelectorCustomTab("代表色", (ColorSelectorCustomColor[])[new ColorSelectorCustomColor("赤", "#FFDC2626")]),
            ]);

        Assert.Single(tabs);
        Assert.True(ColorSelectorControl.ShouldShowTabStrip(tabs.Count, showTabTitles: true));
        Assert.False(ColorSelectorControl.ShouldShowTabStrip(tabs.Count, showTabTitles: false));
    }

    /// <summary>タブが 2 つ以上なら題名の設定に関わらずタブを出すことを検証する。</summary>
    /// <remarks>切り替える相手があるときはタブが要る（隠すと片方の一覧へ到達できない）。</remarks>
    [Fact]
    internal void タブが2つ以上なら題名を隠さない()
    {
        var tabs = ColorSelectorControl.CreateTabs(
            showCustomTab: false,
            showThemeColorTab: true,
            showNamedColorTab: true,
            showPickerTab: false,
            SampleItems(),
            customTabs: (ColorSelectorCustomTab[])[]);

        Assert.Equal(2, tabs.Count);
        Assert.True(ColorSelectorControl.ShouldShowTabStrip(tabs.Count, showTabTitles: false));
    }

    /// <summary>表示できるタブが 1 つも無ければ一覧を開かないことを検証する。</summary>
    /// <remarks>タブが無い一覧は開いても選べる色が無いため、開く処理は <c>CreateTabs</c> の結果で止める。</remarks>
    [Fact]
    internal void タブが1つも無ければ一覧を開かない()
    {
        var tabs = ColorSelectorControl.CreateTabs(
            showCustomTab: false,
            showThemeColorTab: false,
            showNamedColorTab: false,
            showPickerTab: false,
            SampleItems(),
            customTabs: (ColorSelectorCustomTab[])[]);

        Assert.Empty(tabs);
    }

    // ====================================================================
    // 格子（色の四角を 6 列）
    // ====================================================================

    /// <summary>カスタム タブの並べ方が指定どおりに保たれることを検証する。</summary>
    /// <remarks>格子を選んだタブだけが <see cref="ColorSelectorTabLayout.Grid"/> になる（一覧は既定のまま）。</remarks>
    [Fact]
    internal void カスタムタブは色の四角を6列に並べられる()
    {
        var tabs = ColorSelectorControl.CreateTabs(
            showCustomTab: true,
            showThemeColorTab: true,
            showNamedColorTab: false,
            showPickerTab: false,
            SampleItems(),
            (ColorSelectorCustomTab[])
            [
                new ColorSelectorCustomTab("代表色", SixteenColors(), ColorSelectorTabLayout.Grid),
                new ColorSelectorCustomTab("枠線", (ColorSelectorCustomColor[])[new ColorSelectorCustomColor("赤", "#FFDC2626")]),
            ]);

        Assert.Equal(ColorSelectorTabLayout.Grid, tabs[0].Layout);
        Assert.Equal(ColorSelectorTabLayout.List, tabs[1].Layout);
        Assert.Equal(16, tabs[0].Items.Count);
    }

    /// <summary>格子が 6 列で折り返し、16 色なら 3 行に収まることを検証する。</summary>
    /// <remarks>スクロールせずに選べることを、必要な行数として確かめる。</remarks>
    /// <param name="itemCount">並べる色の数。</param>
    /// <param name="expected">必要な行数。</param>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(6, 1)]
    [InlineData(7, 2)]
    [InlineData(16, 3)]
    internal void 格子は6列で折り返す(int itemCount, int expected)
        => Assert.Equal(expected, ColorSelectorControl.CalculateSwatchRows(itemCount));

    // ====================================================================
    // 入力欄の外観（現在色の四角と文字の位置）
    // ====================================================================

    /// <summary>入力欄の左に現在色の四角を出し、文字をその右から始めることを検証する。</summary>
    /// <remarks>
    /// 標準のコンボボックスは文字しか出せないため、四角を重ねて「■ + 色名」に見せる。<br/>
    /// 文字が四角に重ならないよう、内側の入力欄の左の余白を空ける（AvaloniaUI 版と同じ 24 DIP）。
    /// </remarks>
    [Fact]
    internal void 入力欄の左に現在色の四角を出し文字を右へ寄せる()
    {
        Assert.True(
            ColorSelectorControl.TryResolveCurrentColorSwatch("#FFDC2626", out _),
            "有効な色なのに四角を出さない");

        // 左の余白 7 + 四角 12 + 間隔 5 = 24 で、文字は四角の右端（19）より後ろから始まる
        Assert.Equal(24.0, ColorSelectorControl.TextLeftIndent);
    }

    /// <summary>現在色の四角が選んだ色で塗られ、無効な入力の間は隠れることを検証する。</summary>
    /// <remarks>無効な入力の間も前の色を出すと、その色が選ばれていると誤解される。</remarks>
    [Fact]
    internal void 現在色の四角は選んだ色で塗る()
    {
        Assert.True(ColorSelectorControl.TryResolveCurrentColorSwatch("#FFDC2626", out var swatchColor));
        Assert.Equal(0xFF, swatchColor.A);
        Assert.Equal(0xDC, swatchColor.R);
        Assert.Equal(0x26, swatchColor.G);
        Assert.Equal(0x26, swatchColor.B);

        Assert.False(ColorSelectorControl.TryResolveCurrentColorSwatch("not-a-color", out _));
    }

    /// <summary>一覧で同じ色を選び直しても <see cref="ColorSelectorControl.ColorApplied"/> が発生することを検証する。</summary>
    /// <remarks>
    /// 文字単位で色が混ざった範囲を一律にそろえる用途では、値の変更（同じ値では通知されない）だけでは足りない。<br/>
    /// XAML ランタイムを持たないテストではコントロールを生成できないため、確定の扱いを決める純粋関数
    /// （<see cref="ColorSelectorControl.PlanItemSelection"/> / <see cref="ColorSelectorControl.PlanPickerSelection"/>）
    /// が「値が変わらなくても通知する」ことを確かめる。 実際の発火はサンプル アプリで確認する。
    /// </remarks>
    [Fact]
    internal void 同じ色を選び直しても適用の通知が来る()
    {
        var item = new ColorSelectorItem("赤", "#FFDC2626", ColorSelectorCategory.Custom);

        var fromList = ColorSelectorControl.PlanItemSelection(item);
        Assert.True(fromList.RaiseApplied, "一覧で同じ色を選び直すと通知が欠ける");
        Assert.Equal("#FFDC2626", fromList.Value);
        Assert.Null(fromList.ThemeKey);

        var fromPicker = ColorSelectorControl.PlanPickerSelection("#FFDC2626");
        Assert.True(fromPicker.RaiseApplied, "カラーピッカーで同じ色を確定すると通知が欠ける");
        Assert.Equal("#FFDC2626", fromPicker.Value);
        Assert.Null(fromPicker.ThemeKey);
    }

    // ====================================================================
    // 入力欄の表示（カスタム タブの色名）
    // ====================================================================

    /// <summary>カスタム タブの色名が入力欄の表示に使われることを検証する。</summary>
    /// <remarks>色コードだけを見比べなくても、意図した色かどうかが分かる。</remarks>
    [Fact]
    internal void カスタムの色名は入力欄に出る()
        => Assert.Equal(
            "枠線の赤",
            ColorSelectorControl.FormatDisplayText("#FFDC2626", ColorTextFormat.NameWithAlpha, CustomTabs()));

    /// <summary>不透明度があるときは色名へ <c>+{n}%</c> を足すことを検証する。</summary>
    /// <remarks>
    /// 不透明度は入力の <c>+{n}%</c> から取る（Avalonia 版と同じ規則）。
    /// 色名だけでは不透明度が伝わらない。
    /// </remarks>
    [Fact]
    internal void カスタムの色名には不透明度を足す()
        => Assert.Equal(
            "枠線の赤+50%",
            ColorSelectorControl.FormatDisplayText("#80DC2626+50%", ColorTextFormat.NameWithAlpha, CustomTabs()));

    /// <summary>カラーコードの表記では色名を使わないことを検証する。</summary>
    /// <remarks>表記の設定（<see cref="ColorTextFormat"/>）が優先される。</remarks>
    [Fact]
    internal void カラーコードの表記では色名を使わない()
        => Assert.Equal(
            "#FFDC2626",
            ColorSelectorControl.FormatDisplayText("#FFDC2626", ColorTextFormat.HexCode, CustomTabs()));

    /// <summary>色名は文字列ではなく色として比べることを検証する。</summary>
    /// <remarks>登録が色名（<c>Red</c>）でも、保存値（<c>#FFFF0000</c>）と同じ色なら一致させる。</remarks>
    [Fact]
    internal void カスタムの色名は色として一致させる()
    {
        ColorSelectorCustomTab[] tabs =
        [
            new ColorSelectorCustomTab("代表色", (ColorSelectorCustomColor[])[new ColorSelectorCustomColor("赤", "Red")]),
        ];

        Assert.Equal("赤", ColorSelectorControl.ResolveCustomName("#FFFF0000", tabs));
        Assert.Null(ColorSelectorControl.ResolveCustomName("#FF0000FF", tabs));
    }

    // ====================================================================
    // 一覧の幅
    // ====================================================================

    /// <summary>一覧の幅が枠（入力欄）の幅と揃うことを検証する。</summary>
    /// <remarks>一覧だけが枠より広い（狭い）と、開いたときの見た目が崩れる。</remarks>
    [Fact]
    internal void 一覧の幅は枠の幅と揃う()
        => Assert.Equal(480, ColorSelectorControl.CalculateListWidth(frameWidth: 480, widest: 10));

    /// <summary>枠より内容が広いときも、決めるのは一覧の幅だけであることを検証する。</summary>
    /// <remarks>
    /// 一覧を開くたびに枠が広がると、開閉のたびに周りのレイアウトが動く。 一覧だけを内容に合わせて広げる
    /// （コントロールの最小幅は変えない）。
    /// </remarks>
    [Fact]
    internal void 内容が広いときも枠の幅は変わらない()
    {
        const double frameWidth = 120;

        var width = ColorSelectorControl.CalculateListWidth(frameWidth, widest: 400);

        Assert.True(width > frameWidth, "一覧が枠より狭い");
        Assert.Equal(frameWidth, ColorSelectorControl.CalculateListWidth(frameWidth, widest: 0));
    }

    /// <summary>ログの書式が共通であることを検証する。</summary>
    [Fact]
    internal void ログの書式は共通である()
    {
        var logger = new RecordingLogger();
        ColorSelectorLogMessage.Prefix = ColorSelectorLogMessage.DefaultPrefix;

        logger.LogSettingChanged("SelectedColor", "#FFFFFFFF", "#80FF0000");

        Assert.Contains(logger.Messages, message => message.StartsWith("[ColorSelector]", StringComparison.Ordinal));
    }

    /// <summary>タブの決定を検証するための項目を作る（テーマの色 1 件 + 色名 2 件）。</summary>
    /// <returns>一覧に並べる項目。</returns>
    private static IReadOnlyList<ColorSelectorItem> SampleItems() =>
        (ColorSelectorItem[])
        [
            new ColorSelectorItem("テーマ: 背景", "#FFFFFFFF", ColorSelectorCategory.Theme, "背景"),
            new ColorSelectorItem("Red", "Red", ColorSelectorCategory.Named),
            new ColorSelectorItem("Blue", "Blue", ColorSelectorCategory.Named),
        ];

    /// <summary>色名の表示を検証するためのカスタム タブを作る。</summary>
    /// <returns>「枠線の赤」だけを持つタブ。</returns>
    private static IReadOnlyList<ColorSelectorCustomTab> CustomTabs() =>
        (ColorSelectorCustomTab[])
        [
            new ColorSelectorCustomTab("枠線", (ColorSelectorCustomColor[])[new ColorSelectorCustomColor("枠線の赤", "#FFDC2626")]),
        ];

    /// <summary>格子の行数を検証するための 16 色を作る。</summary>
    /// <returns>16 色（16 進の下 2 桁を 00 から 0F まで変えた色）。</returns>
    private static ColorSelectorCustomColor[] SixteenColors()
    {
        var colors = new ColorSelectorCustomColor[16];
        for (var i = 0; i < colors.Length; i++)
        {
            colors[i] = new ColorSelectorCustomColor(
                "色" + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "#FF0000" + i.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
        }

        return colors;
    }
}
