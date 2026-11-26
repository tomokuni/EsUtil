using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Rendering;
using EsUtil.Feature.Marquee;
using EsUtil.Feature.Marquee.Core.Contract;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Input;
using EsUtil.Feature.Annotation.Core.Contract;
using EsUtil.Feature.Annotation.Core.Geometry;
using EsUtil.Feature.Annotation.Core.Interaction;
using EsUtil.Feature.Annotation.Presenter.Drawing;
using EsUtil.Feature.Annotation.Logging;

namespace EsUtil.Ui.MewUI.Annotation;

/// <summary>注釈レイヤーの描画と入力（MewUI 版）。</summary>
/// <remarks>
/// <b>描画の順序</b>: 画像 → 注釈（元画像ピクセルの座標系）→ 選択枠 → しっぽのハンドルの順に描く
/// （選択枠は DIP、注釈は元画像ピクセルで、表示倍率の変換を 1 回だけ掛ける）。<br/>
/// <b>入力は 1 本の経路</b>: 押下 → しっぽの先端 → ハンドル → 注釈の本体（Marquee へ転送）→ 背景の順に判定する
/// （判断はコアと Marquee の提示層が単一所有する）。
/// </remarks>
public sealed partial class AnnotationLayerControl
{
    /// <summary>注釈・画像・選択枠を描く。</summary>
    /// <param name="context">描画先。</param>
    /// <remarks>
    /// <b>描画の座標はウィンドウ基準である</b>: MewUI の <see cref="Element.Bounds"/> はウィンドウの
    /// 絶対座標で、描画コンテキストの原点もウィンドウの左上になる。 ここで自分の左上を足さないと、
    /// 図形とハンドルは互いに揃ったまま<b>画像から (Bounds.X, Bounds.Y) だけ左上へずれる</b>。
    /// </remarks>
    protected override void OnRender(IGraphicsContext context)
    {
        base.OnRender(context);

        // **測定と描画で同じ DPI を使う**: 文字のレイアウトは DPI によって送りが変わる。 画面の倍率を
        // 渡さないと、描画（実機の DPI）と測定（96 固定）で 1 文字あたり約 6.7% 食い違い、
        // 長い文字列ほど文字とキャレットがずれる。
        StyledText.RichTextPainter.DpiScale = context.DpiScale;

        context.Save();
        try
        {
            // 以降はローカル座標で描けるようにする（入力の座標系と一致させる）
            context.Translate(Bounds.X, Bounds.Y);
            var (scale, offset) = _frozenTransform ?? ResolveImageTransform();

            // **選択枠は編集中も描く**: 描く中身は DrawSelection が決める（編集中は破線だけを描き、
            // ハンドルは出さない）。 ここで止めると「どの注釈を編集中か」が分からなくなる
            DrawAll(context, includeSelection: true, scale, new Point(offset.X, offset.Y));
        }
        finally
        {
            context.Restore();
        }
    }

    /// <summary>画像・注釈・選択枠を描く。</summary>
    /// <param name="context">描画先。</param>
    /// <param name="includeSelection">選択枠としっぽのハンドルを描く場合は <see langword="true"/>。</param>
    /// <param name="scale">表示倍率。</param>
    /// <param name="offset">画像の左上（DIP）。</param>
    private void DrawAll(IGraphicsContext context, bool includeSelection, double scale, Point offset)
    {
        if (ShowImage && ResolveImage() is { } image)
        {
            context.DrawImage(
                image,
                new Rect(offset.X, offset.Y, SourcePixelWidth * scale, SourcePixelHeight * scale));
        }

        if (_session.Document.Items.Length > 0)
        {
            // **変換中の文字列はここでは扱わない**: 編集中の本文は本文コントロールが描く
            var plan = _presenter.ResolveDrawPlan(_session.Document, StyledText.RichTextPainter.Measure, _session.SelectedId);

            context.Save();
            try
            {
                context.Translate(offset.X, offset.Y);
                context.Scale(scale, scale);

                // **グリフの位置をデバイス ピクセルへ丸めない**: 丸めると描画の送りだけが変わることがある。
                // 測定と描画で同じ送りにするため（DPI の一致と併せて）、本文の描画では丸めを切る。
                context.TextPixelSnap = false;

                // **同じフレームでは同じレイアウト実体を使う**: 文字の描画とキャレット・選択範囲の
                // 位置計算でレイアウトを作り直すと、丸めやキャッシュの差で位置が食い違う。
                // 生成器を 1 つ渡す。
                var layouts = new StyledText.TextLayoutCache();
                foreach (var shape in plan.Shapes)
                {
                    AnnotationPainter.DrawShape(context, shape, layouts.Create);
                }


            }
            finally
            {
                context.Restore();
            }
        }

        if (!includeSelection)
        {
            return;
        }

        DrawSelection(context);

        // 本文の編集中はハンドルを出さない（動かせないため。 破線だけが「編集中の枠」を示す）
        if (!IsEditingText)
        {
            DrawTailHandle(context);
        }
    }

    /// <summary>選択枠（枠・8 方向のハンドル）を描く。</summary>
    /// <param name="context">描画先。</param>
    private void DrawSelection(IGraphicsContext context)
    {
        if (_session.SelectedId is null)
        {
            return;
        }

        var plan = _marquee.ResolveDrawPlan(
            new EsSize(RenderSize.Width, RenderSize.Height),
            AnnotationMarqueeMetrics.MeasureText,
            false);

        // **本文を編集中は破線だけを描く**: ハンドルは動かせないため出さない。 代わりに「どの注釈を
        // 編集中か」を破線で示す（規則は提示層の AnnotationEditingOutline が単一所有する）。
        // 編集中はマーキーの選択を解除している（枠を出さない）ため、矩形は文書と表示倍率から求める。
        if (IsEditingText && Selected is { } editingItem)
        {
            var (editScale, editOffset) = ResolveImageTransform();
            var outline = AnnotationEditingOutline.Resolve(
                AnnotationEditingOutline.ResolveSourceRect(editingItem), editScale, new EsPoint(editOffset.X, editOffset.Y));
            context.DrawRectangle(
                new Rect(outline.X, outline.Y, outline.Width, outline.Height),
                new Pen(
                    AnnotationPainter.ToColor(_marquee.Appearance.BorderColor),
                    AnnotationEditingOutline.Thickness,
                    new StrokeStyle { DashArray = [.. AnnotationEditingOutline.DashPattern] }));
            return;
        }

        if (plan.Appearance.ShowBorder)
        {
            var color = AnnotationPainter.ToColor(plan.Appearance.BorderColor);
            foreach (var rect in plan.BorderDipRects)
            {
                context.FillRectangle(new Rect(rect.X, rect.Y, rect.Width, rect.Height), color);
            }
        }

        if (plan.Handles is not { } handles)
        {
            return;
        }

        // ポインターの形を決めるのに使う（描いた値と同じ値を使い、判定と見た目をずらさない）
        var handleRects = new EsRect[MarqueeHandleRects.Count];
        for (var i = 0; i < handleRects.Length; i++)
        {
            handleRects[i] = handles.HandleDipRects[i];
        }

        _handleDipRects = handleRects;

        var fill = AnnotationPainter.ToColor(plan.Appearance.HandleColor);
        var border = AnnotationPainter.ToColor(plan.Appearance.HandleBorderColor);
        for (var i = 0; i < MarqueeHandleRects.Count; i++)
        {
            var rect = handles.HandleDipRects[i];
            var mewRect = new Rect(rect.X, rect.Y, rect.Width, rect.Height);
            context.FillRectangle(mewRect, fill);
            context.DrawRectangle(mewRect, border, handles.BorderThickness);
        }
    }

    /// <summary>しっぽのハンドル（円）を描く。</summary>
    /// <param name="context">描画先。</param>
    /// <remarks>
    /// <b>当たり判定と同じ半径で描く</b>（<see cref="TailHandleRadiusDip"/>）。 表示倍率の影響を受けないよう、
    /// 注釈の座標変換の外（DIP）で描く。
    /// </remarks>
    private void DrawTailHandle(IGraphicsContext context)
    {
        if (GetTailHandleCenterDip() is not { } center)
        {
            return;
        }

        var size = HandleSize;
        var fill = AnnotationPainter.ToColor(MarqueeAppearance.Default.HandleColor);
        var border = AnnotationPainter.ToColor(MarqueeAppearance.Default.HandleBorderColor);
        var rect = new Rect(center.X - (size / 2), center.Y - (size / 2), size, size);
        context.FillRectangle(rect, fill);
        context.DrawRectangle(rect, border, 1);
    }

    /// <summary>しっぽの先端のハンドルの中心（DIP）を求める。</summary>
    /// <returns>中心の座標。 吹き出しを選んでいない場合は <see langword="null"/>。</returns>
    internal Point? GetTailHandleCenterDip()
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return null;
        }

        var center = ToClientDip(bubble.Tail.Tip);
        return new Point(center.X, center.Y);
    }

    /// <summary>ポインターが押されたときの処理（左: 選択・移動・サイズ変更。 右: 編集パネル）。</summary>
    /// <param name="e">ポインターの引数。</param>
    /// <remarks>
    /// <b>注釈の無い場所も本レイヤーが受け取る</b>: 選択の解除と、プレビューへの操作の引き渡しを行う。
    /// 入力を要素の切り替えで振り分けると、切り替えた側が次の押下を受け取れない。
    /// </remarks>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        var position = e.GetPosition(this);
        _core.Log.LogPointerPressed(ToSource(position), 1);
        RaisePreviewGesture(AnnotationPreviewGesture.Press, position, 1, 0, e.ShiftKey, e.ControlKey);

        if (e.RightButton)
        {
            HandleRightButton(position);
            e.Handled = true;
            return;
        }

        if (!e.LeftButton)
        {
            return;
        }

        Focus();

        // **新しい押下は前の操作を必ず終わらせる**: 離した通知を取りこぼすと
        // `_isPreviewDragging` などが立ったまま残り、以後のドラッグがプレビューのパンとして
        // 扱われて**注釈（枠線・吹き出し）が動かなくなる**。
        _isPreviewDragging = false;
        _isDraggingTail = false;

        // 押下中の座標変換を固定する（ドラッグ中に倍率が変わってもカーソルと 1:1 で動かす）
        FreezeTransform();

        // 押下の間はポインターを捕まえる（MewUI は捕まえないと 2 回目の押下をダブルクリックとして通知しない。 解放は Up で行う）
        CapturePointer();

        // 押下から始まる操作（移動・サイズ変更・しっぽの移動）を 1 手として記録する。
        // 実際に文書が変わらなければ履歴へは積まれない（選択だけのクリックで手数が増えない）
        _session.BeginChange();

        var source = ToSource(position);

        // 本文の編集中は、本文の外側の押下で編集を終える（内側の押下は本文コントロールが受ける）
        if (IsEditingText)
        {
            if (Selected is BubbleAnnotation editingBubble
                && editingBubble.Bounds.Contains(source)
                && _externalTextId is not null)
            {
                _session.CancelChange();
                return;
            }

            EndTextEditing();
        }

        // **しっぽ → ハンドル → 本体の優先順は 1 か所で決める**（ポインターの形と同じ判定を使う）
        var kind = ResolvePointerKind(position);

        if (kind == AnnotationPointerKind.Tail)
        {
            CapturePointer();
            _isDraggingTail = true;
            InvalidateVisual();
            return;
        }

        if (AnnotationPointerPolicy.IsResize(kind))
        {
            CapturePointer();
            _marquee.Press(source, e.ClickCount);
            InvalidateVisual();
            return;
        }

        if (AnnotationHitTest.HitTest(_session.Document, source) is { } hit)
        {
            if (!string.Equals(_session.SelectedId, hit.Id, StringComparison.Ordinal))
            {
                _session.Select(hit.Id);
            }

            // **押下は常に移動として始める**: ダブルクリックの判定は専用のイベント
            // （<see cref="OnMouseDoubleClick"/>）が担う。 ここで回数を見ると、直前の押下（追加メニューや
            // 選択のクリック）と近い位置での押下が 2 回と数えられ、**最初のドラッグが本文の編集に化けて
            // 未選択からの移動ができない**。
            CapturePointer();
            _marquee.Press(source, 1);
        }
        else
        {
            _session.Select(null);

            // 注釈の無い場所はプレビューの操作（パン・ダブルクリック）へ渡す
            if (e.ClickCount >= 2)
            {
                RaisePreviewGesture(AnnotationPreviewGesture.DoubleClick, position, e.ClickCount, 0, e.ShiftKey, e.ControlKey);
                return;
            }

            // **押している間はポインターを捕まえる**: 捕まえないと離した通知が届かず、
            // `_isPreviewDragging` が立ったまま残る。 以後のドラッグがすべてプレビューのパンとして
            // 扱われ、**注釈（枠線・吹き出し）が動かなくなる**。
            CapturePointer();
            _isPreviewDragging = true;
            RaisePreviewGesture(AnnotationPreviewGesture.DragStart, position, 1, 0, e.ShiftKey, e.ControlKey);
        }

        InvalidateVisual();
    }

    /// <summary>ダブルクリックで本文の編集を始める。</summary>
    /// <param name="e">マウスの引数。</param>
    /// <remarks>
    /// <b>ダブルクリックは専用のイベントで受ける</b>: MewUI は押下の <c>ClickCount</c> を 1 のまま返し、
    /// 2 回目の押下を本イベントとして通知する。 <c>ClickCount</c> だけを見ると
    /// 本文の編集を始められない。
    /// </remarks>
    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);

        var position = e.GetPosition(this);
        var source = ToSource(position);
        if (!IsEditingText && AnnotationHitTest.HitTest(_session.Document, source) is BubbleAnnotation)
        {
            CapturePointer();
            BeginTextEditing();
            e.Handled = true;
        }
    }
    /// <summary>ポインターが動いたときの処理（ドラッグとカーソル形状）。</summary>
    /// <param name="e">ポインターの引数。</param>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var position = e.GetPosition(this);
        var source = ToSource(position);
        _lastPointerSource = source;

        // 注釈の無い場所を押したまま動かしたら、プレビューのパンへ渡す
        if (_isPreviewDragging)
        {
            // **離した後の移動ではパンを続けない**: 解放を取りこぼしたまま動き続けると、
            // 次のドラッグが注釈へ届かない
            if (!e.LeftButton)
            {
                _isPreviewDragging = false;
                ReleasePointer();
                RaisePreviewGesture(AnnotationPreviewGesture.DragEnd, position, 1, 0, e.ShiftKey, e.ControlKey);
                return;
            }

            RaisePreviewGesture(AnnotationPreviewGesture.DragMove, position, 1, 0, e.ShiftKey, e.ControlKey);
            return;
        }


        if (_isDraggingTail)
        {
            if (!e.LeftButton)
            {
                _isDraggingTail = false;
                _session.CommitChange();
                InvalidateVisual();
                return;
            }

            if (Selected is BubbleAnnotation)
            {
                _session.SetTailTip(source, TailGrabMarginSource());
                InvalidateVisual();
            }

            return;
        }

        // **押していない移動では注釈を動かさない**: 解放の通知を取りこぼすと（ポインターの捕獲が
        // ウィンドウへ移った場合など）マーキーがドラッグ中のまま残り、ホバーだけで枠線や注釈が
        // 動き続ける。 押下中だけドラッグを適用し、押していないときは解除する。
        if (!e.LeftButton)
        {
            if (_marquee.CreateSnapshot(source).State is MarqueeInteractionState.Dragging
                or MarqueeInteractionState.DraggingNudging)
            {
                _marquee.Release(source);
            }
        }
        else
        {
            _marquee.Move(source);
        }

        // 注釈の上に無い間は、プレビューのホバー表示（座標・自動切り出しの強調）へ位置を渡す
        if (!HitTestAt(position))
        {
            RaisePreviewGesture(AnnotationPreviewGesture.Hover, position, 1, 0, e.ShiftKey, e.ControlKey);
        }

        // **ポインターの形で「何ができるか」を示す**（規則はコアの AnnotationPointerPolicy が単一所有する）。
        // 押下中は Marquee が操作に合った形を設定するため、ホバーのときだけ決める。
        if (!e.LeftButton)
        {
            ApplyHoverCursor(position);
        }

        InvalidateVisual();
    }

    /// <summary>ホバー中のポインターの形を決めて適用する。</summary>
    /// <param name="position">位置（ローカル DIP）。</param>
    private void ApplyHoverCursor(Point position)
    {
        var kind = ResolvePointerKind(position);

        Cursor = kind switch
        {
            AnnotationPointerKind.Move => CursorType.SizeAll,
            AnnotationPointerKind.ResizeTop or AnnotationPointerKind.ResizeBottom => CursorType.SizeNS,
            AnnotationPointerKind.ResizeLeft or AnnotationPointerKind.ResizeRight => CursorType.SizeWE,
            AnnotationPointerKind.ResizeTopLeft or AnnotationPointerKind.ResizeBottomRight => CursorType.SizeNWSE,
            AnnotationPointerKind.ResizeTopRight or AnnotationPointerKind.ResizeBottomLeft => CursorType.SizeNESW,
            AnnotationPointerKind.Tail => CursorType.Hand,
            _ => CursorType.Arrow,
        };
    }

    /// <summary>ポインターが離されたときの処理。</summary>
    /// <param name="e">ポインターの引数。</param>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        // 押下中に固定した変換を解除する（次の押下で最新の表示矩形から取り直す）
        ReleaseTransform();
        _core.Log.LogPointerReleased(ToSource(e.GetPosition(this)), _session.SelectedId is not null);


        if (_isDraggingTail)
        {
            _isDraggingTail = false;
            _session.CommitChange();
            InvalidateVisual();
            return;
        }

        if (_isPreviewDragging)
        {
            _isPreviewDragging = false;
            RaisePreviewGesture(AnnotationPreviewGesture.DragEnd, e.GetPosition(this), 1, 0, e.ShiftKey, e.ControlKey);
            ReleasePointer();
            return;
        }

        _marquee.Release(ToSource(e.GetPosition(this)));
        ReleasePointer();
        InvalidateVisual();
    }

    /// <summary>ホイールが回されたときの処理（ズーム・スクロールはプレビューへ渡す）。</summary>
    /// <param name="e">ホイールの引数。</param>
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        RaisePreviewGesture(
            AnnotationPreviewGesture.Wheel, e.GetPosition(this), 1, (int)e.Delta.Y, e.ShiftKey, e.ControlKey);
        e.Handled = true;
    }

    /// <summary>ポインターが領域から外れたときの処理（プレビューのホバー表示を消す）。</summary>
    protected override void OnMouseLeave()
    {
        base.OnMouseLeave();

        // 外へ出たら既定の形へ戻す（戻さないと移動の合図が残る）
        Cursor = CursorType.Arrow;

        if (!_isPreviewDragging && !IsEditingText)
        {
            RaisePreviewGesture(AnnotationPreviewGesture.Leave, default, 1, 0, false, false);
        }
    }

    /// <summary>この位置が入力の対象かどうかを判定する。</summary>
    /// <param name="point">位置（ウィンドウの座標）。</param>
    /// <returns>このレイヤー（注釈レイヤーがプレビュー領域の入力を単一所有する）。</returns>
    /// <remarks>
    /// <b>下地を描かない面でも入力を受ける</b>: 画像の上へ重ねるため背景を塗らないが、MewUI の
    /// パネルは背景の無い面をヒットテストの対象にしない。 常に対象とする。<br/>
    /// <b><paramref name="point"/> は要素の座標ではなくウィンドウの座標である</b>: MewUI は
    /// 子へ座標を変換せずに渡すため、判定は <c>Bounds</c> で行う
    /// （<c>RenderSize</c> で判定すると、左上から 1 画面分を超えた位置で
    /// 入力が届かなくなる）。
    /// </remarks>
    protected override UIElement OnHitTest(Point point)
        => IsVisible && IsHitTestVisible && Bounds.Contains(point) ? this : null!;

    /// <summary>キーが押されたときの処理（本文の編集・削除・nudge）。</summary>
    /// <param name="e">キーの引数。</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // 本文の編集中は、キー入力を本文コントロールが受ける（Delete で注釈を消さない）
        if (IsEditingText)
        {
            return;
        }

        if (e.ControlKey && e.Key is Key.Z or Key.Y)
        {
            e.Handled = e.Key == Key.Z ? Undo() : Redo();
            return;
        }

        if (e.Key == Key.Delete)
        {
            e.Handled = DeleteSelected();
            return;
        }

        if (e.Key == Key.Enter && Selected is BubbleAnnotation)
        {
            BeginTextEditing();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            _session.BeginChange();
            e.Handled = _marquee.HandleKeyDown(ToEsKey(e.Key), _lastPointerSource);
            InvalidateVisual();
        }
    }

    /// <summary>フォーカスを失ったときの処理。</summary>
    /// <remarks>
    /// <b>編集は終えない</b>: 書式バーはポップアップへ出すため、開くとフォーカスが一時的に移る。
    /// ここで編集を終えると書式バーが出た瞬間に編集が終わってしまう。
    /// 編集を終えるのは、外側のクリック・Esc・Ctrl+Enter のいずれかである。
    /// </remarks>
    protected override void OnLostFocus() => base.OnLostFocus();

    /// <summary>右クリックを処理する（注釈の上なら編集パネルを出す。 注釈の外なら追加メニューを要求する）。</summary>
    /// <param name="position">位置（DIP）。</param>
    private void HandleRightButton(Point position)
    {
        var source = ToSource(position);

        // 外観フライアウトは注釈（枠線・吹き出し）の上を右クリックしたときだけ出す。
        // 注釈の外は「枠線・吹き出しを追加」のメニューにする（選択も解除する）。
        // 追加できるのは画像の内側だけである（規則はコアが単一所有する）
        if (AnnotationHitTest.HitTest(_session.Document, source) is not { } hit)
        {
            _session.Select(null);
            if (AnnotationGeometry.Contains(source, _session.Document.ImageSize))
            {
                RaiseAddMenuRequested(position.X, position.Y);
            }

            return;
        }

        if (!string.Equals(_session.SelectedId, hit.Id, StringComparison.Ordinal))
        {
            _session.Select(hit.Id);
        }

        _styleEditorX = position.X;
        _styleEditorY = position.Y;

        Focus();
        ShowStyleEditor();
    }

    /// <summary>プレビューへ操作を渡す。</summary>
    /// <param name="gesture">操作の種類。</param>
    /// <param name="position">位置（ローカル DIP）。</param>
    /// <param name="clickCount">クリック回数。</param>
    /// <param name="wheelDelta">ホイールの移動量。</param>
    /// <param name="shift">Shift キーが押されているかどうか。</param>
    /// <param name="control">Ctrl キーが押されているかどうか。</param>
    private void RaisePreviewGesture(
        AnnotationPreviewGesture gesture,
        Point position,
        int clickCount,
        int wheelDelta,
        bool shift,
        bool control)
        => PreviewGestureRequested?.Invoke(
            this,
            new AnnotationPreviewGestureEventArgs(
                gesture, position.X, position.Y, clickCount, wheelDelta, shift, control));

    /// <summary>位置（DIP）で示される操作の種類を求める（しっぽ → ハンドル → 本体の優先順）。</summary>
    /// <param name="position">位置（ローカル DIP）。</param>
    /// <returns>操作の種類。 注釈の上でない場合は <see cref="AnnotationPointerKind.None"/>。</returns>
    private AnnotationPointerKind ResolvePointerKind(Point position)
    {
        var transform = _frozenTransform ?? ResolveImageTransform();
        return AnnotationPointerPolicy.Resolve(
            _session.Document,
            _session.SelectedId,
            transform.ToSource(new EsPoint(position.X, position.Y)),
            new EsPoint(position.X, position.Y),
            _handleDipRects,
            transform.ToSourceRadius(TailHandleRadiusDip));
    }

    /// <summary>しっぽの先端を掴む距離を元画像ピクセルで求める（画面の DIP で一定の大きさを保つ）。</summary>
    /// <returns>掴む距離（元画像ピクセル）。</returns>
    /// <remarks>端の余白はドラッグの目標にだけ使い、保存する値へ UI の都合を持ち込まない。</remarks>
    private double TailGrabMarginSource()
        => (_frozenTransform ?? ResolveImageTransform()).ToSourceRadius(TailHandleRadiusDip);

    /// <summary>Marquee からの通知を文書へ反映する（移動・サイズ変更）。</summary>
    /// <param name="notice">通知。</param>
    private void OnMarqueeNotice(MarqueeNotice notice)
    {
        if (_isApplyingMarquee || _session.SelectedId is null)
        {
            return;
        }

        if (notice.Kind is not (MarqueeNoticeKind.SelectionChanged or MarqueeNoticeKind.SelectionCompleted))
        {
            return;
        }

        var rect = notice.Snapshot.SelectionSourceRect;
        if (rect.IsEmpty)
        {
            return;
        }

        _isApplyingMarquee = true;
        try
        {
            // 画像の外へは出さない（収める規則はコアが単一所有する）
            _session.SetBounds(rect);
        }
        finally
        {
            _isApplyingMarquee = false;
        }

        InvalidateVisual();
    }

    /// <summary>ポインターのキャプチャを取得する（コントロールの外へ出ても移動を受け取る）。</summary>
    private void CapturePointer()
    {
        if (FindVisualRoot() is Window window)
        {
            window.CaptureMouse(this);
        }
    }

    /// <summary>ポインターのキャプチャを解放する。</summary>
    private void ReleasePointer()
    {
        if (FindVisualRoot() is Window window)
        {
            window.ReleaseMouseCapture();
        }
    }

    /// <summary>MewUI のキーをコアのキーへ写す（nudge に使う 4 方向だけ）。</summary>
    /// <param name="key">MewUI のキー。</param>
    /// <returns>コアのキー。 対象外は <see cref="EsKey.None"/>。</returns>
    private static EsKey ToEsKey(Key key) => key switch
    {
        Key.Left => EsKey.Left,
        Key.Right => EsKey.Right,
        Key.Up => EsKey.Up,
        Key.Down => EsKey.Down,
        _ => EsKey.None,
    };

    /// <summary>Marquee のコアが要求する効果を MewUI のコントロールへ写すアダプタ。</summary>
    /// <remarks>
    /// <b>効果だけを実装する</b>: 選択の判断・ハンドルの当たり判定・nudge は Marquee のコアが単一所有する
    /// （自前で書き直すと 4 つの UI 版で挙動がずれる）。
    /// </remarks>
    private sealed class MarqueeEffects(AnnotationLayerControl owner) : MarqueeEffectsBase
    {
        /// <inheritdoc/>
        public override void RequestFocus() => owner.Focus();

        /// <inheritdoc/>
        public override void RequestRedraw() => owner.InvalidateVisual();

        /// <inheritdoc/>
        public override void SetCursorShape(MarqueeCursorKind kind)
        {
            owner.Cursor = kind switch
            {
                MarqueeCursorKind.DiagonalNWSE => CursorType.SizeNWSE,
                MarqueeCursorKind.DiagonalNESW => CursorType.SizeNESW,
                MarqueeCursorKind.Vertical => CursorType.SizeNS,
                MarqueeCursorKind.Horizontal => CursorType.SizeWE,
                MarqueeCursorKind.Move => CursorType.SizeAll,
                _ => null,
            };
        }

        /// <inheritdoc/>
        public override void SetPointerCapture(bool capture)
        {
            if (capture)
            {
                owner.CapturePointer();
            }
            else
            {
                owner.ReleasePointer();
            }
        }

        /// <inheritdoc/>
        public override bool? TryMoveCursor(EsPointInt screenPoint)
            => owner._cursor.TrySetCursorScreenPoint(screenPoint);

        /// <inheritdoc/>
        /// <remarks>
        /// <b>ポインタは隠さない</b>: 隠すと入力位置を見失い、他のウィンドウへフォーカスするまで戻らない
        /// （Win32 の <c>ShowCursor</c> はカウンタ式のため、対応が崩れると復元できない）。 nudge 中も
        /// 表示したままにする。
        /// </remarks>
        public override void ShowCursor(bool show) => _ = show;

        /// <inheritdoc/>
        public override EsPointInt ToScreenPoint(EsPoint sourcePoint)
        {
            if (owner.FindVisualRoot() is not Window window)
            {
                return default;
            }

            var localDip = owner.ToClientDip(sourcePoint);
            var screen = window.ClientToScreen(new Point(localDip.X, localDip.Y));
            return new EsPointInt((int)screen.X, (int)screen.Y);
        }
    }
}

