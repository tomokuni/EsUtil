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

    /// <summary>ホットキーの押下を購読する。</summary>
    /// <param name="onPressed">押下されたホットキーに対応するアクションを受け取る処理。</param>
    /// <returns>購読を解除する <see cref="IDisposable"/>。</returns>
    /// <remarks>
    /// WM_HOTKEY メッセージを受信した実装が、対応するアクションを配信する。<br/>
    /// <b><c>event</c> を使わない理由</b>: 配信するのはホットキーに割り当てた識別子（<typeparamref name="T"/>）であり、
    /// これを <see cref="EventArgs"/> 派生のクラスへ包むと本層（UI 非依存）の公開面が増えるため。
    /// </remarks>
    IDisposable Subscribe(Action<T> onPressed);
}
