namespace EsUtil.Platform.Core;

/// <summary>キャプチャ種別（UI フレームワーク非依存）。</summary>
/// <remarks>
/// キャプチャの開始方法（全画面 / ウィンドウ選択 / 範囲選択）を表す。<br/>
/// ホスト（app 等）は本 enum をグローバルホットキーのアクション識別子（<c>IGlobalHotKeyService&lt;CaptureType&gt;</c>）としても使用する。<br/>
/// UI 実装のキャプチャ UI ライブラリが共通で参照するため、UI 非依存の共通基盤に置く。
/// </remarks>
public enum CaptureType
{
    /// <summary>全画面（ScreenSelect）。</summary>
    ScreenSelect,

    /// <summary>ウィンドウ選択（WindowSelect）。</summary>
    WindowSelect,

    /// <summary>範囲選択（AreaSelect）。</summary>
    AreaSelect,
}

