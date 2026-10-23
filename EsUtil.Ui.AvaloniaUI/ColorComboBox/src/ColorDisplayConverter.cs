using System.Globalization;
using Avalonia.Data.Converters;

namespace EsUtil.Ui.AvaloniaUI.ColorComboBox;

/// <summary>保存値（#AARRGGBB）と画面表示テキスト（{色}+{不透明度%}）を相互変換する双方向コンバーター。</summary>
/// <remarks>
/// ComboBox の <c>Text</c> プロパティ（編集可能）とのバインドに使用する。<br/>
/// - <see cref="Convert"/>: 保存値（"#80FF0000"）→ 表示テキスト（"Red+50%"）<br/>
/// - <see cref="ConvertBack"/>: 入力テキスト（"Red+50%"）→ 保存値（"#80FF0000"）<br/>
/// <br/>
/// 変換は <see cref="ColorString.ToDisplayText(string?)"/> / <see cref="ColorString.ToArgbHex(string?)"/> に委譲する。<br/>
/// 解析できない値は入力をそのまま返す（バリデーションは ColorComboBoxControl 側で行う）。<br/>
/// <br/>
/// **公開 API 一覧**:<br/>
/// - メソッド: <see cref="Convert(object?, Type, object?, CultureInfo)"/> / <see cref="ConvertBack(object?, Type, object?, CultureInfo)"/><br/>
/// <br/>
/// **利用例（XAML）**:<br/>
/// リソース定義: <c>&lt;cp:ColorDisplayConverter x:Key="ColorDisplayConverter"/&gt;</c><br/>
/// バインド: <c>Text="{Binding SelectedColor, Converter={StaticResource ColorDisplayConverter}}"</c><br/>
/// </remarks>
public sealed class ColorDisplayConverter : IValueConverter
{
    /// <summary>保存値（#AARRGGBB）を画面表示テキスト（{色}+{不透明度%}）へ変換する。</summary>
    /// <param name="value">変換元の値。 保存値（例: "#80FF0000"）。</param>
    /// <param name="targetType">変換先の型。</param>
    /// <param name="parameter">変換パラメーター（未使用）。</param>
    /// <param name="culture">カルチャ情報（未使用）。</param>
    /// <returns>表示テキスト（例: "Red+50%"）。 解析できない場合は入力をそのまま返す。</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string text ? ColorString.ToDisplayText(text) : value;

    /// <summary>入力テキスト（{色}+{不透明度%}）を保存値（#AARRGGBB）へ変換する。</summary>
    /// <param name="value">変換元の値。 入力テキスト（例: "Red+50%"）。</param>
    /// <param name="targetType">変換先の型。</param>
    /// <param name="parameter">変換パラメーター（未使用）。</param>
    /// <param name="culture">カルチャ情報（未使用）。</param>
    /// <returns>保存値（例: "#80FF0000"）。 解析できない場合は入力をそのまま返す。</returns>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string text ? ColorString.ToArgbHex(text) : value;
}
