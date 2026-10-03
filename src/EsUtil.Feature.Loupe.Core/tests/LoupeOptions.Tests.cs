using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Feature.Loupe.Tests;

/// <summary><see cref="LoupeOptions"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 設定の既定値・安全域への補正・部分変更・等価性を検証する。<br/>
/// 色・フォント・枠線の太さ・表示可否は描画にだけ必要な設定であり本型は持たない（上位層のテストが担う）。<br/>
/// UI のプロパティ経由（バインディング等）の反映は各 UI のコントロールテストが担う。
/// </remarks>
public class LoupeOptionsTests
{
    [Fact]
    public void 既定値がコアの既定値と一致する()
    {
        var options = new LoupeOptions();

        Assert.Equal(LoupeCore.Limits.RequestedSizeDefault, options.RequestedSize);
        Assert.Equal(LoupeCore.Limits.ZoomLevelDefault, options.ZoomLevel);
    }

    [Fact]
    public void Defaultは既定値と同じ値を持つ()
    {
        Assert.Equal(new LoupeOptions(), LoupeOptions.Default);
    }

    [Fact]
    public void with式で一部だけ変更できる()
    {
        var options = LoupeOptions.Default;

        var changed = options with { ZoomLevel = 8 };

        Assert.Equal(8, changed.ZoomLevel);
        // 他の項目は維持される（部分指定でもゼロ値にならない）
        Assert.Equal(options.RequestedSize, changed.RequestedSize);
    }

    /// <summary>数値項目が生成時に安全域へ補正されることを検証する。</summary>
    /// <remarks>
    /// 補正を <c>init</c> アクセサで行うため、オブジェクト初期化子・<c>with</c> 式・逆シリアル化の<br/>
    /// どの設定経路でも範囲内の値だけが保持される（UI 側に補正の登録を要求しない）。
    /// </remarks>
    [Fact]
    public void 生成時に数値項目が安全域へ補正される()
    {
        var options = new LoupeOptions
        {
            RequestedSize = 100000,
            ZoomLevel = 0,
        };

        Assert.Equal(LoupeCore.Limits.RequestedSizeMax, options.RequestedSize);
        Assert.Equal(LoupeCore.Limits.ZoomLevelMin, options.ZoomLevel);
    }

    /// <summary><c>with</c> 式による変更も生成時と同じ補正を受けることを検証する。</summary>
    [Fact]
    public void with式の変更も安全域へ補正される()
    {
        var changed = LoupeOptions.Default with { RequestedSize = 0, ZoomLevel = 1000 };

        Assert.Equal(LoupeCore.Limits.RequestedSizeMin, changed.RequestedSize);
        Assert.Equal(LoupeCore.Limits.ZoomLevelMax, changed.ZoomLevel);
    }

    [Fact]
    public void 安全域内の値は変更されない()
    {
        var options = new LoupeOptions { ZoomLevel = 8 };

        Assert.Equal(8, options.ZoomLevel);
    }

    [Fact]
    public void 等価性は値で判定される()
    {
        var a = LoupeOptions.Default with { ZoomLevel = 4 };
        var b = LoupeOptions.Default with { ZoomLevel = 4 };

        Assert.Equal(a, b);
        Assert.NotEqual(a, a with { ZoomLevel = 5 });
    }

    /// <summary>数値の変更が値の等価性に反映されることを検証する。</summary>
    /// <param name="same">同じ値にする場合は true。</param>
    /// <remarks>等価性により、同値の設定を再代入しても UI 側の変更通知（再描画）は発生しない。</remarks>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void 等価性が数値の変更を反映する(bool same)
    {
        var options = new LoupeOptions();
        var other = same ? options : options with { ZoomLevel = 8 };

        Assert.Equal(same, options == other);
        Assert.Equal(!same, options != other);
    }

    /// <summary>中心座標ラベルの文言はコアの値型が文字列化することを検証する。</summary>
    /// <remarks>設定は文言を持たない（文言は描画層が生成する）。</remarks>
    [Fact]
    public void ラベルに使う座標は文字列化される()
    {
        var text = new EsPoint(123, 456).Format();

        Assert.Equal("(123, 456)", text);
    }
}
