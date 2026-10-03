namespace EsUtil.Feature.Marquee;

/// <summary>選択矩形の UI 層が描く層（描画順の契約）。</summary>
/// <remarks>
/// <b>描画順は描画層が単一所有する</b>: UI 層は <c>MarqueePresenter.ResolveDrawPlan</c> が返す
/// <see cref="MarqueeDrawPlan.Layers"/> を先頭から順に処理するだけで、順序を判断しない。<br/>
/// <b>テキスト（<see cref="Label"/>）は UI 要素でも描ける</b>: 要素（<c>TextBlock</c> 等）として置く UI は
/// <c>TextBlock</c> の描画を本層に頼らず、要素を子として置く（親より後に描かれるため常に最上位になる）。<br/>
/// その場合、描画実装は本層を無視してよい（基底のフックの既定は何もしない）。<br/>
/// canvas へ直接描く実装は本層に従ってテキストを描く。<br/>
/// <br/>
/// <b>UI 層の実装</b>: 本層の順序に従って自 UI の描画 API へ写す。
/// </remarks>
public enum MarqueeDrawLayer
{
    /// <summary>選択矩形の外側・内側に描く装飾（暗転マスク・外枠・枠線・内側の塗り。 描く値がある場合のみ）。</summary>
    Decoration,

    /// <summary>ドラッグハンドル（選択がある場合のみ）。</summary>
    Handles,

    /// <summary>始点マーカー（nudge 中のみ）。</summary>
    Marker,

    /// <summary>テキスト（サイズラベル・カーソル座標ラベル。 テキストがある場合のみ。 常に最上位）。</summary>
    /// <remarks>UI 要素として置く UI は本層を無視してよい（基底のフックの既定は何もしない）。</remarks>
    Label,
}
