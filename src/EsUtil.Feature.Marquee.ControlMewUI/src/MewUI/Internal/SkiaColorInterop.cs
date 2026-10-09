using EsUtil.Platform.Core.Imaging;
using SkiaSharp;
using DrawingColor = System.Drawing.Color;

// 複製元: EsUtil.SaveEvidence.Platform.Skia（独立パッケージ用の意図的な複製。 .github/依存関係.md §4.1）
namespace EsUtil.Feature.Marquee.MewUI.Internal;

/// <summary>コアの色型（<see cref="DrawingColor"/>）と Skia の色型の相互変換。</summary>
/// <remarks>
/// コアの色型（UI 非依存の <see cref="DrawingColor"/>）を Skia の描画 API へ渡すための変換を単一所有する。<br/>
/// SkiaSharp は UI フレームワークではなくピクセル・描画の基盤であるため、<see cref="EsBitmap"/> と同じ層（本プロジェクト）が担う。<br/>
/// MewUI 版の描画層（Skia を使う 6 箇所）が共用し、同じ変換の複製を各コントロールへ置かない。<br/>
/// 逆方向（Skia → コア）も本クラスが単一所有する（UI が Skia 型の公開プロパティを持つ場合の境界変換に使う）。
/// </remarks>
internal static class SkiaColorInterop
{
    /// <summary>コアの色型を Skia の色型へ変換する。</summary>
    /// <param name="color">変換元の色（不透明度はアルファ）。</param>
    /// <returns>Skia の色（ARGB はそのまま保持される）。</returns>
    public static SKColor ToSKColor(this DrawingColor color) => new(color.R, color.G, color.B, color.A);

    /// <summary>Skia の色型をコアの色型へ変換する。</summary>
    /// <param name="color">変換元の色（不透明度はアルファ）。</param>
    /// <returns>コアの色（ARGB はそのまま保持される）。</returns>
    public static DrawingColor ToDrawingColor(this SKColor color)
        => DrawingColor.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
}
