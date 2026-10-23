using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

using EsUtil.Ui.WinUI3.Interop.Drawing;

namespace EsUtil.Ui.WinUI3.Marquee.Internal;

/// <summary>描画計画（<see cref="MarqueeDrawPlan"/>）を XAML の要素へ写す層。</summary>
/// <remarks>
/// <b>位置付け</b>: 描く層とその順序は描画計画（<see cref="MarqueeDrawPlan.Layers"/>）が単一所有する。<br/>
/// 本クラスは順序を判断せず、層ごとに「矩形を積む」「ラベルを置く」だけを行う（座標・色・寸法はすべて計画に解決済み）。<br/>
/// <br/>
/// <b>要素の順序が描画順になる</b>: 子要素は追加順に前面へ合成されるため、層の順序をそのまま子の順序へ写す。<br/>
/// ラベルは<b>最後に置く</b>（他の層より前面になる）。<br/>
/// <br/>
/// <b>単位</b>: 計画の矩形は DIP であるため、要素へはそのままの値で配置する（換算しない）。<br/>
/// <br/>
/// <b>矩形で表せない装飾</b>: ホストが <see cref="MarqueeControl.Rendered"/> で追加した矩形は
/// <see cref="OverlayLayer"/> へ積む（計画の層より前面、ラベルより背面）。
/// </remarks>
internal sealed partial class MarqueeVisualLayer : Canvas
{
    /// <summary>装飾・ハンドル・始点マーカーの層。</summary>
    private readonly MarqueeShapeLayer _shapes = new() { IsHitTestVisible = false };

    /// <summary>ホストが追加した矩形の層。</summary>
    private readonly MarqueeShapeLayer _overlay = new() { IsHitTestVisible = false };

    /// <summary>ラベルを載せる層。</summary>
    private readonly Canvas _labels = new() { IsHitTestVisible = false };

    /// <summary>ラベルの要素（件数が変わらなければ使い回す）。</summary>
    private readonly List<Border> _labelBorders = [];

    /// <summary>ラベルの文字（件数が変わらなければ使い回す）。</summary>
    private readonly List<TextBlock> _labelTexts = [];

    /// <summary>計画から組み立てた矩形（毎回の更新で詰め直す）。</summary>
    private readonly DrawList _rects = new();

    /// <summary>表示するラベルの配置指示（毎回の更新で詰め直す）。</summary>
    private readonly List<MarqueeLabelPlacement> _visibleLabels = [];

    /// <summary>表示の層を初期化する。</summary>
    /// <remarks>子の順序は「図形 → ホストの追加分 → ラベル」である（計画の層の順序に対応する）。</remarks>
    public MarqueeVisualLayer()
    {
        IsHitTestVisible = false;

        Children.Add(_shapes);
        Children.Add(_overlay);
        Children.Add(_labels);
    }

    /// <summary>ホストが追加した矩形の層を取得する。</summary>
    /// <value>追加描画を受け取る層（計画の層より前面、ラベルより背面）。</value>
    public MarqueeShapeLayer OverlayLayer => _overlay;

    /// <summary>描画計画を要素へ反映する。</summary>
    /// <param name="plan">反映する計画。</param>
    public void Apply(MarqueeDrawPlan plan)
    {
        _rects.Clear();
        _visibleLabels.Clear();
        MarqueeShapePlan.Build(plan, _rects, _visibleLabels);

        // 計画の矩形は DIP であるため、要素へはそのまま配置する（換算係数は 1）
        _shapes.Apply(_rects, 1);
        ApplyLabels();
    }

    /// <summary>ホストが追加した矩形を反映する。</summary>
    /// <param name="list">追加された矩形。 null 不可。</param>
    /// <exception cref="ArgumentNullException"><paramref name="list"/> が null の場合。</exception>
    public void ApplyOverlay(DrawList list)
    {
        _overlay.Apply(list, 1);
    }

    /// <summary>ラベルを反映する。</summary>
    private void ApplyLabels()
    {
        EnsureLabels(_visibleLabels.Count);

        for (var i = 0; i < _labelBorders.Count; i++)
        {
            var border = _labelBorders[i];
            var text = _labelTexts[i];

            if (i >= _visibleLabels.Count)
            {
                border.Visibility = Visibility.Collapsed;
                continue;
            }

            var label = _visibleLabels[i];
            SetLeft(border, label.DipRect.X);
            SetTop(border, label.DipRect.Y);
            border.Width = label.DipRect.Width;
            border.Height = label.DipRect.Height;
            border.Background = new SolidColorBrush(ToColor(MarqueeShapePlan.ToArgb(label.BackgroundColor)));

            text.Text = label.Text;
            text.FontSize = label.FontSize;
            text.Foreground = new SolidColorBrush(ToColor(MarqueeShapePlan.ToArgb(label.TextColor)));
            text.Padding = new Thickness(label.PaddingX, label.PaddingY, label.PaddingX, label.PaddingY);

            border.Visibility = Visibility.Visible;
        }
    }

    /// <summary>ラベルの要素を必要な数まで用意する。</summary>
    /// <param name="count">必要な件数。</param>
    private void EnsureLabels(int count)
    {
        while (_labelBorders.Count < count)
        {
            var text = new TextBlock { IsHitTestVisible = false };
            var border = new Border { IsHitTestVisible = false, Child = text };
            _labelTexts.Add(text);
            _labelBorders.Add(border);
            _labels.Children.Add(border);
        }
    }

    /// <summary>色を WinUI の色へ変換する。</summary>
    /// <param name="argb">色（<c>0xAARRGGBB</c>）。</param>
    /// <returns>変換した色。</returns>
    private static Color ToColor(uint argb) => Color.FromArgb(
        (byte)((argb >> 24) & 0xFF),
        (byte)((argb >> 16) & 0xFF),
        (byte)((argb >> 8) & 0xFF),
        (byte)(argb & 0xFF));
}
