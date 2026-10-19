using static EsUtil.Feature.Loupe.LoupeCore.Limits;
using DrawingColor = System.Drawing.Color;


namespace EsUtil.Feature.Loupe;

/// <summary>ルーペの外観（色・フォント・角丸・枠線の太さ・表示可否）をまとめた不変の設定値。</summary>
/// <remarks>
/// <b>なぜ本層が持つか</b>: 色・フォントサイズ・角丸・寸法（枠線の太さ）・表示可否は<b>描画にだけ必要な設定</b>であり、<br/>
/// コアの計算・判定には一切入らないため、コア（<c>EsUtil.Feature.Loupe.Core</c>）には置かない。<br/>
/// コアは計算に必要な値（表示サイズ・拡大率）と幾何だけを持ち、外観は本型が単一所有する。<br/>
/// <b>ラベルのフォントサイズ・角丸も同じ理由で本型が持つ</b>: ラベルをどの体裁（文字要素 / 画像）で描くかは UI 層の判断であり、<br/>
/// 描画層は文言と位置だけを渡す（<c>LoupeTextPlacement</c>）。<br/>
/// <br/>
/// <b>描画の可否</b>: 描くかどうかは本型の明示フラグ（<see cref="ShowCrosshair"/> / <see cref="ShowBorder"/>）が単一所有する<br/>
/// （非表示の項目は描画層が空の矩形として返すため、描画実装は分岐を持たない）。<br/>
/// <br/>
/// <b>ペーストボード（画像外の色）</b>: 拡大画像の範囲外（画像端の外側と切り上げの余りを含む）の見た目を与える。<br/>
/// 描画層は本色で描画面全体を塗ってから拡大画像を重ねるため、画像の無い部分は本色の無地になる。<br/>
/// 既定は不透明の中性グレー（画像外を無地として見せる）。<br/>
/// <br/>
/// <b>色の型</b>: UI 非依存の <see cref="DrawingColor"/> を使用し、各 UI の色型への変換は境界（描画）で行う。<br/>
/// 既定値は部分変更の基点（<c>LoupeAppearance.Default with { ... }</c>）として使う。
/// </remarks>
public sealed record LoupeAppearance
{
    /// <summary>中心座標ラベルの文字色の既定値（白）。</summary>
    /// <value>アルファを含む色。 既定は白（不透明）。</value>
    private static readonly DrawingColor s_labelTextColorDefault = DrawingColor.White;

    /// <summary>中心座標ラベルの背景色の既定値（黒 60%）。</summary>
    /// <value>アルファを含む色。 既定は黒 60%（<c>#99000000</c>）。</value>
    /// <remarks>背景はチップとして文字を読ませるための帯であり、下の画像を完全には隠さない濃度にする。</remarks>
    private static readonly DrawingColor s_labelBackgroundColorDefault = DrawingColor.FromArgb(153, 0, 0, 0);

    /// <summary>すべての色を既定値にした設定。</summary>
    /// <value>既定の色（部分変更の基点として使用する）。</value>
    public static LoupeAppearance Default { get; } = new();

    /// <summary>画像外（ペーストボード）の色を取得する。</summary>
    /// <value>アルファを含む色。 既定は不透明の中性グレー（<c>#FF535353</c>）。</value>
    /// <remarks>描画層は本色で描画面全体を塗ってから拡大画像を重ねる（画像の無い部分が本色の無地になる）。</remarks>
    public DrawingColor PasteboardColor { get; init; } = DrawingColor.FromArgb(255, 0x53, 0x53, 0x53);

    /// <summary>画像外（ペーストボード）を表示するかどうかを取得する。</summary>
    /// <value>true（既定）の場合は <see cref="PasteboardColor"/> で描画面全体を塗る。 false の場合は塗らない（透明）。</value>
    /// <remarks>
    /// 描画の可否を本値が単一所有する（色のアルファを「描くかどうか」の代用にしない）。<br/>
    /// <b>非表示でも描画層は描画面を塗る</b>（色を透明にする）: 描画面を保持する UI では前フレームの内容が残るため、<br/>
    /// 層を省くとゴーストになる（<see cref="ResolvePasteboardColor"/> が透明を返し、その色で塗る）。
    /// </remarks>
    public bool ShowPasteboard { get; init; } = true;

    /// <summary>描画層が描画面を塗る色（ペーストボード）を解決する。</summary>
    /// <returns>表示する場合は <see cref="PasteboardColor"/>。 非表示の場合は透明。</returns>
    /// <remarks>
    /// 判断と色を本型が単一所有する（描画実装は返された色で描画面を塗るだけであり、分岐を持たない）。<br/>
    /// 非表示でも透明で塗る（<see cref="LoupeDrawLayer.Pasteboard"/> の層は常に含まれる）。
    /// </remarks>
    public DrawingColor ResolvePasteboardColor() => ShowPasteboard ? PasteboardColor : DrawingColor.Transparent;

    /// <summary>枠線の色を取得する。</summary>
    /// <value>アルファを含む色。 既定は LightGray（#FFD3D3D3）。</value>
    public DrawingColor BorderColor { get; init; } = DrawingColor.FromArgb(255, 0xD3, 0xD3, 0xD3);

    /// <summary>十字線の色を取得する。</summary>
    /// <value>アルファを含む色。 既定は赤 40%（#66FF0000）。</value>
    public DrawingColor CrossColor { get; init; } = DrawingColor.FromArgb(102, 255, 0, 0);

    /// <summary>中心座標ラベルの文字色を取得する。</summary>
    /// <value>アルファを含む色。 既定は白。</value>
    public DrawingColor CoordLabelColor { get; init; } = s_labelTextColorDefault;

    /// <summary>中心座標ラベルの背景色を取得する。</summary>
    /// <value>アルファを含む色。 既定は黒 60%（#99000000）。</value>
    public DrawingColor CoordLabelBackgroundColor { get; init; } = s_labelBackgroundColorDefault;

    /// <summary>中心座標ラベルのフォントサイズ（DIP）を取得する。</summary>
    /// <value>フォントサイズ（DIP）。 6〜48 へ補正済み。 既定は 12.0。</value>
    /// <remarks>
    /// ラベルをどの体裁で描くかは<b>描画にだけ必要な設定</b>であり、描画の設定である本型が単一所有する。<br/>
    /// コアは値を持たず、描画の契約（<c>ILoupeRenderer</c>）を通して UI 層へ公開する。<br/>
    /// 安全域への補正（非有限値は既定値）は <see cref="CoerceLabelFontSize"/> が単一所有する。
    /// </remarks>
    public double CoordLabelFontSize
    {
        get;
        init => field = CoerceLabelFontSize(value, CoordLabelFontSizeDefault);
    } = CoordLabelFontSizeDefault;

    /// <summary>中心座標ラベルのフォントサイズの既定値（DIP）。</summary>
    /// <value>既定のフォントサイズ（DIP）。</value>
    public const double CoordLabelFontSizeDefault = 12.0;

    /// <summary>中心座標ラベルのフォントサイズの最小値（DIP）。</summary>
    /// <value>ラベルのフォントサイズとして受け付ける下限。</value>
    public const double LabelFontSizeMin = 6.0;

    /// <summary>中心座標ラベルのフォントサイズの最大値（DIP）。</summary>
    /// <value>ラベルのフォントサイズとして受け付ける上限。</value>
    public const double LabelFontSizeMax = 48.0;

    /// <summary>中心座標ラベルの背景の角丸半径（DIP）を取得する。</summary>
    /// <value>角丸半径（DIP）。 0〜64 へ補正済み（0 は四角形）。 既定は 2。</value>
    /// <remarks>ラベルの体裁（描画にだけ必要な設定）であるため、描画の設定である本型が単一所有する。</remarks>
    public int CoordLabelCornerRadius
    {
        get;
        init => field = Math.Clamp(value, CoordLabelCornerRadiusMin, CoordLabelCornerRadiusMax);
    } = CoordLabelCornerRadiusDefault;

    /// <summary>中心座標ラベルの角丸半径の最小値（DIP。 0 は四角形）。</summary>
    public const int CoordLabelCornerRadiusMin = 0;

    /// <summary>中心座標ラベルの角丸半径の最大値（DIP）。</summary>
    public const int CoordLabelCornerRadiusMax = 64;

    /// <summary>中心座標ラベルの角丸半径の既定値（DIP）。</summary>
    public const int CoordLabelCornerRadiusDefault = 2;

    /// <summary>中心座標ラベルをルーペの縁から離すオフセット（DIP）を取得する。</summary>
    /// <value>ルーペの左上からラベルの基準点までの距離（DIP）。 既定は 2.0。</value>
    /// <remarks>
    /// ラベルがルーペの縁と重ならないための余白である（ラベルは左上起点で置くため、左右・上下とも同じ値を使う）。<br/>
    /// 体裁（描画にだけ必要なレイアウト値）のためコアには置かず、本型が単一所有する。
    /// </remarks>
    public double CoordLabelOffset { get; init; } = CoordLabelOffsetDefault;

    /// <summary>中心座標ラベルのオフセットの既定値（DIP）。</summary>
    /// <value>既定のオフセット（DIP）。</value>
    public const double CoordLabelOffsetDefault = 2.0;

    // ----------------------------------------------------------------
    // 表示可否と寸法（描画にだけ必要。 コアの計算・判断には入らない）
    // ----------------------------------------------------------------

    /// <summary>十字線を表示するかどうかを取得する。</summary>
    /// <value>true（既定）の場合は切り出し領域の中央へ十字線を描く。 false の場合は描かない。</value>
    /// <remarks>描画の可否を本値が単一所有する（色のアルファを「描くかどうか」の代用にしない）。</remarks>
    public bool ShowCrosshair { get; init; } = true;

    /// <summary>枠線を表示するかどうかを取得する。</summary>
    /// <value>true（既定）の場合はルーペの四辺へ枠線を描く。 false の場合は描かない。</value>
    /// <remarks>描画の可否を本値が単一所有する（色のアルファを「描くかどうか」の代用にしない）。</remarks>
    public bool ShowBorder { get; init; } = true;

    /// <summary>枠線の太さ（DIP）を取得する。</summary>
    /// <value>枠線の太さ（DIP）。 既定は 2。 設定時は 1〜64 へ補正される。</value>
    /// <remarks>
    /// 安全域への補正は本型が単一所有する（設定の経路によらず範囲内の値だけが保持される）。<br/>
    /// 数値スキーマ（ <c>LoupeCore.Limits</c>）はコアが単一所有し、本型が参照する。<br/>
    /// 物理ピクセルへの換算は <see cref="LoupePresenter.ResolveDrawPlan"/> が <c>DpiScaler.ScaleToPixel</c> へ委譲する。
    /// </remarks>
    public int BorderThickness
    {
        get;
        init => field = CoerceBorderThickness(value);
    } = BorderThicknessDefault;

    /// <summary>中心座標ラベルを表示するかどうかを取得する。</summary>
    /// <value>true（既定）の場合は中心座標を表示する。 false の場合は表示しない。</value>
    public bool ShowCoordLabel { get; init; } = true;

    /// <summary>中心座標ラベルを描画するかどうかを判定する。</summary>
    /// <param name="hasCenterPoint">拡大中心が設定されているかどうか。</param>
    /// <returns>描画する場合は true。</returns>
    /// <remarks>ラベルの文言は中心座標から決まるため、中心が未設定のときは描画しない。</remarks>
    public bool ShouldShowCoordLabel(bool hasCenterPoint) => ShowCoordLabel && hasCenterPoint;

    /// <summary>中心座標ラベルのフォントサイズを安全域（6〜48）へ補正する。</summary>
    /// <param name="value">補正する値（DIP）。</param>
    /// <param name="defaultValue">非数（NaN / ∞）の場合に使う既定値（DIP）。</param>
    /// <returns>6〜48 に補正した値。 非数の場合は <paramref name="defaultValue"/>。</returns>
    /// <remarks>非数を素通しするとフォントが描画されなくなるため、本型が単一所有する。</remarks>
    private static double CoerceLabelFontSize(double value, double defaultValue)
        => double.IsFinite(value) ? Math.Clamp(value, LabelFontSizeMin, LabelFontSizeMax) : defaultValue;
}
