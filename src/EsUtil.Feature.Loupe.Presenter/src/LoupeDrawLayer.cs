namespace EsUtil.Feature.Loupe;

/// <summary>ルーペの UI 層が描く層（描画順の契約）。</summary>
/// <remarks>
/// <b>描画順は描画層（<see cref="LoupePresenter"/>）が単一所有する</b>: UI 層は <see cref="LoupeDrawPlan.Layers"/> を先頭から順に処理するだけで、<br/>
/// 順序を判断しない（描画計画の解決は <see cref="LoupePresenter.ResolveDrawPlan"/>。 描画は UI 層が行う）。<br/>
/// <b>テキスト（<see cref="Label"/>）は UI 要素でも描ける</b>: 要素（<c>TextBlock</c> 等）として置く UI は<br/>
/// 要素を子として置く（親より後に描かれるため常に最上位になる）。
/// その場合、描画実装は本層を無視してよい（基底のフックの既定は何もしない）。<br/>
/// canvas へ直接描く実装は本層に従ってテキストを描く。<br/>
/// <br/>
/// <b>UI 層の実装</b>: 本層の順序に従って自 UI の描画 API へ写す。
/// </remarks>
public enum LoupeDrawLayer
{
    /// <summary>画像外（ペーストボード）の色。 常に含む（画像より先に全面へ塗る）。</summary>
    Pasteboard,

    /// <summary>拡大画像（切り出し領域を解決できた場合のみ。 拡大中心未設定の場合は含まない）。</summary>
    Image,

    /// <summary>十字線（表示の設定が有効な場合のみ）。</summary>
    Crosshair,

    /// <summary>枠線（表示の設定が有効な場合のみ）。</summary>
    Border,

    /// <summary>テキスト（中心座標ラベル。 テキストがある場合のみ。 常に最上位）。</summary>
    /// <remarks>UI 要素として置く UI は本層を無視してよい（基底のフックの既定は何もしない）。</remarks>
    Label,
}
