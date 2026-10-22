using System.Runtime.InteropServices;

// アセンブリ全体に効く属性だけを持つファイル（名前空間を持たないため src/ の直下に置く）。
// ファイル名を AssemblyInfo.cs にしない理由: SDK が obj/ 配下に <アセンブリ名>.AssemblyInfo.cs を生成するため、
// 検索・diff で両者が混ざる（実測: EsUtil.Platform.Core.AssemblyInfo.cs が obj/Debug と obj/Release に生成される）。
//
// 本アセンブリの P/Invoke はシステム ディレクトリ（System32）だけを探索する。
// 探索順序を固定しないと、アプリと同じフォルダーへ置かれた同名の偽 DLL を読み込む余地が残る（CA5392）。
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]