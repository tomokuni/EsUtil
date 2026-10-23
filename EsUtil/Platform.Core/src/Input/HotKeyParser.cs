using EsUtil.Platform.Core.Common;

namespace EsUtil.Platform.Core.Input;

/// <summary>ホットキーの文字列表現（例: "Ctrl + Shift + Q"）を解釈する。</summary>
/// <remarks>
/// "Ctrl + Shift + Q" / "Ctrl+Shift+Q" / "ctrl + shift + q" の形式に対応する（大文字小文字は区別しない）。<br/>
/// <b>修飾キーが 1 つ以上必要</b>である（"Q" だけでは登録できないため失敗する）。<br/>
/// <b>キー名の解決</b>は <see cref="EnumCanonical.TryParse{TTarget}(string, out TTarget)"/> へ委譲する
/// （<c>Enum.TryParse</c> は数値文字列を受理するため、列挙に無い値がキーとして通る）。<br/>
/// UI 非依存の純粋関数である（設定画面の入力解釈を 4 つの UI 版が共有する）。
/// </remarks>
public static class HotKeyParser
{
    /// <summary>ホットキー文字列を解釈して <see cref="HotKeySetting"/> へ変換する。</summary>
    /// <param name="text">解釈する文字列。 null 可。</param>
    /// <param name="setting">変換結果。 解釈できない場合は修飾キーなし・<see cref="EsKey.None"/>。</param>
    /// <returns>解釈に成功した場合は true。</returns>
    /// <remarks>区切りは <c>+</c> であり、前後の空白は無視する。 最低 1 つの修飾キーを必要とする。</remarks>
    public static bool TryParse(string? text, out HotKeySetting setting)
    {
        setting = new HotKeySetting(HotKeyModifiers.None, EsKey.None);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        var modifiers = HotKeyModifiers.None;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            modifiers |= parts[i].ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" => HotKeyModifiers.Control,
                "SHIFT" => HotKeyModifiers.Shift,
                "ALT" => HotKeyModifiers.Alt,
                "WIN" or "WINDOWS" => HotKeyModifiers.Windows,
                _ => HotKeyModifiers.None,
            };
        }

        if (modifiers == HotKeyModifiers.None)
        {
            return false;
        }

        if (!EnumCanonical.TryParse<EsKey>(parts[^1], out var key) || key == EsKey.None)
        {
            return false;
        }

        setting = new HotKeySetting(modifiers, key);
        return true;
    }
}
