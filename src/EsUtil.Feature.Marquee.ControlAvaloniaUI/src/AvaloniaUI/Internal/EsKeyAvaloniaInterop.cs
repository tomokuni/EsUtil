using EsUtil.Platform.Core;
using AvaloniaKey = Avalonia.Input.Key;

// 複製元: EsUtil.SaveEvidence.Platform.AvaloniaUI.Interop（独立パッケージ用の意図的な複製。 .github/依存関係.md §4.1）
namespace EsUtil.Feature.Marquee.AvaloniaUI.Internal;

/// <summary>UI フレームワーク非依存の <see cref="EsKey"/> と Avalonia の <see cref="AvaloniaKey"/> を相互変換する。</summary>
/// <remarks>
/// 両者は<b>メンバー名が完全に一致</b>するように設計されているため（実測: 両者とも 223 メンバー）、<br/>
/// 数値の一致には依存せず<b>名前ベース</b>で変換する（数値の順序はフレームワーク間で保証されない）。<br/>
/// <br/>
/// <b>別名（同じ値を持つ名前）</b>: Avalonia の <see cref="AvaloniaKey"/> には同じ値を持つ別名が 29 組ある<br/>
/// （実測: 223 名に対し異なる値は 194。 例: <c>Return</c> / <c>Enter</c>、<c>CapsLock</c> / <c>Capital</c>、<c>OemTilde</c> / <c>Oem3</c>）。<br/>
/// 値が同一のため<b>別名の同一性は保てない</b>（<c>EsKey.Enter</c> は <c>AvaloniaKey.Enter</c> へ変換できるが、<br/>
/// 逆向きの変換は代表名 <c>EsKey.Return</c> を返す）。 <b>キーそのもの（値）は保たれる</b>ため実用上の問題はない。<br/>
/// <b>代表名は宣言順では決まらない</b>（実測: 29 組中 10 組が宣言順の先頭と異なる。 .NET は同じ値の複数名のうち<br/>
/// どれを返すかを保証しない）ため、特定の名前を前提にした実装をしない。<br/>
/// <br/>
/// <b>未定義値</b>: <c>Enum.TryParse</c> は数値文字列を受け付けてしまうため、<br/>
/// 名前解決の成否だけでは未定義値を検出できない。 検査は <see cref="EsUtil.Platform.Core.EnumCanonical"/> が単一所有し、<br/>
/// 本変換はそれを用いて未定義値を <c>None</c> へフォールバックする<br/>
/// （実測: 検査が無いと <c>(EsKey)9999</c> が <c>(AvaloniaKey)9999</c> へ変換され、キー入力の解釈が未定義になる）。
/// </remarks>
internal static class EsKeyAvaloniaInterop
{
    /// <summary><see cref="EsKey"/> を Avalonia の <see cref="AvaloniaKey"/> へ変換する。</summary>
    /// <param name="key">変換対象のキー。</param>
    /// <returns>対応する Avalonia のキー。 名前が一致しない場合と未定義値は <see cref="AvaloniaKey.None"/>。</returns>
    public static AvaloniaKey ToAvaloniaKey(this EsKey key) =>
        EnumCanonical.TryParse<EsKey, AvaloniaKey>(key, out var result) ? result : AvaloniaKey.None;

    /// <summary>Avalonia の <see cref="AvaloniaKey"/> を <see cref="EsKey"/> へ変換する。</summary>
    /// <param name="key">変換対象のキー。</param>
    /// <returns>対応する <see cref="EsKey"/>。 名前が一致しない場合と未定義値は <see cref="EsKey.None"/>。</returns>
    public static EsKey ToEsKey(this AvaloniaKey key) =>
        EnumCanonical.TryParse<AvaloniaKey, EsKey>(key, out var result) ? result : EsKey.None;
}
