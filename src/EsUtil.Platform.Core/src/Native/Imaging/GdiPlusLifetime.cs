using System.Runtime.Versioning;

namespace EsUtil.Platform.Core.Native.Imaging;

/// <summary>GDI+ の初期化（プロセス単位）を単一所有するクラス。</summary>
/// <remarks>
/// <b>なぜ必要か</b>: GDI+ のフラット API（<c>Gdiplus*</c> / <c>Gdip*</c>）は、呼び出す前に
/// <c>GdiplusStartup</c> でプロセス単位の初期化を済ませておく必要がある。<br/>
/// <b>初期化は 1 回だけ行う</b>: 複数スレッドから同時に呼ばれても 1 回になるよう <see cref="Lazy{T}"/> が保証する。<br/>
/// <b>終了処理は呼ばない</b>: <c>GdiplusShutdown</c> はプロセス全体の GDI+ を終了させるため、
/// ホスト（アプリ）が別の目的で GDI+（<c>System.Drawing</c> 等）を使っている場合に影響する。
/// GDI+ はプロセスの終了時に OS が解放するため、本クラスは初期化だけを所有する。<br/>
/// <b>Windows 専用</b>: <c>gdiplus.dll</c> は Windows にのみ存在する（<see cref="SupportedOSPlatformAttribute"/> で限定）。
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class GdiPlusLifetime
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
        var input = new GdiPlusNative.GdiplusStartupInput { GdiplusVersion = 1 };
        return GdiPlusNative.GdiplusStartup(out _, ref input, IntPtr.Zero) == GdiPlusNative.Ok;
    }
}
