using System.Collections.Concurrent;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace EsUtil.Ui.AvaloniaUI.ColorComboBox;

/// <summary>色名文字列（例: "Red"）を <see cref="IBrush"/> に変換するコンバーター。</summary>
/// <remarks>
/// カラーComboBox のテキストエリアに該当色の四角を表示するために使用する。<br/>
/// 色と不透明度を統合した形式（例: "Red", "Red+50%", "#336699+75%"）を受け付ける。<br/>
/// 解釈できない値（null・空文字・未知の文字列）の場合は <see cref="Brushes.Transparent"/> を返す。<br/>
/// <br/>
/// **公開 API 一覧**:<br/>
/// - メソッド: <see cref="Convert(object?, Type, object?, CultureInfo)"/> / <see cref="ConvertBack(object?, Type, object?, CultureInfo)"/><br/>
/// <br/>
/// **利用例（XAML）**:<br/>
/// リソース定義: <c>&lt;cp:StringToBrushConverter x:Key="StringToBrushConverter"/&gt;</c><br/>
/// バインド: <c>Background="{Binding SelectedColor, Converter={StaticResource StringToBrushConverter}}"</c><br/>
/// <br/>
/// **適用パターン**: Flyweight（解析済み色の <see cref="SolidColorBrush"/> をキャッシュして再利用）。
/// </remarks>
public sealed class StringToBrushConverter : IValueConverter
{
    /// <summary>解析済み入力文字列 → ブラシ のキャッシュ（Flyweight。 同一入力のブラシ生成を再利用）。</summary>
    /// <remarks>
    /// キーは入力文字列そのもの（"Red" と "#FFFF0000" は別エントリ）。<br/>
    /// キャッシュは静的であるため、複数インスタンス・複数バインディングで共有される。<br/>
    /// 容量超過時は <see cref="BrushCacheCapacity"/> を上限に全クリアする（<see cref="ColorString.AddWithCapacityLimit{T}"/> に委譲）。
    /// </remarks>
    private static readonly ConcurrentDictionary<string, SolidColorBrush> s_brushCache = new(StringComparer.Ordinal);

    /// <summary>ブラシキャッシュの最大容量（超過時は全クリアして無制限の増加を防ぐ）。</summary>
    /// <remarks>テスト（EsUtil.Ui.AvaloniaUI.ColorComboBox.Tests）が容量超過の分岐を検証するため internal 公開する。</remarks>
    internal const int BrushCacheCapacity = 256;

    /// <summary>色名 → IBrush に変換する。</summary>
    /// <param name="value">変換元の値。 色と不透明度を統合した文字列（例: "Red", "Red+50%", "#336699+75%"）。</param>
    /// <param name="targetType">変換先の型。</param>
    /// <param name="parameter">変換パラメーター（未使用）。</param>
    /// <param name="culture">カルチャ情報（未使用）。</param>
    /// <returns>変換結果の <see cref="IBrush"/>。 解釈できない場合は <see cref="Brushes.Transparent"/>。</returns>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. キャッシュを引き、ヒットした場合は解析せず既存ブラシを返す。<br/>
    /// 2. <see cref="ColorString.TryParse(string, out Color, out double)"/> で解析可能な文字列のみ<br/>
    ///    <see cref="SolidColorBrush"/> を生成し、<see cref="ColorString.AddWithCapacityLimit{T}"/> で登録する<br/>
    ///    （不透明度はアルファへ合成される。 容量超過時は自動で全クリアされる）。
    /// </remarks>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string { Length: > 0 } text)
        {
            // キャッシュ優先（ヒット時は解析せず既存ブラシを返す）
            if (s_brushCache.TryGetValue(text, out var cached))
            {
                return cached;
            }

            if (ColorString.TryParse(text, out var color, out _))
            {
                // 容量管理は ColorString の共通ヘルパーに委譲する（容量管理ロジックの単一所有）
                var brush = new SolidColorBrush(color);
                ColorString.AddWithCapacityLimit(s_brushCache, BrushCacheCapacity, text, brush);
                return brush;
            }
        }

        return Brushes.Transparent;
    }

    /// <summary>逆変換は未サポート（常に <see cref="NotSupportedException"/> を送出する）。</summary>
    /// <param name="value">変換元の値。</param>
    /// <param name="targetType">変換先の型。</param>
    /// <param name="parameter">変換パラメーター。</param>
    /// <param name="culture">カルチャ情報。</param>
    /// <returns>常に <see cref="NotSupportedException"/>。</returns>
    /// <exception cref="NotSupportedException">逆変換は未サポートのため常に送出される。</exception>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

