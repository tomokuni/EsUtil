using Xunit;

namespace EsUtil.Ui.WinUI3.MultiColumn.Tests;

/// <summary><see cref="DualSourceItemsManager"/> の単体テスト。</summary>
public class DualSourceItemsManagerTests
{
    /// <summary>ItemsSourceFirst_既定は true</summary>
    [Fact]
    internal void ItemsSourceFirst_既定はtrue()
        => Assert.True(new DualSourceItemsManager().ItemsSourceFirst);

    /// <summary>ItemsSourceFirst_設定を反映する</summary>
    [Fact]
    internal void ItemsSourceFirst_設定を反映する()
    {
        var manager = new DualSourceItemsManager { ItemsSourceFirst = false };

        Assert.False(manager.ItemsSourceFirst);
    }

    /// <summary>HasDirectChildItemsStored_初期状態は false</summary>
    [Fact]
    internal void HasDirectChildItemsStored_初期状態はfalse()
        => Assert.False(new DualSourceItemsManager().HasDirectChildItemsStored);

    /// <summary>DirectChildItems_初期状態は空</summary>
    [Fact]
    internal void DirectChildItems_初期状態は空()
        => Assert.Empty(new DualSourceItemsManager().DirectChildItems);

    /// <summary>Reset_保存の状態を戻す</summary>
    [Fact]
    internal void Reset_保存の状態を戻す()
    {
        var manager = new DualSourceItemsManager();

        manager.Reset();

        Assert.False(manager.HasDirectChildItemsStored);
        Assert.Empty(manager.DirectChildItems);
    }

    /// <summary>GenerateItemsSourceElements_ItemsSourceがnullなら空</summary>
    [Fact]
    internal void GenerateItemsSourceElements_ItemsSourceがnullなら空()
        => Assert.Empty(DualSourceItemsManager.GenerateItemsSourceElements(null, null));

    /// <summary>GenerateItemsSourceElements_ItemTemplateがnullなら空</summary>
    /// <remarks>テンプレートが無い場合は要素を作れないため、例外にせず空を返す。</remarks>
    [Fact]
    internal void GenerateItemsSourceElements_ItemTemplateがnullなら空()
        => Assert.Empty(DualSourceItemsManager.GenerateItemsSourceElements(new object[] { 1, 2, 3 }, null));

    /// <summary>GenerateItemsSourceElements_空のItemsSourceなら空</summary>
    [Fact]
    internal void GenerateItemsSourceElements_空のItemsSourceなら空()
        => Assert.Empty(DualSourceItemsManager.GenerateItemsSourceElements(Array.Empty<object>(), null));
}
