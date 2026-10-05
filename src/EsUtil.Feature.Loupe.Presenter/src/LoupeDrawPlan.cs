using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Loupe;

/// <summary>ルーペの描画 1 フレーム分の計画（描画層が解決した値を UI 層へ渡す唯一の契約）。</summary>
/// <param name="Appearance">外観（色・フォント・角丸・枠線の太さ・表示可否）。</param>
/// <param name="ClientPixelRect">描画面の全体（左上を原点とし、幅・高さが物理ピクセルサイズの正方形）。</param>
/// <param name="MagnifiedImage">拡大画像の切り出しと描画の矩形。 null の場合は描かない（拡大中心未設定）。</param>
/// <param name="CrosshairPixelRects">十字線の矩形（縦 1 本・横 1 本。 表示可否では消さない）。</param>
/// <param name="BorderPixelRects">枠線の矩形（上・下・左・右の 4 辺。 表示可否では消さない）。</param>
/// <param name="Layers">描く値のある層とその順序（<b>描画順は描画層が単一所有する</b>）。 先頭から順に処理する。</param>
/// <param name="Labels">テキスト（中心座標ラベル）の配置指示。 拡大中心が未設定の場合は空。</param>
/// <param name="CaptureTopLeft">切り出し領域の左上（対象座標）。 中心未設定時は null（描画完了通知の組み立てに使う）。</param>
/// <param name="ZoomLevel">拡大率（描画完了通知の組み立てに使う）。</param>
/// <param name="ClientPixelSize">描画に使う一辺の物理ピクセルサイズ（描画完了通知の組み立てに使う）。</param>
/// <remarks>
/// <b>描く値はすべて本値に解決済み</b>である（描画実装は計算を持たず、<paramref name="Layers"/> の順に写すだけ）。<br/>
/// したがって描画実装が幾何計算（矩形の導出・寸法の換算・切り出し領域の算出）を行うことはない。<br/>
/// <b>表示可否（<see cref="LoupeAppearance"/> の <c>Show*</c>）は値も層も消さない</b>:<br/>
/// 値の有無は幾何だけで決まるため、表示を止めても計算値はそのまま残る（描画側がフラグを見て描くかどうかを決める）。<br/>
/// したがって「描く必要があるか」は <see cref="HasText"/> のように表示可否を適用した述語で判定する<br/>
/// （描画実装は層ごとに外観のフラグを見る）。<br/>
/// <b>文字は <paramref name="Labels"/> として値を渡す</b>: UI 層が要素として置くか画像として描くかを決める<br/>
/// （要素は親より後に描かれるため常に最上位になる。 表示可否は各配置指示が持つ）。<br/>
/// <b>拡大元の画像を含めない</b>: 型が UI に依存するため、描画の起動時に引数で受け取る<br/>
/// （切り出す領域と描画の矩形は <paramref name="MagnifiedImage"/> が単一所有する）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public readonly record struct LoupeDrawPlan(
    LoupeAppearance Appearance,
    EsRect ClientPixelRect,
    LoupeMagnifiedImage? MagnifiedImage,
    IReadOnlyList<EsRect> CrosshairPixelRects,
    IReadOnlyList<EsRect> BorderPixelRects,
    IReadOnlyList<LoupeDrawLayer> Layers,
    IReadOnlyList<LoupeLabelPlacement> Labels,
    EsPoint? CaptureTopLeft,
    int ZoomLevel,
    int ClientPixelSize)
{
    /// <summary>テキスト（中心座標ラベル）を描く必要があるかどうかを取得する。</summary>
    /// <value>表示が有効なラベルを持つ場合は true。</value>
    /// <remarks>表示可否（<see cref="LoupeAppearance"/> の <c>ShowCoordLabel</c>）まで適用した結果を返す（計画は可否で値を消さないため、本値で判定できる）。</remarks>
    public bool HasText
    {
        get
        {
            // LINQ を避ける（インデックス可能なコレクションのため、規約どおりインデクサーを使う）
            for (var index = 0; index < Labels.Count; index++)
            {
                if (Labels[index].IsVisible)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
