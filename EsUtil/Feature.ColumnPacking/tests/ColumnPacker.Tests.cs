using Xunit;

using static EsUtil.Feature.ColumnPacking.ColumnPacker;

namespace EsUtil.Feature.ColumnPacking.Tests;

/// <summary>ColumnPacker の単体テスト</summary>
public class ColumnPackerTests
{

    /// <summary>ColumnPacker_コンストラクタ_テスト</summary>
    [Fact]
    internal void ColumnPacker_コンストラクタ_テスト()
    {
        // Arrange
        var items = TestHelper.GetSingleItem();

        // Act
        var engine = new ColumnPacker(items, (0.0, 0.0), 10);

        // Assert
        Assert.NotNull(engine);
        Assert.Equal(Method.BinarySearch, engine.CurrentMethod);
    }

    /// <summary>ColumnPacker_CurrentMethod_設定_テスト</summary>
    [Fact]
    internal void ColumnPacker_CurrentMethod_設定_テスト()
    {
        // Arrange
        var items = TestHelper.GetSingleItem();
        var engine = new ColumnPacker(items, (0.0, 0.0), 10)
        {
            CurrentMethod = Method.DynamicProgramming,
        };

        // Act

        // Assert
        Assert.Equal(Method.DynamicProgramming, engine.CurrentMethod);
    }

    /// <summary>ColumnPacker_BinarySearchOptions_デフォルト_テスト</summary>
    [Fact]
    internal void ColumnPacker_BinarySearchOptions_デフォルト_テスト()
    {
        // Arrange
        var items = TestHelper.GetSingleItem();
        var engine = new ColumnPacker(items, (0.0, 0.0), 10);

        // Act
        var options = engine.CurrentBinarySearchOptions;

        // Assert
        Assert.Equal(BinarySearchOptions.Default, options);
    }

    /// <summary>ColumnPacker_BinarySearchOptions_設定_テスト</summary>
    [Fact]
    internal void ColumnPacker_BinarySearchOptions_設定_テスト()
    {
        // Arrange
        var items = TestHelper.GetSingleItem();
        var engine = new ColumnPacker(items, (0.0, 0.0), 10);
        var newOptions = new BinarySearchOptions(1e-4, 50, 0.9);

        // Act
        engine.CurrentBinarySearchOptions = newOptions;

        // Assert
        Assert.Equal(newOptions, engine.CurrentBinarySearchOptions);
    }

    /// <summary>ColumnPacker_GetCurrentStrategyName_テスト</summary>
    [Fact]
    internal void ColumnPacker_GetCurrentStrategyName_テスト()
    {
        // Arrange
        var items = TestHelper.GetSingleItem();
        var engine = new ColumnPacker(items, (0.0, 0.0), 10);

        // Act
        var name = engine.GetCurrentStrategyName();

        // Assert
        Assert.Equal("BinarySearchLayoutStrategy", name);
    }

    /// <summary>ColumnPacker_GetBinarySearchIterationCount_初期_テスト</summary>
    [Fact]
    internal void ColumnPacker_GetBinarySearchIterationCount_初期_テスト()
    {
        // Arrange
        var items = TestHelper.GetSingleItem();
        var engine = new ColumnPacker(items, (0.0, 0.0), 10);

        // Act
        var count = engine.GetBinarySearchIterationCount();

        // Assert
        Assert.Null(count);
    }

    /// <summary>ColumnPacker_GetBinarySearchIterationCount_実行後_テスト</summary>
    [Fact]
    internal void ColumnPacker_GetBinarySearchIterationCount_実行後_テスト()
    {
        // Arrange
        var items = TestHelper.GetStandardTestItems();
        var engine = new ColumnPacker(items, (0.0, 0.0), 3);
        engine.Solve(150.0); // 実行

        // Act
        var count = engine.GetBinarySearchIterationCount();

        // Assert
        Assert.True(count >= 0);
    }

}
