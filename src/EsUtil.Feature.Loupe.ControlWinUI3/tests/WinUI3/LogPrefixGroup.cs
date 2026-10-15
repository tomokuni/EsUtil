using Xunit;

namespace EsUtil.Feature.Loupe.WinUI3.Tests;

/// <summary>ログの接頭辞（プロセス全体の状態）を設定するテストを直列化するグループ。</summary>
/// <remarks>
/// 接頭辞はログ定義クラスの <c>static</c> プロパティ（プロセス全体の状態）であり、<br/>
/// 並列に実行すると他のテストの出力書式の検証と競合する（実測）。<br/>
/// 接頭辞を設定するテストクラスへ本グループ（<c>[Collection]</c>）を付けて直列化する<br/>
/// （<c>DisableParallelization</c> により他のグループとも並列にしない）。
/// </remarks>
[CollectionDefinition("LoupeWinUI3LogPrefix", DisableParallelization = true)]
public class LogPrefixGroup;
