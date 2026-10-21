using EsUtil.Platform.Core.Svg;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Svg;

/// <summary><see cref="SvgIconSource"/> の仕様テスト（SVG アイコンの読み込み元と解析）。</summary>
/// <remarks>
/// <b>埋め込みリソース</b>は本テスト アセンブリのリソースを使う（テストの <c>.csproj</c> が同封する）。<br/>
/// <b>フォルダー</b>は一時フォルダーへ書き出した SVG を使う（アプリの起動後にファイルが増えても読めること）。<br/>
/// <b>テキスト</b>は XML の解釈（要素の入れ子・属性の順序）を確かめるために使う。
/// </remarks>
public class SvgIconSourceTests
{
    /// <summary>埋め込みリソースの前置き（本テスト アセンブリのリソース名と一致させる）。</summary>
    private const string ResourcePrefix = "EsUtil.Platform.Core.Tests.Svg.";

    [Fact]
    internal void 埋め込みリソース_はパスとviewBoxを返す()
    {
        var source = SvgIconSource.FromEmbeddedResource(typeof(SvgIconSourceTests).Assembly, ResourcePrefix);

        var icon = source.Load("icon_save.svg");

        Assert.Equal("icon_save.svg", icon.FileName);
        Assert.Equal(24, icon.ViewBoxWidth);
        Assert.Equal(24, icon.ViewBoxHeight);
        Assert.NotEmpty(icon.PathData);
        Assert.Equal(0xFF000000u, icon.FillArgb);
    }

    [Fact]
    internal void 埋め込みリソース_は存在しないキーでも空のアイコンを返す()
        => Assert.Empty(SvgIconSource.FromEmbeddedResource(
            typeof(SvgIconSourceTests).Assembly, ResourcePrefix).Load("not_found.svg").PathData);

    [Fact]
    internal void 埋め込みリソース_の一覧は同封したキーを含む()
    {
        var names = SvgIconSource.FromEmbeddedResource(
            typeof(SvgIconSourceTests).Assembly, ResourcePrefix).EnumerateKeys();

        Assert.Contains("icon_save.svg", names);
        Assert.Contains("icon_filled.svg", names);
    }

    [Fact]
    internal void テキスト_は入れ子のpathも文書順に読む()
    {
        var source = SvgIconSource.FromText(
            """
            <svg viewBox="0 0 16 16" xmlns="http://www.w3.org/2000/svg">
              <g><path d="M0 0 L1 1 Z" /></g>
              <path d="M2 2 L3 3 Z" />
            </svg>
            """);

        var icon = source.Load("any");

        Assert.Equal(16, icon.ViewBoxWidth);
        Assert.Equal(16, icon.ViewBoxHeight);
        Assert.Equal(2, icon.PathData.Count);
        Assert.Equal("M0 0 L1 1 Z", icon.PathData[0]);
        Assert.Equal("M2 2 L3 3 Z", icon.PathData[1]);
    }

    [Fact]
    internal void テキスト_はルートのfillを色として読む()
    {
        var source = SvgIconSource.FromText(
            """<svg viewBox="0 0 24 24" fill="#112233"><path d="M0 0 L1 1 Z" /></svg>""");

        Assert.Equal(0xFF112233u, source.Load("any").FillArgb);
    }

    [Fact]
    internal void テキスト_は壊れたXMLでも空のアイコンを返す()
    {
        var source = SvgIconSource.FromText("<svg><path d=");

        Assert.Empty(source.Load("any").PathData);
    }

    [Fact]
    internal void テキスト_はpathが無ければ空のアイコンを返す()
    {
        var source = SvgIconSource.FromText("""<svg viewBox="0 0 24 24"><rect width="24" height="24" /></svg>""");

        Assert.Empty(source.Load("any").PathData);
    }

    [Fact]
    internal void テキスト_はviewBoxが無ければwidthとheightを使う()
    {
        var source = SvgIconSource.FromText("""<svg width="32" height="48"><path d="M0 0 L1 1 Z" /></svg>""");

        var icon = source.Load("any");

        Assert.Equal(32, icon.ViewBoxWidth);
        Assert.Equal(48, icon.ViewBoxHeight);
    }

    [Fact]
    internal void TryLoad_は成否を返す()
    {
        var source = SvgIconSource.FromText("""<svg viewBox="0 0 24 24"><path d="M0 0 L1 1 Z" /></svg>""");

        Assert.True(source.TryLoad("any", out var icon));
        Assert.NotEmpty(icon.PathData);
    }

    [Fact]
    internal void フォルダー_はファイルから読み込み一覧を返す()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"se_svg_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(
                Path.Combine(directory, "a.svg"),
                """<svg viewBox="0 0 24 24"><path d="M0 0 L1 1 Z" /></svg>""");

            var source = SvgIconSource.FromDirectory(directory);

            Assert.NotEmpty(source.Load("a.svg").PathData);
            Assert.Equal(["a.svg"], [.. source.EnumerateKeys()]);
            Assert.Empty(source.Load("b.svg").PathData);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    internal void 解析結果_はキャッシュして共有する()
    {
        var source = SvgIconSource.FromText("""<svg viewBox="0 0 24 24"><path d="M0 0 L1 1 Z" /></svg>""");

        var first = source.Load("any");
        var second = source.Load("any");

        Assert.Same(first.PathData, second.PathData);
    }
}
