using EsUtil.Platform.Core.Common;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Ui.WinUI3.Marquee.Internal;
using Microsoft.UI.Xaml;

namespace EsUtil.Ui.WinUI3.Marquee;

/// <summary>MarqueeControl の描画処理（partial 分割）。</summary>
/// <remarks>
/// <b>描画の手順は提示層が単一所有する</b>: 層の順序と描画 API への写し方は描画計画（<see cref="MarqueeDrawPlan"/>）が持ち、<br/>
/// 本クラスは自 UI の要素へ写すだけである（<see cref="MarqueeVisualLayer"/>）。<br/>
/// ラベルは文字の実寸を測るエンジンと表示するエンジンを一致させるため、<see cref="MarqueeTextMeasure"/> で測る。<br/>
/// 背景は塗らない（透明のままにして下の画像を透かす）。
/// </remarks>
public sealed partial class MarqueeControl
{
    /// <summary>表示の層（子要素の順序が描画順になる）。</summary>
    private readonly MarqueeVisualLayer _visual = new();

    /// <summary>直近に解決した描画計画（null = 未解決）。</summary>
    private MarqueeDrawPlan? _frame;

    /// <summary>直近に解決した描画計画の基準サイズ（DIP）。</summary>
    private EsSize _frameSize;

    /// <summary>描画内容の再解決と、描画面の再描画を要求する（描画に関わる変更の唯一の入口）。</summary>
    /// <remarks>
    /// 本 UI の面は <see cref="MarqueeVisualLayer"/> が所有し、要素の更新が描画の要求である<br/>
    /// （更新の要求は本メソッドへ集約し、呼び出し側へ要素の更新を直接書かせない）。<br/>
    /// 保持している描画計画を破棄し、次の描画で最新の状態を反映する。
    /// </remarks>
    public void InvalidateRender()
    {
        InvalidateFrame();
        ApplyVisual();
    }

    /// <summary>描画計画を自 UI の描画面へ描く（計画どおりに描く）。</summary>
    /// <summary>表示を最新の状態へ反映する（要素の更新の唯一の入口）。</summary>
    /// <remarks>
    /// 描画計画（描画層が解決した幾何・外観・層の順序）を公開プロパティから組み立て、要素へ写す。<br/>
    /// <b>要素の順序が描画順になる</b>（図形 → ホストの追加分 → ラベル）。<br/>
    /// <b>追加描画も要素として置く</b>: 移行前は描画 API をホストへ渡していたが、
    /// 現在は <see cref="MarqueeRenderedEventArgs.AddRect"/> で受け取った矩形を追加の層へ置く。
    /// </remarks>
    private void ApplyVisual()
    {
        try
        {
            var plan = CurrentFrame(new EsSize(ActualWidth, ActualHeight));

            // 層の順序と描画内容は MarqueeVisualLayer が計画に従って写す
            _visual.Apply(plan);

            var args = new MarqueeRenderedEventArgs(new MarqueeRectList(), plan);
            Raise(Rendered, args);
            _visual.ApplyOverlay(args.Overlay);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 表示の例外はアプリを落とさずログへ記録する（要素は中途半端な状態になりうるが、次の更新で復帰する）
            Log.LogOperationError(ex, nameof(ApplyVisual));
        }
    }

    /// <summary>1 フレーム分の描画計画を解決して保持する（同じ基準サイズでは再解決しない）。</summary>
    /// <param name="clientSize">描画領域のサイズ（DIP。 DIP 換算済みの描画先サイズ）。</param>
    /// <returns>そのフレームの描画計画。</returns>
    /// <remarks>
    /// 解決を 2 回行うと、選択矩形・装飾・ラベル文言の解決と確保が 1 フレームで重複する。<br/>
    /// 実寸の測定は表示する要素と同じエンジンで行うため、本層が <see cref="MarqueeTextMeasure"/> を渡す<br/>
    /// （配置の計算（クランプ）は提示層が単一所有する。 背景の矩形は測定結果から解決されるため、<br/>
    /// 測定と描画が同じエンジンでないと文字が背景からはみ出して切れる）。<br/>
    /// 状態が変わると <see cref="InvalidateFrame"/> が破棄し、次の解決で最新の状態を反映する。
    /// </remarks>
    private MarqueeDrawPlan ResolveFrame(EsSize clientSize)
    {
        if (_frame is not { } cached || _frameSize != clientSize)
        {
            cached = _marquee.ResolveDrawPlan(clientSize, MarqueeTextMeasure.Measure);
            cached = DrawPlanResolver?.Invoke(cached) ?? cached;
            _frame = cached;
            _frameSize = clientSize;
        }

        return cached;
    }

    /// <summary>保持している描画計画を取得する（未解決の場合のみ解決する）。</summary>
    /// <param name="clientSize">描画領域のサイズ（DIP。 未解決時の解決にだけ使う）。</param>
    /// <returns>そのフレームの描画計画。</returns>
    private MarqueeDrawPlan CurrentFrame(EsSize clientSize) => _frame ?? ResolveFrame(clientSize);

    /// <summary>保持している描画計画を破棄する（次の解決で最新の状態を反映する）。</summary>
    private void InvalidateFrame() => _frame = null;

    /// <summary>読み込み時に表示倍率の変化を購読し、現在の倍率を反映する。</summary>
    /// <param name="sender">イベント送信元（本コントロール）。</param>
    /// <param name="e">読み込みイベント。</param>
    /// <remarks>購読は解除してから登録する（読み込みと破棄が繰り返されても二重購読しない）。</remarks>
    private void OnControlLoaded(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is { } root)
        {
            root.Changed -= OnXamlRootChanged;
            root.Changed += OnXamlRootChanged;
        }

        UpdateDeviceScaling();
    }

    /// <summary>破棄時に表示倍率の変化の購読を解除する。</summary>
    /// <param name="sender">イベント送信元（本コントロール）。</param>
    /// <param name="e">破棄イベント。</param>
    private void OnControlUnloaded(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is { } root)
        {
            root.Changed -= OnXamlRootChanged;
        }
    }

    /// <summary>XAML のルートの変化（表示倍率・サイズ）に追従する。</summary>
    /// <param name="sender">変化した XAML のルート。</param>
    /// <param name="args">変更内容。</param>
    private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => UpdateDeviceScaling();

    /// <summary>表示倍率（DIP → 物理ピクセル）を現在の値へ合わせる。</summary>
    /// <remarks>
    /// モニター間の移動で倍率が変わっても、枠線・ハンドル・マーカーの画面上の大きさを保つ。<br/>
    /// 未確定（非数・0 以下）の値は基準値へ寄せる（規則は <see cref="DpiScaler.NormalizeScaling"/> が単一所有する）。
    /// </remarks>
    private void UpdateDeviceScaling()
    {
        ClientToPixelScaling = DpiScaler.NormalizeScaling(XamlRoot?.RasterizationScale ?? ClientToPixelScaling);
        ApplyVisual();
    }
}
