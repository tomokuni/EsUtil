namespace EsUtil.Feature.Thumbnail;

/// <summary>サムネイルの名前行へ収まるよう文言を整える純粋計算。</summary>
/// <remarks>
/// <b>名前の行の高さは最大 2 行である</b>（<see cref="MaxLines"/>）。 2 行に収まらない名前は、<br/>
/// <b>最後のピリオドより前を「...」で省略し、拡張子を残す</b>（<c>capture-0123.png</c> → <c>capt….png</c>）。<br/>
/// 拡張子を残すのは、一覧の中で種類（画像形式）を判別できるようにするためである。<br/>
/// <br/>
/// <b>実寸ではなく文字数で判定する</b>: この層は描画 API を持たないため、1 文字の幅を文字サイズから見積もる<br/>
/// （等幅の近似。 多少の余裕を見て切り詰めるため、はみ出しは起きない）。<br/>
/// <b>UI フレームワークに依存しない</b>ため、UI 初期化なしの <c>[Fact]</c> で検証できる。
/// </remarks>
public static class ThumbnailLabelText
{
    /// <summary>1 文字あたりの幅の見積もり（文字サイズに対する倍率）。</summary>
    /// <value>0.62（英数字の平均より広めに取り、切り詰めが足りない状態を避ける）。</value>
    public const double AverageCharWidthFactor = 0.62;

    /// <summary>行送り（文字サイズに対する倍率）。</summary>
    /// <value>1.4（文字が切れない最小の行送り）。</value>
    public const double LineSpacing = 1.4;

    /// <summary>折り返して表示する最大行数。</summary>
    /// <value>2。 超える場合は末尾を省略する。</value>
    public const int MaxLines = 2;

    /// <summary>省略を表す記号。</summary>
    /// <value>3 文字のピリオド。</value>
    public const string Ellipsis = "...";

    /// <summary>名前行へ収まるよう文言を整える。</summary>
    /// <param name="text">表示する名前（null または空の場合はそのまま返す）。</param>
    /// <param name="width">名前行の幅（DIP）。</param>
    /// <param name="fontSize">文字サイズ（DIP）。</param>
    /// <param name="maxLines">折り返して表示する最大行数。</param>
    /// <returns>2 行に収まる名前。 収まらない場合は最後のピリオドより前を省略した名前。</returns>
    /// <remarks>
    /// 幅・文字サイズ・行数が正でない場合は切り詰めない（寸法が未確定の間に文言を変えない）。
    /// </remarks>
    public static string Fit(string? text, double width, double fontSize, int maxLines)
    {
        if (string.IsNullOrEmpty(text) || width <= 0 || fontSize <= 0 || maxLines <= 0)
        {
            return text ?? string.Empty;
        }

        var charsPerLine = Math.Max(1, (int)(width / (fontSize * AverageCharWidthFactor)));
        var budget = charsPerLine * maxLines;
        if (text.Length <= budget)
        {
            return text;
        }

        // 拡張子（最後のピリオド以降）は残す
        var extensionIndex = text.LastIndexOf('.');
        var extension = extensionIndex > 0 && extensionIndex < text.Length - 1 ? text[extensionIndex..] : string.Empty;
        var stem = extension.Length > 0 ? text[..extensionIndex] : text;

        var keep = budget - extension.Length - Ellipsis.Length;
        if (keep <= 0)
        {
            // 拡張子だけで埋まる場合は拡張子を優先し、名前の先頭だけを残す
            return string.Concat(text.AsSpan(0, Math.Max(1, budget - Ellipsis.Length)), Ellipsis);
        }

        var cut = Math.Min(keep, stem.Length);
        return string.Concat(stem.AsSpan(0, cut), Ellipsis, extension);
    }

    /// <summary>名前行の高さを求める。</summary>
    /// <param name="fontSize">文字サイズ（DIP）。</param>
    /// <param name="maxLines">折り返して表示する最大行数。</param>
    /// <returns>名前行の高さ（DIP。 文字が切れない行送り × 行数）。</returns>
    public static double ResolveHeight(double fontSize, int maxLines)
        => fontSize <= 0 || maxLines <= 0
            ? 0
            : Math.Ceiling(fontSize * LineSpacing) * maxLines;

}
