namespace EsUtil.Feature.Thumbnail;

/// <summary>サムネイルの外観（色・名前の体裁・影）をまとめた不変の値オブジェクト。</summary>
/// <remarks>
/// <b>描画にだけ必要な設定を単一所有する</b>: 色・フォントサイズ・影の濃さはコアの計算・判断に入らないため、<br/>
/// 本型が持つ。 コア（<see cref="ThumbnailOptions"/>）が持つのは長さだけである。<br/>
/// <br/>
/// <b>テーマ</b>: 明色・暗色の両方の色を持ち、<see cref="HoverBackground"/> / <see cref="SelectionBackground"/> が<br/>
/// 現在のテーマに応じた値を返す（UI はテーマの判定だけを行う）。<br/>
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
    public double LabelVerticalPadding
    {
        get;
        init => field = Math.Max(0, value);
    } = 1;

    /// <summary>現在表示中の画像の名前を太字にするかどうかを取得する。</summary>
    /// <value>太字にする場合は true（既定）。</value>
    public bool BoldCurrentLabel { get; init; } = true;

    /// <summary>マウスが重なった項目の背景色（明色テーマ）を取得する。</summary>
    /// <value>色（<c>0xAARRGGBB</c>）。 既定は <c>0xFFDADADA</c>。</value>
    /// <remarks>AvaloniaUI の一覧の項目（<c>ListBoxItem</c>）のホバー背景を実測した値である。</remarks>
    public uint HoverBackgroundLightArgb { get; init; } = 0xFFDADADA;

    /// <summary>マウスが重なった項目の背景色（暗色テーマ）を取得する。</summary>
    /// <value>色（<c>0xAARRGGBB</c>）。 既定は <c>0xFF303030</c>。</value>
    public uint HoverBackgroundDarkArgb { get; init; } = 0xFF303030;

    /// <summary>選択中の項目の背景色（明色テーマ）を取得する。</summary>
    /// <value>色（<c>0xAARRGGBB</c>）。 既定は <c>0x330078D4</c>（アクセント色の 20%）。</value>
    /// <remarks>0 の場合は選択の背景を描かない（名前の色だけで示す UI がある）。</remarks>
    public uint SelectionBackgroundLightArgb { get; init; } = 0x330078D4;

    /// <summary>選択中の項目の背景色（暗色テーマ）を取得する。</summary>
    /// <value>色（<c>0xAARRGGBB</c>）。 既定は <c>0x330078D4</c>。</value>
    /// <remarks>0 の場合は選択の背景を描かない（名前の色だけで示す UI がある）。</remarks>
    public uint SelectionBackgroundDarkArgb { get; init; } = 0x330078D4;

    /// <summary>強調（ホバー・選択）中の名前の色を取得する。</summary>
    /// <value>色（<c>0xAARRGGBB</c>）。 既定は <c>0</c>（UI の標準の文字色を使う）。</value>
    /// <remarks>0 以外を指定すると、ホバー・選択中の名前をこの色で描く（WinForms 版の実測値はアクセント色）。</remarks>
    public uint HighlightedLabelArgb { get; init; }

    /// <summary>強調（ホバー・選択）中の項目の影を隠すかどうかを取得する。</summary>
    /// <value>隠す場合は true。 既定は false。</value>
    public bool HideShadowWhenHighlighted { get; init; }

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
    /// 影の広がり（<see cref="IThumbnailShadowPlan.Insets"/>）が <see cref="ThumbnailOptions.SurroundExtent"/> より<br/>
    /// 広い場合は、レイアウト側の余白が自動で広がる（影が切れない）。
    /// </remarks>
    public IThumbnailShadowPlan? Shadow { get; init; } = ThumbnailShadowPlan.Default;

    /// <summary>ホバーの背景色を取得する。</summary>
    /// <param name="isDark">暗色テーマかどうか。</param>
    /// <returns>色（<c>0xAARRGGBB</c>）。</returns>
    public uint HoverBackground(bool isDark) => isDark ? HoverBackgroundDarkArgb : HoverBackgroundLightArgb;

    /// <summary>選択の背景色を取得する。</summary>
    /// <param name="isDark">暗色テーマかどうか。</param>
    /// <returns>色（<c>0xAARRGGBB</c>）。</returns>
    public uint SelectionBackground(bool isDark) => isDark ? SelectionBackgroundDarkArgb : SelectionBackgroundLightArgb;
}
