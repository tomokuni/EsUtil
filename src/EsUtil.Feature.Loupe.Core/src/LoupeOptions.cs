using static EsUtil.Feature.Loupe.LoupeCore.Limits;

namespace EsUtil.Feature.Loupe;

/// <summary>ルーペの設定（計算に必要な値。 表示サイズ・拡大率）をまとめた不変の値オブジェクト。</summary>
/// <remarks>
/// 個別の依存関係プロパティとして公開していた設定を 1 つの値にまとめ、UI 層の公開 API を 1 プロパティへ縮小する。<br/>
/// 設定の既定値も本型が単一所有する（各 UI 実装は既定値を持たず、本型を UI のプロパティ型へ橋渡しするだけ）。<br/>
/// <br/>
/// <b>設定とデータの区別</b>: ホストが稀に変更する値（表示サイズ・拡大率）だけを本型へ含める。<br/>
/// マウス移動ごとに変わるデータ（拡大元画像・拡大中心）は UI の個別プロパティのままとする<br/>
/// （1 つの値へまとめると、中心の更新のたびに値全体を組み立て直すことになる）。<br/>
/// DPI スケーリング係数（環境値）と表示位置のオフセット（ホスト結線）も含めない。<br/>
/// <br/>
/// <b>描画にだけ必要な設定は含めない</b>: 色・フォントサイズ・角丸・枠線の太さ・表示可否は<br/>
/// コアの計算・判断に入らないため、描画層の外観（本コアを参照する上位の層）が単一所有する。<br/>
/// <br/>
/// <b>既定値</b>: 既定値を持つのは <see cref="Default"/> と <see cref="LoupeOptions()"/> のみである。<br/>
/// <see cref="Default"/> を基点に部分変更する（<c>LoupeOptions.Default with { ZoomLevel = 8 }</c>）と、<br/>
/// 指定しない項目は既定値を保つ（プロパティ初期化子を持つため、部分指定でもゼロ値にはならない）。<br/>
/// <br/>
/// <b>等価性</b>: レコードの値等価性により、同値の設定を再代入しても UI 側の変更通知（再描画）は発生しない。<br/>
/// <br/>
/// <b>安全域への補正</b>: 数値項目の補正は本型の生成時に適用する（<c>init</c> アクセサ。 安全域は <c>LoupeCore.Limits</c> が単一所有する）。<br/>
/// 値の設定経路（オブジェクト初期化子・<c>with</c> 式・逆シリアル化）によらず、範囲内の値だけが保持される<br/>
/// （UI 側に補正の登録を要求すると、登録を忘れた UI だけ安全域外の値を保持する）。
/// </remarks>
public sealed record LoupeOptions
{
    /// <summary>すべての項目を既定値にした設定。</summary>
    /// <value>既定の表示サイズ・拡大率（各 UI のプロパティの初期値）。</value>
    /// <remarks>部分変更の基点として使用する（<c>Default with { ... }</c>）。</remarks>
    public static LoupeOptions Default { get; } = new();

    // ----------------------------------------------------------------
    // 寸法（表示レイアウトと描画の両方に使用する）
    // ----------------------------------------------------------------

    /// <summary>ルーペの基本サイズ（DIP。 一辺）を取得する。</summary>
    /// <value>表示サイズ（DIP）。 既定は 160。 設定時は 1〜4096 へ補正される。</value>
    /// <remarks>本値がそのまま表示サイズになる（親領域に合わせた縮小はしない）。</remarks>
    public int RequestedSize
    {
        get;
        init => field = CoerceRequestedSize(value);
    } = RequestedSizeDefault;

    /// <summary>拡大率を取得する。</summary>
    /// <value>拡大率（偶数推奨）。 既定は 4。 設定時は 1〜64 へ補正される。</value>
    /// <remarks>
    /// 拡大率は切り出し領域の一辺（物理ピクセルサイズ ÷ 本値の切り上げ）と、拡大画像の描画先の大きさに使う<br/>
    /// （切り上げの規則は <c>LoupeGeometry.CaptureSize</c> が単一所有する）。
    /// </remarks>
    public int ZoomLevel
    {
        get;
        init => field = CoerceZoomLevel(value);
    } = ZoomLevelDefault;
}
