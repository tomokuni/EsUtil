using System.Text.Json;
using System.Text.Json.Serialization;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation.Core.Internal;

/// <summary>点（<see cref="EsPoint"/>）の JSON 表現（<c>X</c> / <c>Y</c> だけ）。</summary>
/// <remarks>矩形と同じく、派生プロパティを保存しない（<see cref="EsRectJsonConverter"/> と同じ理由）。</remarks>
internal sealed class EsPointJsonConverter : JsonConverter<EsPoint>
{
    /// <summary>点を JSON へ書き込む。</summary>
    /// <param name="writer">書き込み先。</param>
    /// <param name="value">点。</param>
    /// <param name="options">直列化のオプション。</param>
    public override void Write(Utf8JsonWriter writer, EsPoint value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("X", value.X);
        writer.WriteNumber("Y", value.Y);
        writer.WriteEndObject();
    }

    /// <summary>JSON から点を読み込む。</summary>
    /// <param name="reader">読み込み元。</param>
    /// <param name="typeToConvert">変換する型。</param>
    /// <param name="options">直列化のオプション。</param>
    /// <returns>読み込んだ点。 欠落した項目は 0。</returns>
    public override EsPoint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var x = 0.0;
        var y = 0.0;

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
                default: reader.Skip(); break;
            }
        }

        return new EsPoint(x, y);
    }
}
