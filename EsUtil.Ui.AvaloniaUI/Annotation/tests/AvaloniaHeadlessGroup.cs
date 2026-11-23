using Xunit;

namespace EsUtil.Ui.AvaloniaUI.Annotation.Tests;

/// <summary>Avalonia ヘッドレステストの直列化用コレクション定義。</summary>
/// <remarks>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）は並列実行非対応（Dispatcher.UIThread が共有）のため、
/// このコレクションに属するテストクラスは直列実行される。
/// </remarks>
[CollectionDefinition("AvaloniaHeadless", DisableParallelization = true)]
public sealed class AvaloniaHeadlessGroup;
