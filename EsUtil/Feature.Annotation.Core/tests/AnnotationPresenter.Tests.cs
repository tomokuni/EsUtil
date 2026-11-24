using EsUtil.Platform.Core.Geometry;
using Xunit;
using EsUtil.Feature.Annotation.Core.Defaults;
using EsUtil.Feature.Text;
using EsUtil.Feature.Text.Core.Defaults;

namespace EsUtil.Feature.Annotation.Core.Tests;

/// <summary><see cref="AnnotationPresenter"/> の仕様テスト（折り返し・しっぽ・行の配置）。</summary>
/// <remarks>
/// 折り返しと行の配置は 4 つの UI 版で共有するため、固定の計測関数（<see cref="FixedTextMetrics"/>）で
/// 結果の位置を固定する（フォントの実測値に依存させない）。
/// </remarks>
public class AnnotationPresenterTests
{
    /// <summary>幅を超えた位置で折り返すことを検証する。</summary>
    [Fact]
    internal void 折り返し_幅を超える位置で行を分ける()
    {
        var style = TextDefaults.DefaultStyle with { FontSize = 20 };
        var bubble = CreateBubble("ああああああ", style, width: 80);

        var plan = new AnnotationPresenter().ResolveDrawPlan(CreateDocument(bubble), FixedTextMetrics.Measure);
        var shape = Assert.Single(plan.Shapes);

        // 1 文字 10 px、本文の幅は 80 - 余白 12*2 - 枠線 1.5 = 53 → 5 文字で折り返す
        Assert.Equal(2, shape.Lines.Count);
        Assert.Equal("あああああ", Concat(shape.Lines[0]));
        Assert.Equal("あ", Concat(shape.Lines[1]));
    }

    /// <summary>明示改行が行を分けることを検証する。</summary>
    [Fact]
    internal void 折り返し_明示改行で行を分ける()
    {
        var bubble = CreateBubble("1行目\n2行目", TextDefaults.DefaultStyle, width: 600);

        var plan = new AnnotationPresenter().ResolveDrawPlan(CreateDocument(bubble), FixedTextMetrics.Measure);
        var shape = Assert.Single(plan.Shapes);

        Assert.Equal(2, shape.Lines.Count);
        Assert.Equal("1行目", Concat(shape.Lines[0]));
        Assert.Equal("2行目", Concat(shape.Lines[1]));
    }

    /// <summary>句読点を行頭へ置かないことを検証する。</summary>
    [Fact]
    internal void 折り返し_句読点を行頭へ置かない()
    {
        var style = TextDefaults.DefaultStyle with { FontSize = 20 };
        var bubble = CreateBubble("あああああ、", style, width: 80);

        var plan = new AnnotationPresenter().ResolveDrawPlan(CreateDocument(bubble), FixedTextMetrics.Measure);
        var shape = Assert.Single(plan.Shapes);

        Assert.All(shape.Lines, line => Assert.False(Concat(line).StartsWith('、'), $"[{Concat(line)}] / 全体=[{string.Concat(shape.Lines.Select(Concat))}]"));
    }

    /// <summary>しっぽの多角形が先端と付け根の 3 点になることを検証する。</summary>
    [Fact]
    internal void しっぽ_先端と付け根の3点になる()
    {
        var bubble = CreateBubble("test", TextDefaults.DefaultStyle, width: 400);
        bubble = bubble with { Tail = new BubbleTail(new EsPoint(1000, 800), BubbleEdge.Bottom, 0.5, 20) };

        var plan = new AnnotationPresenter().ResolveDrawPlan(CreateDocument(bubble), FixedTextMetrics.Measure);
        var shape = Assert.Single(plan.Shapes);

        Assert.True(shape.HasTail);
        Assert.Equal(new EsPoint(1000, 800), shape.Tail[0]);
        Assert.Equal(bubble.Bounds.Bottom, shape.Tail[1].Y);
        Assert.Equal(bubble.Bounds.Center.X, shape.Tail[1].X + 10);
    }

    /// <summary>行の間隔が文字の実寸より広いことを検証する。</summary>
    [Fact]
    internal void 行の配置_行間は文字の高さより広い()
    {
        var bubble = CreateBubble("1行目\n2行目", TextDefaults.DefaultStyle, width: 600);

        var plan = new AnnotationPresenter().ResolveDrawPlan(CreateDocument(bubble), FixedTextMetrics.Measure);
        var shape = Assert.Single(plan.Shapes);

        Assert.True(shape.Lines[1].Bounds.Y - shape.Lines[0].Bounds.Y > TextDefaults.FontSizeDefault);
    }

    /// <summary>空行（明示改行だけの行）にも高さが付くことを検証する。</summary>
    /// <remarks>
    /// 高さ 0 にすると空けた行が詰まって見え（空行を入れられない）、キャレットも高さを持たない。
    /// 空行の高さは近い行の高さへ合わせる。
    /// </remarks>
    [Fact]
    internal void 行の配置_空行にも高さが付く()
    {
        var style = TextDefaults.DefaultStyle with { FontSize = 20 };
        var bubble = CreateBubble("1行目\n\n3行目", style, width: 600);

        var plan = new AnnotationPresenter().ResolveDrawPlan(CreateDocument(bubble), FixedTextMetrics.Measure);
        var shape = Assert.Single(plan.Shapes);

        Assert.Equal(3, shape.Lines.Count);
        Assert.True(shape.Lines[1].Bounds.Height > 0, "空行の高さが 0");
        Assert.Equal(shape.Lines[0].Bounds.Height, shape.Lines[1].Bounds.Height, 3);
        Assert.True(shape.Lines[2].Bounds.Y > shape.Lines[1].Bounds.Y);
    }

    /// <summary>空行だけの本文でも 1 行分の高さを持つことを検証する。</summary>
    [Fact]
    internal void 行の配置_空行だけでも高さが付く()
    {
        var bubble = CreateBubble("\n", TextDefaults.DefaultStyle, width: 600);

        var plan = new AnnotationPresenter().ResolveDrawPlan(CreateDocument(bubble), FixedTextMetrics.Measure);
        var shape = Assert.Single(plan.Shapes);

        Assert.All(shape.Lines, line => Assert.True(line.Bounds.Height > 0, "空行の高さが 0"));
    }

    /// <summary>行間の倍率は 0 以下・非有限を受け付けない（既定値へ補正する）。</summary>
    /// <param name="value">設定する値。</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    internal void 行間の倍率_無効な値は既定値へ補正する(double value)
    {
        var presenter = new AnnotationPresenter { LineHeightFactor = value };

        Assert.Equal(AnnotationPresenter.LineHeightFactorDefault, presenter.LineHeightFactor);
    }

    /// <summary>行間の倍率を変えると、変化後の値を 1 件だけ記録する。</summary>
    [Fact]
    internal void 行間の倍率_変更を設定のログへ記録する()
    {
        var logger = new RecordingLogger();
        _ = new AnnotationPresenter { Log = logger, LineHeightFactor = 1.5 };

        // Log の設定でも 1 件（提示層の生成）が出るため、設定変更の行だけを見る
        var settings = logger.Messages.Where(m => m.Contains("設定:", StringComparison.Ordinal)).ToArray();
        Assert.Single(settings);
        Assert.Equal("[Annotation] 設定: LineHeightFactor 1.25 → 1.5", settings[0]);

        // 設定変更は Information（生成の記録は Debug のため、最後の 1 件だけを見る）
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Information, logger.Levels[^1]);
    }

    /// <summary>同じ値の再設定では記録しない。</summary>
    [Fact]
    internal void 行間の倍率_同じ値では記録しない()
    {
        var logger = new RecordingLogger();
        _ = new AnnotationPresenter
        {
            Log = logger,
            LineHeightFactor = AnnotationPresenter.LineHeightFactorDefault,
        };

        Assert.DoesNotContain(logger.Messages, m => m.Contains("設定:", StringComparison.Ordinal));
    }

    /// <summary>折り返しは書記素クラスタを分割しない（孤立したサロゲートを作らない）。</summary>
    [Fact]
    internal void 折り返し_サロゲートペアを分割しない()
    {
        var style = TextDefaults.DefaultStyle with { FontSize = 20 };
        var bubble = CreateBubble("😀😀😀😀", style, width: 80);

        var plan = new AnnotationPresenter().ResolveDrawPlan(CreateDocument(bubble), FixedTextMetrics.Measure);
        var shape = Assert.Single(plan.Shapes);

        Assert.All(shape.Lines, line =>
        {
            var text = Concat(line);
            Assert.False(text.Length > 0 && char.IsHighSurrogate(text[^1]), "行の末尾が孤立したサロゲート");
            Assert.False(text.Length > 0 && char.IsLowSurrogate(text[0]), "行の先頭が孤立したサロゲート");
        });
        Assert.Equal("😀😀😀😀", string.Concat(shape.Lines.Select(Concat)));
    }

    /// <summary>折り返しは結合文字（基底文字 + 結合記号）を分割しない。</summary>
    [Fact]
    internal void 折り返し_結合文字を分割しない()
    {
        var style = TextDefaults.DefaultStyle with { FontSize = 20 };
        var bubble = CreateBubble("e\u0301e\u0301e\u0301", style, width: 80);

        var plan = new AnnotationPresenter().ResolveDrawPlan(CreateDocument(bubble), FixedTextMetrics.Measure);
        var shape = Assert.Single(plan.Shapes);

        Assert.All(shape.Lines, line => Assert.False(Concat(line).StartsWith('\u0301'), "行の先頭が結合記号"));
        Assert.Equal("e\u0301e\u0301e\u0301", string.Concat(shape.Lines.Select(Concat)));
    }

    /// <summary>検証用の吹き出しを生成する。</summary>
    /// <param name="text">本文。</param>
    /// <param name="style">書式。</param>
    /// <param name="width">本体の幅。</param>
    /// <returns>吹き出し。</returns>
    private static BubbleAnnotation CreateBubble(string text, TextStyle style, double width) => new()
    {
        Id = "bubble",
        Bounds = new EsRect(0, 0, width, 400),
        Appearance = AnnotationDefaults.BubbleAppearance,
        BodyPadding = AnnotationDefaults.BodyPaddingDefault,
        Text = RichText.Create(text, style),
    };

    /// <summary>検証用の文書を生成する。</summary>
    /// <param name="bubble">含める吹き出し。</param>
    /// <returns>文書。</returns>
    private static AnnotationDocument CreateDocument(BubbleAnnotation bubble)
        => new(1920, 1080, "shot.png") { Items = [bubble] };

    /// <summary>行の文字列を連結する。</summary>
    /// <param name="line">行。</param>
    /// <returns>連結した文字列。</returns>
    private static string Concat(TextLinePlan line)
        => string.Concat(line.Segments.Select(segment => segment.Text));
}
