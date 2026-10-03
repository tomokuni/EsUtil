namespace EsUtil.Platform.Core.Native;

/// <summary>ホストウィンドウの「カーソル上書き抑止」を抽象化するインターフェイス。</summary>
/// <remarks>
/// 選択操作（Crop モード等）中に OS がカーソル形状を上書きするのを防ぎ、選択操作のカーソル形状を維持する。<br/>
/// 対象ウィンドウは UI フレームワーク非依存の <see cref="EsPlatformHandle"/> で受け取るため、<br/>
/// UI 側はウィンドウオブジェクトからハンドルを取得して渡す（Interop 層の拡張メソッドが仲介する）。<br/>
/// 実装は各プラットフォーム（Windows: WndProc サブクラス化）が提供する。
/// </remarks>
public interface ICursorOverrideGuard : IDisposable
{
    /// <summary>抑止がアタッチ済みかどうか。</summary>
    bool IsAttached { get; }

    /// <summary>指定したウィンドウにカーソル上書き抑止をアタッチする。</summary>
    /// <param name="handle">対象ウィンドウのネイティブハンドル。</param>
    /// <remarks>
    /// 抑止を実現できない環境（デザインモード・ハンドル未取得等）では何もせず、<see cref="IsAttached"/> は false のままとなる。<br/>
    /// アタッチ中は、OS によるカーソル形状の上書き（WM_SETCURSOR）を抑止し、現在のカーソル形状を維持する。
    /// </remarks>
    void Attach(EsPlatformHandle handle);
}
