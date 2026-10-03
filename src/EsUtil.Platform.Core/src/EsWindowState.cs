namespace EsUtil.Platform.Core;

/// <summary>ウィンドウの表示状態（UI フレームワーク非依存）。</summary>
/// <remarks>
/// 各 UI フレームワークのウィンドウ状態（例: Avalonia の <c>WindowState</c>）と同名のメンバーを持ち、<br/>
/// 設定ファイルの文字列表現（<c>JsonStringEnumConverter</c>）の互換性を保つ。<br/>
/// 相互変換は各 UI 実装の Interop 層が提供する（本プロジェクトは UI 型を参照しない）。<br/>
/// </remarks>
public enum EsWindowState
{
    /// <summary>通常表示。</summary>
    Normal,

    /// <summary>最小化。</summary>
    Minimized,

    /// <summary>最大化。</summary>
    Maximized,

    /// <summary>全画面表示。</summary>
    FullScreen,
}
