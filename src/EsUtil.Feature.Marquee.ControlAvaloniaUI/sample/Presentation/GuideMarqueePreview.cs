using Avalonia.Controls;
using Avalonia.Media;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Marquee.AvaloniaUI.Sample.Presentation;

/// <summary>派生提示層（<see cref="GuideMarqueePresenter"/>）の描画計画を描く見本。</summary>
/// <remarks>
/// <b>示すこと</b>: 提示層（<see cref="GuideMarqueePresenter"/>）と描画実装（<see cref="MarqueeContextPainter"/>）は<br/>
/// ともに公開型であり、コントロールを通さずに組み合わせられる（コントロールは「入力の転送 + ラベルの要素配置」を足すだけである）。<br/>
/// <br/>
/// <b>文字を描かない理由</b>: ラベルは UI 要素として置くものが本 UI の既定であり、本見本は図形（装飾・ハンドル）だけを見せる。<br/>
/// そのためテキストの実寸は測定しない（計画の解決に必要な関数として 0 サイズを返す）。
/// </remarks>
internal sealed class GuideMarqueePreview : Control
{
    /// <summary>見本に固定で与える選択矩形（対象座標）。</summary>
    private static readonly EsRect PreviewSelection = new(26, 22, 176, 112);

    /// <summary>見本の提示層（画面を持たないため効果は空実装）。</summary>
    private readonly GuideMarqueePresenter _presenter = new(new HeadlessMarqueeEffects());

    /// <summary>見本の選択矩形と倍率を設定する。</summary>
    public GuideMarqueePreview()
    {
        _presenter.ClientToSourceScaling = 1.0;
        _presenter.SetSelection(PreviewSelection);
    }

    /// <summary>提示層が解決した描画計画を描く。</summary>
    /// <param name="context">描画先の描画コンテキスト。</param>
    public override void Render(DrawingContext context)
        => MarqueeContextPainter.Paint(context, _presenter.ResolveDrawPlan(new EsSize(Bounds.Width, Bounds.Height), static (_, _) => default));
}
