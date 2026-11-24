namespace EsUtil.Feature.StyledText;

/// <summary>同じ書式で続く文字列のまとまり（文字単位の書式の単位）。</summary>
/// <remarks>
/// <b>run が自分の文字列を持つ理由</b>: 文字列全体と範囲指定で持つ方式は、文字を挿入するたびに
/// すべての範囲の境界をずらす必要があり、ずれると書式が壊れる。 run 方式なら挿入・削除は
/// その run の中だけで完結する。<br/>
/// <b>書式は解決済み</b>である（<see cref="TextStyle"/> をそのまま保存する）。
/// </remarks>
/// <param name="Text">文字列（空でもよい）。</param>
/// <param name="Style">この文字列に適用する書式。</param>
public sealed record TextRun(string Text, TextStyle Style);
