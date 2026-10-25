namespace EsUtil.Ui.WinUI3.MultiColumn.Sample;

/// <summary>一覧に並べる 1 件分の項目。</summary>
/// <remarks>
/// <b>クラスにする理由</b>: WinUI の XAML は型情報を生成するため、<c>init</c> だけのプロパティを持つ
/// <c>record</c> では代入できません（生成コードが setter を要求します）。 サンプルは XAML から
/// <c>x:Bind</c> で読むだけですが、型情報の生成を通すために setter を持つクラスにしています。
/// </remarks>
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