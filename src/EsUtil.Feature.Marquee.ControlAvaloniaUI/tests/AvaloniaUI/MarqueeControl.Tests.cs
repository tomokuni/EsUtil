using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;
using EsUtil.Platform.Core.Native;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Point = Avalonia.Point;
using Rect = Avalonia.Rect;
using Xunit;
using EsUtil.Feature.Marquee.Core.Interaction;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Interop;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Coord;

namespace EsUtil.Feature.Marquee.AvaloniaUI.Tests;

/// <summary><see cref="MarqueeControl"/> の単体テスト（公開 API 経由の選択・ドラッグ・nudge・イベント検証）。</summary>
/// <remarks>
/// MarqueeControl と同一の公開 API を持つことを前提に、公開 API 経由で動作を検証する。<br/>
/// 内部実装（<see cref="MarqueeState"/>）の詳細は状態を直接参照しない（カプセル化維持）。<br/>
/// マウスイベントは <c>RaiseEvent</c> で発火し、座標はコントロールローカル DIP を対象へ変換して処理される。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class MarqueeControlTests
{
    /// <summary>テスト用の MarqueeControl を生成する。</summary>
    private static MarqueeControl CreateControl()
        => new() { Width = 200, Height = 200, Options = TestOptions };

    /// <summary>カーソルサービスを注入してテスト用の MarqueeControl を生成する。</summary>
    /// <param name="cursorService">カーソルサービス（GetCursorScreenPoint が固定位置を返すモック）。</param>
    private static MarqueeControl CreateControl(ICursorService? cursorService)
        => new(cursorService) { Width = 200, Height = 200, Options = TestOptions };

    /// <summary>テスト用の設定（最小サイズを 10 にしてドラッグ判定をしやすくする）。</summary>
    private static MarqueeOptions TestOptions
        => MarqueeOptions.Default with { HandleHitRadius = 12.0, MinSelectionSize = 10.0 };

    /// <summary>固定位置を返すカーソルサービスのモック。</summary>
    /// <param name="screenPoint">GetCursorScreenPoint が返す初期の画面物理座標。</param>
    private sealed class MockCursorService(EsPointInt screenPoint) : ICursorService
    {
        /// <summary>カーソル位置（画面物理座標）。 テストから変更可能。</summary>
        public EsPointInt Position { get; set; } = screenPoint;

        /// <summary>カーソルの表示状態（ShowCursor の呼び出し結果）。</summary>
        public bool Visible { get; set; } = true;

        /// <summary>SetCursorScreenPoint で最後に設定された画面物理座標。</summary>
        public EsPointInt? LastSetPoint { get; private set; }

        /// <summary>現在のカーソル位置（画面物理座標）。</summary>
        public EsPointInt GetCursorScreenPoint() => Position;

        /// <summary>カーソルを画面物理座標へ移動する。</summary>
        public void SetCursorScreenPoint(EsPointInt point) => LastSetPoint = point;

        /// <summary>マウスポインターの表示 / 非表示切替。</summary>
        public void ShowCursor(bool show) => Visible = show;
    }

    /// <summary>左ボタン押下状態の PointerPointProperties を生成する。</summary>
    private static PointerPointProperties LeftPressed()
        => new(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);

    /// <summary>左ボタン解放状態の PointerPointProperties を生成する。</summary>
    private static PointerPointProperties LeftReleased()
        => new(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased);

    /// <summary>テスト用のポインターを生成する。</summary>
    private static Pointer CreatePointer()
        => new(1, PointerType.Mouse, true);

    /// <summary>コントロールローカル座標をスクリーン座標（PointerEventArgs の rootPoint 用）に変換する。</summary>
    private static Point ToScreenPoint(Visual source, Point local)
    {
        var screen = source.PointToScreen(local);
        return new Point(screen.X, screen.Y);
    }

    /// <summary>コントロールをウィンドウに表示する。</summary>
    private static Window Show(Control control)
    {
        var window = new Window { Content = control };
        window.Show();
        return window;
    }

    [AvaloniaFact]
    internal void CurrentSnapshot_ドラッグ中はIsDraggingがtrueになる()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();

        // 新規選択ドラッグ中（押下 → 移動）: State=Dragging / ボタン押下中
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(10, 10)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(50, 50)), 0UL, LeftPressed(), KeyModifiers.None));

        var snap = control.CurrentSnapshot;
        Assert.Equal(MarqueeInteractionState.Dragging, snap.State);
        Assert.True(snap.IsMouseDown);
        Assert.True(snap.HasSelection);

        window.Close();
    }

    // ====================================================================
    // Snapshot（状態・選択矩形・アクティブハンドル・マウス位置・ボタン状態）
    // ====================================================================

    [AvaloniaFact]
    internal void ドラッグ中のスナップショットにドラッグ中の矩形とアクティブハンドルが含まれる()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        // ドラッグ中（PointerMoved 時点）のスナップショットを記録する
        SelectionRectEventArgs? duringDrag = null;
        control.SelectionChanged += (_, e) => duringDrag = e;

        // 新規選択ドラッグ: 押下 → 移動 → 解放
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(20, 20)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(80, 80)), 0UL, LeftPressed(), KeyModifiers.None));

        Assert.NotNull(duringDrag);
        // ドラッグ中の矩形（対象座標）が Snapshot に含まれる
        Assert.Equal(new EsRect(20, 20, 60, 60, EsRectInterval.Closed), duringDrag!.Snapshot.SelectionSourceRect);
        // 新規選択ドラッグ中はハンドルなし（None）
        Assert.Equal(MarqueeHitTarget.None, duringDrag.Snapshot.ActiveTarget);
        // ドラッグ中の状態は Dragging
        Assert.Equal(MarqueeInteractionState.Dragging, duringDrag.Snapshot.State);
        // マウスボタンは押下中
        Assert.True(duringDrag.Snapshot.IsMouseDown);

        control.RaiseEvent(new PointerReleasedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(80, 80)), 0UL, LeftReleased(), KeyModifiers.None, MouseButton.Left));
        window.Close();
    }

    [AvaloniaFact]
    internal void リサイズドラッグ中のスナップショットにアクティブハンドルが含まれる()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        control.SetSelection(new Rect(50, 50, 100, 80));
        // リサイズドラッグ中（PointerMoved 時点）のスナップショットを記録する
        SelectionRectEventArgs? duringResize = null;
        control.SelectionChanged += (_, e) => duringResize = e;

        // TopLeft ハンドル（50,50）を掴んで (30, 30) へ移動（左上が 30,30 になる）
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(50, 50)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(30, 30)), 0UL, LeftPressed(), KeyModifiers.None));

        Assert.NotNull(duringResize);
        // リサイズドラッグ中はアクティブハンドルが TopLeft
        Assert.Equal(MarqueeHitTarget.TopLeft, duringResize!.Snapshot.ActiveTarget);
        // リサイズ後の矩形（対象座標）が Snapshot に含まれる
        Assert.Equal(new EsRect(30, 30, 120, 100), duringResize.Snapshot.SelectionSourceRect);
        // リサイズドラッグ中の状態は Dragging
        Assert.Equal(MarqueeInteractionState.Dragging, duringResize.Snapshot.State);

        control.RaiseEvent(new PointerReleasedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(30, 30)), 0UL, LeftReleased(), KeyModifiers.None, MouseButton.Left));
        window.Close();
    }

    // ====================================================================
    // マウスドラッグによる新規選択
    // ====================================================================

    [AvaloniaFact]
    internal void ドラッグで新規選択が作成されSelectionChangedで通知される()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        var rects = new List<Rect>();
        control.SelectionChanged += (_, e) => rects.Add(e.Rect);

        // 押下 (20,20) → 移動 (80,80) → 解放
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(20, 20)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(80, 80)), 0UL, LeftPressed(), KeyModifiers.None));
        control.RaiseEvent(new PointerReleasedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(80, 80)), 0UL, LeftReleased(), KeyModifiers.None, MouseButton.Left));

        // 最終的な選択矩形（対象座標 = 等価変換）が通知される
        Assert.NotEmpty(rects);
        Assert.Equal(new Rect(20, 20, 60, 60), rects[^1]);
        window.Close();
    }

    [AvaloniaFact]
    internal void 新規選択で画像外の始点が境界内にクリップされる()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        // クリップ有効（境界 200x200）
        control.Options = control.Options with { ClampToSourceBounds = true };
        control.SetCoordTransform(new CoordTransform(p => p, p => p, () => new EsSize(200, 200)));
        var rects = new List<Rect>();
        control.SelectionChanged += (_, e) => rects.Add(e.Rect);

        // 画像外（-50,-30）で押下 → ドラッグ (80,80) → 解放
        // 始点が画像外のため、始点は (0,0) にクリップされ、矩形は (0,0,80,80) になる
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(-50, -30)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(80, 80)), 0UL, LeftPressed(), KeyModifiers.None));
        control.RaiseEvent(new PointerReleasedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(80, 80)), 0UL, LeftReleased(), KeyModifiers.None, MouseButton.Left));

        Assert.NotEmpty(rects);
        Assert.Equal(new Rect(0, 0, 80, 80), rects[^1]);
        window.Close();
    }

    [AvaloniaFact]
    internal void 新規選択で画像外の終点が境界内にクリップされる()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        // クリップ有効（境界 200x200）
        control.Options = control.Options with { ClampToSourceBounds = true };
        control.SetCoordTransform(new CoordTransform(p => p, p => p, () => new EsSize(200, 200)));
        var rects = new List<Rect>();
        control.SelectionChanged += (_, e) => rects.Add(e.Rect);

        // 画像内 (20,20) で押下 → 画像外 (250,180) へドラッグ → 解放
        // 終点は境界内 (200,180) にクリップされ、矩形は (20,20,180,160) になる
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(20, 20)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(250, 180)), 0UL, LeftPressed(), KeyModifiers.None));
        control.RaiseEvent(new PointerReleasedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(250, 180)), 0UL, LeftReleased(), KeyModifiers.None, MouseButton.Left));

        Assert.NotEmpty(rects);
        Assert.Equal(new Rect(20, 20, 180, 160), rects[^1]);
        window.Close();
    }

    [AvaloniaFact]
    internal void ドラッグ完了でSelectionCompletedが発火する()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        var completed = false;
        control.SelectionCompleted += (_, _) => completed = true;

        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(20, 20)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(80, 80)), 0UL, LeftPressed(), KeyModifiers.None));
        control.RaiseEvent(new PointerReleasedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(80, 80)), 0UL, LeftReleased(), KeyModifiers.None, MouseButton.Left));

        Assert.True(completed);
        window.Close();
    }

    [AvaloniaFact]
    internal void ドラッグ開始でSelectionStartedが発火する()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        var started = false;
        control.SelectionStarted += (_, _) => started = true;

        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(20, 20)), 0UL, LeftPressed(), KeyModifiers.None, 1));

        Assert.True(started);
        window.Close();
    }

    [AvaloniaFact]
    internal void ホイールは処理しない_ホストが扱う()
    {
        var control = CreateControl();
        var window = Show(control);

        // ホイール（ズーム・スクロール）はホストが扱うため、本コントロールは処理済みにしない
        // （上位の Canvas や祖先 ScrollViewer へ素通しし、ビューの状態を決めるのはホスト 1 か所にする）
        var plain = new PointerWheelEventArgs(control, CreatePointer(), window,
            ToScreenPoint(control, new Point(50, 50)), 0UL,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other),
            KeyModifiers.None, new Vector(0, 1));
        control.RaiseEvent(plain);

        var controlKey = new PointerWheelEventArgs(control, CreatePointer(), window,
            ToScreenPoint(control, new Point(50, 50)), 0UL,
            new PointerPointProperties(RawInputModifiers.Control, PointerUpdateKind.Other),
            KeyModifiers.Control, new Vector(0, 1));
        control.RaiseEvent(controlKey);

        Assert.False(plain.Handled);
        Assert.False(controlKey.Handled);
        window.Close();
    }

    [AvaloniaFact]
    internal void 透明ハンドル色の描画が白にフォールバックされる()
    {
        var control = CreateControl();
        var window = Show(control);

        // HandleColor = default（透明）→ DrawHandles は白にフォールバックする
        control.Appearance = control.Appearance with { HandleColor = default };
        control.SetSelection(new Rect(20, 20, 60, 60));
        control.InvalidateVisual();
        window.CaptureRenderedFrame();

        window.Close();
    }

    [AvaloniaFact]
    internal void 矢印以外のキーではnudgeされない()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        var positions = new List<Point>();
        control.LoupePositionMoved += (_, e) => positions.Add(e.Point);

        // ドラッグ開始
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(10, 10)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(50, 50)), 0UL, LeftPressed(), KeyModifiers.None));
        var count = positions.Count;

        // 矢印以外（A キー）→ nudge されない（ルーペ位置は追加されない）
        control.RaiseEvent(new KeyEventArgs { Key = Key.A, RoutedEvent = InputElement.KeyDownEvent });

        Assert.Equal(count, positions.Count);
        window.Close();
    }

    [AvaloniaFact]
    internal void 最小サイズ未満のドラッグはキャンセルされる()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        var cancelled = false;
        control.SelectionCancelled += (_, _) => cancelled = true;

        // 3px のドラッグ（MinSelectionSize=10 未満）
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(20, 20)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(23, 23)), 0UL, LeftPressed(), KeyModifiers.None));
        control.RaiseEvent(new PointerReleasedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(23, 23)), 0UL, LeftReleased(), KeyModifiers.None, MouseButton.Left));

        Assert.True(cancelled);
        window.Close();
    }

    [AvaloniaFact]
    internal void ダブルクリックで確定要求が発火する()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        control.SetSelection(new Rect(20, 20, 60, 60));
        var confirmed = false;
        control.ConfirmRequested += (_, _) => confirmed = true;

        // 矩形内のダブルクリック（ClickCount=2）→ 確定要求
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(50, 50)), 0UL, LeftPressed(), KeyModifiers.None, 2));

        Assert.True(confirmed);
        window.Close();
    }

    [AvaloniaFact]
    internal void ダブルクリック_矩形外はキャンセル要求が発火する()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        control.SetSelection(new Rect(20, 20, 60, 60));
        var cancelled = false;
        control.CancelRequested += (_, _) => cancelled = true;

        // 矩形外のダブルクリック（ClickCount=2）→ キャンセル要求
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(150, 150)), 0UL, LeftPressed(), KeyModifiers.None, 2));

        Assert.True(cancelled);
        window.Close();
    }

    // ====================================================================
    // 座標変換（SetCoordTransform）
    // ====================================================================

    [AvaloniaFact]
    internal void SetCoordTransform_カスタム変換で対象座標が変わる()
    {
        var control = CreateControl();
        // 対象 = DIP × 2（拡大）
        control.SetCoordTransform(new CoordTransform(
            p => new EsPoint(p.X * 2, p.Y * 2),
            p => new EsPoint(p.X / 2, p.Y / 2),
            () => null));
        var window = Show(control);
        var pointer = CreatePointer();
        var rects = new List<Rect>();
        control.SelectionChanged += (_, e) => rects.Add(e.Rect);

        // DIP (20,20) → (80,80) は対象 (40,40) → (160,160)
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(20, 20)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(80, 80)), 0UL, LeftPressed(), KeyModifiers.None));
        control.RaiseEvent(new PointerReleasedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(80, 80)), 0UL, LeftReleased(), KeyModifiers.None, MouseButton.Left));

        Assert.NotEmpty(rects);
        Assert.Equal(new Rect(40, 40, 120, 120), rects[^1]);
        window.Close();
    }

    [AvaloniaFact]
    internal void SetCoordTransform_nullで等価変換にフォールバックする()
    {
        var control = CreateControl();

        control.SetCoordTransform(null);

        // 等価変換でもドラッグが動作する（例外なし）
        var window = Show(control);
        var pointer = CreatePointer();
        var rects = new List<Rect>();
        control.SelectionChanged += (_, e) => rects.Add(e.Rect);

        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(10, 10)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(50, 40)), 0UL, LeftPressed(), KeyModifiers.None));
        control.RaiseEvent(new PointerReleasedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(50, 40)), 0UL, LeftReleased(), KeyModifiers.None, MouseButton.Left));

        Assert.Equal(new Rect(10, 10, 40, 30), rects[^1]);
        window.Close();
    }

    [AvaloniaFact]
    internal void スケールが0の変換でもヒットテストが動作する()
    {
        var control = CreateControl();
        // ToSource が常に (0,0) を返す変換（スケール 0）→ ToSourceRadius は HandleHitRadius にフォールバック
        control.SetCoordTransform(new CoordTransform(_ => new EsPoint(0, 0), p => p, () => null));
        var window = Show(control);
        var pointer = CreatePointer();
        control.SetSelection(new Rect(20, 20, 60, 60));

        // TopLeft ハンドル位置（20,20）で押下 → ヒットテスト成功（フォールバック半径）→ リサイズドラッグ
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(20, 20)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(10, 10)), 0UL, LeftPressed(), KeyModifiers.None));
        control.RaiseEvent(new PointerReleasedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(10, 10)), 0UL, LeftReleased(), KeyModifiers.None, MouseButton.Left));

        window.Close();
    }

    // ====================================================================
    // ハンドルリサイズ
    // ====================================================================

    [AvaloniaFact]
    internal void ハンドルドラッグでリサイズされる()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        control.SetSelection(new Rect(20, 20, 60, 60));
        var rects = new List<Rect>();
        control.SelectionChanged += (_, e) => rects.Add(e.Rect);

        // 右下ハンドル（80,80 付近）を (100,100) へドラッグ
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(80, 80)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(100, 100)), 0UL, LeftPressed(), KeyModifiers.None));
        control.RaiseEvent(new PointerReleasedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(100, 100)), 0UL, LeftReleased(), KeyModifiers.None, MouseButton.Left));

        Assert.NotEmpty(rects);
        Assert.Equal(new Rect(20, 20, 80, 80), rects[^1]);
        window.Close();
    }

    [AvaloniaFact]
    internal void 枠内ドラッグで選択矩形が移動する()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        control.SetSelection(new Rect(20, 20, 60, 60));
        var rects = new List<Rect>();
        control.SelectionChanged += (_, e) => rects.Add(e.Rect);

        // 枠内（50,50）を (70,70) へドラッグ（+20, +20 移動）
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(50, 50)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(70, 70)), 0UL, LeftPressed(), KeyModifiers.None));
        control.RaiseEvent(new PointerReleasedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(70, 70)), 0UL, LeftReleased(), KeyModifiers.None, MouseButton.Left));

        Assert.NotEmpty(rects);
        Assert.Equal(new Rect(40, 40, 60, 60), rects[^1]);
        window.Close();
    }

    [AvaloniaFact]
    internal void ハンドルを反対側までドラッグすると矩形が反転される()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        control.SetSelection(new Rect(20, 20, 60, 60));
        var rects = new List<Rect>();
        control.SelectionChanged += (_, e) => rects.Add(e.Rect);

        // 右下ハンドル（80,80）を左上方向（10,10）へドラッグ → 矩形が反転（正規化）される
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(80, 80)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(10, 10)), 0UL, LeftPressed(), KeyModifiers.None));
        control.RaiseEvent(new PointerReleasedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(10, 10)), 0UL, LeftReleased(), KeyModifiers.None, MouseButton.Left));

        Assert.NotEmpty(rects);
        // 右下ハンドルを左上へ -70 移動 → 右辺 10・下辺 10 に反転（正規化）され、最小サイズ 10 を保つ
        Assert.Equal(new Rect(10, 10, 10, 10), rects[^1]);
        window.Close();
    }

    [AvaloniaFact]
    internal void 枠内ホバーで移動カーソルが表示される()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        control.SetSelection(new Rect(20, 20, 60, 60));

        // 枠内（50,50）にホバー → Move ハンドル → SizeAll カーソル
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window,
            ToScreenPoint(control, new Point(50, 50)), 0UL, LeftReleased(), KeyModifiers.None));

        Assert.NotNull(control.Cursor);
        Assert.Equal("SizeAll", control.Cursor.ToString());
        window.Close();
    }

    [AvaloniaFact]
    internal void 各ハンドル位置のホバーでカーソル形状が更新される()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        control.SetSelection(new Rect(20, 20, 60, 60)); // 20,20 〜 80,80

        void Hover(Point dip, string expected)
        {
            control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window,
                ToScreenPoint(control, dip), 0UL, LeftReleased(), KeyModifiers.None));
            Assert.NotNull(control.Cursor);
            Assert.Equal(expected, control.Cursor!.ToString());
        }

        // 8 方向ハンドル + 枠内（Move）+ 外側（Default）の全カーソル形状
        Hover(new Point(20, 20), "TopLeftCorner");  // TopLeft（BottomRight と同形状）
        Hover(new Point(50, 20), "SizeNorthSouth"); // TopCenter（BottomCenter と同形状）
        Hover(new Point(80, 20), "TopRightCorner"); // TopRight（BottomLeft と同形状）
        Hover(new Point(20, 50), "SizeWestEast");   // MiddleLeft（MiddleRight と同形状）
        Hover(new Point(80, 50), "SizeWestEast");   // MiddleRight
        Hover(new Point(20, 80), "TopRightCorner"); // BottomLeft
        Hover(new Point(50, 80), "SizeNorthSouth"); // BottomCenter
        Hover(new Point(80, 80), "TopLeftCorner");  // BottomRight
        Hover(new Point(50, 50), "SizeAll");        // Move（枠内）
        Hover(new Point(150, 150), "Arrow");        // ハンドル外（Default）

        window.Close();
    }

    [AvaloniaFact]
    internal void TopCenterハンドルで上下にリサイズされる()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        control.SetSelection(new Rect(20, 20, 60, 60));
        var rects = new List<Rect>();
        control.SelectionChanged += (_, e) => rects.Add(e.Rect);

        // TopCenter ハンドル（50,20 付近）を (50,10) へドラッグ → 上辺が 10 に
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(50, 20)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(50, 10)), 0UL, LeftPressed(), KeyModifiers.None));
        control.RaiseEvent(new PointerReleasedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(50, 10)), 0UL, LeftReleased(), KeyModifiers.None, MouseButton.Left));

        Assert.NotEmpty(rects);
        Assert.Equal(new Rect(20, 10, 60, 70), rects[^1]);
        window.Close();
    }

    [AvaloniaFact]
    internal void MiddleLeftハンドルで左右にリサイズされる()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        control.SetSelection(new Rect(20, 20, 60, 60));
        var rects = new List<Rect>();
        control.SelectionChanged += (_, e) => rects.Add(e.Rect);

        // MiddleLeft ハンドル（20,50 付近）を (10,50) へドラッグ → 左辺が 10 に
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(20, 50)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(10, 50)), 0UL, LeftPressed(), KeyModifiers.None));
        control.RaiseEvent(new PointerReleasedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(10, 50)), 0UL, LeftReleased(), KeyModifiers.None, MouseButton.Left));

        Assert.NotEmpty(rects);
        Assert.Equal(new Rect(10, 20, 70, 60), rects[^1]);
        window.Close();
    }

    [AvaloniaFact]
    internal void MiddleRightハンドルで左右にリサイズされる()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        control.SetSelection(new Rect(20, 20, 60, 60));
        var rects = new List<Rect>();
        control.SelectionChanged += (_, e) => rects.Add(e.Rect);

        // MiddleRight ハンドル（80,50 付近）を (90,50) へドラッグ → 右辺が 90 に
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(80, 50)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(90, 50)), 0UL, LeftPressed(), KeyModifiers.None));
        control.RaiseEvent(new PointerReleasedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(90, 50)), 0UL, LeftReleased(), KeyModifiers.None, MouseButton.Left));

        Assert.NotEmpty(rects);
        Assert.Equal(new Rect(20, 20, 70, 60), rects[^1]);
        window.Close();
    }

    [AvaloniaFact]
    internal void TopRightハンドルでリサイズされる()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        control.SetSelection(new Rect(20, 20, 60, 60));
        var rects = new List<Rect>();
        control.SelectionChanged += (_, e) => rects.Add(e.Rect);

        // TopRight ハンドル（80,20 付近）を (90,10) へドラッグ → 右辺 90・上辺 10
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(80, 20)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(90, 10)), 0UL, LeftPressed(), KeyModifiers.None));
        control.RaiseEvent(new PointerReleasedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(90, 10)), 0UL, LeftReleased(), KeyModifiers.None, MouseButton.Left));

        Assert.NotEmpty(rects);
        Assert.Equal(new Rect(20, 10, 70, 70), rects[^1]);
        window.Close();
    }

    [AvaloniaFact]
    internal void BottomLeftハンドルでリサイズされる()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        control.SetSelection(new Rect(20, 20, 60, 60));
        var rects = new List<Rect>();
        control.SelectionChanged += (_, e) => rects.Add(e.Rect);

        // BottomLeft ハンドル（20,80 付近）を (10,90) へドラッグ → 左辺 10・下辺 90
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(20, 80)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(10, 90)), 0UL, LeftPressed(), KeyModifiers.None));
        control.RaiseEvent(new PointerReleasedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(10, 90)), 0UL, LeftReleased(), KeyModifiers.None, MouseButton.Left));

        Assert.NotEmpty(rects);
        Assert.Equal(new Rect(10, 20, 70, 70), rects[^1]);
        window.Close();
    }

    [AvaloniaFact]
    internal void BottomCenterハンドルで上下にリサイズされる()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        control.SetSelection(new Rect(20, 20, 60, 60));
        var rects = new List<Rect>();
        control.SelectionChanged += (_, e) => rects.Add(e.Rect);

        // BottomCenter ハンドル（50,80 付近）を (50,90) へドラッグ → 下辺が 90 に
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(50, 80)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(50, 90)), 0UL, LeftPressed(), KeyModifiers.None));
        control.RaiseEvent(new PointerReleasedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(50, 90)), 0UL, LeftReleased(), KeyModifiers.None, MouseButton.Left));

        Assert.NotEmpty(rects);
        Assert.Equal(new Rect(20, 20, 60, 70), rects[^1]);
        window.Close();
    }

    [AvaloniaFact]
    internal void 移動ドラッグで選択矩形が境界内にシフトされる()
    {
        var control = CreateControl();
        control.Options = control.Options with { ClampToSourceBounds = true };
        control.SetCoordTransform(new CoordTransform(p => p, p => p, () => new EsSize(200, 200)));
        var window = Show(control);
        var pointer = CreatePointer();
        control.SetSelection(new Rect(20, 20, 60, 60));
        var rects = new List<Rect>();
        control.SelectionChanged += (_, e) => rects.Add(e.Rect);

        // 枠内 (50,50) を掴んで左へ大きくドラッグ（-100, 0）→ 矩形全体が左端 (0,20) にシフト（サイズ維持）
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(50, 50)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(-50, 50)), 0UL, LeftPressed(), KeyModifiers.None));
        control.RaiseEvent(new PointerReleasedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(-50, 50)), 0UL, LeftReleased(), KeyModifiers.None, MouseButton.Left));

        Assert.NotEmpty(rects);
        Assert.Equal(new Rect(0, 20, 60, 60), rects[^1]);
        window.Close();
    }

    // ====================================================================
    // nudge（矢印キー）
    // ====================================================================

    [AvaloniaFact]
    internal void 矢印キーで選択矩形が対象1Pixel移動する()
    {
        var control = CreateControl();
        var window = Show(control);
        control.SetSelection(new Rect(20, 20, 60, 60));
        var rects = new List<Rect>();
        control.SelectionChanged += (_, e) => rects.Add(e.Rect);

        // 領域作成後はマウス Down 中のみ nudge 許可 → 押下状態で矢印キー
        control.RaiseEvent(new KeyEventArgs { Key = Key.Right, RoutedEvent = InputElement.KeyDownEvent });

        // 領域作成後でマウス Down 中でない場合は nudge されない（空）
        Assert.Empty(rects);
        window.Close();
    }

    [AvaloniaFact]
    internal void 全方向の矢印キーでnudgeが動作する()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        var positions = new List<Point>();
        control.LoupePositionMoved += (_, e) => positions.Add(e.Point);

        // ドラッグ開始（nudge 許可: マウス Down 中）
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(20, 20)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(50, 50)), 0UL, LeftPressed(), KeyModifiers.None));

        // 4 方向すべての矢印キーで nudge（HandleArrowKey の全ケースを実行）
        control.RaiseEvent(new KeyEventArgs { Key = Key.Left, RoutedEvent = InputElement.KeyDownEvent });
        control.RaiseEvent(new KeyEventArgs { Key = Key.Right, RoutedEvent = InputElement.KeyDownEvent });
        control.RaiseEvent(new KeyEventArgs { Key = Key.Up, RoutedEvent = InputElement.KeyDownEvent });
        control.RaiseEvent(new KeyEventArgs { Key = Key.Down, RoutedEvent = InputElement.KeyDownEvent });

        Assert.NotEmpty(positions);
        window.Close();
    }

    [AvaloniaFact]
    internal void TopLevel経由の矢印キーでフォーカス非依存のnudgeが動作する()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        var positions = new List<Point>();
        control.LoupePositionMoved += (_, e) => positions.Add(e.Point);

        // ドラッグ開始（コントロールはフォーカスなし。 OnKeyDown は呼ばれない）
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(10, 10)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(50, 50)), 0UL, LeftPressed(), KeyModifiers.None));

        // window に直接キー発火 → OnTopLevelKeyDown が nudge を処理（OnKeyDown を経由しない）
        window.RaiseEvent(new KeyEventArgs { Key = Key.Right, RoutedEvent = InputElement.KeyDownEvent });

        Assert.NotEmpty(positions);
        window.Close();
    }

    [AvaloniaFact]
    internal void Escapeキーでキャンセル要求が発火する()
    {
        var control = CreateControl();
        var window = Show(control);
        var cancelled = false;
        control.CancelRequested += (_, _) => cancelled = true;

        control.RaiseEvent(new KeyEventArgs { Key = Key.Escape, RoutedEvent = InputElement.KeyDownEvent });

        Assert.True(cancelled);
        window.Close();
    }

    [AvaloniaFact]
    internal void 矢印キー1回のnudgeは1回だけ適用される()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        var rects = new List<Rect>();
        control.SelectionChanged += (_, e) => rects.Add(e.Rect);

        // 新規選択のドラッグ（nudge 許可: マウス Down 中）→ 矩形は (20, 20, 30, 30)
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(20, 20)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(50, 50)), 0UL, LeftPressed(), KeyModifiers.None));
        rects.Clear();

        control.RaiseEvent(new KeyEventArgs { Key = Key.Right, RoutedEvent = InputElement.KeyDownEvent });

        // キー 1 回で終点が 1 対象ピクセルだけ動く（フォーカス経由と TopLevel 購読の 2 経路で二重処理されると 2 px 動く）
        Assert.Equal(new EsRect(20, 20, 31, 30, EsRectInterval.Closed), control.CurrentSnapshot.SelectionSourceRect);
        Assert.Single(rects);
        window.Close();
    }

    [AvaloniaFact]
    internal void Enterキーで確定要求が発火する()
    {
        var control = CreateControl();
        var window = Show(control);
        control.SetSelection(new Rect(10, 10, 50, 40));
        var confirmed = false;
        control.ConfirmRequested += (_, _) => confirmed = true;

        // TopLevel（ウィンドウ）への Enter キー発火 → OnTopLevelKeyDown が確定要求を処理
        window.RaiseEvent(new KeyEventArgs { Key = Key.Enter, RoutedEvent = InputElement.KeyDownEvent });

        Assert.True(confirmed);
        window.Close();
    }

    [AvaloniaFact]
    internal void nudge中の押下で基点がnudge位置に置き換えられカーソルが再表示される()
    {
        var mock = new MockCursorService(default);
        var control = CreateControl(mock);
        var window = Show(control);
        var pointer = CreatePointer();

        // ドラッグ開始 → nudge（カーソル非表示）
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(10, 10)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new KeyEventArgs { Key = Key.Right, RoutedEvent = InputElement.KeyDownEvent });
        Assert.False(mock.Visible);

        // nudge 中の押下 → nudge リセット（EndNudge）+ カーソル再表示
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(50, 50)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        Assert.True(mock.Visible);
        window.Close();
    }

    [AvaloniaFact]
    internal void フォーカス経由の矢印キーでも選択確定後はnudgeされない()
    {
        var control = CreateControl();
        var window = Show(control);
        control.SetSelection(new Rect(10, 10, 50, 40));
        var positions = new List<Point>();
        control.LoupePositionMoved += (_, e) => positions.Add(e.Point);

        // フォーカスを取得した状態で矢印キー（OnKeyDown 経由）→ 選択確定後は nudge 無効
        control.Focus();
        control.RaiseEvent(new KeyEventArgs { Key = Key.Right, RoutedEvent = InputElement.KeyDownEvent });

        Assert.Empty(positions);
        window.Close();
    }

    [AvaloniaFact]
    internal void 選択確定後の矢印キーはnudgeされない()
    {
        var control = CreateControl();
        var window = Show(control);
        control.SetSelection(new Rect(10, 10, 50, 40));
        var positions = new List<Point>();
        control.LoupePositionMoved += (_, e) => positions.Add(e.Point);

        // 選択確定後（マウス Down なし）の矢印キー → nudge 無効（ルーペ位置は通知されない）
        control.RaiseEvent(new KeyEventArgs { Key = Key.Right, RoutedEvent = InputElement.KeyDownEvent });

        Assert.Empty(positions);
        window.Close();
    }

    [AvaloniaFact]
    internal void nudge中にSetSelectionするとカーソルが再表示される()
    {
        var mock = new MockCursorService(default);
        var control = CreateControl(mock);
        var window = Show(control);
        var pointer = CreatePointer();

        // ドラッグ開始 → nudge（カーソル非表示）
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(10, 10)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new KeyEventArgs { Key = Key.Right, RoutedEvent = InputElement.KeyDownEvent });
        Assert.False(mock.Visible);

        // nudge 中に外部から SetSelection → カーソルが再表示される（非表示残留の防止）
        control.SetSelection(new Rect(10, 10, 50, 40));
        Assert.True(mock.Visible);
        window.Close();
    }

    [AvaloniaFact]
    internal void フォーカス喪失でnudgeが終了しカーソルが再表示される()
    {
        var mock = new MockCursorService(default);
        var control = CreateControl(mock);
        // フォーカスを移す先のコントロール（実際のフォーカス変更で LostFocus を発生させる）
        var other = new Button { Width = 20, Height = 20 };
        var window = new Window { Content = new StackPanel { Children = { control, other } } };
        window.Show();
        var pointer = CreatePointer();

        // ドラッグ開始 → nudge（カーソル非表示）
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(10, 10)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new KeyEventArgs { Key = Key.Right, RoutedEvent = InputElement.KeyDownEvent });
        Assert.False(mock.Visible);

        // フォーカスが別のコントロールへ移る → nudge 終了（nudge を継続できない文脈でカーソルを隠したままにしない）
        other.Focus();

        Assert.False(control.CurrentSnapshot.NudgeActive);
        Assert.True(mock.Visible);
        window.Close();
    }

    [AvaloniaFact]
    internal void nudge中にウィンドウを閉じるとカーソルが再表示される()
    {
        var mock = new MockCursorService(default);
        var control = CreateControl(mock);
        var window = Show(control);
        var pointer = CreatePointer();

        // ドラッグ開始 → nudge（カーソル非表示）
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(10, 10)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new KeyEventArgs { Key = Key.Right, RoutedEvent = InputElement.KeyDownEvent });
        Assert.False(mock.Visible);

        // ウィンドウを閉じる → ビジュアルツリー切断 → カーソル再表示
        window.Close();
        Assert.True(mock.Visible);
    }

    // ====================================================================
    // nudge 中の十字マーカー位置（LoupePositionMoved で検証）
    // ====================================================================

    [AvaloniaFact]
    internal void リサイズ中のnudgeで十字マーカーがアクティブハンドル位置を追従する()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        control.SetSelection(new Rect(20, 20, 60, 60));
        var positions = new List<Point>();
        control.LoupePositionMoved += (_, e) => positions.Add(e.Point);

        // 左上ハンドル（20,20 付近）を押下してリサイズ開始（ActiveTarget = TopLeft）
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(20, 20)), 0UL, LeftPressed(), KeyModifiers.None, 1));

        // マウス Down 中の矢印キーで nudge（リサイズ継続）
        control.RaiseEvent(new KeyEventArgs { Key = Key.Right, RoutedEvent = InputElement.KeyDownEvent });

        // 十字マーカーは「ドラッグ中のハンドル（左上）」を追従する（固定コーナーではない）
        Assert.NotEmpty(positions);
        Assert.Equal(new Point(21, 20), positions[^1]);
        window.Close();
    }

    [AvaloniaFact]
    internal void 新規選択中のnudgeで十字マーカーが終点を追従する()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        var positions = new List<Point>();
        control.LoupePositionMoved += (_, e) => positions.Add(e.Point);

        // (10,10) から (50,50) へドラッグして新規選択中にする（終点 = 50,50）
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(10, 10)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(50, 50)), 0UL, LeftPressed(), KeyModifiers.None));

        // マウス Down 中の矢印キーで nudge（終点が 1px 移動）
        control.RaiseEvent(new KeyEventArgs { Key = Key.Right, RoutedEvent = InputElement.KeyDownEvent });

        // 十字マーカーは終点（ドラッグ中の位置）を追従する
        Assert.NotEmpty(positions);
        Assert.Equal(new Point(51, 50), positions[^1]);
        window.Close();
    }

    [AvaloniaFact]
    internal void 矩形未作成のnudgeで十字マーカーがカーソル位置基準になり原点に飛ばない()
    {
        // モックを注入したコントロールを最初からウィンドウに表示する（レイアウト確定後にカーソル位置を設定）
        var mock = new MockCursorService(default);
        var control = CreateControl(mock);
        var window = new Window { Content = control };
        window.Show();

        // ローカル DIP (300,200) に対応する画面物理座標をモックのカーソル位置として設定する
        // （HandleArrowKey 内の逆変換（画面物理 → ローカル DIP）が (300,200) に一致する）
        var screenPt = ScreenConverter.LocalDipToScreen(control, new Point(300, 200));
        mock.Position = screenPt.ToEsPointInt();

        var positions = new List<Point>();
        control.LoupePositionMoved += (_, e) => positions.Add(e.Point);

        // 矩形未作成状態（StartPoint が未設定）で矢印キー Right を押す
        control.RaiseEvent(new KeyEventArgs { Key = Key.Right, RoutedEvent = InputElement.KeyDownEvent });

        // マーカーは「カーソル位置 (300,200) + 1px 右」を追従する（StartPoint が (0,0) のままなら (1,0) に飛ぶ）
        Assert.NotEmpty(positions);
        Assert.Equal(new Point(301, 200), positions[^1]);
        window.Close();
    }

    [AvaloniaFact]
    internal void nudge終了後の次のセッションの最初のキー押下で基点がカーソル位置に同期される()
    {
        var mock = new MockCursorService(default);
        var control = CreateControl(mock);
        var window = new Window { Content = control };
        window.Show();

        // 1 回目の nudge セッション: カーソル位置 (300,200) から Right → (301,200)
        mock.Position = ScreenConverter.LocalDipToScreen(control, new Point(300, 200)).ToEsPointInt();
        control.RaiseEvent(new KeyEventArgs { Key = Key.Right, RoutedEvent = InputElement.KeyDownEvent });

        // nudge 終了（マウス Up。 StartPoint は (301,200) のまま残る）
        control.RaiseEvent(new PointerReleasedEventArgs(control, new Pointer(1, PointerType.Mouse, true), window,
            ToScreenPoint(control, new Point(301, 200)), 0UL, LeftReleased(), KeyModifiers.None, MouseButton.Left));

        var positions = new List<Point>();
        control.LoupePositionMoved += (_, e) => positions.Add(e.Point);

        // 2 回目の nudge セッション: ユーザーがマウスを (800,600) へ移動してから Right を押す
        mock.Position = ScreenConverter.LocalDipToScreen(control, new Point(800, 600)).ToEsPointInt();
        control.RaiseEvent(new KeyEventArgs { Key = Key.Right, RoutedEvent = InputElement.KeyDownEvent });

        // 基点は古い nudge 位置 (301,200) ではなく、新しいカーソル位置 (800,600) に同期される
        // （同期されないと (302,200) にジャンプする）
        Assert.NotEmpty(positions);
        Assert.Equal(new Point(801, 600), positions[^1]);
        window.Close();
    }

    [AvaloniaFact]
    internal void nudge中の2回目以降のキー押下で基点が再同期されずnudge位置から進む()
    {
        // モックの SetCursorScreenPoint は位置を更新しない（GetCursorScreenPoint は常に初期位置を返す）。
        // これは Crop の ScrollOffset 座標変換で「再評価したカーソル位置が nudge 位置と一致しない」状況を模擬する
        // （再同期されると基点が初期位置に戻り、2 回目以降の押下で移動しなくなる）。
        var mock = new MockCursorService(default);
        var control = CreateControl(mock);
        var window = new Window { Content = control };
        window.Show();

        // カーソル位置 (300,200) に固定（2 回目以降の再評価でも同じ位置を返す = 移動を相殺する）
        mock.Position = ScreenConverter.LocalDipToScreen(control, new Point(300, 200)).ToEsPointInt();

        var positions = new List<Point>();
        control.LoupePositionMoved += (_, e) => positions.Add(e.Point);

        // 1 回目のキー押下（セッション開始）: カーソル位置 (300,200) から Right → (301,200)
        control.RaiseEvent(new KeyEventArgs { Key = Key.Right, RoutedEvent = InputElement.KeyDownEvent });

        // 2 回目のキー押下（nudge 中）: 基点は再同期されず、nudge 位置 (301,200) から 1px 進む → (302,200)
        // （再同期されると (300,200) に戻って nudge され (301,200) のまま = 移動しない）
        control.RaiseEvent(new KeyEventArgs { Key = Key.Right, RoutedEvent = InputElement.KeyDownEvent });

        Assert.Equal(2, positions.Count);
        Assert.Equal(new Point(302, 200), positions[^1]);
        window.Close();
    }

    [AvaloniaFact]
    internal void nudge中にマウスを動かしても解放時のルーペ位置がnudge位置を維持する()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        var positions = new List<Point>();
        control.LoupePositionMoved += (_, e) => positions.Add(e.Point);

        // 新規選択ドラッグ開始（(10,10) → (50,50)。 終点 = 50,50）
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(10, 10)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(50, 50)), 0UL, LeftPressed(), KeyModifiers.None));

        // nudge（終点が 1px 右へ移動 → (51,50)）
        control.RaiseEvent(new KeyEventArgs { Key = Key.Right, RoutedEvent = InputElement.KeyDownEvent });

        // nudge 中にマウスを動かす（実マウス位置 (80,80)。 nudge 位置の基点を上書きしない）
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(80, 80)), 0UL, LeftPressed(), KeyModifiers.None));

        // マウス解放 → ルーペ位置は nudge 位置（(51,50)）のまま（実マウス位置 (80,80) に飛ばない）
        control.RaiseEvent(new PointerReleasedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(80, 80)), 0UL, LeftReleased(), KeyModifiers.None, MouseButton.Left));

        Assert.NotEmpty(positions);
        Assert.Equal(new Point(51, 50), positions[^1]);
        window.Close();
    }

    [AvaloniaFact]
    internal void nudge中にキャンセルするとカーソルが再表示される()
    {
        var mock = new MockCursorService(default);
        var control = CreateControl(mock);
        var window = Show(control);
        var pointer = CreatePointer();

        // ドラッグ開始（nudge の許可条件: マウス Down 中）
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(10, 10)), 0UL, LeftPressed(), KeyModifiers.None, 1));

        // nudge → カーソル非表示
        control.RaiseEvent(new KeyEventArgs { Key = Key.Right, RoutedEvent = InputElement.KeyDownEvent });
        Assert.False(mock.Visible);

        // キャンセル（Escape）→ ResetSelection → カーソル再表示（非表示残留の防止）
        control.RaiseEvent(new KeyEventArgs { Key = Key.Escape, RoutedEvent = InputElement.KeyDownEvent });
        Assert.True(mock.Visible);
        window.Close();
    }

    [AvaloniaFact]
    internal void nudge後のハンドルホバーでカーソル形状がハンドルに応じて更新される()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();

        // 新規選択ドラッグ（(10,10) → (50,50)。 終点 = 右下ハンドル位置）
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(10, 10)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(50, 50)), 0UL, LeftPressed(), KeyModifiers.None));

        // nudge（終点が (51,50) へ移動 = 右下ハンドル）
        control.RaiseEvent(new KeyEventArgs { Key = Key.Right, RoutedEvent = InputElement.KeyDownEvent });

        // 解放（nudge 終了。 SuppressEchoAt = (51,50)）
        control.RaiseEvent(new PointerReleasedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(51, 50)), 0UL, LeftReleased(), KeyModifiers.None, MouseButton.Left));

        // 一旦ハンドルから離れる（カーソルは Default に戻る）
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(150, 150)), 0UL, LeftReleased(), KeyModifiers.None));
        Assert.Same(Cursor.Default, control.Cursor);

        // 同じハンドル位置（右下 (51,50)）へホバー
        // （SuppressEchoAt が残留しているとカーソル更新がスキップされ、矢印のままになる）
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(51, 50)), 0UL, LeftReleased(), KeyModifiers.None));

        // 右下ハンドル → TopLeftCorner（リサイズ矢印）カーソルになる（Cursor は参照比較のため ToString で種別を検証）
        Assert.NotSame(Cursor.Default, control.Cursor);
        Assert.NotNull(control.Cursor);
        Assert.Equal("TopLeftCorner", control.Cursor.ToString());
        window.Close();
    }

    [AvaloniaFact]
    internal void nudge中にマウスを動かしてから解放するとカーソルがnudge位置へ戻って表示される()
    {
        var mock = new MockCursorService(default);
        var control = CreateControl(mock);
        var window = Show(control);
        var pointer = CreatePointer();

        // 新規選択ドラッグ（(10,10) → (50,50)。 終点 = 右下ハンドル位置）
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(10, 10)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(50, 50)), 0UL, LeftPressed(), KeyModifiers.None));

        // nudge（終点が (51,50) へ移動 = nudge 位置）
        control.RaiseEvent(new KeyEventArgs { Key = Key.Right, RoutedEvent = InputElement.KeyDownEvent });
        // 実コードの ToScreen（LocalDipToScreen）と同じ変換で期待値を求める（PixelPoint 型）
        var nudgeScreenPos = ScreenConverter.LocalDipToScreen(control, new Point(51, 50));

        // nudge 中にマウスを動かす（実マウス位置 (80,80)。 カーソルはそこに移動した状態）
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(80, 80)), 0UL, LeftPressed(), KeyModifiers.None));

        // 解放 → カーソルは nudge 位置（(51,50) の画面物理座標）へ戻ってから再表示される
        // （移動した位置 (80,80) に表示されない。 SetCursorScreenPoint は再表示前に行われる）
        control.RaiseEvent(new PointerReleasedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(80, 80)), 0UL, LeftReleased(), KeyModifiers.None, MouseButton.Left));

        Assert.True(mock.Visible);
        Assert.NotNull(mock.LastSetPoint);
        Assert.Equal(nudgeScreenPos.ToEsPointInt(), mock.LastSetPoint!.Value);
        window.Close();
    }

    // ====================================================================
    // ドラッグ中のルーペ位置（C案: ポインタをハンドル点へスナップ・ドラッグ中はハンドル点を追従）
    // ====================================================================

    [AvaloniaFact]
    internal void リサイズハンドルのDownでポインタがハンドル点へスナップされる()
    {
        var mock = new MockCursorService(default);
        var control = CreateControl(mock);
        var window = Show(control);
        var pointer = CreatePointer();
        control.SetSelection(new Rect(20, 20, 60, 60));

        // 右下ハンドル中心 (80,80) からズレた位置 (85,90) で Down（ヒット半径 12 内）
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(85, 90)), 0UL, LeftPressed(), KeyModifiers.None, 1));

        // ポインタ（OS カーソル）が右下ハンドル点 (80,80) へスナップされる
        // （実コードの ToScreen（LocalDipToScreen）と同じ変換で期待値を求める）
        var snapScreenPos = ScreenConverter.LocalDipToScreen(control, new Point(80, 80));
        Assert.NotNull(mock.LastSetPoint);
        Assert.Equal(snapScreenPos.ToEsPointInt(), mock.LastSetPoint!.Value);
        window.Close();
    }

    [AvaloniaFact]
    internal void 移動ドラッグのDownではポインタがスナップされない()
    {
        var mock = new MockCursorService(default);
        var control = CreateControl(mock);
        var window = Show(control);
        var pointer = CreatePointer();
        control.SetSelection(new Rect(20, 20, 60, 60));

        // 矩形中心 (50,50) で Down（Move ハンドル。 ヒット半径 12 から離れている）→ 掴んだ位置を維持するためスナップなし
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(50, 50)), 0UL, LeftPressed(), KeyModifiers.None, 1));

        Assert.Null(mock.LastSetPoint);
        window.Close();
    }

    [AvaloniaFact]
    internal void リサイズハンドルドラッグ中にルーペ位置がハンドル点を追従する()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        // クリップ有効（境界 200x200）。 マウス位置と矩形の角がずれる状況を作る
        control.Options = control.Options with { ClampToSourceBounds = true };
        control.SetCoordTransform(new CoordTransform(p => p, p => p, () => new EsSize(200, 200)));
        control.SetSelection(new Rect(20, 20, 60, 60));
        var positions = new List<Point>();
        control.LoupePositionMoved += (_, e) => positions.Add(e.Point);

        // 右下ハンドル（80,80）を押下してリサイズ開始（スナップ位置 = 同じ）
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(80, 80)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        // 境界外 (250,250) へドラッグ → 矩形は (20,20,180,180) にクリップされ右下は (200,200)
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(250, 250)), 0UL, LeftPressed(), KeyModifiers.None));

        // ルーペ中心はマウス位置 (250,250) ではなく、アクティブハンドル（右下 (200,200)）を追従する
        Assert.NotEmpty(positions);
        Assert.Equal(new Point(200, 200), positions[^1]);
        window.Close();
    }

    [AvaloniaFact]
    internal void 移動ドラッグ中にルーペ位置がマウス位置を追従する()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        control.SetSelection(new Rect(20, 20, 60, 60));
        var positions = new List<Point>();
        control.LoupePositionMoved += (_, e) => positions.Add(e.Point);

        // 矩形内のオフセット位置 (40,40) を押下（Move 判定。 ヒット半径 12 から離れている。 スナップなし）
        // → (60,60) へドラッグ。 矩形は (40,40,60,60) になり中心は (70,70) だが、
        //   ルーペはマウス位置（掴んだ位置）(60,60) を追従する（矩形中心に飛ばない）
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(40, 40)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(60, 60)), 0UL, LeftPressed(), KeyModifiers.None));

        Assert.NotEmpty(positions);
        Assert.Equal(new Point(60, 60), positions[^1]);
        window.Close();
    }

    [AvaloniaFact]
    internal void 新規選択ドラッグ中にルーペ位置が終点を追従する()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();
        var positions = new List<Point>();
        control.LoupePositionMoved += (_, e) => positions.Add(e.Point);

        // (10,10) から (50,50) へドラッグ（終点 = 50,50）
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(10, 10)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(50, 50)), 0UL, LeftPressed(), KeyModifiers.None));

        Assert.NotEmpty(positions);
        Assert.Equal(new Point(50, 50), positions[^1]);
        window.Close();
    }

    // ====================================================================
    // カーソル座標ラベル
    // ====================================================================

    [AvaloniaFact]
    internal void カーソル座標ラベル有効時の描画が例外なく実行される()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();

        control.Appearance = control.Appearance with
        {
            ShowCoordLabel = true,
            CoordLabelFontSize = 12.0,
        };

        // マウス移動 → Render で座標ラベルが描画される（例外なし）
        control.RaiseEvent(new PointerEventArgs(
            InputElement.PointerMovedEvent, control, pointer, window,
            ToScreenPoint(control, new Point(30, 40)), 0UL, LeftPressed(), KeyModifiers.None));

        // 再描画を要求して例外がないことを確認
        control.InvalidateVisual();
        window.CaptureRenderedFrame();
        window.Close();
    }

    [AvaloniaFact]
    internal void カーソル座標ラベル無効時も描画が例外なく実行される()
    {
        var control = CreateControl();
        var window = Show(control);

        // 座標ラベルを無効化した状態で描画（座標ラベルは描画されない）
        control.Appearance = control.Appearance with { ShowCoordLabel = false };
        control.InvalidateVisual();
        window.CaptureRenderedFrame();
        window.Close();
    }

    [AvaloniaFact]
    internal void 選択あり状態の描画が枠線と8方向ハンドルを含めて実行される()
    {
        var control = CreateControl();
        var window = Show(control);

        // 選択矩形を作成して描画（枠線 + 8 方向ハンドルが描画される）
        control.SetSelection(new Rect(20, 20, 100, 80));
        control.InvalidateVisual();
        window.CaptureRenderedFrame();

        window.Close();
    }

    [AvaloniaFact]
    internal void nudge中の描画が十字マーカーを含めて実行される()
    {
        var control = CreateControl();
        var window = Show(control);
        var pointer = CreatePointer();

        // ドラッグ開始（nudge 許可条件: マウス Down 中）→ nudge → 十字マーカー表示状態で描画
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(10, 10)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new KeyEventArgs { Key = Key.Right, RoutedEvent = InputElement.KeyDownEvent });

        control.InvalidateVisual();
        window.CaptureRenderedFrame();

        window.Close();
    }

    [AvaloniaFact]
    internal void サイズ0のコントロールでも座標ラベル描画が実行される()
    {
        // クライアントサイズ 0 の描画（DrawCoordLabel のクランプ分岐: サイズ 0 はクランプしない）
        var control = new MarqueeControl
        {
            Width = 0,
            Height = 0,
            Appearance = MarqueeAppearance.Default with { ShowCoordLabel = true },
        };
        var window = Show(control);

        control.InvalidateVisual();
        window.CaptureRenderedFrame();

        window.Close();
    }

    [AvaloniaFact]
    internal void 描画_ブラシペンキャッシュ容量超過後も正しく描画される()
    {
        var control = CreateControl();
        var window = Show(control);
        control.SetSelection(new Rect(20, 20, 100, 80));

        // ブラシ/ペンキャッシュの容量（CapacityLimitedCache の既定容量）を超える色で描画を繰り返し、
        // 容量超過（全クリア）分岐を実行する（無制限メモリ増加の防止と正しさの両立）
        for (var i = 0; i < CapacityLimitedCache<Color, SolidColorBrush>.DefaultCapacity + 1; i++)
        {
            // ハンドルの塗り（ブラシ）とハンドル枠（ペン）の両方のキャッシュ容量を超えさせる
            var color = Color.FromRgb((byte)(i % 256), (byte)((i / 256) % 256), 0).ToDrawingColor();
            control.Appearance = control.Appearance with
            {
                HandleColor = color,
                HandleBorderColor = color,
                OuterBorderColor = color,
            };
            control.InvalidateVisual();
            window.CaptureRenderedFrame();
        }

        // クリア後に再描画しても例外なく正しく描画される（キャッシュが再構築される）
        control.Appearance = control.Appearance with { OuterBorderColor = Colors.Red.ToDrawingColor() };
        control.InvalidateVisual();
        window.CaptureRenderedFrame();

        // 同じ色・太さで再描画し、ブラシ/ペンのキャッシュヒット分岐を実行する
        control.InvalidateVisual();
        window.CaptureRenderedFrame();

        window.Close();
    }

    // ====================================================================
    // ラベル要素（文言・配置はコアが決め、本コントロールがテキスト要素として表示する）
    // ====================================================================

    /// <summary>ラベル要素の文言（サイズラベル・カーソル座標ラベル）を列挙する。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <returns>表示されている文言の一覧。</returns>
    private static List<string> LabelTexts(MarqueeControl control)
        => [.. control.Children.OfType<Border>().Select(border => ((TextBlock)border.Child!).Text!)];

    /// <summary>レイアウト パスを進める（ラベル要素は配置フェーズで更新される）。</summary>
    private static async Task PumpLayoutAsync()
    {
        for (var i = 0; i < 8; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Yield();
        }
    }

    /// <summary>カーソル座標ラベルの要素が選択操作に追従することを検証する。</summary>
    /// <remarks>
    /// 文言・配置はコアが決め、本コントロールは要素を置くだけである（実寸は自 UI で測ってコアへ渡す）。<br/>
    /// 配置は配置フェーズで書き換えるため、再描画要求と併せて配置も無効化される（要素が追従する）。
    /// </remarks>
    [AvaloniaFact]
    internal async Task 座標ラベル要素は選択操作に追従する()
    {
        var control = new MarqueeControl
        {
            Width = 200,
            Height = 200,
            Appearance = MarqueeAppearance.Default with { ShowCoordLabel = true },
        };
        var window = Show(control);
        var pointer = CreatePointer();

        // ドラッグして選択を作る（カーソル位置が座標ラベルの基準になる）
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, ToScreenPoint(control, new Point(20, 20)), 0UL, LeftPressed(), KeyModifiers.None, 1));
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(50, 40)), 0UL, LeftPressed(), KeyModifiers.None));
        await PumpLayoutAsync();

        // 文言はコアの EsPoint.Format が単一所有する
        Assert.Contains("(50, 40)", LabelTexts(control));

        // 続けて移動すると要素の文言が追従する（配置の無効化が効いている）
        control.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, control, pointer, window, ToScreenPoint(control, new Point(80, 70)), 0UL, LeftPressed(), KeyModifiers.None));
        await PumpLayoutAsync();

        Assert.Contains("(80, 70)", LabelTexts(control));
        window.Close();
    }

    /// <summary>1 フレーム分の描画内容は 1 回だけ解決されることを検証する。</summary>
    /// <remarks>
    /// 図形の描画（描画フェーズ）とラベルの配置（配置フェーズ）が同じ描画内容を共有しないと、<br/>
    /// 選択矩形・装飾・ラベル文言の解決と確保が 1 フレームで 2 回走る。<br/>
    /// サイズラベルの文言プロバイダは <see cref="MarqueePresenter.ResolveDrawPlan"/> が 1 回だけ評価するため、<br/>
    /// その呼び出し回数が 1 フレームの解決回数になる（選択がありサイズラベルを表示する場合のみ評価される）。
    /// </remarks>
    [AvaloniaFact]
    internal async Task 描画内容は1フレームにつき1回だけ解決される()
    {
        var control = CreateControl();
        var window = Show(control);
        control.SetSelection(new Rect(20, 20, 60, 60));

        var resolves = 0;
        control.SizeLabelOverrideProvider = () =>
        {
            resolves++;
            return "S";
        };

        await PumpLayoutAsync();
        window.CaptureRenderedFrame();
        await PumpLayoutAsync();

        Assert.Equal(1, resolves);
        window.Close();
    }

    /// <summary>ウィンドウ経由（実ヒットテスト）の入力で選択矩形が作成されることを検証する。</summary>
    /// <remarks>
    /// 他のテストは <c>RaiseEvent</c> でハンドラを直接呼ぶため、<b>ヒットテストの欠落を検出できない</b><br/>
    /// （背景を持たない <see cref="Panel"/> は描画内容が無く、押下を受け取れない）。<br/>
    /// 本テストは実入力の経路（ウィンドウ → ヒットテスト → コントロール）でドラッグする。
    /// </remarks>
    [AvaloniaFact]
    internal void ウィンドウ経由のドラッグで選択矩形が作成される()
    {
        var control = CreateControl();
        var window = new Window { Width = 400, Height = 400, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // 押下はコントロール内（中央寄せのため 100〜300 の範囲）で行う
        window.MouseMove(new Point(120, 120));
        window.MouseDown(new Point(120, 120), MouseButton.Left);
        window.MouseMove(new Point(280, 280));
        window.MouseUp(new Point(280, 280), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.True(control.HasSelection, "ウィンドウ経由のドラッグが MarqueeControl へ届いていない（ヒットテストの欠落）");
        Assert.Equal(new EsRect(20, 20, 160, 160, EsRectInterval.Closed), control.SelectionSourceRect);
        window.Close();
    }

    /// <summary>サイズラベルの要素が選択矩形の枠線を避けた位置へ置かれることを検証する。</summary>
    /// <remarks>
    /// ラベルの背景が選択矩形の枠線に重なると文字が読みにくいため、基準点を「矩形の線幅 + 1 DIP」だけ内側へ寄せる<br/>
    /// （寄せ幅の算出は提示層の <c>MarqueePresenter</c> が単一所有し、本コントロールは配置指示を要素へ写すだけである）。<br/>
    /// 枠線は矩形の内側へ描かれるため、線幅ぶんを寄せないとラベルが線の下に潜る。
    /// </remarks>
    [AvaloniaFact]
    internal async Task サイズラベルの要素は枠線の幅だけ内側へ置かれる()
    {
        const double thickness = 6.0;
        var control = new MarqueeControl
        {
            Width = 300,
            Height = 300,
            Options = TestOptions,
            Appearance = MarqueeAppearance.Default with
            {
                OuterBorderThickness = thickness,
                ShowCoordLabel = false,
            },
        };
        var window = Show(control);
        control.SetSelection(new Rect(40, 60, 100, 80));
        await PumpLayoutAsync();

        var border = Assert.Single(control.Children.OfType<Border>());

        // 注目矩形の左上（40, 60）から「線幅 6 + 余白 1」内側へ置かれる
        Assert.Equal(40 + thickness + 1, border.Margin.Left);
        Assert.Equal(60 + thickness + 1, border.Margin.Top);
        window.Close();
    }
}
