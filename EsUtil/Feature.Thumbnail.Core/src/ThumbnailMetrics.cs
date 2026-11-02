namespace EsUtil.Feature.Thumbnail;

/// <summary>配置の計算に使う「外観から決まる長さ」（利用者が設定しない寸法）。</summary>
/// <remarks>
/// <b>なぜ設定（<see cref="ThumbnailOptions"/>）と分けるか</b>: 影の余白と名前の行の高さは、<br/>
/// 利用者が決める値ではなく<b>外観（影の算法・文字サイズ）から決まる</b>値である。 設定へ混ぜると、<br/>
/// 利用者が設定しても無視される項目（設定したのに効かない）が生まれ、設定の意味が濁る。<br/>
/// そこで本型へ分け、提示層が外観から求めて配置の計算へ渡す。<br/>
/// <br/>
/// <b>座標系</b>: すべて DIP。 負の値は 0 として扱う。
/// </remarks>
public readonly record struct ThumbnailMetrics
{
    /// <summary>すべて 0 の寸法（影も名前の行も確保しない）。</summary>
    /// <value>余白 0・名前の行 0 の寸法。</value>
    public static ThumbnailMetrics Zero { get; } = new(0, 0);

    /// <summary>寸法を生成する。</summary>
    /// <param name="slotInset">画像の外側（外枠の内側）に確保する余白（片側。 影が切れない幅）。</param>
    /// <param name="labelHeight">名前の行の高さ。</param>
    /// <remarks>負の値は 0 へ補正する（設定の経路によらず範囲内の値だけを保持する）。</remarks>
    public ThumbnailMetrics(double slotInset, double labelHeight)
    {
        SlotInset = Coerce(slotInset);
        LabelHeight = Coerce(labelHeight);
    }

    /// <summary>画像の外側（外枠の内側）に確保する余白を取得する。</summary>
    /// <value>片側の余白（DIP）。 影はこの余白の中へ描く（余白より広い影は切れる）。</value>
    public double SlotInset { get; }

    /// <summary>名前の行の高さを取得する。</summary>
    /// <value>高さ（DIP）。 文字が切れない 2 行ぶんに上下の余白を足した値であり、名前が 1 行でも高さを変えない（行内で項目の下端を揃えるため）。</value>
    public double LabelHeight { get; }

    /// <summary>負の値と非数を 0 にする。</summary>
    /// <param name="value">補正する値。</param>
    /// <returns>0 以上の値。</returns>
    private static double Coerce(double value) => double.IsFinite(value) ? Math.Max(0, value) : 0;
}
