namespace EsUtil.Feature.Annotation.Core.History;

/// <summary>注釈の変更履歴（元に戻す・やり直す）。</summary>
/// <remarks>
/// <b>文書の複製を積む</b>: 注釈の座標・しっぽはドラッグ中に書き換わるため、文書そのものを持つと
/// 過去の状態が書き換わる（<see cref="AnnotationDocument.Clone"/> で複製する）。<br/>
/// <b>変更の前に記録する</b>: 変更後の文書を記録すると 1 手戻りがずれる。 呼び出し側は変更の直前に
/// <see cref="BeginChange"/> を呼び、変更し終えたら <see cref="CommitChange"/> を呼ぶ（ドラッグのように
/// 何度も書き換える操作は 1 手にまとまる）。<br/>
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

    /// <summary>操作の開始時に控えた文書（操作が終わるまで積まない）。</summary>
    private AnnotationDocument? _pending;

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

    /// <summary>操作の開始を記録する（変更の直前の文書を控える）。</summary>
    /// <param name="document">操作の直前の文書。</param>
    /// <remarks>
    /// <b>1 操作 = 1 手を型で表す</b>: ドラッグのように文書を何度も書き換える操作は、開始時に控えて
    /// 終了時に 1 度だけ積む（UI が「押下から離すまでで 1 手」を毎回書かない）。<br/>
    /// 控え中に再度呼ばれた場合は何もしない（最初の状態が 1 手の基準になる）。
    /// </remarks>
    public void BeginChange(AnnotationDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        _pending ??= document.Clone();
    }

    /// <summary>操作の終了を記録する（控えた文書が実際に変更されていれば積む）。</summary>
    /// <param name="document">操作し終えた文書（控えた文書と同じ内容なら積まない）。</param>
    /// <remarks>
    /// <b>変更が無い操作は積まない</b>: 選択だけのクリックや、同じ位置への移動で手数が増えないようにする。
    /// 比較は保存形式（<see cref="AnnotationJson.Serialize"/>）で行い、値の一致で判定する。
    /// </remarks>
    public void CommitChange(AnnotationDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (_pending is not { } candidate)
        {
            return;
        }

        _pending = null;
        if (string.Equals(
            AnnotationJson.Serialize(candidate),
            AnnotationJson.Serialize(document),
            StringComparison.Ordinal))
        {
            return;
        }

        Record(candidate);
    }

    /// <summary>控えを捨てる（操作を取り消したとき）。</summary>
    public void CancelChange() => _pending = null;

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
        _pending = null;
        _undo.Clear();
        _redo.Clear();
    }
}
