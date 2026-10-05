using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Interop;
using EsUtil.Platform.Core.Geometry;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace EsUtil.Feature.Marquee.AvaloniaUI;

/// <summary>
/// MarqueeControl のビジュアルツリー接続と描画処理（partial 分割）。
/// </summary>
/// <remarks>
/// ビジュアルツリー接続時に TopLevel のキーイベントを購読し、切断時に解除する。<br/>
/// 描画は提示層が解決した描画計画（<see cref="MarqueeDrawPlan"/>）を自 UI の描画面へ写し、座標変換（対象 → DIP）は座標変換ポートに問い合わせる
/// （描画は提示層が解決した計画を読むだけで、判断を持たない）。
/// </remarks>
public partial class MarqueeControl
{
    // ====================================================================
    // ビジュアルツリー接続・切断
    // ====================================================================

    /// <summary>ビジュアルツリー接続時に、属する TopLevel のキーイベントを購読する。</summary>
    /// <param name="e">ビジュアルツリー接続イベント。</param>
    /// <remarks>
    /// Drag 開始前（マウス押下前）は本コントロールにフォーカスがなく、コントロール自身のキーハンドラが呼ばれないため、<br/>
    /// 属するウィンドウのキーイベントを購読して矢印キー nudge を自己完結で処理する。<br/>
    /// <b>トンネル（Tunnel）で購読する</b>: バブリングでは祖先の <c>ScrollViewer</c> が先に矢印キーを処理して
    /// キーボードスクロールしてしまう（Crop モードのプレビューは ScrollViewer 内にある）。<br/>
    /// トンネルはルートから下降するため、祖先より先に受け取って処理済みにできる。
    /// これにより「コントロール自身のハンドラ（フォーカス時）と TopLevel 購読（フォーカス非依存）」の
    /// 2 経路を持たずに済み、同一キーの二重処理を防ぐ仕組みが不要になる（配送は 1 経路）。<br/>
    /// </remarks>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            _attachedTopLevel = topLevel;
            // handledEventsToo: true で購読する。
            // 他のコントロール（ScrollViewer 等）が e.Handled=true にしたキーでも呼ばれるようにするため。
            topLevel.AddHandler(InputElement.KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Tunnel, true);

            // ウィンドウの非アクティブ化（Alt+Tab 等）でも nudge を終了する。
            // Avalonia のフォーカスは TopLevel 単位で保持されるため、ウィンドウを離れてもコントロールはフォーカスを失わない
            // （Win32 の WM_KILLFOCUS に相当する契機。 MewUI 版のフォーカス喪失による nudge 終了と同じ扱いにする）。
            if (topLevel is WindowBase window)
            {
                _attachedWindow = window;
                window.Deactivated += OnWindowDeactivated;
            }
        }
    }

    /// <summary>ビジュアルツリー切断時に TopLevel のキーイベントの購読を解除する。</summary>
    /// <param name="e">ビジュアルツリー切断イベント。</param>
    /// <remarks>
    /// ウィンドウクローズ・Crop 終了等でビジュアルツリーから外れる際に、キーイベント購読を解除し、<br/>
    /// コアへ対話状態の初期化を要求する（nudge 中ならポインターを再表示して選択状態を初期化する）。
    /// </remarks>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_attachedTopLevel is { } topLevel)
        {
            topLevel.RemoveHandler(InputElement.KeyDownEvent, OnTopLevelKeyDown);
            _attachedTopLevel = null;
        }
        if (_attachedWindow is { } window)
        {
            window.Deactivated -= OnWindowDeactivated;
            _attachedWindow = null;
        }
        // ビジュアルツリーからの切断は UI のライフサイクルであるため、コアへは「対話状態の初期化」だけを要求する
        // （選択の破棄と、nudge 中だった場合のカーソル再表示はセッションが単一所有する）。
        _marquee.ResetSelection();
    }

    /// <summary>ウィンドウが非アクティブ化されたときに nudge を終了する。</summary>
    /// <param name="sender">イベント送信元（ウィンドウ）。</param>
    /// <param name="e">イベント引数。</param>
    /// <remarks>
    /// nudge 中はカーソルを非表示にするため、ウィンドウを離れた時点で終了して再表示する（非表示残留の防止）。<br/>
    /// 本コントロールはキーを TopLevel から購読するため、非アクティブ化＝キー入力の継続条件を失った時点である。
    /// </remarks>
    private void OnWindowDeactivated(object? sender, EventArgs e) => _marquee.EndNudge();

    // ====================================================================
    // 描画（描画内容の解決と描画先の用意だけを行い、手順は描画層へ委譲する）
    // ====================================================================

    /// <summary>図形（装飾・ハンドル・始点マーカー）を描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <remarks>
    /// <b>描画の手順は提示層が単一所有する</b>: 層の順序と描画 API への写し方は描画計画（<see cref="MarqueeDrawPlan"/>）が持ち、<br/>
    /// 本メソッドは自 UI の描画面へ写すだけである（<see cref="MarqueeContextPainter.Paint"/>）。<br/>
    /// <b>ラベルは <see cref="Border"/> + <see cref="TextBlock"/> の子要素として配置する</b>ため図形の層だけを描く<br/>
    /// （子は親より後に描かれるため常に最上位になる）。<br/>
    /// 選択枠線は装飾の外枠（同じ矩形の内側）と重なるため描画しない（別々に描くと線幅が二重になる）。
    /// </remarks>
    internal void RenderShapes(DrawingContext context)
    {
        // 描画コンテキストの原点は常に自コントロールの左上（0, 0）であるため、親座標（Bounds.X/Y）は渡さない。
        // 描画計画は配置フェーズが解決した値を共有する（未解決の場合のみ本メソッドが解決する）。
        var plan = CurrentFrame(Bounds.Size.ToEsSize());

        MarqueeContextPainter.Paint(context, in plan, ShowDefaultDecorations);

        // 描画完了をホストへ通知する（操作・情報ラベルなどホスト固有の付加情報を描くための入口）。
        Rendered?.Invoke(this, new MarqueeRenderedEventArgs(context, plan));
    }

    // ====================================================================
    // ラベル（テキスト要素として配置する）
    // ====================================================================

    /// <summary>配置中のラベル要素（背景 + テキスト）。 子の末尾側へ置くため常に最上位になる。</summary>
    private readonly List<Border> _labels = [];

    /// <summary>子要素の配置を確定し、ラベル要素を追加・更新する。</summary>
    /// <param name="finalSize">本コントロールへ割り当てられたサイズ（DIP）。</param>
    /// <returns>割り当てサイズ。</returns>
    /// <remarks>
    /// <b>位置はコアと描画層が決める</b>: コアのテキスト配置から <c>MarqueePresenter</c> が求めた配置指示に従って<br/>
    /// ラベル要素を配置するだけであり、本クラスは位置の計算を持たない。<br/>
    /// 要素は <see cref="Panel.Children"/> の末尾へ置く（子は親より後に描かれるため、ラベルは常に最上位になる）。
    /// </remarks>
    protected override Size ArrangeOverride(Size finalSize)
    {
        // 描画計画は本フェーズで 1 回だけ解決し、図形の描画（RenderShapes）と共有する。
        // ラベルは描画計画が持つ配置指示（実寸を反映済み）をそのまま反映する。
        SyncLabels(ResolveFrame(finalSize.ToEsSize()).Labels);

        // 図形を描く子も併せて無効化する（子は自分の描画リストを持ち、親の配置だけでは描き直されない）。
        // これにより、サイズ変更や追加直後の初回描画でも図形が最新の状態で描かれる。
        _shapes.InvalidateVisual();

        return base.ArrangeOverride(finalSize);
    }

    /// <summary>ラベル要素（背景 + テキスト）を配置指示へ同期する。</summary>
    /// <param name="placements">配置するラベル（描画順に並ぶ）。</param>
    /// <remarks>
    /// 余分な要素は親から外し、不足分は生成して子の末尾へ追加する（配置のたびに呼ばれる）。<br/>
    /// 各要素は左上寄せと <c>Margin</c> で絶対配置する（配置矩形はコアと描画層が単一所有する）。
    /// </remarks>
    private void SyncLabels(IReadOnlyList<MarqueeLabelPlacement> placements)
    {
        while (_labels.Count > placements.Count)
        {
            var last = _labels[^1];
            _labels.RemoveAt(_labels.Count - 1);
            Children.Remove(last);
        }

        while (_labels.Count < placements.Count)
        {
            var border = new Border { Child = new TextBlock { TextWrapping = TextWrapping.NoWrap } };
            _labels.Add(border);
            Children.Add(border);
        }

        for (var i = 0; i < placements.Count; i++)
        {
            Apply(_labels[i], placements[i]);
        }
    }

    /// <summary>1 つのラベル要素の内容と配置を配置指示に合わせる。</summary>
    /// <param name="border">更新する要素（背景）。</param>
    /// <param name="placement">配置指示（寸法は実寸を反映済み）。</param>
    private static void Apply(Border border, in MarqueeLabelPlacement placement)
    {
        border.Background = SolidColorBrushCache.GetOrCreate(placement.BackgroundColor);
        border.Padding = new Thickness(placement.PaddingX, placement.PaddingY);
        border.Width = placement.DipRect.Width;
        border.Height = placement.DipRect.Height;
        border.Margin = new Thickness(placement.DipRect.X, placement.DipRect.Y, 0, 0);
        border.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        border.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;

        if (border.Child is TextBlock text)
        {
            text.Text = placement.Text;
            text.FontSize = placement.FontSize;
            text.Foreground = SolidColorBrushCache.GetOrCreate(placement.TextColor);
        }
    }

    /// <summary>テキストの実寸を測定する（描画 API でしか測れない値）。</summary>
    /// <param name="text">測定するテキスト。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <returns>テキストの実寸（DIP）。</returns>
    /// <remarks>
    /// 配置矩形は実寸から決まるため、実寸を返さないと背景が内側余白だけの大きさになり文字が切り落とされる<br/>
    /// （測定の実装は <see cref="AvaloniaTextMeasure"/> が単一所有する）。
    /// </remarks>
    private static EsSize MeasureText(string text, double fontSize) => AvaloniaTextMeasure.Measure(text, fontSize);
}
