using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Feature.Loupe.AvaloniaUI;

/// <summary>ルーペのビットマップ生成パイプライン（ソース解決・描画の合成）。</summary>
/// <remarks>
/// <see cref="LoupeControl"/> から、Avalonia のソース画像を解決してルーペ画像（<see cref="EsBitmap"/>）を生成する<br/>
/// パイプラインを分離する（描画の手順と値の解釈は UI 非依存の描画層が単一所有する）。<br/>
/// <b>UI 固有の責務</b>: Avalonia の <see cref="IImage"/> 型判定と、<see cref="Bitmap"/> → <see cref="EsBitmap"/> の複製、<br/>
/// 描画の UI スレッドへのディスパッチ、<b>オフスクリーン ビットマップ（<see cref="RenderTargetBitmap"/>）の生成</b>。
/// 切り出しの可否（ソースと中心）と破棄済みの扱いはコアが単一所有する。
/// </remarks>
internal static class LoupeRenderPipeline
{
    /// <summary>オフスクリーン ビットマップの DPI（1 ピクセル = 1 DIP として描く）。</summary>
    private static readonly Vector s_dpi = new(DpiScaler.StandardDpi, DpiScaler.StandardDpi);

    /// <summary>画像抽出を非同期に実行し、Avaloniaの描画オブジェクトをUIスレッドで合成する。</summary>
    /// <param name="source">ルーペのソース画像。</param>
    /// <param name="plan">描画層が解決した描画計画（層の順序・矩形・拡大画像の幾何を持つ）。</param>
    /// <param name="drawDecorations">装飾（十字線・枠線）を描くかどうか。</param>
    /// <returns>生成したルーペ画像（<see cref="EsBitmap"/>）。</returns>
    /// <exception cref="NotSupportedException">
    /// <paramref name="source"/> が <see cref="Bitmap"/> / <see cref="EsBitmapAvaloniaImage"/> 以外の場合。
    /// </exception>
    /// <remarks>
    /// 1. 未対応型の判定（<see cref="LoupeSourceResolver.IsSourceSupported"/>）を行い、未対応型は例外にする。<br/>
    /// 2. 切り出し元の <see cref="EsBitmap"/> を解決する（<see cref="Bitmap"/> は複製、<see cref="EsBitmapAvaloniaImage"/> は共有参照）。<br/>
    /// 3. 描画（切り出し・拡大・十字線・枠線）は <see cref="LoupeContextPainter"/> へ委譲し、UI スレッドで行う<br/>
    /// （切り出しの可否と破棄済みソースの扱いは描画層が単一所有する）。
    /// </remarks>
    public static async Task<EsBitmap> RenderAsync(IImage? source, LoupeDrawPlan plan, bool drawDecorations = true)
    {
        // 未対応型の判定は LoupeSourceResolver（単一所有）へ委譲する。
        if (!LoupeSourceResolver.IsSourceSupported(source))
        {
            throw new NotSupportedException(
                $"LoupeControl は {source!.GetType().FullName} をサポートしていません。Bitmap または EsBitmapAvaloniaImage を指定してください。");
        }

        // 抽出元の EsBitmap を解決する（Bitmap は複製、EsBitmapAvaloniaImage は共有参照。 破棄済みは null）
        var resolved = LoupeSourceResolver.Resolve(source, out var sourceBitmap);

        try
        {
            // 切り出し（EsBitmap.Crop = 行コピーとパディングの透明化のみで軽量）と合成（RenderTargetBitmap / DrawingContext）は UI スレッドで行う。
            // Task.Run によるバックグラウンド抽出は、マウス移動中の連続再描画でスレッドプールが競合し、
            // 抽出結果が UI スレッドへ戻る前に次の再描画が要求されてルーペ画像が移動停止まで更新されない問題を生む。
            return await Dispatcher.UIThread.InvokeAsync(
                () => Rasterize(sourceBitmap, in plan, drawDecorations),
                DispatcherPriority.Render);
        }
        finally
        {
            // Bitmap 由来の場合のみ複製を破棄する（EsBitmapAvaloniaImage の共有参照は破棄しない）。
            if (resolved.IsSourceBitmap)
            {
                sourceBitmap!.Dispose();
            }
        }
    }

    /// <summary>描画計画を描画実装へ渡し、結果を <see cref="EsBitmap"/> として返す。</summary>
    /// <param name="source">拡大元の画像。</param>
    /// <param name="plan">描画層が解決した描画計画（層の順序・矩形・拡大画像の幾何を持つ）。</param>
    /// <param name="drawDecorations">装飾（十字線・枠線）を描くかどうか。</param>
    /// <returns>描画結果（ホストがピクセルへオーバーレイを描画できる）。</returns>
    /// <remarks>
    /// オフスクリーン ビットマップは UI 固有の機構（Avalonia の <see cref="RenderTargetBitmap"/>）であるため本層が生成し、<br/>
    /// 描画の手順（層の順序・写し方）は <see cref="LoupeContextPainter"/> へ委譲する。
    /// </remarks>
    private static EsBitmap Rasterize(EsBitmap? source, in LoupeDrawPlan plan, bool drawDecorations)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(plan.ClientPixelSize, plan.ClientPixelSize), s_dpi);

        using (var context = bitmap.CreateDrawingContext())
        {
            LoupeContextPainter.Paint(context, source, in plan, drawDecorations);
        }

        return bitmap.ToEsBitmap();
    }
}
