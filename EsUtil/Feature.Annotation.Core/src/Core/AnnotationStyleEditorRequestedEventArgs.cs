namespace EsUtil.Feature.Annotation.Core;

/// <summary>外観フライアウト（右クリックの編集パネル）を出す要求。</summary>
/// <remarks>
/// <b>表示位置はホストが決める</b>: 右クリックした位置（注釈レイヤーのローカル DIP）を渡し、
/// ホストはその位置へ自分の UI のパネルを出す。 パネルの移動（ドラッグ）と外側クリックでの終了もホストが担う。
/// </remarks>
public sealed class AnnotationStyleEditorRequestedEventArgs(double x, double y) : EventArgs
{
    /// <summary>位置（ローカル DIP）を取得する。</summary>
    public double X { get; } = x;

    /// <summary>位置（ローカル DIP）を取得する。</summary>
    public double Y { get; } = y;
}
