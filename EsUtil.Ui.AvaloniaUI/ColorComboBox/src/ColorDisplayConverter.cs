using System.Globalization;
using Avalonia.Data.Converters;
using CoreColorString = EsUtil.Platform.Core.Palette.ColorString;

namespace EsUtil.Ui.AvaloniaUI.ColorComboBox;

/// <summary>保存値（<c>#AARRGGBB</c>）と画面表示テキスト（<c>{色}+{不透明度%}</c>）を相互変換する双方向コンバーター。</summary>
/// <remarks>
/// ComboBox の <c>Text</c> プロパティ（編集可能）とのバインドに使用する。<br/>
/// - <see cref="Convert"/>: 保存値（<c>#80FF0000</c>）→ 表示テキスト（<c>Red+50%</c>）<br/>
/// - <see cref="ConvertBack"/>: 入力テキスト（<c>Red+50%</c>）→ 保存値（<c>#80FF0000</c>）<br/>
/// <b>変換の規則はコアが単一所有する</b>（<see cref="CoreColorString"/>）。 本クラスは Avalonia の
/// <see cref="IValueConverter"/> への適合だけを行う。 解析できない値は入力をそのまま返す
/// （バリデーションは <see cref="ColorComboBoxControl"/> が行う）。
/// </remarks>
public sealed class ColorDisplayConverter : IValueConverter
{
    /// <summary>保存値を表示テキストへ変換する。</summary>
    /// <param name="value">変換元の値（保存値）。</param>
    /// <param name="targetType">変換先の型。</param>
    /// <param name="parameter">変換パラメーター（未使用）。</param>
    /// <param name="culture">カルチャ情報（未使用）。</param>
    /// <returns>表示テキスト。 解析できない場合は入力をそのまま返す。</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string text ? CoreColorString.ToDisplayText(text) : value;

    /// <summary>入力テキストを保存値へ変換する。</summary>
    /// <param name="value">変換元の値（入力テキスト）。</param>
    /// <param name="targetType">変換先の型。</param>
    /// <param name="parameter">変換パラメーター（未使用）。</param>
    /// <param name="culture">カルチャ情報（未使用）。</param>
    /// <returns>保存値。 解析できない場合は入力をそのまま返す。</returns>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string text ? CoreColorString.ToArgbHex(text) : value;
}