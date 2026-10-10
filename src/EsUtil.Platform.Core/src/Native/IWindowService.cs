using System.ComponentModel;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Platform.Core.Native;

/// <summary>OS 依存のネイティブウィンドウ操作を抽象化するインターフェイス。</summary>
/// <remarks>
/// オーバーレイウィンドウに必要な最小限のネイティブ操作（Alt+Tab 非表示・キャプチャ除外・配置・退避）を定義する。<br/>
/// ウィンドウの特定は UI フレームワーク非依存の <see cref="EsPlatformHandle"/> で行う。<br/>
/// 実装は各プラットフォーム（Windows: Win32 API）が提供する。<br/>
/// 具象実装ではなく本インターフェイスへ依存させることで、テスト時のモックが可能になる（DIP）。<br/>
/// <br/>
/// <b>失敗の表し方</b>: 失敗の種類で 2 つに分ける（[`.github/例外処理規約.md`](../../../../.github/例外処理規約.md) §2・§3）。<br/>
/// - <b>能力の有無（正常な入力の変種）</b>: <see cref="SetCaptureExclusion"/> の <see langword="bool"/> 戻り値で返す<br/>
///   （Windows 10 2004 未満では OS が対応しない。 撮影自体は続行できる）。<br/>
/// - <b>OS 操作の失敗（回復できない）</b>: <see cref="Win32Exception"/> を投げる<br/>
///   （<see cref="SetBounds"/> 等。 呼び出し側が結果を判断できないため、投げて原因を残す）。<br/>
/// <b>本層はログを出力しない</b>: 失敗の記録は呼び出し側が行う。
/// </remarks>
public interface IWindowService
{
    /// <summary>ウィンドウをタスクバー / Alt+Tab に表示しないよう設定する（選択オーバーレイ用、仕様 2.2）。</summary>
    /// <param name="handle">対象ウィンドウの識別子。</param>
    /// <exception cref="Win32Exception">拡張スタイルの取得または設定に失敗した場合。</exception>
    /// <remarks>
    /// WS_EX_TOOLWINDOW を拡張スタイルに追加し、Alt+Tab の切り替え候補から除外する。<br/>
    /// <b>現在のスタイルを取得できない場合は例外にする</b>（取得できない状態で書き換えると、既存の
    /// 拡張スタイルのビットを失いウィンドウの振る舞いを壊すため、書き換えずに失敗として通知する）。
    /// </remarks>
    void HideFromTaskSwitcher(EsPlatformHandle handle);

    /// <summary>ウィンドウをキャプチャから除外する/除外を解除する（仕様 6.2.1）。</summary>
    /// <param name="handle">対象ウィンドウの識別子。</param>
    /// <param name="exclude">キャプチャ除外する場合は <see langword="true"/>。</param>
    /// <returns>設定できた場合は <see langword="true"/>。 OS が対応しない、または設定できなかった場合は <see langword="false"/>。</returns>
    /// <remarks>
    /// WDA_EXCLUDEFROMCAPTURE により、そのウィンドウは画面キャプチャ（BitBlt / DXGI 等）に
    /// 一切写らなくなる（フレームタイミングに依存しない決定的な除外）。<br/>
    /// 除外設定時のみ DWM の再合成待ち（<c>DwmFlush</c>）を行い、設定直後のキャプチャを防ぐ。<br/>
    /// Windows 10 2004（build 19041）以降で利用可能。 それ以前では失敗（false）を返す。<br/>
    /// <b>例外にしない理由</b>: 未対応 OS では<b>写り込むだけで撮影は成立する</b>ため、撮影を中止させるべきではない
    /// （対応の有無は呼び出し側が判断できる）。
    /// </remarks>
    bool SetCaptureExclusion(EsPlatformHandle handle, bool exclude);

    /// <summary>ウィンドウの位置とサイズを画面物理ピクセルで指定する（仕様 2.3）。</summary>
    /// <param name="handle">対象ウィンドウの識別子。</param>
    /// <param name="bounds">設定する矩形（画面物理ピクセル。 負の座標可）。</param>
    /// <exception cref="Win32Exception">位置とサイズを設定できなかった場合。</exception>
    /// <remarks>
    /// SetWindowPos により、OS の作業領域補正を受けずに物理ピクセルで配置する。<br/>
    /// Z オーダーとアクティブ状態は変更しない（<c>SWP_NOZORDER</c> / <c>SWP_NOACTIVATE</c>）。<br/>
    /// 識別子が未設定（既定値）の場合は何もしない（ヘッドレステスト等）。<br/>
    /// <b>失敗を値で返さない理由</b>: 配置できない状態（オーバーレイが意図しない位置に残る）は
    /// 呼び出し側が回復できないため、例外にして原因（Win32 のエラー コード）を残す。
    /// </remarks>
    void SetBounds(EsPlatformHandle handle, EsRectInt bounds);

    /// <summary>ウィンドウを最前面解除し、非表示にして閉じる（UI ハング時の退避、仕様 3.6）。</summary>
    /// <param name="handle">対象ウィンドウの識別子。</param>
    /// <remarks>
    /// UI スレッドがハングした場合にバックグラウンドスレッドから呼び出され、<br/>
    /// タスクマネージャー等でプロセスを終了できるようにする。<br/>
    /// 1. Topmost を解除（タスクマネージャー等が前面に出せるようにする）<br/>
    /// 2. ウィンドウを非表示にする（全画面オーバーレイが邪魔しないようにする）<br/>
    /// 3. WM_CLOSE を送信（UI スレッドが復帰した場合に自動キャンセル処理が走る）<br/>
    /// Win32 API はスレッド非依存のため、バックグラウンドスレッドから呼び出し可能。<br/>
    /// 本メソッドは <see cref="EsPlatformHandle"/>（数値）のみを使用し、UI オブジェクトに触れない。<br/>
    /// <b>例外にしない理由</b>: 本操作は<b>最後の手段（ベストエフォート）</b>であり、呼び出し元（ウォッチドッグ）に
    /// 代替手段が無い（失敗しても試せる操作が残っていない）。 各手順は独立に実行し、1 つが失敗しても残りを続行する。
    /// </remarks>
    void EvacuateWindow(EsPlatformHandle handle);
}
