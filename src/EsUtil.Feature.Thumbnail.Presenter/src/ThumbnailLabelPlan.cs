using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Thumbnail;

/// <summary>サムネイルの名前 1 件分の描画計画（文言・配置・体裁）。</summary>
/// <param name="Text">表示する名前。</param>
/// <param name="Bounds">名前の行の矩形（一覧の内容の座標）。</param>
/// <param name="FontSize">文字サイズ（DIP）。</param>
/// <param name="IsBold">太字にするかどうか。</param>
/// <param name="ForegroundArgb">文字色（<c>0xAARRGGBB</c>。 0 は UI の標準色）。</param>
/// <param name="VerticalPadding">上下の余白（DIP）。</param>
/// <remarks>
/// <b>テキストを要素に写す配置計算は提示層が持つ</b>: 文言・矩形・文字サイズ・太字・色を解決し、<br/>
/// 要素として置くか文字として描くかは UI が選ぶ。<br/>
/// <br/>
/// <b>色が 0 のとき</b>: UI の標準の文字色を使う（テーマの前色。 外観が色を指定しない場合）。<br/>
/// <br/>
/// <b>ホストが生成できる</b>: <see cref="ThumbnailDrawPlan"/> の一部として、独自の提示<br/>
/// （<see cref="ThumbnailPresenter"/> の派生）から渡せる。
/// </remarks>
public readonly record struct ThumbnailLabelPlan(
    string Text,
    EsRect Bounds,
    double FontSize,
    bool IsBold,
    uint ForegroundArgb,
    double VerticalPadding);
