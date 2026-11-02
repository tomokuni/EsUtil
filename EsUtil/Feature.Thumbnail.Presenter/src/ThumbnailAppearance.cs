namespace EsUtil.Feature.Thumbnail;

/// <summary>サムネイルの外観（色・名前の体裁・影）をまとめた不変の値オブジェクト。</summary>
/// <remarks>
/// <b>描画にだけ必要な設定を単一所有する</b>: 色・フォントサイズ・影の濃さはコアの計算・判断に入らないため、<br/>
/// 本型が持つ。 コア（<see cref="ThumbnailOptions"/>）が持つのは長さだけである。<br/>
/// <br/>
/// <b>テーマ</b>: 帯の色そのものは持たない。 計画は <see cref="ThumbnailHighlightKind"/>（意味）だけを運び、<br/>
/// UI が自分のテーマから解決した文字色・アクセント色を <see cref="ResolveHighlightArgb"/> へ渡す<br/>
/// （明暗 2 色の固定表を持つとテーマの実際の値と一致せず、利用者のアクセント変更にも追随できない）。<br/>
/// <br/>
/// <b>既定値</b>: <see cref="Default"/> を基点に部分変更する（<c>ThumbnailAppearance.Default with { ... }</c>）。<br/>
/// <br/>
/// <b>等価性</b>: レコードの値等価性により、同値の外観を再代入しても UI 側の再描画は発生しない。
/// </remarks>
public sealed record ThumbnailAppearance
{
    /// <summary>すべての項目を既定値にした外観。</summary>
    public static ThumbnailAppearance Default { get; } = new();

    /// <summary>名前の文字サイズを取得する。</summary>
    /// <value>文字サイズ（DIP）。 既定は 11。 0 以下の指定は既定値へ戻す。</value>
    public double LabelFontSize
    {
        get;
        init => field = value > 0 ? value : 11;
    } = 11;

    /// <summary>名前の行の上下の余白を取得する。</summary>
    /// <value>余白（DIP）。 既定は 1。 負の値は 0 として扱う。</value>
    /// <remarks>
    /// <b>名前の行の高さは本値を含む</b>: 提示層が <c>ResolveMetrics</c> で 2 行ぶんの高さへ本値を足すため、<br/>
    /// 本値を増やしても文字を置ける高さ（2 行ぶん）は変わらない（文字が切れない）。
    /// </remarks>
    public double LabelVerticalPadding
    {
        get;
        init => field = Math.Max(0, value);
    } = 1;

    /// <summary>現在表示中の画像の名前を太字にするかどうかを取得する。</summary>
    /// <value>太字にする場合は true（既定）。</value>
    public bool BoldCurrentLabel { get; init; } = true;

    /// <summary>ホバーの帯の不透明度を取得する。</summary>
    /// <value>0〜1。 既定は <c>0.10</c>（テーマの文字色を 10% で敷く）。</value>
    /// <remarks>帯の色そのものは持たない（UI のテーマの文字色を使う。 <see cref="ResolveHighlightArgb"/>）。</remarks>
    public double HoverHighlightOpacity
    {
        get;
        init => field = Math.Clamp(value, 0, 1);
    } = 0.10;

    /// <summary>選択の帯の不透明度を取得する。</summary>
    /// <value>0〜1。 既定は <c>0.20</c>（テーマのアクセント色を 20% で敷く）。</value>
    /// <remarks>帯の色そのものは持たない（UI のテーマのアクセント色を使う。 <see cref="ResolveHighlightArgb"/>）。</remarks>
    public double SelectionHighlightOpacity
    {
        get;
        init => field = Math.Clamp(value, 0, 1);
    } = 0.20;

    /// <summary>強調（ホバー・選択）中の名前の色を取得する。</summary>
    /// <value>色（<c>0xAARRGGBB</c>）。 既定は <c>0</c>（UI の標準の文字色を使う）。</value>
    /// <remarks>0 以外を指定すると、ホバー・選択中の名前をこの色で描く（WinForms 版の実測値はアクセント色）。</remarks>
    public uint HighlightedLabelArgb { get; init; }

    /// <summary>項目の影を描くかどうかを取得する。</summary>
    /// <value>描く場合は true（既定）。 false の場合は <see cref="Shadow"/> の設定によらず影を描かない。</value>
    /// <remarks>
    /// <b>影の算法（<see cref="Shadow"/>）と表示可否を分ける</b>: 算法を差し替えたまま表示だけを止められる<br/>
    /// （算法を null にすると差し替えた設定を失う。 表示可否は本プロパティが単一所有する）。<br/>
    /// 影の広がり（<see cref="IThumbnailShadowPlan.Insets"/>）は表示しない場合も配置の計算へ渡さない<br/>
    /// （余白だけが残ると項目の間が不自然に空く）。
    /// </remarks>
    public bool ShowShadow { get; init; } = true;

    /// <summary>強調（ホバー・選択）中の項目の影を隠すかどうかを取得する。</summary>
    /// <value>隠す場合は true。 既定は false。</value>
    public bool HideShadowWhenSelected { get; init; }

    /// <summary>ホバーの背景を描くかどうかを取得する。</summary>
    /// <value>描く場合は true（既定）。</value>
    public bool ShowHoverBackground { get; init; } = true;

    /// <summary>選択の背景を描くかどうかを取得する。</summary>
    /// <value>描く場合は true（既定）。</value>
    public bool ShowSelectionBackground { get; init; } = true;

    /// <summary>項目の影の計画を取得する。</summary>
    /// <value>影の計画。 既定は <see cref="ThumbnailShadowPlan.Default"/>。 null の場合は影を描かない。</value>
    /// <remarks>
    /// 影の算法を変える場合は <see cref="IThumbnailShadowPlan"/> を実装した値を渡す（差し替え点）。<br/>
    /// 影の広がり（<see cref="IThumbnailShadowPlan.Insets"/>）が、影が切れない余白として配置の計算へ渡される<br/>
    /// 広い場合は、レイアウト側の余白が自動で広がる（影が切れない）。
    /// </remarks>
    public IThumbnailShadowPlan? Shadow { get; init; } = ThumbnailShadowPlan.Default;

    /// <summary>強調の帯の色を解決する。</summary>
    /// <param name="kind">強調の種類（<see cref="ThumbnailHighlightKind.None"/> の場合は 0）。</param>
    /// <param name="textArgb">UI のテーマが決めた文字色（<c>0xAARRGGBB</c>）。 ホバーはこの色を薄く敷く。</param>
    /// <param name="accentArgb">UI のテーマが決めたアクセント色（<c>0xAARRGGBB</c>）。 選択はこの色を薄く敷く。</param>
    /// <returns>帯の色（<c>0xAARRGGBB</c>）。 0 の場合は帯を描かない。</returns>
    /// <remarks>
    /// <b>色は UI のテーマから受け取る</b>: 明暗で固定の 2 色を持つとテーマの実際の値と一致せず、利用者がアクセント色を<br/>
    /// 変えたときにも追随できない。 本メソッドは「どの色をどの濃さで敷くか」だけを持ち、色そのものは UI が解決する。<br/>
    /// アクセント色が取れない UI（<paramref name="accentArgb"/> が 0）は文字色で代用する。
    /// </remarks>
    public uint ResolveHighlightArgb(ThumbnailHighlightKind kind, uint textArgb, uint accentArgb)
    {
        if (kind == ThumbnailHighlightKind.None)
        {
            return 0;
        }

        var source = kind == ThumbnailHighlightKind.Selected && accentArgb != 0 ? accentArgb : textArgb;
        var opacity = kind == ThumbnailHighlightKind.Selected ? SelectionHighlightOpacity : HoverHighlightOpacity;
        var alpha = (uint)Math.Round(((source >> 24) & 0xFF) * opacity);
        return alpha == 0 ? 0 : (alpha << 24) | (source & 0x00FFFFFF);
    }
}
