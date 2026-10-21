using System.Runtime.InteropServices;

// 本アセンブリの P/Invoke はシステム ディレクトリ（System32）だけを探索する。
// 探索順序を固定しないと、アプリと同じフォルダーへ置かれた同名の偽 DLL を読み込む余地が残る（CA5392）。
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
