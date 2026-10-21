using Avalonia.Controls;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Native;

namespace EsUtil.Ui.AvaloniaUI.Interop;

/// <summary><see cref="ICursorOverrideGuard"/> の Avalonia 向け拡張。</summary>
/// <remarks>
/// コアの <see cref="ICursorOverrideGuard"/> は UI フレームワーク非依存のハンドルを受け取るため、<br/>
/// Avalonia の <see cref="TopLevel"/> からハンドルを取得する処理を本拡張に集約する。<br/>
/// これにより、消費側は <c>guard.Attach(window)</c> のまま利用できる。
/// </remarks>
public static class CursorOverrideGuardExtensions
{
    /// <summary>Avalonia のウィンドウにカーソル上書き抑止をアタッチする。</summary>
    /// <param name="guard">対象のガード。</param>
    /// <param name="window">対象ウィンドウ。</param>
    /// <remarks>
    /// <see cref="TopLevel"/> からネイティブハンドルを取得して <see cref="ICursorOverrideGuard.Attach(EsPlatformHandle)"/> へ委譲する。<br/>
    /// ハンドルを取得できない環境（デザインモード等）でも安全に呼べる（抑止は行われない）。
    /// </remarks>
    public static void Attach(this ICursorOverrideGuard guard, TopLevel window)
        => guard.Attach(window.TryGetPlatformHandle().ToEsPlatformHandle());
}
