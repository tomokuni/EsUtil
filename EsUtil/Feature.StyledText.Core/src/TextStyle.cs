using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.StyledText.Core.Defaults;

namespace EsUtil.Feature.StyledText;

/// <summary>テキストの書式（フォント・大きさ・色・装飾）。</summary>
/// <remarks>
/// <b>文字単位で変えられる単位</b>である: 吹き出しの本文は <see cref="TextRun"/> の並びで持ち、
/// 各 run が自分の <see cref="TextStyle"/> を保持する（1 文字だけ色を変える等ができる）。<br/>
/// <b>大きさは元画像ピクセル</b>である（画面の DIP ではない）。 表示倍率を変えても保存画像との比率が変わらない
/// （規則の所有者は <c>.github/コーディング規約.md</c> §1）。<br/>
/// <b>書式は解決済みで保存する</b>: 既定の書式を後から変えても、保存済みの注釈の見た目は変わらない。
/// </remarks>
/// <param name="FontFamily">フォント ファミリ名（例: <c>Yu Gothic UI</c>）。 空の場合は <see cref="TextDefaults.PreferredFontFamilies"/> の先頭を使う。</param>
/// <param name="FontSize">文字の大きさ（元画像ピクセル）。</param>
/// <param name="Color">文字色（不透明度を含む）。</param>
/// <param name="IsBold">太字にするかどうか。</param>
/// <param name="IsItalic">斜体にするかどうか。</param>
/// <param name="IsUnderline">下線を引くかどうか。</param>
/// <param name="IsStrike">取り消し線を引くかどうか。</param>
public sealed record TextStyle(
    string FontFamily,
    double FontSize,
    DrawingColor Color,
    bool IsBold = false,
    bool IsItalic = false,
    bool IsUnderline = false,
    bool IsStrike = false)
{
    /// <summary>既定の書式（フォントは <see cref="TextDefaults.PreferredFontFamilies"/> の先頭、20 px、黒、装飾なし）を取得する。</summary>
    public static TextStyle Default { get; } = new(
        TextDefaults.PreferredFontFamilies[0],
        TextDefaults.FontSizeDefault,
        DrawingColor.FromArgb(255, 0, 0, 0));

    /// <summary>安全域へ補正した書式を取得する。</summary>
    /// <returns>フォント名が空なら既定のフォント、大きさが範囲外なら最も近い境界へ補正した書式。</returns>
    /// <remarks>数値の安全域は <see cref="TextLimits"/> が単一所有する（設定・JSON・入力のどの経路でも同じ補正になる）。</remarks>
    public TextStyle Normalize() => this with
    {
        FontFamily = string.IsNullOrWhiteSpace(FontFamily)
            ? TextDefaults.PreferredFontFamilies[0]
            : FontFamily,
        FontSize = TextLimits.CoerceFontSize(FontSize),
    };
}
