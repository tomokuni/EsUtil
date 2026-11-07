namespace EsUtil.Platform.Core.Native;

/// <summary>クリップボードの内容の読み書きを抽象化するインターフェイス。</summary>
/// <remarks>
/// <b>開閉の順序を実装の内側に閉じる</b>: 開く・ロックする・解放する・閉じるの順序は OS の作法であり、1 つでも抜けると
/// クリップボードが他のアプリから使えなくなる。 実装の内側で必ず解放し、呼び出し側には「読めたか／書けたか」だけを返す。<br/>
/// <b>失敗を例外にしない</b>: 他のアプリが使用中で開けない場合・内容が無い場合は <see langword="false"/> を返す
/// （選択操作や監視は続行できる）。<br/>
/// 具象実装ではなく本インターフェイスへ依存させることで、テスト時の差し替えが可能になる（DIP）。
/// </remarks>
public interface IClipboardService
{
    /// <summary>クリップボードの変更を表すシーケンス番号を取得する。</summary>
    /// <value>変更のたびに増える番号（同じ値なら内容は変わっていない）。</value>
    /// <remarks>クリップボードを開かずに取得できるため、他のアプリが使用中でも監視を続けられる。</remarks>
    uint SequenceNumber { get; }

    /// <summary>クリップボードの DIB（デバイス非依存ビットマップ）を読み、処理へ渡す。</summary>
    /// <param name="format">クリップボードの形式（<c>CF_DIB</c> は 8）。</param>
    /// <param name="read">DIB の先頭を指すポインターを受け取る処理。 戻るまでメモリはロックされたままである。</param>
    /// <returns>読めた場合は <see langword="true"/>。 開けない・内容が無い・ロックできない場合は <see langword="false"/>。</returns>
    /// <remarks>ポインターを返さない（返すと解放を呼び出し側が忘れやすい）。 処理の中で使い切らせる。</remarks>
    bool TryReadDib(uint format, Action<IntPtr> read);

    /// <summary>クリップボードのテキストを読む。</summary>
    /// <param name="text">読んだテキスト。 読めなかった場合は <see langword="null"/>。</param>
    /// <returns>読めた場合は <see langword="true"/>。</returns>
    bool TryReadText(out string? text);

    /// <summary>クリップボードへテキストを書く。</summary>
    /// <param name="text">書くテキスト。 null 不可。</param>
    /// <returns>書けた場合は <see langword="true"/>。 開けない場合は <see langword="false"/>。</returns>
    bool TryWriteText(string text);
}
