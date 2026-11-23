using Xunit;

namespace EsUtil.Feature.Annotation.Core.Tests;

/// <summary><see cref="AnnotationJson"/> の読み込みと元画像の解決のテスト。</summary>
/// <remarks>ファイルを使うため、テストごとに一時ディレクトリを作って片付ける。</remarks>
public class AnnotationJsonLoadTests : IDisposable
{
    /// <summary>テスト用のディレクトリ。</summary>
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "EsUtil.AnnotationJson." + Guid.NewGuid().ToString("N"));

    /// <summary>テスト用のディレクトリを作る。</summary>
    public AnnotationJsonLoadTests() => Directory.CreateDirectory(_directory);

    /// <summary>注釈ファイルを保存して読み直すと内容が一致する。</summary>
    [Fact]
    internal void 保存した注釈を読み直せる()
    {
        var document = new AnnotationDocument(200, 120, "shot.png")
            .Add(new FrameAnnotation { Bounds = new(10, 20, 30, 40) });
        var path = Path.Combine(_directory, "shot.annotations.json");
        File.WriteAllText(path, AnnotationJson.Serialize(document));

        var loaded = AnnotationJson.Load(path);

        Assert.Equal(200, loaded.ImageWidth);
        Assert.Equal("shot.png", loaded.ImageFileName);
        Assert.Single(loaded.Items);
    }

    /// <summary>元画像のファイル名から元画像のパスを解決する。</summary>
    [Fact]
    internal void 元画像のファイル名からパスを解決する()
    {
        var imagePath = Path.Combine(_directory, "shot.png");
        File.WriteAllBytes(imagePath, [0x89, 0x50, 0x4E, 0x47]);
        var document = new AnnotationDocument(10, 10, "shot.png");

        var resolved = AnnotationJson.ResolveImagePath(Path.Combine(_directory, "shot.annotations.json"), document);

        Assert.Equal(imagePath, resolved);
    }

    /// <summary>元画像のファイル名が無い場合は注釈ファイルの名前から解決する。</summary>
    [Fact]
    internal void 元画像のファイル名が無い場合は注釈の名前から解決する()
    {
        var imagePath = Path.Combine(_directory, "shot.jpg");
        File.WriteAllBytes(imagePath, [0xFF, 0xD8, 0xFF]);
        var document = new AnnotationDocument(10, 10);

        var resolved = AnnotationJson.ResolveImagePath(Path.Combine(_directory, "shot.annotations.json"), document);

        Assert.Equal(imagePath, resolved);
    }

    /// <summary>元画像が見つからない場合は null を返す。</summary>
    [Fact]
    internal void 元画像が見つからない場合はnullを返す()
    {
        var document = new AnnotationDocument(10, 10);

        var resolved = AnnotationJson.ResolveImagePath(Path.Combine(_directory, "shot.annotations.json"), document);

        Assert.Null(resolved);
    }

    /// <summary>一時ディレクトリを片付ける。</summary>
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }
}
