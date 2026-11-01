using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Thumbnail;

/// <summary>名前を折り返して描いたときの実寸を測る関数。</summary>
/// <param name="text">測る文言。</param>
/// <param name="maxWidth">折り返す幅（DIP。 この幅を超える位置で折り返す）。</param>
/// <param name="fontSize">文字サイズ（DIP）。</param>
/// <param name="isBold">太字で描くかどうか。</param>
/// <returns>折り返した後の実寸（DIP。 幅は使った幅、高さは全行の合計）。</returns>
/// <remarks>
/// <b>描画 API を持つ UI 層が渡す</b>: コアは描画 API を持たないため、実寸の測定だけを関数として受け取る。<br/>
/// <b>折り返しの判定を UI 層へ任せる理由</b>: 折り返しの位置は文字の送り幅（フォント・DPI）に依存し、<br/>
/// 文字数からの見積もりでは実際の行数と一致しない（見積もりより実際の行数が多いと、<br/>
/// 省略したはずの名前がさらに折り返されて切れる。 実測 2026-11-01）。<br/>
/// <b>純粋関数にすること</b>: 同じ入力には同じ寸法を返す（名前の文言の決定が呼び出しの時点に依存しない）。
/// </remarks>
public delegate EsSize ThumbnailTextMeasure(string text, double maxWidth, double fontSize, bool isBold);
