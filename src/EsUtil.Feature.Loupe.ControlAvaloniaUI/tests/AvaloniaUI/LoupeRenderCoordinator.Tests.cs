using Xunit;

namespace EsUtil.Feature.Loupe.AvaloniaUI.Tests;

/// <summary><see cref="LoupeRenderCoordinator"/> の単体テスト（再描画バッチ化・世代番号管理）。</summary>
/// <remarks>
/// 実行キューへのポストはコンストラクタへ注入した同期実行コールバックに置き換えるため、<br/>
/// UI 初期化（Avalonia ヘッドレス等）に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// 表示状態・ビットマップ解放・再生成はコールバック（モック）で注入する。
/// </remarks>
public class LoupeRenderCoordinatorTests
{
    /// <summary>テスト用のコーディネーターを生成する（表示状態のみ注入）。</summary>
    /// <param name="isVisible">表示状態（モック）。</param>
    /// <returns>ポストを同期実行するコーディネーター。</returns>
    private static LoupeRenderCoordinator Create(bool isVisible)
        => new(
            isVisible: () => isVisible,
            releaseBitmap: () => { },
            startRender: _ => { },
            logRenderCore: _ => { },
            post: action => action()); // 実行キューを同期実行に置き換え、並列テスト可能にする

    /// <summary>テスト用のコーディネーターと、直近に採番された世代番号を保持するホルダーを生成する。</summary>
    /// <param name="isVisible">表示状態（モック）。</param>
    /// <returns>コーディネーターと世代番号の取り出し。</returns>
    /// <remarks>世代番号は再描画の実行時にのみ採番されるため、テストは再描画を実行して世代番号を得る。</remarks>
    private static (LoupeRenderCoordinator Coordinator, Func<long> LastGeneration) CreateWithGeneration(bool isVisible)
    {
        long generation = 0;
        var coordinator = new LoupeRenderCoordinator(
            isVisible: () => isVisible,
            releaseBitmap: () => { },
            startRender: g => generation = g,
            logRenderCore: _ => { },
            post: action => action());

        return (coordinator, () => generation);
    }

    /// <summary>同期実行のポストで再描画を 1 回実行する（採番された世代番号を返す）。</summary>
    /// <param name="coordinator">対象のコーディネーター。</param>
    /// <param name="lastGeneration">直近に採番された世代番号の取り出し。</param>
    /// <returns>採番された世代番号。</returns>
    private static long BeginRender(LoupeRenderCoordinator coordinator, Func<long> lastGeneration)
    {
        coordinator.RequestRender();
        return lastGeneration();
    }

    [Fact]
    internal void 生成_必須のコールバックがnullなら例外を投げる()
    {
        // 実行キュー（post）は UI 層が指定するため必須である（未指定は配線の誤り）
        Assert.Throws<ArgumentNullException>(() => new LoupeRenderCoordinator(() => true, () => { }, _ => { }, _ => { }, null!));
        Assert.Throws<ArgumentNullException>(() => new LoupeRenderCoordinator(null!, () => { }, _ => { }, _ => { }, _ => { }));
    }

    [Fact]
    internal void ShouldApply_表示中かつ最新世代ならtrueを返す()
    {
        var (coordinator, lastGeneration) = CreateWithGeneration(isVisible: true);
        var generation = BeginRender(coordinator, lastGeneration);

        Assert.Equal(1, generation);
        Assert.True(coordinator.ShouldApply(generation));
    }

    [Fact]
    internal void ShouldApply_古い世代ならfalseを返す()
    {
        var (coordinator, lastGeneration) = CreateWithGeneration(isVisible: true);
        var old = BeginRender(coordinator, lastGeneration);
        BeginRender(coordinator, lastGeneration); // 世代が進む

        Assert.False(coordinator.ShouldApply(old));
    }

    [Fact]
    internal void ShouldApply_非表示ならfalseを返す()
    {
        var (coordinator, lastGeneration) = CreateWithGeneration(isVisible: false);
        var generation = BeginRender(coordinator, lastGeneration);

        Assert.False(coordinator.ShouldApply(generation));
    }

    [Fact]
    internal void RequestRender_表示中は再描画を開始し世代が進む()
    {
        long? startGeneration = null;
        var coordinator = new LoupeRenderCoordinator(
            isVisible: () => true,
            releaseBitmap: () => { },
            startRender: g => startGeneration = g,
            logRenderCore: _ => { },
            post: action => action());

        coordinator.RequestRender();

        Assert.NotNull(startGeneration);
        Assert.Equal(1, startGeneration.Value);
    }

    [Fact]
    internal void RequestRender_複数要求は1回の再描画に結合される()
    {
        var startCount = 0;
        Action? pending = null;
        var coordinator = new LoupeRenderCoordinator(
            isVisible: () => true,
            releaseBitmap: () => { },
            startRender: _ => startCount++,
            logRenderCore: _ => { },
            post: action => pending = action); // ポストを遅延実行し、バッチ化を検証する

        // 複数要求は結合フラグにより1つのポストにまとめられる
        coordinator.RequestRender();
        coordinator.RequestRender();
        coordinator.RequestRender();
        pending!.Invoke(); // ポストされた RenderCore を 1 回だけ実行する

        Assert.Equal(1, startCount);
    }

    [Fact]
    internal void RequestRender_非表示時はビットマップを解放し再描画しない()
    {
        var releaseCount = 0;
        var startCount = 0;
        var coordinator = new LoupeRenderCoordinator(
            isVisible: () => false,
            releaseBitmap: () => releaseCount++,
            startRender: _ => startCount++,
            logRenderCore: _ => { },
            post: action => action());

        coordinator.RequestRender();

        Assert.Equal(1, releaseCount);
        Assert.Equal(0, startCount);
    }

    [Fact]
    internal void CancelPending_未実行の再描画要求を破棄する()
    {
        var startCount = 0;
        Action? pending = null;
        var coordinator = new LoupeRenderCoordinator(
            isVisible: () => true,
            releaseBitmap: () => { },
            startRender: _ => startCount++,
            logRenderCore: _ => { },
            post: action => pending = action);

        // 再描画を要求（post で pending に保持）し、キャンセル後に RenderCore を実行しても再描画されない
        coordinator.RequestRender();
        coordinator.CancelPending();
        pending?.Invoke();

        Assert.Equal(0, startCount);
    }

    [Fact]
    internal void CancelAndRelease_要求の破棄と解放を一度に行う()
    {
        var releaseCount = 0;
        var startCount = 0;
        Action? pending = null;
        var coordinator = new LoupeRenderCoordinator(
            isVisible: () => true,
            releaseBitmap: () => releaseCount++,
            startRender: _ => startCount++,
            logRenderCore: _ => { },
            post: action => pending = action);

        coordinator.RequestRender();
        coordinator.CancelAndRelease();
        pending?.Invoke();

        // 保留中の再描画は破棄され、ビットマップは解放される
        Assert.Equal(1, releaseCount);
        Assert.Equal(0, startCount);
    }

    [Fact]
    internal void CancelAndRelease_冪等に繰り返し呼べる()
    {
        var releaseCount = 0;
        var coordinator = new LoupeRenderCoordinator(
            isVisible: () => false,
            releaseBitmap: () => releaseCount++,
            startRender: _ => { },
            logRenderCore: _ => { },
            post: action => action());

        // 既に非表示の状態で明示クリアを繰り返しても解放は呼ばれる（解放自体が冪等）
        coordinator.CancelAndRelease();
        coordinator.CancelAndRelease();

        Assert.Equal(2, releaseCount);
    }

    [Fact]
    internal void RenderCore_キャンセル済みなら描画を開始しない()
    {
        var startCount = 0;
        var coordinator = new LoupeRenderCoordinator(
            isVisible: () => true,
            releaseBitmap: () => { },
            startRender: _ => startCount++,
            logRenderCore: _ => { },
            post: action => action());

        // 要求していない状態で RenderCore を直接呼んでも何も起きない（防御）
        coordinator.RenderCore();

        Assert.Equal(0, startCount);
    }

    [Fact]
    internal void RenderCore_世代番号の記録コールバックを呼ぶ()
    {
        long? logged = null;
        var coordinator = new LoupeRenderCoordinator(
            isVisible: () => true,
            releaseBitmap: () => { },
            startRender: _ => { },
            logRenderCore: g => logged = g,
            post: action => action());

        coordinator.RequestRender();

        // 世代番号の採番と記録は描画開始の前に行われる
        Assert.Equal(1, logged);
    }
}
