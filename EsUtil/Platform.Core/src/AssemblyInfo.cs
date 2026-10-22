using System.Runtime.InteropServices;

// 本アセンブリの P/Invoke はシステム ディレクトリ（System32）だけを探索する。
// 探索順序を固定しないと、アプリと同じフォルダーへ置かれた同名の偽 DLL を読み込む余地が残る（CA5392）。
// アセンブリ全体に効く宣言のため、フォルダ（名前空間）に属さず src/ 直下に置く。
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]