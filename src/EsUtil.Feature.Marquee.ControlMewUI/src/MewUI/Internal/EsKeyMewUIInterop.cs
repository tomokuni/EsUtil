using EsUtil.Platform.Core;
using MewUIKey = Aprillz.MewUI.Key;

// 複製元: 製品共通の MewUI 変換ヘルパー（独立パッケージ用の意図的な複製。 .github/依存関係.md §4.1）
namespace EsUtil.Feature.Marquee.MewUI.Internal;

/// <summary>UI フレームワーク非依存の <see cref="EsKey"/> と MewUI の <see cref="MewUIKey"/> を相互変換する。</summary>
/// <remarks>
/// <b>対応関係</b>: <see cref="EsKey"/> は Avalonia / Win32 のキー体系（223 メンバー）に合わせているのに対し、<br/>
/// MewUI の <see cref="MewUIKey"/> は抽象化された小さな集合（91 メンバー・独自採番。 Win32 の仮想キーコードではない）である。<br/>
/// そのため <b>値による対応付けはできず</b>、メンバー名で変換する。<br/>
/// <br/>
/// <b>名前が異なるキー</b>: <see cref="EsKey.Back"/> ⇔ <see cref="MewUIKey.Backspace"/> の 1 組のみ
/// （<see cref="s_esToMew"/> で吸収する）。<br/>
/// <b>対応先がないキー</b>: MewUI が表現できない <see cref="EsKey"/>（133 メンバー。 例: <c>BrowserBack</c> / <c>DbeRoman</c>）は
/// <see cref="MewUIKey.None"/> を返す。<br/>
/// <b>設定ファイルの互換性</b>: ホットキーは <c>JsonStringEnumConverter</c> が名前で保存するため、<br/>
/// 変換は数値ではなく名前ベースで行う（<c>EsKey</c> 側の名前を変えなければ既存設定をそのまま読み込める）。<br/>
/// </remarks>
internal static class EsKeyMewUIInterop
{
    /// <summary>名前が異なるが同じキーを表す組（<see cref="EsKey"/> → <see cref="MewUIKey"/>）。</summary>
    /// <remarks>
    /// <see cref="EsKey"/> は Avalonia と同じ <c>Back</c>、MewUI は <c>Backspace</c> という名前を使う。
    /// </remarks>
    private static readonly Dictionary<EsKey, MewUIKey> s_esToMew = new()
    {
        [EsKey.Back] = MewUIKey.Backspace,
    };

    /// <summary>名前が異なるが同じキーを表す組（<see cref="MewUIKey"/> → <see cref="EsKey"/>）。</summary>
    /// <remarks><see cref="s_esToMew"/> の逆引き（対称性を保つ）。</remarks>
    private static readonly Dictionary<MewUIKey, EsKey> s_mewToEs =
        s_esToMew.ToDictionary(static pair => pair.Value, static pair => pair.Key);

    /// <summary><see cref="EsKey"/> を MewUI の <see cref="MewUIKey"/> へ変換する。</summary>
    /// <param name="key">変換対象のキー。</param>
    /// <returns>対応する MewUI のキー。 対応先がない場合（MewUI が表現できないキー・未定義値）は <see cref="MewUIKey.None"/>。</returns>
    /// <remarks>
    /// 1. 名前が異なるキー（<see cref="EsKey.Back"/> 等）を別名表で解決する<br/>
    /// 2. 未定義値（<c>(EsKey)999</c> 等）は <see cref="EsUtil.Platform.Core.EnumCanonical"/> が検出し <see cref="MewUIKey.None"/> を返す
    ///    （<c>Enum.TryParse</c> は数値文字列を受け付けてしまうため、名前解決の成否だけでは検出できない）<br/>
    /// 3. 名前が一致する MewUI のメンバーへ変換する
    /// </remarks>
    public static MewUIKey ToMewUIKey(this EsKey key)
    {
        if (s_esToMew.TryGetValue(key, out var aliased))
        {
            return aliased;
        }

        return EnumCanonical.TryParse<EsKey, MewUIKey>(key, out var result) ? result : MewUIKey.None;
    }

    /// <summary>MewUI の <see cref="MewUIKey"/> を <see cref="EsKey"/> へ変換する。</summary>
    /// <param name="key">変換対象のキー。</param>
    /// <returns>対応する <see cref="EsKey"/>。 対応先がない場合（未定義値）は <see cref="EsKey.None"/>。</returns>
    /// <remarks>
    /// 1. 名前が異なるキー（<see cref="MewUIKey.Backspace"/> 等）を別名表で解決する<br/>
    /// 2. 未定義値は <see cref="EsUtil.Platform.Core.EnumCanonical"/> が検出し <see cref="EsKey.None"/> を返す<br/>
    /// 3. 名前が一致する <see cref="EsKey"/> のメンバーへ変換する
    /// </remarks>
    public static EsKey ToEsKey(this MewUIKey key)
    {
        if (s_mewToEs.TryGetValue(key, out var aliased))
        {
            return aliased;
        }

        return EnumCanonical.TryParse<MewUIKey, EsKey>(key, out var result) ? result : EsKey.None;
    }

    /// <summary>MewUI が表現できる <see cref="EsKey"/> かどうかを判定する。</summary>
    /// <param name="key">判定対象のキー。</param>
    /// <returns>MewUI に対応するキーがある場合は true。</returns>
    public static bool IsRepresentable(this EsKey key) => key.ToMewUIKey() != MewUIKey.None || key == EsKey.None;
}
