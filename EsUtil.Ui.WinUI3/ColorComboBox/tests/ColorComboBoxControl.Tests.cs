using EsUtil.Platform.Core.Palette;
using Xunit;

namespace EsUtil.Ui.WinUI3.ColorComboBox.Tests;

/// <summary>色選択コントロール（WinUI 3 版）のテスト。</summary>
/// <remarks>
/// WinUI のコントロールは XAML ランタイム（<c>Application</c>）を必要とするため、コントロールを生成せずに
/// 検証できる範囲（色文字列の規則がコアと一致すること・ログ定義）を対象にする。
/// 入力と表示はサンプル アプリと利用側アプリで確認する。
/// </remarks>
[Collection(ColorComboBoxLogPrefix.Name)]
public class ColorComboBoxControlTests
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

    [Fact]
    internal void ログの書式は共通である()
    {
        var logger = new RecordingLogger();
        LogMessage.Prefix = LogMessage.DefaultPrefix;

        logger.LogSettingChanged("SelectedColor", "#FFFFFFFF", "#80FF0000");

        Assert.Contains(logger.Messages, message => message.StartsWith("[ColorComboBox]", StringComparison.Ordinal));
    }
}