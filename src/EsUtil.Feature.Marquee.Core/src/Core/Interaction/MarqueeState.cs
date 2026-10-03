using EsUtil.Feature.Marquee.Core.Selection;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Marquee.Core.Interaction;

/// <summary>選択・ドラッグ・nudge の状態を保持し、状態遷移の操作と読み取りを提供する内部クラス。</summary>
/// <remarks>
/// <b>適用パターン</b>: StateMachine（状態と遷移を 1 つの型に閉じ込める）。<br/>
/// <br/>
/// 選択矩形（対象座標）・ドラッグ状態・アクティブハンドル・nudge 状態を単一クラスに集約する。<br/>
/// <b>状態は操作（メソッド）でのみ変わる</b>: 可変プロパティを公開しないため、呼び出し側が
/// 「ドラッグ中なのに選択が無い」のような矛盾した状態を作れない（読み取りはプロパティ、変更は操作）。<br/>
/// <br/>
/// 座標系:<br/>
/// - 選択矩形・nudge は対象座標（ToSource = UI 論理座標→対象 / ToDip = 対象→UI 論理座標）で動作する。<br/>
/// - マウス入力・描画は各 UI 実装が UI 論理座標で受け取り、座標変換ポート（<see cref="ICoordTransform"/>）で変換する。<br/>
/// <br/>
/// カーソルの表示切替（nudge 中の非表示・終了時の再表示）は<b>効果</b>としてセッションが UI 層へ要求するため、本クラスはカーソル API を知らない。<br/>
/// <b>依存分離</b>: コア型（<see cref="EsRect"/> / <see cref="EsPoint"/>）のみに依存し、UI フレームワークへ依存しない。<br/>
/// <b>可視性</b>: 本クラスは <see cref="MarqueeSession"/> の内部実装である。 外部は <see cref="MarqueeSnapshot"/> を通して状態を読む。
/// </remarks>
internal sealed class MarqueeState
{
    /// <summary>選択矩形（対象座標）。 未選択は幅・高さが 0。</summary>
    private MarqueeSelection _selection;

    /// <summary>選択矩形（対象座標）。 未選択時は 0x0。</summary>
    internal EsRect SelectionSourceRect => _selection.Rect;

    /// <summary>選択矩形が有効かどうか（幅・高さが 0 より大きい）。</summary>
    internal bool HasSelection => !SelectionSourceRect.IsEmpty;

    /// <summary>ドラッグの始点（対象座標）。 未選択時の nudge の基点にもなる。</summary>
    internal EsPoint StartPoint { get; private set; }

    /// <summary>直近のマウス位置（対象座標）。</summary>
    internal EsPoint LastMousePosition { get; private set; }

    /// <summary>ドラッグ中かどうか（新規選択・移動・リサイズ）。</summary>
    internal bool IsDragging { get; private set; }

    /// <summary>マウスボタン（左）が押下中かどうか。</summary>
    internal bool IsMouseDown { get; private set; }

    /// <summary>アクティブなドラッグハンドル（リサイズ・移動）。<see cref="MarqueeHitTarget.None"/> は新規選択。</summary>
    internal MarqueeHitTarget ActiveTarget { get; private set; }

    /// <summary>nudge（矢印キー微調整）中かどうか。</summary>
    internal bool NudgeActive { get; private set; }

    /// <summary>座標変換ポート（ToSource = UI 論理座標→対象 / ToDip = 対象→UI 論理座標 / SourceArea = 対象の境界）。</summary>
    /// <remarks>環境値（注入された依存）であるため、設定は <see cref="SetPort"/> で行う。</remarks>
    internal ICoordTransform Port { get; private set; } = CoordTransform.Identity;

    /// <summary>ドラッグ開始時の矩形（移動・リサイズの基準）。</summary>
    private EsRect _dragStartRect;

    /// <summary>ドラッグ開始時のマウス位置（移動・リサイズの基準・対象座標）。</summary>
    private EsPoint _dragStartPoint;

    /// <summary>nudge 終了（カーソル位置移動）直後のエコー移動イベントの抑制位置（対象座標）。 null は抑制なし。</summary>
    private EsPoint? _suppressEchoAt;

    /// <summary>座標変換ポートを設定する。</summary>
    /// <param name="port">座標変換ポート。 null 不可（null を等価変換へ置換する規則は呼び出し側が単一所有する）。</param>
    internal void SetPort(ICoordTransform port) => Port = port;

    /// <summary>ポインター押下を記録する。</summary>
    internal void BeginPress() => IsMouseDown = true;

    /// <summary>ポインター解放を記録する。</summary>
    internal void EndPress() => IsMouseDown = false;

    /// <summary>マウス位置を記録する。</summary>
    /// <param name="position">マウス位置（対象座標）。</param>
    internal void SetMousePosition(EsPoint position) => LastMousePosition = position;

    /// <summary>現在の対話状態（公開イベントの Snapshot 用）を算出する。</summary>
    /// <returns>対話状態（NoSelection / StartPointNudging / Dragging / DraggingNudging / Selected / SelectedNudging）。</returns>
    /// <remarks>上位の描画モード判定用に、nudge・ドラッグ・選択の各状態を合成した状態を返す。</remarks>
    internal MarqueeInteractionState GetInteractionState()
    {
        // 優先順位: nudge 状態を最優先で合成し、次にドラッグ/選択状態を判定する
        if (NudgeActive)
        {
            return HasSelection ? MarqueeInteractionState.SelectedNudging
                : IsDragging ? MarqueeInteractionState.DraggingNudging
                : MarqueeInteractionState.StartPointNudging;
        }

        return IsDragging ? MarqueeInteractionState.Dragging
            : HasSelection ? MarqueeInteractionState.Selected
            : MarqueeInteractionState.NoSelection;
    }

    /// <summary>公開イベント用のスナップショットを生成する。</summary>
    /// <param name="mousePosition">通知時点のマウス位置（対象座標）。 省略時は最終位置。</param>
    /// <returns>状態・選択矩形・アクティブハンドル・マウス位置・ボタン状態・ドラッグ状態・nudge 状態を含むスナップショット。</returns>
    internal MarqueeSnapshot CreateSnapshot(EsPoint? mousePosition = null)
        => new(
            GetInteractionState(),
            SelectionSourceRect,
            ActiveTarget,
            mousePosition ?? LastMousePosition,
            IsMouseDown,
            NudgeActive);

    /// <summary>選択状態と nudge 状態を初期化する。</summary>
    /// <remarks>nudge 中だった場合は nudge 状態を終了する（カーソルの再表示はセッションが効果として要求する）。</remarks>
    internal void Reset()
    {
        EndNudge();
        _selection = default;
        StartPoint = default;
        LastMousePosition = default;
        IsDragging = false;
        IsMouseDown = false;
        ActiveTarget = MarqueeHitTarget.None;
        _dragStartRect = default;
        _dragStartPoint = default;
        _suppressEchoAt = null;
    }

    /// <summary>選択矩形を外部状態から復元する。</summary>
    /// <param name="rect">選択矩形（対象座標）。</param>
    /// <remarks>nudge 中だった場合は nudge 状態を終了する（カーソルの再表示はセッションが効果として要求する）。</remarks>
    internal void SetSelection(EsRect rect)
    {
        EndNudge();
        _selection = new MarqueeSelection(rect);
        IsDragging = false;
        ActiveTarget = MarqueeHitTarget.None;
    }

    /// <summary>nudge 状態を開始する。</summary>
    /// <returns>新規に nudge を開始した場合は true（既に nudge 中の場合は何もせず false）。</returns>
    /// <remarks>
    /// 未選択・未ドラッグの nudge では基点（<see cref="StartPoint"/>）を現在のカーソル位置へ同期する<br/>
    /// （カーソル位置が未取得のまま nudge すると基点が原点になり矩形が飛ぶため）。<br/>
    /// nudge 中（2 回目以降）は基点を再同期しない（座標変換の丸め誤差・スクロール追従で移動が相殺されるのを防ぐ）。
    /// </remarks>
    internal bool BeginNudge()
    {
        if (NudgeActive)
        {
            return false;
        }

        if (!HasSelection && !IsDragging)
        {
            StartPoint = LastMousePosition;
        }

        NudgeActive = true;
        return true;
    }

    /// <summary>nudge 状態を終了する。</summary>
    /// <returns>nudge 中だった場合は true（カーソルの再表示が必要かどうかの判定材料）。</returns>
    /// <remarks>本メソッドは状態のみを更新する（カーソルの再表示は効果としてセッションが UI 層へ要求する）。</remarks>
    internal bool EndNudge()
    {
        if (!NudgeActive)
        {
            return false;
        }

        NudgeActive = false;
        return true;
    }

    /// <summary>nudge 終了直後のエコー移動を抑制する位置を記録する。</summary>
    /// <param name="position">抑制する位置（対象座標）。</param>
    /// <remarks>カーソルを nudge 位置へ戻した直後に届く、実マウス位置への移動イベントを無視するために使う。</remarks>
    internal void SuppressEcho(EsPoint position) => _suppressEchoAt = position;

    /// <summary>指定位置が抑制位置の近傍なら抑制を解除する。</summary>
    /// <param name="position">比較する位置（対象座標）。</param>
    /// <param name="hitRadius">近傍とみなす半径（対象座標）。</param>
    /// <returns>抑制を解除した場合は true（呼び出し側は移動を適用しない）。</returns>
    /// <remarks>抑制は 1 回で解除する（以降の移動イベントは通常どおり適用する）。</remarks>
    internal bool ConsumeSuppressedEcho(EsPoint position, double hitRadius)
    {
        if (_suppressEchoAt is not { } echoPosition || !IsNear(position, echoPosition, hitRadius))
        {
            return false;
        }

        _suppressEchoAt = null;
        return true;
    }

    /// <summary>2 点が指定半径以内か判定する（各軸独立の距離 = 矩形の近傍判定）。</summary>
    /// <param name="a">1 点目（対象座標）。</param>
    /// <param name="b">2 点目（対象座標）。</param>
    /// <param name="radius">判定半径（対象座標）。 0 以上。</param>
    /// <returns>半径以内の場合は true。</returns>
    /// <remarks>ヒットテストと nudge 終了直後のエコー抑制が同じ判定を使う（判定規則の単一所有）。</remarks>
    internal static bool IsNear(EsPoint a, EsPoint b, double radius)
        => Math.Abs(a.X - b.X) <= radius && Math.Abs(a.Y - b.Y) <= radius;

    /// <summary>点を境界内にクリップする。</summary>
    /// <param name="point">クリップする点（対象座標）。</param>
    /// <param name="bounds">クリップ範囲（対象座標）。</param>
    /// <returns>境界内に収めた点。</returns>
    /// <remarks>新規選択の始点・終点を画像外へ出さないために使う（開始点が画像外でもはみ出さない）。</remarks>
    internal static EsPoint ClampPointToBounds(EsPoint point, EsRect bounds)
        => new(
            Math.Clamp(point.X, bounds.X, bounds.Right),
            Math.Clamp(point.Y, bounds.Y, bounds.Bottom));

    /// <summary>指定座標がどのドラッグハンドルに当たるかを判定する。</summary>
    /// <param name="pos">判定する座標（対象座標）。</param>
    /// <param name="hitRadius">ヒットテスト半径（対象座標）。</param>
    /// <returns>ヒットしたハンドル種別。 選択矩形が空の場合は常に <see cref="MarqueeHitTarget.None"/>。</returns>
    /// <remarks>
    /// ハンドル位置は <see cref="MarqueeSelection.Handle"/> から取得し、各ハンドルへの距離判定でヒットを確定する（ハンドル位置計算の DRY）。<br/>
    /// 判定順は「角（4 方向）→ 辺（4 方向）→ 矩形内（Move）」で、角が辺より優先される。
    /// </remarks>
    internal MarqueeHitTarget HitTest(EsPoint pos, double hitRadius)
    {
        if (!HasSelection) return MarqueeHitTarget.None;

        // 8 方向のハンドル位置を列挙してヒット判定する（角優先 → 辺 → Move の順）
        var selection = _selection;
        if (IsNear(pos, selection.Handle(MarqueeHitTarget.TopLeft), hitRadius)) return MarqueeHitTarget.TopLeft;
        if (IsNear(pos, selection.Handle(MarqueeHitTarget.TopRight), hitRadius)) return MarqueeHitTarget.TopRight;
        if (IsNear(pos, selection.Handle(MarqueeHitTarget.BottomLeft), hitRadius)) return MarqueeHitTarget.BottomLeft;
        if (IsNear(pos, selection.Handle(MarqueeHitTarget.BottomRight), hitRadius)) return MarqueeHitTarget.BottomRight;
        if (IsNear(pos, selection.Handle(MarqueeHitTarget.TopCenter), hitRadius)) return MarqueeHitTarget.TopCenter;
        if (IsNear(pos, selection.Handle(MarqueeHitTarget.BottomCenter), hitRadius)) return MarqueeHitTarget.BottomCenter;
        if (IsNear(pos, selection.Handle(MarqueeHitTarget.MiddleLeft), hitRadius)) return MarqueeHitTarget.MiddleLeft;
        if (IsNear(pos, selection.Handle(MarqueeHitTarget.MiddleRight), hitRadius)) return MarqueeHitTarget.MiddleRight;
        if (selection.Rect.Contains(pos)) return MarqueeHitTarget.Move;
        return MarqueeHitTarget.None;
    }

    /// <summary>新規選択のドラッグを開始する。</summary>
    /// <param name="startPos">始点座標（対象座標）。</param>
    internal void StartNewSelection(EsPoint startPos)
    {
        StartPoint = startPos;
        _selection = new MarqueeSelection(new EsRect(startPos.X, startPos.Y, 0, 0));
        IsDragging = true;
        ActiveTarget = MarqueeHitTarget.None;
        _dragStartPoint = startPos;
        _dragStartRect = SelectionSourceRect;
        LastMousePosition = startPos;
    }

    /// <summary>既存ハンドルによるリサイズ/移動のドラッグを開始する。</summary>
    /// <param name="handle">アクティブにするハンドル。</param>
    /// <param name="position">現在のマウス位置（対象座標）。</param>
    internal void StartDrag(MarqueeHitTarget handle, EsPoint position)
    {
        ActiveTarget = handle;
        IsDragging = true;
        _dragStartPoint = position;
        _dragStartRect = SelectionSourceRect;
        LastMousePosition = position;
    }

    /// <summary>ドラッグの基準点とマウス位置を指定位置へスナップする。</summary>
    /// <param name="position">スナップ先（ハンドルの位置・対象座標）。</param>
    /// <remarks>
    /// リサイズハンドル（8 方向）の押下時に使う（ポインター・矩形の角・追従位置を一体にする）。<br/>
    /// 基準点をハンドル点へ揃えることで、スナップ後の最初の移動がハンドル点からの差分として正しく矩形へ反映される。<br/>
    /// 移動（<see cref="MarqueeHitTarget.Move"/>）は掴んだ位置を維持するのが自然なためスナップしない。
    /// </remarks>
    internal void SnapToHandle(EsPoint position)
    {
        _dragStartPoint = position;
        LastMousePosition = position;
    }

    /// <summary>ドラッグ中のマウス移動を処理する。</summary>
    /// <param name="currentPos">現在のマウス位置（対象座標）。</param>
    /// <param name="minSize">選択矩形の最小サイズ。</param>
    /// <param name="clampToBounds">対象の境界内にクリップするかどうか。</param>
    /// <remarks>
    /// クリップ有効時（<paramref name="clampToBounds"/>）は対象の境界（<see cref="Port"/> の Bounds）内に収める:<br/>
    /// 1. ハンドル変形: 固定角を動かさず、動く辺のみ境界内に制限する。<br/>
    /// 2. 移動（Move）: 矩形全体が境界内に収まるようにシフトする。<br/>
    /// 3. 新規選択: 始点・終点の両方を境界内にクリップしてから矩形を作成する（始点が画像外の場合にはみ出さない）。<br/>
    /// </remarks>
    internal void UpdateDrag(EsPoint currentPos, double minSize, bool clampToBounds)
    {
        var bounds = GetBoundsRect(clampToBounds);

        if (ActiveTarget != MarqueeHitTarget.None)
        {
            // ハンドルによるリサイズ/移動（変形・クリップは MarqueeSelection へ委譲・単一所有）
            double dx = currentPos.X - _dragStartPoint.X;
            double dy = currentPos.Y - _dragStartPoint.Y;
            var dragged = new MarqueeSelection(_dragStartRect).Drag(dx, dy, ActiveTarget, minSize);
            _selection = bounds is { } b ? dragged.ClampToBounds(b, ActiveTarget) : dragged;
        }
        else if (IsDragging)
        {
            // 新規選択（始点→終点）: 始点・終点の両方を境界内にクリップしてから矩形を作成する
            // （始点が画像外（レターボックス等）で押下された場合でも、矩形が画像外にはみ出さないようにする）
            var start = bounds is { } b ? ClampPointToBounds(StartPoint, b) : StartPoint;
            var end = bounds is { } b2 ? ClampPointToBounds(currentPos, b2) : currentPos;
            _selection = MarqueeSelection.FromPoints(start, end);
        }
        LastMousePosition = currentPos;
    }

    /// <summary>対象の境界の矩形を取得する（クリップ有効時のみ）。</summary>
    /// <param name="clampToBounds">対象の境界内にクリップするかどうか。</param>
    /// <returns>対象の境界の矩形。 クリップ無効または境界未設定（null）の場合は null。</returns>
    /// <remarks><see cref="UpdateDrag"/> / <see cref="Nudge"/> で共通利用する（境界 Rect 生成の重複排除）。</remarks>
    private EsRect? GetBoundsRect(bool clampToBounds)
        => clampToBounds && Port.SourceArea is { } size ? new EsRect(0, 0, size.Width, size.Height) : null;

    /// <summary>ドラッグを終了する。</summary>
    internal void EndDrag()
    {
        IsDragging = false;
        ActiveTarget = MarqueeHitTarget.None;
    }

    /// <summary>キーボード微調整（カーソルキー 1 回分）を適用する。</summary>
    /// <param name="dx">X 方向の移動量（対象座標）。</param>
    /// <param name="dy">Y 方向の移動量（対象座標）。</param>
    /// <param name="minSize">選択矩形の最小サイズ。</param>
    /// <param name="clampToBounds">対象の境界内にクリップするかどうか。</param>
    /// <remarks>
    /// 状態に応じて動作が変化する:<br/>
    /// 1. ハンドル選択中（<see cref="ActiveTarget"/> 有効）: ハンドル方向にリサイズする。<br/>
    /// 2. ドラッグ中（<see cref="IsDragging"/>）: 終点を移動する（新規選択の延長線上）。<br/>
    /// 3. 選択確定後（<see cref="HasSelection"/>）: 終点（StartPoint でない方の隅）を移動する。<br/>
    /// 4. 未選択: 始点を移動する（次回クリック時の始点となる）。
    /// </remarks>
    internal void Nudge(double dx, double dy, double minSize, bool clampToBounds)
    {
        var bounds = GetBoundsRect(clampToBounds);

        if (ActiveTarget != MarqueeHitTarget.None)
        {
            // ハンドルドラッグ中 → ハンドル方向にリサイズ（変形・クリップは MarqueeSelection へ委譲・単一所有）
            var dragged = _selection.Drag(dx, dy, ActiveTarget, minSize);
            _selection = bounds is { } b ? dragged.ClampToBounds(b, ActiveTarget) : dragged;
            // ドラッグ基準を nudge 後の状態に合わせる（次の UpdateDrag で nudge が失われるのを防ぐ）
            _dragStartRect = SelectionSourceRect;
            _dragStartPoint = _selection.Handle(ActiveTarget);
            LastMousePosition = _dragStartPoint;
        }
        else if (IsDragging || HasSelection)
        {
            // 新規選択ドラッグ中 / 選択確定後 → 終点を微調整
            var end = _selection.OppositeCorner(StartPoint);
            end = new EsPoint(end.X + dx, end.Y + dy);
            end = bounds is { } b ? ClampPointToBounds(end, b) : end;
            // 始点も境界内にクリップしてから矩形を作成する（画像外の始点によるはみ出し防止）
            var start = bounds is { } b2 ? ClampPointToBounds(StartPoint, b2) : StartPoint;
            LastMousePosition = end;
            _selection = MarqueeSelection.FromPoints(start, end);
        }
        else
        {
            // 未選択・未ドラッグ → 始点を移動（次回クリック時の始点となる）
            StartPoint = new EsPoint(StartPoint.X + dx, StartPoint.Y + dy);
        }
    }

    /// <summary>十字マーカーが追従する位置（対象座標）を取得する。</summary>
    /// <returns>マーカーを表示する位置（対象座標）。</returns>
    /// <remarks>
    /// ドラッグ・nudge はカーソルを動かしながら矩形を変形するため、十字マーカーは「動いている側」を追従する:<br/>
    /// 1. ハンドル操作中（リサイズ）: アクティブハンドルの現在位置。<br/>
    /// 2. ハンドル操作中（移動）: 掴んだ位置（マウス位置）。<br/>
    /// 3. 新規選択ドラッグ中・nudge: 終点（クリップ・最小サイズ保証後の矩形の角）。<br/>
    /// 4. 未選択（始点決め nudge）: 始点。
    /// </remarks>
    internal EsPoint GetMarkerPosition()
    {
        // ハンドル操作中:
        // - リサイズ（8 方向）: ドラッグ中のハンドル位置（ポインタ・矩形の角・追従位置を一体にする）
        // - 移動（Move）: 掴んだ位置（マウス位置）を追従（追従位置が矩形中心に飛ばず、マウスポインタに追従する）
        if (ActiveTarget != MarqueeHitTarget.None)
        {
            return ActiveTarget == MarqueeHitTarget.Move
                ? LastMousePosition
                : _selection.Handle(ActiveTarget);
        }

        // 新規選択ドラッグ中・nudge: 終点（クリップ後の矩形の角。 nudge で追従）
        if (IsDragging)
        {
            return _selection.OppositeCorner(StartPoint);
        }

        // 未選択（始点決め nudge）: 始点
        return StartPoint;
    }

    /// <summary>現在の選択矩形を対象の境界（<see cref="Port"/> の Bounds）内にクリップして適用する。</summary>
    /// <returns>クリップによって矩形が変更された場合は true。</returns>
    /// <remarks>
    /// 「画像外の選択を禁止」が ON に切り替わった瞬間や、外部から復元した矩形が画像外にはみ出している場合に適用する。<br/>
    /// 純粋計算の <see cref="ClipRectToBounds"/> に委譲し、変更があった場合のみ状態へ適用する（純粋計算と状態適用の分離）。
    /// </remarks>
    internal bool ClampSelectionToBounds()
    {
        if (!HasSelection) return false;
        var clipped = ClipRectToBounds(SelectionSourceRect);
        if (clipped == SelectionSourceRect) return false;
        _selection = new MarqueeSelection(clipped);
        return true;
    }

    /// <summary>指定した矩形を対象の境界（<see cref="Port"/> の <see cref="ICoordTransform.SourceArea"/>）内にクリップする。</summary>
    /// <param name="rect">クリップする矩形（対象座標）。</param>
    /// <returns>境界内に収めた矩形。 境界が未設定（null）の場合は元の矩形。</returns>
    /// <remarks>
    /// 矩形の四隅を境界内にクリップし、その包絡矩形（bounding box）を返す。<br/>
    /// 矩形全体を境界内に収める（<see cref="SetSelection"/> 等の外部復元・設定変更時の即時適用用）。<br/>
    /// 包絡矩形の計算は <see cref="EsRect.ClampToArea(EsSize)"/> が単一所有する（四隅を個別にクランプし、その包絡を返す規則）。
    /// </remarks>
    internal EsRect ClipRectToBounds(EsRect rect)
        => Port.SourceArea is { } size ? rect.ClampToArea(size) : rect;
}
