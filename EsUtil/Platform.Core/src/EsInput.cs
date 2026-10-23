using EsUtil.Platform.Core.Common;
using EsUtil.Platform.Core.Input;

namespace EsUtil.Platform.Core;

/// <summary>入力（キーとホットキー）機能の窓口。</summary>
/// <remarks>
/// 機能の型は <see cref="EsUtil.Platform.Core.Input"/>（<see cref="EsKey"/> / <see cref="HotKeyModifiers"/> / <see cref="HotKeySetting"/>）に置く。<br/>
/// 本クラスは<b>利用側が <c>using EsUtil.Platform.Core;</c> だけで解釈・正規化・生成を行えるようにする</b>ための窓口であり、<br/>
/// <b>実装を持たない</b>（判断は各型が単一所有し、本クラスは委譲する）。
/// </remarks>
public static class EsInput
{
    /// <summary>文字列（設定ファイルの値）からキーを解釈する。</summary>
    /// <param name="name">キーの名前。</param>
    /// <param name="key">解釈したキー。 解釈できなかった場合は <see cref="EsKey.None"/>。</param>
    /// <returns>解釈できた場合は <see langword="true"/>。</returns>
    /// <remarks>数値文字列は受理しない（列挙に無い値を弾く。 判断は <see cref="EnumCanonical"/> が単一所有する）。</remarks>
    public static bool TryParseKey(string name, out EsKey key) => EnumCanonical.TryParse(name, out key);

    /// <summary>キーを正規化する（未定義値を <see cref="EsKey.None"/> へ寄せる）。</summary>
    /// <param name="key">対象のキー。</param>
    /// <returns>定義済みの場合は <paramref name="key"/> 自身。 未定義値の場合は <see cref="EsKey.None"/>。</returns>
    /// <remarks>正規化の内容は <see cref="EsKeyExtensions.Normalize"/> が単一所有する。</remarks>
    public static EsKey NormalizeKey(EsKey key) => key.Normalize();

    /// <summary>キーの組合せ（修飾キー＋キー）を生成する。</summary>
    /// <param name="modifiers">修飾キー。</param>
    /// <param name="key">ホットキーとして割り当てるキー。</param>
    /// <returns>生成した組合せ。</returns>
    public static HotKeySetting CreateHotKey(HotKeyModifiers modifiers, EsKey key) => new(modifiers, key);
}
