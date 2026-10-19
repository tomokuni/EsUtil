namespace EsUtil.Feature.Thumbnail;

/// <summary>画像の周囲へ確保する影の余白（DIP）。</summary>
/// <param name="Left">左辺の幅。</param>
/// <param name="Top">上辺の幅。</param>
/// <param name="Right">右辺の幅。</param>
/// <param name="Bottom">下辺の幅。</param>
public readonly record struct ThumbnailShadowInsets(double Left, double Top, double Right, double Bottom);
