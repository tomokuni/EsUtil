namespace EsUtil.Feature.Thumbnail;

/// <summary>サムネイル一覧の設定（計算に必要な値。 余白・行の寸法・選択の規則）をまとめた不変の値オブジェクト。</summary>
/// <remarks>
/// <b>設定の既定値も本型が単一所有する</b>（各 UI 実装は既定値を持たず、本型を UI のプロパティへ橋渡しするだけ）。<br/>
/// <see cref="Default"/> を基点に部分変更する（<c>ThumbnailOptions.Default with { ItemPadding = 8 }</c>）と、<br/>
/// 指定しない項目は既定値を保つ。<br/>
/// <br/>
/// <b>描画にだけ必要な設定は含めない</b>: 色・フォントサイズ・影の濃さはコアの計算・判断に入らないため、<br/>
/// 提示層の外観（<c>ThumbnailAppearance</c>）が単一所有する。 本型が持つのは<b>長さ</b>だけである。<br/>
/// <br/>
/// <b>数値の補正</b>: 負の値は 0、<see cref="LabelHeight"/> は 0 以上へ補正する（設定の経路によらず範囲内の値だけを保持する）。
/// </remarks>
public sealed record ThumbnailOptions
{
    /// <summary>すべての項目を既定値にした設定。</summary>
    /// <value>既定の余白・行の寸法（各 UI のプロパティの初期値）。</value>
    public static ThumbnailOptions Default { get; } = new();

    /// <summary>項目の外枠（ホバー帯）と画像の周囲との間に確保する余白を取得する。</summary>
    /// <value>余白（DIP）。 既定は 2。 負の値は 0 として扱う。</value>
    public double ItemMargin
    {
        get;
        init => field = CoerceNonNegative(value);
    } = 2;

    /// <summary>項目の外枠（ホバー帯）の内側に確保する余白を取得する。</summary>
    /// <value>余白（DIP）。 既定は 12。 負の値は 0 として扱う。</value>
    /// <remarks>
    /// ホバー帯を項目の外枠いっぱいに広げるための余白である。 画像の影（<see cref="SurroundExtent"/>）が<br/>
    /// この余白と <see cref="ItemMargin"/> の合計より広い場合は、影が切れないよう <see cref="SlotInset"/> が広がる。
    /// </remarks>
    public double ItemPadding
    {
        get;
        init => field = CoerceNonNegative(value);
    } = 12;

    /// <summary>隣り合う項目の外枠の間に確保する間隔を取得する。</summary>
    /// <value>間隔（DIP）。 既定は 0。 負の値は 0 として扱う。</value>
    public double ItemSpacing
    {
        get;
        init => field = CoerceNonNegative(value);
    }

    /// <summary>名前の行の高さを取得する。</summary>
    /// <value>高さ（DIP）。 既定は 30。 負の値は 0 として扱う。</value>
    /// <remarks>
    /// 名前が 1 行でも 2 行でも高さを変えない（行内で項目の下端を揃えるため）。
    /// </remarks>
    public double LabelHeight
    {
        get;
        init => field = CoerceNonNegative(value);
    } = 30;

    /// <summary>名前の行の左右の余白を取得する。</summary>
    /// <value>余白（DIP）。 既定は 1。 負の値は 0 として扱う。</value>
    public double LabelHorizontalPadding
    {
        get;
        init => field = CoerceNonNegative(value);
    } = 1;

    /// <summary>影の広がり（画像の片側）を取得する。</summary>
    /// <value>影の幅（DIP）。 既定は 10。 負の値は 0 として扱う。</value>
    /// <remarks>
    /// <b>長さだけを持つ</b>: 濃さ・色は提示層の外観が単一所有する。 本値は「影が切れない余白」を<br/>
    /// 確保するためにだけ使う（実際に描く影の広がりは外観が決める。 両者は同じ値にする）。
    /// </remarks>
    public double SurroundExtent
    {
        get;
        init => field = CoerceNonNegative(value);
    } = 10;

    /// <summary>一覧の内容の外側へ確保する余白を取得する。</summary>
    /// <value>余白（DIP）。 既定は 2。 負の値は 0 として扱う。</value>
    public double ContentPadding
    {
        get;
        init => field = CoerceNonNegative(value);
    } = 2;

    /// <summary>画像の幅の下限を取得する。</summary>
    /// <value>幅（DIP）。 既定は 16。 0 以下の指定は既定値へ戻す。</value>
    public double MinimumImageWidth
    {
        get;
        init => field = value > 0 ? value : 16;
    } = 16;

    /// <summary>列数の上限を取得する。</summary>
    /// <value>上限。 既定は 0（無制限）。 0 以下は無制限として扱う。</value>
    public int MaximumColumns
    {
        get;
        init => field = Math.Max(0, value);
    }

    /// <summary>画像の外側（外枠の内側）に確保する余白を取得する。</summary>
    /// <value>片側の余白（DIP）。 <see cref="ItemMargin"/> + <see cref="ItemPadding"/> と <see cref="SurroundExtent"/> の大きい方。</value>
    /// <remarks>
    /// 影はこの余白の中へ描く（余白より広い影は切れる）。 <see cref="ItemPadding"/> を小さくしても<br/>
    /// 影の広がりは失われない。
    /// </remarks>
    public double SlotInset => Math.Max(ItemMargin + ItemPadding, SurroundExtent);

    /// <summary>負の値を 0 にする。</summary>
    /// <param name="value">補正する値。</param>
    /// <returns>0 以上の値。</returns>
    private static double CoerceNonNegative(double value) => double.IsNaN(value) ? 0 : Math.Max(0, value);
}
