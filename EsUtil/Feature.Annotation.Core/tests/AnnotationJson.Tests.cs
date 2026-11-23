using DrawingColor = System.Drawing.Color;
using EsUtil.Platform.Core.Geometry;
using Xunit;
using EsUtil.Feature.Annotation.Core.Defaults;

namespace EsUtil.Feature.Annotation.Core.Tests;

/// <summary><see cref="AnnotationJson"/> の仕様テスト（保存形式の互換性）。</summary>
/// <remarks>
/// 保存形式は 4 つの UI 版とアプリが共有する契約である。 一度公開した綴りを変えると、
/// 保存済みの注釈が読めなくなるため、往復と主要な項目の綴りを固定する。
/// </remarks>
public class AnnotationJsonTests
{
    /// <summary>枠線と吹き出しの往復で値が保持されることを検証する。</summary>
    [Fact]
    internal void 往復_枠線と吹き出しの値が保持される()
    {
        var document = CreateDocument();

        var restored = AnnotationJson.Deserialize(AnnotationJson.Serialize(document));

        Assert.Equal(document.ImageWidth, restored.ImageWidth);
        Assert.Equal(document.ImageHeight, restored.ImageHeight);
        Assert.Equal(document.ImageFileName, restored.ImageFileName);
        Assert.Equal(2, restored.Items.Length);

        var frame = Assert.IsType<FrameAnnotation>(restored.Items[0]);
        Assert.Equal("frame-1", frame.Id);
        Assert.Equal(new EsRect(10, 20, 300, 200), frame.Bounds);
        Assert.Equal(DrawingColor.FromArgb(255, 220, 38, 38), frame.Appearance.Border);
        Assert.Equal(6, frame.Appearance.CornerRadius);

        var bubble = Assert.IsType<BubbleAnnotation>(restored.Items[1]);
        Assert.Equal("bubble-1", bubble.Id);
        Assert.Equal(new EsPoint(500, 420), bubble.Tail.Tip);
        Assert.Equal(BubbleEdge.Right, bubble.Tail.BaseEdge);
        Assert.Equal(2, bubble.Text.Runs.Length);
        Assert.Equal("重要", bubble.Text.Runs[1].Text);
        Assert.True(bubble.Text.Runs[1].Style.IsBold);
        Assert.True(bubble.Text.Runs[1].Style.IsUnderline);
    }

    /// <summary>JSON の綴り（種別・矩形・色）が固定されていることを検証する。</summary>
    [Fact]
    internal void 保存形式_種別と矩形と色の綴りが固定されている()
    {
        var json = AnnotationJson.Serialize(CreateDocument());

        // 種別は判別子で保存する（読み込み時に型を決める唯一の手掛かり）
        Assert.Contains("\"Kind\": \"Frame\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Kind\": \"Bubble\"", json, StringComparison.Ordinal);

        // 矩形は X / Y / Width / Height だけ（派生プロパティを保存しない）
        Assert.Contains("\"Width\": 300", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Right\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Center\"", json, StringComparison.Ordinal);

        // 色は設定ファイルと同じ #AARRGGBB
        Assert.Contains("\"#FFDC2626\"", json, StringComparison.Ordinal);
    }

    /// <summary>注釈ファイルと合成画像のパスを検証する。</summary>
    /// <remarks>どちらも<b>元画像の拡張子を置き換える</b>（<c>&lt;ファイル名&gt;.annotations.json</c> / <c>&lt;ファイル名&gt;.annotated.&lt;拡張子&gt;</c>）。</remarks>
    [Fact]
    internal void パス_注釈は拡張子を置き換え合成は拡張子の前へ入る()
    {
        var imagePath = Path.Combine("C:\\", "pictures", "shot.png");

        Assert.Equal(
            Path.Combine("C:\\", "pictures", "shot.annotations.json"),
            AnnotationJson.GetDocumentPath(imagePath));
        Assert.Equal(Path.Combine("C:\\", "pictures", "shot.annotated.png"), AnnotationJson.GetComposedPath(imagePath));
    }

    /// <summary>ディレクトリを持たないパスでも注釈ファイルのパスを求められることを検証する。</summary>
    [Fact]
    internal void パス_ディレクトリが無くても注釈ファイルのパスを求める()
    {
        Assert.Equal("shot.annotations.json", AnnotationJson.GetDocumentPath("shot.png"));
        Assert.Equal("shot.annotated.png", AnnotationJson.GetComposedPath("shot.png"));
    }

    /// <summary>付随ファイルの判定を検証する。</summary>
    /// <remarks>一覧から除く対象は注釈ファイルと合成画像だけである（<c>annotated.png</c> のような元画像は残す）。</remarks>
    [Fact]
    internal void 付随ファイル_注釈と合成だけを判定する()
    {
        Assert.True(AnnotationJson.IsCompanionFile(Path.Combine("C:\\", "shots", "shot.annotations.json")));
        Assert.True(AnnotationJson.IsCompanionFile(Path.Combine("C:\\", "shots", "shot.annotated.png")));
        Assert.False(AnnotationJson.IsCompanionFile(Path.Combine("C:\\", "shots", "shot.png")));
        Assert.False(AnnotationJson.IsCompanionFile(Path.Combine("C:\\", "shots", "annotated.png")));
    }

    /// <summary>未知の種別は読み込み時に例外になることを検証する（前方互換は版で扱う）。</summary>
    [Fact]
    internal void 読み込み_未知の種別は例外になる()
    {
        const string json = """
            {
              "SchemaVersion": 1,
              "ImageWidth": 100,
              "ImageHeight": 100,
              "Items": [ { "Kind": "Arrow", "Id": "a" } ]
            }
            """;

        Assert.ThrowsAny<System.Text.Json.JsonException>(() => AnnotationJson.Deserialize(json));
    }

    /// <summary>検証用の文書を生成する。</summary>
    /// <returns>枠線 1 件と吹き出し 1 件を持つ文書。</returns>
    internal static AnnotationDocument CreateDocument()
    {
        var bold = AnnotationTextStyle.Default with { IsBold = true, IsUnderline = true, Color = DrawingColor.FromArgb(255, 200, 0, 0) };
        var bubble = new BubbleAnnotation
        {
            Id = "bubble-1",
            Bounds = new EsRect(400, 100, 480, 160),
            Appearance = AnnotationDefaults.BubbleAppearance,
            BodyPadding = 10,
            Tail = new BubbleTail(new EsPoint(500, 420), BubbleEdge.Right, 0.4, 24),
            Text = new RichText([
                new TextRun("ここが", AnnotationDefaults.TextStyle),
                new TextRun("重要", bold),
            ]),
        };

        var frame = new FrameAnnotation
        {
            Id = "frame-1",
            Bounds = new EsRect(10, 20, 300, 200),
            Appearance = AnnotationDefaults.FrameAppearance with { CornerRadius = 6 },
        };

        return new AnnotationDocument(1920, 1080, "shot.png") { Items = [frame, bubble] };
    }
}
