using Xunit;

using static EsUtil.Feature.MultiColumn.MultiColumnLayoutEngine;

namespace EsUtil.Feature.MultiColumn.Tests;

/// <summary>MultiColumnLayoutEngine の単体テスト</summary>
public class MultiColumnLayoutEngineTests
{

    /// <summary>MultiColumnLayoutEngine_コンストラクタ_テスト</summary>
    [Fact]
    internal void MultiColumnLayoutEngine_コンストラクタ_テスト()
    {
        // Arrange
        var items = TestHelper.GetSingleItem();

        // Act
        var engine = new MultiColumnLayoutEngine(items, (0.0, 0.0), 10);

        // Assert
        Assert.NotNull(engine);
        Assert.Equal(Method.BinarySearch, engine.CurrentMethod);
    }

    /// <summary>MultiColumnLayoutEngine_CurrentMethod_設定_テスト</summary>
    [Fact]
    internal void MultiColumnLayoutEngine_CurrentMethod_設定_テスト()
    {
        // Arrange
        var items = TestHelper.GetSingleItem();
        var engine = new MultiColumnLayoutEngine(items, (0.0, 0.0), 10)
        {
            CurrentMethod = Method.DynamicProgramming,
        };

        // Act

        // Assert
        Assert.Equal(Method.DynamicProgramming, engine.CurrentMethod);
    }

    /// <summary>MultiColumnLayoutEngine_BinarySearchOptions_デフォルト_テスト</summary>
    [Fact]
    internal void MultiColumnLayoutEngine_BinarySearchOptions_デフォルト_テスト()
    {
        // Arrange
        var items = TestHelper.GetSingleItem();
        var engine = new MultiColumnLayoutEngine(items, (0.0, 0.0), 10);

        // Act
        var options = engine.CurrentBinarySearchOptions;

        // Assert
        Assert.Equal(BinarySearchOptions.Default, options);
    }

    /// <summary>MultiColumnLayoutEngine_BinarySearchOptions_設定_テスト</summary>
    [Fact]
    internal void MultiColumnLayoutEngine_BinarySearchOptions_設定_テスト()
    {
        // Arrange
        var items = TestHelper.GetSingleItem();
        var engine = new MultiColumnLayoutEngine(items, (0.0, 0.0), 10);
        var newOptions = new BinarySearchOptions(1e-4, 50, 0.9);

        // Act
        engine.CurrentBinarySearchOptions = newOptions;

        // Assert
        Assert.Equal(newOptions, engine.CurrentBinarySearchOptions);
    }

    /// <summary>MultiColumnLayoutEngine_GetCurrentStrategyName_テスト</summary>
    [Fact]
    internal void MultiColumnLayoutEngine_GetCurrentStrategyName_テスト()
    {
        // Arrange
        var items = TestHelper.GetSingleItem();
        var engine = new MultiColumnLayoutEngine(items, (0.0, 0.0), 10);

        // Act
        var name = engine.GetCurrentStrategyName();

        // Assert
        Assert.Equal("BinarySearchLayoutStrategy", name);
    }

    /// <summary>MultiColumnLayoutEngine_GetBinarySearchIterationCount_初期_テスト</summary>
    [Fact]
    internal void MultiColumnLayoutEngine_GetBinarySearchIterationCount_初期_テスト()
    {
        // Arrange
        var items = TestHelper.GetSingleItem();
        var engine = new MultiColumnLayoutEngine(items, (0.0, 0.0), 10);

        // Act
        var count = engine.GetBinarySearchIterationCount();

        // Assert
        Assert.Null(count);
    }

    /// <summary>MultiColumnLayoutEngine_GetBinarySearchIterationCount_実行後_テスト</summary>
    [Fact]
    internal void MultiColumnLayoutEngine_GetBinarySearchIterationCount_実行後_テスト()
    {
        // Arrange
        var items = TestHelper.GetStandardTestItems();
        var engine = new MultiColumnLayoutEngine(items, (0.0, 0.0), 3);
        engine.Solve(150.0); // 実行

        // Act
        var count = engine.GetBinarySearchIterationCount();

        // Assert
        Assert.True(count >= 0);
    }

}
