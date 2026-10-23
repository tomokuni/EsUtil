using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Threading;
using EsUtil.Ui.AvaloniaUI.ColorPicker.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EsUtil.Ui.AvaloniaUI.ColorPicker;

/// <summary>色選択コンボボックスとカラーピッカーを統合した再利用可能コントロール。</summary>
/// <remarks>
/// ComboBox（色名/16進入力＋ドロップダウン選択）のテキスト領域左端に
/// 選択中の色を示す色スウォッチ（四角）をオーバーレイ配置する。<br/>
/// ComboBox はデフォルトテーマを維持し、カスタム ControlTemplate は使用しない。<br/>
/// 右側にカラーピッカーダイアログ起動ボタンを配置する。<br/>
/// 内部レイアウトは Grid（*,Auto）で、ComboBox が残りの幅を埋める。<br/>
/// そのため UserControl 自体の <c>MinWidth</c>/<c>Width</c>/<c>MaxWidth</c> で
/// ComboBox＋ボタン＋スペースを含めた全体のサイズを制御できる。<br/>
/// ドロップダウン選択後はテキスト選択状態を自動解除する。<br/>
/// 無効な色が入力された場合は最終有効値に自動復帰する。<br/>
/// <see cref="HighlightBorderOnError"/> が true の場合、バリデーション失敗時に ComboBox の枠線が赤くなる。<br/>
/// ComboBox 単体のサイズ/パディング/マージンは <see cref="ComboBoxMinWidth"/>、
/// <see cref="ComboBoxMaxWidth"/>、<see cref="ComboBoxWidth"/>、
/// <see cref="ComboBoxPadding"/>、<see cref="ComboBoxMargin"/> で外部から制御できる。<br/>
/// <br/>
/// **公開 API 一覧**:<br/>
/// - コンストラクタ: <see cref="ColorPickerControl()"/><br/>
/// - 値: <see cref="SelectedColor"/> / <see cref="ColorSortMode"/><br/>
/// - 検証: <see cref="HasValidationError"/> / <see cref="ValidationErrorMessage"/> / <see cref="HighlightBorderOnError"/><br/>
/// - 外観（ComboBox サイズ）: <see cref="ComboBoxWidth"/> / <see cref="ComboBoxMinWidth"/> / <see cref="ComboBoxMaxWidth"/> /<br/>
///   <see cref="ComboBoxPadding"/> / <see cref="ComboBoxMargin"/><br/>
/// - ログ: <see cref="Log"/><br/>
/// - 関連型: <see cref="ColorSortMode"/> / <see cref="StringToBrushConverter"/> / <see cref="ColorDisplayConverter"/> / <see cref="ColorString"/> / <see cref="ColorEntry"/>（internal）<br/>
/// <br/>
/// **構成**: 本クラスは <c>ColorPicker.axaml</c>（<c>x:Class</c>）と対になるコードビハインドであり、<br/>
/// 依存関係プロパティ（StyledProperty）・公開プロパティ・入力イベント処理・バリデーション状態を保持する。<br/>
/// 関心事ごとに以下のクラスへ委譲し、単一責任と一方向依存を維持する:<br/>
/// - カラーピッカー Flyout 構築: <see cref="ColorPickerFlyoutFactory"/>（Simple Factory パターン）<br/>
/// - 色文字列の妥当性検証: <see cref="ColorValidator"/><br/>
/// - ComboBox テキスト選択の解除: <see cref="TextBoxSelectionHelper"/><br/>
/// - 色文字列の解析・整形・正規化: <see cref="ColorString"/> / <see cref="ColorEntry"/> / 各コンバーター<br/>
/// </remarks>
public sealed partial class ColorPickerControl : UserControl
{
    // ====================================================================
    // 依存関係プロパティ（StyledProperty）
    // ====================================================================

    /// <summary><see cref="SelectedColor"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<string> SelectedColorProperty =
        AvaloniaProperty.Register<ColorPickerControl, string>(nameof(SelectedColor),
            defaultValue: "White", defaultBindingMode: BindingMode.TwoWay);

    /// <summary><see cref="ColorSortMode"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<ColorSortMode> ColorSortModeProperty =
        AvaloniaProperty.Register<ColorPickerControl, ColorSortMode>(nameof(ColorSortMode), defaultValue: ColorSortMode.ByHue);

    /// <summary><see cref="HasValidationError"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<bool> HasValidationErrorProperty =
        AvaloniaProperty.Register<ColorPickerControl, bool>(nameof(HasValidationError), defaultValue: false);

    /// <summary><see cref="ValidationErrorMessage"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<string?> ValidationErrorMessageProperty =
        AvaloniaProperty.Register<ColorPickerControl, string?>(nameof(ValidationErrorMessage));

    /// <summary><see cref="HighlightBorderOnError"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<bool> HighlightBorderOnErrorProperty =
        AvaloniaProperty.Register<ColorPickerControl, bool>(nameof(HighlightBorderOnError), defaultValue: false);

    /// <summary><see cref="ComboBoxMinWidth"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<double> ComboBoxMinWidthProperty =
        AvaloniaProperty.Register<ColorPickerControl, double>(nameof(ComboBoxMinWidth), defaultValue: 120d);

    /// <summary><see cref="ComboBoxPadding"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<Thickness?> ComboBoxPaddingProperty =
        AvaloniaProperty.Register<ColorPickerControl, Thickness?>(nameof(ComboBoxPadding));

    /// <summary><see cref="ComboBoxMargin"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<Thickness?> ComboBoxMarginProperty =
        AvaloniaProperty.Register<ColorPickerControl, Thickness?>(nameof(ComboBoxMargin));

    /// <summary><see cref="ComboBoxMaxWidth"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<double> ComboBoxMaxWidthProperty =
        AvaloniaProperty.Register<ColorPickerControl, double>(nameof(ComboBoxMaxWidth), defaultValue: double.PositiveInfinity);

    /// <summary><see cref="ComboBoxWidth"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<double> ComboBoxWidthProperty =
        AvaloniaProperty.Register<ColorPickerControl, double>(nameof(ComboBoxWidth), defaultValue: double.NaN);

    // ====================================================================
    // 値
    // ====================================================================

    /// <summary>現在選択されている色（双方向バインド用）。</summary>
    /// <value>保存値は <c>#AARRGGBB</c>（アルファ先頭16進）形式（例: "#80FF0000"）。<br/>
    /// 色名や <c>+{n}%</c> 形式を設定することもできる（正規化は <see cref="ColorString"/> で行う）。<br/>
    /// ComboBox の表示テキストは <see cref="ColorDisplayConverter"/> により「色名+不透明度%」形式へ変換される。</value>
    public string SelectedColor
    {
        get => GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    /// <summary>ドロップダウンの色の並び順を取得または設定する。</summary>
    /// <value>色名順 (<see cref="ColorSortMode.ByName"/>) または色相順 (<see cref="ColorSortMode.ByHue"/>)。</value>
    public ColorSortMode ColorSortMode
    {
        get => GetValue(ColorSortModeProperty);
        set => SetValue(ColorSortModeProperty, value);
    }

    // ====================================================================
    // 検証
    // ====================================================================

    /// <summary>バリデーションエラーが有効かどうかを取得する。</summary>
    /// <value>現在の <see cref="SelectedColor"/> が無効な場合 true。</value>
    public bool HasValidationError
    {
        get => GetValue(HasValidationErrorProperty);
        private set => SetValue(HasValidationErrorProperty, value);
    }

    /// <summary>バリデーションエラーメッセージを取得する。</summary>
    /// <value>無効な場合の理由説明。 有効な場合は null。</value>
    public string? ValidationErrorMessage
    {
        get => GetValue(ValidationErrorMessageProperty);
        private set => SetValue(ValidationErrorMessageProperty, value);
    }

    /// <summary>バリデーション失敗時に ComboBox の枠線を赤くするかどうかを取得または設定する。</summary>
    /// <value>true の場合、バリデーション失敗時に ComboBox の BorderBrush が赤色になる。</value>
    public bool HighlightBorderOnError
    {
        get => GetValue(HighlightBorderOnErrorProperty);
        set => SetValue(HighlightBorderOnErrorProperty, value);
    }

    // ====================================================================
    // 外観（ComboBox サイズ・パディング・マージン）
    // ====================================================================

    /// <summary>ComboBox の明示的な幅を取得または設定する。</summary>
    /// <value>デフォルトは NaN（自動）。</value>
    public double ComboBoxWidth
    {
        get => GetValue(ComboBoxWidthProperty);
        set => SetValue(ComboBoxWidthProperty, value);
    }

    /// <summary>ComboBox の最小幅を取得または設定する。</summary>
    /// <value>デフォルトは 120。</value>
    public double ComboBoxMinWidth
    {
        get => GetValue(ComboBoxMinWidthProperty);
        set => SetValue(ComboBoxMinWidthProperty, value);
    }

    /// <summary>ComboBox の最大幅を取得または設定する。</summary>
    /// <value>デフォルトは Infinity（制限なし）。</value>
    public double ComboBoxMaxWidth
    {
        get => GetValue(ComboBoxMaxWidthProperty);
        set => SetValue(ComboBoxMaxWidthProperty, value);
    }

    /// <summary>ComboBox の内部パディングを取得または設定する。</summary>
    /// <value>null の場合はテーマ標準のパディングが適用される。</value>
    public Thickness? ComboBoxPadding
    {
        get => GetValue(ComboBoxPaddingProperty);
        set => SetValue(ComboBoxPaddingProperty, value);
    }

    /// <summary>ComboBox のマージンを取得または設定する。</summary>
    /// <value>null の場合はテーマ標準のマージンが適用される。</value>
    public Thickness? ComboBoxMargin
    {
        get => GetValue(ComboBoxMarginProperty);
        set => SetValue(ComboBoxMarginProperty, value);
    }

    // ====================================================================
    // ログ
    // ====================================================================

    /// <summary>内部ロガー（横断的関心事）。</summary>
    /// <value>null を設定した場合は <see cref="NullLogger"/> へフォールバックする。</value>
    /// <remarks>
    /// C#14 の <c>field</c> キーワードでバッキングフィールドを暗黙定義する。<br/>
    /// getter / setter とも null を <see cref="NullLogger"/> へ安全に変換する。
    /// </remarks>
    public ILogger Log
    {
        get => field ?? NullLogger.Instance;
        set => field = value ?? NullLogger.Instance;
    }

    /// <summary>ログの接頭辞を取得または設定する（ロガーと一緒に設定する）。</summary>
    /// <value>接頭辞（例: <c>[ColorPicker]</c>）。 null または空白を設定すると既定値へ戻す。</value>
    /// <remarks>本アセンブリのログ定義（<see cref="ColorPickerLogMessage"/>）の接頭辞を設定する。</remarks>
    public string? LogPrefix
    {
        get;
        set
        {
            field = value;
            ColorPickerLogMessage.Prefix =
                string.IsNullOrWhiteSpace(value) ? ColorPickerLogMessage.DefaultPrefix : value;
        }
    }

    // ====================================================================
    // 定数
    // ====================================================================

    /// <summary>色スウォッチの幅＋両脇マージン（ピクセル）。 Left Padding に加算される。</summary>
    private const double SwatchPaddingOffset = 20;

    /// <summary>スウォッチの縦位置をテキストと揃えるための微調整（ピクセル）。</summary>
    private const double SwatchTextYAxisOffset = 1;

    // ====================================================================
    // コンストラクタ・初期化
    // ====================================================================

    /// <summary>直前に有効だった色（無効入力の復帰先）。 コンストラクタ・入力処理で使用する。</summary>
    private string? _lastValidColor;

    /// <summary>ColorPickerControl を初期化する。</summary>
    public ColorPickerControl()
    {
        InitializeComponent();

        _comboBox.ItemsSource = ColorEntry.AllByHue;

        if (!Design.IsDesignMode)
        {
            // SelectedColor は 1 回だけ取得して初期値とバリデーションで共有する（二重 getter の排除）
            var initialColor = SelectedColor;
            _lastValidColor = initialColor;
            UpdateValidationStatus(initialColor);
        }

        // テーマテンプレート適用後に Padding を調整
        _comboBox.TemplateApplied += OnComboBoxTemplateApplied;
    }

    // ====================================================================
    // 入力イベント処理・非公開 UI ロジック
    // ====================================================================

    /// <summary>ComboBox テンプレート適用後、テーマ標準 Padding にスウォッチ分を加算する。</summary>
    private void OnComboBoxTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        _comboBox.TemplateApplied -= OnComboBoxTemplateApplied;
        ApplyPaddingWithSwatchOffset();
    }

    /// <summary>ComboBox の Padding にスウォッチ分の左余白を加算して設定する。</summary>
    private void ApplyPaddingWithSwatchOffset()
    {
        var basePadding = ComboBoxPadding ?? _comboBox.Padding;
        _comboBox.Padding = new Thickness(
            basePadding.Left + SwatchPaddingOffset,
            basePadding.Top + SwatchTextYAxisOffset,
            basePadding.Right,
            basePadding.Bottom - SwatchTextYAxisOffset);
    }

    /// <summary>プロパティ変更時に有効な色の追跡とバリデーション状態を更新する。</summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SelectedColorProperty)
        {
            // change.NewValue / OldValue は StyledProperty<string> により常に string（分岐なし）
            var color = (string)change.NewValue!;
            Log.LogSelectedColorChanged((string)change.OldValue!, color);

            // 解析は 1 回だけ行い、最終有効値の追跡とバリデーション更新で共有する（二重解析の排除）
            var valid = ColorValidator.IsValid(color);
            if (valid)
            {
                _lastValidColor = color;
            }
            UpdateValidationStatusCore(color, valid);
        }
        else if (change.Property == HighlightBorderOnErrorProperty)
        {
            UpdateBorderValidationVisual();
        }
        else if (change.Property == ColorSortModeProperty)
        {
            // change.NewValue は StyledProperty&lt;ColorSortMode&gt; により常に ColorSortMode（分岐なし）
            var mode = (ColorSortMode)change.NewValue!;
            _comboBox.ItemsSource = mode == ColorSortMode.ByHue ? ColorEntry.AllByHue : ColorEntry.All;
        }
        else if (change.Property == ComboBoxPaddingProperty)
        {
            ApplyPaddingWithSwatchOffset();
        }
        else if (change.Property == ComboBoxMarginProperty)
        {
            if (change.NewValue is Thickness margin)
                _comboBox.Margin = margin;
            else
                _comboBox.ClearValue(MarginProperty);
        }
    }

    /// <summary>ドロップダウン選択後にテキスト選択状態を解除する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数（未使用）。</param>
    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox) return;

        Dispatcher.UIThread.Post(() => TextBoxSelectionHelper.ResetSelection(comboBox));
    }

    /// <summary>フォーカス喪失時、無効な色を最終有効値に戻す。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数（未使用）。</param>
    /// <remarks>
    /// 最終有効値が存在し現在の値が無効な場合は復帰する（復帰後の再検証は
    /// <see cref="OnPropertyChanged(AvaloniaPropertyChangedEventArgs)"/> が行う）。<br/>
    /// 解析は 1 回だけ行い、復帰判定・バリデーション更新・ログ判定で共有する。
    /// </remarks>
    private void OnLostFocus(object? sender, RoutedEventArgs e)
    {
        var color = SelectedColor;
        var valid = ColorValidator.IsValid(color);
        var hasLastValid = _lastValidColor is not null;

        // 無効な入力を最終有効値へ復帰するかは純粋関数（ColorValidation.ShouldRecover）で判定する
        if (ColorValidation.ShouldRecover(valid, hasLastValid))
        {
            // 無効な入力は最終有効値へ復帰する（再検証は OnPropertyChanged が行う）
            color = _lastValidColor!;
            SelectedColor = color;
        }
        else
        {
            UpdateValidationStatusCore(color, valid);
        }

        // ログ用の最終有効性（復帰時は復帰値が有効なため true）
        Log.LogLostFocus(color, valid || hasLastValid);
    }

    /// <summary>Flyout 形式のカラーピッカーを開いて選択色を反映する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数（未使用）。</param>
    private void OnColorPickerButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;

        // SelectedColor は 1 回だけ取得してログと Flyout 構築で共有する（二重 getter の排除）
        var color = SelectedColor;
        Log.LogOpenFlyout(color);
        var flyout = ColorPickerFlyoutFactory.Create(color, Log, value => SelectedColor = value);
        flyout.ShowAt(button);
    }

    /// <summary>バリデーション状態に応じて ComboBox の枠線を更新する。</summary>
    /// <remarks>
    /// ComboBox の <c>BorderBrush</c> を直接設定する方式では、フォーカス・ホバー時（<c>:pointerover</c>）に
    /// Fluent テーマのテンプレートスタイル（<c>Border#Background</c> への上書き）が優先され、
    /// エラー枠が表示されない。<br/>
    /// そのため <c>validation-error</c> クラスを設定し、コントロールのローカルスタイル
    /// （<c>ColorPicker.axaml</c> の <c>ComboBox.validation-error /template/ Border#Background</c>）で
    /// 枠線色を上書きする（ローカルスタイルはテーマスタイルより優先される）。<br/>
    /// 疑似クラス（<c>PseudoClasses</c>）は protected のため外部から操作できず、<c>Classes</c> で設定する。
    /// </remarks>
    private void UpdateBorderValidationVisual()
    {
        // InitializeComponent 中は名前スコープのフィールド代入前のため _comboBox が null のことがある
        // （TwoWay Text バインディングの初期書き戻しが SelectedColor 変更として届く）。
        // バリデーション状態（HasValidationError / ValidationErrorMessage）は更新済みのため、
        // 枠線のみ構築後にコンストラクタの UpdateValidationStatus が適用する。
        if (_comboBox is null) return;

        _comboBox.Classes.Set("validation-error", HighlightBorderOnError && HasValidationError);
    }

    // ====================================================================
    // 色バリデーション（純粋演算）
    // ====================================================================

    /// <summary>バリデーション状態を更新する。</summary>
    /// <param name="color">検証対象の色文字列。</param>
    /// <remarks>テスト（EsUtil.Ui.AvaloniaUI.ColorPicker.Tests）が全分岐を検証するため internal 公開する。</remarks>
    internal void UpdateValidationStatus(string color)
        => UpdateValidationStatusCore(color, ColorValidator.IsValid(color));

    /// <summary>バリデーション状態を更新する（解析結果を再利用する内部実装）。</summary>
    /// <param name="color">検証対象の色文字列。</param>
    /// <param name="isValid">事前に判定済みの有効性（呼び出し元での二重解析を排除）。</param>
    /// <remarks>
    /// 空文字または有効な色はエラーなし、無効な色はエラーありとして<br/>
    /// <see cref="HasValidationError"/> / <see cref="ValidationErrorMessage"/> を更新し、枠線表示も更新する。<br/>
    /// 判定ロジックは <see cref="ColorValidation.Determine"/>（純粋関数・UI 非依存）へ委譲する。
    /// </remarks>
    private void UpdateValidationStatusCore(string color, bool isValid)
    {
        var state = ColorValidation.Determine(color, isValid);
        HasValidationError = state.hasError;
        ValidationErrorMessage = state.message;
        UpdateBorderValidationVisual();
    }
}

