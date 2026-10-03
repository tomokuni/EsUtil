using Avalonia.Controls;
using Avalonia.Input;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Interop;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Coord;

namespace EsUtil.Feature.Marquee.AvaloniaUI;

/// <summary>
/// MarqueeControl のキーボード入力処理（partial 分割）。
/// </summary>
/// <remarks>
/// キー入力をコア型（<see cref="EsKey"/>）へ変換してコア（ファサード）へ通知し、返された効果を適用するだけである。<br/>
/// 矢印キー nudge・Escape のキャンセル・Enter の確定の判断（許可条件を含む）はコアが行う。<br/>
/// キーボード（矢印キー nudge）はフォーカス非依存（TopLevel のキー購読）のため、コントロールにフォーカスがなくても動作する。
/// </remarks>
public sealed partial class MarqueeControl
{
    // ====================================================================
    // キーボード（矢印キー nudge・Escape/Enter の確定/キャンセル）
    // ====================================================================

    /// <summary>キーダウン処理（フォーカス非依存）。 コアへ通知し、返された効果を適用する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">キーイベント。</param>
    /// <remarks>
    /// キーをコア型（<see cref="EsKey"/>）へ変換して通知し、返された効果（再描画・カーソル操作）を適用するだけである。<br/>
    /// 矢印キー nudge・Escape のキャンセル・Enter の確定の判断（許可条件を含む）は提示層（<see cref="MarqueePresenter"/>）が行う。<br/>
    /// コントロール自身のキーハンドラを持たず、本購読だけがキーを受け取る（キーの配送を 1 経路にして二重処理を防ぐ）。<br/>
    /// トンネルで購読するためルートから下降する順に届き、祖先（祖先コンテナ等）のバブリング処理より先に処理済みにできる。<br/>
    /// <c>|| e.Handled</c> は、先に誰かが処理済みにしたフラグを false へ戻さないため。
    /// </remarks>
    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = _marquee.KeyDown(e.Key.ToEsKey(), CurrentCursorSourcePosition()) || e.Handled;
    }

    /// <summary>フォーカスを失ったときに nudge を終了する。</summary>
    /// <param name="e">フォーカスイベント。</param>
    /// <remarks>
    /// nudge 中はカーソルを非表示にするため、nudge を継続できない文脈（フォーカス喪失）では終了して再表示する
    /// （非表示残留の防止。 判断は提示層（<see cref="MarqueePresenter"/>）が単一所有する）。<br/>
    /// 本コントロールは矢印キーを TopLevel から購読するため、フォーカス移動後もキーを受け取り続ける。
    /// 「カーソルを隠したまま操作対象を離れる」状態を作らないため、MewUI 版と同じ契機で終了する。<br/>
    /// ウィンドウの非アクティブ化（Alt+Tab 等）はビジュアルツリーの接続時の購読（<c>Deactivated</c>）が扱う。
    /// </remarks>
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _marquee.EndNudge();
    }

    /// <summary>現在のカーソル位置（対象座標）を取得する。</summary>
    /// <returns>カーソル位置（対象座標）。 TopLevel を特定できない場合は null。</returns>
    /// <remarks>
    /// nudge の基点精度を上げるために記録する（マウス移動が未発生でも現在位置から微調整を開始でき、原点へ飛ばない）。<br/>
    /// 画面物理座標 → ビジュアルの DIP → 対象座標の順に変換する（境界の 1 行変換）。
    /// </remarks>
    private EsPoint? CurrentCursorSourcePosition()
    {
        if (TopLevel.GetTopLevel(this) is null)
        {
            return null;
        }

        var screenPoint = _cursor.GetCursorScreenPoint();
        var localDip = ScreenConverter.ScreenToLocalDip(this, screenPoint.ToAvaloniaPixelPoint()).ToEsPoint();
        return _marquee.ToSource(localDip);
    }
}
