namespace EsUtil.Ui.MewUI.ColumnPacking.Sample;

/// <summary>一覧に並べる 1 件分の項目。</summary>
/// <remarks>
/// <b>MewUI のテンプレートは要素を作ってから項目を束縛する</b>ため、値は束縛の時点で要素へ写す。<br/>
/// そのため本型は不変（<c>record</c>）でよい。
/// </remarks>
/// <param name="Label">表示名。</param>
/// <param name="Height">高さ（DIP）。</param>
public sealed record SampleItem(string Label, double Height);
