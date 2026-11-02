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
}