using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using SkiaSharp;

// 複製元: EsUtil.SaveEvidence.Platform.Skia（独立パッケージ用の意図的な複製。 .github/依存関係.md §4.1）
namespace EsUtil.Feature.Marquee.MewUI.Internal;

/// <summary>コアの幾何型と Skia の幾何型の相互変換。</summary>
/// <remarks>
/// コアの矩形（<see cref="EsRect"/>）を Skia の描画 API へ渡すための変換を単一所有する。<br/>
/// SkiaSharp は UI フレームワークではなくピクセル・描画の基盤であるため、<see cref="EsReadOnlyBitmap"/> と同じ層（本プロジェクト）が担う。<br/>
/// MewUI 版の描画層が共用し、同じ変換の複製を各コントロールへ置かない。<br/>
/// UI スレッドに依存しないため <c>[Fact]</c> で並列検証できる。
/// </remarks>
internal static class SkiaGeometryInterop
{
    /// <summary>コアの矩形を Skia の矩形へ変換する。</summary>
    /// <param name="rect">変換元の矩形（コア座標）。</param>
    /// <returns>Skia の矩形（左上と右下で表現する）。</returns>
    /// <remarks>負のサイズもそのまま引き継ぐ（<see cref="EsRect"/> は負のサイズを保持できる）。</remarks>
    public static SKRect ToSKRect(this EsRect rect)
        => new((float)rect.X, (float)rect.Y, (float)rect.Right, (float)rect.Bottom);

    /// <summary>Skia の整数サイズをコアのサイズへ変換する。</summary>
    /// <param name="size">変換元のサイズ（物理ピクセル）。</param>
    /// <returns>コアのサイズ。</returns>
    /// <remarks>描画先の表面サイズ（<c>SKImageInfo.Size</c> 等）をコアの計算へ渡すときに使う。</remarks>
    public static EsSize ToEsSize(this SKSizeI size) => new(size.Width, size.Height);
}
