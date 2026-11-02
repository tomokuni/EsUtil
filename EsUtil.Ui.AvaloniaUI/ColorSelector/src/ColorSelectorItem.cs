namespace EsUtil.Ui.AvaloniaUI.ColorSelector;

/// <summary>一覧の 1 件（表示する名前と、選んだときに適用する色の文字列）。</summary>
/// <param name="Label">一覧に表示する名前。</param>
/// <param name="Value">適用する色の文字列（色名または <c>#AARRGGBB</c>）。</param>
/// <param name="Category">分類（ドロップダウン上部のタブ）。</param>
/// <remarks>
/// <b>名前と適用値を分ける理由</b>: テーマの色は「テーマ: 背景」のような読みやすい名前を表示しつつ、<br/>
/// 適用する値はそのときの色（<c>#AARRGGBB</c>）でなければならない（名前からは色を解決できない）。<br/>
/// 色名の一覧（<see cref="EsUtil.Platform.Core.Palette.ColorEntry"/>）は名前と値が同じため、両方に名前を入れる。
/// </remarks>
internal sealed record ColorSelectorItem(string Label, string Value, ColorSelectorCategory Category);
