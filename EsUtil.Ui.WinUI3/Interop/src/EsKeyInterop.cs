using VirtualKey = Windows.System.VirtualKey;
using EsUtil.Platform.Core.Common;
using EsUtil.Platform.Core.Input;

namespace EsUtil.Ui.WinUI3.Interop;

/// <summary>UI フレームワーク非依存の <see cref="EsKey"/> と WinUI 3 の <see cref="VirtualKey"/> を相互変換する。</summary>
/// <remarks>
/// <b>対応関係</b>: <see cref="EsKey"/> は Avalonia / Win32 のキー体系（223 メンバー）に合わせているのに対し、<br/>
/// WinUI 3 の <see cref="VirtualKey"/> は Win32 の仮想キーコードを名前で表した 172 メンバーである。<br/>
/// <b>体系が異なるため値（序数・仮想キーコード）による対応付けはできず</b>、名前で変換する。<br/>
/// <br/>
/// <b>名前が異なるキー</b>: <see cref="s_aliases"/> が吸収する（例: <c>Return</c> ⇔ <c>Enter</c>、<c>D0</c> ⇔ <c>Number0</c>）。<br/>
/// <b>対応先がないキー</b>: <see cref="VirtualKey"/> が持たない記号キー（<c>Oem*</c>）とメディア キー（<c>Volume*</c> / <c>Media*</c> / <c>Launch*</c>）は<br/>
/// <see cref="VirtualKey.None"/> を返す（未対応は 0 で表す。 序数を返して別のキーへ化けさせない）。<br/>
/// <b>同じキーを表す複数のメンバー</b>: <see cref="EsKey.Return"/> と <see cref="EsKey.Enter"/> のように同じ物理キーを指すメンバーは<br/>
/// 同じ <see cref="VirtualKey"/> へ写り、逆変換は先に宣言した方を代表とする（<see cref="CreateReverseTable"/>）。<br/>
/// <b>設定ファイルの互換性</b>: ホットキーは <c>JsonStringEnumConverter</c> が名前で保存するため、<br/>
/// 変換は数値ではなく名前ベースで行う（<see cref="EsKey"/> 側の名前を変えなければ既存設定をそのまま読み込める）。
/// </remarks>
public static class EsKeyInterop
{
    /// <summary>名前が異なるが同じキーを表す組（<see cref="EsKey"/> → <see cref="VirtualKey"/>）。</summary>
    /// <remarks>
    /// 名前が一致する組（<c>Escape</c> / <c>F1</c> 等）は列挙しない（<see cref="EnumCanonical"/> が解決する）。<br/>
    /// 同じ <see cref="VirtualKey"/> へ写る組を複数含む（逆変換の代表は <see cref="CreateReverseTable"/> が決める）。
    /// </remarks>
    private static readonly (EsKey Es, VirtualKey Virtual)[] s_aliases =
    [
        // キー名そのものが異なる組
        // （同じ WinUI 3 のキーへ写る組は、先に宣言した方を逆変換の代表とする。 主名を先に置く）
        (EsKey.Return, VirtualKey.Enter),
        (EsKey.Enter, VirtualKey.Enter),
        (EsKey.CapsLock, VirtualKey.CapitalLock),
        (EsKey.Capital, VirtualKey.CapitalLock),
        (EsKey.PageUp, VirtualKey.PageUp),
        (EsKey.Prior, VirtualKey.PageUp),
        (EsKey.PageDown, VirtualKey.PageDown),
        (EsKey.Next, VirtualKey.PageDown),
        (EsKey.Snapshot, VirtualKey.Snapshot),
        (EsKey.PrintScreen, VirtualKey.Snapshot),
        (EsKey.NumLock, VirtualKey.NumberKeyLock),
        (EsKey.LWin, VirtualKey.LeftWindows),
        (EsKey.RWin, VirtualKey.RightWindows),
        (EsKey.Apps, VirtualKey.Application),
        (EsKey.LeftCtrl, VirtualKey.LeftControl),
        (EsKey.RightCtrl, VirtualKey.RightControl),
        (EsKey.LeftAlt, VirtualKey.LeftMenu),
        (EsKey.RightAlt, VirtualKey.RightMenu),
        // 入力方式（IME）のキー
        (EsKey.HangulMode, VirtualKey.Hangul),
        (EsKey.KanaMode, VirtualKey.Kana),
        (EsKey.JunjaMode, VirtualKey.Junja),
        (EsKey.FinalMode, VirtualKey.Final),
        (EsKey.HanjaMode, VirtualKey.Hanja),
        (EsKey.KanjiMode, VirtualKey.Kanji),
        (EsKey.ImeConvert, VirtualKey.Convert),
        (EsKey.ImeNonConvert, VirtualKey.NonConvert),
        (EsKey.ImeAccept, VirtualKey.Accept),
        (EsKey.ImeModeChange, VirtualKey.ModeChange),
        // 数字キーとテンキー（WinUI 3 は体系名を使う）
        (EsKey.D0, VirtualKey.Number0),
        (EsKey.D1, VirtualKey.Number1),
        (EsKey.D2, VirtualKey.Number2),
        (EsKey.D3, VirtualKey.Number3),
        (EsKey.D4, VirtualKey.Number4),
        (EsKey.D5, VirtualKey.Number5),
        (EsKey.D6, VirtualKey.Number6),
        (EsKey.D7, VirtualKey.Number7),
        (EsKey.D8, VirtualKey.Number8),
        (EsKey.D9, VirtualKey.Number9),
        (EsKey.NumPad0, VirtualKey.NumberPad0),
        (EsKey.NumPad1, VirtualKey.NumberPad1),
        (EsKey.NumPad2, VirtualKey.NumberPad2),
        (EsKey.NumPad3, VirtualKey.NumberPad3),
        (EsKey.NumPad4, VirtualKey.NumberPad4),
        (EsKey.NumPad5, VirtualKey.NumberPad5),
        (EsKey.NumPad6, VirtualKey.NumberPad6),
        (EsKey.NumPad7, VirtualKey.NumberPad7),
        (EsKey.NumPad8, VirtualKey.NumberPad8),
        (EsKey.NumPad9, VirtualKey.NumberPad9),
        // ブラウザー キー（WinUI 3 は操作名を使う）
        (EsKey.BrowserBack, VirtualKey.GoBack),
        (EsKey.BrowserForward, VirtualKey.GoForward),
        (EsKey.BrowserRefresh, VirtualKey.Refresh),
        (EsKey.BrowserStop, VirtualKey.Stop),
        (EsKey.BrowserSearch, VirtualKey.Search),
        (EsKey.BrowserFavorites, VirtualKey.Favorites),
        (EsKey.BrowserHome, VirtualKey.GoHome),
    ];

    /// <summary>名前が異なる組の順方向の対応表（<see cref="EsKey"/> → <see cref="VirtualKey"/>）。</summary>
    private static readonly Dictionary<EsKey, VirtualKey> s_esToVirtual =
        s_aliases.ToDictionary(static pair => pair.Es, static pair => pair.Virtual);

    /// <summary>名前が異なる組の逆方向の対応表（<see cref="VirtualKey"/> → <see cref="EsKey"/>）。</summary>
    private static readonly Dictionary<VirtualKey, EsKey> s_virtualToEs = CreateReverseTable();

    /// <summary>逆方向の対応表を生成する。</summary>
    /// <returns>同じ <see cref="VirtualKey"/> へ写る組のうち、先に宣言した <see cref="EsKey"/> を代表とする表。</returns>
    /// <remarks>
    /// 同じ物理キーを表す複数の <see cref="EsKey"/>（<see cref="EsKey.Return"/> と <see cref="EsKey.Enter"/> 等）は<br/>
    /// 同じ <see cref="VirtualKey"/> へ写るため、1 対 1 の反転では表を作れない（先着を代表とする）。
    /// </remarks>
    private static Dictionary<VirtualKey, EsKey> CreateReverseTable()
    {
        var table = new Dictionary<VirtualKey, EsKey>();

        foreach (var (es, virtualKey) in s_aliases)
        {
            table.TryAdd(virtualKey, es);
        }

        return table;
    }

    /// <summary><see cref="EsKey"/> を WinUI 3 の <see cref="VirtualKey"/> へ変換する。</summary>
    /// <param name="key">変換対象のキー。</param>
    /// <returns>対応する WinUI 3 のキー。 対応先がない場合（未定義値・WinUI 3 が表現できないキー）は <see cref="VirtualKey.None"/>。</returns>
    /// <remarks>
    /// 1. 名前が異なるキー（<see cref="EsKey.Return"/> 等）を別名表で解決する<br/>
    /// 2. 未定義値（<c>(EsKey)999</c> 等）は <see cref="EsUtil.Platform.Core.Common.EnumCanonical"/> が検出し <see cref="VirtualKey.None"/> を返す<br/>
    ///    （<c>Enum.TryParse</c> は数値文字列を受け付けてしまうため、名前解決の成否だけでは検出できない）<br/>
    /// 3. 名前が一致する WinUI 3 のメンバーへ変換する
    /// </remarks>
    public static VirtualKey ToVirtualKey(this EsKey key)
    {
        if (s_esToVirtual.TryGetValue(key, out var aliased))
        {
            return aliased;
        }

        return EnumCanonical.TryParse<EsKey, VirtualKey>(key, out var result) ? result : VirtualKey.None;
    }

    /// <summary>WinUI 3 の <see cref="VirtualKey"/> を <see cref="EsKey"/> へ変換する。</summary>
    /// <param name="key">変換対象のキー。</param>
    /// <returns>対応する <see cref="EsKey"/>。 対応先がない場合（未定義値・キー以外の入力）は <see cref="EsKey.None"/>。</returns>
    /// <remarks>
    /// 1. 名前が異なるキー（<see cref="VirtualKey.Enter"/> 等）を別名表で解決する<br/>
    /// 2. 未定義値は <see cref="EsUtil.Platform.Core.Common.EnumCanonical"/> が検出し <see cref="EsKey.None"/> を返す<br/>
    /// 3. 名前が一致する <see cref="EsKey"/> のメンバーへ変換する<br/>
    /// <b>キー以外の入力</b>（<c>LeftButton</c> / <c>GamepadA</c> 等）は <see cref="EsKey"/> に同名のメンバーがないため <see cref="EsKey.None"/> になる。
    /// </remarks>
    public static EsKey ToEsKey(this VirtualKey key)
    {
        if (s_virtualToEs.TryGetValue(key, out var aliased))
        {
            return aliased;
        }

        return EnumCanonical.TryParse<VirtualKey, EsKey>(key, out var result) ? result : EsKey.None;
    }

    /// <summary>WinUI 3 が表現できる <see cref="EsKey"/> かどうかを判定する。</summary>
    /// <param name="key">判定対象のキー。</param>
    /// <returns>WinUI 3 に対応するキーがある場合は true。</returns>
    public static bool IsRepresentable(this EsKey key) => key.ToVirtualKey() != VirtualKey.None || key == EsKey.None;
}
