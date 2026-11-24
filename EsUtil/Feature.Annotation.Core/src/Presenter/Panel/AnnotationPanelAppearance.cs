using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Annotation.Presenter.Editor;

namespace EsUtil.Feature.Annotation.Presenter.Panel;

/// <summary>フローティング パネル（書式バー・右クリックの外観フライアウト）の外観。</summary>
/// <remarks>
/// <b>4 つの UI 版で同じ見た目にする</b>: 枠・余白・幅を本クラスが単一所有し、
/// UI は与えられた値をそのまま適用する（UI 側で枠・余白を足さない）。 UI ごとに値を持つと、
/// 「余計な枠」「横幅が足りない」「ボタンが枠からはみ出す」といった差が出る。<br/>
/// <b>パネル自身が枠と余白を持つ</b>: ウィンドウ（トップレベル）には飾りを付けない。 ウィンドウの枠を
/// 残すと、パネルの枠と二重になり「辺枠が追加されている」ように見える。<br/>
/// <b>書式バーと外観フライアウトで値を分ける</b>: 書式バーは中身（フォント名・大きさ・色）で幅が決まるため
/// 幅を固定せず、外観フライアウトは入力欄の幅を揃えるため固定する。
/// </remarks>
/// <param name="Background">パネルの下地の色（ウィンドウの下地にも同じ色を使う）。</param>
/// <param name="Border">パネルの枠の色。</param>
/// <param name="BorderThickness">パネルの枠の太さ（DIP）。 0 は枠を描かない。</param>
/// <param name="CornerRadius">パネルの角丸の半径（DIP）。 0 は角を丸めない。</param>
/// <param name="Padding">パネルの内側の余白（DIP）。</param>
/// <param name="VerticalPadding">パネルの内側の余白の上下（DIP）。</param>
/// <param name="ContentWidth">中身の幅（DIP）。 0 は中身に合わせる（書式バー）。</param>
/// <param name="MaxWidth">パネルの幅の上限（DIP）。</param>
/// <param name="MaxContentHeight">中身の高さの上限（DIP）。 超える場合は中身をスクロールさせる。</param>
/// <param name="MinContentHeight">中身の最小の高さ（DIP）。 希望寸法がこれより小さくてもこの高さを保つ。</param>
public sealed record AnnotationPanelAppearance(
    DrawingColor Background,
    DrawingColor Border,
    double BorderThickness,
    double CornerRadius,
    double Padding,
    double VerticalPadding,
    double ContentWidth,
    double MaxWidth,
    double MaxContentHeight,
    double MinContentHeight)
{
    /// <summary>書式バーの中身の高さ（DIP）。 0 は中身に合わせる。</summary>
    /// <value>書式バーは <see cref="AnnotationFormatBarLayout.ItemHeight"/> と同じ値、右クリックの外観フライアウトは 0（中身に合わせる）。</value>
    /// <remarks>
    /// <b>高さを固定する理由</b>: 書式バーの中身（コンボボックス・数値入力・色選択）の希望寸法は UI ごとに
    /// 実際の描画サイズとずれる。 実寸で位置を決めると「上へ置いたつもりが注釈に重なる」ことが起きるため、
    /// 書式バーの高さは 1 か所で固定し、置く位置の計算にも同じ値を使う。
    /// </remarks>
    public double FixedContentHeight { get; init; }

    /// <summary>中身の実寸からパネルの高さ（DIP）を求める。</summary>
    /// <param name="measuredContentHeight">測った中身の高さ（DIP）。</param>
    /// <returns>枠と余白を足したパネルの高さ。</returns>
    /// <remarks>
    /// <b>置く位置と実際の高さを同じ規則で決める</b>: <see cref="FixedContentHeight"/> が 0 より大きい場合は
    /// その値を使う（測定値のずれで位置と実寸が食い違い、注釈に重なるのを防ぐ）。
    /// </remarks>
    public double ResolvePanelHeight(double measuredContentHeight)
        => ResolveContentHeight(measuredContentHeight)
            + (VerticalPadding * 2) + (BorderThickness * 2);

    /// <summary>中身の高さ（DIP）を求める（固定値があればそれを使う）。</summary>
    /// <param name="measuredContentHeight">測った中身の高さ（DIP）。</param>
    /// <returns>パネルへ与える中身の高さ。</returns>
    public double ResolveContentHeight(double measuredContentHeight)
        => FixedContentHeight > 0 ? FixedContentHeight : measuredContentHeight;

    /// <summary>右クリックの外観フライアウトの外観を取得する。</summary>
    /// <value>下地 <c>#FFFFFF</c> / 枠 <c>#999999</c> / 枠 1 DIP / 角丸 4 DIP / 余白 10 DIP / 中身の幅 290 DIP / 幅の上限 640 DIP / 高さの上限 720 DIP。</value>
    /// <remarks>
    /// <b>AvaloniaUI 版の見た目に合わせる</b>: 4 つの UI 版の基準は AvaloniaUI 版である。
    /// 下地は白、枠は <c>#999999</c> の 1 DIP、内側の余白は 10 DIP。
    /// </remarks>
    public static AnnotationPanelAppearance StyleEditor { get; } = new(
        Background: DrawingColor.FromArgb(255, 255, 255, 255),
        Border: DrawingColor.FromArgb(255, 153, 153, 153),
        BorderThickness: StyleEditorBorder,
        CornerRadius: 4,
        Padding: StyleEditorPadding,
        VerticalPadding: StyleEditorPadding,
        ContentWidth: ResolveStyleEditorContentWidth(),
        MaxWidth: 640,
        MaxContentHeight: 720,
        MinContentHeight: 0)
    {
        FixedContentHeight = 0,
    };

    /// <summary>外観フライアウトの内側の余白（DIP）。</summary>
    private const double StyleEditorPadding = 10;

    /// <summary>外観フライアウトの枠の太さ（DIP）。</summary>
    private const double StyleEditorBorder = 1;

    /// <summary>右クリックの外観フライアウトの中身の幅（DIP）を求める。</summary>
    /// <returns>幅（DIP）。</returns>
    /// <remarks>
    /// <b>項目名と入力欄から決める</b>: 入力欄の幅を揃えたうえで、その合計に余白と枠を足す。
    /// 固定値にすると、入力欄の右側に使わない幅が空いて「間延びした」見た目になる。
    /// </remarks>
    private static double ResolveStyleEditorContentWidth()
        => AnnotationEditorLayout.LabelWidth
            + AnnotationEditorLayout.RowSpacing
            + AnnotationEditorLayout.FieldWidth
            + (StyleEditorPadding * 2)
            + (StyleEditorBorder * 2);

    /// <summary>書式バーの外観を取得する。</summary>
    /// <value>下地 <c>#FFFFFF</c> / 枠 <c>#999999</c> / 枠 1 DIP / 角丸 0 DIP / 余白 6,3 DIP / 幅は中身に合わせる / 幅の上限 2400 DIP / 高さの上限 200 DIP。</value>
    /// <remarks>
    /// <b>幅を固定しない</b>: 中身（フォント名・大きさ・色）で幅が決まる。 固定すると項目が枠から
    /// はみ出す（実測 2026-11-20）。<br/>
    /// <b>角を丸めない</b>: 高さが低いため、丸めると項目の角が欠けて見える。<br/>
    /// <b>中身の高さは項目の高さと同じにする</b>: 項目の高さは
    /// <see cref="AnnotationFormatBarLayout.ItemHeight"/> が単一所有する。 これより低いと中身が
    /// 枠からはみ出して上下の枠線が切れ、高いと余白が空く。 固定値も同じ値に揃える。
    /// </remarks>
    public static AnnotationPanelAppearance FormatBar { get; } = new(
        Background: DrawingColor.FromArgb(255, 255, 255, 255),
        Border: DrawingColor.FromArgb(255, 153, 153, 153),
        BorderThickness: 1,
        CornerRadius: 0,
        Padding: 6,
        VerticalPadding: 3,
        ContentWidth: 0,
        MaxWidth: 2400,
        MaxContentHeight: 200,
        MinContentHeight: AnnotationFormatBarLayout.ItemHeight)
    {
        FixedContentHeight = AnnotationFormatBarLayout.ItemHeight,
    };
}
