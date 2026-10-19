namespace EsUtil.Feature.Thumbnail;

/// <summary>サムネイル 1 件を描く層（描く順序）。</summary>
/// <remarks>
/// 層の順序は提示層が単一所有する（UI ごとに順序を決めると、影と背景の重なりが UI で変わる）。<br/>
/// 影は背景の上・画像の下へ描く（画像が影を覆い、影の内側が画像の縁の混色の下地になる）。
/// </remarks>
public enum ThumbnailDrawLayer
{
    /// <summary>項目の背景（ホバー・選択の帯）。</summary>
    Background,

    /// <summary>画像の周囲の影。</summary>
    Shadow,

    /// <summary>画像。</summary>
    Image,

    /// <summary>名前。</summary>
    Label,
}
