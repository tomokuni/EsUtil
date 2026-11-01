using Microsoft.UI.Xaml;

namespace EsUtil.Ui.WinUI3.ColumnPacking.Sample;

/// <summary>ItemsSource から子を作るときの DataTemplate を持つリソース ディクショナリ。</summary>
/// <remarks>
/// <c>{x:Bind}</c> はコンパイル時に型を解決するため、XAML 単体では使えません（コードビハインドを持つ型が必要です）。
/// テンプレートを C# で組み立てると <c>XamlReader</c>（AOT 非対応）が必要になるため、XAML のリソースとして置きます。
/// </remarks>
public sealed partial class ItemTemplates : ResourceDictionary
{
    /// <summary>リソース ディクショナリを初期化する。</summary>
    public ItemTemplates() => InitializeComponent();
}