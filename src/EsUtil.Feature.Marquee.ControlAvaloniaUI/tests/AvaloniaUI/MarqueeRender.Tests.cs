using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Xunit;
using Rect = Avalonia.Rect;

namespace EsUtil.Feature.Marquee.AvaloniaUI.Tests;

/// <summary>描画の回帰テスト（図形とラベルが実際に描画されるか）。</summary>
/// <remarks>
/// 図形は子の描画層（<c>ShapeLayer</c>）が描くため、<b>親の <c>InvalidateVisual</c> では再描画されない</b><br/>
/// （子は自分の描画リストを持つ）。 状態の変更で図形が再描画されないと、選択矩形が表示されない不具合になる。<br/>
/// ラベルは実寸から配置矩形を決めるため、実寸を測らないと背景が内側余白だけの大きさになり文字が切り落とされる。<br/>
/// 本テストは描画完了イベント（<see cref="MarqueeControl.Rendered"/>）と要素の寸法で両方を検出する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class MarqueeRenderTests
{
    /// <summary>Render が呼ばれた回数を数えるだけのプローブ（描画パスが走ったことの確認用）。</summary>
    private sealed class Probe : Control
    {
        /// <summary>Render の呼び出し回数。</summary>
        public int Calls { get; private set; }

        /// <summary>描画時に呼ばれる。</summary>
        /// <param name="context">描画コンテキスト。</param>
        public override void Render(DrawingContext context)
        {
            Calls++;
            context.FillRectangle(Brushes.Red, new Rect(Bounds.Size));
        }
    }

    /// <summary>ラベル要素の文言（サイズラベル・カーソル座標ラベル）を列挙する。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <returns>表示されている文言の一覧。</returns>
    private static List<string> LabelTexts(MarqueeControl control)
        => [.. control.Children.OfType<Border>().Select(border => ((TextBlock)border.Child!).Text!)];

    /// <summary>ラベル要素の背景（配置矩形）を列挙する。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <returns>要素が占める矩形の一覧。</returns>
    private static List<Rect> LabelBounds(MarqueeControl control)
        => [.. control.Children.OfType<Border>().Select(border => border.Bounds)];

    /// <summary>状態の変更で図形の描画が要求されることを検証する（描画パスで Rendered が発火する）。</summary>
    /// <remarks>
    /// 図形は子の描画層が持つ描画リストに描かれるため、変更時に子も無効化しないと再描画されない<br/>
    /// （再描画されないと選択矩形・暗転マスクが表示されたまま更新されない）。
    /// </remarks>
    [AvaloniaFact]
    internal void 選択の変更で図形が再描画される()
    {
        var control = new MarqueeControl { Width = 200, Height = 200 };
        // 描画パスを確実に走らせるためのプローブ（同じ階層に置く）
        var probe = new Probe { Width = 200, Height = 200 };
        var root = new Panel { Width = 200, Height = 200 };
        root.Children.Add(probe);
        root.Children.Add(control);
        var window = new Window { Width = 200, Height = 200, Content = root };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var rendered = 0;
        var layers = 0;
        control.Rendered += (_, e) =>
        {
            rendered++;
            layers = e.Content.Layers.Count;
        };

        // 選択を作る（状態の変更 → 図形の再描画要求）
        control.SetSelection(new Rect(20, 20, 60, 60));
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();

        Assert.True(probe.Calls > 0, "描画パスが走っていない（テストの前提が成立していない）");
        Assert.True(rendered > 0, "選択の変更で図形が再描画されていない");
        Assert.True(layers > 0, "描く層が解決されていない");
        window.Close();
    }

    /// <summary>ラベル要素の配置矩形が文字の実寸を反映することを検証する。</summary>
    /// <remarks>
    /// 実寸を測らない（0 を返す）と、配置矩形が内側余白（4 DIP）だけの 8x8 になり、内側の文字が切り落とされる。
    /// </remarks>
    [AvaloniaFact]
    internal void ラベル要素の寸法が文字の実寸を反映する()
    {
        var control = new MarqueeControl
        {
            Width = 200,
            Height = 200,
            Appearance = MarqueeAppearance.Default with { ShowCoordLabel = false },
        };
        var window = new Window { Width = 200, Height = 200, Content = control };
        window.Show();
        control.SetSelection(new Rect(20, 20, 60, 60));
        Dispatcher.UIThread.RunJobs();

        var texts = LabelTexts(control);
        var bounds = LabelBounds(control);

        Assert.Single(texts);
        Assert.Single(bounds);

        // サイズラベルの文言はコアの書式（位置・寸法・表示倍率）を持つ
        Assert.StartsWith("(20, 20)", texts[0], StringComparison.Ordinal);

        // 文字の実寸 + 内側余白 × 2 の大きさになる（内側余白は既定 4.0）
        var padding = control.Appearance.LabelPadding;
        Assert.True(
            bounds[0].Width > padding * 2 && bounds[0].Height > padding * 2,
            $"ラベルの配置矩形が内側余白だけの大きさになっている（文字が切り落とされる）: {bounds[0]}");
        window.Close();
    }
}
