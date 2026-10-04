namespace EsUtil.Platform.Core.Native;

/// <summary>グローバルホットキーの登録・解除・管理を抽象化する。</summary>
/// <remarks>
/// アプリが非アクティブな状態でも機能するシステムワイドなホットキーを管理する。<br/>
/// 実装は各プラットフォーム（Windows: RegisterHotKey / UnregisterHotKey Win32 API と WndProc サブクラス化）が提供する。<br/>
/// ホットキーとアクション（<typeparamref name="T"/>）の対応はジェネリック型パラメータで保持し、<br/>
/// アプリケーション固有の型への依存を持たない（DIP）。<br/>
/// ウィンドウはネイティブハンドル（<see cref="IntPtr"/>）で受け取るため、UI フレームワークに依存しない。<br/>
/// </remarks>
/// <typeparam name="T">ホットキーに割り当てるアクションの識別子（アプリが定義する列挙型等）。</typeparam>
public interface IGlobalHotKeyService<T> : IDisposable where T : notnull
{
    /// <summary>初期化する（ウィンドウハンドルを設定し、WM_HOTKEY 受信のためのフックを設定する）。</summary>
    /// <param name="hWnd">ホットキー通知を受け取るウィンドウのネイティブハンドル。</param>
    void Initialize(IntPtr hWnd);

    /// <summary>ホットキーとアクションの対応を登録する。</summary>
    /// <param name="registrations">ホットキー設定とアクションの対応一覧。</param>
    void RegisterAll(IReadOnlyList<(T Action, HotKeySetting Setting)> registrations);

    /// <summary>全ホットキーを解除する。</summary>
    void UnregisterAll();

    /// <summary>ホットキーが押下されたときに発生する。</summary>
    /// <remarks>
    /// WM_HOTKEY メッセージを受信した実装が、対応するアクションを配信する。<br/>
    /// <b>素のデリゲート（<see cref="Action{T}"/>）で公開する</b>: 本層（最下層）は UI 非依存であり、
    /// <see cref="System.EventHandler{T}"/> の規約（sender と <see cref="EventArgs"/>）を強制しない。
    /// </remarks>
    event Action<T>? HotKeyPressed;
}
