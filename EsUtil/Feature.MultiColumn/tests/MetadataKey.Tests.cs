using Xunit;

using static EsUtil.Feature.MultiColumn.ColumnPacker;

namespace EsUtil.Feature.MultiColumn.Tests;

/// <summary>MetadataKey の単体テスト</summary>
public class MetadataKeyTests
{

    /// <summary>MetadataKey_値_テスト</summary>
    [Fact]
    internal void MetadataKey_値_テスト()
    {
        // Assert
        Assert.Equal(0, (int)MetadataKey.StrategyName);
        Assert.Equal(1, (int)MetadataKey.BinarySearchIterationCount);
    }

    /// <summary>MetadataKey_名前_テスト</summary>
    [Fact]
    internal void MetadataKey_名前_テスト()
    {
        // Assert
        Assert.Equal("StrategyName", MetadataKey.StrategyName.ToString());
        Assert.Equal("BinarySearchIterationCount", MetadataKey.BinarySearchIterationCount.ToString());
    }

}
