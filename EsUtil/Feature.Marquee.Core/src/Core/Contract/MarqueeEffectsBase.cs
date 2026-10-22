using EsUtil.Feature.Marquee.Core.Interaction;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native;

namespace EsUtil.Feature.Marquee.Core.Contract;

/// <summary>Marquee が要求する効果の既定実装（すべて何もしない）。</summary>
/// <remarks>
/// <b>位置付け</b>: 独自のコントロールへ Marquee を組み込むときの入力を減らす基底クラスである。<br/>
/// <see cref="IMarqueeEffects"/> は 5 つの効果（再描画・フォーカス・カーソル形状・ポインターキャプチャ・画面座標変換）を<br/>
/// 持つが、実際に必要になるのは一部だけである（例: 矢印キーの nudge を使わないコントロールはフォーカスもカーソルも不要）。<br/>
/// 本クラスを継承すれば<b>必要な効果だけを override</b> すればよく、不要な効果の空実装を書かなくて済む。<br/>
/// <br/>
/// <b>拡張に対して開いている</b>: 将来 <see cref="IMarqueeEffects"/> へ効果が追加されても、本クラスが既定実装を<br/>
/// 提供するため、本クラスを継承した実装は変更不要である（<see cref="IMarqueeEffects"/> を直接実装した場合は<br/>
/// 未実装メンバーがコンパイルエラーになる）。<br/>
/// <br/>
/// <b>適用順はコアが単一所有する</b>: 効果をどの順で適用するか（フォーカス → カーソル形状 → 再描画）は<br/>
/// コア（<see cref="MarqueeFeedback"/>）が固定するため、実装は呼ばれた順に自 UI の操作へ接続するだけでよい。
/// </remarks>
/// <example>
/// 再描画だけを行う最小の実装:
/// <code>
/// private sealed class Effects(MyControl owner) : MarqueeEffectsBase
/// {
///     public override void RequestRedraw() => owner.InvalidateVisual();
/// }
/// </code>
/// </example>
public abstract class MarqueeEffectsBase : IMarqueeEffects
{
    /// <summary>コントロールへフォーカスを設定する（既定では何もしない）。</summary>
    /// <remarks>矢印キーの nudge を受け付けるコントロールだけが override する。</remarks>
    public virtual void RequestFocus()
    {
    }

    /// <summary>再描画を要求する（既定では何もしない）。</summary>
    /// <remarks>ラベルを UI 要素として配置するコントロールは、再描画と併せて配置の再計算も要求する。</remarks>
    public virtual void RequestRedraw()
    {
    }

    /// <summary>カーソル形状を設定する（既定では何もしない）。</summary>
    /// <param name="kind">カーソル形状の種別（UI 層が自 UI のカーソル型へ写像する）。</param>
    public virtual void SetCursorShape(MarqueeCursorKind kind)
    {
    }

    /// <summary>ポインターキャプチャを取得または解放する（既定では何もしない）。</summary>
    /// <param name="capture">true = 取得 / false = 解放。</param>
    /// <remarks>ポインターを暗黙にキャプチャする UI は override しなくてよい。</remarks>
    public virtual void SetPointerCapture(bool capture)
    {
    }

    /// <summary>対象座標を画面物理座標へ変換する（既定では変換せず既定値を返す）。</summary>
    /// <param name="sourcePoint">対象座標。</param>
    /// <returns>画面物理座標。 既定では <c>default</c>。</returns>
    /// <remarks>
    /// 実カーソルを動かす nudge（<see cref="ICursorService"/> を渡した場合）だけが必要とする。<br/>
    /// カーソルサービスを渡さない場合は呼ばれない。
    /// </remarks>
    public virtual EsPointInt ToScreenPoint(EsPoint sourcePoint) => default;
}
