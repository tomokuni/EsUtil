using CommunityToolkit.Mvvm.ComponentModel;
using EsUtil.Platform.Core.Palette;

namespace EsUtil.Ui.AvaloniaUI.ColorComboBox.Sample.ViewModels;

/// <summary>メインウィンドウの ViewModel。</summary>
/// <remarks>
/// ColorComboBoxControl の各種プロパティ（SelectedColor / ColorSortMode / HighlightBorderOnError /
/// ComboBoxMinWidth / ComboBoxMaxWidth / ComboBoxWidth）を公開し、プレビュー用の色文字列を保持する。<br/>
/// </remarks>
public class MainWindowViewModel : ObservableObject
{
    private string _selectedColor = "Red";
    private ColorSortMode _sortMode = ColorSortMode.ByHue;
    private bool _highlightBorderOnError = true;
    private double _comboBoxMinWidth = 120;
    private double _comboBoxMaxWidth = double.PositiveInfinity;
    private double _comboBoxWidth = double.NaN;

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

    /// <summary>ComboBox の最小幅（ColorComboBoxControl へバインド）。</summary>
    public double ComboBoxMinWidth
    {
        get => _comboBoxMinWidth;
        set => SetProperty(ref _comboBoxMinWidth, value);
    }

    /// <summary>ComboBox の最大幅（ColorComboBoxControl へバインド）。</summary>
    public double ComboBoxMaxWidth
    {
        get => _comboBoxMaxWidth;
        set => SetProperty(ref _comboBoxMaxWidth, value);
    }

    /// <summary>ComboBox の明示的な幅（NaN は自動。 ColorComboBoxControl へバインド）。</summary>
    public double ComboBoxWidth
    {
        get => _comboBoxWidth;
        set => SetProperty(ref _comboBoxWidth, value);
    }
}
