using System.Text.Json;
using System.Text.Json.Serialization;

namespace EsUtil.Platform.Core.Geometry;

/// <summary><see cref="EsRectInt"/> の JSON コンバーター（読み取り専用プロパティ対応）。</summary>
/// <remarks>
/// <see cref="EsRectInt"/> は読み取り専用プロパティ（<see cref="EsRectInt.Right"/> 等）を持つため、<br/>
/// 既定のシリアライザーでは書き込み時に不要なプロパティが混入し、読み込み時にコンストラクター引数へ束縛できない。<br/>
/// 本コンバーターは <c>X</c> / <c>Y</c> / <c>Width</c> / <c>Height</c> だけを読み書きする（既存の設定ファイルをそのまま読み込める）。<br/>
/// <b>本ライブラリに置く理由</b>: 保存形式は型の形（読み取り専用プロパティを持つこと）から決まるため、
/// アプリごとに同じコンバーターを書くと<b>保存形式が食い違う</b>。 解釈は本コンバーターが単一所有する。<br/>
/// <see cref="System.Text.Json"/> は .NET の共有フレームワークに含まれるため、外部パッケージは増えない。
/// </remarks>
public sealed class EsRectIntJsonConverter : JsonConverter<EsRectInt>
{
    /// <summary><see cref="EsRectInt"/> を JSON から読み取る。</summary>
    /// <param name="reader">JSON リーダー。</param>
    /// <param name="typeToConvert">変換対象の型。</param>
    /// <param name="options">シリアライズ オプション。</param>
    /// <returns>読み取った矩形。 欠落したプロパティは 0（未知のプロパティは無視する）。</returns>
    public override EsRectInt Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        int x = 0, y = 0, width = 0, height = 0;
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

            var property = reader.GetString();
            reader.Read();
            switch (property)
            {
                case "X": x = reader.GetInt32(); break;
                case "Y": y = reader.GetInt32(); break;
                case "Width": width = reader.GetInt32(); break;
                case "Height": height = reader.GetInt32(); break;
            }
        }

        return new EsRectInt(x, y, width, height);
    }

    /// <summary><see cref="EsRectInt"/> を JSON へ書き込む。</summary>
    /// <param name="writer">JSON ライター。</param>
    /// <param name="value">書き込む矩形。</param>
    /// <param name="options">シリアライズ オプション。</param>
    public override void Write(Utf8JsonWriter writer, EsRectInt value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("X", value.X);
        writer.WriteNumber("Y", value.Y);
        writer.WriteNumber("Width", value.Width);
        writer.WriteNumber("Height", value.Height);
        writer.WriteEndObject();
    }
}
