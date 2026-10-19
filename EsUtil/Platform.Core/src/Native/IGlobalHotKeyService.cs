using System.ComponentModel;

namespace EsUtil.Platform.Core.Native;

/// <summary>グローバル ホットキーの登録・解除と押下の配信を抽象化するインターフェイス。</summary>
/// <remarks>
/// OS のグローバル ホットキー（他アプリが前面にあっても届くキー）を登録し、押下時に対応するアクションを配信する。<br/>
/// 実装は各プラットフォーム（Windows: <c>RegisterHotKey</c>）が提供する。<br/>
/// 具象実装ではなく本インターフェイスへ依存させることで、テスト時のモックが可能になる（DIP）。<br/>
/// <br/>
/// <b>失敗の表し方</b>: 失敗の種類で 2 つに分ける（[`.github/例外処理規約.md`](../../../../.github/例外処理規約.md) §2・§3）。<br/>
/// - <b>他のアプリとの競合（正常な入力の変種）</b>: <see cref="RegisterAll"/> が<b>失敗した登録の一覧</b>を返す<br/>
///   （他のアプリが同じ組み合わせを使用している場合にも起こり、そのホットキーが機能しないだけ。 アプリは継続する）。<br/>
/// - <b>OS 操作の失敗（回復できない）</b>: <see cref="Initialize"/> が <see cref="Win32Exception"/> を投げる<br/>
///   （ウィンドウ プロシージャを差し替えられないと、ホットキーを登録できても押下を検出できない）。<br/>
/// <b>本層はログを出力しない</b>: 失敗の記録は呼び出し側が行う。
/// </remarks>
/// <typeparam name="T">ホットキーに割り当てるアクションの識別子。</typeparam>
public interface IGlobalHotKeyService<T> : IDisposable where T : notnull
{
    /// <summary>ホットキーの受信を開始する（対象ウィンドウのメッセージを受信できるようにする）。</summary>
    /// <param name="hWnd">ホットキーの通知先ウィンドウのネイティブハンドル。</param>
    /// <exception cref="Win32Exception">ウィンドウ プロシージャの差し替えに失敗した場合。</exception>
    /// <remarks>
    /// <b>ウィンドウ表示後に呼ぶこと</b>: OS はウィンドウ生成時にウィンドウ プロシージャを確定するため、
    /// 生成前に差し替えても上書きされる。<br/>
    /// ハンドルが未設定（既定値）の場合は何もしない。
    /// </remarks>
    void Initialize(IntPtr hWnd);

    /// <summary>押下されたときに配信するアクションを購読する。</summary>
    /// <param name="onPressed">押下時に呼び出す処理。</param>
    /// <returns>購読を解除するハンドル。</returns>
    IDisposable Subscribe(Action<T> onPressed);

    /// <summary>ホットキーをまとめて登録し直す（既存の登録はすべて解除する）。</summary>
    /// <param name="registrations">登録するアクションと設定の組。</param>
    /// <returns>登録できなかった組（他のアプリとの競合等）。 すべて登録できた場合は空。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="registrations"/> が null の場合。</exception>
    /// <remarks>
    /// <b>失敗を一覧で返す理由</b>: 他のアプリが同じ組み合わせを使用している場合にも起こり、
    /// <b>そのホットキーが機能しないだけ</b>でアプリは継続できる（1 件の競合で起動を止めない）。<br/>
    /// 失敗したアクションと設定を返すため、呼び出し側は利用者へ通知するか記録するかを選べる。
    /// </remarks>
    IReadOnlyList<(T Action, HotKeySetting Setting)> RegisterAll(
        IReadOnlyList<(T Action, HotKeySetting Setting)> registrations);

    /// <summary>登録されているホットキーをすべて解除する。</summary>
    /// <remarks>
    /// <b>失敗を報告しない理由</b>: 呼び出し元（再登録・<see cref="IDisposable.Dispose"/>）に回復手段が無く、
    /// 解除できなかった登録はプロセスの終了で OS が解放する（最後の手段の後始末であるため）。
    /// </remarks>
    void UnregisterAll();
}
