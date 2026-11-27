using System.Drawing.Drawing2D;
using EsUtil.Feature.Marquee;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Input;
using EsUtil.Feature.Annotation.Core.Contract;
using EsUtil.Feature.Annotation.Core.Geometry;
using EsUtil.Feature.Annotation.Core.Interaction;
using EsUtil.Feature.Annotation.Presenter.Drawing;
using EsUtil.Feature.Annotation.Logging;

namespace EsUtil.Ui.WinForms.Annotation;

/// <summary>注釈レイヤーの描画と入力（WinForms 版）。</summary>
/// <remarks>
/// <b>描画の順序</b>: 画像 → 注釈 → 選択枠 → しっぽのハンドル（画像と注釈は元画像ピクセル、選択枠は DIP）。<br/>
/// <b>入力は 1 本の経路</b>: 押下 → しっぽの先端 → ハンドル → 注釈の本体（Marquee へ転送）→ 背景の順に判定する。
/// </remarks>
public sealed partial class AnnotationLayerControl
{
    /// <summary>ダブルクリックの判定（時間と距離の規則は共通基盤が単一所有する）。</summary>
    private readonly DoubleClickTracker _doubleClick = new();

    /// <summary>再描画と選択枠の更新を行う（画像・注釈・選択枠を今の状態へ合わせる）。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var (scale, offset) = ResolveImageTransform();
        if (ShowImage && _image is { } image)
        {
            graphics.DrawImage(
                image,
                new RectangleF(
                    (float)offset.X,
                    (float)offset.Y,
                    (float)(SourcePixelWidth * scale),
                    (float)(SourcePixelHeight * scale)));
        }

        var plan = _presenter.ResolveDrawPlan(_session.Document, StyledText.RichTextPainter.Measure, _session.SelectedId);
        AnnotationPainter.Draw(graphics, plan, scale, offset.X, offset.Y, ExternalTextId);

        DrawSelection(graphics);
    }

    /// <summary>選択枠としっぽのハンドルを描く。</summary>
    /// <param name="graphics">描画先（DIP）。</param>
    private void DrawSelection(Graphics graphics)
    {
        if (_session.SelectedId is null)
        {
            return;
        }

        var plan = _marquee.ResolveDrawPlan(
            new EsSize(ClientSize.Width, ClientSize.Height),
            AnnotationMarqueeMetrics.MeasureText,
            false);

        // **本文を編集中は破線だけを描く**: ハンドルは動かせないため出さない。 代わりに「どの注釈を
        // 編集中か」を破線で示す（規則は提示層の AnnotationEditingOutline が単一所有する）。
        // 編集中はマーキーの選択を解除している（枠を出さない）ため、矩形は文書と表示倍率から求める。
        if (IsEditing && Selected is { } editingItem)
        {
            var (editScale, editOffset) = ResolveImageTransform();
            DrawEditingOutline(
                graphics,
                AnnotationEditingOutline.Resolve(
                    AnnotationEditingOutline.ResolveSourceRect(editingItem), editScale, editOffset),
                _marquee.Appearance.BorderColor);
            return;
        }

        List<EsRect>? handles = null;
        if (plan.Handles is { } source)
        {
            handles = new List<EsRect>(8);
            for (var i = 0; i < 8; i++)
            {
                handles.Add(source.HandleDipRects[i]);
            }
        }

        // ポインターの形を決めるのに使う（描いた値と同じ値を使い、判定と見た目をずらさない）
        _handleDipRects = handles;

        AnnotationPainter.DrawSelection(
            graphics,
            plan.BorderDipRects,
            plan.Appearance.BorderColor,
            handles,
            plan.Appearance.HandleColor,
            plan.Appearance.HandleBorderColor);

        if (GetTailHandleCenterDip() is not { } center)
        {
            return;
        }

        AnnotationPainter.DrawTailHandle(
            graphics,
            new PointF((float)center.X, (float)center.Y),
            HandleSize,
            plan.Appearance.HandleColor,
            plan.Appearance.HandleBorderColor);
    }

    /// <summary>本文を編集中の注釈を破線で囲む。</summary>
    /// <param name="graphics">描画先（DIP）。</param>
    /// <param name="rect">破線を描く矩形（DIP）。</param>
    /// <param name="color">線の色。</param>
    /// <remarks>
    /// <b>見た目はコアが単一所有する</b>（<see cref="AnnotationEditingOutline"/>）: 移動できない状態で
    /// ハンドルを出すと「掴めるのに動かない枠」に見えるため、破線で「いま対象の枠」だけを示す。
    /// </remarks>
    private static void DrawEditingOutline(Graphics graphics, EsRect rect, Color color)
    {
        using var pen = new Pen(color, (float)AnnotationEditingOutline.Thickness)
        {
            DashPattern = [.. AnnotationEditingOutline.DashPattern.Select(static value => (float)value)],
            DashStyle = System.Drawing.Drawing2D.DashStyle.Custom,
        };
        graphics.DrawRectangle(
            pen, (float)rect.X, (float)rect.Y, (float)rect.Width, (float)rect.Height);
    }

    /// <summary>しっぽの先端のハンドルの中心（DIP）を求める。</summary>
    /// <returns>中心の座標。 吹き出しを選んでいない場合は <see langword="null"/>。</returns>
    private EsPoint? GetTailHandleCenterDip()
    {
        if (Selected is not BubbleAnnotation bubble)
        {
            return null;
        }

        return ToClientDip(bubble.Tail.Tip);
    }

    /// <summary>マウスが押されたときの処理。</summary>
    /// <param name="e">マウスの引数。</param>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseDown(e);

        var position = new EsPoint(e.X, e.Y);
        _core.Log.LogPointerPressed(ToSource(position), 1);

        // 押下をホストへ知らせる（表示中のメニューを外側の押下で閉じるために使う）
        RaisePreviewGesture(AnnotationPreviewGesture.Press, position, 1, 0);

        if (e.Button == MouseButtons.Right)
        {
            HandleRightButton(position);
            return;
        }

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        Focus();

        // 押下中の座標変換を固定する（ドラッグ中に倍率が変わってもカーソルと 1:1 で動かす）
        FreezeTransform();
        _session.BeginChange();

        var source = ToSource(position);
        var isDoubleClick = _doubleClick.Press(position, DateTimeOffset.UtcNow);
        _lastPointerSource = source;

        // 本文の編集中は、本文の外側の押下で編集を終える（内側の押下は本文コントロールが受ける）
        if (IsEditing)
        {
            if (Selected is BubbleAnnotation editingBubble
                && editingBubble.Bounds.Contains(source)
                && _externalTextId is not null)
            {
                _session.CancelChange();
                return;
            }

            EndEdit();
        }

        // **しっぽ → ハンドル → 本体の優先順は 1 か所で決める**（ポインターの形と同じ判定を使う）
        var kind = ResolvePointerKind(position);

        if (kind == AnnotationPointerKind.Tail)
        {
            Capture = true;
            _isDraggingTail = true;
            return;
        }

        if (AnnotationPointerPolicy.IsResize(kind))
        {
            Capture = true;
            _marquee.Press(source, 1);
            return;
        }

        if (AnnotationHitTest.HitTest(_session.Document, source) is not { } hit)
        {
            _session.SelectItem(null);

            // 注釈の無い場所はプレビューの操作（パン・ダブルクリック）へ渡す
            if (isDoubleClick)
            {
                RaisePreviewGesture(AnnotationPreviewGesture.DoubleClick, position, 2, 0);
                return;
            }

            _isPreviewDragging = true;
            RaisePreviewGesture(AnnotationPreviewGesture.DragStart, position, 1, 0);
            return;
        }

        _session.SelectItem(hit.Id);
        if (isDoubleClick && hit is BubbleAnnotation)
        {
            Capture = true;
            BeginEdit();
            return;
        }

        Capture = true;
        _marquee.Press(source, 1);
    }

    /// <summary>マウスが動いたときの処理。</summary>
    /// <param name="e">マウスの引数。</param>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseMove(e);

        var position = new EsPoint(e.X, e.Y);
        var source = ToSource(position);
        _lastPointerSource = source;

        // **ボタンを押していない移動では何も動かさない**: Up の取りこぼしで「押していないのに
        // ドラッグ中」の状態が残ると、ホバーだけで枠線や注釈が動き続ける。 押下中だけ動かす（4 つの UI 版で同じ規則）。
        var isPressed = (e.Button & MouseButtons.Left) != 0;

        // 注釈の無い場所を押したまま動かしたら、プレビューのパンへ渡す
        if (_isPreviewDragging)
        {
            RaisePreviewGesture(AnnotationPreviewGesture.DragMove, position, 1, 0);
            return;
        }

        if (_isDraggingTail)
        {
            if (!isPressed)
            {
                _isDraggingTail = false;
                _session.CommitChange();
                Invalidate();
                return;
            }

            if (Selected is BubbleAnnotation)
            {
                _session.SetTailTip(source, TailGrabMarginSource());
                Invalidate();
            }

            return;
        }

        if (isPressed)
        {
            _marquee.Move(source);
        }
        else if (_marquee.CreateSnapshot(source).State is MarqueeInteractionState.Dragging
            or MarqueeInteractionState.DraggingNudging)
        {
            _marquee.Release(source);
        }

        // 注釈の上に無い間は、プレビューのホバー表示（座標・自動切り出しの強調）へ位置を渡す
        if (!IsOverAnnotation(new Point(e.X, e.Y)))
        {
            RaisePreviewGesture(AnnotationPreviewGesture.Hover, position, 1, 0);
        }

        // **ポインターの形で「何ができるか」を示す**（規則はコアの AnnotationPointerPolicy が単一所有する）
        if (!isPressed)
        {
            ApplyHoverCursor(position);
        }

        Invalidate();
    }

    /// <summary>ホバー中のポインターの形を決めて適用する。</summary>
    /// <param name="position">位置（ローカル DIP）。</param>
    private void ApplyHoverCursor(EsPoint position)
    {
        var kind = ResolvePointerKind(position);

        Cursor = kind switch
        {
            AnnotationPointerKind.Move => Cursors.SizeAll,
            AnnotationPointerKind.ResizeTop or AnnotationPointerKind.ResizeBottom => Cursors.SizeNS,
            AnnotationPointerKind.ResizeLeft or AnnotationPointerKind.ResizeRight => Cursors.SizeWE,
            AnnotationPointerKind.ResizeTopLeft or AnnotationPointerKind.ResizeBottomRight => Cursors.SizeNWSE,
            AnnotationPointerKind.ResizeTopRight or AnnotationPointerKind.ResizeBottomLeft => Cursors.SizeNESW,
            AnnotationPointerKind.Tail => Cursors.Hand,
            _ => Cursors.Default,
        };
    }

    /// <summary>マウスが離されたときの処理。</summary>
    /// <param name="e">マウスの引数。</param>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseUp(e);

        // 押下中に固定した変換を解除する
        ReleaseTransform();

        var source = ToSource(new EsPoint(e.X, e.Y));
        _core.Log.LogPointerReleased(source, _session.SelectedId is not null);
        if (_isPreviewDragging)
        {
            _isPreviewDragging = false;
            RaisePreviewGesture(AnnotationPreviewGesture.DragEnd, new EsPoint(e.X, e.Y), 1, 0);
        }
        else if (_isDraggingTail)
        {
            _isDraggingTail = false;
            _session.CommitChange();
            Invalidate();
        }
        else
        {
            _marquee.Release(source);
        }

        Capture = false;
        Invalidate();
    }

    /// <summary>キーが押されたときの処理（本文の編集・削除・nudge）。</summary>
    /// <param name="e">キーの引数。</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnKeyDown(e);

        // 本文の編集中は、キー入力を本文コントロールが受ける（Delete で注釈を消さない）
        if (IsEditing)
        {
            return;
        }

        if (e.Control && e.KeyCode == Keys.Z)
        {
            e.Handled = Undo();
            return;
        }

        if (e.Control && e.KeyCode == Keys.Y)
        {
            e.Handled = Redo();
            return;
        }

        if (e.KeyCode == Keys.Delete)
        {
            e.Handled = DeleteSelected();
            return;
        }

        if (e.KeyCode == Keys.Enter && Selected is BubbleAnnotation)
        {
            BeginEdit();
            e.Handled = true;
            return;
        }

        if (e.KeyCode is Keys.Left or Keys.Right or Keys.Up or Keys.Down)
        {
            _session.BeginChange();
            e.Handled = _marquee.HandleKeyDown(ToEsKey(e.KeyCode), _lastPointerSource);
            Invalidate();
        }
    }


    /// <summary>キーをコアのキーへ写す（nudge に使う 4 方向だけ）。</summary>
    /// <param name="key">WinForms のキー。</param>
    /// <returns>コアのキー。 対象外は <see cref="EsKey.None"/>。</returns>
    private static EsKey ToEsKey(Keys key) => key switch
    {
        Keys.Left => EsKey.Left,
        Keys.Right => EsKey.Right,
        Keys.Up => EsKey.Up,
        Keys.Down => EsKey.Down,
        _ => EsKey.None,
    };

    /// <summary>右クリックを処理する（注釈の上なら編集パネルを出す。 注釈の外なら追加メニューを要求する）。</summary>
    /// <param name="position">位置（DIP）。</param>
    private void HandleRightButton(EsPoint position)
    {
        var source = ToSource(position);

        // 外観フライアウトは注釈（枠線・吹き出し）の上を右クリックしたときだけ出す。
        // 注釈の外は「枠線・吹き出しを追加」のメニューにする（選択も解除する）。
        // 追加できるのは画像の内側だけである（規則はコアが単一所有する）
        if (AnnotationHitTest.HitTest(_session.Document, source) is not { } hit)
        {
            _session.SelectItem(null);
            if (AnnotationGeometry.Contains(source, _session.Document.ImageSize))
            {
                RaiseAddMenuRequested(position.X, position.Y);
            }

            return;
        }

        _session.SelectItem(hit.Id);
        _styleEditorX = position.X;
        _styleEditorY = position.Y;

        ShowStyleEditor();
    }

    /// <summary>プレビューへ操作を渡す。</summary>
    /// <param name="gesture">操作の種類。</param>
    /// <param name="position">位置（ローカル DIP）。</param>
    /// <param name="clickCount">クリック回数。</param>
    /// <param name="wheelDelta">ホイールの移動量。</param>
    private void RaisePreviewGesture(
        AnnotationPreviewGesture gesture,
        EsPoint position,
        int clickCount,
        int wheelDelta)
        => PreviewGestureRequested?.Invoke(
            this,
            new AnnotationPreviewGestureEventArgs(
                gesture,
                position.X,
                position.Y,
                clickCount,
                wheelDelta,
                (ModifierKeys & Keys.Shift) == Keys.Shift,
                (ModifierKeys & Keys.Control) == Keys.Control));

    /// <summary>ホイールが回されたときの処理（ズーム・スクロールはプレビューへ渡す）。</summary>
    /// <param name="e">マウスの引数。</param>
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseWheel(e);

        RaisePreviewGesture(
            AnnotationPreviewGesture.Wheel, new EsPoint(e.X, e.Y), 1, e.Delta);
    }

    /// <summary>マウスが領域から外れたときの処理（プレビューのホバー表示を消す）。</summary>
    /// <param name="e">マウスの引数。</param>
    protected override void OnMouseLeave(EventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseLeave(e);

        // 外へ出たら既定の形へ戻す（戻さないと移動の合図が残る）
        Cursor = Cursors.Default;

        if (!_isPreviewDragging && !IsEditing)
        {
            RaisePreviewGesture(AnnotationPreviewGesture.Leave, default, 1, 0);
        }
    }

    /// <summary>位置（DIP）で示される操作の種類を求める（しっぽ → ハンドル → 本体の優先順）。</summary>
    /// <param name="position">位置（ローカル DIP）。</param>
    /// <returns>操作の種類。 注釈の上でない場合は <see cref="AnnotationPointerKind.None"/>。</returns>
    private AnnotationPointerKind ResolvePointerKind(EsPoint position)
    {
        var transform = _frozenTransform ?? ResolveImageTransform();
        return AnnotationPointerPolicy.Resolve(
            _session.Document,
            _session.SelectedId,
            transform.ToSource(position),
            position,
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

        Invalidate();
    }
}

