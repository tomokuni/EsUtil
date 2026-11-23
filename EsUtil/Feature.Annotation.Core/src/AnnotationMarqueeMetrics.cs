using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation;

/// <summary>選択枠（Marquee）へ渡す文字の実寸の近似（単一所有）。</summary>
/// <remarks>
/// <b>近似で足りる</b>: 注釈の選択枠はサイズ・カーソル座標のラベルを出さないため、文字の実寸は
/// 枠の計算にしか使われません。 実寸を測る関数を渡せない場面（枠の計算だけを行う場面）でも同じ結果にします。<br/>
/// <b>式を UI に書かない</b>: 4 つの UI 版が同じ式を持つと、版ごとに枠の寸法が変わり、
/// ハンドルの位置と当たり判定が食い違う原因になります。
/// </remarks>
public static class AnnotationMarqueeMetrics
{
    /// <summary>文字列の実寸を近似する。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="fontSize">文字の大きさ（DIP）。</param>
    /// <returns>近似した実寸（幅は文字数の半分 × 文字の大きさ）。</returns>
    public static EsSize MeasureText(string text, double fontSize)
        => new(text.Length * fontSize * 0.5, fontSize);
}
