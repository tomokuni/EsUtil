namespace EsUtil.Platform.Core;

/// <summary>ホットキーの組合せ（修飾キー＋キー）を表す値オブジェクト。</summary>
/// <remarks>
/// <see cref="Modifiers"/> と <see cref="Key"/> の組合せで一意のホットキーを定義する。<br/>
/// <c>ToString()</c> で "Ctrl + Shift + Q" のようなユーザー可読な文字列を返す。<br/>
/// <see cref="Key"/> は UI フレームワーク非依存の <see cref="EsKey"/> を使用する。 各 UI 実装は、
/// 自身のキー型との相互変換を Interop 層で提供する（本プロジェクトは UI 型を参照しない）。<br/>
/// </remarks>
/// <param name="Modifiers">修飾キー（Alt / Control / Shift / Windows の組合せ）</param>
/// <param name="Key">ホットキーとして割り当てるキー（<see cref="EsKey"/>）</param>
public sealed record HotKeySetting(HotKeyModifiers Modifiers, EsKey Key)
{
    /// <summary>ホットキーのユーザー可読な文字列表現（例: "Ctrl + Shift + Q"）を取得する。</summary>
    public override string ToString()
    {
        var parts = new List<string>(4);
        if ((Modifiers & HotKeyModifiers.Control) != 0) parts.Add("Ctrl");
        if ((Modifiers & HotKeyModifiers.Alt) != 0) parts.Add("Alt");
        if ((Modifiers & HotKeyModifiers.Shift) != 0) parts.Add("Shift");
        if ((Modifiers & HotKeyModifiers.Windows) != 0) parts.Add("Win");
        parts.Add(KeyToSymbolString(Key));
        return string.Join(" + ", parts);
    }

    private static string KeyToSymbolString(EsKey key) => key switch
    {
        EsKey.D0 => "0",
        EsKey.D1 => "1",
        EsKey.D2 => "2",
        EsKey.D3 => "3",
        EsKey.D4 => "4",
        EsKey.D5 => "5",
        EsKey.D6 => "6",
        EsKey.D7 => "7",
        EsKey.D8 => "8",
        EsKey.D9 => "9",
        EsKey.OemTilde => "`",
        EsKey.OemMinus => "-",
        EsKey.OemPlus => "=",
        EsKey.OemOpenBrackets => "[",
        EsKey.OemCloseBrackets => "]",
        EsKey.OemPipe => "\\",
        EsKey.OemSemicolon => ";",
        EsKey.OemComma => ",",
        EsKey.OemPeriod => ".",
        EsKey.OemQuestion => "/",
        _ => key.ToString()
    };
}
