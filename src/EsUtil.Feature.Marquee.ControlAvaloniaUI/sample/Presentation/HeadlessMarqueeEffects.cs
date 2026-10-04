using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Marquee.AvaloniaUI.Sample.Presentation;

/// <summary>画面を持たない提示層（拡張の見本）に必要な効果の適用先（すべて何もしない）。</summary>
/// <remarks>
/// コアは状態の更新と通知だけを行い、画面への反映は <see cref="IMarqueeEffects"/> 経由で UI 層へ委ねる。<br/>
/// 見本の提示層は画面（フォーカス・カーソル・キャプチャ）を持たないため、効果はすべて空実装で応答する<br/>
/// （コントロールを通さない見本が、この実装だけで動作することを示す）。
/// </remarks>
internal sealed class HeadlessMarqueeEffects : IMarqueeEffects
{
    /// <summary>フォーカス要求を受け取る（画面を持たないため何もしない）。</summary>
    public void RequestFocus()
    {
    }

    /// <summary>再描画要求を受け取る（画面を持たないため何もしない）。</summary>
    public void RequestRedraw()
    {
    }

    /// <summary>カーソル形状の設定を受け取る（画面を持たないため何もしない）。</summary>
    /// <param name="kind">カーソル形状の種別（使用しない）。</param>
    public void SetCursorShape(MarqueeCursorKind kind)
    {
    }

    /// <summary>ポインターキャプチャの取得・解放を受け取る（画面を持たないため何もしない）。</summary>
    /// <param name="capture">true = 取得 / false = 解放（使用しない）。</param>
    public void SetPointerCapture(bool capture)
    {
    }

    /// <summary>対象座標を画面物理座標へ変換する（画面を持たないため既定値を返す）。</summary>
    /// <param name="sourcePoint">対象座標（使用しない）。</param>
    /// <returns>既定値。</returns>
    public EsPointInt ToScreenPoint(EsPoint sourcePoint) => default;
}
