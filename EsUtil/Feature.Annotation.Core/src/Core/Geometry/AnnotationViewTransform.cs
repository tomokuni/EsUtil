using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation.Core.Geometry;

/// <summary>注釈レイヤーの座標変換（画像の表示倍率と左上の位置。 単一所有）。</summary>
/// <remarks>
/// <b>表示矩形はホストが与える</b>: ズーム・パンの状態はプレビュー（ホスト）が所有するため、
/// 注釈レイヤーは「プレビュー内の画像の表示矩形」を受け取って使う。<br/>
/// <b>受け取った矩形は検証する</b>: レイアウト途中の値・別の座標系の値・古い値が入ると、注釈の位置が
/// 画像からずれる。 縦横比が画像と一致しない・有限でない矩形は捨てて、自分で中央へ収めた値を使う。<br/>
/// <b>座標は注釈レイヤーのローカル DIP</b>である（画面座標ではない）。 変換の式は本型が単一所有し、
/// 4 つの UI 版は自分の点の型へ写すだけにする。
/// </remarks>
/// <param name="Scale">画像の表示倍率（元画像ピクセル → DIP）。</param>
/// <param name="Offset">画像の左上の位置（ローカル DIP）。</param>
public readonly record struct AnnotationViewTransform(double Scale, EsPoint Offset)
{
    /// <summary>縦横比の許容差（比の相対誤差）。</summary>
    private const double AspectTolerance = 0.02;

    /// <summary>元画像ピクセルの座標の分解能（小数点以下の桁数）。</summary>
    private const int SourceDigits = 2;

    /// <summary>等倍・原点の変換を取得する。</summary>
    public static AnnotationViewTransform Identity { get; } = new(1, default);

    /// <summary>画像の表示倍率と左上の位置を求める。</summary>
    /// <param name="clientSize">注釈レイヤーの大きさ（DIP）。</param>
    /// <param name="imageSize">元画像の大きさ（ピクセル）。</param>
    /// <param name="preview">プレビュー内の画像の表示矩形（無い場合は <see langword="null"/>）。</param>
    /// <returns>変換。 レイヤーが未配置（大きさ 0）の場合は等倍・原点。</returns>
    /// <remarks>受け取った表示矩形が疑わしい場合は、縦横比を保って中央へ収めた値を使う。</remarks>
    public static AnnotationViewTransform Resolve(EsSize clientSize, EsSize imageSize, EsRect? preview)
    {
        var width = Math.Max(1, imageSize.Width);
        var height = Math.Max(1, imageSize.Height);
        var clientWidth = clientSize.Width;
        var clientHeight = clientSize.Height;
        if (clientWidth <= 0 || clientHeight <= 0)
        {
            return Identity;
        }

        if (preview is { Width: > 0, Height: > 0 } rect && IsPlausible(rect, imageSize))
        {
            // 縦横比で倍率を決める（幅と高さで別々に求めると丸めの差で伸び縮みする）
            return new AnnotationViewTransform(rect.Width / width, new EsPoint(rect.X, rect.Y));
        }

        var fit = Math.Min(clientWidth / width, clientHeight / height);
        return new AnnotationViewTransform(fit, new EsPoint(
            (clientWidth - (width * fit)) / 2,
            (clientHeight - (height * fit)) / 2));
    }

    /// <summary>ローカル DIP の点を元画像ピクセルへ変換する。</summary>
    /// <param name="dip">ローカル DIP の座標。</param>
    /// <returns>元画像ピクセルの座標。</returns>
    /// <remarks>
    /// <b>1/100 ピクセルまでに丸める</b>: 単精度で座標を持つ UI（WinUI 3）から来た値は、変換すると
    /// <c>120.79998779292969</c> のような誤差を含む。 画素より細かい値に意味はなく、そのままではログ・
    /// 保存した注釈・当たり判定の比較へ誤差が広がる。
    /// </remarks>
    public EsPoint ToSource(EsPoint dip)
        => new(
            RoundSource((dip.X - Offset.X) / Scale),
            RoundSource((dip.Y - Offset.Y) / Scale));

    /// <summary>元画像ピクセルの点をローカル DIP へ変換する。</summary>
    /// <param name="source">元画像ピクセルの座標。</param>
    /// <returns>ローカル DIP の座標。</returns>
    public EsPoint ToClient(EsPoint source)
        => new(Offset.X + (source.X * Scale), Offset.Y + (source.Y * Scale));

    /// <summary>元画像ピクセルの矩形をローカル DIP へ変換する。</summary>
    /// <param name="source">元画像ピクセルの矩形。</param>
    /// <returns>ローカル DIP の矩形。</returns>
    public EsRect ToClient(EsRect source)
    {
        var topLeft = ToClient(new EsPoint(source.X, source.Y));
        return new EsRect(topLeft.X, topLeft.Y, source.Width * Scale, source.Height * Scale);
    }

    /// <summary>元画像ピクセルの座標を分解能までに丸める。</summary>
    /// <param name="value">丸める値。</param>
    /// <returns>丸めた値。</returns>
    private static double RoundSource(double value)
        => double.IsFinite(value)
            ? Math.Round(value, SourceDigits, MidpointRounding.AwayFromZero)
            : value;

    /// <summary>受け取った表示矩形を使ってよいかどうかを判定する。</summary>
    /// <param name="rect">表示矩形（ローカル DIP）。</param>
    /// <param name="imageSize">元画像の大きさ（ピクセル）。</param>
    /// <returns>使ってよい場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <b>レイヤーの外へはみ出す値も正しい</b>: 画像がプレビューより大きい（ズーム 100% で見切れる）場合、
    /// 表示矩形の左上は負になる。 はみ出しを不正とすると、実際の画像と注釈で別の倍率・位置を使って
    /// 右上へずれる。 判定は「有限・正の大きさ・縦横比が画像と一致」だけにする。
    /// </remarks>
    private static bool IsPlausible(EsRect rect, EsSize imageSize)
    {
        if (rect.Width <= 0 || rect.Height <= 0
            || !double.IsFinite(rect.X) || !double.IsFinite(rect.Y)
            || !double.IsFinite(rect.Width) || !double.IsFinite(rect.Height))
        {
            return false;
        }

        var expected = imageSize.Width / Math.Max(1, imageSize.Height);
        var actual = rect.Width / rect.Height;
        return Math.Abs(actual - expected) <= expected * AspectTolerance;
    }
}
