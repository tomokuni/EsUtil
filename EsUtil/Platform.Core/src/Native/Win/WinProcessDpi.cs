using Windows.Win32;
using Windows.Win32.UI.HiDpi;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>プロセス全体の DPI 対応を設定する（UI フレームワークを持たないホスト向け）。</summary>
/// <remarks>
/// <b>UI フレームワークを持つアプリは使わない</b>: WinForms は <c>Application.SetHighDpiMode</c>、MewUI は
/// <c>DpiHelper.EnablePerMonitorDpiAwareness</c>、Avalonia は既定の初期化がプロセスの DPI 対応を所有する。<br/>
/// 本クラスは<b>サービス・ツール・コンソール アプリ</b>のように UI フレームワークを持たないホスト向けである
/// （規則は [`.github/ビルド設定規約.md`](../../../../.github/ビルド設定規約.md) §5）。<br/>
/// <b>呼ぶ時機</b>: ウィンドウを作る前（プロセスの起動直後）に 1 度だけ呼ぶ。 2 回目以降は OS が拒否する。<br/>
/// <b>Win32 の宣言は CsWin32 が生成する</b>（<c>src/NativeMethods.txt</c>）。
/// </remarks>
public static class WinProcessDpi
{
    /// <summary>Per-Monitor V2 を表す <c>DPI_AWARENESS_CONTEXT</c> の値（<c>DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2</c>）。</summary>
    /// <remarks>Win32 の定義は <c>((DPI_AWARENESS_CONTEXT)-4)</c> であり、定数は CsWin32 が生成しないため本クラスが持つ。</remarks>
    private const int PerMonitorAwareV2Context = -4;

    /// <summary>プロセス全体を Per-Monitor V2 の DPI 対応にする。</summary>
    /// <returns>設定した場合は <see langword="true"/>。 既に設定済み、または OS が拒否した場合は <see langword="false"/>。</returns>
    /// <remarks>
    /// <b>失敗を例外にしない</b>: 既に設定済みの状態は正常であり（フレームワークが先に設定した場合）、
    /// 呼び出し側は結果で判断できる（[`.github/例外処理規約.md`](../../../../.github/例外処理規約.md) §3）。<br/>
    /// Windows 10 バージョン 1703（ビルド 15063）以降で利用できる（本リポジトリの下限 20348 はこれを満たす）。
    /// </remarks>
    public static bool TryEnablePerMonitorV2()
        => PInvoke.SetProcessDpiAwarenessContext(new DPI_AWARENESS_CONTEXT(PerMonitorAwareV2Context));
}
