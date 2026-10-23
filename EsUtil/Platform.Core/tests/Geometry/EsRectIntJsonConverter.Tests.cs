using System.Text.Json;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Geometry;

/// <summary><see cref="EsRectIntJsonConverter"/> の単体テスト（保存形式の互換）。</summary>
/// <remarks>
/// JSON の読み書きのみを検証するため UI 初期化に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// 既存の設定ファイルと同じ JSON 形式（<c>X</c> / <c>Y</c> / <c>Width</c> / <c>Height</c>）を保つことを検証する。
/// </remarks>
public class EsRectIntJsonConverterTests
{
    /// <summary>本コンバーターを登録したシリアライズ オプション。</summary>
    private static readonly JsonSerializerOptions s_options = new()
    {
        Converters = { new EsRectIntJsonConverter() },
    };

    [Fact]
    internal void Write_XYWidthHeightのみを書き出す()
    {
        var rect = new EsRectInt(444, 230, 1284, 1032);

        var json = JsonSerializer.Serialize(rect, s_options);

        Assert.Equal("""{"X":444,"Y":230,"Width":1284,"Height":1032}""", json);
    }

    [Fact]
    internal void Read_XYWidthHeightを読み取る()
    {
        var rect = JsonSerializer.Deserialize<EsRectInt>(
            """{"X":444,"Y":230,"Width":1284,"Height":1032}""", s_options);

        Assert.Equal(new EsRectInt(444, 230, 1284, 1032), rect);
    }

    [Fact]
    internal void 往復変換で値が一致する()
    {
        var rect = new EsRectInt(-100, -50, 1920, 1080);

        var json = JsonSerializer.Serialize(rect, s_options);
        var back = JsonSerializer.Deserialize<EsRectInt>(json, s_options);

        Assert.Equal(rect, back);
    }

    [Fact]
    internal void Read_欠落したプロパティは0になる()
    {
        var rect = JsonSerializer.Deserialize<EsRectInt>("""{"X":10,"Width":20}""", s_options);

        Assert.Equal(new EsRectInt(10, 0, 20, 0), rect);
    }

    [Fact]
    internal void Read_未知のプロパティは無視する()
    {
        var rect = JsonSerializer.Deserialize<EsRectInt>(
            """{"X":1,"Y":2,"Width":3,"Height":4,"Right":4,"Unknown":99}""", s_options);

        Assert.Equal(new EsRectInt(1, 2, 3, 4), rect);
    }

    [Fact]
    internal void Read_順序が異なっても読み取る()
    {
        var rect = JsonSerializer.Deserialize<EsRectInt>(
            """{"Height":40,"Width":30,"Y":20,"X":10}""", s_options);

        Assert.Equal(new EsRectInt(10, 20, 30, 40), rect);
    }
}