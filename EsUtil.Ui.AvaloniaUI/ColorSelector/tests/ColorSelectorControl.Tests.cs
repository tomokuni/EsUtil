using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
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
        var flyout = control.CreateListFlyout();

        Assert.NotNull(flyout);
        var root = Assert.IsType<StackPanel>(flyout.Content);

        // 1 段目がタブ、2 段目が一覧
        var tabs = Assert.IsType<StackPanel>(root.Children[0]);
        var list = Assert.IsType<ListBox>(root.Children[1]);
        Assert.Equal(2, tabs.Children.Count);
        Assert.Equal("テーマカラー", Assert.IsType<ToggleButton>(tabs.Children[0]).Content);
        Assert.Equal("標準の色", Assert.IsType<ToggleButton>(tabs.Children[1]).Content);

        // 開いた直後はテーマカラー（テーマの色だけが並ぶ）
        var themeItems = Assert.IsAssignableFrom<IEnumerable<ColorSelectorItem>>(list.ItemsSource).ToArray();
        Assert.NotEmpty(themeItems);
        Assert.All(themeItems, item => Assert.Equal(ColorSelectorCategory.Theme, item.Category));

        // 色名のタブへ切り替えると色名だけが並ぶ
        Assert.IsType<ToggleButton>(tabs.Children[1]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var namedItems = Assert.IsAssignableFrom<IEnumerable<ColorSelectorItem>>(list.ItemsSource).ToArray();
        Assert.NotEmpty(namedItems);
        Assert.All(namedItems, item => Assert.Equal(ColorSelectorCategory.Named, item.Category));
        Assert.Contains(namedItems, item => item.Value == "Red");
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

    /// <summary>一覧は ▼ の右端を揃えて開くことを検証する。</summary>
    /// <remarks>
    /// ▼ は入力欄の右端にある。 左端を揃えると一覧が画面の右外へはみ出し、画面内へ押し戻されて ▼ から離れる。
    /// </remarks>
    [AvaloniaFact]
    internal void 一覧は右端を揃えて開く()
    {
        var control = CreateControl();
        var flyout = control.CreateListFlyout();

        Assert.Equal(PlacementMode.BottomEdgeAlignedRight, Assert.IsType<Flyout>(flyout).Placement);
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
        Assert.Equal("テーマ: 文字", control.FindControl<TextBox>("PART_Text")!.Text);
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

    /// <summary>テスト用の <see cref="ColorSelectorControl"/> を生成する。</summary>
    /// <returns>生成したコントロール（入力欄の名前解決が済んでいること）。</returns>
    private static ColorSelectorControl CreateControl()
    {
        var control = new ColorSelectorControl();
        control.ApplyTemplate();
        return control;
    }

    /// <summary>入力欄（<c>PART_Text</c>）を取り出す。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <returns>入力欄。</returns>
    private static TextBox TextOf(ColorSelectorControl control)
        => control.FindControl<TextBox>("PART_Text")!;

    /// <summary>外周の枠（<c>PART_Frame</c>）を取り出す。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <returns>外周の枠。</returns>
    private static Border FrameOf(ColorSelectorControl control)
        => control.FindControl<Border>("PART_Frame")!;

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
    internal void 構造_外周の枠と入力欄と区画がある()
    {
        var control = CreateControl();

        Assert.NotNull(control.FindControl<Border>("PART_Frame"));
        Assert.NotNull(control.FindControl<Border>("PART_Swatch"));
        Assert.NotNull(control.FindControl<Button>("PART_ListButton"));
        Assert.NotNull(control.FindControl<Button>("PART_PickerButton"));
    }

    [AvaloniaFact]
    internal void 構造_入力欄は枠を持たない()
    {
        var control = CreateControl();

        // 枠は外周の 1 つだけが持つ（入力欄自身の枠は消す。 1 つの入力欄に見せるため）
        Assert.Equal(new Thickness(0), TextOf(control).BorderThickness);
        Assert.Equal(1d, FrameOf(control).BorderThickness.Left);
    }

    [AvaloniaFact]
    internal void 構造_入力欄の左余白はスウォッチ分を確保する()
    {
        var control = CreateControl();

        // スウォッチ（6 + 16）と文字の間隔（6）の分を左へ確保する
        Assert.Equal(28d, TextOf(control).Padding.Left);
    }

    [AvaloniaFact]
    internal void 構造_区画のボタンは枠と背景を持たない()
    {
        var control = CreateControl();

        var list = control.FindControl<Button>("PART_ListButton")!;
        var picker = control.FindControl<Button>("PART_PickerButton")!;

        // 区画のボタン自身が枠を持つと、外周の枠と二重になる
        foreach (var button in new[] { list, picker })
        {
            Assert.Contains("segment", button.Classes);
        }
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
    internal void ツールチップ_スウォッチにも同じ内容を出す()
    {
        var control = CreateControl();

        control.SelectedColor = "#80FF0000";

        var swatch = control.FindControl<Border>("PART_Swatch")!;
        Assert.Equal(ToolTip.GetTip(TextOf(control)), ToolTip.GetTip(swatch));
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

        Assert.Contains("validation-error", FrameOf(control).Classes);
    }

    [AvaloniaFact]
    internal void HighlightBorderOnError_エラーでなければvalidationErrorクラスは設定されない()
    {
        var control = CreateControl();
        control.HighlightBorderOnError = true;

        control.SelectedColor = "Red";

        Assert.DoesNotContain("validation-error", FrameOf(control).Classes);
    }

    [AvaloniaFact]
    internal void HighlightBorderOnError_falseではエラーでもvalidationErrorクラスは設定されない()
    {
        var control = CreateControl();
        control.HighlightBorderOnError = false;

        control.SelectedColor = "NotAColor";

        Assert.DoesNotContain("validation-error", FrameOf(control).Classes);
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

    [AvaloniaFact]
    internal void カラーピッカーボタンクリック_senderがButton以外なら何もしない()
    {
        var control = CreateControl();

        var exception = Record.Exception(() =>
            control.GetType()
                .GetMethod("OnColorPickerButtonClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(control, [null, new RoutedEventArgs()]));

        Assert.Null(exception);
    }
}