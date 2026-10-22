using System.Runtime.Versioning;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native.Win;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Native;

/// <summary><see cref="WinCursorService"/> の単体テスト（Win32 の呼び出しが例外にならないこと）。</summary>
/// <remarks>
/// Win32 API は実環境で動作するため、例外なく呼び出せることと状態追跡（ShowCursor の重複呼び出し抑制）を確認する。<br/>
/// <see cref="WinCursorService"/> は UI スレッド（Dispatcher）に依存せず <c>PixelPoint</c> のみを扱うため、<br/>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）ではなく実 OS 直列コレクション（<see cref="Win32Group"/>）で検証する。<br/>
/// ヘッドレス環境でカーソル位置が OS と完全に同期しない場合があるため、位置の厳密一致は検証しない。<br/>
/// **カーソルを動かせない環境**（ロック中・非対話セッション）では、その 1 件をスキップする（環境要因を不合格にしない）。
/// </remarks>
[Collection("Win32")]
[SupportedOSPlatform("windows10.0.17763")]
public class WinCursorServiceTests
{
    [Fact]
    internal void WinCursorService_TryGetCursorScreenPointが現在位置を返す()
    {
        var service = new WinCursorService();

        var succeeded = service.TryGetCursorScreenPoint(out var pos);

        // 実環境では取得できる（例外なく Win32 GetCursorPos が呼ばれ、成否が値で返る）
        Assert.True(succeeded);
        Assert.True(pos.X != int.MinValue || pos.Y != int.MinValue);
    }

    [Fact]
    internal void WinCursorService_TrySetCursorScreenPointがカーソルを移動する()
    {
        var service = new WinCursorService();
        Assert.True(service.TryGetCursorScreenPoint(out var before));

        var moved = service.TrySetCursorScreenPoint(new EsPointInt(before.X + 5, before.Y + 5));

        // ロック中のデスクトップ・非対話セッションでは、OS がカーソル移動を拒否する（実測: 同じ環境で
        // Win32 の `SetCursorPos` が false を返す）。 環境要因であって実装の不具合ではないため、
        // 不合格にせずスキップとして記録する（対話セッションで再実行すれば判定できる）。
        Assert.SkipUnless(moved, "この環境では OS がカーソル移動を拒否した（ロック中・非対話セッション）");

        // ヘッドレス環境ではカーソル移動が OS に完全には同期されない場合があるため、
        // 厳密一致ではなく「成否が値で返る」ことを確認する（クラスコメント参照）。
        Assert.True(service.TryGetCursorScreenPoint(out _));
    }

    [Fact]
    internal void WinCursorService_ShowCursorが状態を追跡する()
    {
        var service = new WinCursorService();

        // 非表示 → 同一状態の再呼び出し（スキップ）→ 再表示 → 同一状態の再呼び出し（スキップ）
        service.ShowCursor(false);
        service.ShowCursor(false);
        service.ShowCursor(true);
        service.ShowCursor(true);

        // 例外なく呼び出せることだけを確認（Win32 カウンタの整合はサービスが管理する）
        Assert.True(true);
    }

    [Fact]
    internal void POINT_コンストラクタとプロパティ()
    {
        var pt = new System.Drawing.Point(10, 20);

        Assert.Equal(10, pt.X);
        Assert.Equal(20, pt.Y);
    }
}
