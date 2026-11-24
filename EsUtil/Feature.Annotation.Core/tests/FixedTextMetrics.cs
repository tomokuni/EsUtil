using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.Text;

namespace EsUtil.Feature.Annotation.Core.Tests;

/// <summary>テスト用の文字の実寸（1 文字 = 文字サイズの半分の幅、行の高さ = 文字サイズ）。</summary>
/// <remarks>
/// 実寸の計測は描画 API に依存するため本層から切り離してある。 テストでは決まった比率で測り、
/// 折り返しの位置だけを検証する（フォントの実測値に依存させない）。
/// </remarks>
internal static class FixedTextMetrics
{
    /// <summary>1 文字の幅の比率（文字サイズに対する比）。</summary>
    internal const double CharacterWidthRatio = 0.5;

    /// <summary>文字列と書式から実寸を測る。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <returns>実寸（元画像ピクセル）。</returns>
    internal static EsSize Measure(string text, TextStyle style)
        => new(text.Length * style.FontSize * CharacterWidthRatio, style.FontSize);
}
