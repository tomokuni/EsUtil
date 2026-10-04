using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Feature.Loupe.AvaloniaUI.Sample.Presentation;

/// <summary>派生提示層（<see cref="GridLoupePresenter"/>）の描画計画を描く見本。</summary>
/// <remarks>
/// <b>示すこと</b>: 提示層（<see cref="GridLoupePresenter"/>）と描画実装（<see cref="LoupeContextPainter"/>）は<br/>
/// ともに公開型であり、コントロールを通さずに組み合わせられる（コントロールは「入力の転送 + ラベルの要素配置」を足すだけである）。<br/>
/// <br/>
/// <b>見本の画像</b>: 拡大の元画像は本見本が自前で用意する（<c>EsBitmap</c> は UI 非依存の画素バッファである）。<br/>
/// 文字を描かない理由は、ラベルを UI 要素として置くのが本 UI の既定であり、本見本は図形だけを見せるためである。
/// </remarks>
internal sealed class GridLoupePreview : Control
{
    /// <summary>見本のルーペの一辺（DIP）。 プレビューの高さも同じ値にする。</summary>
    internal const int ClientSize = 150;

    /// <summary>見本の拡大率。</summary>
    private const int ZoomLevel = 6;

    /// <summary>見本の拡大中心（元画像ピクセル）。</summary>
    private static readonly EsPoint CenterPoint = new(120, 80);

    /// <summary>見本の元画像（自前で用意する画素バッファ）。</summary>
    private static readonly EsBitmap Source = CreateSampleSource();

    /// <summary>テキストの実寸測定に使うブラシ（測定に色は影響しないため静的共有）。</summary>
    private static readonly SolidColorBrush MeasureBrush = new(Colors.White);

    /// <summary>見本の提示層。</summary>
    private readonly GridLoupePresenter _presenter = new();

    /// <summary>見本の設定（サイズ・拡大率・中心・倍率）を設定する。</summary>
    public GridLoupePreview()
    {
        _presenter.Options = LoupeOptions.Default with { RequestedSize = ClientSize, ZoomLevel = ZoomLevel };
        _presenter.CenterPoint = CenterPoint;
        _presenter.ClientToPixelScaling = 1.0;
    }

    /// <summary>提示層が解決した描画計画を描く。</summary>
    /// <param name="context">描画先の描画コンテキスト。</param>
    public override void Render(DrawingContext context)
    {
        var layout = _presenter.ResolveLayout(new EsSize(Bounds.Width, Bounds.Height));
        var plan = _presenter.ResolveDrawPlan(layout.ClientSize, MeasureText);

        LoupeContextPainter.Paint(context, Source, in plan);
    }

    /// <summary>テキストの実寸を測定する（描画 API でしか測れない値）。</summary>
    /// <param name="text">測定するテキスト。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <returns>テキストの実寸（DIP）。</returns>
    private static EsSize MeasureText(string text, double fontSize)
    {
        var formatted = new FormattedText(
            text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, fontSize, MeasureBrush);

        return new EsSize(formatted.Width, formatted.Height);
    }

    /// <summary>見本の元画像（グラデーション）を生成する。</summary>
    /// <returns>幅 240 × 高さ 160 の画素バッファ。</returns>
    private static EsBitmap CreateSampleSource()
    {
        var bitmap = EsBitmap.Create(240, 160);

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                bitmap.SetPixel(x, y, 60, (byte)(y * 255 / bitmap.Height), (byte)(x * 255 / bitmap.Width), 255);
            }
        }

        return bitmap;
    }
}
