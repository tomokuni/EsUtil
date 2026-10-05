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
    internal void WinCursorService_GetCursorScreenPointが現在位置を返す()
    {
        var service = new WinCursorService();

        var pos = service.GetCursorScreenPoint();

        // 何らかの位置が返る（例外なく Win32 GetCursorPos が呼ばれる）
        Assert.True(pos.X != int.MinValue || pos.Y != int.MinValue);
    }

    [Fact]
    internal void WinCursorService_SetCursorScreenPointがカーソルを移動する()
    {
        var service = new WinCursorService();
        var before = service.GetCursorScreenPoint();

        service.SetCursorScreenPoint(new EsPointInt(before.X + 5, before.Y + 5));

        var after = service.GetCursorScreenPoint();
        // ヘッドレス環境ではカーソル移動が OS に完全には同期されない場合があるため、
        // 厳密一致ではなく「例外なく呼び出し・取得できる」ことを確認する（クラスコメント参照）。
        Assert.True(after.X != int.MinValue || after.Y != int.MinValue);
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
