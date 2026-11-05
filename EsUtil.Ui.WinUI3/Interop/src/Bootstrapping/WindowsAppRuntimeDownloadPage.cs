using System.Diagnostics;
using EsUtil.Platform.Core;

namespace EsUtil.Ui.WinUI3.Interop.Bootstrapping;

/// <summary>Windows App Runtime のダウンロード ページ（インストーラーの入手先）を表し、ブラウザーで開く。</summary>
/// <remarks>
/// 案内 UI（<see cref="WindowsAppRuntimeInstallDialog"/>）と <see cref="WindowsAppRuntimeBootstrap"/> の両方が使う
/// （URL の値と開く手順を 1 か所に置き、表示する URL と開く URL をずらさない）。
/// </remarks>
internal static class WindowsAppRuntimeDownloadPage
{
    /// <summary>ダウンロード ページの URL を取得する。</summary>
    /// <value>Microsoft の公式ダウンロード ページ（すべての版のインストーラーが並ぶ）。</value>
    public static Uri Uri { get; } = new("https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads");

    /// <summary>ダウンロード ページを既定のブラウザーで開く。</summary>
    /// <returns>開けた場合は true。 関連付けが無い・ポリシーで禁止されている場合は false。</returns>
    /// <remarks>
    /// 開けなくても処理は続ける（案内には URL を文字として表示しているため、利用者は手で開ける）。
    /// </remarks>
    public static bool Open()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(Uri.AbsoluteUri)
            {
                UseShellExecute = true,
            });

            return process is not null;
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // ブラウザーを起動できない（関連付けが無い・ポリシーで禁止）。 呼び出し側は表示だけを続ける
            return false;
        }
    }
}
