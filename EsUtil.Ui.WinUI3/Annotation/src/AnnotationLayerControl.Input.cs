using EsUtil.Feature.Marquee;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.System;
using EsUtil.Feature.Annotation.Core.Contract;
using EsUtil.Feature.Annotation.Core.Defaults;
using EsUtil.Feature.Annotation.Core.Geometry;
using EsUtil.Feature.Annotation.Core.Interaction;
using EsUtil.Feature.Annotation.Presenter.Drawing;
using EsUtil.Feature.Annotation.Logging;

namespace EsUtil.Ui.WinUI3.Annotation;

/// <summary>注釈レイヤーの描画と入力（WinUI 3 版）。</summary>
/// <remarks>
/// <b>描画の順序</b>: 画像 → 注釈 → 選択枠 → しっぽのハンドル（画像と注釈は元画像ピクセル、選択枠は DIP）。<br/>
/// <b>入力は 1 本の経路</b>: 押下 → しっぽの先端 → ハンドル → 注釈の本体（Marquee へ転送）→ 背景の順に判定する
/// （判断はコアと Marquee の提示層が単一所有する）。
/// </remarks>
public sealed partial class AnnotationLayerControl
{
    /// <summary>ポインター キャプチャ中のポインター（解放に使う）。</summary>
    private Microsoft.UI.Xaml.Input.Pointer? _capturedPointer;

    /// <summary>注釈の無い場所でドラッグしている間かどうか（プレビューのパンへ渡す）。</summary>
    private bool _isPreviewDragging;

    /// <summary>最後に押下した時刻（ダブルクリックの判定）。</summary>
    private DateTimeOffset _lastPressedAt = DateTimeOffset.MinValue;

    /// <summary>最後に押下した位置（ダブルクリックの判定）。</summary>
    private EsPoint _lastPressedPoint;

    /// <summary>描画に使う文書を取得する（変換中の文字列を入れた複製を含む）。</summary>
    /// <value>変換中でなければ本文の文書。 変換中は変換中の文字列を入れた描画用の複製。</value>
    /// <remarks>
    /// <b>変換中の文字列も本文として組む</b>: キャレットの位置へ重ねて描くと既存の文字と重なり、
    /// 折り返しも動かないため読めません。 規則はコアの
    /// <see cref="AnnotationCompositionPreview"/> が単一所有します（4 つの UI 版で同じ）。
    /// </remarks>
    private AnnotationDocument RenderDocument
        => IsEditingText
            ? AnnotationCompositionPreview.Apply(_session.Document, _session.SelectedId, _session.CaretIndex, _preeditText)
            : _session.Document;

    /// <summary>再描画する（画像・注釈・選択枠を今の状態へ合わせる）。</summary>
    public void Redraw()
    {
        var (scale, offset) = ResolveImageTransform();

        ApplyImageSource();
        if (!ShowImage)
        {
            _image.Visibility = Visibility.Collapsed;
        }
        else if (_image.Source is not null && ActualWidth > 0 && ActualHeight > 0)
        {
            _image.Width = SourcePixelWidth * scale;
            _image.Height = SourcePixelHeight * scale;
            _image.Margin = new Thickness(offset.X, offset.Y, 0, 0);
            _image.Visibility = Visibility.Visible;
        }
        else
        {
            _image.Visibility = Visibility.Collapsed;
        }

        var plan = _presenter.ResolveDrawPlan(RenderDocument, MeasureText, _session.SelectedId);

        // **編集中も本文を描く**: WinUI 3 は本文を別の入力欄で表示しないため、編集中に本文を
        // 描かないと文字が消える。 キャレットと選択範囲は後から重ねる。
        AnnotationPainter.Draw(
            _shapes,
            plan,
            scale,
            offset.X,
            offset.Y,
            null,
            MeasureText);

        DrawEditing();

        DrawSelection();
        Clip = new RectangleGeometry
        {
            Rect = new Windows.Foundation.Rect(0, 0, Math.Max(0, ActualWidth), Math.Max(0, ActualHeight)),
        };

    }

    /// <summary>再描画を要求する。</summary>
    public void Invalidate() => Redraw();

    /// <summary>表示用の画像を作り直す（同じ画像なら作り直さない）。</summary>
    private void ApplyImageSource()
    {
        if (_imageBitmap is not null || Source is not { IsDisposed: false } source)
        {
            _image.Source = _imageBitmap;
            return;
        }

        var bitmap = new WriteableBitmap(source.Width, source.Height);
        var pixels = source.ReadOnlyPixels.ToArray();
        using (var stream = bitmap.PixelBuffer.AsStream())
        {
            stream.Write(pixels, 0, pixels.Length);
        }

        _imageBitmap = bitmap;
        _image.Source = bitmap;
    }

    /// <summary>Marquee の要求に従ってポインター キャプチャを取得・解放する。</summary>
    /// <param name="capture">取得する場合は <see langword="true"/>。</param>
    internal void ApplyPointerCapture(bool capture)
    {
        if (capture)
        {
            if (_capturedPointer is { } pointer)
            {
                CapturePointer(pointer);
            }

            return;
        }

        if (_capturedPointer is { } captured)
        {
            ReleasePointerCapture(captured);
        }
    }

    /// <summary>選択枠としっぽのハンドルを描く。</summary>
    private void DrawSelection()
    {
        if (_session.SelectedId is null)
        {
            AnnotationPainter.DrawSelection(
                _selection, [], AnnotationDefaults.FrameAppearance.Border, null, AnnotationDefaults.FrameAppearance.Border,
                AnnotationDefaults.FrameAppearance.Border);
            return;
        }

        var plan = _marquee.ResolveDrawPlan(
            new EsSize(Math.Max(0, ActualWidth), Math.Max(0, ActualHeight)),
            AnnotationMarqueeMetrics.MeasureText,
            false);

        // **本文を編集中は破線だけを描く**: ハンドルは動かせないため出さない。 代わりに「どの注釈を
        // 編集中か」を破線で示す（規則は提示層の AnnotationEditingOutline が単一所有する）。
        // 編集中はマーキーの選択を解除している（枠を出さない）ため、矩形は文書と表示倍率から求める。
        if (IsEditingText && Selected is { } editingItem)
        {
            // 前のフレームのハンドルを残さない（残すと「動かせる枠」に見える）
            _selection.Children.Clear();

            var (editScale, editOffset) = ResolveImageTransform();
            var outline = AnnotationEditingOutline.Resolve(
                AnnotationEditingOutline.ResolveSourceRect(editingItem), editScale, editOffset);
            var outlineRect = new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                Width = outline.Width,
                Height = outline.Height,
                Stroke = AnnotationTextMetrics.ToBrush(s_marqueeAppearance.BorderColor),
                StrokeThickness = AnnotationEditingOutline.Thickness,
                StrokeDashArray = [.. AnnotationEditingOutline.DashPattern],
            };
            Canvas.SetLeft(outlineRect, outline.X);
            Canvas.SetTop(outlineRect, outline.Y);
            _selection.Children.Add(outlineRect);
            return;
        }

        var handles = plan.Handles is { } value
            ? new List<EsRect>(8)
            : null;

        if (plan.Handles is { } source)
        {
            for (var i = 0; i < 8; i++)
            {
                handles!.Add(source.HandleDipRects[i]);
            }
        }

        // ポインターの形を決めるのに使う（描いた値と同じ値を使い、判定と見た目をずらさない）
        _handleDipRects = handles;

        AnnotationPainter.DrawSelection(
            _selection,
            plan.BorderDipRects,
            plan.Appearance.BorderColor,
            handles,
            plan.Appearance.HandleColor,
            plan.Appearance.HandleBorderColor);

        if (GetTailHandleCenterDip() is not { } center)
        {
            return;
        }

        var size = HandleSize;
        var handle = new Microsoft.UI.Xaml.Shapes.Rectangle
        {
            Width = size,
            Height = size,
            Fill = AnnotationTextMetrics.ToBrush(plan.Appearance.HandleColor),
            Stroke = AnnotationTextMetrics.ToBrush(plan.Appearance.HandleBorderColor),
            StrokeThickness = 1,
        };

        Canvas.SetLeft(handle, center.X - (size / 2));
        Canvas.SetTop(handle, center.Y - (size / 2));
        _selection.Children.Add(handle);
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

    /// <summary>ポインターが押されたときの処理。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">ポインターの引数。</param>
    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _ = sender;

        var point = e.GetCurrentPoint(this);
        var position = new EsPoint(point.Position.X, point.Position.Y);
        _core.Log.LogPointerPressed(ToSource(position), 1);

        // 押下をホストへ知らせる（表示中のメニューを外側の押下で閉じるために使う）
        RaisePreviewGesture(AnnotationPreviewGesture.Press, point.Position, 1, 0);

        if (point.Properties.IsRightButtonPressed)
        {
            HandleRightButton(position);
            e.Handled = true;
            return;
        }

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _ = Focus(FocusState.Pointer);

        // 押下中の座標変換を固定する（ドラッグ中に倍率が変わってもカーソルと 1:1 で動かす）
        FreezeTransform();

        // 押下から始まる操作（移動・サイズ変更・しっぽの移動）を 1 手として記録する
        _session.BeginChange();

        var source = ToSource(position);
        var now = DateTimeOffset.UtcNow;
        var isDoubleClick = now - _lastPressedAt < TimeSpan.FromMilliseconds(500)
            && Math.Abs(position.X - _lastPressedPoint.X) < 4
            && Math.Abs(position.Y - _lastPressedPoint.Y) < 4;
        _lastPressedAt = now;
        _lastPressedPoint = position;

        if (IsEditingText)
        {
            // 編集中は本文の内側を押したらキャレットを移す（外側を押したら編集を終える）。
            // 押したまま動かすと選択範囲を広げる（_isSelectingText）。 矢印キーだけでなく
            // マウスのドラッグでも選択できるようにする。
            if (Selected is BubbleAnnotation editingBubble && editingBubble.Bounds.Contains(source))
            {
                CapturePointerHere(e);
                _session.CancelChange();
                _isSelectingText = true;
                SetCaretFromPoint(source, IsDown(VirtualKey.Shift));
                Redraw();
                e.Handled = true;
                return;
            }

            EndTextEditing();
        }

        // **しっぽ → ハンドル → 本体の優先順は 1 か所で決める**（ポインターの形と同じ判定を使う）
        var kind = ResolvePointerKind(new Windows.Foundation.Point(position.X, position.Y));

        if (kind == AnnotationPointerKind.Tail)
        {
            CapturePointerHere(e);
            _isDraggingTail = true;
            e.Handled = true;
            return;
        }

        if (AnnotationPointerPolicy.IsResize(kind))
        {
            CapturePointerHere(e);
            _marquee.Press(source, 1);
            e.Handled = true;
            return;
        }

        if (AnnotationHitTest.HitTest(_session.Document, source) is not { } hit)
        {
            _session.Select(null);

            // 注釈の無い場所はプレビューの操作（パン・ダブルクリック）へ渡す
            if (isDoubleClick)
            {
                RaisePreviewGesture(AnnotationPreviewGesture.DoubleClick, point.Position, 2, 0);
                return;
            }

            _isPreviewDragging = true;
            RaisePreviewGesture(AnnotationPreviewGesture.DragStart, point.Position, 1, 0);
            e.Handled = true;
            return;
        }

        _session.Select(hit.Id);
        if (isDoubleClick && hit is BubbleAnnotation)
        {
            CapturePointerHere(e);
            BeginTextEditing();
            e.Handled = true;
            return;
        }

        CapturePointerHere(e);
        _marquee.Press(source, 1);
        e.Handled = true;
    }

    /// <summary>ポインターのキャプチャを取得する（注釈の操作を始めるときだけ呼ぶ）。</summary>
    /// <param name="e">押下の引数。</param>
    /// <remarks>
    /// <b>注釈の無い場所ではキャプチャしない</b>: プレビューのパンはキャプチャ不要であり、
    /// キャプチャが残るとカーソルがウィンドウへ掴まれたままになる。
    /// </remarks>
    private void CapturePointerHere(PointerRoutedEventArgs e)
    {
        _capturedPointer = e.Pointer;
        CapturePointer(e.Pointer);
    }

    /// <summary>ポインターが動いたときの処理。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">ポインターの引数。</param>
    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        _ = sender;

        var point = e.GetCurrentPoint(this);
        var source = ToSource(new EsPoint(point.Position.X, point.Position.Y));
        _lastPointerSource = source;

        // **ボタンを押していない移動では何も動かさない**: 捕捉の解放や Up の取りこぼしで
        // 「押していないのにドラッグ中」の状態が残ると、ホバーだけで枠線や注釈が動き続ける。
        // 押下中だけ動かす。
        var isPressed = point.Properties.IsLeftButtonPressed;

        // 本文を押したまま動かしている間は、選択範囲を広げる（マウスでの選択）
        if (_isSelectingText)
        {
            if (!isPressed)
            {
                _isSelectingText = false;
                ReleasePointerCapture(e.Pointer);
                Redraw();
                return;
            }

            SetCaretFromPoint(source, extend: true);
            Redraw();
            return;
        }

        if (_isDraggingTail)
        {
            if (!isPressed)
            {
                _isDraggingTail = false;
                _session.CommitChange();
                Redraw();
                return;
            }

            if (Selected is BubbleAnnotation)
            {
                _session.SetTailTip(source, TailGrabMarginSource());
                Redraw();
            }

            return;
        }

        // 注釈の無い場所を押したまま動かしたら、プレビューのパンへ渡す
        if (_isPreviewDragging)
        {
            RaisePreviewGesture(AnnotationPreviewGesture.DragMove, point.Position, 1, 0);
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
        if (!HitTestAt(point.Position))
        {
            RaisePreviewGesture(AnnotationPreviewGesture.Hover, point.Position, 1, 0);
        }

        // **ポインターの形で「何ができるか」を示す**（規則はコアの AnnotationPointerPolicy が単一所有する）
        if (!isPressed)
        {
            ApplyHoverCursor(point.Position);
        }

        Redraw();
    }

    /// <summary>ポインターが離されたときの処理。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">ポインターの引数。</param>
    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _ = sender;

        // 押下中に固定した変換を解除する
        ReleaseTransform();

        var point = e.GetCurrentPoint(this);
        var source = ToSource(new EsPoint(point.Position.X, point.Position.Y));
        _core.Log.LogPointerReleased(source, _session.SelectedId is not null);

        if (_isSelectingText)
        {
            _isSelectingText = false;
            Redraw();
        }
        else if (_isPreviewDragging)
        {
            _isPreviewDragging = false;
            RaisePreviewGesture(AnnotationPreviewGesture.DragEnd, point.Position, 1, 0);
        }
        else if (_isDraggingTail)
        {
            _isDraggingTail = false;
            _session.CommitChange();
            Redraw();
        }
        else
        {
            _marquee.Release(source);
        }

        if (_capturedPointer is { } captured)
        {
            ReleasePointerCapture(captured);
            _capturedPointer = null;
        }

        Redraw();
    }

    /// <summary>キーが押されたときの処理（本文の編集・削除・nudge）。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">キーの引数。</param>
    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        _ = sender;

        if (IsEditingText)
        {
            if (HandleTextEditingKey(e))
            {
                e.Handled = true;
            }

            return;
        }

        var control = IsDown(VirtualKey.Control);
        switch (e.Key)
        {
            case VirtualKey.Z when control:
                e.Handled = Undo();
                return;
            case VirtualKey.Y when control:
                e.Handled = Redo();
                return;
            case VirtualKey.Delete:
                e.Handled = DeleteSelected();
                return;
            case VirtualKey.Enter or VirtualKey.GamepadA when Selected is BubbleAnnotation:
                BeginTextEditing();
                e.Handled = true;
                return;
            case VirtualKey.Left:
            case VirtualKey.Right:
            case VirtualKey.Up:
            case VirtualKey.Down:
                _session.BeginChange();
                e.Handled = _marquee.KeyDown(ToEsKey(e.Key), _lastPointerSource);
                Redraw();
                return;
        }
    }

    /// <summary>修飾キーが押されているかどうかを判定する。</summary>
    /// <param name="key">修飾キー。</param>
    /// <returns>押されている場合は <see langword="true"/>。</returns>
    private static bool IsDown(VirtualKey key)
    {
        var state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key);
        return (state & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
    }

    /// <summary>キーをコアのキーへ写す（nudge に使う 4 方向だけ）。</summary>
    /// <param name="key">WinUI 3 のキー。</param>
    /// <returns>コアのキー。 対象外は <see cref="EsKey.None"/>。</returns>
    private static EsKey ToEsKey(VirtualKey key) => key switch
    {
        VirtualKey.Left => EsKey.Left,
        VirtualKey.Right => EsKey.Right,
        VirtualKey.Up => EsKey.Up,
        VirtualKey.Down => EsKey.Down,
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
            _session.Select(null);
            if (AnnotationGeometry.Contains(source, _session.Document.ImageSize))
            {
                AddMenuRequested?.Invoke(this, new AnnotationAddMenuRequestedEventArgs(position.X, position.Y));
            }

            return;
        }

        _session.Select(hit.Id);
        _styleEditorX = position.X;
        _styleEditorY = position.Y;

        ShowStyleEditor(isFromPointer: true);
    }

    /// <summary>プレビューへ操作を渡す。</summary>
    /// <param name="gesture">操作の種類。</param>
    /// <param name="position">位置（ローカル DIP）。</param>
    /// <param name="clickCount">クリック回数。</param>
    /// <param name="wheelDelta">ホイールの移動量。</param>
    private void RaisePreviewGesture(
        AnnotationPreviewGesture gesture,
        Windows.Foundation.Point position,
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
                IsDown(VirtualKey.Shift),
                IsDown(VirtualKey.Control)));

    /// <summary>ホイールが回されたときの処理（ズーム・スクロールはプレビューへ渡す）。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">ポインターの引数。</param>
    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        _ = sender;

        RaisePreviewGesture(
            AnnotationPreviewGesture.Wheel,
            e.GetCurrentPoint(this).Position,
            1,
            e.GetCurrentPoint(this).Properties.MouseWheelDelta);
        e.Handled = true;
    }

    /// <summary>ホバー中のポインターの形を決めて適用する。</summary>
    /// <param name="position">位置（ローカル DIP）。</param>
    private void ApplyHoverCursor(Windows.Foundation.Point position)
    {
        var kind = ResolvePointerKind(new Windows.Foundation.Point(position.X, position.Y));

        ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(kind switch
        {
            AnnotationPointerKind.Move => Microsoft.UI.Input.InputSystemCursorShape.SizeAll,
            AnnotationPointerKind.ResizeTop or AnnotationPointerKind.ResizeBottom
                => Microsoft.UI.Input.InputSystemCursorShape.SizeNorthSouth,
            AnnotationPointerKind.ResizeLeft or AnnotationPointerKind.ResizeRight
                => Microsoft.UI.Input.InputSystemCursorShape.SizeWestEast,
            AnnotationPointerKind.ResizeTopLeft or AnnotationPointerKind.ResizeBottomRight
                => Microsoft.UI.Input.InputSystemCursorShape.SizeNorthwestSoutheast,
            AnnotationPointerKind.ResizeTopRight or AnnotationPointerKind.ResizeBottomLeft
                => Microsoft.UI.Input.InputSystemCursorShape.SizeNortheastSouthwest,
            AnnotationPointerKind.Tail => Microsoft.UI.Input.InputSystemCursorShape.Hand,
            _ => Microsoft.UI.Input.InputSystemCursorShape.Arrow,
        });
    }

    /// <summary>ポインターが領域から外れたときの処理（プレビューのホバー表示を消す）。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">ポインターの引数。</param>
    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _ = sender;

        // 外へ出たら既定の形へ戻す（戻さないと移動の合図が残る）
        ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Arrow);

        if (!_isPreviewDragging && !IsEditingText)
        {
            RaisePreviewGesture(AnnotationPreviewGesture.Leave, e.GetCurrentPoint(this).Position, 1, 0);
        }
    }

    /// <summary>位置（DIP）で示される操作の種類を求める（しっぽ → ハンドル → 本体の優先順）。</summary>
    /// <param name="position">位置（ローカル DIP）。</param>
    /// <returns>操作の種類。 注釈の上でない場合は <see cref="AnnotationPointerKind.None"/>。</returns>
    private AnnotationPointerKind ResolvePointerKind(Windows.Foundation.Point position)
    {
        var transform = _frozenTransform ?? ResolveImageTransform();
        var dip = new EsPoint(position.X, position.Y);
        return AnnotationPointerPolicy.Resolve(
            _session.Document,
            _session.SelectedId,
            transform.ToSource(dip),
            dip,
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

        Redraw();
    }
}

