using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
namespace EsUtil.Ui.WinUI3.Loupe.Internal;

/// <summary>描画計画（<see cref="LoupeDrawPlan"/>）を XAML と Composition の要素へ写す層。</summary>
/// <remarks>
/// <b>位置付け</b>: 描く層とその順序は描画計画（<see cref="LoupeDrawPlan.Layers"/>）が単一所有する。<br/>
/// 本クラスは順序を判断せず、層ごとに「矩形を積む」「ラベルを置く」だけを行う（座標・色・寸法はすべて計画に解決済み）。<br/>
/// <br/>
/// <b>要素の順序が描画順になる</b>: 子要素は追加順に前面へ合成されるため、層の順序をそのまま子の順序へ写す。<br/>
/// ラベルは<b>最後に置く</b>（他の層より前面になる）。<br/>
/// <br/>
/// <b>単位</b>: 計画の矩形は物理ピクセルであるため、要素へ写すときに <c>1 / ClientToPixelScaling</c> で DIP へ換算する<br/>
/// （換算は <see cref="LoupeShapeLayer"/> と本クラスの 2 か所に閉じる）。<br/>
/// <br/>
/// <b>矩形で表せない装飾</b>: ホストが <see cref="LoupeControl.Rendered"/> で追加した矩形は本クラスの
/// <see cref="OverlayLayer"/> へ積む（計画の層より前面、ラベルより背面）。
/// </remarks>
internal sealed partial class LoupeVisualLayer : Canvas
{
    /// <summary>拡大画像の層。</summary>
    private readonly LoupeImageView _image = new() { IsHitTestVisible = false };

    /// <summary>十字線の層。</summary>
    private readonly LoupeShapeLayer _crosshair = new() { IsHitTestVisible = false };

    /// <summary>枠線の層。</summary>
    private readonly LoupeShapeLayer _border = new() { IsHitTestVisible = false };

    /// <summary>ホストが追加した矩形の層。</summary>
    private readonly LoupeShapeLayer _overlay = new() { IsHitTestVisible = false };

    /// <summary>ラベルを載せる層（要素の追加順を保つため <see cref="Canvas"/> を使う）。</summary>
    private readonly Canvas _labels = new() { IsHitTestVisible = false };

    /// <summary>ラベルの要素（件数が変わらなければ使い回す）。</summary>
    private readonly List<Border> _labelBorders = [];

    /// <summary>ラベルの文字（件数が変わらなければ使い回す）。</summary>
    private readonly List<TextBlock> _labelTexts = [];


    /// <summary>拡大画像の表示層を初期化する。</summary>
    /// <remarks>子の順序は「画像 → 十字線 → 枠線 → ホストの追加分 → ラベル」である（計画の層の順序に対応する）。</remarks>
    public LoupeVisualLayer()
    {
        IsHitTestVisible = false;

        Children.Add(_image);
        Children.Add(_crosshair);
        Children.Add(_border);
        Children.Add(_overlay);
        Children.Add(_labels);
    }

    /// <summary>拡大画像の表示層を取得する（ホストが画像を設定するための窓口）。</summary>
    /// <value>拡大画像の層。</value>
    public LoupeImageView Image => _image;

    /// <summary>ホストが追加した矩形の層を取得する。</summary>
    /// <value>追加描画を受け取る層（計画の層より前面、ラベルより背面）。</value>
    public LoupeShapeLayer OverlayLayer => _overlay;

    /// <summary>描画計画を要素へ反映する。</summary>
    /// <param name="plan">反映する計画。 null 不可。</param>
    /// <param name="pixelToDip">物理ピクセル → DIP の係数（1 / スケーリング係数）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="plan"/> が null の場合。</exception>
    /// <remarks>
    /// <b>層ごとに外観の表示可否を見る</b>: 計画は表示可否で値を消さないため、置くかどうかの判断は本クラス（描画側）が行う。<br/>
    /// 拡大画像とペーストボード（下地）は表示可否を持たず、常に反映する。
    /// </remarks>
    public void Apply(LoupeDrawPlan plan, double pixelToDip)
    {

        ApplyLayerSize(plan.ClientPixelRect.Width * pixelToDip);
        ApplyImage(plan);
        ApplyShapes(plan, pixelToDip);
        ApplyLabels(plan);
    }

    /// <summary>層の大きさをルーペの表示サイズ（DIP）へ合わせる。</summary>
    /// <param name="size">一辺の長さ（DIP）。</param>
    /// <remarks>
    /// <b>拡大画像の層は自身の大きさで切り取る</b>（<see cref="LoupeImageView"/> が <c>Clip</c> を自分の矩形へ設定する）。<br/>
    /// <c>Canvas</c> の子は既定で要求サイズ 0×0 のため、大きさを与えないと切り取りが常に空になり<br/>
    /// <b>拡大画像が 1 枚も描かれない</b>（下地の色だけが見える。 実測 2026-10-25）。<br/>
    /// 図形・追加描画は切り取りを持たないため表示は変わらないが、当たり判定と切り取りの基準を揃えるために同じ大きさを与える。
    /// </remarks>
    private void ApplyLayerSize(double size)
    {
        Width = size;
        Height = size;

        _image.Width = size;
        _image.Height = size;
        _crosshair.Width = size;
        _crosshair.Height = size;
        _border.Width = size;
        _border.Height = size;
        _overlay.Width = size;
        _overlay.Height = size;
        _labels.Width = size;
        _labels.Height = size;
    }

    /// <summary>拡大画像を反映する。</summary>
    /// <param name="plan">描画計画。</param>
    private void ApplyImage(LoupeDrawPlan plan)
    {
        if (plan.MagnifiedImage is not { } magnified)
        {
            return;
        }

        _image.SetMagnification(
            magnified.CropSourceRect.X,
            magnified.CropSourceRect.Y,
            magnified.DrawClientPixelRect.X,
            magnified.DrawClientPixelRect.Y,
            plan.ZoomLevel,
            plan.ClientToPixelScaling);
    }

    /// <summary>十字線と枠線を反映する。</summary>
    /// <param name="plan">描画計画。</param>
    /// <param name="pixelToDip">物理ピクセル → DIP の係数。</param>
    /// <remarks>どの矩形を置くかは <see cref="LoupeShapePlan"/> が単一所有する。</remarks>
    private void ApplyShapes(LoupeDrawPlan plan, double pixelToDip)
    {
        _crosshair.Apply(LoupeShapePlan.BuildCrosshair(plan), pixelToDip);
        _border.Apply(LoupeShapePlan.BuildBorder(plan), pixelToDip);
    }
    /// <summary>ラベルを反映する。</summary>
    /// <param name="plan">描画計画。</param>
    /// <remarks>表示が有効な指示だけを置く（表示可否は配置指示が持つ）。 文字の実寸は計画が確保済みである。</remarks>
    private void ApplyLabels(LoupeDrawPlan plan)
    {
        EnsureLabels(plan.Labels.Count);

        for (var i = 0; i < _labelBorders.Count; i++)
        {
            var border = _labelBorders[i];
            var text = _labelTexts[i];

            if (i >= plan.Labels.Count || !plan.Labels[i].IsVisible)
            {
                border.Visibility = Visibility.Collapsed;
                continue;
            }

            var label = plan.Labels[i];
            SetLeft(border, label.DipRect.X);
            SetTop(border, label.DipRect.Y);
            border.Width = label.DipRect.Width;
            border.Height = label.DipRect.Height;
            border.CornerRadius = new CornerRadius(Math.Max(0, label.CornerRadius));
            border.Background = new SolidColorBrush(ToColor(LoupeShapePlan.ToArgb(label.BackgroundColor)));

            text.Text = label.Text;
            text.FontSize = label.FontSize;
            text.Foreground = new SolidColorBrush(ToColor(LoupeShapePlan.ToArgb(label.TextColor)));
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
