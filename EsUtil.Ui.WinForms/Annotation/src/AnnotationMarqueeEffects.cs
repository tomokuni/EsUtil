using EsUtil.Feature.Marquee;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.WinForms.Annotation;

/// <summary>Marquee のコアが要求する効果を WinForms のコントロールへ写すアダプタ。</summary>
/// <remarks>
/// <b>効果だけを実装する</b>: 選択の判断・ハンドルの当たり判定・nudge は Marquee のコアが単一所有する。
/// 本クラスは再描画・フォーカス・カーソル・キャプチャを中継するだけである。
/// </remarks>
internal sealed class AnnotationMarqueeEffects : IMarqueeEffects
{
    /// <summary>効果の適用先。</summary>
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
    public void RequestRedraw() => _owner.Invalidate();

    /// <summary>カーソル形状を設定する。</summary>
    /// <param name="kind">Marquee のカーソル種別。</param>
    public void SetCursorShape(MarqueeCursorKind kind) => _owner.ApplyCursor(kind);

    /// <summary>ポインター キャプチャの取得・解放を要求する。</summary>
    /// <param name="capture">取得する場合は <see langword="true"/>。</param>
    public void SetPointerCapture(bool capture) => _owner.Capture = capture;

    /// <summary>ローカル DIP を画面座標へ変換する。</summary>
    /// <param name="sourcePoint">ローカル DIP の座標。</param>
    /// <returns>画面座標。</returns>
    public EsPointInt ToScreenPoint(EsPoint sourcePoint)
    {
        var screen = _owner.PointToScreen(new Point((int)sourcePoint.X, (int)sourcePoint.Y));
        return new EsPointInt(screen.X, screen.Y);
    }

    /// <summary>実カーソルを画面座標へ移動する（nudge では動かさない）。</summary>
    /// <param name="screenPoint">画面座標。</param>
    /// <returns>常に <see langword="null"/>（移動しない）。</returns>
    public bool? TryMoveCursor(EsPointInt screenPoint)
    {
        _ = screenPoint;
        return null;
    }

    /// <summary>実カーソルの表示を切り替える（何もしない）。</summary>
    /// <param name="show">表示する場合は <see langword="true"/>。</param>
    public void ShowCursor(bool show) => _ = show;
}
