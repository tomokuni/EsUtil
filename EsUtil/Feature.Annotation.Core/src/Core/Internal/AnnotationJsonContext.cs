using System.Text.Json.Serialization;

namespace EsUtil.Feature.Annotation.Core.Internal;

/// <summary>注釈の JSON ソース生成コンテキスト（Native AOT / トリミング対応）。</summary>
/// <remarks>
/// <b>反射ベースの直列化を使わない</b>: 反射はトリミングでプロパティが削除され、保存した注釈が空になる
/// （設定ファイルと同じ理由）。<br/>
/// <b>本プロジェクトに置ける理由</b>: 注釈のモデルは CommunityToolkit.Mvvm のソース生成を使わない
/// plain な型であるため、設定のコンテキスト（UI 側に置く必要がある）と事情が異なる。
/// </remarks>
[JsonSerializable(typeof(AnnotationDocument))]
internal sealed partial class AnnotationJsonContext : JsonSerializerContext;
