namespace EsUtil.Platform.Core.Palette;

/// <summary>色の入力欄へ表示する表記。</summary>
/// <remarks>
/// <b>保存値は変わらない</b>: 表記は画面に出す文字列だけを決める（保存値は常に <c>#AARRGGBB</c>）。
/// 入力欄は編集できるため、利用者が表記と異なる形（色名・16 進）を入力しても受理する
/// （受理する形は <see cref="ColorString.TryParse(string, out System.Drawing.Color, out double)"/> が単一所有する）。
/// </remarks>
public enum ColorTextFormat
{
    /// <summary>色名と不透明度（例: <c>Red</c> / <c>Orange+75%</c>）。 色名が無い色は 16 進で表す。</summary>
    NameWithAlpha = 0,

    /// <summary>カラーコード（例: <c>#FFFF0000</c>）。 アルファが先頭（保存値と同じ形）。</summary>
    HexCode = 1,
}