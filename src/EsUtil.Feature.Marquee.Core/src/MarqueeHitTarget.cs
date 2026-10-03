namespace EsUtil.Feature.Marquee;

/// <summary>ドラッグハンドルの種別。</summary>
/// <remarks>
/// ヒットテスト・矩形変形・nudge・描画で共通利用する。<br/>
/// 上位（ホスト）がカーソル形状のマッピングに使用するため public で公開する。
/// </remarks>
public enum MarqueeHitTarget
{
    /// <summary>なし。</summary>
    None,

    /// <summary>左上。</summary>
    TopLeft,

    /// <summary>上中央。</summary>
    TopCenter,

    /// <summary>右上。</summary>
    TopRight,

    /// <summary>中左。</summary>
    MiddleLeft,

    /// <summary>中右。</summary>
    MiddleRight,

    /// <summary>左下。</summary>
    BottomLeft,

    /// <summary>下中央。</summary>
    BottomCenter,

    /// <summary>右下。</summary>
    BottomRight,

    /// <summary>移動（領域全体）。</summary>
    Move,
}
