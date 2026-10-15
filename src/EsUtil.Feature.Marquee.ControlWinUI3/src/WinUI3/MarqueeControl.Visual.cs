using EsUtil.Feature.Marquee.WinUI3.Internal;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Microsoft.UI.Xaml;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace EsUtil.Feature.Marquee.WinUI3;

/// <summary>MarqueeControl の描画処理（partial 分割）。</summary>
/// <remarks>
/// <b>描画の手順は提示層が単一所有する</b>: 層の順序と描画 API への写し方は描画計画（<see cref="MarqueeDrawPlan"/>）が持ち、<br/>
/// 本クラスは自 UI の描画面へ写すだけである（<see cref="MarqueeSkiaPainter"/> / <see cref="MarqueeLabelPainter"/>）。<br/>
/// ラベルは文字の実寸を測るエンジンと描くエンジンを一致させるため、同じ描画面へ描く（<see cref="SkiaText"/>）。<br/>
/// 背景は塗らない（透明のままにして下の画像を透かす）。
/// </remarks>
public sealed partial class MarqueeControl
{
    /// <summary>直近に解決した描画計画（null = 未解決）。</summary>
    private MarqueeDrawPlan? _frame;

    /// <summary>直近に解決した描画計画の基準サイズ（DIP）。</summary>
    private EsSize _frameSize;

    /// <summary>描画内容の再解決と、描画面の再描画を要求する（描画に関わる変更の唯一の入口）。</summary>
    /// <remarks>
    /// 本 UI の描画面は <see cref="SKXamlCanvas"/>（基底クラス）が所有し、<c>Invalidate</c> が描画の要求である<br/>
    /// （無効化の要求は本メソッドへ集約し、呼び出し側へ <c>Invalidate</c> を直接書かせない）。<br/>
    /// 保持している描画計画を破棄し、次の描画で最新の状態を反映する。
    /// </remarks>
    public void InvalidateRender()
    {
        InvalidateFrame();
        Invalidate();
    }

    /// <summary>描画計画を自 UI の描画面へ描く（計画どおりに描く）。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標で描画できる状態であること）。 null 不可。</param>
    /// <param name="plan">描く描画計画。 null 不可。</param>
    /// <exception cref="ArgumentNullException"><paramref name="canvas"/> が null の場合。</exception>
    /// <remarks>
    /// <b>既定の描画をホストへ提供する入口</b>である（ホストが自前の描画と組み合わせて使う。 例: 自前の下地 → 本メソッド → 自前の上描き）。<br/>
    /// <b>計画どおり（外観の <c>Show*</c> を適用済みの層）に描く</b>ため、装飾を止めている場合は描かれない。<br/>
    /// 描画実装（<see cref="MarqueeSkiaPainter"/>）は本プロジェクトの内部であるため、外部へは本メソッドが窓口になる。<br/>
    /// 計画を必要とする場合は <see cref="Rendered"/> の <c>Content</c> を使う。
    /// </remarks>
    public static void DrawPlan(SKCanvas canvas, in MarqueeDrawPlan plan)
        => MarqueeSkiaPainter.Paint(canvas, in plan);

    /// <summary>描画面へ図形とラベルを描き、付加情報の描画機会をホストへ通知する。</summary>
    /// <param name="e">描画面の情報（表面・寸法）。</param>
    /// <remarks>
    /// <b>倍率は描画面（物理ピクセル）と割り当て領域（DIP）の比から求める</b>（XAML のルートが未確定の間は既定値のため、<br/>
    /// 保持値だけに頼ると 100% で描いてしまう）。<br/>
    /// 前フレームの描画を残さないため、自分の描画面を消してから描く。<br/>
    /// キャンバスを表示倍率でスケーリングしてから DIP 座標で描画する（コアの計算と座標系を一致させる）。
    /// </remarks>
    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);

        var info = e.Info;
        var scaling = ClientPixelScale.Resolve(info.Width, ActualWidth, ClientToPixelScaling);
        var canvas = e.Surface.Canvas;

        // 描画面は前フレームの内容を保持するため、自分の領域を消してから描く（背景は透明のままにする）
        canvas.Clear(SKColors.Transparent);

        canvas.Save();
        canvas.Scale((float)scaling);
        try
        {
            var plan = CurrentFrame(DpiScaler.ToDip(info.Size.ToEsSize(), scaling));

            MarqueeSkiaPainter.Paint(canvas, in plan);
            MarqueeLabelPainter.Paint(canvas, in plan);

            // 描画完了をホストへ通知する（操作・情報ラベルなどホスト固有の付加情報を描くための入口）
            Raise(Rendered, new MarqueeRenderedEventArgs(canvas, plan));
        }
        finally
        {
            canvas.Restore();
        }
    }

    /// <summary>1 フレーム分の描画計画を解決して保持する（同じ基準サイズでは再解決しない）。</summary>
    /// <param name="clientSize">描画領域のサイズ（DIP。 DIP 換算済みの描画先サイズ）。</param>
    /// <returns>そのフレームの描画計画。</returns>
    /// <remarks>
    /// 解決を 2 回行うと、選択矩形・装飾・ラベル文言の解決と確保が 1 フレームで重複する。<br/>
    /// 実寸の測定は描画 API に依存するため、本層が <see cref="SkiaText"/>（Skia の測定）を渡す<br/>
    /// （配置の計算（クランプ）は提示層が単一所有する。 背景の矩形は測定結果から解決されるため、<br/>
    /// 測定と描画が同じエンジンでないと文字が背景からはみ出して切れる）。<br/>
    /// 状態が変わると <see cref="InvalidateFrame"/> が破棄し、次の解決で最新の状態を反映する。
    /// </remarks>
    private MarqueeDrawPlan ResolveFrame(EsSize clientSize)
    {
        if (_frame is not { } cached || _frameSize != clientSize)
        {
            cached = _marquee.ResolveDrawPlan(clientSize, SkiaText.Measure);
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
    /// モニター間の移動で倍率が変わっても、枠線・ハンドル・マーカーの画面上の大きさを保つ（描画の倍率は描画面からも実測する）。<br/>
    /// 未確定（非数・0 以下）の値は基準値へ寄せる（規則は <see cref="DpiScaler.NormalizeScaling"/> が単一所有する）。
    /// </remarks>
    private void UpdateDeviceScaling()
    {
        if (XamlRoot is { } root)
        {
            ClientToPixelScaling = DpiScaler.NormalizeScaling(root.RasterizationScale);
        }
    }
}
