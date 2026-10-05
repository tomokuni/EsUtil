using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Interop;

namespace EsUtil.Feature.Loupe.AvaloniaUI.Sample.Presentation;

/// <summary>派生提示層（<see cref="GridLoupePresenter"/>）を使う見本コントロール。</summary>
/// <remarks>
/// <b>示すこと</b>: <see cref="LoupeControl.CreatePresenter"/> を上書きするだけで、ルーペの見た目を<br/>
/// 派生提示層（中心の格子線 + 四隅の鉤）へ差し替えられる（表示位置・ラベル配置・描画はコントロールがそのまま担う）。<br/>
/// <b>見本の画像</b>: 拡大の元画像は本見本が自前で用意する（<see cref="EsBitmap"/> は UI 非依存の画素バッファである）。<br/>
/// <b>配置の基準になる親</b>: 本見本は <see cref="Avalonia.Controls.Border"/> の中に置くため親がパネルではなく、<br/>
/// 位置の補正は行われない（親領域の基準が無いため。 規則はコアが単一所有する）。
/// </remarks>
internal sealed class GridLoupePreview : LoupeControl
{
    /// <summary>見本のルーペの一辺（DIP）。 プレビューの高さも同じ値にする。</summary>
    internal const int ClientSize = 150;

    /// <summary>見本の拡大率。</summary>
    private const int ZoomLevel = 6;

    /// <summary>見本の拡大中心（元画像ピクセル）。</summary>
    private static readonly EsPoint s_previewCenter = new(120, 80);

    /// <summary>見本の元画像（自前で用意する画素バッファ）。</summary>
    private static readonly EsBitmap s_previewSource = CreateSampleSource();

    /// <summary>見本の設定（サイズ・拡大率・中心・倍率）を設定する。</summary>
    public GridLoupePreview()
    {
        Options = LoupeOptions.Default with { RequestedSize = ClientSize, ZoomLevel = ZoomLevel };
        ClientToPixelScaling = 1.0;
        Source = s_previewSource.ToAvaloniaImage();
        CenterPoint = s_previewCenter.ToAvaloniaPoint();
        // 本見本は常に表示する（既定は非表示。 表示可否はホストが指示する）
        IsVisible = true;
    }

    /// <summary>派生提示層（格子線と四隅の鉤）を生成する。</summary>
    /// <returns>見本の提示層。</returns>
    protected override LoupePresenter CreatePresenter() => new GridLoupePresenter();

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
