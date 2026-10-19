using Xunit;

namespace EsUtil.Feature.Thumbnail.Tests;

/// <summary>ログの接頭辞（プロセス全体の状態）を設定するテストのコレクション定義。</summary>
/// <remarks>
/// 接頭辞は定義クラスの <c>static</c> プロパティ（プロセス全体の状態）であるため、<br/>
/// 接頭辞を設定するテストは他のコレクションと並列実行させない（ログ規約 §7）。
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ThumbnailLogPrefix
{
    /// <summary>コレクション名。</summary>
    public const string Name = "ThumbnailPresenterLogPrefix";
}