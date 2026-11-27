namespace EsUtil.Platform.Core.Logging;

/// <summary>ログに載せる文字列の整形（自由入力の値を 1 行・先頭 100 文字へそろえる）。</summary>
/// <remarks>
/// <b>値は大きくなりうる</b>: 本文のように自由入力の値は、そのまま記録すると 1 行が数百 KB になる。<br/>
/// 障害の切り分けに要るのは先頭の内容だけであるため、改行を 1 行にまとめ、100 文字で切って末尾に <c>…</c> を付ける。<br/>
/// <b>1 か所で持つ</b>: 切り詰めの長さと改行の表し方は機能をまたいで同じにする（記録の体裁を機能ごとに変えない）。
/// </remarks>
public static class LogText
{
    /// <summary>ログに載せる 1 行の最大文字数。</summary>
    public const int MaxPreviewLength = 100;

    /// <summary>ログに載せる文字列を整形する（改行を 1 行にし、先頭 100 文字へ切り詰める）。</summary>
    /// <param name="text">対象の文字列。</param>
    /// <returns>整形した文字列。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> が null の場合。</exception>
    public static string ToPreview(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var singleLine = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", "\\n", StringComparison.Ordinal);
        return singleLine.Length <= MaxPreviewLength
            ? singleLine
            : string.Concat(singleLine.AsSpan(0, MaxPreviewLength), "…");
    }
}
