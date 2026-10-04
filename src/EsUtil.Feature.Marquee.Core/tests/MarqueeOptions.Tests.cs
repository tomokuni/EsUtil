using Xunit;


namespace EsUtil.Feature.Marquee.Tests;

/// <summary><see cref="MarqueeOptions"/> の単体テスト（既定値・部分変更・値補正・等価性）。</summary>
/// <remarks>
/// 設定の既定値と補正は本型が単一所有するため、両 UI のプロパティの既定値・補正は本テストで担保する。<br/>
/// 色は UI 非依存の <see cref="System.Drawing.Color"/> で比較する。
/// </remarks>
public class MarqueeOptionsTests
{
    [Fact]
    internal void 既定値が両UIの期待値と一致する()
    {
        var options = MarqueeOptions.Default;

        // 色・表示可否・フォントサイズは描画にだけ必要な設定のため本型は持たない
        // （描画層の外観が単一所有する）
        Assert.Equal(10.0, options.HandleSize);
        Assert.Equal(16.0, options.MarkerLength);
        Assert.Equal(12.0, options.HandleHitRadius);
        Assert.Equal(6.0, options.MinSelectionSize);
        Assert.True(options.ClampToSourceBounds);
    }

    [Fact]
    internal void newとDefaultは等価である()
    {
        // プロパティ初期化子で既定値が入るため、暗黙の new() でも Default と等しくなる
        Assert.Equal(MarqueeOptions.Default, new MarqueeOptions());
        Assert.Equal(MarqueeOptions.Default.GetHashCode(), new MarqueeOptions().GetHashCode());
    }

    [Fact]
    internal void 一部の項目だけを変更しても他の項目は既定値を保つ()
    {
        var options = MarqueeOptions.Default with { MinSelectionSize = 20.0 };

        Assert.Equal(20.0, options.MinSelectionSize);
        Assert.Equal(MarqueeOptions.Default.HandleSize, options.HandleSize);
        Assert.True(options.ClampToSourceBounds);
    }

    [Fact]
    internal void ハンドルのサイズを変更しても他の項目は既定値を保つ()
    {
        var options = MarqueeOptions.Default with { HandleSize = 20 };

        Assert.Equal(20, options.HandleSize);
        Assert.Equal(MarqueeOptions.Default.MarkerLength, options.MarkerLength);
    }

    [Fact]
    internal void 同名で同値の設定は等価である()
    {
        var a = MarqueeOptions.Default with { HandleSize = 12.0, MarkerLength = 20.0 };
        var b = MarqueeOptions.Default with { HandleSize = 12.0, MarkerLength = 20.0 };

        Assert.Equal(a, b);
        Assert.NotEqual(a, a with { MarkerLength = 22.0 });
    }

    [Fact]
    internal void 設定は不変である()
    {
        var options = MarqueeOptions.Default;
        var changed = options with { MinSelectionSize = 20.0 };

        // with は新しい値を作る（元の値は変わらない）
        Assert.Equal(6.0, options.MinSelectionSize);
        Assert.Equal(20.0, changed.MinSelectionSize);
        Assert.NotEqual(options, changed);
    }

    [Fact]
    internal void HandleSize_指定された値をそのまま保持する()
    {
        var options = MarqueeOptions.Default with { HandleSize = 12.5 };

        Assert.Equal(12.5, options.HandleSize);
    }

    /// <summary>数値項目が生成時に安全域へ補正されることを検証する。</summary>
    /// <param name="name">補正対象の項目名。</param>
    /// <param name="value">設定する値。</param>
    /// <param name="expected">補正後の値。</param>
    /// <remarks>
    /// 補正は生成時に行われるため、設定の経路（オブジェクト初期化子・<c>with</c> 式・逆シリアル化）によらず<br/>
    /// 安全域内の値だけが保持される（UI 側に補正の登録を要求しない）。
    /// </remarks>
    [Theory]
    [InlineData("HandleSize", 0.5, 1.0)]
    [InlineData("HandleSize", 300.0, 256.0)]
    [InlineData("HandleSize", double.NaN, 10.0)]
    [InlineData("MarkerLength", 1.0, 2.0)]
    [InlineData("MarkerLength", 5000.0, 4096.0)]
    [InlineData("MarkerLength", double.PositiveInfinity, 16.0)]
    [InlineData("HandleHitRadius", -1.0, 0.0)]
    [InlineData("HandleHitRadius", 300.0, 256.0)]
    [InlineData("MinSelectionSize", 0.0, 1.0)]
    [InlineData("MinSelectionSize", 5000.0, 4096.0)]
    [InlineData("MinSelectionSize", double.NaN, 6.0)]
    internal void 数値項目は生成時に安全域へ補正される(string name, double value, double expected)
    {
        var options = name switch
        {
            "HandleSize" => MarqueeOptions.Default with { HandleSize = value },
            "MarkerLength" => MarqueeOptions.Default with { MarkerLength = value },
            "HandleHitRadius" => MarqueeOptions.Default with { HandleHitRadius = value },
            _ => MarqueeOptions.Default with { MinSelectionSize = value },
        };

        var actual = name switch
        {
            "HandleSize" => options.HandleSize,
            "MarkerLength" => options.MarkerLength,
            "HandleHitRadius" => options.HandleHitRadius,
            _ => options.MinSelectionSize,
        };

        Assert.Equal(expected, actual);
    }
}
