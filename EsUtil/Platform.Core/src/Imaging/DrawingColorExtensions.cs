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
}
