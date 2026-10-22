
namespace EsUtil.Ui.WinForms.Interop.Imaging;

/// <summary>GDI+ のラベル 1 件（背景 + 文字）の描画順を単一所有する。</summary>
/// <remarks>
/// 「背景を塗ってから文字を描く」という順序は、塗りと <see cref="GdiText.Draw"/> を<br/>
/// 呼ぶ順序そのものであり、各 UI・各ホストが書くと順序（重なり）がずれうるため本クラスへ集約する。<br/>
/// <b>テキストの実寸測定と配置の計算は呼び出し側が行う</b>（余白・行送り・クランプの規則は各コントロールのコアが単一所有する）。<br/>
/// 背景の角丸にも対応する（角丸半径 0 以下は四角形として塗る）。
/// </remarks>
public static class GdiLabel
{
    /// <summary>ラベル 1 件（背景 + 文字）を描画する。</summary>
    /// <param name="graphics">描画先（DPI の拡大が適用されていてよい）。 null 不可。</param>
    /// <param name="background">背景の矩形（コア座標）。 幅・高さが 0 以下の場合は背景を塗らない。</param>
    /// <param name="backgroundColor">背景色（不透明度はアルファ）。</param>
    /// <param name="text">描画する文字列。 null 不可。</param>
    /// <param name="textOrigin">文字の左上原点（背景の内側）。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <param name="textColor">文字色。</param>
    /// <param name="cornerRadius">背景の角丸半径（DIP）。 0 以下は四角形（既定）。</param>
    /// <remarks>
    /// 1. 背景（<see cref="GdiFill.RoundRect"/>。 角丸 0 は四角形）を塗る。<br/>
    /// 2. 文字（<see cref="GdiText.Draw"/>）を左上原点で描く。<br/>
    /// <br/>
    /// 文字は背景の後に描く（先に描くと背景に隠れる）。 本メソッドが順序を保証する。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="graphics"/> または <paramref name="text"/> が null の場合。</exception>
    public static void Draw(
        Graphics graphics,
        EsRect background,
        Color backgroundColor,
        string text,
        EsPoint textOrigin,
        double fontSize,
        Color textColor,
        double cornerRadius = 0)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(text);

        GdiFill.RoundRect(graphics, background, cornerRadius, backgroundColor);
        GdiText.Draw(graphics, text, textOrigin.X, textOrigin.Y, fontSize, textColor);
    }
}
