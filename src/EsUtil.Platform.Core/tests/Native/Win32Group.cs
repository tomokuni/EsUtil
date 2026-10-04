using Xunit;

namespace EsUtil.Platform.Core.Tests.Native;

/// <summary>Win32 API（実 OS）に依存するテストの直列化用コレクション定義。</summary>
/// <remarks>
/// Win32 API（カーソル移動・ウィンドウ/モニター列挙）は実 OS 状態を操作するため、<br/>
/// UI スレッド（Dispatcher）には依存しないが、並列実行すると OS リソースが競合する。<br/>
/// そのため本コレクションで直列化する（Avalonia ヘッドレス初期化は不要）。
/// </remarks>
[CollectionDefinition("Win32", DisableParallelization = true)]
public sealed class Win32Group;
