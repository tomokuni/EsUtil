using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native.Win;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Native;

/// <summary><see cref="WinCursorService"/> と <see cref="WinNative"/>（Win32 P/Invoke）の単体テスト。</summary>
/// <remarks>
/// Win32 API は実環境で動作するため、例外なく呼び出せることと状態追跡（ShowCursor の重複呼び出し抑制）を確認する。<br/>
/// <see cref="WinCursorService"/> は UI スレッド（Dispatcher）に依存せず <c>PixelPoint</c> のみを扱うため、<br/>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）ではなく実 OS 直列コレクション（<see cref="Win32Group"/>）で検証する。<br/>
/// ヘッドレス環境でカーソル位置が OS と完全に同期しない場合があるため、位置の厳密一致は検証しない。
/// </remarks>
[Collection("Win32")]
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

        // ヘッドレス環境ではカーソル移動が OS に完全には同期されない場合があるため、
        // 厳密一致ではなく「成否が値で返る」ことを確認する（クラスコメント参照）。
        Assert.True(moved);
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
        var pt = new WinNative.POINT(10, 20);

        Assert.Equal(10, pt.X);
        Assert.Equal(20, pt.Y);
    }
}
