using EsUtil.Ui.WinUI3.Interop.Bootstrapping;
using Xunit;

namespace EsUtil.Ui.WinUI3.Interop.Tests;

/// <summary><see cref="WindowsAppRuntimeInventory"/> の単体テスト（必要としている版・列挙の規則）。</summary>
/// <remarks>
/// インストール済みの版は実行する PC によって変わるため、<b>件数や具体的な版を固定しない</b>。
/// 「必要としている版の定義」「並び順」「判定と一覧の整合」という、どの PC でも成り立つ規則だけを検証する。
/// </remarks>
public class WindowsAppRuntimeInventoryTests
{
    [Fact]
    internal void RequiredVersion_は2_0_1以降である()
    {
        var required = WindowsAppRuntimeInventory.RequiredVersion;

        // 「Windows App Runtime 2.0.1 以降で EsUtil の WinUI3 版が動作する版数」という定義
        Assert.Equal(2, required.Major);
        Assert.True(required >= new Version(2, 0, 1), $"2.0.1 以降ではありません: {required}");
    }

    [Fact]
    internal void GetInstalledVersions_は新しい順で重複しない()
    {
        var versions = WindowsAppRuntimeInventory.GetInstalledVersions();

        for (var i = 1; i < versions.Count; i++)
        {
            Assert.True(versions[i - 1] > versions[i], $"新しい順ではありません: {versions[i - 1]} → {versions[i]}");
        }
    }

    [Fact]
    internal void TryGetInstalledVersion_は一覧の先頭と一致する()
    {
        var installed = WindowsAppRuntimeInventory.GetInstalledVersions();
        var found = WindowsAppRuntimeInventory.TryGetInstalledVersion(out var version);

        Assert.Equal(installed.Count > 0, found);
        Assert.Equal(installed.Count > 0 ? installed[0] : null, version);
    }

    [Fact]
    internal void IsRequiredVersionInstalled_は一覧の判定と一致する()
    {
        var installed = WindowsAppRuntimeInventory.GetInstalledVersions();
        var expected = installed.Any(version => version >= WindowsAppRuntimeInventory.RequiredVersion);

        Assert.Equal(expected, WindowsAppRuntimeInventory.IsRequiredVersionInstalled());
        Assert.Equal(expected, WindowsAppRuntimeInventory.IsVersionInstalled(WindowsAppRuntimeInventory.RequiredVersion));
    }

    [Fact]
    internal void IsVersionInstalled_は存在しない版を偽とする()
    {
        // 現在のランタイムより確実に大きい版（将来も含めて用意されていない値）
        Assert.False(WindowsAppRuntimeInventory.IsVersionInstalled(new Version(int.MaxValue, int.MaxValue)));

        // 下限 0.0 は「1 件でも入っていれば真」（一覧と一致する）
        Assert.Equal(
            WindowsAppRuntimeInventory.GetInstalledVersions().Count > 0,
            WindowsAppRuntimeInventory.IsVersionInstalled(new Version(0, 0)));
    }

    [Fact]
    internal void IsVersionInstalled_は版がnullの場合は例外()
    {
        Assert.Throws<ArgumentNullException>(() => WindowsAppRuntimeInventory.IsVersionInstalled(null!));
    }

    [Fact]
    internal void TryGetLoadedVersion_は例外を出さない()
    {
        // 使用中の版は「このプロセスが実際に読み込んだランタイム」であり、初期化前は取得できない。
        // 取得できなくても例外にしない（診断のための値。 .github/例外処理規約.md §3）
        var found = WindowsAppRuntimeInventory.TryGetLoadedVersion(out var version);

        if (found)
        {
            Assert.NotNull(version);
            Assert.True(version >= new Version(1, 0));
        }
        else
        {
            Assert.Null(version);
        }
    }
}
