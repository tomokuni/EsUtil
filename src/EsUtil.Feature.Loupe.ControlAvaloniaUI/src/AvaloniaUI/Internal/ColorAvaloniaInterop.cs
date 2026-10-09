using Avalonia.Media;
using DrawingColor = System.Drawing.Color;

// 複製元: EsUtil.SaveEvidence.Platform.AvaloniaUI.Interop（独立パッケージ用の意図的な複製。 .github/依存関係.md §4.1）
namespace EsUtil.Feature.Loupe.AvaloniaUI.Internal;

/// <summary>Avalonia の色型とコア色型（<see cref="DrawingColor"/>）を相互変換する。</summary>
/// <remarks>
/// コアロジックの色は <see cref="DrawingColor"/>（<c>System.Drawing.Primitives</c>。 クロスプラットフォーム）で扱い、<br/>
/// UI 境界でのみ本クラスで変換する。<br/>
/// 変換は拡張メソッドとして提供し、呼び出し側は <c>using EsUtil.SaveEvidence.Platform.AvaloniaUI;</c> のみで利用できる。<br/>
/// <see cref="DrawingColor"/> は <c>System.Drawing.Common</c>（Windows 専用の描画 API）ではなく<br/>
/// 共有フレームワーク同梱の値型（<c>System.Drawing.Primitives</c>）である。
/// </remarks>
internal static class ColorAvaloniaInterop
{
    /// <summary>コア色型を Avalonia の色型へ変換する。</summary>
    /// <param name="color">変換元の色。</param>
    /// <returns>Avalonia の色（ARGB はそのまま保持される）。</returns>
    public static Color ToAvaloniaColor(this DrawingColor color) => new(color.A, color.R, color.G, color.B);

    /// <summary>Avalonia の色型をコア色型へ変換する。</summary>
    /// <param name="color">変換元の色。</param>
    /// <returns>コアの色（ARGB はそのまま保持される）。</returns>
    public static DrawingColor ToDrawingColor(this Color color) => DrawingColor.FromArgb(color.A, color.R, color.G, color.B);
}
