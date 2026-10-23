using System.Globalization;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Platform.Core.Imaging;

/// <summary>描画色（<see cref="DrawingColor"/>）の判定を提供する拡張メソッド。</summary>
/// <remarks>
/// 「アルファが 0 の色は描かない」という判定を単一所有する。<br/>
/// 各外観型（暗転・枠線・塗り・十字線）が <c>Color.A &gt; 0</c> を書くと、<br/>
/// 同じ意味の判定が複数箇所へ散り、片方だけが透明を描く（不可視の描画要求が UI へ届く）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class DrawingColorExtensions
{
    /// <summary>色が可視（アルファが 0 より大きい）かどうかを判定する。</summary>
    /// <param name="color">判定する色。</param>
    /// <returns>アルファが 0 より大きい場合は true。 アルファ 0（完全な透明）の場合は false。</returns>
    /// <remarks>判定に使うのはアルファのみである（RGB は描画に現れないため見ない）。</remarks>
    public static bool IsVisible(this DrawingColor color) => color.A > 0;

    /// <summary>色を <c>#AARRGGBB</c> 形式の文字列へ整形する。</summary>
    /// <param name="color">対象の色。</param>
    /// <returns><c>#AARRGGBB</c> 形式の文字列（例: <c>#80FF0000</c>）。</returns>
    /// <remarks>
    /// ログへ色を出す用途に使う（外観の変更ログの書式を 1 か所に集約する）。<br/>
    /// 標準の <c>ToString</c>（<c>Color [A=…, R=…, G=…, B=…]</c>）より短く、アルファを先頭に置くため<br/>
    /// 「描くかどうか（アルファ 0 か）」の判別も同じ表記でできる。
    /// </remarks>
    public static string ToHexText(this DrawingColor color) => $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    /// <summary><c>#AARRGGBB</c> / <c>#RRGGBB</c> 形式の文字列を色へ変換する。</summary>
    /// <param name="text">色の文字列（前後の空白と先頭の <c>#</c> は省略できる）。 null 可。</param>
    /// <param name="color">変換した色。 変換できなかった場合は <see cref="DrawingColor.Empty"/>。</param>
    /// <returns>変換できた場合は true。</returns>
    /// <remarks>
    /// <b>桁数で意味が変わる</b>: 8 桁は <c>AARRGGBB</c>、6 桁は <c>RRGGBB</c>（アルファは不透明として補う）。<br/>
    /// <b>それ以外の桁数・16 進数でない文字は拒否する</b>（<c>Enum.TryParse</c> と同種の「緩い解釈」をしない）。<br/>
    /// 設定ファイルの色は各 UI が自 UI の色型で保持するが、<b>文字列の解釈は本メソッドが単一所有する</b>
    /// （UI ごとに桁数・大文字小文字・<c>#</c> の扱いがずれるのを防ぐ）。
    /// </remarks>
    public static bool TryParseHexText(string? text, out DrawingColor color)
    {
        color = DrawingColor.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var span = text.AsSpan().Trim();
        if (span.Length > 0 && span[0] == '#')
        {
            span = span[1..];
        }

        if (span.Length == 8)
        {
            if (!uint.TryParse(span, NumberStyles.HexNumber, null, out var argb))
            {
                return false;
            }

            color = DrawingColor.FromArgb(
                (byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
            return true;
        }

        if (span.Length == 6 && uint.TryParse(span, NumberStyles.HexNumber, null, out var rgb))
        {
            color = DrawingColor.FromArgb(0xFF, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            return true;
        }

        return false;
    }
}
