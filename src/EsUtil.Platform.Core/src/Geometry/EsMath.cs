using System.Runtime.CompilerServices;

namespace EsUtil.Platform.Core.Geometry;

/// <summary>幾何値の比較と丸めの計算を単一所有する。</summary>
/// <remarks>
/// 浮動小数の「ほぼ等しい」判定と整数への丸めは、各型が書くと許容誤差・丸め規則が型ごとに散る。<br/>
/// 本クラスが唯一の所有者であり、各型の <c>NearlyEquals</c> / <c>FromEsXxx</c> はこれへ委譲する。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で検証できる。
/// </remarks>
internal static class EsMath
{
    /// <summary>2 つの値がほぼ等しいかどうかを判定する。</summary>
    /// <param name="a">比較する値。</param>
    /// <param name="b">比較する値。</param>
    /// <param name="tolerance">許容誤差（0 以上。 既定 1e-9）。</param>
    /// <returns>ほぼ等しい場合は true。</returns>
    /// <remarks>
    /// 差が <paramref name="tolerance"/> 以下、または差が両者の大きい方の絶対値 × <paramref name="tolerance"/> 以下のとき true。<br/>
    /// 完全に一致する場合（同じ無限大を含む）は差分の計算をせずに true を返し、<br/>
    /// 以降は有限値のみを対象とする（無限大と有限値の差は無限大になり、相対誤差の判定が常に成立してしまうため）。<br/>
    /// 大きい座標でも相対誤差で判定できるよう両方を見る（絶対値だけでは 1 万を超える座標で必ず不一致になる）。
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool AreClose(double a, double b, double tolerance = 1e-9)
    {
        if (a == b)
        {
            // 完全一致（同じ無限大を含む）は差分が非数になるため先に確定させる
            return true;
        }

        if (!double.IsFinite(a) || !double.IsFinite(b))
        {
            // 非数・無限大の不一致は「ほぼ等しい」とは言えない（相対誤差の判定が誤って成立するのを防ぐ）
            return false;
        }

        var diff = Math.Abs(a - b);
        return diff <= tolerance || diff <= Math.Max(Math.Abs(a), Math.Abs(b)) * tolerance;
    }

    /// <summary>小数を指定の方法で整数へ丸める。</summary>
    /// <param name="value">丸める値。</param>
    /// <param name="rounding">丸め方法。</param>
    /// <returns>丸めた整数。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rounding"/> が <see cref="EsRounding"/> の定義値でない場合。</exception>
    /// <remarks>丸め規則を 1 箇所に閉じるため、各整数型の変換は本メソッドへ委譲する。</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int RoundToInt(double value, EsRounding rounding)
        => rounding switch
        {
            EsRounding.Truncate => (int)Math.Truncate(value),
            EsRounding.Round => (int)Math.Round(value, MidpointRounding.AwayFromZero),
            EsRounding.Ceiling => (int)Math.Ceiling(value),
            _ => throw new ArgumentOutOfRangeException(nameof(rounding), rounding, "丸め方法は EsRounding の定義値である必要があります。"),
        };
}
