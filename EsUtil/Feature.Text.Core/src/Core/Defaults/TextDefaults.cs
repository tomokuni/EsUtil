using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Text.Core.Defaults;

/// <summary>書式付きの文字列の既定値（フォントの優先順・文字の大きさ・既定の書式）。</summary>
/// <remarks>
/// <b>既定値は 1 か所に置く</b>: 設定ページの初期値・新規作成時の値・JSON の欠落値が同じ値になる
/// （機能ごとに書き分けない。 規則の所有者は <c>.github/コーディング規約.md</c> §1）。<br/>
/// 設定で変えられるのは<b>新規作成時の既定値</b>であり、既存の文字の書式は変わらない（書式は文字ごとに解決して保存する）。
/// </remarks>
public static class TextDefaults
{
    /// <summary>使うフォントの優先順（先頭から順に、見つかった最初のものを使う）。</summary>
    /// <remarks>
    /// 先頭は <c>Yu Gothic UI</c>（日本語の画面で標準的に使える UI フォント）、
    /// 無い環境では <c>Segoe UI</c> を使う（両方無い場合は UI の既定フォントへ落ちる）。
    /// </remarks>
    public static IReadOnlyList<string> PreferredFontFamilies { get; } = ["Yu Gothic UI", "Segoe UI"];

    /// <summary>文字の大きさの既定値（元画像ピクセル）。</summary>
    public const double FontSizeDefault = 20.0;

    /// <summary>既定の書式（フォントは <see cref="PreferredFontFamilies"/> の先頭、<see cref="FontSizeDefault"/>、黒、装飾なし）を取得する。</summary>
    public static TextStyle DefaultStyle { get; } = new(
        PreferredFontFamilies[0],
        FontSizeDefault,
        DrawingColor.FromArgb(255, 0, 0, 0));
}