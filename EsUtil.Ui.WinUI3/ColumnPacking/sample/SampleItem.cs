using Microsoft.UI.Xaml.Data;

namespace EsUtil.Ui.WinUI3.ColumnPacking.Sample;

/// <summary>一覧に並べる 1 件分の項目。</summary>
/// <remarks>
/// <b>クラスにする理由</b>: WinUI の XAML は型情報を生成するため、<c>init</c> だけのプロパティを持つ
/// <c>record</c> では代入できません（生成コードが setter を要求します）。 サンプルは XAML から
/// <c>x:Bind</c> で読むだけですが、型情報の生成を通すために setter を持つクラスにしています。
/// </remarks>
/// <remarks>
/// <c>[Bindable]</c> は XAML の <c>{Binding}</c> を Native AOT でも解決できるようにする宣言です
/// （この属性が無いとリフレクションに頼り、AOT で値が解決されません）。
/// </remarks>
[Bindable]
public sealed class SampleItem
{
    /// <summary>項目を生成する。</summary>
    public SampleItem()
    {
    }

    /// <summary>項目を生成する。</summary>
    /// <param name="label">表示名。</param>
    /// <param name="height">高さ（DIP）。</param>
    public SampleItem(string label, double height)
    {
        Label = label;
        Height = height;
    }

    /// <summary>表示名を取得または設定する。</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>高さ（DIP）を取得または設定する。</summary>
    public double Height { get; set; }
}
