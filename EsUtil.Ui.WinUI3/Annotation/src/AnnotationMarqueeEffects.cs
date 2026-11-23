using EsUtil.Feature.Marquee;
using EsUtil.Platform.Core.Geometry;
using Microsoft.UI.Xaml;

namespace EsUtil.Ui.WinUI3.Annotation;

/// <summary>Marquee のコアが要求する効果を WinUI 3 のコントロールへ写すアダプタ。</summary>
/// <remarks>
/// <b>効果だけを実装する</b>: 選択の判断・ハンドルの当たり判定・nudge は Marquee のコアが単一所有する
/// （自前で書き直すと UI 版ごとに挙動がずれる）。 本クラスは再描画・フォーカス・キャプチャを中継するだけである。<br/>
/// <b>カーソル形状は変えない</b>: WinUI 3 のカーソル変更は要素の保護メンバーであり、選択枠の外観（ハンドル）で
/// 操作の種類が分かるため、標準カーソルのままにする。
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
    public void RequestFocus() => _ = _owner.Focus(FocusState.Pointer);

    /// <summary>再描画を要求する。</summary>
    public void RequestRedraw() => _owner.Invalidate();

    /// <summary>カーソル形状を設定する（標準カーソルのままにする）。</summary>
    /// <param name="kind">Marquee のカーソル種別。</param>
    public void SetCursorShape(MarqueeCursorKind kind) => _ = kind;

    /// <summary>ポインター キャプチャの取得・解放を要求する。</summary>
    /// <param name="capture">取得する場合は <see langword="true"/>。</param>
    public void SetPointerCapture(bool capture) => _owner.ApplyPointerCapture(capture);

    /// <summary>ローカル DIP を画面座標へ変換する。</summary>
    /// <param name="sourcePoint">ローカル DIP の座標。</param>
    /// <returns>画面座標（実カーソルを動かさないためローカル座標をそのまま返す）。</returns>
    public EsPointInt ToScreenPoint(EsPoint sourcePoint) => new((int)sourcePoint.X, (int)sourcePoint.Y);

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
