using EsUtil.Platform.Core;
using WinFormsKeys = System.Windows.Forms.Keys;

// 複製元: EsUtil.Feature.Marquee.ControlMewUI.Internal（独立パッケージ用の意図的な複製。 .github/依存関係.md §4.1）
namespace EsUtil.Feature.Marquee.WinForms.Internal;

/// <summary>UI フレームワーク非依存の <see cref="EsKey"/> と WinForms の <see cref="WinFormsKeys"/> を相互変換する。</summary>
/// <remarks>
/// <b>対応関係</b>: <see cref="EsKey"/> は Avalonia / Win32 のキー体系（223 メンバー）に合わせているのに対し、<br/>
/// WinForms の <see cref="WinFormsKeys"/> は <b>Win32 仮想キーコードを値に持つ列挙</b>（194 名 / 185 値。 修飾キーのフラグと<br/>
/// マウス ボタン・キー以外の入力も含む）である。<br/>
/// <b>値の一致には依存せず名前で変換する</b>（<see cref="EsKey"/> の値は序数であり、キー体系をまたぐ数値の対応は保証されない）。<br/>
/// 実測（2026-10-15）: 名前が <b>完全に一致する 162 件</b>はそのまま解決でき、<b>大文字小文字だけが異なる 9 組</b><br/>
/// （例: <c>OemTilde</c> ⇔ <c>Oemtilde</c>。 WinForms 側が歴史的に小文字始まりの名前を持つ）と、<br/>
/// <b>名前が異なる 6 組</b>（例: <c>LeftShift</c> ⇔ <c>LShiftKey</c>）を <see cref="s_esToKeys"/> で吸収する。<br/>
/// <br/>
/// <b>対応先がないキー</b>: WinForms が表現できない <see cref="EsKey"/>（46 件。 例: <c>DbeRoman</c> / <c>FnLeftArrow</c> / <c>MediaHome</c>）と、<br/>
/// キー以外の値 16 件（マウス ボタン 5 件・IME とプロセス用のキー 3 件・修飾キーのフラグ 5 件・左右を区別しない修飾キー 3 件）は<br/>
/// <see cref="WinFormsKeys.None"/> を返す。<br/>
/// <b>設定ファイルの互換性</b>: ホットキーは <c>JsonStringEnumConverter</c> が名前で保存するため、<br/>
/// 変換は数値ではなく名前ベースで行う（<c>EsKey</c> 側の名前を変えなければ既存設定をそのまま読み込める）。
/// </remarks>
internal static class EsKeyWinFormsInterop
{
    /// <summary>名前が異なるが同じキーを表す組（<see cref="EsKey"/> → <see cref="WinFormsKeys"/>）。</summary>
    /// <remarks>
    /// <b>左右の接頭辞</b>: <see cref="EsKey"/> は左右を名前に含める（Avalonia 系）が、WinForms は Win32 の <c>L</c> / <c>R</c> 接頭辞を使う。<br/>
    /// 左右を区別しない <see cref="WinFormsKeys.ShiftKey"/> / <see cref="WinFormsKeys.ControlKey"/> / <see cref="WinFormsKeys.Menu"/> は<br/>
    /// <see cref="EsKey"/> に同じ意味の名前が無いため対応させない（既定の矢印キーへ化けさせない）。<br/>
    /// <b>大文字小文字</b>: WinForms 側は <c>Oemtilde</c> / <c>Oemplus</c> / <c>Oemcomma</c> と <c>IMEConvert</c> 系・<c>Crsel</c> / <c>Exsel</c> に<br/>
    /// 慣習的な綴りを持つため、名前解決（大文字小文字を区別する）では一致しない。 ここで吸収する。
    /// </remarks>
    private static readonly Dictionary<EsKey, WinFormsKeys> s_esToKeys = new()
    {
        [EsKey.LeftShift] = WinFormsKeys.LShiftKey,
        [EsKey.RightShift] = WinFormsKeys.RShiftKey,
        [EsKey.LeftCtrl] = WinFormsKeys.LControlKey,
        [EsKey.RightCtrl] = WinFormsKeys.RControlKey,
        [EsKey.LeftAlt] = WinFormsKeys.LMenu,
        [EsKey.RightAlt] = WinFormsKeys.RMenu,
        [EsKey.ImeConvert] = WinFormsKeys.IMEConvert,
        [EsKey.ImeNonConvert] = WinFormsKeys.IMENonconvert,
        [EsKey.ImeAccept] = WinFormsKeys.IMEAccept,
        [EsKey.ImeModeChange] = WinFormsKeys.IMEModeChange,
        [EsKey.OemPlus] = WinFormsKeys.Oemplus,
        [EsKey.OemComma] = WinFormsKeys.Oemcomma,
        [EsKey.OemTilde] = WinFormsKeys.Oemtilde,
        [EsKey.CrSel] = WinFormsKeys.Crsel,
        [EsKey.ExSel] = WinFormsKeys.Exsel,
    };

    /// <summary>名前が異なるが同じキーを表す組（<see cref="WinFormsKeys"/> → <see cref="EsKey"/>）。</summary>
    /// <remarks><see cref="s_esToKeys"/> の逆引き（対称性を保つ）。</remarks>
    private static readonly Dictionary<WinFormsKeys, EsKey> s_keysToEs =
        s_esToKeys.ToDictionary(static pair => pair.Value, static pair => pair.Key);

    /// <summary><see cref="EsKey"/> を WinForms の <see cref="WinFormsKeys"/> へ変換する。</summary>
    /// <param name="key">変換対象のキー。</param>
    /// <returns>対応する WinForms のキー。 対応先がない場合（WinForms が表現できないキー・未定義値）は <see cref="WinFormsKeys.None"/>。</returns>
    /// <remarks>
    /// 1. 名前が異なるキー（<see cref="EsKey.LeftShift"/> 等）を別名表で解決する<br/>
    /// 2. 未定義値（<c>(EsKey)999</c> 等）は <see cref="EnumCanonical"/> が検出し <see cref="WinFormsKeys.None"/> を返す
    ///    （<c>Enum.TryParse</c> は数値文字列を受け付けてしまうため、名前解決の成否だけでは検出できない）<br/>
    /// 3. 名前が一致する WinForms のメンバーへ変換する
    /// </remarks>
    public static WinFormsKeys ToWinFormsKeys(this EsKey key)
    {
        if (s_esToKeys.TryGetValue(key, out var aliased))
        {
            return aliased;
        }

        return EnumCanonical.TryParse<EsKey, WinFormsKeys>(key, out var result) ? result : WinFormsKeys.None;
    }

    /// <summary>WinForms の <see cref="WinFormsKeys"/> を <see cref="EsKey"/> へ変換する。</summary>
    /// <param name="key">変換対象のキー。</param>
    /// <returns>対応する <see cref="EsKey"/>。 対応先がない場合（キー以外の入力・未定義値）は <see cref="EsKey.None"/>。</returns>
    /// <remarks>
    /// 1. 名前が異なるキー（<see cref="WinFormsKeys.LShiftKey"/> 等）を別名表で解決する<br/>
    /// 2. 未定義値は <see cref="EnumCanonical"/> が検出し <see cref="EsKey.None"/> を返す<br/>
    /// 3. 名前が一致する <see cref="EsKey"/> のメンバーへ変換する
    /// </remarks>
    public static EsKey ToEsKey(this WinFormsKeys key)
    {
        if (s_keysToEs.TryGetValue(key, out var aliased))
        {
            return aliased;
        }

        return EnumCanonical.TryParse<WinFormsKeys, EsKey>(key, out var result) ? result : EsKey.None;
    }

    /// <summary>WinForms が表現できる <see cref="EsKey"/> かどうかを判定する。</summary>
    /// <param name="key">判定対象のキー。</param>
    /// <returns>WinForms に対応するキーがある場合は true。</returns>
    public static bool IsRepresentable(this EsKey key) => key.ToWinFormsKeys() != WinFormsKeys.None || key == EsKey.None;
}
