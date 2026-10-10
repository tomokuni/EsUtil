using System.Runtime.Versioning;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>GDI+ の初期化（プロセス単位）を単一所有するクラス（Windows 固有の実装）。</summary>
/// <remarks>
/// <b>なぜ必要か</b>: GDI+ のフラット API（<c>Gdiplus*</c> / <c>Gdip*</c>）は、呼び出す前に
/// <c>GdiplusStartup</c> でプロセス単位の初期化を済ませておく必要がある
/// （未初期化の呼び出しは例外ではなく<b>状態 18（<c>GdiplusNotInitialized</c>）の戻り値で失敗する</b>ため、
/// 初期化を忘れると「動かないが例外も出ない」状態になる。 実測）。<br/>
/// <b>初期化は 1 回だけ行う</b>: 複数スレッドから同時に呼ばれても 1 回になるよう <see cref="Lazy{T}"/> が保証する。<br/>
/// <b>終了処理は呼ばない</b>: <c>GdiplusShutdown</c> はプロセス全体の GDI+ を終了させるため、
/// ホスト（アプリ）が別の目的で GDI+（<c>System.Drawing</c> 等）を使っている場合に影響する
/// （GDI+ は参照カウント式であり、開始と終了を対で呼ばないとホストの利用が壊れる。
/// 実測: 2 回開始して 1 回終了すると API はまだ動作する）。 GDI+ はプロセスの終了時に OS が解放する。<br/>
/// <b>副作用（実測）</b>: 初期化すると<b>バックグラウンド スレッドが 1 本増え</b>
/// （GDI+ が表示設定の通知を受け取るために作る。 起動直後 8 → 9）、<b>約 4.9 ms の遅延</b>が乗る
/// （2 回目以降は 0.5 ms）。 スレッドの生成は抑止できない（<c>SuppressBackgroundThread = 1</c> は
/// 通知フックの提供を要求するため、フック無しでは初期化自体が <c>InvalidParameter</c> で失敗する。 実測）。<br/>
/// <b>Windows 専用</b>: <c>gdiplus.dll</c> は Windows にのみ存在する（<see cref="SupportedOSPlatformAttribute"/> で限定）。
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class WinGdiPlusLifetime
{
    /// <summary>初期化の結果（成功した場合のみ true）。 初回アクセス時に 1 回だけ実行される。</summary>
    private static readonly Lazy<bool> s_initialized = new(Start, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>GDI+ が使用可能であることを保証する（未初期化なら初期化する）。</summary>
    /// <exception cref="InvalidOperationException">GDI+ を初期化できなかった場合。</exception>
    /// <remarks>初期化に失敗した場合は毎回同じ例外になる（再試行しない）。</remarks>
    public static void EnsureInitialized()
    {
        if (!s_initialized.Value)
        {
            throw new InvalidOperationException("GDI+ を初期化できませんでした（gdiplus.dll の GdiplusStartup が失敗しました）。");
        }
    }

    /// <summary>GDI+ を初期化する。</summary>
    /// <returns>初期化に成功した場合は <see langword="true"/>。</returns>
    private static bool Start()
    {
        var input = new WinNative.GdiplusStartupInput { GdiplusVersion = 1 };
        return WinNative.GdiplusStartup(out _, ref input, IntPtr.Zero) == WinNative.GpOk;
    }
}
