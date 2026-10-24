using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using EsUtil.Platform.Core.Caching;
using CoreColorString = EsUtil.Platform.Core.Palette.ColorString;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.AvaloniaUI.ColorComboBox;

/// <summary>色文字列（例: <c>Red</c>）を <see cref="IBrush"/> に変換するコンバーター。</summary>
/// <remarks>
/// テキスト領域の左端に色のスウォッチを表示するために使用する。 受理する入力（色名・16 進・不透明度 %）は
/// コアの <see cref="CoreColorString"/> が単一所有する。 解釈できない値は <see cref="Brushes.Transparent"/> になる。<br/>
/// <b>ブラシは入力文字列ごとに共有する</b>（容量管理は
/// <see cref="CapacityLimitedCache{TKey, TValue}"/> が単一所有し、上限に達したら全体をクリアする）。
/// </remarks>
public sealed class StringToBrushConverter : IValueConverter
{
    /// <summary>入力文字列 → ブラシ のキャッシュ。</summary>
    private static readonly CapacityLimitedCache<string, IBrush> s_brushCache =
        new(text => CoreColorString.TryParse(text, out var color, out _)
                ? new SolidColorBrush(ToAvalonia(color))
                : Brushes.Transparent,
            StringComparer.Ordinal);

    /// <summary>色文字列を <see cref="IBrush"/> へ変換する。</summary>
    /// <param name="value">変換元の値（色文字列）。</param>
    /// <param name="targetType">変換先の型。</param>
    /// <param name="parameter">変換パラメーター（未使用）。</param>
    /// <param name="culture">カルチャ情報（未使用）。</param>
    /// <returns>変換結果のブラシ。 解釈できない場合は <see cref="Brushes.Transparent"/>。</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string { Length: > 0 } text ? s_brushCache.GetOrAdd(text) : Brushes.Transparent;

    /// <summary>逆変換は未サポートである。</summary>
    /// <param name="value">変換元の値。</param>
    /// <param name="targetType">変換先の型。</param>
    /// <param name="parameter">変換パラメーター（未使用）。</param>
    /// <param name="culture">カルチャ情報（未使用）。</param>
    /// <returns>常に例外を送出する。</returns>
    /// <exception cref="NotSupportedException">常に送出する。</exception>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    /// <summary>コアの色を Avalonia の色へ写す。</summary>
    /// <param name="color">コアの色。</param>
    /// <returns>Avalonia の色。</returns>
    private static Color ToAvalonia(DrawingColor color) => Color.FromArgb(color.A, color.R, color.G, color.B);
}