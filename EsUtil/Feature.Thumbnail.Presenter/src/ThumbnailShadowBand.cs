namespace EsUtil.Feature.Thumbnail;

/// <summary>影の 1 画素分の帯（物理ピクセルの矩形と色）。</summary>
/// <param name="X">左端（物理ピクセル）。</param>
/// <param name="Y">上端（物理ピクセル）。</param>
/// <param name="Width">幅（物理ピクセル）。</param>
/// <param name="Height">高さ（物理ピクセル）。</param>
/// <param name="Argb">塗る色（<c>0xAARRGGBB</c>）。 透過度が影の濃さ。</param>
/// <remarks>
/// <b>1 画素の帯を単色で塗る理由</b>: グラデーション ブラシの端の扱いは描画 API ごとに異なり、<br/>
/// GDI+ は軸の外側を繰り返して<b>最も濃い色の 1 画素</b>を外側へ描く（実測: 影の外側に線が見える）。<br/>
/// 濃さを画素単位で計画しておけば、どの UI 版も「単色の矩形を塗る」だけで同じ結果になる。
/// </remarks>
public readonly record struct ThumbnailShadowBand(int X, int Y, int Width, int Height, uint Argb);
