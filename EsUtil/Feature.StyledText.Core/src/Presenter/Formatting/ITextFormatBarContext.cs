namespace EsUtil.Feature.StyledText.Presenter.Formatting;

/// <summary>書式バーが書式を読み書きする相手（ホストが実装する）。</summary>
/// <remarks>
/// <b>バーは相手を知らない</b>: バーは「選択範囲の書式を読む・変える」だけを行い、何を編集しているか
/// （注釈の本文・アプリの本文など）を知らない。 ホストは自分の編集状態へつなぐ実装を渡す。<br/>
/// <b>適用先の決め方はホストが単一所有する</b>: 選択が無いときに以降の入力へ回すか、本文全体へ適用するかは
/// ホストの規則である（バーは <see cref="TransformStyle"/> を呼ぶだけ）。
/// </remarks>
public interface ITextFormatBarContext
{
    /// <summary>現在の選択範囲（選択が無ければキャレットの位置）の書式を取得する。</summary>
    TextStyle SelectedTextStyle { get; }

    /// <summary>現在の選択範囲の書式がすべて条件を満たすかどうかを取得する。</summary>
    /// <param name="match">書式が条件を満たすかどうかを返す関数。</param>
    /// <returns>すべて満たす場合は <see langword="true"/>。</returns>
    bool SelectedTextMatches(Func<TextStyle, bool> match);

    /// <summary>選択範囲（ホストの規則で適用先を決める）の書式を変換する。</summary>
    /// <param name="transform">現在の書式から新しい書式を作る関数。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    bool TransformStyle(Func<TextStyle, TextStyle> transform);

    /// <summary>本文の文字を選んでいるかどうかを取得する。</summary>
    bool HasSelection { get; }

    /// <summary>書式全体を差し替える（フォント名・大きさ・色のように 1 つの値で決まる項目が使う）。</summary>
    /// <param name="style">新しい書式。</param>
    /// <returns>適用した場合は <see langword="true"/>。</returns>
    bool ApplyStyle(TextStyle style);
}