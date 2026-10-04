namespace EsUtil.Platform.Core;

/// <summary>同じ値を持つ別名（重複値）を許す列挙型の「代表名」による解決を提供する。</summary>
/// <remarks>
/// 列挙型が<b>同じ値を持つ複数の名前</b>（別名）を持つ場合、.NET は「値から名前を引くときにどれを返すか」を<br/>
/// 保証しない（<see cref="Enum.GetName{TEnum}(TEnum)"/> の仕様）。 実際、<c>Avalonia.Input.Key</c> には<br/>
/// 29 組の別名があり（<c>Return</c> / <c>Enter</c>、<c>OemTilde</c> / <c>Oem3</c> 等）、値 → 名前 → 値の往復で<br/>
/// <b>代表名へ正規化される</b>（実測: 29 組中 10 組は宣言順の先頭とも一致しない）。<br/>
/// <br/>
/// したがって「どの名前が代表になるか」を前提にした実装をしてはならない。 代わりに<br/>
/// <b>値（キーそのもの）を契約として扱い</b>、フレームワークをまたぐ写像は<b>名前で解決</b>して<br/>
/// 正規化された結果を使う。 本クラスはその解決手順を単一所有する（各 Interop が同じ手順を書かない）。<br/>
/// <br/>
/// <b>正規化の実体</b>: <see cref="Enum.TryParse{T}(string, out T)"/> は「名前 → 値」なので正規化しない。<br/>
/// 正規化するのは <see cref="object.ToString"/>（<see cref="Enum"/> が実装する「値 → 単一の名前」）である。<br/>
/// 未定義値は <see cref="Enum.IsDefined{T}(T)"/> で検出できる（<see cref="Enum.TryParse{T}(string, out T)"/> は<br/>
/// 数値文字列を受理してしまうため、名前解決の成否だけでは検出できない）。
/// </remarks>
public static class EnumCanonical
{
    /// <summary>別名を持つ可能性のある列挙値を、代表名で別の列挙型へ解決する。</summary>
    /// <typeparam name="TSource">変換元の列挙型。</typeparam>
    /// <typeparam name="TTarget">変換先の列挙型。</typeparam>
    /// <param name="source">変換元の値。</param>
    /// <param name="target">解決した値。 解決できなかった場合は <c>default</c>。</param>
    /// <returns>解決できた場合は true。</returns>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. <see cref="Enum.IsDefined{T}(T)"/> で未定義値（数値文字列で作られた値等）を除外する<br/>
    /// 2. <paramref name="source"/> の名前（代表名）で <typeparamref name="TTarget"/> のメンバーを解決する<br/>
    /// 3. 名前が一致しなければ false（<paramref name="target"/> は <c>default</c>）
    /// </remarks>
    public static bool TryParse<TSource, TTarget>(TSource source, out TTarget target)
        where TSource : struct, Enum
        where TTarget : struct, Enum
    {
        target = default;

        if (!Enum.IsDefined(source))
        {
            return false;
        }

        return Enum.TryParse(source.ToString(), out target);
    }
}
