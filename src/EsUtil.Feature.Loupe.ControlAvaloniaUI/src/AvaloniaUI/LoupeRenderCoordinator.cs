namespace EsUtil.Feature.Loupe.AvaloniaUI;

/// <summary>ルーペの再描画要求を実行キューへのポストでバッチ化する（1 フレームに 1 回）。</summary>
/// <remarks>
/// 複数のプロパティ変更で多重に再描画されるのを防ぐオーケストレーションを、UI 状態<br/>
/// （実行キューへのポスト・表示状態・ビットマップ解放・描画開始）から分離する。<br/>
/// ポスト・表示状態・ビットマップ解放・描画開始はすべてコールバックで受け取るため、UI コントロールに依存しない<br/>
/// （UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる）。<br/>
/// 位置付け: 中間ビットマップを生成する UI（Avalonia）にだけ必要になる処理である（同期描画の MewUI は不要）。<br/>
/// <b>描画の開始はコールバック</b>である: 中間ビットマップを生成するか画面のキャンバスへ直接描くかは UI ごとに異なる。
/// </remarks>
public sealed class LoupeRenderCoordinator
{
    /// <summary>現在表示中かどうかを取得するコールバック。</summary>
    private readonly Func<bool> _isVisible;

    /// <summary>ビットマップを解放して表示をクリアするコールバック。</summary>
    private readonly Action _releaseBitmap;

    /// <summary>指定した世代番号で描画を開始するコールバック。</summary>
    private readonly Action<long> _startRender;

    /// <summary>再描画コア実行の記録コールバック（出力の有無はロガー自身が判定する）。</summary>
    private readonly Action<long> _logRenderCore;

    /// <summary>実行キューへ処理を投入するコールバック（UI 層が実行キューと優先度を指定する）。</summary>
    private readonly Action<Action> _post;

    /// <summary>再描画要求の結合フラグ（true の間は追加要求を無視して 1 回のポストにまとめる）。</summary>
    private bool _renderQueued;

    /// <summary>再描画の世代番号（古い世代のビットマップ破棄判定に使用）。</summary>
    private long _renderGeneration;

    /// <summary>再描画コーディネーターを初期化する。</summary>
    /// <param name="isVisible">現在表示中かどうかを取得するコールバック。 null 不可。</param>
    /// <param name="releaseBitmap">ビットマップを解放して表示をクリアするコールバック。 null 不可。</param>
    /// <param name="startRender">指定した世代番号で描画を開始するコールバック。 null 不可。</param>
    /// <param name="logRenderCore">再描画コア実行の記録コールバック（出力の有無はロガー自身が判定する）。 null 不可。</param>
    /// <param name="post">実行キューへ処理を投入するコールバック（UI 層が実行キューと優先度を指定する）。 null 不可。</param>
    /// <exception cref="ArgumentNullException">いずれかのコールバックが null の場合。</exception>
    public LoupeRenderCoordinator(
        Func<bool> isVisible,
        Action releaseBitmap,
        Action<long> startRender,
        Action<long> logRenderCore,
        Action<Action> post)
    {
        ArgumentNullException.ThrowIfNull(isVisible);
        ArgumentNullException.ThrowIfNull(releaseBitmap);
        ArgumentNullException.ThrowIfNull(startRender);
        ArgumentNullException.ThrowIfNull(logRenderCore);
        ArgumentNullException.ThrowIfNull(post);

        _isVisible = isVisible;
        _releaseBitmap = releaseBitmap;
        _startRender = startRender;
        _logRenderCore = logRenderCore;
        _post = post;
    }

    /// <summary>現在の世代番号。</summary>
    /// <value>世代番号（描画要求のたびに増える）。 古い世代のビットマップ破棄判定に使用する。</value>
    public long Generation => _renderGeneration;

    /// <summary>指定した世代のビットマップを表示へ反映してよいか判定する。</summary>
    /// <param name="generation">生成したビットマップの世代番号。</param>
    /// <returns>表示中かつ指定世代が最新なら true（古い世代・非表示では破棄）。</returns>
    /// <remarks>非表示中、または別世代のビットマップが生成済み（世代が進んでいる）場合は反映せず破棄する。</remarks>
    public bool ShouldApply(long generation)
        => _isVisible() && generation == _renderGeneration;

    /// <summary>未実行の再描画要求を破棄し、ビットマップを解放する（非表示・明示クリアで共用）。</summary>
    /// <remarks>「要求を破棄して表示中の画像を解放する」手順を単一所有する。</remarks>
    public void CancelAndRelease()
    {
        CancelPending();
        _releaseBitmap();
    }

    /// <summary>再描画を要求する（複数要求は実行キューへのポストにより 1 回に結合される）。</summary>
    /// <remarks>
    /// 表示中でない場合は未実行要求を破棄しビットマップを解放する（描画は行わない）。<br/>
    /// 表示中は既に要求が積まれていれば何もしない（1 フレームに 1 回の生成へ結合する）。
    /// </remarks>
    public void RequestRender()
    {
        if (!_isVisible())
        {
            Interlocked.Increment(ref _renderGeneration);
            _releaseBitmap();
            return;
        }

        if (_renderQueued)
        {
            return;
        }

        _renderQueued = true;
        _post(RenderCore);
    }

    /// <summary>ポストされた再描画を実行する（世代番号を採番して描画を開始する）。</summary>
    /// <remarks>
    /// ログ出力の有無（無効ロガー）はロガー自身（生成コードの <c>IsEnabled</c> 判定）が担うため、本メソッドは判断しない。<br/>
    /// 要求が既に取り消されている場合（非表示遷移後）は何もしない。
    /// </remarks>
    public void RenderCore()
    {
        if (!_renderQueued)
        {
            return;
        }

        _renderQueued = false;
        var generation = Interlocked.Increment(ref _renderGeneration);
        _logRenderCore(generation);
        _startRender(generation);
    }

    /// <summary>未実行の再描画要求をキャンセルする（非表示遷移時）。</summary>
    public void CancelPending() => _renderQueued = false;
}
