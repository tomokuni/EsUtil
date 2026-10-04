namespace EsUtil.Platform.Core;

/// <summary><see cref="EsKey"/> の正規化（値の妥当性へ寄せる）を提供する。</summary>
/// <remarks>
/// <b>正規化する理由</b>: <see cref="System.Enum"/> の文字列解釈（<c>Enum.TryParse</c>）は<b>数値文字列を受理する</b>ため、<br/>
/// 文字列から作られた値には列挙に無い値（例: <c>"999"</c> → <c>(EsKey)999</c>）が混入しうる。<br/>
/// また <see cref="EsKey"/> の別名は存在しない（223 名 = 223 値）が、他のキー体系（<c>Avalonia.Input.Key</c> 等）を<br/>
/// 経由した値は<b>代表名へ正規化</b>されている（<see cref="EnumCanonical"/> を参照）。<br/>
/// <br/>
/// <b>正規化の内容</b>: 未定義値を <see cref="EsKey.None"/> へ寄せる。 定義済みの値はそのまま返す<br/>
/// （<see cref="EsKey"/> は値の指定が無い連番であり、同じ値を持つ別名を持たないため）。<br/>
/// 「値の有無」と「None キー」の区別が必要な場合は <see cref="Enum.IsDefined{T}(T)"/> を併用する。
/// </remarks>
public static class EsKeyExtensions
{
    /// <summary>キーを正規化する（未定義値を <see cref="EsKey.None"/> へ寄せる）。</summary>
    /// <param name="key">対象のキー。</param>
    /// <returns>定義済みの場合は <paramref name="key"/> 自身。 未定義値の場合は <see cref="EsKey.None"/>。</returns>
    /// <remarks>
    /// 文字列から解釈した値（<c>Enum.TryParse</c> が数値文字列を受理した結果）をそのまま使うと、<br/>
    /// 列挙に無い値がキーとして通り、キーの登録（Win32 の仮想キーコード変換）で失敗する。 <b>解釈の直後に本メソッドを通す</b>。<br/>
    /// 本メソッドは冪等である（<c>key.Normalize().Normalize() == key.Normalize()</c>）。
    /// </remarks>
    public static EsKey Normalize(this EsKey key) =>
        EnumCanonical.TryParse<EsKey, EsKey>(key, out var normalized) ? normalized : EsKey.None;
}
