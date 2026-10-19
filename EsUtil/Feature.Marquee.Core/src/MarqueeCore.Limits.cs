using EsUtil.Platform.Core.Coord;

namespace EsUtil.Feature.Marquee;

public sealed partial class MarqueeCore
{
    /// <summary>選択矩形コントロールの数値パラメータの既定値・安全域と、未設定・範囲外の解決規則（ファサードが公開する設定のスキーマ）。</summary>
    /// <remarks>
    /// <b>位置付け</b>: ホストが設定する数値の既定値と安全域（最小・最大）、および範囲外の値の補正を単一所有する。<br/>
    /// <b>ファサードの入れ子である理由</b>: 利用者（UI 実装）はプロパティの既定値を登録するため本値を参照する。<br/>
    /// ファサード（<see cref="MarqueeCore"/>）の入れ子とすることで、UI がファサード以外のコア型を直接呼ぶ経路を作らない<br/>
    /// （利用者は選択矩形の数値を得るのに <c>MarqueeCore</c> だけを参照すればよい）。<br/>
    /// <b>補正は値の生成時に行う</b>: 設定型（<see cref="MarqueeOptions"/>）と装飾の外観の <c>init</c> アクセサが本値を参照するため、<br/>
    /// 設定の経路（オブジェクト初期化子・<c>with</c> 式・逆シリアル化）によらず安全域内の値だけが保持される<br/>
    /// （UI 側に補正の登録を要求すると、登録を忘れた UI だけ安全域外の値を保持する）。<br/>
    /// <b>規則の所在</b>: 倍率が未設定（0 以下・等倍）の場合に等価変換とする規則は <see cref="MarqueeRenderRequest.ResolvePort"/> が、<br/>
    /// 表示スケールが未設定（0 以下）の場合に倍率を使う規則は <see cref="ResolveDisplayScale"/> が単一所有する。<br/>
    /// ラベルのフォントサイズの安全域は、他のコントロールと共通の規則のため描画層の外観が単一所有する。<br/>
    /// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
    /// </remarks>
    public static class Limits
    {
        /// <summary>ドラッグハンドルの一辺サイズの最小値（DIP）。</summary>
        public const double HandleSizeMin = 1.0;

        /// <summary>ドラッグハンドルの一辺サイズの最大値（DIP）。</summary>
        public const double HandleSizeMax = 256.0;

        /// <summary>ドラッグハンドルの一辺サイズの既定値（DIP）。</summary>
        public const double HandleSizeDefault = 10.0;

        /// <summary>十字マーカーの全長の最小値（DIP）。</summary>
        public const double MarkerLengthMin = 2.0;

        /// <summary>十字マーカーの全長の最大値（DIP）。</summary>
        public const double MarkerLengthMax = 4096.0;

        /// <summary>十字マーカーの全長の既定値（DIP。 中心から両端までの半幅は 1/2）。</summary>
        public const double MarkerLengthDefault = 16.0;

        /// <summary>ドラッグハンドルのヒットテスト半径の最小値（DIP。 0 は「ちょうど重なったときだけ当たる」）。</summary>
        public const double HandleHitRadiusMin = 0.0;

        /// <summary>ドラッグハンドルのヒットテスト半径の最大値（DIP）。</summary>
        public const double HandleHitRadiusMax = 256.0;

        /// <summary>ドラッグハンドルのヒットテスト半径の既定値（DIP）。</summary>
        public const double HandleHitRadiusDefault = 12.0;

        /// <summary>選択矩形の最小サイズの最小値（対象座標。 0 以下の指定を無効化する）。</summary>
        public const double MinSelectionSizeMin = 1.0;

        /// <summary>選択矩形の最小サイズの最大値（対象座標）。</summary>
        public const double MinSelectionSizeMax = 4096.0;

        /// <summary>選択矩形の最小サイズの既定値（対象座標）。</summary>
        public const double MinSelectionSizeDefault = 6.0;

        /// <summary>枠線の太さの最小値（DIP）。</summary>
        /// <remarks>0 以下の指定を無効化する（選択枠は細い線でも視認できる必要がある）。</remarks>
        public const double BorderThicknessMin = 0.5;

        /// <summary>枠線の太さの最大値（DIP）。</summary>
        public const double BorderThicknessMax = 64.0;

        /// <summary>枠線の太さの既定値（DIP）。</summary>
        /// <remarks>装飾の外観（<c>MarqueeOuterDecoAppearance</c> / <c>MarqueeInnerDecoAppearance</c>）の既定値も本値を参照する（既定値の二重所有を防ぐ）。</remarks>
        public const double BorderThicknessDefault = 2.0;

        /// <summary>ドラッグハンドルの枠線の太さの最小値（DIP）。</summary>
        /// <remarks>0 以下の指定を無効化する（ハンドルの輪郭は細い線でも視認できる必要がある）。</remarks>
        public const double HandleBorderThicknessMin = 0.5;

        /// <summary>ドラッグハンドルの枠線の太さの最大値（DIP）。</summary>
        public const double HandleBorderThicknessMax = 64.0;

        /// <summary>ドラッグハンドルの枠線の太さの既定値（DIP）。</summary>
        /// <remarks>塗りの中に隠れない細さでありながら、拡大表示でも輪郭が判別できる太さである。</remarks>
        public const double HandleBorderThicknessDefault = 1.5;

        /// <summary>ポート未指定時に適用する倍率（対象座標 / ローカル DIP）の最小値（環境値。 他の数値より後に置く）。</summary>
        public const double ClientToSourceScalingMin = 0.1;

        /// <summary>ポート未指定時に適用する倍率（対象座標 / ローカル DIP）の最大値（環境値）。</summary>
        public const double ClientToSourceScalingMax = 8.0;

        /// <summary>ポート未指定時に適用する倍率（対象座標 / ローカル DIP）の既定値（環境値。 対象座標と DIP が 1:1）。</summary>
        /// <remarks>各 UI の依存関係プロパティ（<c>ClientToSourceScaling</c>）の既定値も本値を参照する（既定値の二重所有を防ぐ）。</remarks>
        public const double ClientToSourceScalingDefault = 1.0;

        /// <summary>ドラッグハンドルの一辺サイズを安全域へ補正する。</summary>
        /// <param name="value">補正する値（DIP）。</param>
        /// <returns>安全域に補正した値。 非有限の場合は既定値。</returns>
        public static double CoerceHandleSize(double value)
            => CoerceFinite(value, HandleSizeMin, HandleSizeMax, HandleSizeDefault);

        /// <summary>十字マーカーの全長を安全域へ補正する。</summary>
        /// <param name="value">補正する値（DIP）。</param>
        /// <returns>安全域に補正した値。 非有限の場合は既定値。</returns>
        public static double CoerceMarkerLength(double value)
            => CoerceFinite(value, MarkerLengthMin, MarkerLengthMax, MarkerLengthDefault);

        /// <summary>ドラッグハンドルのヒットテスト半径を安全域へ補正する。</summary>
        /// <param name="value">補正する値（DIP）。</param>
        /// <returns>安全域に補正した値。 非有限の場合は既定値。</returns>
        public static double CoerceHandleHitRadius(double value)
            => CoerceFinite(value, HandleHitRadiusMin, HandleHitRadiusMax, HandleHitRadiusDefault);

        /// <summary>選択矩形の最小サイズを安全域へ補正する。</summary>
        /// <param name="value">補正する値（対象座標）。</param>
        /// <returns>安全域に補正した値。 非有限の場合は既定値。</returns>
        /// <remarks>0 以下の指定は最小値（1.0）へ補正される（幅・高さが 0 の選択を残さない）。</remarks>
        public static double CoerceMinSelectionSize(double value)
            => CoerceFinite(value, MinSelectionSizeMin, MinSelectionSizeMax, MinSelectionSizeDefault);

        /// <summary>枠線の太さを安全域へ補正する。</summary>
        /// <param name="value">補正する値（DIP）。</param>
        /// <returns>安全域に補正した値。 非有限の場合は既定値。</returns>
        /// <remarks>
        /// 補正は装飾の外観（<c>MarqueeOuterDecoAppearance</c> / <c>MarqueeInnerDecoAppearance</c>）の生成時に適用する<br/>
        /// （設定の経路によらず、範囲内の値だけが保持される）。
        /// </remarks>
        public static double CoerceBorderThickness(double value)
            => CoerceFinite(value, BorderThicknessMin, BorderThicknessMax, BorderThicknessDefault);

        /// <summary>ドラッグハンドルの枠線の太さを安全域へ補正する。</summary>
        /// <param name="value">補正する値（DIP）。</param>
        /// <returns>安全域に補正した値。 非有限の場合は既定値。</returns>
        /// <remarks>補正は設定型（<see cref="MarqueeOptions"/>）の生成時に適用する（設定の経路によらず範囲内の値だけが保持される）。</remarks>
        public static double CoerceHandleBorderThickness(double value)
            => CoerceFinite(value, HandleBorderThicknessMin, HandleBorderThicknessMax, HandleBorderThicknessDefault);

        /// <summary>ポート未指定時に適用する倍率を安全域へ補正する。</summary>
        /// <param name="value">補正する値。</param>
        /// <returns>安全域に補正した値。 非有限の場合は既定値。</returns>
        /// <remarks>倍率は環境値（DPI・マルチスクリーン）として設定され、座標変換ポートが指定されている場合は使われない。</remarks>
        public static double CoerceClientToSourceScaling(double value)
            => CoerceFinite(value, ClientToSourceScalingMin, ClientToSourceScalingMax, ClientToSourceScalingDefault);

        /// <summary>表示スケールの係数を解決する（0 以下の場合は座標換算の係数を使う）。</summary>
        /// <param name="displayScaling">指定された表示スケール。 0 以下は未指定として扱う。</param>
        /// <param name="dipToSourceScaling">座標換算の係数（1.0 未満は 1.0 として扱う）。</param>
        /// <returns>解決した表示スケール。</returns>
        /// <remarks>
        /// 「0 以下は未指定」という規則を単一所有する（UI 層に同じ三項演算を書かせない）。<br/>
        /// サイズラベルの文言（描画層が生成する）も本規則を使うため public とする。
        /// </remarks>
        public static double ResolveDisplayScale(double displayScaling, double dipToSourceScaling)
            => displayScaling > 0 ? displayScaling : DpiScaler.NormalizeScaling(dipToSourceScaling);

        /// <summary>実数を安全域へ補正する（非有限は既定値へ戻す）。</summary>
        /// <param name="value">補正する値。</param>
        /// <param name="min">安全域の最小値。</param>
        /// <param name="max">安全域の最大値。</param>
        /// <param name="fallback">非有限（NaN / 無限）の場合に使う値。</param>
        /// <returns>安全域に補正した値。</returns>
        /// <remarks>
        /// 「非有限は素通ししない」規則を単一所有する（<c>Math.Clamp</c> は NaN を素通しするため、<br/>
        /// 幾何計算へ NaN が入ると矩形が全て NaN になり描画が消える）。
        /// </remarks>
        private static double CoerceFinite(double value, double min, double max, double fallback)
            => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
    }
}
