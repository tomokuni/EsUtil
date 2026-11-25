namespace EsUtil.Feature.StyledText.Presenter.Formatting;

/// <summary>書式バーを表示する条件（編集中・自動表示が有効・1 文字以上を選択）を単一所有する。</summary>
/// <remarks>
/// <b>表示可否は提示層が決める</b>: 本文の編集状態（キャレット・選択・入力）はコアが持ち、
/// UI 要素を出すかどうかは描画にだけ必要な判断であるため本層が持つ（層の帰属の規則は `EsUtil/README.md` が持つ）。<br/>
/// <b>4 つの UI 版は本判定だけを使う</b>: 条件を UI ごとに書き写すと、選択の畳み方の違いでバーの出方が食い違う。<br/>
/// <b>適用先が決まらない間は出さない</b>: 選択が無い状態でバーを出すと、操作しても効かない。
/// </remarks>
public static class TextFormatBarVisibility
{
    /// <summary>書式バーを表示する最小の選択文字数。</summary>
    /// <value>1（1 文字以上を選んだときだけ表示する）。</value>
    public const int MinimumSelectionLength = 1;

    /// <summary>書式バーを表示すべきかどうかを判定する。</summary>
    /// <param name="autoShow">編集中に書式バーを自動で表示する設定かどうか。 利用者・ホストが決める。</param>
    /// <param name="isEditing">本文を編集中かどうか。</param>
    /// <param name="selectionLength">選択している文字数。</param>
    /// <returns>自動表示が有効で、編集中で、<see cref="MinimumSelectionLength"/> 文字以上を選んでいる場合は <see langword="true"/>。</returns>
    public static bool ShouldShow(bool autoShow, bool isEditing, int selectionLength)
        => autoShow && isEditing && selectionLength >= MinimumSelectionLength;
}
