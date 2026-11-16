using EsUtil.Platform.Core.Palette;
using Xunit;

namespace EsUtil.Ui.WinUI3.ColorSelector.Tests;

/// <summary>色選択コントロール（WinUI 3 版）のテスト。</summary>
/// <remarks>
/// WinUI のコントロールは XAML ランタイム（<c>Application</c>）を必要とするため、コントロールを生成せずに
/// 検証できる範囲（色文字列の規則がコアと一致すること・一覧のタブの決め方・一覧の幅の決め方・ログ定義）を対象にする。
/// タブと幅はコントロールが内部で使う純粋関数（<c>internal static</c>）を直接呼ぶ。
/// 入力と表示はサンプル アプリと利用側アプリで確認する。
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
    // 一覧のタブと幅（コントロールを作らずに検証する範囲）
    // ====================================================================

    /// <summary>テーマの色と色名のタブを隠すと、カスタム タブだけになることを検証する。</summary>
    [Fact]
    internal void 既存のタブは隠せる()
    {
        var tabs = ColorSelectorControl.CreateTabs(
            showThemeColorTab: false,
            showNamedColorTab: false,
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

    /// <summary>カスタム タブを複数追加できることを検証する（題名と色を動的に決められる）。</summary>
    [Fact]
    internal void カスタムタブは複数追加できる()
    {
        var tabs = ColorSelectorControl.CreateTabs(
            showThemeColorTab: true,
            showNamedColorTab: true,
            SampleItems(),
            (ColorSelectorCustomTab[])
            [
                new ColorSelectorCustomTab("枠線", (ColorSelectorCustomColor[])[new ColorSelectorCustomColor("赤", "#FFDC2626")]),
                new ColorSelectorCustomTab("内側", (ColorSelectorCustomColor[])[new ColorSelectorCustomColor("白", "#FFFFFFFF")]),
            ]);

        Assert.Equal(4, tabs.Count);
        Assert.Equal((string[])["テーマカラー", "色名", "枠線", "内側"], tabs.Select(tab => tab.Title));
    }

    /// <summary>中身の無いカスタム タブは表示しないことを検証する。</summary>
    /// <remarks>色の値が空文字のものだけのタブも、選んでも色が変わらないため表示しない。</remarks>
    [Fact]
    internal void 空のカスタムタブは表示しない()
    {
        var tabs = ColorSelectorControl.CreateTabs(
            showThemeColorTab: true,
            showNamedColorTab: true,
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
            showThemeColorTab: false,
            showNamedColorTab: false,
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
            showThemeColorTab: true,
            showNamedColorTab: true,
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
            showThemeColorTab: false,
            showNamedColorTab: false,
            SampleItems(),
            customTabs: (ColorSelectorCustomTab[])[]);

        Assert.Empty(tabs);
    }

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

    /// <summary>タブの決定を検証するための項目を作る（テーマの色 1 件 + 色名 2 件）。</summary>
    /// <returns>一覧に並べる項目。</returns>
    private static IReadOnlyList<ColorSelectorItem> SampleItems() =>
        (ColorSelectorItem[])
        [
            new ColorSelectorItem("テーマ: 背景", "#FFFFFFFF", ColorSelectorCategory.Theme, "背景"),
            new ColorSelectorItem("Red", "Red", ColorSelectorCategory.Named),
            new ColorSelectorItem("Blue", "Blue", ColorSelectorCategory.Named),
        ];

    [Fact]
    internal void ログの書式は共通である()
    {
        var logger = new RecordingLogger();
        ColorSelectorLogMessage.Prefix = ColorSelectorLogMessage.DefaultPrefix;

        logger.LogSettingChanged("SelectedColor", "#FFFFFFFF", "#80FF0000");

        Assert.Contains(logger.Messages, message => message.StartsWith("[ColorSelector]", StringComparison.Ordinal));
    }
}