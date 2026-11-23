namespace EsUtil.Feature.Annotation.Presenter.Editor;

/// <summary>注釈の編集 UI（右クリックのフライアウト・書式バー）へ出す文言（単一所有）。</summary>
/// <remarks>
/// <b>文面は UI に書きません</b>: 同じ項目名を 4 つの UI 版がそれぞれ持つと、版ごとに言い回しが変わります
/// （実測 2026-11-22: 同じ項目が「しっぽの向き」と「しっぽの辺」に分かれる）。<br/>
/// <b>訳す場合はここを差し替える</b>: 表示の文言を 1 か所に集めることで、翻訳や表記の統一が 1 回で済みます。
/// </remarks>
public static class AnnotationEditorText
{
    /// <summary>外観の見出し。</summary>
    public const string AppearanceHeading = "外観";

    /// <summary>吹き出しの見出し。</summary>
    public const string BubbleHeading = "吹き出し";

    /// <summary>枠線の色の項目名。</summary>
    public const string BorderColor = "枠線の色";

    /// <summary>枠線の太さの項目名。</summary>
    public const string BorderThickness = "枠線の太さ";

    /// <summary>内側の色の項目名。</summary>
    public const string FillColor = "内側の色";

    /// <summary>角丸の項目名。</summary>
    public const string CornerRadius = "角丸";

    /// <summary>本文の余白の項目名。</summary>
    public const string BodyPadding = "本文の余白";

    /// <summary>しっぽの幅の項目名。</summary>
    public const string TailWidth = "しっぽの幅";

    /// <summary>しっぽの辺の項目名。</summary>
    public const string TailEdge = "しっぽの辺";

    /// <summary>最前面へ移すボタンの文言。</summary>
    public const string MoveToFront = "最前面へ";

    /// <summary>最背面へ移すボタンの文言。</summary>
    public const string MoveToBack = "最背面へ";

    /// <summary>注釈を選んでいないときの案内。</summary>
    public const string NoSelection = "注釈を選んでください。";
}
