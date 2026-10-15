namespace EsUtil.Feature.Marquee.WinForms.Sample;

/// <summary>サンプル アプリケーションのエントリポイント。</summary>
/// <remarks>
/// <b>必須の初期化（この順序で行う）</b>:<br/>
/// 1. <c>Application.SetHighDpiMode(PerMonitorV2)</c>（DPI 対応。 呼ばないとコントロールの描画が実寸とずれる）<br/>
/// 2. <c>Application.EnableVisualStyles()</c>（視覚スタイル。 ウィンドウ生成より前に呼ぶ）<br/>
/// 3. <c>Application.SetCompatibleTextRenderingDefault(false)</c>（コントロール生成より前に呼ぶ）
/// </remarks>
internal static class Program
{
    /// <summary>サンプル アプリケーションを起動する。</summary>
    [STAThread]
    private static void Main()
    {
        // Per-Monitor V2 の DPI 対応を有効化する（コントロールの描画が DPI へ追随するために必要）
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Application.Run(new MainForm());
    }
}
