using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation;

/// <summary>画像の表示倍率と位置の解決（単一所有。 4 つの UI 版が同じ結果になる）。</summary>
/// <remarks>
/// <b>表示矩形はホストが与える</b>: ズーム・パンの状態はプレビュー（ホスト）が所有するため、
/// 注釈レイヤーは「プレビュー内の画像の表示矩形」を受け取って使う。<br/>
/// <b>受け取った矩形は検証する</b>: レイアウト途中の値・別の座標系の値・古い値が入ると、注釈の位置が
/// 画像からずれる（実測 2026-11-21: ドラッグ中だけ倍率が 0.625 になり、注釈が右上へずれた）。
/// 縦横比が画像と一致しない・レイヤーの外へ出る矩形は捨てて、自分で中央へ収めた値を使う。<br/>
/// <b>座標は注釈レイヤーのローカル DIP</b>である（画面座標ではない）。
/// </remarks>
public static class AnnotationImageLayout
{
    /// <summary>縦横比の許容差（比の相対誤差）。</summary>
    private const double AspectTolerance = 0.02;

    /// <summary>画像の表示倍率と左上の位置を求める。</summary>
    /// <param name="clientSize">注釈レイヤーの大きさ（DIP）。</param>
    /// <param name="imageSize">元画像の大きさ（ピクセル）。</param>
    /// <param name="preview">プレビュー内の画像の表示矩形（無い場合は <see langword="null"/>）。</param>
    /// <returns>倍率と左上の位置（レイヤーのローカル DIP）。</returns>
    /// <remarks>受け取った表示矩形が疑わしい場合は、縦横比を保って中央へ収めた値を使う。</remarks>
    public static (double Scale, EsPoint Offset) Resolve(
        EsSize clientSize, EsSize imageSize, EsRect? preview)
    {
        var width = Math.Max(1, imageSize.Width);
        var height = Math.Max(1, imageSize.Height);
        var clientWidth = clientSize.Width;
        var clientHeight = clientSize.Height;
        if (clientWidth <= 0 || clientHeight <= 0)
        {
            return (1, default);
        }

        if (preview is { Width: > 0, Height: > 0 } rect && IsPlausible(rect, imageSize, clientWidth, clientHeight))
        {
            // 縦横比で倍率を決める（幅と高さで別々に求めると丸めの差で伸び縮みする）
            var scale = rect.Width / width;
            return (scale, new EsPoint(rect.X, rect.Y));
        }

        var fit = Math.Min(clientWidth / width, clientHeight / height);
        return (fit, new EsPoint(
            (clientWidth - (width * fit)) / 2,
            (clientHeight - (height * fit)) / 2));
    }

    /// <summary>受け取った表示矩形を使ってよいかどうかを判定する。</summary>
    /// <param name="rect">表示矩形（ローカル DIP）。</param>
    /// <param name="imageSize">元画像の大きさ（ピクセル）。</param>
    /// <param name="clientWidth">レイヤーの幅（DIP）。</param>
    /// <param name="clientHeight">レイヤーの高さ（DIP）。</param>
    /// <returns>使ってよい場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>レイヤーの外へはみ出す値も正しい</b>: 画像がプレビューより大きい（ズーム 100% で見切れる）場合、
    /// 表示矩形の左上は負になる。 はみ出しを不正とすると、実際の画像と注釈で別の倍率・位置を使って
    /// 右上へずれる（実測 2026-11-21）。 判定は「有限・正の大きさ・縦横比が画像と一致」だけにする。
    /// </remarks>
    private static bool IsPlausible(
        EsRect rect, EsSize imageSize, double clientWidth, double clientHeight)
    {
        _ = clientWidth;
        _ = clientHeight;

        if (rect.Width <= 0 || rect.Height <= 0
            || !double.IsFinite(rect.X) || !double.IsFinite(rect.Y)
            || !double.IsFinite(rect.Width) || !double.IsFinite(rect.Height))
        {
            return false;
        }

        // 縦横比が画像と一致する（別の座標系の値・古い値を弾く）
        var expected = imageSize.Width / Math.Max(1, imageSize.Height);
        var actual = rect.Width / rect.Height;
        return Math.Abs(actual - expected) <= expected * AspectTolerance;
    }
}
