using CommunityToolkit.Mvvm.ComponentModel;
using EsUtil.Platform.Core.Palette;

namespace EsUtil.Ui.AvaloniaUI.ColorComboBox.Sample.ViewModels;

/// <summary>メインウィンドウの ViewModel。</summary>
/// <remarks>
/// ColorComboBoxControl の各種プロパティ（SelectedColor / ColorSortMode / HighlightBorderOnError /
/// TextFormat / TextMinWidth / TextMaxWidth / TextWidth）を公開し、プレビュー用の色文字列を保持する。<br/>
/// </remarks>
public class MainWindowViewModel : ObservableObject
{
    private string _selectedColor = "Red";
    private ColorSortMode _sortMode = ColorSortMode.ByHue;
    private ColorTextFormat _textFormat = ColorTextFormat.NameWithAlpha;
    private bool _highlightBorderOnError = true;
    private double _textMinWidth = 120;
    private double _textMaxWidth = double.PositiveInfinity;
    private double _textWidth = double.NaN;

    /// <summary>現在選択されている色（ColorComboBoxControl と双方向バインド）。</summary>
    public string SelectedColor
    {
        get => _selectedColor;
        set
        {
            if (SetProperty(ref _selectedColor, value))
            {
                OnPropertyChanged(nameof(SelectedColorHex));
            }
        }
    }

    /// <summary>プレビュー表示用の色文字列（16進または色名）。</summary>
    public string SelectedColorHex => SelectedColor;

    /// <summary>ドロップダウンの色の並び順。</summary>
    public ColorSortMode ColorSortMode
    {
        get => _sortMode;
        set => SetProperty(ref _sortMode, value);
    }

    /// <summary>並び順切替 ComboBox の選択インデックス（0 = 色相順 / 1 = 色名順）。</summary>
    public int SortModeIndex
    {
        get => ColorSortMode == ColorSortMode.ByHue ? 0 : 1;
        set => ColorSortMode = value == 0 ? ColorSortMode.ByHue : ColorSortMode.ByName;
    }

    /// <summary>バリデーション失敗時に枠線を赤くするかどうか。</summary>
    public bool HighlightBorderOnError
    {
        get => _highlightBorderOnError;
        set => SetProperty(ref _highlightBorderOnError, value);
    }

    /// <summary>入力欄の表記（色名 + 不透明度 / #AARRGGBB。 ColorComboBoxControl へバインド）。</summary>
    public ColorTextFormat TextFormat
    {
        get => _textFormat;
        set => SetProperty(ref _textFormat, value);
    }

    /// <summary>表記切替 ComboBox の選択インデックス（0 = 色名 + 不透明度 / 1 = #AARRGGBB）。</summary>
    public int TextFormatIndex
    {
        get => TextFormat == ColorTextFormat.NameWithAlpha ? 0 : 1;
        set => TextFormat = value == 0 ? ColorTextFormat.NameWithAlpha : ColorTextFormat.HexCode;
    }

    /// <summary>入力欄の最小幅（ColorComboBoxControl へバインド）。</summary>
    public double TextMinWidth
    {
        get => _textMinWidth;
        set => SetProperty(ref _textMinWidth, value);
    }

    /// <summary>入力欄の最大幅（ColorComboBoxControl へバインド）。</summary>
    public double TextMaxWidth
    {
        get => _textMaxWidth;
        set => SetProperty(ref _textMaxWidth, value);
    }

    /// <summary>入力欄の明示的な幅（NaN は自動。 ColorComboBoxControl へバインド）。</summary>
    public double TextWidth
    {
        get => _textWidth;
        set => SetProperty(ref _textWidth, value);
    }
}
