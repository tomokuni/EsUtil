using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Reactive;
using EsUtil.Platform.Core.Palette;
using EsUtil.Ui.AvaloniaUI.ColorComboBox.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EsUtil.Ui.AvaloniaUI.ColorComboBox;

/// <summary>色を選ぶコントロール（色名の入力・一覧からの選択・カラーピッカー）。</summary>
/// <remarks>
/// <b>1 つの入力欄に見せる</b>: 外周の 1 つの <c>Border</c> の中へ「スウォッチ（現在色）」「テキスト入力」
/// 「一覧ボタン（▼）」「カラーピッカー ボタン（…）」を隙間なく並べ、区切りは枠より薄い垂直線で表す
/// （区画のボタン自身は枠も背景も持たない）。<br/>
/// <b>規則の所有者はコアである</b>: 受理する入力（色名・16 進・<c>+{n}%</c>）、色名の表、保存値（<c>#AARRGGBB</c>）と
/// 画面表示（<c>{色}+{不透明度%}</c>）、並び順、入力の妥当性は
/// <see cref="EsUtil.Platform.Core.Palette"/>（<see cref="ColorString"/> / <see cref="ColorEntry"/> /
/// <see cref="ColorValidation"/>）が単一所有する。 本クラスは要素の組み立てと、入力と状態の同期だけを行う。<br/>
/// <b>ツールチップ</b>: スウォッチとテキスト部には「色名+不透明度」と「<c>#AARRGGBB</c>」の 2 行を出す。<br/>
/// <b>カラーピッカー</b>は <see cref="ColorComboBoxFlyoutFactory"/> が構築する（OK / キャンセル付き）。<br/>
/// <br/>
/// **公開 API 一覧**:<br/>
/// - コンストラクタ: <see cref="ColorComboBoxControl()"/><br/>
/// - 値: <see cref="SelectedColor"/> / <see cref="ColorSortMode"/> / <see cref="TextFormat"/><br/>
/// - 検証: <see cref="HasValidationError"/> / <see cref="ValidationErrorMessage"/> / <see cref="HighlightBorderOnError"/><br/>
/// - 外観（入力欄のサイズ）: <see cref="TextWidth"/> / <see cref="TextMinWidth"/> / <see cref="TextMaxWidth"/> /<br/>
///   <see cref="TextPadding"/> / <see cref="TextMargin"/><br/>
/// - ログ: <see cref="Log"/><br/>
/// - 関連型: <see cref="ColorSortMode"/>（コア）/ <see cref="ColorTextFormat"/>（コア）/ <see cref="StringToBrushConverter"/> /
///   <see cref="ColorString"/>（コア）/ <see cref="ColorEntry"/>（コア）
/// </remarks>
public sealed partial class ColorComboBoxControl : UserControl
{
    // ====================================================================
    // 依存関係プロパティ（StyledProperty）
    // ====================================================================

    /// <summary><see cref="SelectedColor"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<string> SelectedColorProperty =
        AvaloniaProperty.Register<ColorComboBoxControl, string>(nameof(SelectedColor),
            defaultValue: "White", defaultBindingMode: BindingMode.TwoWay);

    /// <summary><see cref="ColorSortMode"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<ColorSortMode> ColorSortModeProperty =
        AvaloniaProperty.Register<ColorComboBoxControl, ColorSortMode>(nameof(ColorSortMode), defaultValue: ColorSortMode.ByHue);

    /// <summary><see cref="TextFormat"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<ColorTextFormat> TextFormatProperty =
        AvaloniaProperty.Register<ColorComboBoxControl, ColorTextFormat>(
            nameof(TextFormat), defaultValue: ColorTextFormat.NameWithAlpha);

    /// <summary><see cref="HasValidationError"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<bool> HasValidationErrorProperty =
        AvaloniaProperty.Register<ColorComboBoxControl, bool>(nameof(HasValidationError), defaultValue: false);

    /// <summary><see cref="ValidationErrorMessage"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<string?> ValidationErrorMessageProperty =
        AvaloniaProperty.Register<ColorComboBoxControl, string?>(nameof(ValidationErrorMessage));

    /// <summary><see cref="HighlightBorderOnError"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<bool> HighlightBorderOnErrorProperty =
        AvaloniaProperty.Register<ColorComboBoxControl, bool>(nameof(HighlightBorderOnError), defaultValue: false);

    /// <summary><see cref="TextMinWidth"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<double> TextMinWidthProperty =
        AvaloniaProperty.Register<ColorComboBoxControl, double>(nameof(TextMinWidth), defaultValue: 120d);

    /// <summary><see cref="TextPadding"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<Thickness?> TextPaddingProperty =
        AvaloniaProperty.Register<ColorComboBoxControl, Thickness?>(nameof(TextPadding));

    /// <summary><see cref="TextMargin"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<Thickness?> TextMarginProperty =
        AvaloniaProperty.Register<ColorComboBoxControl, Thickness?>(nameof(TextMargin));

    /// <summary><see cref="TextMaxWidth"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<double> TextMaxWidthProperty =
        AvaloniaProperty.Register<ColorComboBoxControl, double>(nameof(TextMaxWidth), defaultValue: double.PositiveInfinity);

    /// <summary><see cref="TextWidth"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<double> TextWidthProperty =
        AvaloniaProperty.Register<ColorComboBoxControl, double>(nameof(TextWidth), defaultValue: double.NaN);

    // ====================================================================
    // 定数
    // ====================================================================

    /// <summary>スウォッチの一辺（ピクセル）。</summary>
    private const double SwatchSize = 16;

    /// <summary>スウォッチと枠・文字との間の余白（ピクセル）。</summary>
    private const double SwatchGap = 6;

    // ====================================================================
    // 状態
    // ====================================================================

    /// <summary>最後に有効だった色（無効な入力の復帰先）。</summary>
    private string? _lastValidColor;

    /// <summary>入力の反映中かどうか（入力と状態の往復でイベントが循環するのを防ぐ）。</summary>
    private bool _applying;

    /// <summary>保存値の正規化中かどうか（正規化の再入を防ぐ）。</summary>
    private bool _normalizing;

    /// <summary>内部ロガー。</summary>
    private ILogger _log = NullLogger.Instance;

    /// <summary>ログの接頭辞。</summary>
    private string? _logPrefix;

    // ====================================================================
    // 値
    // ====================================================================

    /// <summary>現在選択されている色（双方向バインド用）。</summary>
    /// <value>保存値は <c>#AARRGGBB</c>（アルファ先頭16進）形式（例: "#80FF0000"）。<br/>
    /// 色名や <c>+{n}%</c> 形式を設定することもできる（正規化は <see cref="ColorString"/> で行う）。<br/>
    /// 入力欄の表示は <see cref="TextFormat"/> が決める。</value>
    public string SelectedColor
    {
        get => GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    /// <summary>一覧の色の並び順を取得または設定する。</summary>
    /// <value>色名順 (<see cref="ColorSortMode.ByName"/>) または色相順 (<see cref="ColorSortMode.ByHue"/>)。</value>
    public ColorSortMode ColorSortMode
    {
        get => GetValue(ColorSortModeProperty);
        set => SetValue(ColorSortModeProperty, value);
    }

    /// <summary>入力欄の表記を取得または設定する。</summary>
    /// <value>色名 + 不透明度（既定）またはカラーコード。 入力欄は編集できるため、表記と異なる形の入力も受理する。</value>
    public ColorTextFormat TextFormat
    {
        get => GetValue(TextFormatProperty);
        set => SetValue(TextFormatProperty, value);
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
    /// <value>無効な場合の理由。 有効な場合は null。</value>
    public string? ValidationErrorMessage
    {
        get => GetValue(ValidationErrorMessageProperty);
        private set => SetValue(ValidationErrorMessageProperty, value);
    }

    /// <summary>エラー時に枠線を強調するかどうかを取得または設定する。</summary>
    /// <value>true の場合、バリデーションエラー時に外周の枠線が赤くなる。</value>
    public bool HighlightBorderOnError
    {
        get => GetValue(HighlightBorderOnErrorProperty);
        set => SetValue(HighlightBorderOnErrorProperty, value);
    }

    // ====================================================================
    // 外観
    // ====================================================================

    /// <summary>入力欄の幅を取得または設定する。</summary>
    /// <value>幅。 <see cref="double.NaN"/>（既定）の場合は親の幅に従う。</value>
    public double TextWidth
    {
        get => GetValue(TextWidthProperty);
        set => SetValue(TextWidthProperty, value);
    }

    /// <summary>入力欄の最小幅を取得または設定する。</summary>
    /// <value>最小幅。 既定は 120。</value>
    public double TextMinWidth
    {
        get => GetValue(TextMinWidthProperty);
        set => SetValue(TextMinWidthProperty, value);
    }

    /// <summary>入力欄の最大幅を取得または設定する。</summary>
    /// <value>最大幅。 既定は無制限。</value>
    public double TextMaxWidth
    {
        get => GetValue(TextMaxWidthProperty);
        set => SetValue(TextMaxWidthProperty, value);
    }

    /// <summary>入力欄の余白を取得または設定する。</summary>
    /// <value>余白。 null の場合はテーマの既定値を使う。</value>
    public Thickness? TextPadding
    {
        get => GetValue(TextPaddingProperty);
        set => SetValue(TextPaddingProperty, value);
    }

    /// <summary>入力欄の外側の余白を取得または設定する。</summary>
    /// <value>余白。 null の場合はテーマの既定値を使う。</value>
    public Thickness? TextMargin
    {
        get => GetValue(TextMarginProperty);
        set => SetValue(TextMarginProperty, value);
    }

    // ====================================================================
    // ログ
    // ====================================================================

    /// <summary>内部ロガーを取得または設定する。</summary>
    /// <value>ロガー。 null を設定した場合は <see cref="NullLogger"/> を使う。</value>
    public ILogger Log
    {
        get => _log;
        set => _log = value ?? NullLogger.Instance;
    }

    /// <summary>ログの接頭辞を取得または設定する。</summary>
    /// <value>接頭辞。 null の場合は既定値を使う。</value>
    public string? LogPrefix
    {
        get => _logPrefix;
        set
        {
            _logPrefix = value;
            ColorComboBoxLogMessage.Prefix = value ?? ColorComboBoxLogMessage.DefaultPrefix;
        }
    }

    // ====================================================================
    // コンストラクタ・初期化
    // ====================================================================

    /// <summary>コントロールを生成する。</summary>
    public ColorComboBoxControl()
    {
        InitializeComponent();

        // TextChanged はプログラムからの Text 変更では発火しない（実測 2026-10-25）。
        // Text プロパティの変更を直接購読し、入力の経路に依らず同期する
        _text.GetObservable(TextBox.TextProperty).Subscribe(new AnonymousObserver<string?>(OnTextValueChanged));
        _text.LostFocus += OnTextLostFocus;

        if (!Design.IsDesignMode)
        {
            // SelectedColor は 1 回だけ取得して初期値とバリデーションで共有する（二重 getter の排除）
            var initialColor = SelectedColor;
            _lastValidColor = initialColor;
            UpdateTextAndToolTip(initialColor);
            UpdateValidationStatus(initialColor);
        }

        ApplyTextLayout();
    }

    /// <summary><see cref="_text"/>（入力欄）。</summary>
    private TextBox _text => PART_Text;

    /// <summary>外周の枠（検証エラーの強調を付ける対象）。</summary>
    private Border Frame => PART_Frame;

    // ====================================================================
    // プロパティ変更
    // ====================================================================

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SelectedColorProperty)
        {
            // change.NewValue / OldValue は StyledProperty<string> により常に string（分岐なし）
            var color = (string)change.NewValue!;

            // 保存値は #AARRGGBB へ正規化する（色名や #RRGGBB を設定しても保存値の形を保つ）。
            // 正規化した値の通知で残りの処理を行う（同じ処理を 2 回書かない）
            var normalized = ColorString.ToArgbHex(color);
            if (!_normalizing && !string.Equals(normalized, color, StringComparison.Ordinal))
            {
                _normalizing = true;
                try
                {
                    SetCurrentValue(SelectedColorProperty, normalized);
                }
                finally
                {
                    _normalizing = false;
                }

                return;
            }

            Log.LogSelectedColorChanged((string)change.OldValue!, color);

            // 解析は 1 回だけ行い、最終有効値の追跡とバリデーション更新で共有する（二重解析の排除）
            var valid = ColorValidation.IsValid(color);
            if (valid)
            {
                _lastValidColor = color;
            }

            UpdateValidationStatusCore(color, valid);

            // 入力中の文字を打ち消さないよう、入力欄からの反映では書き戻さない
            if (!_applying)
            {
                UpdateTextAndToolTip(color);
            }
        }
        else if (change.Property == TextFormatProperty)
        {
            UpdateTextAndToolTip(SelectedColor);
        }
        else if (change.Property == HighlightBorderOnErrorProperty)
        {
            UpdateBorderValidationVisual();
        }
        else if (change.Property == TextPaddingProperty ||
                 change.Property == TextMarginProperty ||
                 change.Property == TextWidthProperty ||
                 change.Property == TextMinWidthProperty ||
                 change.Property == TextMaxWidthProperty)
        {
            ApplyTextLayout();
        }
    }

    /// <summary>入力欄のサイズ・余白の指定を反映する。</summary>
    /// <remarks>
    /// 左の余白にはスウォッチの幅と両脇の余白を必ず足す（文字がスウォッチへ重ならないようにする）。
    /// 上下の余白はテーマの既定値を使う（0 にすると文字が上端へ寄る）。
    /// </remarks>
    private void ApplyTextLayout()
    {
        var text = _text;
        text.MinWidth = TextMinWidth;
        text.MaxWidth = TextMaxWidth;
        text.Width = TextWidth;

        if (TextMargin is { } margin)
        {
            text.Margin = margin;
        }
        else
        {
            text.ClearValue(MarginProperty);
        }

        var basePadding = TextPadding ?? default;
        text.Padding = new Thickness(
            basePadding.Left + (SwatchGap * 2) + SwatchSize,
            basePadding.Top,
            basePadding.Right,
            basePadding.Bottom);
    }

    // ====================================================================
    // 入力イベント処理・非公開 UI ロジック
    // ====================================================================

    /// <summary>入力欄へ色の表記とツールチップを反映する。</summary>
    /// <param name="color">表示する色（保存値）。</param>
    /// <remarks>ツールチップは 2 行（色名+不透明度 / カラーコード）を出す。</remarks>
    private void UpdateTextAndToolTip(string? color)
    {
        _applying = true;
        try
        {
            var normalized = ColorString.ToArgbHex(color);
            _text.Text = ColorString.ToText(normalized, TextFormat);

            var tooltip = string.Concat(
                ColorString.ToDisplayText(normalized), Environment.NewLine, ColorString.ToArgbHex(normalized));
            ToolTip.SetTip(_text, tooltip);
            ToolTip.SetTip(PART_Swatch, tooltip);
        }
        finally
        {
            _applying = false;
        }
    }

    /// <summary>入力欄の変更を <see cref="SelectedColor"/> へ反映する。</summary>
    /// <param name="text">入力欄の文字列。</param>
    private void OnTextValueChanged(string? text)
    {
        if (_applying)
        {
            return;
        }

        _applying = true;
        try
        {
            SelectedColor = text ?? string.Empty;
        }
        finally
        {
            _applying = false;
        }
    }

    /// <summary>フォーカス喪失時、無効な色を最終有効値に戻す。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    /// <remarks>
    /// 最終有効値が存在し現在の値が無効な場合は復帰する（復帰後の再検証は
    /// <see cref="OnPropertyChanged(AvaloniaPropertyChangedEventArgs)"/> が行う）。<br/>
    /// 解析は 1 回だけ行い、復帰判定・バリデーション更新・ログ判定で共有する。
    /// </remarks>
    private void OnTextLostFocus(object? sender, RoutedEventArgs e)
    {
        var color = SelectedColor;
        var valid = ColorValidation.IsValid(color);
        var hasLastValid = _lastValidColor is not null;

        // 無効な入力を最終有効値へ復帰するかは純粋関数（ColorValidation.ShouldRecover）で判定する
        if (ColorValidation.ShouldRecover(valid, hasLastValid))
        {
            // 無効な入力は最終有効値へ復帰する（再検証は OnPropertyChanged が行う）
            color = _lastValidColor!;
            SelectedColor = color;
            UpdateTextAndToolTip(color);
        }
        else
        {
            UpdateValidationStatusCore(color, valid);
        }

        // ログ用の最終有効性（復帰時は復帰値が有効なため true）
        Log.LogLostFocus(color, valid || hasLastValid);
    }

    /// <summary>一覧のポップアップを開く。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnListButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        var list = new ListBox
        {
            ItemsSource = ColorEntry.GetSorted(ColorSortMode),
            ItemTemplate = (IDataTemplate?)Resources["ColorEntryTemplate"],
            MaxHeight = 360,
        };

        var flyout = new Flyout { Content = list, Placement = PlacementMode.BottomEdgeAlignedLeft };
        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedItem is ColorEntry entry)
            {
                SelectedColor = entry.Name;
                UpdateTextAndToolTip(entry.Name);
                flyout.Hide();
            }
        };

        flyout.ShowAt(button);
    }

    /// <summary>Flyout 形式のカラーピッカーを開いて選択色を反映する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnColorPickerButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        // SelectedColor は 1 回だけ取得してログと Flyout 構築で共有する（二重 getter の排除）
        var color = SelectedColor;
        Log.LogOpenFlyout(color);
        var flyout = ColorComboBoxFlyoutFactory.Create(color, Log, value => SelectedColor = value);
        flyout.ShowAt(button);
    }

    // ====================================================================
    // 検証状態
    // ====================================================================

    /// <summary>バリデーション状態を更新し、エラー時に枠線を強調する。</summary>
    /// <param name="color">検証対象の色。</param>
    /// <remarks>テスト（EsUtil.Ui.AvaloniaUI.ColorComboBox.Tests）が全分岐を検証するため internal 公開する。</remarks>
    internal void UpdateValidationStatus(string color)
        => UpdateValidationStatusCore(color, ColorValidation.IsValid(color));

    /// <summary>バリデーション状態を更新する。</summary>
    /// <param name="color">検証対象の色。</param>
    /// <param name="isValid">検証済みの結果。</param>
    internal void UpdateValidationStatusCore(string color, bool isValid)
    {
        var state = ColorValidation.Determine(color, isValid);
        HasValidationError = state.HasError;
        ValidationErrorMessage = state.Message;
        UpdateBorderValidationVisual();
    }

    /// <summary>検証エラーの強調を枠線へ反映する。</summary>
    private void UpdateBorderValidationVisual()
    {
        Frame.Classes.Set("validation-error", HighlightBorderOnError && HasValidationError);
    }
}