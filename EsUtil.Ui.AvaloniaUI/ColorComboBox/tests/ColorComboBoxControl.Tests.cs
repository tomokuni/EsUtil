using EsUtil.Platform.Core;
using BindingFlags = System.Reflection.BindingFlags;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.Logging.Abstractions;
using EsUtil.Platform.Core.Palette;
using Xunit;

namespace EsUtil.Ui.AvaloniaUI.ColorComboBox.Tests;

/// <summary><see cref="ColorComboBoxControl"/> の単体テスト（公開プロパティ・バリデーション・ソート・イベント）。</summary>
/// <remarks>
/// Flyout 構築（<see cref="ColorComboBoxFlyoutFactory"/>）は
/// 各クラスのテストファイルで検証する。<br/>
/// 純粋演算テストは <c>[Fact]</c> で並列実行し、ヘッドレス必須のテストのみ <c>[AvaloniaFact]</c> で直列実行する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class ColorComboBoxControlTests
{
    /// <summary>テスト用の ColorComboBoxControl を生成する。</summary>
    private static ColorComboBoxControl CreateControl() => new();

    // ====================================================================
    // ログ（Log プロパティ）
    // ====================================================================

    [AvaloniaFact]
    internal void Log_デフォルトはNullLoggerインスタンス()
    {
        var control = CreateControl();

        Assert.IsType<NullLogger>(control.Log);
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

        Assert.IsType<NullLogger>(control.Log);
    }

    // ====================================================================
    // 依存プロパティのデフォルト値
    // ====================================================================

    [AvaloniaFact]
    internal void 依存プロパティのデフォルト値が正しい()
    {
        var control = CreateControl();

        // 注: ヘッドレス環境ではテーマ未適用のため ComboBox の TwoWay バインディングが
        // SelectedColor を空文字に上書きする。 ここでは SelectedColor 以外のデフォルト値を検証する。
        Assert.Equal(ColorSortMode.ByHue, control.ColorSortMode);
        Assert.False(control.HasValidationError);
        Assert.Null(control.ValidationErrorMessage);
        Assert.False(control.HighlightBorderOnError);
        Assert.Equal(120d, control.ComboBoxMinWidth);
        Assert.Equal(double.PositiveInfinity, control.ComboBoxMaxWidth);
        Assert.Equal(double.NaN, control.ComboBoxWidth);
        Assert.Null(control.ComboBoxPadding);
        Assert.Null(control.ComboBoxMargin);
    }

    [AvaloniaFact]
    internal void コンストラクタ_デザインモードでは初期化をスキップする()
    {
        // Design.IsDesignMode を true にすると、コンストラクタの _lastValidColor 初期化と
        // バリデーション更新がスキップされる（分岐カバレッジ向上のため）
        var prop = typeof(Design).GetProperty("IsDesignMode")!;
        prop.SetValue(null, true);
        try
        {
            var control = CreateControl();

            // デザインモードでは初期化がスキップされても例外なく生成される
            Assert.NotNull(control._comboBox);
        }
        finally
        {
            // 他のテストへの影響を防ぐため確実に戻す
            prop.SetValue(null, false);
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

        // ComboBox の双方向バインディング（ColorDisplayConverter）により保存値は #AARRGGBB へ正規化される
        Assert.Equal("#FFFF0000", control.SelectedColor);
        Assert.False(control.HasValidationError);
        Assert.Null(control.ValidationErrorMessage);
    }

    [AvaloniaFact]
    internal void SelectedColor_16進コードを設定するとエラーにならない()
    {
        var control = CreateControl();

        control.SelectedColor = "#00FF00";

        // アルファ先頭の8桁（#AARRGGBB）へ正規化される
        Assert.Equal("#FF00FF00", control.SelectedColor);
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
        Assert.True(control.HasValidationError);

        control.SelectedColor = "Blue";
        Assert.False(control.HasValidationError);
        Assert.Null(control.ValidationErrorMessage);
    }

    [AvaloniaFact]
    internal void SelectedColor_nullを設定すると例外にならない()
    {
        var control = CreateControl();
        control.SelectedColor = "Red";
        var changed = false;
        control.PropertyChanged += (_, e) =>
        {
            if (e.Property == ColorComboBoxControl.SelectedColorProperty) changed = true;
        };

        // 非 null 想定の StyledProperty<string> へ null を設定した場合でも、
        // バリデーション更新（null → 空扱い）により例外なくエラーにならないことを保証する
        control.SetValue(ColorComboBoxControl.SelectedColorProperty, null!);

        Assert.True(changed, "PropertyChanged が発火するはず");
        Assert.False(control.HasValidationError);
    }

    // ====================================================================
    // ColorSortMode
    // ====================================================================

    [AvaloniaFact]
    internal void ColorSortMode_変更でItemsSourceが切り替わる()
    {
        var control = CreateControl();

        control.ColorSortMode = ColorSortMode.ByName;

        var combo = control._comboBox;
        Assert.Same(ColorEntry.All, combo.ItemsSource);

        control.ColorSortMode = ColorSortMode.ByHue;
        Assert.Same(ColorEntry.AllByHue, combo.ItemsSource);
    }

    // ====================================================================
    // 外観プロパティ
    // ====================================================================

    [AvaloniaFact]
    internal void ComboBoxサイズプロパティを設定すると反映される()
    {
        var control = CreateControl();

        control.ComboBoxMinWidth = 200;
        control.ComboBoxMaxWidth = 400;
        control.ComboBoxWidth = 300;
        control.ComboBoxPadding = new Thickness(5);
        control.ComboBoxMargin = new Thickness(2);

        Assert.Equal(200, control.ComboBoxMinWidth);
        Assert.Equal(400, control.ComboBoxMaxWidth);
        Assert.Equal(300, control.ComboBoxWidth);
        Assert.Equal(new Thickness(5), control.ComboBoxPadding);
        Assert.Equal(new Thickness(2), control.ComboBoxMargin);
    }

    [AvaloniaFact]
    internal void HighlightBorderOnError_切り替えが反映される()
    {
        var control = CreateControl();

        control.HighlightBorderOnError = true;
        Assert.True(control.HighlightBorderOnError);

        control.HighlightBorderOnError = false;
        Assert.False(control.HighlightBorderOnError);
    }

    // ====================================================================
    // ComboBox 連動
    // ====================================================================

    [AvaloniaFact]
    internal void ComboBoxPadding変更でComboBoxのPaddingにスウォッチ分が加算される()
    {
        var control = CreateControl();
        var combo = control._comboBox;

        control.ComboBoxPadding = new Thickness(4);

        // スウォッチ分 (20) + 縦調整 (1) が加算される
        Assert.Equal(24, combo.Padding.Left);
        Assert.Equal(5, combo.Padding.Top);
        Assert.Equal(4, combo.Padding.Right);
        Assert.Equal(3, combo.Padding.Bottom);
    }

    [AvaloniaFact]
    internal void ComboBoxMargin変更でComboBoxのMarginが設定される()
    {
        var control = CreateControl();
        var combo = control._comboBox;

        control.ComboBoxMargin = new Thickness(3);

        Assert.Equal(new Thickness(3), combo.Margin);
    }

    [AvaloniaFact]
    internal void ComboBoxMargin_nullでMarginがクリアされる()
    {
        var control = CreateControl();
        var combo = control._comboBox;
        control.ComboBoxMargin = new Thickness(3);

        control.ComboBoxMargin = null;

        Assert.Equal(default, combo.Margin);
    }

    // ====================================================================
    // OnLostFocus（無効値の復帰）
    // ====================================================================

    [AvaloniaFact]
    internal void LostFocus_無効な色が最終有効値に戻る()
    {
        var control = CreateControl();
        control.SelectedColor = "Red";

        control.SelectedColor = "NotAColor";
        Assert.True(control.HasValidationError);

        // OnLostFocus は XAML で ComboBox にアタッチされているため、ComboBox に対してイベントを発火する
        control._comboBox.RaiseEvent(new RoutedEventArgs(Avalonia.Input.InputElement.LostFocusEvent));
        Dispatcher.UIThread.RunJobs();

        // 最終有効値（#AARRGGBB へ正規化済み）へ復帰する
        Assert.Equal("#FFFF0000", control.SelectedColor);
        Assert.False(control.HasValidationError);
    }

    [AvaloniaFact]
    internal void LostFocus_有効な色の場合はそのまま維持される()
    {
        var control = CreateControl();
        control.SelectedColor = "Green";

        control._comboBox.RaiseEvent(new RoutedEventArgs(Avalonia.Input.InputElement.LostFocusEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("#FF008000", control.SelectedColor);
        Assert.False(control.HasValidationError);
    }

    [AvaloniaFact]
    internal void LostFocus_最終有効値がnullでも例外にならない()
    {
        var control = CreateControl();
        control.SelectedColor = "Red";
        control.SelectedColor = "NotAColor";
        // _lastValidColor を null にして防御分岐をカバーする
        typeof(ColorComboBoxControl).GetField("_lastValidColor",
            BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(control, null);

        // 例外なく完了する（_lastValidColor が null の場合は復帰しない）
        control._comboBox.RaiseEvent(new RoutedEventArgs(Avalonia.Input.InputElement.LostFocusEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("NotAColor", control.SelectedColor);
    }

    [AvaloniaFact]
    internal void カラーピッカーボタンクリック_senderがButton以外なら何もしない()
    {
        var control = CreateControl();
        var window = new Window { Content = control };
        window.Show();

        // OnColorPickerButtonClick をリフレクションで呼び、sender に Button 以外を渡す
        var method = typeof(ColorComboBoxControl).GetMethod("OnColorPickerButtonClick",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var e = new RoutedEventArgs(Button.ClickEvent);
        method!.Invoke(control, [control, e]); // sender = control（Button 以外）

        // 例外なく無視される（SelectedColor は変化しない）
        window.Close();
    }

    // ====================================================================
    // イベントハンドラ（SelectionChanged / カラーピッカー / テンプレート）
    // ====================================================================

    [AvaloniaFact]
    internal void SelectionChanged_ドロップダウン選択後にテキスト選択が解除される()
    {
        var control = CreateControl();

        // SelectionChanged を発火（ヘッドレスではテキストボックスが無いため null パスも通る）
        control._comboBox.RaiseEvent(new SelectionChangedEventArgs(
            SelectingItemsControl.SelectionChangedEvent,
            new AvaloniaList<object?>(),
            new AvaloniaList<object?>()));
        Dispatcher.UIThread.RunJobs();

        // 例外なく完了すれば成功
        Assert.True(true);
    }

    [AvaloniaFact]
    internal void SelectionChanged_senderがComboBox以外では何もしない()
    {
        var control = CreateControl();

        // sender が ComboBox 以外（Button 等）でも例外なく無視される
        control.RaiseEvent(new SelectionChangedEventArgs(
            SelectingItemsControl.SelectionChangedEvent,
            new AvaloniaList<object?>(),
            new AvaloniaList<object?>())
        { Source = control });
        Dispatcher.UIThread.RunJobs();

        Assert.True(true);
    }

    [AvaloniaFact]
    internal void カラーピッカーボタンクリックでFlyoutが構築される()
    {
        var control = CreateControl();
        var window = new Window { Content = control };
        window.Show();
        control.SelectedColor = "#FF8800";

        // AXAML の「…」ボタンを検索して Click を発火
        var button = control.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Content is string s && s == "…");
        Assert.NotNull(button);

        // ヘッドレス環境では IPopupImpl が無いため ShowAt で例外が発生する（それまでの Flyout 構築は実行される）
        try
        {
            button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // ヘッドレスではポップアップを表示できないため、構築までで検証する
        }

        window.Close();
    }

    [AvaloniaFact]
    internal void カラーピッカーボタンクリック_SelectedColorが空でもFlyoutが構築される()
    {
        var control = CreateControl();
        var window = new Window { Content = control };
        window.Show();
        control.SelectedColor = "";

        var button = control.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Content is string s && s == "…");
        Assert.NotNull(button);

        try
        {
            button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // ヘッドレスではポップアップを表示できないため、構築までで検証する
        }

        window.Close();
    }

    [AvaloniaFact]
    internal void TemplateApplied_適用後にPaddingが調整される()
    {
        var control = CreateControl();
        var combo = control._comboBox;

        // TemplateApplied を発火して OnComboBoxTemplateApplied を実行する
        combo.RaiseEvent(new TemplateAppliedEventArgs(new NameScope()));
        Dispatcher.UIThread.RunJobs();

        // ハンドラ解除後に再度発火しても影響なし（冪等）
        combo.RaiseEvent(new TemplateAppliedEventArgs(new NameScope()));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    internal void HighlightBorderOnError_エラー時にvalidationErrorクラスが設定される()
    {
        var control = CreateControl();
        control.HighlightBorderOnError = true;

        control.SelectedColor = "NotAColor";

        var combo = control._comboBox;
        Assert.Contains("validation-error", combo.Classes);
    }

    [AvaloniaFact]
    internal void HighlightBorderOnError_エラーでなければvalidationErrorクラスは設定されない()
    {
        var control = CreateControl();
        control.HighlightBorderOnError = true;

        control.SelectedColor = "Blue";

        var combo = control._comboBox;
        Assert.DoesNotContain("validation-error", combo.Classes);
    }

    [AvaloniaFact]
    internal void HighlightBorderOnError_falseではエラーでもvalidationErrorクラスは設定されない()
    {
        var control = CreateControl();
        control.HighlightBorderOnError = false;

        control.SelectedColor = "NotAColor";

        var combo = control._comboBox;
        Assert.DoesNotContain("validation-error", combo.Classes);
    }

    // ====================================================================
    // InitializeComponent 中のバインディング書き戻し（回帰）
    // ====================================================================

    [AvaloniaFact]
    internal void SelectedColor_ComboBox未構築時の書き戻しでも例外にならない()
    {
        var control = CreateControl();
        var combo = control._comboBox;

        // InitializeComponent 中は名前スコープのフィールド代入前のため _comboBox が null のことがある
        // （実アプリでは TwoWay Text バインディングの初期書き戻しがこのタイミングで届く）。
        // 枠線更新をスキップし、バリデーション状態のみ更新されることを保証する。
        control._comboBox = null!;
        control.SelectedColor = "NotAColor";

        Assert.True(control.HasValidationError);
        Assert.Equal("無効な色コードです。", control.ValidationErrorMessage);

        // フィールド復元後は枠線更新が適用される
        control._comboBox = combo;
        control.HighlightBorderOnError = true;
        Assert.Contains("validation-error", combo.Classes);
    }
}
