using Xunit;

namespace EsUtil.Feature.Marquee.AvaloniaUI.Tests;

/// <summary>Avalonia ヘッドレステストの直列化用コレクション定義。</summary>
/// <remarks>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）は並列実行非対応（Dispatcher.UIThread が共有）のため、<br/>
/// このコレクションに属するテストクラスは直列実行される。<br/>
/// <see cref="Xunit.CollectionDefinitionAttribute.DisableParallelization"/> により、他のコレクション（純粋ロジック <c>[Fact]</c>）と並列実行しないことを保証し、<br/>
/// ServerCompositor のスレッド所有権競合（テストクリーンアップ時の <c>System.InvalidOperationException</c>）を防ぐ。<br/>
/// 純粋ロジックテスト（<c>[Fact]</c>）はコレクションに属さず並列実行され、テスト全体の高速化を維持する。
/// </remarks>
[CollectionDefinition("AvaloniaHeadless", DisableParallelization = true)]
public sealed class AvaloniaHeadlessCollection;
