using EsUtil.Feature.Marquee.Core.Selection;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using EsUtil.Feature.Marquee.Core.Logging;

namespace EsUtil.Feature.Marquee.Core.Interaction;

/// <summary>Marquee の対話（選択・ドラッグ・nudge）の状態を管理し、入力を通知されて状態を更新する状態管理クラス（Mediator パターン: 入力と状態の仲介）。</summary>
/// <remarks>
/// 提供機能: UI 層から入力（ポインター押下・移動・解放・ホイール以外のキー）を通知されると、状態を更新し、<br/>
/// 「上位へ伝える通知（<see cref="Subscribe"/>）」と「画面へ反映する効果（<see cref="MarqueeFeedback"/>）」を返す。<br/>
/// <br/>
/// 責務の境界:<br/>
/// - 本クラス: 入力の解釈と状態遷移の判断（nudge セッションの開始・終了、ダブルクリックの確定/キャンセル、<br/>
///   ハンドルのヒットテストとスナップ、クリック検出、最小サイズによるキャンセル、エコー移動の抑制、カーソル種別の決定）。<br/>
/// - UI 層: 入力イベントの受付（座標の取得）と、本クラスが返す効果の適用・通知の公開イベントへの転送・状態の描画のみ。<br/>
/// <br/>
/// 座標系:<br/>
/// 選択矩形・nudge は対象座標で扱う。 UI 層は入力座標を <see cref="ToSource"/> で変換し、描画座標を <see cref="ToDip"/> で得る。<br/>
/// <br/>
/// 依存分離: コア型（<see cref="EsRect"/> / <see cref="EsPoint"/> / <see cref="EsKey"/>）と座標変換ポート（<see cref="ICoordTransform"/>）の<br/>
/// 抽象にのみ依存し、UI フレームワークへ依存しない。<br/>
/// カーソル操作（nudge 中の非表示・終了時の再表示）は効果（<see cref="MarqueeCursorActions"/>）として UI 層へ要求するだけで、本クラスはカーソル API を知らない。<br/>
/// UI 側の概念（マウスボタン・キー種別・コントロールへのフォーカス・画面座標）は、UI 層が本クラスの入出力へ変換する。<br/>
/// <br/>
/// 状態機械の保持と純粋計算は <see cref="MarqueeState"/> が担い、本クラスは入力の解釈と通知の組み立てに専念する（単一責任）。<br/>
/// <br/>
/// <b>可視性</b>: 本クラスは公開ファサード <see cref="MarqueeCore"/> の内部実装であるため internal とする<br/>
/// （UI 層・ホストは <see cref="MarqueeCore"/> を通して操作する）。
/// </remarks>
internal sealed class MarqueeSession
{
    /// <summary>選択・ドラッグ・nudge の状態と純粋計算。</summary>
    private readonly MarqueeState _state;

    /// <summary>内部ロガー（未設定は null。 読み取り時に <see cref="LoggerFallback"/> が吸収する）。</summary>
    private ILogger? _log;

    /// <summary>状態管理クラスを初期化する。</summary>
    /// <param name="options">初期設定。 null 可（null の場合は <see cref="MarqueeOptions.Default"/>）。</param>
    internal MarqueeSession(MarqueeOptions? options = null)
    {
        _state = new MarqueeState();
        Options = options ?? MarqueeOptions.Default;
    }

    /// <summary>上位（UI 層）へ伝える通知の購読（値型の通知のため <c>event</c> を使わない）。</summary>
    private readonly CallbackList<MarqueeNotice> _notified = new();

    /// <summary>上位（UI 層）へ伝える通知を購読する。</summary>
    /// <param name="notify">通知を受け取る処理。</param>
    /// <returns>購読を解除する <see cref="IDisposable"/>。</returns>
    /// <remarks>
    /// <b><c>event</c> を使わない理由</b>: <see cref="MarqueeNotice"/> は値型（<c>record struct</c>）であり、
    /// <see cref="System.EventHandler{T}"/> の <c>T</c>（<see cref="EventArgs"/> 派生）にできない。<br/>
    /// 値型のまま通知するため、ポインター移動のたびにヒープを確保しない
    /// （<c>MarqueeCoreTests.Move_ポインター移動はヒープを確保しない</c> が担保する）。
    /// </remarks>
    internal IDisposable Subscribe(Action<MarqueeNotice> notify) => _notified.Add(notify);

    /// <summary>選択・ドラッグ・nudge の状態（本クラスが内部で使用する状態機械）。</summary>
    /// <value>状態機械。 外部は <see cref="CreateSnapshot"/> または各読み取りプロパティを使う。</value>
    /// <remarks>
    /// 本プロパティはコア・テストからのみ参照する（状態機械は可変プロパティを持つため、外部からの直接操作を避ける）。<br/>
    /// ホストと UI 実装は <see cref="CreateSnapshot"/>（<see cref="MarqueeSnapshot"/>）または個別の読み取りプロパティを使う。
    /// </remarks>
    internal MarqueeState State => _state;

    /// <summary>ロガー。 null を設定した場合は出力なし。</summary>
    /// <value>注入されたロガー。 null 可（null の場合は <see cref="NullLogger"/> へフォールバックする）。</value>
    internal ILogger Log
    {
        get => _log.OrNullLogger();
        set => _log = value;
    }

    /// <summary>現在の設定（寸法・対話パラメータ）。</summary>
    /// <value>反映済みの設定。 既定は <see cref="MarqueeOptions.Default"/>。</value>
    /// <remarks>本クラスは対話パラメータのみを使用する（外観は UI 層が描画時に読む）。</remarks>
    internal MarqueeOptions Options { get; private set; } = MarqueeOptions.Default;

    /// <summary>現在の選択矩形（対象座標）。</summary>
    /// <value>選択矩形。 未選択時は 0x0。</value>
    internal EsRect SelectionSourceRect => _state.SelectionSourceRect;

    /// <summary>選択矩形が有効かどうか（幅・高さが 0 より大きい）。</summary>
    /// <value>選択ありの場合は true。</value>
    internal bool HasSelection => _state.HasSelection;

    /// <summary>ドラッグ中かどうか（新規選択・移動・リサイズ）。</summary>
    /// <value>ドラッグ中の場合は true。</value>
    internal bool IsDragging => _state.IsDragging;

    /// <summary>nudge（矢印キー微調整）中かどうか。</summary>
    /// <value>nudge 中の場合は true。</value>
    internal bool NudgeActive => _state.NudgeActive;

    /// <summary>直近のマウス位置（対象座標。 カーソル座標ラベルの描画に使用する）。</summary>
    /// <value>直近のマウス位置。 未移動時は既定値。</value>
    internal EsPoint LastMousePosition => _state.LastMousePosition;

    /// <summary>十字マーカーが追従する位置（対象座標）。</summary>
    /// <value>ドラッグ中のハンドル位置・新規選択中の終点・未選択時の始点。</value>
    internal EsPoint MarkerPosition => _state.GetMarkerPosition();

    /// <summary>公開イベント用の状態スナップショットを生成する。</summary>
    /// <param name="mousePosition">スナップショットに含めるマウス位置（対象座標）。 null の場合は直近の位置。</param>
    /// <returns>状態・選択矩形・アクティブハンドル・マウス位置・操作フラグを含むスナップショット。</returns>
    internal MarqueeSnapshot CreateSnapshot(EsPoint? mousePosition = null) => _state.CreateSnapshot(mousePosition);

    /// <summary>対象 ↔ ローカル DIP の座標変換ポートを設定する。</summary>
    /// <param name="port">座標変換ポート。 null の場合は等価変換（1 対象ピクセル = 1 DIP・無制限）。</param>
    internal void SetPort(ICoordTransform? port) => _state.SetPort(port.OrIdentity());

    /// <summary>ローカル DIP 座標を対象座標へ変換する。</summary>
    /// <param name="localDip">ローカル DIP 座標。</param>
    /// <returns>対象座標。</returns>
    internal EsPoint ToSource(EsPoint localDip) => _state.Port.ToSource(localDip);

    /// <summary>対象座標をローカル DIP 座標へ変換する。</summary>
    /// <param name="source">対象座標。</param>
    /// <returns>ローカル DIP 座標。</returns>
    internal EsPoint ToDip(EsPoint source) => _state.Port.ToDip(source);

    // ====================================================================
    // 入力の通知（UI 層 → コア）
    // ====================================================================

    /// <summary>ポインター押下（左ボタン）を通知する。</summary>
    /// <param name="position">押下位置（対象座標）。</param>
    /// <param name="clickCount">クリック数（2 以上 = ダブルクリック）。</param>
    /// <returns>UI 層が適用する効果（フォーカス取得・再描画・カーソル操作）。</returns>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. nudge 中なら nudge を終了し、押下位置を nudge 後の始点に置き換える（ずれの防止）。<br/>
    /// 2. ダブルクリックは選択矩形の内側なら確定要求、外側ならキャンセル要求を通知して終了する。<br/>
    /// 3. ハンドルにヒットした場合はリサイズ / 移動を開始する（移動以外はポインターをハンドル点へスナップする）。<br/>
    /// 4. ヒットしない場合は新規選択を開始する。<br/>
    /// ハンドルが無い場合も <c>DragStart: handle=None</c> として記録する（新規選択のドラッグ開始も同じ経過として扱う）。
    /// </remarks>
    internal MarqueeFeedback Press(EsPoint position, int clickCount)
    {
        _state.BeginPress();
        Log.LogPositionBeforeDown(position.X, position.Y);
        Log.LogMouseDown(position.X, position.Y, clickCount, _state.HasSelection);

        // nudge 中は nudge 位置を押下位置として扱い、ポインターを再表示する（nudge 位置と押下位置のずれを防ぐ）
        var cursor = MarqueeCursorActions.None;
        if (_state.NudgeActive)
        {
            position = _state.StartPoint;
            _state.EndNudge();
            cursor = MarqueeCursorActions.Show;
            NotifyAt(MarqueeNoticeKind.MarkerPositionMoved, position);
        }

        if (clickCount >= 2)
        {
            return DoubleClick(position, cursor);
        }

        var handle = _state.HitTest(position, ToSourceRadius());
        Log.LogDragStart(handle.ToString(), position.X, position.Y);
        var feedback = new MarqueeFeedback
        {
            Redraw = true,
            RequestFocus = true,
            Capture = MarqueeCaptureAction.Capture,
            Cursor = cursor,
        };

        if (handle != MarqueeHitTarget.None)
        {
            _state.StartDrag(handle, position);

            // リサイズハンドル（8 方向）ではポインターをハンドル点へスナップし、追従位置もハンドル点へ揃える
            // （ポインター・矩形の角・追従位置を一体にする）。 ドラッグ基準点もハンドル点に揃えることで、
            // スナップ後の最初の移動がハンドル点からの差分として正しく矩形へ反映される。
            // 移動（Move）は掴んだ位置を維持するのが自然なためスナップしない。
            if (handle != MarqueeHitTarget.Move)
            {
                var handlePosition = new MarqueeSelection(_state.SelectionSourceRect).Handle(handle);
                _state.SnapToHandle(handlePosition);
                feedback = feedback with
                {
                    Cursor = cursor | MarqueeCursorActions.MoveTo,
                    CursorSourcePosition = handlePosition,
                };
                NotifyAt(MarqueeNoticeKind.MarkerPositionMoved, handlePosition);
            }
        }
        else
        {
            _state.StartNewSelection(position);
            NotifyAt(MarqueeNoticeKind.SelectionStarted, position);
        }

        return feedback;
    }

    /// <summary>ポインター移動を通知する。</summary>
    /// <param name="position">マウス位置（対象座標）。</param>
    /// <returns>UI 層が適用する効果（再描画・カーソル形状）。</returns>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. nudge 終了直後のエコー移動は、選択矩形を上書きせずカーソル形状のみ更新する（抑制は 1 回で解除する）。<br/>
    /// 2. マウス位置を通知し、カーソル形状を更新する。<br/>
    /// 3. nudge 中は選択矩形・マウス位置を更新しない（エコー移動で nudge の結果が戻るのを防ぐ）。<br/>
    /// 4. ドラッグ中は選択矩形を更新し、変更通知と追従位置（動いている側のハンドル）を通知する。<br/>
    /// 5. ドラッグ中でなければホバー位置を追従位置として通知する。
    /// </remarks>
    internal MarqueeFeedback Move(EsPoint position)
    {
        var hitRadius = ToSourceRadius();

        // nudge 終了（カーソル移動）に伴うエコー移動をスキップする（消費したら解除する）
        if (_state.ConsumeSuppressedEcho(position, hitRadius))
        {
            return new MarqueeFeedback
            {
                UpdateCursorShape = true,
                CursorKind = ToCursorKind(_state.HitTest(position, hitRadius)),
            };
        }

        var cursor = new MarqueeFeedback
        {
            UpdateCursorShape = true,
            CursorKind = ToCursorKind(_state.HitTest(position, hitRadius)),
        };

        // nudge 中はエコー移動で選択矩形・マウス位置を上書きしない（解放時の追従位置が実マウス位置へ飛ぶのを防ぐ）
        if (_state.NudgeActive)
        {
            return cursor;
        }

        _state.SetMousePosition(position);

        if (_state.IsDragging)
        {
            _state.UpdateDrag(position, Options.MinSelectionSize, Options.ClampToSourceBounds);
            NotifyState(MarqueeNoticeKind.SelectionChanged);
            NotifyAt(MarqueeNoticeKind.MarkerPositionMoved, _state.GetMarkerPosition());
        }
        else
        {
            NotifyAt(MarqueeNoticeKind.MarkerPositionMoved, position);
        }

        return cursor with { Redraw = true };
    }

    /// <summary>ポインター解放（左ボタン）を通知する。</summary>
    /// <param name="position">解放位置（対象座標）。</param>
    /// <returns>UI 層が適用する効果（再描画・カーソル操作）。</returns>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. ドラッグ中だった場合はドラッグを終了し、最小サイズ未満なら選択を破棄してキャンセル、それ以外は確定を通知する。<br/>
    /// 2. nudge 中だった場合はカーソルを nudge 位置へ戻して再表示し、その位置への直後の移動（エコー）を抑制する。<br/>
    /// nudge 位置はドラッグ終了処理の前に求める（終了処理がドラッグ状態を解除するため、後から求めると固定コーナーを返す）。
    /// </remarks>
    internal MarqueeFeedback Release(EsPoint position)
    {
        _state.EndPress();
        Log.LogMouseUp(position.X, position.Y, _state.IsDragging, _state.HasSelection);
        Log.LogPositionAfterUp(position.X, position.Y);

        var wasDragging = _state.IsDragging;
        var wasNudging = _state.NudgeActive;
        var nudgePosition = wasNudging ? _state.GetMarkerPosition() : default;

        if (wasDragging)
        {
            var cancelled = _state.SelectionSourceRect.Width < Options.MinSelectionSize
                || _state.SelectionSourceRect.Height < Options.MinSelectionSize;
            _state.EndDrag();

            if (cancelled)
            {
                _state.Reset();
                NotifyEmpty(MarqueeNoticeKind.SelectionCancelled);
            }
            else
            {
                NotifyState(MarqueeNoticeKind.SelectionCompleted);
            }
        }

        var feedback = new MarqueeFeedback { Redraw = true, Capture = MarqueeCaptureAction.Release };
        if (wasNudging)
        {
            // nudge の状態は最小サイズ未満のキャンセル経路（状態の初期化）で既に解除されている場合があるため、
            // 「nudge 中だった」の記録で再表示を要求する（カーソルの非表示残留を防ぐ）。
            // nudge 中にユーザーが物理マウスを動かしても、カーソルは nudge 位置へ戻してから再表示する
            // （nudge から解放までのマウス移動はキャンセルする。 SetCursorScreenPoint は再表示前に行う）
            _state.EndNudge();
            feedback = feedback with { Cursor = MarqueeCursorActions.Show };
            NotifyAt(MarqueeNoticeKind.MarkerPositionMoved, nudgePosition);
            _state.SuppressEcho(nudgePosition);

            // カーソルは nudge 位置（ハンドル上）にあるため、ハンドルに応じた形状へ即時更新する
            // （マウスを動かすまで移動イベントが発火せず、形状が更新されないため）
            feedback = feedback with
            {
                Cursor = feedback.Cursor | MarqueeCursorActions.MoveTo,
                CursorSourcePosition = nudgePosition,
                UpdateCursorShape = true,
                CursorKind = ToCursorKind(_state.HitTest(nudgePosition, ToSourceRadius())),
            };
        }

        return feedback;
    }

    /// <summary>キー入力を通知する（キャンセル・確定・矢印キーの微調整）。</summary>
    /// <param name="key">押下されたキー。</param>
    /// <param name="cursorPosition">押下時点のカーソル位置（対象座標）。 取得できない場合は null。</param>
    /// <returns>UI 層が適用する効果（処理済み・再描画・カーソル操作）。</returns>
    /// <remarks>
    /// Escape はキャンセル要求、Enter は確定要求として扱い、常に処理済みとする。<br/>
    /// それ以外は矢印キーとして <see cref="NudgeByKey"/> へ委譲する。<br/>
    /// 「領域作成後はマウス押下中のみ nudge を許可する」条件も <see cref="NudgeByKey"/> が判断する。<br/>
    /// <b>Enter と Return は同一のキー</b>として扱う（UI フレームワークの <c>Key</c> は両者が同値のエイリアスのため、
    /// 名前ベースの相互変換では <c>Return</c> が得られ、UI ごとに届く値が異なる）。
    /// </remarks>
    internal MarqueeFeedback KeyDown(EsKey key, EsPoint? cursorPosition)
    {
        if (key == EsKey.Escape)
        {
            Log.LogKeyDown(key.ToString(), handled: true);
            return RequestCancel() with { Handled = true };
        }

        if (key is EsKey.Enter or EsKey.Return)
        {
            Log.LogKeyDown(key.ToString(), handled: true);
            return RequestConfirm() with { Handled = true };
        }

        var feedback = NudgeByKey(key, cursorPosition);
        if (feedback.Handled)
        {
            Log.LogKeyDown(key.ToString(), handled: true);
        }
        return feedback;
    }

    /// <summary>矢印キーによる微調整（nudge）を通知する。</summary>
    /// <param name="key">押下されたキー。</param>
    /// <param name="cursorPosition">押下時点のカーソル位置（対象座標）。 取得できない場合は null。</param>
    /// <returns>UI 層が適用する効果（処理済み・再描画・カーソル操作）。</returns>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. 領域作成後はマウス押下中のみ許可する（ドラッグ完了後の nudge は無効）。<br/>
    /// 2. 矢印キー以外は処理しない。<br/>
    /// 3. 押下時点のカーソル位置を記録する（基点の精度向上。 未移動時に基点が原点になり飛ぶのを防ぐ）。<br/>
    /// 4. 未選択・未ドラッグで nudge セッション開始時のみ基点をカーソル位置へ同期する。<br/>
    /// 5. nudge セッションを開始する（カーソル非表示・十字マーカー表示）。<br/>
    /// 6. 1 対象ピクセル単位の整数加算で矩形を更新し、変更通知と追従位置を通知する。<br/>
    /// nudge 中（2 回目以降）は基点を再同期しない（座標変換の丸め誤差・スクロール追従で移動が相殺されるのを防ぐ）。
    /// </remarks>
    internal MarqueeFeedback NudgeByKey(EsKey key, EsPoint? cursorPosition)
    {
        if (_state.HasSelection && !_state.IsMouseDown)
        {
            return default;
        }

        var (dx, dy) = key switch
        {
            EsKey.Left => (-1.0, 0.0),
            EsKey.Right => (1.0, 0.0),
            EsKey.Up => (0.0, -1.0),
            EsKey.Down => (0.0, 1.0),
            _ => (0.0, 0.0),
        };
        if (dx == 0 && dy == 0)
        {
            return default;
        }

        if (cursorPosition is { } current)
        {
            _state.SetMousePosition(current);
        }

        var feedback = new MarqueeFeedback { Handled = true, Redraw = true };
        if (_state.BeginNudge())
        {
            feedback = feedback with { Cursor = MarqueeCursorActions.Hide };
        }

        // nudge 適用（対象座標の整数加算のみ。 浮動小数の誤差を累積しない）
        _state.Nudge(dx, dy, Options.MinSelectionSize, Options.ClampToSourceBounds);
        NotifyState(MarqueeNoticeKind.SelectionChanged);

        // カーソルを動かしている側のハンドル（マーカー位置）へ合わせる
        var marker = _state.GetMarkerPosition();
        NotifyAt(MarqueeNoticeKind.MarkerPositionMoved, marker);
        return feedback with
        {
            Cursor = feedback.Cursor | MarqueeCursorActions.MoveTo,
            CursorSourcePosition = marker,
        };
    }

    /// <summary>nudge を終了する（カーソルを再表示する）。</summary>
    /// <returns>UI 層が適用する効果（nudge 中だった場合は再描画とカーソルの再表示）。</returns>
    /// <remarks>
    /// nudge 中の状態破棄（フォーカス喪失）で使用する。<br/>
    /// nudge 中でない場合は何もしない（<c>default</c> を返す）。
    /// </remarks>
    internal MarqueeFeedback EndNudge() => WithNudgeEnded(default);

    // ====================================================================
    // 外部（ホスト）からの操作
    // ====================================================================

    /// <summary>選択矩形を外部状態から復元する。</summary>
    /// <param name="rect">選択矩形（対象座標）。</param>
    /// <returns>UI 層が適用する効果（再描画。 nudge 中だった場合はカーソルの再表示）。</returns>
    /// <remarks>
    /// <see cref="MarqueeOptions.ClampToSourceBounds"/> が有効な場合は、はみ出す矩形を境界内へクリップしてから設定する<br/>
    /// （外部からの復元でも「画像外の選択を禁止」を尊重する）。 設定後は選択変更を通知する。
    /// </remarks>
    internal MarqueeFeedback SetSelection(EsRect rect)
    {
        // 選択の復元は nudge 状態を解除するため、解除の判定は復元より前に行う（カーソルの再表示を落とさない）
        var feedback = WithNudgeEnded(new MarqueeFeedback { Redraw = true });
        var selectionSourceRect = Options.ClampToSourceBounds ? _state.ClipRectToBounds(rect) : rect;
        _state.SetSelection(selectionSourceRect);
        NotifyState(MarqueeNoticeKind.SelectionChanged);
        return feedback;
    }

    /// <summary>選択状態と nudge 状態を初期化する。</summary>
    /// <returns>UI 層が適用する効果（再描画。 nudge 中だった場合はカーソルの再表示）。</returns>
    internal MarqueeFeedback Reset()
    {
        var feedback = WithNudgeEnded(new MarqueeFeedback { Redraw = true });
        _state.Reset();
        return feedback;
    }

    /// <summary>確定要求を処理する（Enter キー・ダブルクリック・ホストからの API 呼び出しと同じ経路）。</summary>
    /// <returns>UI 層が適用する効果（確定した場合は再描画。 nudge 中だった場合はカーソルの再表示）。</returns>
    /// <remarks>
    /// ドラッグ中は無視し、選択確定後のみ確定要求を通知する（通知時点の選択矩形を含む）。<br/>
    /// 確定後は選択状態をクリアする（確定後の再ドラッグ・再確定を防ぐ）。
    /// </remarks>
    internal MarqueeFeedback RequestConfirm()
    {
        if (_state.IsDragging || !_state.HasSelection)
        {
            return default;
        }

        Log.LogConfirmRequested(_state.SelectionSourceRect);
        NotifyState(MarqueeNoticeKind.ConfirmRequested);
        var feedback = WithNudgeEnded(new MarqueeFeedback { Redraw = true });
        _state.Reset();
        return feedback;
    }

    /// <summary>キャンセル要求を処理する（Escape キー・枠外ダブルクリック・ホストからの API 呼び出しと同じ経路）。</summary>
    /// <returns>UI 層が適用する効果（選択を破棄した場合または nudge 中だった場合は再描画。 nudge 中だった場合はカーソルの再表示）。</returns>
    /// <remarks>ドラッグ中または選択ありの場合は選択を破棄した後にキャンセル要求を通知する。</remarks>
    internal MarqueeFeedback RequestCancel()
    {
        Log.LogCancelRequested();
        // キャンセルは nudge も終了させる（nudge 位置の十字マーカーとカーソル非表示を残留させない）
        var feedback = WithNudgeEnded(new MarqueeFeedback { Redraw = false });
        if (_state.IsDragging || _state.HasSelection)
        {
            _state.Reset();
            feedback = feedback with { Redraw = true };
        }
        NotifyEmpty(MarqueeNoticeKind.CancelRequested);
        return feedback;
    }

    /// <summary>対象の境界外の選択の禁止設定の変更を現在の選択矩形へ適用する。</summary>
    /// <returns>UI 層が適用する効果（再描画）。</returns>
    /// <remarks>
    /// 設定が ON に切り替わった瞬間に、既存の選択矩形を境界内へクリップして即時反映する（ドラッグ開始を待たない）。<br/>
    /// クリップで矩形が変更された場合のみ選択変更を通知する。
    /// </remarks>
    internal MarqueeFeedback ApplyClampToBounds()
    {
        if (_state.ClampSelectionToBounds())
        {
            NotifyState(MarqueeNoticeKind.SelectionChanged);
        }
        return new MarqueeFeedback { Redraw = true };
    }

    /// <summary>設定（<see cref="MarqueeOptions"/>）を反映する。</summary>
    /// <param name="options">反映する設定（null 不可）。</param>
    /// <returns>UI 層が適用する効果（境界外選択の禁止が変化した場合のみ再描画）。</returns>
    /// <remarks>
    /// 1. 設定を取り込み、対話パラメータ（最小サイズ・ヒットテスト半径・境界外選択の禁止）を更新する。<br/>
    /// 2. 変化した項目をログ出力する（差分検出はログ定義が単一所有する）。<br/>
    /// 3. 境界外選択の禁止が変化した場合は既存の選択矩形へ即時適用する。<br/>
    /// <br/>
    /// 同値の設定を再適用しても効果・ログを発生させない（UI 層は変更内容を確認せず委譲できる）。
    /// </remarks>
    internal MarqueeFeedback ApplyOptions(MarqueeOptions options)
    {
        var previous = Options;
        Options = options;

        if (previous == options)
        {
            return default;
        }

        Log.LogOptionsChanged(previous, options);

        // 即時クリップは「境界外選択の禁止」が実際に変化した場合のみ行う
        return previous.ClampToSourceBounds != options.ClampToSourceBounds ? ApplyClampToBounds() : default;
    }

    // ====================================================================
    // 内部ヘルパー
    // ====================================================================

    /// <summary>nudge 中なら nudge を終了し、カーソルの再表示と再描画を効果に加える。</summary>
    /// <param name="feedback">nudge 終了前の効果。</param>
    /// <returns>nudge 中だった場合は再描画とカーソル再表示を加えた効果。 nudge 中でなければ元の効果。</returns>
    /// <remarks>
    /// カーソルの再表示は本メソッドが単一所有する（状態は状態のみを更新し、画面への反映は効果として UI 層へ要求する）。<br/>
    /// nudge 状態を解除する操作（<see cref="MarqueeState.Reset"/> / <see cref="MarqueeState.SetSelection"/>）より<b>前に</b>呼ぶ<br/>
    /// （解除後は「nudge 中だったか」を判定できず、カーソルが非表示のまま残留する）。
    /// </remarks>
    private MarqueeFeedback WithNudgeEnded(in MarqueeFeedback feedback)
        => _state.EndNudge()
            ? feedback with { Redraw = true, Cursor = feedback.Cursor | MarqueeCursorActions.Show }
            : feedback;

    /// <summary>ダブルクリックを処理する（選択矩形の内側なら確定、外側ならキャンセル）。</summary>
    /// <param name="position">ダブルクリック位置（対象座標）。</param>
    /// <param name="cursor">適用済みのカーソル操作。</param>
    /// <returns>UI 層が適用する効果。</returns>
    /// <remarks>
    /// ドラッグ処理は行わない（ダブルクリックは確定/キャンセルの操作として扱う）。<br/>
    /// 確定した場合は <see cref="MarqueeState.Reset"/> により選択を破棄するため再描画を要求する。
    /// </remarks>
    private MarqueeFeedback DoubleClick(EsPoint position, MarqueeCursorActions cursor)
    {
        var inside = _state.HasSelection && _state.SelectionSourceRect.Contains(position);
        Log.LogDoubleClick(position.X, position.Y, inside);

        if (inside)
        {
            Log.LogConfirmRequested(_state.SelectionSourceRect);
            NotifyAt(MarqueeNoticeKind.ConfirmRequested, position);
            _state.Reset();
            return new MarqueeFeedback { Redraw = true, Cursor = cursor };
        }

        Log.LogCancelRequested();
        NotifyEmpty(MarqueeNoticeKind.CancelRequested);
        return new MarqueeFeedback { Cursor = cursor };
    }

    /// <summary>DIP 半径を対象半径へ変換する（ヒットテスト用）。</summary>
    /// <returns>対象座標系でのヒットテスト半径。</returns>
    /// <remarks>
    /// <see cref="ICoordTransform.ToSource"/> の差を取ることでオフセット（レターボックス等）をキャンセルし、スケールのみを得る。<br/>
    /// スケールがほぼ 0（等価変換）の場合は DIP 半径をそのまま使用する。
    /// </remarks>
    private double ToSourceRadius()
    {
        var port = _state.Port;
        var origin = port.ToSource(default);
        var offset = port.ToSource(new EsPoint(Options.HandleHitRadius, 0));
        var radius = Math.Abs(offset.X - origin.X);
        return radius < 0.001 ? Options.HandleHitRadius : radius;
    }

    /// <summary>ハンドル種別をカーソル形状の種別へ変換する。</summary>
    /// <param name="handle">ハンドル種別。</param>
    /// <returns>カーソル形状の種別。 ハンドル外（<see cref="MarqueeHitTarget.None"/>）の場合は <see cref="MarqueeCursorKind.Default"/>。</returns>
    /// <remarks>
    /// 「どのハンドルが同じカーソル形状になるか」を単一所有する（UI 層は自 UI のカーソル型へ写像するだけ）。<br/>
    /// 左上と右下、右上と左下は同じ斜めカーソルになる（対角のハンドルは同じ向きの図形で表す）。
    /// </remarks>
    internal static MarqueeCursorKind ToCursorKind(MarqueeHitTarget handle) => handle switch
    {
        MarqueeHitTarget.TopLeft or MarqueeHitTarget.BottomRight => MarqueeCursorKind.DiagonalNWSE,
        MarqueeHitTarget.TopRight or MarqueeHitTarget.BottomLeft => MarqueeCursorKind.DiagonalNESW,
        MarqueeHitTarget.TopCenter or MarqueeHitTarget.BottomCenter => MarqueeCursorKind.Vertical,
        MarqueeHitTarget.MiddleLeft or MarqueeHitTarget.MiddleRight => MarqueeCursorKind.Horizontal,
        MarqueeHitTarget.Move => MarqueeCursorKind.Move,
        _ => MarqueeCursorKind.Default,
    };

    /// <summary>点座標付きの通知を発火する（スナップショットのマウス位置も指定座標とする）。</summary>
    /// <param name="kind">通知の種別。</param>
    /// <param name="position">通知する座標（対象座標）。</param>
    private void NotifyAt(MarqueeNoticeKind kind, EsPoint position)
        => _notified.Raise(new MarqueeNotice(kind, position, _state.CreateSnapshot(position)));

    /// <summary>現在のマウス位置で通知を発火する（選択矩形の変更・確定など、通知座標を持たない通知）。</summary>
    /// <param name="kind">通知の種別。</param>
    private void NotifyState(MarqueeNoticeKind kind)
        => _notified.Raise(new MarqueeNotice(kind, _state.LastMousePosition, _state.CreateSnapshot()));

    /// <summary>座標を持たない通知を発火する（キャンセルなど）。</summary>
    /// <param name="kind">通知の種別。</param>
    private void NotifyEmpty(MarqueeNoticeKind kind)
        => _notified.Raise(new MarqueeNotice(kind, default, _state.CreateSnapshot()));
}
