using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Platform.Core.Coord;

/// <summary>ローカル DIP と対象座標の双方向変換ポート（対象座標への変換・DIP への変換・対象の境界）。</summary>
/// <remarks>
/// <b>軸は 1 つだけである</b>: 本ポートが扱う 2 つの座標系は「ローカル DIP」と「対象座標」である。<br/>
/// 対象座標はホスト（上位 AP）が定義する外側の座標系であり、画像ピクセル・画面物理ピクセル等が該当する。<br/>
/// 別の軸（DIP ↔ デバイスピクセル）の換算は <see cref="DpiScaler"/> が単一所有する（1 つの型に 2 つの軸を混ぜない）。<br/>
/// <br/>
/// <b>名前が向きを表す</b>: <see cref="ToSource"/> はローカル DIP を受け取って対象座標を返し、<br/>
/// <see cref="ToDip"/> はその逆である（向きを持たない <c>Forward</c> / <c>Inverse</c> は使わない）。<br/>
/// <br/>
/// 具象実装はホストが変換関数と境界を提供して生成する（<see cref="CoordTransform"/>）。<br/>
/// 変換関数は呼び出し毎に評価されるため、ズーム・スクロール等の動的変化を反映できる。<br/>
/// nudge 等の精度保証が必要な計算は<b>対象座標の整数加算のみ</b>で行い、境界でのみ本ポートで変換する。<br/>
/// <b>依存分離</b>: 座標は UI フレームワーク非依存の <see cref="EsPoint"/> / <see cref="EsSize"/> で扱う。
/// </remarks>
public interface ICoordTransform
{
    /// <summary>ローカル DIP 座標を対象座標へ変換する。</summary>
    /// <param name="dip">ローカル DIP 座標。</param>
    /// <returns>対象座標。</returns>
    EsPoint ToSource(EsPoint dip);

    /// <summary>対象座標をローカル DIP 座標へ変換する。</summary>
    /// <param name="source">対象座標。</param>
    /// <returns>ローカル DIP 座標。</returns>
    EsPoint ToDip(EsPoint source);

    /// <summary>対象座標の境界（クリップ範囲）。 null = 無制限。</summary>
    EsSize? SourceArea { get; }
}
