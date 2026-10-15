using EsUtil.Feature.Marquee.WinUI3.Internal;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using WinUIPoint = Windows.Foundation.Point;

namespace EsUtil.Feature.Marquee.WinUI3;

/// <summary>MarqueeControl の入力処理（partial 分割）。</summary>
/// <remarks>
/// ポインターとキーの入力をコア型（<see cref="EsPoint"/> / <see cref="EsKey"/>）へ変換してコア（ファサード）へ通知し、<br/>
/// 返された効果（再描画・カーソル操作）を適用するだけである。<br/>
/// ドラッグの適用・nudge の許可条件・ダブルクリックの判断（<b>クリック数の数え上げは本層</b>）・カーソル形状の決定は<br/>
/// ファサード（<see cref="MarqueePresenter"/>）とコアが単一所有する。<br/>
/// 矢印キーの nudge はフォーカスを必要とする（本コントロールにフォーカスがある間だけキーが届く）。
/// </remarks>
public sealed partial class MarqueeControl
{
    /// <summary>直前の押下位置（ローカル DIP）。 クリック数の判定にだけ使う。</summary>
    private EsPoint _lastPressPosition;

    /// <summary>直前の押下の時刻（<see cref="Environment.TickCount64"/>）。 クリック数の判定にだけ使う。</summary>
    private long _lastPressMilliseconds;

    /// <summary>直前の押下を記録済みかどうか（初回の押下をダブルクリックと見なさないための前提）。</summary>
    private bool _hasPreviousPress;

    /// <summary>ドラッグ中のポインター（キャプチャの取得に使う）。 未ドラッグの場合は null。</summary>
    private Pointer? _activePointer;

    /// <summary>ポインターが本コントロールの上にあるかどうか（追従位置の取り直しの条件）。</summary>
    private bool _isPointerOver;

    /// <summary>入力のイベントを購読する（初期化の一部）。</summary>
    /// <remarks>
    /// WinUI 3 のポインターとキーの入力は <c>UIElement</c> のイベントであり、<c>protected override</c> の入口を持たない<br/>
    /// （他の UI 版の <c>OnMouseDown</c> / <c>OnKeyDown</c> に相当するものを本クラスが購読する）。<br/>
    /// ポインターの進入・退出も購読する（<c>UIElement</c> は「ポインターが上にあるか」を公開しないため、<br/>
    /// 追従位置の取り直しの条件を本クラスが保持する）。<br/>
    /// 購読は解除してから登録する（同じインスタンスで初期化が繰り返されても二重購読しない）。
    /// </remarks>
    private void SubscribeInputEvents()
    {
        PointerPressed -= OnPointerPressed;
        PointerPressed += OnPointerPressed;
        PointerMoved -= OnPointerMoved;
        PointerMoved += OnPointerMoved;
        PointerReleased -= OnPointerReleased;
        PointerReleased += OnPointerReleased;
        PointerEntered -= OnPointerEntered;
        PointerEntered += OnPointerEntered;
        PointerExited -= OnPointerExited;
        PointerExited += OnPointerExited;
        KeyDown -= OnKeyDown;
        KeyDown += OnKeyDown;
        LostFocus -= OnLostFocus;
        LostFocus += OnLostFocus;
    }

    /// <summary>ポインターが本コントロールへ入ったことを記録する。</summary>
    /// <param name="sender">イベント送信元（本コントロール）。</param>
    /// <param name="e">ポインターの情報。</param>
    private void OnPointerEntered(object sender, PointerRoutedEventArgs e) => _isPointerOver = true;

    /// <summary>ポインターが本コントロールから出たことを記録する。</summary>
    /// <param name="sender">イベント送信元（本コントロール）。</param>
    /// <param name="e">ポインターの情報。</param>
    private void OnPointerExited(object sender, PointerRoutedEventArgs e) => _isPointerOver = false;

    /// <summary>左ボタン押下時にドラッグを開始する。</summary>
    /// <param name="sender">イベント送信元（本コントロール）。</param>
    /// <param name="e">ポインターの情報。</param>
    /// <remarks>
    /// 押下位置を対象座標へ変換してコア（ファサード）へ通知する（ダブルクリックの確定/キャンセル・nudge の終了・<br/>
    /// ハンドルのスナップはコアの判断に従う）。<br/>
    /// ドラッグ中はポインターがコントロール外へ出ても移動イベントを受け取れるよう、<br/>
    /// ポインターを記録してからコアへ通知する（キャプチャの取得はコアからの要求で行う）。
    /// </remarks>
    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        e.Handled = true;
        _activePointer = e.Pointer;
        _marquee.Press(ToSource(point.Position), ResolveClickCount(point.Position.ToEsPoint()));
    }

    /// <summary>ドラッグ中のポインター移動を処理する。</summary>
    /// <param name="sender">イベント送信元（本コントロール）。</param>
    /// <param name="e">ポインターの情報。</param>
    /// <remarks>
    /// 位置を対象座標へ変換してコア（ファサード）へ通知する。<br/>
    /// ドラッグの適用・nudge 中のエコー移動の抑制・カーソル形状の決定はコアの判断に従う。
    /// </remarks>
    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        => _marquee.Move(ToSource(e.GetCurrentPoint(this).Position));

    /// <summary>左ボタン解放時にドラッグを終了する。</summary>
    /// <param name="sender">イベント送信元（本コントロール）。</param>
    /// <param name="e">ポインターの情報。</param>
    /// <remarks>
    /// 解放位置を対象座標へ変換してコア（ファサード）へ通知する（ポインター キャプチャの解放もコアからの要求で行う）。<br/>
    /// 最小サイズによるキャンセル・確定・クリック検出・nudge の終了はコアの判断に従う。
    /// </remarks>
    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        e.Handled = true;
        _marquee.Release(ToSource(e.GetCurrentPoint(this).Position));
    }

    /// <summary>キーダウン処理（矢印キーの nudge・Escape のキャンセル・Enter の確定）。</summary>
    /// <param name="sender">イベント送信元（本コントロール）。</param>
    /// <param name="e">キーイベント。</param>
    /// <remarks>
    /// キーをコア型（<see cref="EsKey"/>）へ変換して通知し、返された効果（再描画・カーソル操作）を適用するだけである。<br/>
    /// 矢印キー nudge・Escape・Enter の判断（nudge の許可条件を含む）はコアが行う。<br/>
    /// <c>|| e.Handled</c> は、先に誰かが処理済みにしたフラグを false へ戻さないため。<br/>
    /// 本コントロールにフォーカスがある場合のみ呼ばれる（他の UI 版のフォーカス非依存 nudge とは異なる）。
    /// </remarks>
    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        => e.Handled = _marquee.KeyDown(e.Key.ToEsKey(), CurrentCursorSourcePosition()) || e.Handled;

    /// <summary>フォーカスを失ったときに nudge を終了する。</summary>
    /// <param name="sender">イベント送信元（本コントロール）。</param>
    /// <param name="e">フォーカス イベント。</param>
    /// <remarks>
    /// nudge 中はカーソルを非表示にするため、nudge を継続できない文脈（フォーカス喪失）では終了して再表示する<br/>
    /// （非表示残留の防止。 判断はコアが単一所有する）。
    /// </remarks>
    private void OnLostFocus(object sender, RoutedEventArgs e) => _marquee.EndNudge();

    /// <summary>直前の押下からの時間と距離から、今回の押下のクリック数を決める。</summary>
    /// <param name="localDip">今回の押下位置（ローカル DIP）。</param>
    /// <returns>1（単独の押下）または 2（ダブルクリック）。</returns>
    /// <remarks>
    /// WinUI 3 のポインター イベントはクリック数を報告しないため本層が数え上げる（判断の材料は本層、<br/>
    /// ダブルクリックの意味付けはコア）。<br/>
    /// ダブルクリックと判定した後は記録を消す（3 回目の押下をさらにダブルクリックとして扱わない）。<br/>
    /// 時間の上限は OS の設定値を使う（<see cref="Win32Interop.DoubleClickIntervalMilliseconds"/>）。
    /// </remarks>
    private int ResolveClickCount(EsPoint localDip)
    {
        var now = Environment.TickCount64;
        var isDoubleClick = _hasPreviousPress
            && DoubleClickDetector.IsDoubleClick(
                _lastPressPosition,
                localDip,
                now - _lastPressMilliseconds,
                Win32Interop.DoubleClickIntervalMilliseconds());

        _hasPreviousPress = !isDoubleClick;
        _lastPressPosition = localDip;
        _lastPressMilliseconds = now;

        return isDoubleClick ? DoubleClickDetector.DoubleClickCount : DoubleClickDetector.SingleClickCount;
    }

    /// <summary>現在のカーソル位置（対象座標）を取得する。</summary>
    /// <returns>カーソル位置（対象座標）。 ウィンドウを特定できない場合と OS から位置を取得できない場合は null。</returns>
    /// <remarks>
    /// <b>nudge の基点</b>（ポインター移動が未発生でも現在位置から微調整を開始でき、原点へ飛ばない）と、<br/>
    /// <b>追従位置の取り直し</b>（<see cref="TryGetCurrentLoupePosition"/>。 入力へ積まれた分だけ古いイベント座標を実カーソルで置き換える）に使う。<br/>
    /// 画面物理座標 → ウィンドウのクライアント座標 → 対象座標の順に変換する（境界の 1 行変換）。<br/>
    /// 位置を取得できない場合は基点を記録しない（失敗を原点で表さないため <c>Try</c> 系と null を使う）。
    /// </remarks>
    private EsPoint? CurrentCursorSourcePosition()
    {
        if (!_cursor.TryGetCursorScreenPoint(out var screenPoint))
        {
            return null;
        }

        return ScreenConverter.TryScreenToLocalDip(this, screenPoint, out var localDip)
            ? _marquee.ToSource(localDip)
            : null;
    }

    /// <summary>対象座標を画面物理座標へ変換する。</summary>
    /// <param name="sourcePosition">対象座標。</param>
    /// <returns>画面物理座標。 ウィンドウを特定できない場合は等価変換（ローカル DIP を丸めた座標）。</returns>
    /// <remarks>
    /// カーソルの移動（nudge）に使用する。 変換できない場合は原点へカーソルが飛ぶため、呼び出し側は nudge 中のウィンドウ表示を前提とする。
    /// </remarks>
    private EsPointInt ToScreen(EsPoint sourcePosition)
        => ScreenConverter.LocalDipToScreen(this, _marquee.ToDip(sourcePosition));

    /// <summary>ローカル DIP 座標を対象座標へ変換する。</summary>
    /// <param name="localDip">ローカル DIP 座標。</param>
    /// <returns>対象座標。</returns>
    private EsPoint ToSource(WinUIPoint localDip) => _marquee.ToSource(localDip.ToEsPoint());

    /// <summary>カーソル形状をコアが示す種別に応じて更新する。</summary>
    /// <param name="kind">カーソル形状の種別（コア提供）。</param>
    /// <remarks>種別と自 UI のカーソルの対応のみを持つ（どのハンドルが同じカーソルになるかはコアが判断済み）。</remarks>
    private void ApplyCursorShape(MarqueeCursorKind kind)
        => ProtectedCursor = kind switch
        {
            MarqueeCursorKind.DiagonalNWSE => InputSystemCursor.Create(InputSystemCursorShape.SizeNorthwestSoutheast),
            MarqueeCursorKind.DiagonalNESW => InputSystemCursor.Create(InputSystemCursorShape.SizeNortheastSouthwest),
            MarqueeCursorKind.Vertical => InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth),
            MarqueeCursorKind.Horizontal => InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast),
            MarqueeCursorKind.Move => InputSystemCursor.Create(InputSystemCursorShape.SizeAll),
            _ => null,
        };

    /// <summary>ポインター キャプチャを取得または解放する。</summary>
    /// <param name="capture">true = 取得 / false = 解放。</param>
    /// <remarks>
    /// ドラッグ中にポインターがコントロール外へ出ても移動イベントを受け取れるようにする。<br/>
    /// ポインターは押下時に記録したものを使う（解放は本コントロールが持つ全キャプチャを対象にするため引数を要さない）。
    /// </remarks>
    private void ApplyPointerCapture(bool capture)
    {
        if (capture)
        {
            if (_activePointer is { } pointer)
            {
                CapturePointer(pointer);
            }

            return;
        }

        ReleasePointerCaptures();
    }
}
