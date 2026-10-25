using Xunit;

using static EsUtil.Feature.MultiColumn.ColumnPacker;

namespace EsUtil.Feature.MultiColumn.Tests;

/// <summary>ColumnPackerExtensions（C# 14 拡張メンバー）の単体テスト</summary>
public class ColumnPackerExtensionsTests
{

    /// <summary>Solve_アルゴリズム指定_テスト</summary>
    [Theory]
    [InlineData(Method.DynamicProgramming)]
    [InlineData(Method.Greedy)]
    [InlineData(Method.BinarySearch)]
    internal void Solve_アルゴリズム指定_テスト(Method method)
    {
        // Arrange
        var items = TestHelper.GetStandardTestItems();
        var expectedEngine = new ColumnPacker(items, (0.0, 0.0), 3)
        {
            CurrentMethod = method
        };
        var expected = expectedEngine.Solve(150.0);

        var engine = new ColumnPacker(items, (0.0, 0.0), 3);

        // Act
        var actual = engine.Solve(150.0, method);

        // Assert
        Assert.Equal(expected.UsedWidth, actual.UsedWidth);
        Assert.Equal(expected.MinHeight, actual.MinHeight);
    }

    /// <summary>Solve_CurrentMethod_復元_テスト</summary>
    [Theory]
    [InlineData(Method.DynamicProgramming, Method.Greedy)]
    [InlineData(Method.Greedy, Method.BinarySearch)]
    [InlineData(Method.BinarySearch, Method.DynamicProgramming)]
    internal void Solve_CurrentMethod_復元_テスト(Method before, Method method)
    {
        // Arrange
        var items = TestHelper.GetStandardTestItems();
        var engine = new ColumnPacker(items, (0.0, 0.0), 3)
        {
            CurrentMethod = before
        };

        // Act
        engine.Solve(150.0, method);

        // Assert
        Assert.Equal(before, engine.CurrentMethod);
    }

    /// <summary>Solve_例外時_CurrentMethod_復元_テスト</summary>
    [Fact]
    internal void Solve_例外時_CurrentMethod_復元_テスト()
    {
        // Arrange
        var items = TestHelper.GetStandardTestItems();
        var engine = new ColumnPacker(items, (0.0, 0.0), 3)
        {
            CurrentMethod = Method.DynamicProgramming
        };

        // Act
        var ex = Assert.Throws<ArgumentException>(() => engine.Solve(0.0, Method.Greedy));

        // Assert
        Assert.Contains("widthLimit must be positive.", ex.Message, StringComparison.Ordinal);
        Assert.Equal(Method.DynamicProgramming, engine.CurrentMethod);
    }

    /// <summary>ItemCount_アイテム数_テスト</summary>
    [Fact]
    internal void ItemCount_アイテム数_テスト()
    {
        // Arrange
        var items = TestHelper.GetStandardTestItems();
        var engine = new ColumnPacker(items, (0.0, 0.0), 3);

        // Act
        var count = engine.ItemCount;

        // Assert
        Assert.Equal(items.Count, count);
    }

    /// <summary>ItemCount_空リスト_テスト</summary>
    [Fact]
    internal void ItemCount_空リスト_テスト()
    {
        // Arrange
        var items = new List<(double Width, double Height)>();
        var engine = new ColumnPacker(items, (0.0, 0.0), 3);

        // Act
        var count = engine.ItemCount;

        // Assert
        Assert.Equal(0, count);
    }

    /// <summary>ColumnCount_未実行_0_テスト</summary>
    [Fact]
    internal void ColumnCount_未実行_0_テスト()
    {
        // Arrange
        var items = TestHelper.GetStandardTestItems();
        var engine = new ColumnPacker(items, (0.0, 0.0), 3);

        // Act
        var count = engine.ColumnCount;

        // Assert
        Assert.Equal(0, count);
    }

    /// <summary>ColumnCount_単列フォールバック_1_テスト</summary>
    [Fact]
    internal void ColumnCount_単列フォールバック_1_テスト()
    {
        // Arrange
        var items = TestHelper.GetStandardTestItems();
        var engine = new ColumnPacker(items, (0.0, 0.0), 3);

        // Act
        engine.Solve(1.0); // 最大アイテム幅を下回るため単列配置へフォールバックする
        var count = engine.ColumnCount;

        // Assert
        Assert.Equal(1, count);
    }

    /// <summary>ColumnCount_複数列_テスト</summary>
    [Fact]
    internal void ColumnCount_複数列_テスト()
    {
        // Arrange
        var items = TestHelper.GetStandardTestItems();
        var engine = new ColumnPacker(items, (0.0, 0.0), 3);

        // Act
        engine.Solve(150.0);
        var count = engine.ColumnCount;

        // Assert
        Assert.True(count >= 1);
        Assert.Equal(engine.GetLastColumnSegments().Length, count);
    }

    /// <summary>IterationCount_未実行_0_テスト</summary>
    [Fact]
    internal void IterationCount_未実行_0_テスト()
    {
        // Arrange
        var items = TestHelper.GetStandardTestItems();
        var engine = new ColumnPacker(items, (0.0, 0.0), 3);

        // Act
        var count = engine.IterationCount;

        // Assert
        Assert.Equal(0, count);
    }

    /// <summary>IterationCount_BinarySearch実行後_テスト</summary>
    [Fact]
    internal void IterationCount_BinarySearch実行後_テスト()
    {
        // Arrange
        var items = TestHelper.GetStandardTestItems();
        var engine = new ColumnPacker(items, (0.0, 0.0), 3);

        // Act
        engine.Solve(150.0, Method.BinarySearch);
        var count = engine.IterationCount;

        // Assert
        Assert.True(count >= 0);
        Assert.Equal(engine.GetBinarySearchIterationCount(), count);
    }
}
