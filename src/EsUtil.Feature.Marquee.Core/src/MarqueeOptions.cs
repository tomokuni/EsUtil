using static EsUtil.Feature.Marquee.MarqueeCore.Limits;

namespace EsUtil.Feature.Marquee;

/// <summary>選択矩形コントロールの設定（計算に必要な値。 寸法・対話パラメータ）をまとめた不変の値オブジェクト。</summary>
/// <remarks>
/// 個別の依存関係プロパティとして公開していた設定を 1 つの値にまとめ、UI 層の公開 API を 1 プロパティへ縮小する。<br/>
/// 設定の既定値も本型が単一所有する（各 UI 実装は既定値を持たず、本型を UI のプロパティ型へ橋渡しするだけ）。<br/>
/// <b>体裁・フォントサイズは本型に含まない</b>: ラベルをどの体裁で描くかは描画にだけ必要な設定であり、<br/>
/// 描画層の外観が単一所有する。<br/>
/// <br/>
/// 設定は 2 つの性格に分かれる:<br/>
/// - <b>寸法</b>: 基準点と表示を決めるための長さ（描画と配置に使用する）。<br/>
/// - <b>対話パラメータ</b>: 最小選択サイズ・ハンドルのヒットテスト半径・境界外選択の禁止（入力の解釈に使用し、セッションへ伝搬する）。<br/>
/// <br/>
/// <b>装飾（暗転マスク・外枠・内側の塗り）も本値に含まない</b>: 装飾の外観と表示ポリシーは<br/>
/// 選択矩形コントロール単体の概念ではなく、コントロールを撮影オーバーレイ等へ組み込むときにだけ必要になる表示のため、<br/>
/// 描画層の外観が単一所有する。<br/>
/// <br/>
/// <b>既定値</b>: 既定値を持つのは <see cref="Default"/> と <see cref="MarqueeOptions()"/> のみである。<br/>
/// <see cref="Default"/> を基点に部分変更する（<c>MarqueeOptions.Default with { HandleSize = ... }</c>）と、<br/>
/// 指定しない項目は既定値を保つ（プロパティ初期化子を持つため、部分指定でもゼロ値にはならない）。<br/>
/// <br/>
/// <b>等価性</b>: レコードの値等価性により、同値の設定を再代入しても UI 側の変更通知（再描画）は発生しない。<br/>
/// <br/>
/// <b>安全域への補正</b>: 数値項目の補正は本型の生成時に適用する（<c>init</c> アクセサ。 安全域は <c>MarqueeCore.Limits</c> が単一所有する）。<br/>
/// 値の設定経路（オブジェクト初期化子・<c>with</c> 式・逆シリアル化）によらず、範囲内の値だけが保持される<br/>
/// （UI 側に補正の登録を要求すると、登録を忘れた UI だけ安全域外の値を保持する）。<br/>
/// <br/>
/// <b>色と表示可否は含まない</b>: 色と「描くかどうか」は<b>描画にだけ必要な設定</b>であり、描画層の外観が単一所有する。
/// </remarks>
public sealed record MarqueeOptions
{
    /// <summary>すべての項目を既定値にした設定。</summary>
    /// <value>既定の寸法・対話パラメータ（各 UI のプロパティの初期値）。</value>
    /// <remarks>部分変更の基点として使用する（<c>Default with { ... }</c>）。</remarks>
    public static MarqueeOptions Default { get; } = new();

    // ----------------------------------------------------------------
    // 寸法（表示レイアウトと描画の両方に使用する）
    // ----------------------------------------------------------------

    /// <summary>ドラッグハンドルの一辺サイズを取得する。</summary>
    /// <value>ハンドルの一辺サイズ（DIP）。 既定は 10.0。 設定時は 1.0〜256.0 へ補正される。</value>
    public double HandleSize
    {
        get;
        init => field = CoerceHandleSize(value);
    } = HandleSizeDefault;

    /// <summary>ドラッグハンドルの枠線の太さを取得する。</summary>
    /// <value>ハンドル枠の線の太さ（DIP）。 既定は 1.5。 設定時は 0.5〜64.0 へ補正される。</value>
    /// <remarks>
    /// ハンドルの見えている大きさ（<see cref="HandleSize"/> の矩形と、その周囲へ描かれる枠線）を決めるため、<br/>
    /// 設定値は描画だけでなくヒットテストの余地にも影響する（大きいハンドルを細い枠で描くと、見えている範囲と当たる範囲が食い違う）。<br/>
    /// 本値と <see cref="HandleHitRadius"/> を揃えて設定することで、表示と操作が同じ大きさになる。
    /// </remarks>
    public double HandleBorderThickness
    {
        get;
        init => field = CoerceHandleBorderThickness(value);
    } = HandleBorderThicknessDefault;

    /// <summary>十字マーカーの全長を取得する。</summary>
    /// <value>十字マーカーの全長（DIP）。 中心から両端までの半幅はこの 1/2。 既定は 16.0。 設定時は 2.0〜4096.0 へ補正される。</value>
    public double MarkerLength
    {
        get;
        init => field = CoerceMarkerLength(value);
    } = MarkerLengthDefault;

    // ----------------------------------------------------------------
    // 対話パラメータ（入力の解釈に使用し、セッションへ伝搬する）
    // ----------------------------------------------------------------

    /// <summary>ドラッグハンドルのヒットテスト半径を取得する。</summary>
    /// <value>ヒットテスト半径（DIP）。 既定は 12.0。 設定時は 0.0〜256.0 へ補正される。</value>
    /// <remarks>
    /// DIP 座標での半径であるため、ズーム・縮小表示を変えてもヒットテストの画面上の大きさが一定になる。<br/>
    /// 対象座標系への変換はセッションが行う。
    /// </remarks>
    public double HandleHitRadius
    {
        get;
        init => field = CoerceHandleHitRadius(value);
    } = HandleHitRadiusDefault;

    /// <summary>選択矩形の最小サイズを取得する。</summary>
    /// <value>最小サイズ（対象座標）。 既定は 6.0。 設定時は 1.0〜4096.0 へ補正される。</value>
    /// <remarks>ドラッグ完了時にこれ未満の矩形はキャンセル扱いになる（判断はセッションが行う）。</remarks>
    public double MinSelectionSize
    {
        get;
        init => field = CoerceMinSelectionSize(value);
    } = MinSelectionSizeDefault;

    /// <summary>対象の境界外の選択を禁止するかどうかを取得する。</summary>
    /// <value>true（既定）の場合は選択を対象の境界内に制限する。 false の場合は境界外も選択できる。</value>
    /// <remarks>
    /// クリップ範囲は座標変換ポートの Bounds で指定する。<br/>
    /// true へ切り替えた場合は既存の選択矩形を即時に境界内へクリップする（セッションが判断する）。
    /// </remarks>
    public bool ClampToSourceBounds { get; init; } = true;
}
