namespace EsUtil.Feature.Thumbnail;

/// <summary>サムネイルの名前行へ収まるよう文言を整える純粋計算。</summary>
/// <remarks>
/// <b>名前の行の高さは最大 2 行である</b>（<see cref="MaxLines"/>）。 2 行に収まらない名前は、<br/>
/// <b>最後のピリオドより前を「...」で省略し、拡張子を残す</b>（<c>capture-0123.png</c> → <c>capt….png</c>）。<br/>
/// 拡張子を残すのは、一覧の中で種類（画像形式）を判別できるようにするためである。<br/>
/// <br/>
/// <b>実寸は UI 層が測る</b>: この層は描画 API を持たないため、折り返した実寸を <see cref="ThumbnailTextMeasure"/> として受け取る。<br/>
/// 測定を渡さない場合は 1 文字の幅を文字サイズから見積もる（等幅の近似。 多少の余裕を見て切り詰める）。<br/>
/// <b>高さも判定に含める</b>: 上下の余白や行の高さで文字を置ける高さが変わると、収まる行数も変わる<br/>
/// （幅だけで判定すると、省略されないまま縦に切れる。 実測 2026-11-01）。<br/>
/// <b>UI フレームワークに依存しない</b>ため、UI 初期化なしの <c>[Fact]</c> で検証できる。
/// </remarks>
public static class ThumbnailLabelText
{
    /// <summary>1 文字あたりの幅の見積もり（通常の太さ。 文字サイズに対する倍率）。</summary>
    /// <value>0.62（英数字の平均より広めに取り、切り詰めが足りない状態を避ける）。</value>
    /// <remarks>測定（<see cref="ThumbnailTextMeasure"/>）を渡さない場合だけ使う。</remarks>
    public const double AverageCharWidthFactor = 0.62;

    /// <summary>1 文字あたりの幅の見積もり（太字。 文字サイズに対する倍率）。</summary>
    /// <value>0.68（太字は同じ文字でも幅が広いため、通常より大きい係数を使う）。</value>
    /// <remarks>測定（<see cref="ThumbnailTextMeasure"/>）を渡さない場合だけ使う。</remarks>
    public const double BoldCharWidthFactor = 0.68;

    /// <summary>行送り（文字サイズに対する倍率）。</summary>
    /// <value>1.4（文字が切れない最小の行送り）。</value>
    public const double LineSpacing = 1.4;

    /// <summary>折り返して表示する最大行数。</summary>
    /// <value>2。 超える場合は末尾を省略する。</value>
    /// <remarks>名前行の高さの既定値（<see cref="ResolveHeight"/>）を決める。</remarks>
    public const int MaxLines = 2;

    /// <summary>省略を表す記号。</summary>
    /// <value>3 文字のピリオド。</value>
    public const string Ellipsis = "...";

    /// <summary>高さの比較に許す誤差（DIP）。</summary>
    /// <value>0.5（実寸の丸めの差で「収まらない」と誤判定しないための許容）。</value>
    private const double HeightTolerance = 0.5;

    /// <summary>名前行へ収まるよう文言を整える。</summary>
    /// <param name="text">表示する名前（null または空の場合はそのまま返す）。</param>
    /// <param name="width">名前を表示できる幅（DIP。 名前の行の幅から左右の余白を除いた値）。</param>
    /// <param name="height">名前を表示できる高さ（DIP。 名前の行の高さから上下の余白を除いた値）。 0 以下の場合は <see cref="ResolveHeight"/> を使う。</param>
    /// <param name="fontSize">文字サイズ（DIP）。</param>
    /// <param name="isBold">太字で表示するかどうか（太字は幅が広いため、少なめに見積もる）。</param>
    /// <param name="measure">折り返した実寸を測る関数。 null の場合は文字数から見積もる。</param>
    /// <returns>収まる名前。 収まらない場合は最後のピリオドより前を省略した名前。</returns>
    /// <remarks>
    /// 幅・文字サイズが正でない場合は切り詰めない（寸法が未確定の間に文言を変えない）。<br/>
    /// 高さで 1 行も入らない場合は省略記号だけを返す（描画側は行の高さで切る）。<br/>
    /// 省略は<b>文字の境界</b>で行う（拡張子の手前で切らないため、必ず拡張子が残る）。
    /// </remarks>
    public static string Fit(
        string? text,
        double width,
        double height,
        double fontSize,
        bool isBold = false,
        ThumbnailTextMeasure? measure = null)
    {
        if (string.IsNullOrEmpty(text) || width <= 0 || fontSize <= 0)
        {
            return text ?? string.Empty;
        }

        var availableHeight = height > 0 ? height : ResolveHeight(fontSize, MaxLines);
        return measure is { } measurer
            ? FitMeasured(text, width, availableHeight, fontSize, isBold, measurer)
            : FitEstimated(text, width, fontSize, ResolveMaxLines(availableHeight, fontSize), isBold);
    }

    /// <summary>行の高さ（文字が切れない行送り）を求める。</summary>
    /// <param name="fontSize">文字サイズ（DIP）。</param>
    /// <returns>1 行の高さ（DIP）。 文字サイズが正でない場合は 0。</returns>
    public static double ResolveLineHeight(double fontSize)
        => fontSize <= 0 ? 0 : Math.Ceiling(fontSize * LineSpacing);

    /// <summary>名前行の高さを求める。</summary>
    /// <param name="fontSize">文字サイズ（DIP）。</param>
    /// <param name="maxLines">折り返して表示する最大行数。</param>
    /// <returns>名前行の高さ（DIP。 文字が切れない行送り × 行数）。</returns>
    public static double ResolveHeight(double fontSize, int maxLines)
        => fontSize <= 0 || maxLines <= 0 ? 0 : ResolveLineHeight(fontSize) * maxLines;

    /// <summary>高さに収まる行数を求める。</summary>
    /// <param name="height">文字を置ける高さ（DIP）。</param>
    /// <param name="fontSize">文字サイズ（DIP）。</param>
    /// <returns>収まる行数（最低 1）。 高さ・文字サイズが正でない場合は 0。</returns>
    /// <remarks>上下の余白で高さが減っても、1 行は必ず置けるものとして扱う。</remarks>
    public static int ResolveMaxLines(double height, double fontSize)
    {
        var lineHeight = ResolveLineHeight(fontSize);
        return height <= 0 || lineHeight <= 0 ? 0 : Math.Max(1, (int)(height / lineHeight));
    }

    /// <summary>実寸を測って収まる文言を求める。</summary>
    /// <param name="text">表示する名前。</param>
    /// <param name="width">名前を表示できる幅（DIP）。</param>
    /// <param name="height">名前を表示できる高さ（DIP）。</param>
    /// <param name="fontSize">文字サイズ（DIP）。</param>
    /// <param name="isBold">太字で表示するかどうか。</param>
    /// <param name="measure">折り返した実寸を測る関数。</param>
    /// <returns>高さに収まる名前。</returns>
    /// <remarks>
    /// そのままの名前が収まる場合は測定 1 回で返す（収まる場合が大半のため、費用は項目数に比例する）。<br/>
    /// 収まらない場合は残す文字数を二分探索する（測定は <c>log(文字数)</c> 回で済む）。
    /// </remarks>
    private static string FitMeasured(
        string text,
        double width,
        double height,
        double fontSize,
        bool isBold,
        ThumbnailTextMeasure measure)
    {
        if (Fits(text, width, height, fontSize, isBold, measure))
        {
            return text;
        }

        var (stem, extension) = SplitExtension(text);
        var suffix = string.Concat(Ellipsis, extension);
        if (!Fits(suffix, width, height, fontSize, isBold, measure))
        {
            return suffix;
        }

        var low = 0;
        var high = stem.Length - 1;
        var keep = 0;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (Fits(Compose(stem, middle, suffix), width, height, fontSize, isBold, measure))
            {
                keep = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return Compose(stem, keep, suffix);
    }

    /// <summary>文言が与えられた寸法に収まるかどうかを判定する。</summary>
    /// <param name="text">判定する文言。</param>
    /// <param name="width">名前を表示できる幅（DIP）。</param>
    /// <param name="height">名前を表示できる高さ（DIP）。</param>
    /// <param name="fontSize">文字サイズ（DIP）。</param>
    /// <param name="isBold">太字で表示するかどうか。</param>
    /// <param name="measure">折り返した実寸を測る関数。</param>
    /// <returns>収まる場合は true。</returns>
    private static bool Fits(string text, double width, double height, double fontSize, bool isBold, ThumbnailTextMeasure measure)
        => measure(text, width, fontSize, isBold).Height <= height + HeightTolerance;

    /// <summary>拡張子を除いた名前の先頭 <paramref name="keep"/> 文字へ省略記号と拡張子を継ぐ。</summary>
    /// <param name="stem">拡張子を除いた名前。</param>
    /// <param name="keep">残す文字数。</param>
    /// <param name="suffix">省略記号と拡張子。</param>
    /// <returns>省略した名前。</returns>
    /// <remarks>残した部分の末尾の空白は落とす（省略記号との間に空白を残さない）。</remarks>
    private static string Compose(string stem, int keep, string suffix)
        => string.Concat(stem.AsSpan(0, Math.Clamp(keep, 0, stem.Length)).TrimEnd(), suffix.AsSpan());

    /// <summary>実寸を測らずに文字数から収まる文言を求める。</summary>
    /// <param name="text">表示する名前。</param>
    /// <param name="width">名前を表示できる幅（DIP）。</param>
    /// <param name="fontSize">文字サイズ（DIP）。</param>
    /// <param name="maxLines">収まる行数。</param>
    /// <param name="isBold">太字で表示するかどうか。</param>
    /// <returns>収まる名前。</returns>
    /// <remarks>文字幅は文字サイズから見積もる（描画 API を持たないため）。 実際の折り返し位置とずれないよう、余裕のある係数を使う。</remarks>
    private static string FitEstimated(string text, double width, double fontSize, int maxLines, bool isBold)
    {
        if (maxLines <= 0)
        {
            return text;
        }

        var factor = isBold ? BoldCharWidthFactor : AverageCharWidthFactor;
        var charsPerLine = Math.Max(1, (int)(width / (fontSize * factor)));
        var budget = charsPerLine * maxLines;
        if (text.Length <= budget)
        {
            return text;
        }

        var (stem, extension) = SplitExtension(text);
        var keep = budget - extension.Length - Ellipsis.Length;
        if (keep <= 0)
        {
            // 拡張子だけで埋まる場合は拡張子を優先し、名前の先頭だけを残す
            return Compose(text, Math.Max(1, budget - Ellipsis.Length), string.Empty);
        }

        return Compose(stem, Math.Min(keep, stem.Length), string.Concat(Ellipsis, extension));
    }

    /// <summary>名前を「拡張子を除いた部分」と「拡張子」へ分ける。</summary>
    /// <param name="text">分ける名前。</param>
    /// <returns>拡張子を除いた部分と拡張子（拡張子が無い場合は空）。</returns>
    private static (string Stem, string Extension) SplitExtension(string text)
    {
        var index = text.LastIndexOf('.');
        return index > 0 && index < text.Length - 1
            ? (text[..index], text[index..])
            : (text, string.Empty);
    }
}
