using System.Runtime.Versioning;
using Windows.Win32;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>プロセスのアプリ ユーザー モデル ID（AUMID）を設定する（Windows 実装）。</summary>
/// <remarks>
/// <b>なぜ必要か</b>: Windows はタスク バーのボタンを AUMID でグループ化する。 実行ファイルが複数あるアプリは、
/// AUMID を明示しないと<b>実行ファイルごとに別のアプリ</b>としてタスク バーの別の位置へ表示される（実測）。
/// 同じ AUMID を設定すると 1 か所にまとまる。<br/>
/// <b>どの AUMID を設定するかはアプリが決める</b>（本クラスは値を受け取るだけで、製品固有の値を持たない）。
/// <b>設定する時機</b>: ウィンドウを作る<b>前</b>（プロセス単位の設定のため）。<br/>
/// <b>Win32 の宣言は CsWin32 が生成する</b>（<c>src/NativeMethods.txt</c>）。<br/>
/// <b>失敗を例外にしない</b>: AUMID を設定できなくても機能は損なわれない（タスク バーのグループ分けが既定のまま）。
/// </remarks>
[SupportedOSPlatform("windows10.0.17763")]
public static class WinAppUserModelId
{
    /// <summary>プロセスの AUMID を設定する。</summary>
    /// <param name="appUserModelId">アプリ ユーザー モデル ID（逆ドメイン形式）。</param>
    /// <returns>設定できた場合は <see langword="true"/>。</returns>
    public static bool SetProcessId(string appUserModelId)
        => PInvoke.SetCurrentProcessExplicitAppUserModelID(appUserModelId).Succeeded;
}
