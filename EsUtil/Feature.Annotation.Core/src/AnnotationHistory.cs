namespace EsUtil.Feature.Annotation;

/// <summary>注釈の変更履歴（元に戻す・やり直す）。</summary>
/// <remarks>
/// <b>文書の複製を積む</b>: 注釈の座標・しっぽはドラッグ中に書き換わるため、文書そのものを持つと
/// 過去の状態が書き換わる（<see cref="AnnotationDocument.Clone"/> で複製する）。<br/>
/// <b>変更の前に記録する</b>: 変更後の文書を記録すると 1 手戻りがずれる。 呼び出し側は変更の直前に
/// <see cref="Record"/> を呼ぶ。<br/>
/// <b>新しい変更でやり直しは消える</b>（一般的な編集の履歴と同じ）。
/// </remarks>
public sealed class AnnotationHistory(int capacity = AnnotationHistory.DefaultCapacity)
{
    /// <summary>履歴の既定の保持数。</summary>
    public const int DefaultCapacity = 50;

    /// <summary>元に戻すための文書（末尾が直近）。</summary>
    private readonly List<AnnotationDocument> _undo = [];

    /// <summary>やり直すための文書（末尾が直近）。</summary>
    private readonly List<AnnotationDocument> _redo = [];

    /// <summary>保持する変更の数を取得する。</summary>
    public int Capacity { get; } = Math.Max(1, capacity);

    /// <summary>元に戻せるかどうかを取得する。</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>やり直せるかどうかを取得する。</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>元に戻せる回数を取得する。</summary>
    public int UndoCount => _undo.Count;

    /// <summary>やり直せる回数を取得する。</summary>
    public int RedoCount => _redo.Count;

    /// <summary>変更の直前の文書を記録する。</summary>
    /// <param name="document">変更の直前の文書。</param>
    public void Record(AnnotationDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        _undo.Add(document.Clone());
        if (_undo.Count > Capacity)
        {
            _undo.RemoveAt(0);
        }

        _redo.Clear();
    }

    /// <summary>1 手戻した文書を返す。</summary>
    /// <param name="current">現在の文書（やり直しのために複製して保持する）。</param>
    /// <returns>戻した文書。 戻せない場合は <see langword="null"/>。</returns>
    public AnnotationDocument? Undo(AnnotationDocument current)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (_undo.Count == 0)
        {
            return null;
        }

        var previous = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(current.Clone());
        return previous;
    }

    /// <summary>1 手やり直した文書を返す。</summary>
    /// <param name="current">現在の文書（元に戻すために複製して保持する）。</param>
    /// <returns>やり直した文書。 やり直せない場合は <see langword="null"/>。</returns>
    public AnnotationDocument? Redo(AnnotationDocument current)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (_redo.Count == 0)
        {
            return null;
        }

        var next = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(current.Clone());
        return next;
    }

    /// <summary>履歴を空にする。</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
