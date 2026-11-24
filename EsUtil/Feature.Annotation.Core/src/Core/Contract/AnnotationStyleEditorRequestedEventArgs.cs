namespace EsUtil.Feature.Annotation.Core.Contract;

/// <summary>外観フライアウト（右クリックの編集パネル）を出す要求。</summary>
/// <remarks>
/// <b>表示位置はホストが決める</b>: 右クリックした位置（注釈レイヤーのローカル DIP）を渡し、
/// ホストはその位置へ自分の UI のパネルを出す。 パネルの移動（ドラッグ）と外側クリックでの終了もホストが担う。<br/>
/// <b>出した押下で閉じない</b>: ポインターの押下から出した場合、その押下はホストの「外側を押したら閉じる」
/// 判定にも届く。 <see cref="FromPointer"/> を見て、その 1 回だけ判定を飛ばす（飛ばさないとパネルが開いた直後に閉じる）。
/// </remarks>
public sealed class AnnotationStyleEditorRequestedEventArgs(double x, double y, bool fromPointer = false) : EventArgs
{
    /// <summary>位置（ローカル DIP）を取得する。</summary>
    public double X { get; } = x;

    /// <summary>位置（ローカル DIP）を取得する。</summary>
    public double Y { get; } = y;

    /// <summary>ポインターの押下から出した要求かどうかを取得する。</summary>
    /// <value>押下から出した場合は <see langword="true"/>（呼び出し側はその押下で閉じない）。</value>
    public bool FromPointer { get; } = fromPointer;
}
