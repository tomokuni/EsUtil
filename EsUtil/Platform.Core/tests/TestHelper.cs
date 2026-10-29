using Xunit;

namespace EsUtil.Platform.Core.Tests;

/// <summary>テストで共有するユーティリティ。</summary>
internal static class TestHelper
{
    /// <summary>実 OS の状態を変えるテストを有効にする環境変数。</summary>
    internal const string RealOsStateVariable = "ESUTIL_REAL_OS_TESTS";

    /// <summary>実 OS の状態を変えるテストを実行するか。</summary>
    /// <value>環境変数 <see cref="RealOsStateVariable"/> が <c>1</c>、または CI（<c>CI</c> / <c>GITHUB_ACTIONS</c> が <c>true</c>）のときに <see langword="true"/>。</value>
    /// <remarks>
    /// カーソルの移動・表示の切り替えは<b>開発機の実画面へ影響する</b>（テスト実行中にマウスが動き、
    /// 他の作業を妨げる）。 そのため既定では実行せず、CI（利用者が居ない）と明示したときだけ実行する。<br/>
    /// 規則は <c>.github/テスト規約.md</c> §7。
    /// </remarks>
    internal static bool IsRealOsStateEnabled { get; } =
        Environment.GetEnvironmentVariable(RealOsStateVariable) == "1"
        || string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>実 OS の状態を変えるテストであることを宣言し、無効なら読み飛ばす。</summary>
    /// <param name="action">状態を変える操作の説明（例: 「実 OS のカーソルを動かす」）。</param>
    internal static void SkipUnlessRealOsState(string action)
        => Assert.SkipUnless(
            IsRealOsStateEnabled,
            $"{action}ため既定では実行しない（開発機の操作を妨げない）。 CI か {RealOsStateVariable}=1 のときだけ実行する");
}
