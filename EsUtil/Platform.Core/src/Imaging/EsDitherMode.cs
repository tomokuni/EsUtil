namespace EsUtil.Platform.Core.Imaging;

/// <summary>減色するときのディザ（誤差拡散）の指定。</summary>
/// <remarks>
/// <b>ディザとは何か</b>: 減色で残せる色は 256 色以下であるため、グラデーションのような連続した色は<b>縞（バンディング）</b>に
/// なる。 ディザは画素を置き換えたときの誤差をとなりへ配り、<b>離れて見ると元の色に戻る</b>ようにパレットの色を
/// 交互に置く。
///
/// <list type="table">
/// <item>
///   <term><see cref="None"/></term>
///   <description><b>既定</b>。 画素ごとに最も近いパレットの色へ置き換える。 <b>保存のファイルが最も小さくなる</b>
///   （縞は残る）。</description>
/// </item>
/// <item>
///   <term><see cref="ErrorDiffusion"/></term>
///   <description>誤差拡散（WIC の <c>WICBitmapDitherTypeErrorDiffusion</c>。 誤差を周囲の画素へ配る）。</description>
/// </item>
/// </list>
///
/// <b>測り方</b>: ディザは<b>1 画素ごとの誤差を大きくする</b>（1 つの画素は元の色から遠いパレットの色になるため）。
/// 目的は「離れて見たときに元の色に戻る」ことであるため、<b>8x8 ブロックの平均の差</b>で測る。
///
/// <list type="table">
/// <item>
///   <term>色数が足りない場合（16 色など）</term>
///   <description>1 画素ごとの誤差と知覚誤差は悪化するが、<b>8x8 ブロック平均の差は小さくなる</b>（<b>縞が消える</b>）。</description>
/// </item>
/// <item>
///   <term>色数が足りている場合（256 色など）</term>
///   <description>直すべき縞が無いのに画素へむらを加えるため、局所平均でもわずかに悪化する。</description>
/// </item>
/// <item>
///   <term>平坦な色の画像（スクリーンショット風）</term>
///   <description><b>変化しない</b>（誤差が 0 の画素には何も配られないため）。</description>
/// </item>
/// </list>
///
/// <b>勧めるのは色数が内容に対して足りないとき</b>である（縞が消え、8x8 ブロック平均の差が小さくなる）。
/// <b>256 色のように足りている場合は勧めない</b>。
///
/// <b>ファイルは大きくなる</b>: パレットの色を交互に置くと<b>隣り合う画素が変わる</b>ため、PNG の圧縮が効かなくなる
/// （<b>平坦な色の画像は変化しない</b>）。
///
/// <b>実装は WIC が持ちます</b>（本ライブラリは減色の計算を自前で持ちません。 判断の記録は
/// <see cref="EsReadOnlyBitmap.ReduceColors"/> の備考を参照）。
/// </remarks>
public enum EsDitherMode
{
    /// <summary>ディザを行わない（既定。 ファイルが最も小さい）。</summary>
    None,

    /// <summary>誤差拡散を行う（色数が足りないときの縞を消す。 ファイルは大きい）。</summary>
    ErrorDiffusion,
}
