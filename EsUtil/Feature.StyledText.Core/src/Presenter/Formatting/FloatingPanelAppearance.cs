using DrawingColor = System.Drawing.Color;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.StyledText.Presenter.Formatting;

/// <summary>枠なしウィンドウへ載せるパネル（書式バーなど）の外観。</summary>
/// <remarks>
/// <b>4 つの UI 版で同じ見た目にする</b>: 枠・余白・角丸を本クラスが単一所有し、UI は与えられた値を
/// そのまま適用する（UI 側で枠・余白を足さない）。 UI ごとに値を持つと「余計な枠」「横幅が足りない」
/// といった差が出る。<br/>
/// <b>パネル自身が枠と余白を持つ</b>: ウィンドウ（トップレベル）には飾りを付けない。 ウィンドウの枠を
/// 残すとパネルの枠と二重になり、辺枠が追加されているように見える。<br/>
/// <b>余白は掴む場所でもある</b>: パネルを動かすときの取っ手になる（中身の操作を奪わない）。
/// </remarks>
/// <param name="Background">パネルの下地の色。</param>
/// <param name="Border">パネルの枠の色。</param>
/// <param name="BorderThickness">パネルの枠の太さ（DIP）。 0 は枠を描かない。</param>
/// <param name="CornerRadius">パネルの角丸の半径（DIP）。 0 は角を丸めない。</param>
/// <param name="Padding">パネルの内側の余白の左右（DIP）。</param>
/// <param name="VerticalPadding">パネルの内側の余白の上下（DIP）。</param>
/// <param name="MaxWidth">パネルの幅の上限（DIP）。</param>
/// <param name="MaxContentHeight">中身の高さの上限（DIP）。 超える場合は中身をスクロールさせる。</param>
public sealed record FloatingPanelAppearance(
    DrawingColor Background,
    DrawingColor Border,
    double BorderThickness,
    double CornerRadius,
    double Padding,
    double VerticalPadding,
    double MaxWidth,
    double MaxContentHeight)
{
    /// <summary>中身の高さ（DIP）。 0 は中身に合わせる。</summary>
    /// <value>書式バーは <see cref="TextFormatBarLayout.ItemHeight"/> と同じ値。</value>
    /// <remarks>
    /// <b>高さを固定する理由</b>: 書式バーの中身（コンボボックス・数値入力・色選択）の希望寸法は UI ごとに
    /// 実際の描画サイズとずれる。 実寸で位置を決めると「上へ置いたつもりが本文に重なる」ことが起きるため、
    /// 高さは 1 か所で固定し、置く位置の計算にも同じ値を使う。
    /// </remarks>
    public double FixedContentHeight { get; init; }

    /// <summary>書式バーの外観を取得する。</summary>
    /// <value>下地 <c>#FFFFFF</c> / 枠 <c>#999999</c> / 枠 1 DIP / 角丸 0 DIP / 余白 8,6 DIP / 幅の上限 2400 DIP / 高さの上限 200 DIP。</value>
    /// <remarks>
    /// <b>幅を固定しない</b>: 中身（フォント名・大きさ・色）で幅が決まる。 固定すると項目が枠から
    /// はみ出す。<br/>
    /// <b>角を丸めない</b>: 高さが低いため、丸めると項目の角が欠けて見える。<br/>
    /// <b>余白は掴む場所でもある</b>: パネルを動かす取っ手になるため、枠と余白をはっきり見せ、
    /// 掴める幅を空ける（中身に重ねると掴めない）。
    /// <b>中身の高さは項目の高さと同じにする</b>: これより低いと中身が枠からはみ出して上下の枠線が切れ、
    /// 高いと余白が空く。
    /// </remarks>
    public static FloatingPanelAppearance FormatBar { get; } = new(
        Background: DrawingColor.FromArgb(255, 255, 255, 255),
        Border: DrawingColor.FromArgb(255, 153, 153, 153),
        BorderThickness: 1,
        CornerRadius: 0,
        Padding: 8,
        VerticalPadding: 6,
        MaxWidth: 2400,
        MaxContentHeight: 200)
    {
        FixedContentHeight = TextFormatBarLayout.ItemHeight,
    };

    /// <summary>中身の実寸からパネルの大きさ（DIP）を求める。</summary>
    /// <param name="measuredContent">測った中身の大きさ（DIP）。</param>
    /// <returns>幅は左右の余白と枠、高さは上下の余白と枠を足したパネルの大きさ。</returns>
    /// <remarks>UI ごとに余白と枠を足す計算を書き写さない（片方だけ直すとパネルの大きさが UI 間でずれる）。</remarks>
    public EsSize ResolvePanelSize(EsSize measuredContent)
        => new(
            measuredContent.Width + (Padding * 2) + (BorderThickness * 2),
            ResolvePanelHeight(measuredContent.Height));

    /// <summary>中身の実寸からパネルの高さ（DIP）を求める。</summary>
    /// <param name="measuredContentHeight">測った中身の高さ（DIP）。</param>
    /// <returns>枠と余白を足したパネルの高さ。</returns>
    /// <remarks>
    /// <b>置く位置と実際の高さを同じ規則で決める</b>: <see cref="FixedContentHeight"/> が 0 より大きい場合は
    /// その値を使う（測定値のずれで位置と実寸が食い違い、本文に重なるのを防ぐ）。
    /// </remarks>
    public double ResolvePanelHeight(double measuredContentHeight)
        => ResolveContentHeight(measuredContentHeight) + (VerticalPadding * 2) + (BorderThickness * 2);

    /// <summary>中身の高さ（DIP）を求める（固定値があればそれを使う）。</summary>
    /// <param name="measuredContentHeight">測った中身の高さ（DIP）。</param>
    /// <returns>パネルへ与える中身の高さ。</returns>
    public double ResolveContentHeight(double measuredContentHeight)
        => FixedContentHeight > 0 ? FixedContentHeight : measuredContentHeight;
}
