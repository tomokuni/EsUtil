namespace EsUtil.Feature.Annotation.Core.Contract;

/// <summary>注釈の追加メニューを出す要求。</summary>
/// <remarks>
/// <b>メニューの表示はホストの仕事</b>である: 見た目は UI ごとに異なるため、位置だけをレイヤーが決めて
/// ホストが自分の UI のメニューを出す。 位置は注釈レイヤーのローカル DIP（プレビューと同じ座標系）。
/// </remarks>
public sealed class AnnotationAddMenuRequestedEventArgs(double x, double y) : EventArgs
{
    /// <summary>位置（ローカル DIP）を取得する。</summary>
    public double X { get; } = x;

    /// <summary>位置（ローカル DIP）を取得する。</summary>
    public double Y { get; } = y;
}
