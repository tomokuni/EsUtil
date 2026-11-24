using EsUtil.Platform.Core.Geometry;
using Xunit;
using EsUtil.Feature.Annotation.Core.Defaults;
using EsUtil.Feature.Annotation.Core.Geometry;

namespace EsUtil.Feature.Annotation.Core.Tests;

/// <summary><see cref="AnnotationCore"/> の付随ファイルの保存規則を検証する。</summary>
public sealed class AnnotationCoreTests
{
    /// <summary>注釈がある場合は注釈ファイルを書き、合成画像の書き出しを呼ぶ。</summary>
    [Fact]
    internal void SaveOrRemoveCompanionFiles_注釈がある場合は保存する()
    {
        using var directory = new TemporaryDirectory();
        var imagePath = Path.Combine(directory.Path, "shot.png");
        var document = new AnnotationDocument(100, 100).Add(CreateFrame());
        var composedCalled = false;

        new AnnotationCore().SaveOrRemoveCompanionFiles(imagePath, document, () =>
        {
            composedCalled = true;
            File.WriteAllText(AnnotationJson.GetComposedPath(imagePath), "composed");
        });

        Assert.True(File.Exists(AnnotationJson.GetDocumentPath(imagePath)));
        Assert.True(composedCalled);
    }

    /// <summary>注釈が 0 件の場合は付随ファイルを削除し、合成画像の書き出しを呼ばない。</summary>
    [Fact]
    internal void SaveOrRemoveCompanionFiles_注釈が無い場合は削除する()
    {
        using var directory = new TemporaryDirectory();
        var imagePath = Path.Combine(directory.Path, "shot.png");
        var documentPath = AnnotationJson.GetDocumentPath(imagePath);
        var composedPath = AnnotationJson.GetComposedPath(imagePath);
        File.WriteAllText(documentPath, "{}");
        File.WriteAllText(composedPath, "composed");
        var composedCalled = false;

        new AnnotationCore().SaveOrRemoveCompanionFiles(
            imagePath, new AnnotationDocument(100, 100), () => composedCalled = true);

        Assert.False(File.Exists(documentPath));
        Assert.False(File.Exists(composedPath));
        Assert.False(composedCalled);
    }

    /// <summary>検証用の枠線を生成する。</summary>
    /// <returns>枠線。</returns>
    private static FrameAnnotation CreateFrame()
        => AnnotationGeometry.CreateFrame(
            new EsRect(10, 10, 20, 20), AnnotationDefaults.FrameAppearance, new EsSize(100, 100));

    /// <summary>検証の間だけ使う一時フォルダー（終了時に中身ごと削除する）。</summary>
    private sealed class TemporaryDirectory : IDisposable
    {
        /// <summary>一時フォルダーのパスを取得する。</summary>
        public string Path { get; } = Directory.CreateTempSubdirectory("annotation-").FullName;

        /// <summary>一時フォルダーを削除する。</summary>
        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
