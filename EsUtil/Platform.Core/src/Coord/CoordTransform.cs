using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Platform.Core.Coord;

/// <summary><see cref="ICoordTransform"/> の具象実装（Func 委譲）。</summary>
/// <param name="toSource">ローカル DIP 座標を対象座標へ変換する関数。</param>
/// <param name="toDip">対象座標をローカル DIP 座標へ変換する関数。</param>
/// <param name="sourceArea">対象座標の境界を返す関数（null = 無制限）。</param>
/// <remarks>
/// ホスト（上位 AP）が変換関数と境界を提供して生成する。 等価変換（ローカル DIP = 対象座標・境界なし）の既定実装は
/// <see cref="Identity"/> で提供する。<br/>
/// 変換関数は呼び出し毎に評価されるため、ズーム・スクロール等の動的変化を反映できる。<br/>
/// <b>軸はローカル DIP と対象座標の 1 つだけである</b>: DIP とデバイスピクセルの換算は <see cref="DpiScaler"/> が
/// 単一所有する（1 つの型に 2 つの軸を混ぜない）。
/// </remarks>
public sealed class CoordTransform(
    Func<EsPoint, EsPoint> toSource,
    Func<EsPoint, EsPoint> toDip,
    Func<EsSize?> sourceArea) : ICoordTransform
{
    /// <summary>等価変換（ローカル DIP = 対象座標・境界なし）の既定ポート。</summary>
    public static ICoordTransform Identity { get; } = new CoordTransform(
        static p => p, static p => p, static () => null);

    /// <summary>対象座標 = ローカル DIP × 倍率のポートを生成する（倍率は対象座標 / ローカル DIP）。</summary>
    /// <param name="sourcePerDip">対象座標 / ローカル DIP の倍率を提供する関数（呼び出し毎に評価。 0 以下は 1.0 として扱う）。</param>
    /// <param name="sourceArea">対象座標の境界（画像サイズ等）を提供する関数（呼び出し毎に評価）。 null の場合は境界なし（無制限）。</param>
    /// <returns>ローカル DIP と対象座標の双方向変換ポート。</returns>
    /// <remarks>
    /// 同一原点の座標系間の比例変換（例: 対象 = 画面物理ピクセル、ローカル DIP × DPI スケーリング）を提供する。<br/>
    /// <b>倍率の向きは 1 通りだけである</b>: <paramref name="sourcePerDip"/> は常に「対象座標 / ローカル DIP」であり、<br/>
    /// 逆数（ローカル DIP / 対象）を渡す入口は設けない（同じ変換が 2 通りの数値で書ける状態を残さない）。<br/>
    /// 境界は選択を対象外へ出さないためのクランプに使われる（境界が不要なホストは省略する）。
    /// </remarks>
    public static ICoordTransform ClientToSource(
        Func<double> sourcePerDip,
        Func<EsSize?>? sourceArea = null)
    {
        static double Normalize(double factor) => factor > 0 ? factor : 1.0;

        return new CoordTransform(
            dip =>
            {
                var factor = Normalize(sourcePerDip());
                return new EsPoint(dip.X * factor, dip.Y * factor);
            },
            source =>
            {
                var factor = Normalize(sourcePerDip());
                return new EsPoint(source.X / factor, source.Y / factor);
            },
            sourceArea ?? (static () => null));
    }

    private readonly Func<EsPoint, EsPoint> _toSource = toSource;
    private readonly Func<EsPoint, EsPoint> _toDip = toDip;
    private readonly Func<EsSize?> _sourceArea = sourceArea;

    /// <inheritdoc/>
    public EsPoint ToSource(EsPoint dip) => _toSource(dip);

    /// <inheritdoc/>
    public EsPoint ToDip(EsPoint source) => _toDip(source);

    /// <inheritdoc/>
    public EsSize? SourceArea => _sourceArea();
}