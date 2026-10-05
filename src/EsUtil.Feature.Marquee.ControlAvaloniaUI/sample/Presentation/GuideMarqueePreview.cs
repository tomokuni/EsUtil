using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Interop;

namespace EsUtil.Feature.Marquee.AvaloniaUI.Sample.Presentation;

/// <summary>派生提示層（<see cref="GuideMarqueePresenter"/>）を使う見本コントロール。</summary>
/// <remarks>
/// <b>示すこと</b>: <see cref="MarqueeControl.CreatePresenter"/> を上書きするだけで、コントロールの見た目を<br/>
/// 派生提示層へ差し替えられる（入力の受付・効果の適用・ラベルの配置・描画はコントロールがそのまま担う）。<br/>
/// <b>差し替えの単位</b>: 外観（<see cref="MarqueeAppearance"/>）で表せる見た目はプロパティで指定し、<br/>
/// 破線枠・ガイド線のような<b>矩形の並び</b>で表す装飾は提示層の描画計画（<see cref="MarqueeDrawPlan"/>）が持つ。
/// </remarks>
internal sealed class GuideMarqueePreview : MarqueeControl
{
    /// <summary>見本に固定で与える選択矩形（対象座標）。</summary>
    private static readonly EsRect PreviewSelection = new(26, 22, 176, 112);

    /// <summary>見本の選択矩形と倍率を設定する。</summary>
    public GuideMarqueePreview()
    {
        ClientToSourceScaling = 1.0;
        SetSelection(PreviewSelection.ToAvaloniaRect());
    }

    /// <summary>派生提示層（ガイド線付きの見た目）を生成する。</summary>
    /// <param name="effects">コアが要求する効果の適用先（本コントロールが実装する）。</param>
    /// <param name="cursor">カーソルサービス。</param>
    /// <param name="options">初期設定（本コントロールの現在値）。</param>
    /// <returns>見本の提示層。</returns>
    protected override MarqueePresenter CreatePresenter(
        IMarqueeEffects effects, ICursorService cursor, MarqueeOptions options)
        => new GuideMarqueePresenter(effects, cursor, options);
}
