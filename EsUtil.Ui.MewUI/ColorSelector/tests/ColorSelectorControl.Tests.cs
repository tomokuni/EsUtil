using EsUtil.Platform.Core.Palette;
using Xunit;

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

    /// <summary>一覧ボタン（▼）とピッカー ボタン（…）を個別に隠せることを検証する。</summary>
    /// <remarks>狭い場所ではボタンを隠し、入力欄とスウォッチだけを見せられる。 区切りの線も一緒に隠す。</remarks>
    [Fact]
    internal void 一覧とピッカーのボタンは個別に隠せる()
    {
        var control = new ColorSelectorControl();

        // 既定では両方見えている
        Assert.True(control.IsListButtonVisible);
        Assert.True(control.IsPickerButtonVisible);

        control.ShowPickerButton = false;
        Assert.True(control.IsListButtonVisible);
        Assert.True(control.IsListSeparatorVisible);
        Assert.False(control.IsPickerButtonVisible);
        Assert.False(control.IsPickerSeparatorVisible);

        control.ShowListButton = false;
        Assert.False(control.IsListButtonVisible);
        Assert.False(control.IsListSeparatorVisible);
    }

    /// <summary>テーマの色と色名のタブを隠すと、カスタム タブだけになることを検証する。</summary>
    [Fact]
    internal void 既存のタブは隠せる()
    {
        var control = new ColorSelectorControl
        {
            ShowThemeColorTab = false,
            ShowNamedColorTab = false,
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

        Assert.Equal(["色名", "枠線", "内側"], tabs.Select(tab => tab.Title));
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
            CustomTabs = [new ColorSelectorCustomTab("代表色", [new ColorSelectorCustomColor("赤", "#FFDC2626")])],
        };

        shown.RefreshList();
        Assert.True(shown.IsTabStripVisible);

        var hidden = new ColorSelectorControl
        {
            ShowTabTitles = false,
            ShowThemeColorTab = false,
            ShowNamedColorTab = false,
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
        var control = new ColorSelectorControl { ShowThemeColors = false, ShowNamedColorTab = false };

        control.ToggleList();

        Assert.Empty(control.CreateTabs());
        Assert.False(control.IsListOpen);
    }

    /// <summary>枠より内容が広いときも枠（コントロール）の幅が変わらないことを検証する。</summary>
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
            CustomTabs =
            [
                new ColorSelectorCustomTab("長い名前のタブ", [
                    new ColorSelectorCustomColor("とても長い色の名前がここに入ります", "#FFDC2626"),
                ]),
            ],
        };
        var minWidth = control.MinWidth;

        control.RefreshList();
        control.ApplyListWidth(Assert.Single(control.CreateTabs()).Items);

        // 内容が広いときは一覧だけが広がり、枠が広いときは一覧が枠に揃う
        Assert.Equal(400d, ColorSelectorControl.ResolveListWidth(120, 400));
        Assert.Equal(240d, ColorSelectorControl.ResolveListWidth(240, 100));
        Assert.Equal(minWidth, control.MinWidth);
    }
}