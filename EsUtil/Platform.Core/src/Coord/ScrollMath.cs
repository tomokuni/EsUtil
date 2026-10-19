namespace EsUtil.Platform.Core.Coord;

/// <summary>スクロールバーの範囲・ビューポートサイズの計算（純粋関数）。</summary>
/// <remarks>
/// スクロールバーの幾何計算を UI 状態（スクロールバーコントロール等）から分離し、単体テストで仕様を検証できるようにする。<br/>
/// 座標系は「コンテンツ座標 = 元画像ピクセル」で、ビューポートサイズはスケールで除してコンテンツ座標に変換する。<br/>
/// <c>double</c> のみに依存し、UI フレームワーク（Avalonia 等）に依存しないため、ソリューション外でも使用できる汎用部品。
/// </remarks>
public static class ScrollMath
{
    /// <summary>通常ホイール 1 段あたりのスクロール量（DIP）。</summary>
    /// <value>ホイールの回転量 1 を DIP の移動量へ換算する係数。</value>
    /// <remarks>
    /// OS のホイール 1 段は 3 行（行の高さにより変動する）をスクロールするが、<br/>
    /// キャンバスのパンでは「見た目の移動量」を優先して固定値を用いる（全 UI・全ホイール経路で同じ値を使う）。
    /// </remarks>
    public const double WheelScrollUnit = 48.0;

    /// <summary>スクロール範囲の最大オフセット（コンテンツ座標）を計算する。</summary>
    /// <param name="sourceSize">コンテンツサイズ（コンテンツ座標）。</param>
    /// <param name="viewportSize">ビューポートサイズ（表示座標）。</param>
    /// <param name="scale">スケール（倍率 / 100）。</param>
    /// <returns>最大オフセット。 コンテンツがビューポート以下の場合は 0。</returns>
    /// <remarks>
    /// 描画位置 = スケール × (コンテンツ座標 − オフセット) の定義に基づき、<br/>
    /// オフセットの最大値はコンテンツ − ビューポート / スケール。
    /// </remarks>
    public static double MaxOffset(double sourceSize, double viewportSize, double scale)
    {
        scale = scale > 0 ? scale : 1;
        return Math.Max(0, sourceSize - viewportSize / scale);
    }

    /// <summary>スクロールバーのビューポートサイズ（コンテンツ座標での表示範囲）を計算する。</summary>
    /// <param name="viewportSize">ビューポートサイズ（表示座標）。</param>
    /// <param name="scale">スケール（倍率 / 100）。</param>
    /// <returns>コンテンツ座標での表示範囲。</returns>
    /// <remarks>サムサイズ = ビューポート / コンテンツ × トラック長の計算に使用する。</remarks>
    public static double ViewportSize(double viewportSize, double scale)
    {
        scale = scale > 0 ? scale : 1;
        return viewportSize / scale;
    }
}
