using Xunit;

namespace EsUtil.Ui.AvaloniaUI.Thumbnail.Tests;

/// <summary>Avalonia ヘッドレステストの直列化用コレクション定義。</summary>
/// <remarks>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）は並列実行非対応（Dispatcher.UIThread が共有）のため、<br/>
/// このコレクションに属するテストクラスは直列実行される。 ログの接頭辞（プロセス全体の状態）を設定する<br/>
/// テストも同じコレクションへ置き、他のコレクションと並列実行させない（ログ規約 §7）。
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AvaloniaHeadlessGroup
{
    /// <summary>コレクション名。</summary>
    public const string Name = "AvaloniaHeadless";
}