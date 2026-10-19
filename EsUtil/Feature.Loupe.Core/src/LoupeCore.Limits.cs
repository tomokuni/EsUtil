
namespace EsUtil.Feature.Loupe;

public sealed partial class LoupeCore
{
    /// <summary>ルーペの数値パラメータの既定値・安全域と、範囲外の値の補正（ファサードが公開する設定のスキーマ）。</summary>
    /// <remarks>
    /// <b>位置付け</b>: ホストが設定する数値の既定値と安全域（最小・最大）、および範囲外の値の補正を単一所有する。<br/>
    /// <b>ファサードの入れ子である理由</b>: 利用者（UI 実装）はプロパティの既定値と補正を登録するため本値を参照する。<br/>
    /// ファサード（<see cref="LoupeCore"/>）の入れ子とすることで、UI がファサード以外のコア型を直接呼ぶ経路を作らない<br/>
    /// （利用者はルーペの数値を得るのに <c>LoupeCore</c> だけを参照すればよい）。<br/>
    /// <b>補正は値の生成時に行う</b>: 設定型（<see cref="LoupeOptions"/>）の <c>init</c> アクセサが本値を参照するため、<br/>
    /// 設定の経路（オブジェクト初期化子・<c>with</c> 式・逆シリアル化）によらず安全域内の値だけが保持される。<br/>
    /// <b>規則の所在</b>: 環境値（<see cref="LoupeCore.ClientToPixelScaling"/>）の補正も本値へ委譲する（補正の規則を二重に持たない）。<br/>
    /// ラベルのフォントサイズの安全域は、他のコントロールと共通の規則のため描画層の外観（本コアを参照する上位の層）が単一所有する。<br/>
    /// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
    /// </remarks>
    public static class Limits
    {
        /// <summary>表示サイズ要求値の最小値（DIP）。</summary>
        public const int RequestedSizeMin = 1;

        /// <summary>表示サイズ要求値の最大値（DIP）。</summary>
        public const int RequestedSizeMax = 4096;

        /// <summary>表示サイズ要求値の既定値（DIP）。</summary>
        public const int RequestedSizeDefault = 160;

        /// <summary>拡大率の最小値。</summary>
        public const int ZoomLevelMin = 1;

        /// <summary>拡大率の最大値。</summary>
        public const int ZoomLevelMax = 64;

        /// <summary>拡大率の既定値。</summary>
        public const int ZoomLevelDefault = 4;

        /// <summary>枠線の太さの最小値（ピクセル）。</summary>
        public const int BorderThicknessMin = 1;

        /// <summary>枠線の太さの最大値（ピクセル）。</summary>
        public const int BorderThicknessMax = 64;

        /// <summary>枠線の太さの既定値（ピクセル）。</summary>
        public const int BorderThicknessDefault = 2;

        /// <summary>スケーリング係数の最小値（環境値。 他の数値より後に置く）。</summary>
        public const double ClientToPixelScalingMin = 0.1;

        /// <summary>スケーリング係数の最大値（環境値）。</summary>
        public const double ClientToPixelScalingMax = 8.0;

        /// <summary>スケーリング係数の既定値（環境値。 非有限値のフォールバックも兼ねる）。</summary>
        public const double ClientToPixelScalingDefault = 1.0;

        /// <summary>表示サイズ要求値を安全域へ補正する（1〜4096）。</summary>
        /// <param name="value">補正する値（DIP）。</param>
        /// <returns>安全域へ補正した値。</returns>
        public static int CoerceRequestedSize(int value) => Math.Clamp(value, RequestedSizeMin, RequestedSizeMax);

        /// <summary>拡大率を安全域へ補正する（1〜64）。</summary>
        /// <param name="value">補正する値。</param>
        /// <returns>安全域へ補正した値。</returns>
        public static int CoerceZoomLevel(int value) => Math.Clamp(value, ZoomLevelMin, ZoomLevelMax);

        /// <summary>枠線の太さを安全域へ補正する（1〜64）。</summary>
        /// <param name="value">補正する値（ピクセル）。</param>
        /// <returns>安全域へ補正した値。</returns>
        public static int CoerceBorderThickness(int value) => Math.Clamp(value, BorderThicknessMin, BorderThicknessMax);

        /// <summary>スケーリング係数を安全域へ補正する（0.1〜8.0。 非有限値は既定値）。</summary>
        /// <param name="value">補正する値。</param>
        /// <returns>安全域へ補正した値。 非有限値の場合は既定値。</returns>
        public static double CoerceClientToPixelScaling(double value)
            => CoerceFinite(value, ClientToPixelScalingMin, ClientToPixelScalingMax, ClientToPixelScalingDefault);

        /// <summary>実数を安全域へ補正する（非有限値は既定値）。</summary>
        /// <param name="value">補正する値。</param>
        /// <param name="min">安全域の最小値。</param>
        /// <param name="max">安全域の最大値。</param>
        /// <param name="fallback">非有限値のときに返す既定値。</param>
        /// <returns>安全域に補正した値。</returns>
        /// <remarks>
        /// 「非有限は素通ししない」規則を単一所有する（<c>Math.Clamp</c> は NaN を素通しするため、<br/>
        /// 表示サイズの換算へ NaN が入ると切り出し領域が全て NaN になり描画が消える）。
        /// </remarks>
        private static double CoerceFinite(double value, double min, double max, double fallback)
            => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
    }
}
