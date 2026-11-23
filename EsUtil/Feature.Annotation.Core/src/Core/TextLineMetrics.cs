namespace EsUtil.Feature.Annotation.Core;

/// <summary>1 つの書式の行の実寸（高さとベースライン）。</summary>
/// <param name="Height">行の高さ（元画像ピクセル）。</param>
/// <param name="Baseline">行の上端からベースラインまでの距離（元画像ピクセル）。</param>
public readonly record struct TextLineMetrics(double Height, double Baseline);
