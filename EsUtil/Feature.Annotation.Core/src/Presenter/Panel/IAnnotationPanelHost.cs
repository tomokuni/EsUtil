using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation.Presenter.Panel;

/// <summary>フローティング パネル（書式バー・右クリックの外観フライアウト）を置く窓口（UI が実装する）。</summary>
/// <remarks>
/// <b>UI が実装するのはここだけ</b>: アプリの画面へパネルを載せ、動かし、外すだけを行う。
/// いつ出すか・どこへ出すか・ドラッグでどう動くかは <see cref="AnnotationPanelController"/> が
/// 単一所有する（4 つの UI 版で動作が食い違わないようにするため）。<br/>
/// <b>独立したウィンドウを作らない</b>: OS のウィンドウはタスク バーへ出る・アプリの後ろへ回る・
/// フォーカスを奪うといった問題が版ごとに出るため、アプリの画面の上へ重ねる。<br/>
/// <b>座標はパネルを描く面の左上を原点とする DIP</b>: 画像（プレビュー）の内側で切れないよう、
/// 重ねる先はプレビューではなくアプリの画面の最上位にする。
/// </remarks>
public interface IAnnotationPanelHost
{
    /// <summary>パネルを置ける領域（DIP）を取得する。</summary>
    /// <value>パネルを置ける範囲。 パネルはこの外へは出さない（見えなくなるため）。</value>
    /// <remarks>
    /// <b>見えている範囲の内側に限る</b>: 注釈レイヤーの外側へも置けるが、ウィンドウの外は切れて
    /// 見えなくなる。 領域の求め方は <see cref="AnnotationPanelPlacement.ResolveAvailableArea"/> が
    /// 単一所有する（4 つの UI 版で同じ位置になる）。
    /// </remarks>
    EsRect AvailableArea { get; }

    /// <summary>表示中のパネルの左上（DIP）を取得する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <returns>左上の位置。 表示していない場合は原点。</returns>
    /// <remarks>
    /// <b>種別ごとに答える</b>: 書式バーと外観フライアウトは同時に出るため、
    /// 「どちらか 1 つ」を返すと外側押下の判定とドラッグの起点が食い違う。
    /// </remarks>
    EsPoint GetPanelPosition(AnnotationPanelKind kind);

    /// <summary>表示中のパネルの大きさ（DIP）を取得する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <returns>パネルの大きさ。 表示していない場合は原点。</returns>
    /// <remarks>種別ごとに答える理由は <see cref="GetPanelPosition"/> と同じ。</remarks>
    EsSize GetPanelSize(AnnotationPanelKind kind);

    /// <summary>パネルを作って表示する（表示中の種別は作り直さない。 表示の判断は呼び出し側が持つ）。</summary>
    /// <param name="kind">パネルの種別（中身を決める）。</param>
    /// <param name="appearance">外観（枠・余白・幅。 そのまま適用する）。</param>
    /// <param name="position">左上の位置（アプリの画面の左上を原点とする DIP）。</param>
    void CreatePanel(AnnotationPanelKind kind, AnnotationPanelAppearance appearance, EsPoint position);

    /// <summary>パネルを移動する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="position">左上の位置（アプリの画面の左上を原点とする DIP）。</param>
    void MovePanel(AnnotationPanelKind kind, EsPoint position);

    /// <summary>パネルを画面から外す。</summary>
    /// <param name="kind">パネルの種別。</param>
    void DestroyPanel(AnnotationPanelKind kind);
}
