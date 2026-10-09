using System.Runtime.InteropServices;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native.Logging;
using Microsoft.Extensions.Logging;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>Win32 API を用いた OS カーソル操作（位置取得・位置移動・表示/非表示）を提供する。</summary>
/// <remarks>
/// カーソル位置の取得は <c>GetCursorPos</c>、移動は <c>SetCursorPos</c>、表示/非表示は <c>ShowCursor</c> を使用する。<br/>
/// カーソル形状（Avalonia の <c>Cursor</c> プロパティで代替）やポインター位置取得
/// （<c>PointerEventArgs.GetPosition</c> で代替）は扱わない。<br/>
/// <see cref="ICursorService"/> の既定実装（Windows）。<br/>
/// 他プラットフォームでは <see cref="ICursorService"/> を注入することで差し替え可能（DIP）。<br/>
/// 継承不可。<br/>
/// </remarks>
public sealed class WinCursorService : ICursorService
{
    /// <summary>ロガー（null の場合は出力なし）。</summary>
    private readonly ILogger _log;

    /// <summary><see cref="WinNative.CURSORINFO"/> 構造体のマーシャリングサイズ（バイト）。</summary>
    private static readonly int s_cursorInfoSize = Marshal.SizeOf<WinNative.CURSORINFO>();

    /// <summary><see cref="WinCursorService"/> の新しいインスタンスを初期化します。</summary>
    /// <param name="logger">ロガー。 null の場合は出力なし。</param>
    /// <remarks>
    /// 表示状態（<see cref="_cursorVisible"/>）は Win32 の <c>GetCursorInfo</c> から実状態を取得して初期化する。<br/>
    /// これにより、他コードがカーソルを非表示にした状態（表示カウンタが負）で本サービスを生成しても、<br/>
    /// nudge 中の <c>ShowCursor(false)</c> が余分にカウンタを減らさず、<c>ShowCursor(true)</c> で確実に復元できる。<br/>
    /// </remarks>
    public WinCursorService(ILogger? logger = null)
    {
        _log = logger.OrNullLogger();

        // Win32 の表示カウンタと状態追跡を同期する（初期状態の実態を取得）
        var info = new WinNative.CURSORINFO { cbSize = s_cursorInfoSize };
        if (WinNative.GetCursorInfo(ref info))
        {
            _cursorVisible = (info.flags & WinNative.CURSOR_SHOWING) != 0;
        }
        else
        {
            // 取得失敗時は表示中とみなす（フォールバック）。 失敗は記録する
            _log.LogCursorVisibilityQueryFailed();
            _cursorVisible = true;
        }
    }

    // ====================================================================
    // カーソル位置取得
    // ====================================================================

    /// <summary>現在のカーソル位置を画面物理座標で取得する。</summary>
    /// <returns>現在のカーソル位置（画面物理座標）。</returns>
    /// <remarks>
    /// WM_SETCURSOR 等の OS イベント外でカーソルの絶対位置が必要な場合に使用する。<br/>
    /// <b>取得に失敗した場合は原点を返し、失敗を記録する</b>（例外にしない。 高頻度で呼ばれるため既定の出力では溢れないよう Warning）。
    /// </remarks>
    public EsPointInt GetCursorScreenPoint()
    {
        if (!WinNative.GetCursorPos(out var pt))
        {
            _log.LogCursorPositionQueryFailed();
        }

        return new EsPointInt(pt.X, pt.Y);
    }

    // ====================================================================
    // カーソル位置移動
    // ====================================================================

    /// <summary>カーソルを画面物理座標に移動する。</summary>
    /// <param name="point">移動先の画面物理座標。</param>
    /// <remarks>
    /// nudge（カーソルキーによる選択の移動）で使用する。<br/>
    /// <b>移動に失敗した場合は失敗を記録する</b>（例外にしない）。
    /// </remarks>
    public void SetCursorScreenPoint(EsPointInt point)
    {
        if (!WinNative.SetCursorPos(point.X, point.Y))
        {
            _log.LogCursorPositionSetFailed(point.X, point.Y);
        }
    }

    // ====================================================================
    // カーソル表示/非表示
    // ====================================================================

    /// <summary>マウスポインターの表示/非表示を切り替える。</summary>
    /// <param name="show">表示する場合は <see langword="true"/>、非表示にする場合は <see langword="false"/>。</param>
    /// <remarks>
    /// Win32 の <c>ShowCursor</c> は表示カウンタを増減するため、<br/>
    /// 非表示（false）と表示（true）を対で呼び出すことでカウンタのバランスを保つ。<br/>
    /// 本メソッドは現在の表示状態（コンストラクタで実状態と同期済み）を追跡し、状態が変化したときのみ Win32 を呼び出す。<br/>
    /// これにより、nudge 中に <c>ShowCursor(false)</c> を複数回呼んでもカウンタが深く負にならず、<br/>
    /// <c>ShowCursor(true)</c> を1回呼ぶだけで確実に再表示される（仕様 4.4）。<br/>
    /// </remarks>
    public void ShowCursor(bool show)
    {
        if (_cursorVisible == show) return;
        _cursorVisible = show;
        _log.LogCursorVisibilityChanged(show);
        WinNative.ShowCursor(show);
    }

    /// <summary>マウスポインターの表示状態（Win32 の表示カウンタとの整合を保つための追跡値）。</summary>
    /// <remarks>コンストラクタで <c>GetCursorInfo</c> の実状態から初期化する。</remarks>
    private bool _cursorVisible = true;
}


