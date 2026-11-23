using DrawingColor = System.Drawing.Color;
using System.Text.Json;
using System.Text.Json.Serialization;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Feature.Annotation.Core;

/// <summary>色（<see cref="DrawingColor"/>）の JSON 表現（<c>#AARRGGBB</c>）。</summary>
/// <remarks>
/// <b>設定ファイルと同じ表記にする</b>: 色はアプリの設定（<c>#AARRGGBB</c>）と同じ綴りにして、
/// 利用者が手で直せるようにする（変換の実装は <c>DrawingColorExtensions</c> が単一所有する）。
/// </remarks>
internal sealed class DrawingColorJsonConverter : JsonConverter<DrawingColor>
{
    /// <summary>色を JSON へ書き込む。</summary>
    /// <param name="writer">書き込み先。</param>
    /// <param name="value">色。</param>
    /// <param name="options">直列化のオプション。</param>
    public override void Write(Utf8JsonWriter writer, DrawingColor value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToHexText());

    /// <summary>JSON から色を読み込む。</summary>
    /// <param name="reader">読み込み元。</param>
    /// <param name="typeToConvert">変換する型。</param>
    /// <param name="options">直列化のオプション。</param>
    /// <returns>読み込んだ色。 解釈できない場合は透明。</returns>
    public override DrawingColor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => DrawingColorExtensions.TryParseHexText(reader.GetString(), out var color) ? color : DrawingColor.Transparent;
}
