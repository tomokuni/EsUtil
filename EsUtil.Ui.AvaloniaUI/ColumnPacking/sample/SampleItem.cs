namespace EsUtil.Ui.AvaloniaUI.ColumnPacking.Sample;

/// <summary>一覧に並べる 1 件分の項目。</summary>
/// <remarks>
/// <b>XAML のバインディングを使わない理由</b>: 本サンプルはコードだけで画面を組み立てるため、<br/>
/// テンプレートは <c>FuncDataTemplate</c> で作り、値は <c>DataContext</c> から直接読む。<br/>
/// そのため本型は不変（<c>record</c>）でよい。
/// </remarks>
/// <param name="Label">表示名。</param>
/// <param name="Height">高さ（DIP）。</param>
public sealed record SampleItem(string Label, double Height);
