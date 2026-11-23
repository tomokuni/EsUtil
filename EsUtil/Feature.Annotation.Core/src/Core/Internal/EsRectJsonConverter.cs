using System.Text.Json;
using System.Text.Json.Serialization;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation.Core.Internal;

/// <summary>矩形（<see cref="EsRect"/>）の JSON 表現（<c>X</c> / <c>Y</c> / <c>Width</c> / <c>Height</c> だけ）。</summary>
/// <remarks>
/// <b>派生プロパティを保存しない</b>: <see cref="EsRect"/> は <c>Right</c> / <c>Bottom</c> / <c>Center</c> 等を
/// 公開するため、既定の直列化では不要な項目が並び、読み込み時に無視される値が増える。<br/>
/// <b>区間の種類は保存しない</b>（注釈の矩形は常に半開区間として扱う。 閉区間で保存すると
/// 隣接する矩形の幅が 1 違って見える）。
/// </remarks>
internal sealed class EsRectJsonConverter : JsonConverter<EsRect>
{
    /// <summary>矩形を JSON へ書き込む。</summary>
    /// <param name="writer">書き込み先。</param>
    /// <param name="value">矩形。</param>
    /// <param name="options">直列化のオプション。</param>
    public override void Write(Utf8JsonWriter writer, EsRect value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("X", value.X);
        writer.WriteNumber("Y", value.Y);
        writer.WriteNumber("Width", value.Width);
        writer.WriteNumber("Height", value.Height);
        writer.WriteEndObject();
    }

    /// <summary>JSON から矩形を読み込む。</summary>
    /// <param name="reader">読み込み元。</param>
    /// <param name="typeToConvert">変換する型。</param>
    /// <param name="options">直列化のオプション。</param>
    /// <returns>読み込んだ矩形。 欠落した項目は 0。</returns>
    public override EsRect Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var x = 0.0;
        var y = 0.0;
        var width = 0.0;
        var height = 0.0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "X": x = reader.GetDouble(); break;
                case "Y": y = reader.GetDouble(); break;
                case "Width": width = reader.GetDouble(); break;
                case "Height": height = reader.GetDouble(); break;
                default: reader.Skip(); break;
            }
        }

        return new EsRect(x, y, width, height);
    }
}
