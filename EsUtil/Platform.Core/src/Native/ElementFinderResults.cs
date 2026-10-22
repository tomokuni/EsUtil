using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Platform.Core.Native;

/// <summary>要素（スクリーン・ウィンドウ）検出の結果。</summary>
/// <param name="Bounds">検出した要素の矩形（画面物理座標）。</param>
/// <param name="IsDetected">要素を検出できたかどうか。</param>
/// <param name="Title">検出した要素の表示名。 ウィンドウ検出時はウィンドウタイトル、スクリーン検出時は「Screen {インデックス}」。 未検出時は null。</param>
/// <param name="Handle">検出したウィンドウの識別子（ウィンドウ検出時のみ設定。 スクリーン検出時・未検出時は null）。</param>
/// <param name="ClassName">検出したウィンドウのクラス名（ウィンドウ検出時のみ設定。 スクリーン検出時・未検出時は null）。</param>
/// <remarks>
/// 指定された位置（画面物理座標）のスクリーン / 最前面ウィンドウを判定した結果を保持する不変データ。<br/>
/// 座標系は入力と同じ<b>画面物理座標</b>で統一される（座標変換は呼び出し側の責務）。<br/>
/// 未検出時は <see cref="Bounds"/> = default、<see cref="IsDetected"/> = false、<see cref="Title"/> / <see cref="Handle"/> / <see cref="ClassName"/> = null。<br/>
/// <see cref="IElementFinder"/> の実装が返す。
/// </remarks>
public readonly record struct ElementFinderResults(
    EsRectInt Bounds,
    bool IsDetected,
    string? Title = null,
    EsPlatformHandle? Handle = null,
    string? ClassName = null);
