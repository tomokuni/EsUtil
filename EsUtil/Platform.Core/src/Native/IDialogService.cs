namespace EsUtil.Platform.Core.Native;

/// <summary>OS のダイアログ（案内 UI）を抽象化するインターフェイス。</summary>
/// <remarks>
/// <b>なぜ抽象にするか</b>: 案内 UI は利用者の選択で分岐するため、<b>選択の組み合わせを UI なしで検証できない</b>と
/// 分岐の被覆が落ちる。 抽象にすれば、すべての選択と「表示できない環境」をテストで再現できる。<br/>
/// <b>失敗を例外にしない</b>: 表示できない状況（対話セッションでない・タスク ダイアログを使えない等）でも
/// 起動処理を妨げない（<see langword="false"/> を返すか、呼び出し側の既定値を返す）。<br/>
/// 具象実装ではなく本インターフェイスへ依存させることで、テスト時の差し替えが可能になる（DIP）。
/// </remarks>
public interface IDialogService
{
    /// <summary>タスク ダイアログを表示し、押されたボタンの識別子を返す。</summary>
    /// <param name="windowTitle">ダイアログの表題。 null 不可。</param>
    /// <param name="mainInstruction">主要な指示（太字で表示される 1 行）。 null 不可。</param>
    /// <param name="content">本文。 改行は <see cref="Environment.NewLine"/>。 null 不可。</param>
    /// <param name="buttons">ボタン（1 つ以上。 識別子は 0 以外）。 null 不可。</param>
    /// <param name="defaultButtonId">既定のボタンの識別子（<paramref name="buttons"/> のいずれか）。</param>
    /// <param name="hyperlinkHandler">本文のリンクが押されたときに URL を受け取る処理。 null の場合はリンクを有効にしない。</param>
    /// <param name="selectedButtonId">押されたボタンの識別子。 表示できなかった場合・閉じられた場合は <paramref name="defaultButtonId"/>。</param>
    /// <returns>表示できた場合は <see langword="true"/>。 タスク ダイアログを使えない環境では <see langword="false"/>。</returns>
    /// <remarks>選択の解釈（識別子から意味への写像）は呼び出し側が行う（ダイアログは特定の業務を知らない）。</remarks>
    bool TryShowTaskDialog(
        string windowTitle,
        string mainInstruction,
        string content,
        IReadOnlyList<EsDialogButton> buttons,
        int defaultButtonId,
        Func<string, bool>? hyperlinkHandler,
        out int selectedButtonId);

    /// <summary>警告のメッセージ ボックスを表示し、押されたボタンを返す（はい / いいえ / キャンセル）。</summary>
    /// <param name="caption">表題（アプリ名）。 null 不可。</param>
    /// <param name="text">本文。 null 不可。</param>
    /// <returns>押されたボタン。</returns>
    /// <remarks>タスク ダイアログを使えない環境の代替として使う（<see cref="TryShowTaskDialog"/> が false を返した場合）。</remarks>
    EsDialogResult ShowYesNoCancel(string caption, string text);

    /// <summary>エラーのメッセージ ボックスを表示する。</summary>
    /// <param name="caption">表題（アプリ名）。 null 不可。</param>
    /// <param name="text">本文。 null 不可。</param>
    /// <remarks>利用者が閉じるまで待つが、選択されたボタンでは分岐しない（戻り値を持たない）。</remarks>
    void ShowError(string caption, string text);
}
