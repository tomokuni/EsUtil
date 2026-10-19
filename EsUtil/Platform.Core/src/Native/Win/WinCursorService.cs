using System.Runtime.InteropServices;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>Win32 API を用いた OS カーソル操作（位置取得・位置移動・表示/非表示）を提供する。</summary>
/// <remarks>
/// カーソル位置の取得は <c>GetCursorPos</c>、移動は <c>SetCursorPos</c>、表示/非表示は <c>ShowCursor</c> を使用する。<br/>
/// カーソル形状（Avalonia の <c>Cursor</c> プロパティで代替）やポインター位置取得
/// （<c>PointerEventArgs.GetPosition</c> で代替）は扱わない。<br/>
/// <see cref="ICursorService"/> の Windows 実装。<br/>
/// 他プラットフォームでは <see cref="ICursorService"/> を注入することで差し替え可能（DIP）。<br/>
/// <b>ログを出力しない</b>: 失敗は <c>Try</c> 系の戻り値で呼び出し側へ渡し、記録は呼び出し側が行う。<br/>
/// 継承不可。<br/>
/// </remarks>
public sealed class WinCursorService : ICursorService
{
    /// <summary><see cref="WinNative.CURSORINFO"/> 構造体のマーシャリングサイズ（バイト）。</summary>
    private static readonly int s_cursorInfoSize = Marshal.SizeOf<WinNative.CURSORINFO>();

    /// <summary>マウスポインターの表示状態（Win32 の表示カウンタとの整合を保つための追跡値）。</summary>
    private bool _cursorVisible = true;

    /// <summary><see cref="WinCursorService"/> の新しいインスタンスを初期化します。</summary>
    /// <remarks>
    /// 表示状態（<see cref="_cursorVisible"/>）は Win32 の <c>GetCursorInfo</c> から実状態を取得して初期化する。<br/>
    /// これにより、他コードがカーソルを非表示にした状態（表示カウンタが負）で本サービスを生成しても、<br/>
    /// nudge 中の <c>ShowCursor(false)</c> が余分にカウンタを減らさず、<c>ShowCursor(true)</c> で確実に復元できる。<br/>
    /// <b>取得に失敗した場合は「表示中」とみなす</b>（Win32 の既定の状態。 失敗を「非表示」とみなすと、
    /// 初期化直後の <c>ShowCursor(true)</c> が呼ばれずカーソルが非表示のまま残る可能性があるため、安全側を選ぶ）。<br/>
    /// コンストラクターのため失敗を返す手段が無く、この状態を呼び出し側が判別する方法も無い
    /// （クエリは補助情報であり、失敗しても操作の成否には影響しない）。
    /// </remarks>
    public WinCursorService()
    {
        // Win32 の表示カウンタと状態追跡を同期する（初期状態の実態を取得）
        var info = new WinNative.CURSORINFO { cbSize = s_cursorInfoSize };
        if (WinNative.GetCursorInfo(ref info))
        {
            _cursorVisible = (info.flags & WinNative.CURSOR_SHOWING) != 0;
        }
    }

    // ====================================================================
    // カーソル位置取得
    // ====================================================================

    /// <inheritdoc/>
    /// <remarks>
    /// WM_SETCURSOR 等の OS イベント外でカーソルの絶対位置が必要な場合に使用する。<br/>
    /// <b>失敗した場合は <see langword="false"/> を返し、<paramref name="point"/> へ原点を入れない</b>
    /// （原点は有効な座標であるため、失敗を原点で表すと誤った位置へ移動する原因になる）。
    /// </remarks>
    public bool TryGetCursorScreenPoint(out EsPointInt point)
    {
        if (WinNative.GetCursorPos(out var pt))
        {
            point = new EsPointInt(pt.X, pt.Y);
            return true;
        }

        point = default;
        return false;
    }

    // ====================================================================
    // カーソル位置移動
    // ====================================================================

    /// <inheritdoc/>
    /// <remarks>nudge（カーソルキーによる選択の移動）で使用する。</remarks>
    public bool TrySetCursorScreenPoint(EsPointInt point) => WinNative.SetCursorPos(point.X, point.Y);

    // ====================================================================
    // カーソル表示/非表示
    // ====================================================================

    /// <inheritdoc/>
    /// <remarks>
    /// Win32 の <c>ShowCursor</c> は表示カウンタを増減するため、<br/>
    /// 非表示（false）と表示（true）を対で呼び出すことでカウンタのバランスを保つ。<br/>
    /// 本メソッドは現在の表示状態（コンストラクターで実状態と同期済み）を追跡し、状態が変化したときのみ Win32 を呼び出す。<br/>
    /// これにより、nudge 中に <c>ShowCursor(false)</c> を複数回呼んでもカウンタが深く負にならず、<br/>
    /// <c>ShowCursor(true)</c> を 1 回呼ぶだけで確実に再表示される（仕様 4.4）。
    /// </remarks>
    public void ShowCursor(bool show)
    {
        if (_cursorVisible == show) return;
        _cursorVisible = show;
        WinNative.ShowCursor(show);
    }
}
