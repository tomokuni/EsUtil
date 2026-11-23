using Avalonia.Input;
using EsUtil.Feature.Marquee;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.AvaloniaUI.Annotation;

/// <summary>Marquee のコアが要求する効果を Avalonia のコントロールへ写すアダプタ。</summary>
/// <remarks>
/// <b>効果だけを実装する</b>: 選択の判断・ハンドルの当たり判定・nudge は Marquee のコアが単一所有する
/// （自前で書き直すと 4 つの UI 版で挙動がずれる）。 本クラスは再描画・フォーカス・カーソル・キャプチャの
/// 4 つをコントロールへ中継するだけである。
/// </remarks>
internal sealed class AnnotationMarqueeEffects : IMarqueeEffects
{
    private readonly AnnotationLayerControl _owner;

    /// <summary>効果の適用先を指定して初期化する。</summary>
    /// <param name="owner">適用先のコントロール。</param>
    internal AnnotationMarqueeEffects(AnnotationLayerControl owner)
    {
        _owner = owner;
    }

    /// <summary>コントロールへフォーカスを要求する。</summary>
    public void RequestFocus() => _owner.Focus();

    /// <summary>再描画を要求する。</summary>
    public void RequestRedraw() => _owner.InvalidateVisual();

    /// <summary>カーソル形状を設定する。</summary>
    /// <param name="kind">Marquee のカーソル種別。</param>
    public void SetCursorShape(MarqueeCursorKind kind) => _owner.ApplyCursor(kind);

    /// <summary>ポインター キャプチャの取得・解放を要求する（取得は押下時にコントロールが行う）。</summary>
    /// <param name="capture">取得する場合は <see langword="true"/>。</param>
    /// <remarks>解放はポインターの解放イベントが担うため、ここでは何もしない（取得だけをコントロールへ伝える）。</remarks>
    public void SetPointerCapture(bool capture)
    {
        if (capture)
        {
            _owner.CapturePointerForMarquee();
        }
    }

    /// <summary>ローカル DIP を画面座標へ変換する。</summary>
    /// <param name="sourcePoint">ローカル DIP の座標。</param>
    /// <returns>画面座標。</returns>
    /// <remarks>実カーソルを動かす nudge（<see cref="TryMoveCursor"/>）を行わないため、ローカル座標をそのまま返す。</remarks>
    public EsPointInt ToScreenPoint(EsPoint sourcePoint) => new((int)sourcePoint.X, (int)sourcePoint.Y);

    /// <summary>実カーソルを画面座標へ移動する（nudge 中に使う）。</summary>
    /// <param name="screenPoint">画面座標。</param>
    /// <returns>移動できなかった場合は <see langword="false"/>。</returns>
    public bool? TryMoveCursor(EsPointInt screenPoint)
    {
        _ = screenPoint;
        return null;
    }

    /// <summary>実カーソルの表示を切り替える（nudge 中は隠す）。</summary>
    /// <param name="show">表示する場合は <see langword="true"/>。</param>
    public void ShowCursor(bool show) => _ = show;

    /// <summary>コントロールの現在のカーソルを取得する。</summary>
    /// <returns>標準カーソルの種別（色はテーマに任せる）。</returns>
    internal static StandardCursorType ToStandardCursor(MarqueeCursorKind kind) => kind switch
    {
        MarqueeCursorKind.DiagonalNWSE => StandardCursorType.TopLeftCorner,
        MarqueeCursorKind.DiagonalNESW => StandardCursorType.TopRightCorner,
        MarqueeCursorKind.Vertical => StandardCursorType.SizeNorthSouth,
        MarqueeCursorKind.Horizontal => StandardCursorType.SizeWestEast,
        MarqueeCursorKind.Move => StandardCursorType.SizeAll,
        _ => StandardCursorType.Arrow,
    };
}
