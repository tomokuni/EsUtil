using Avalonia;
using EsUtil.Feature.Loupe.AvaloniaUI.Internal;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Feature.Loupe.AvaloniaUI;

/// <summary>ルーペのビットマップ生成パイプライン（ソース解決・描画の合成）。</summary>
/// <remarks>
/// <see cref="LoupeControl"/> から、Avalonia のソース画像を解決してルーペ画像（<see cref="EsReadOnlyBitmap"/>）を生成する<br/>
/// パイプラインを分離する（描画の手順と値の解釈は UI 非依存の描画層が単一所有する）。<br/>
/// <b>UI 固有の責務</b>: Avalonia の <see cref="IImage"/> 型判定と、<see cref="Bitmap"/> → <see cref="EsReadOnlyBitmap"/> の複製、<br/>
/// UI スレッド外からの呼び出しの描画フェーズへのディスパッチ、<b>オフスクリーン ビットマップ（<see cref="RenderTargetBitmap"/>）の生成</b>。
/// 切り出しの可否（ソースと中心）と破棄済みの扱いはコアが単一所有する。
/// </remarks>
internal static class LoupeRenderPipeline
{
    /// <summary>オフスクリーン ビットマップの DPI（1 ピクセル = 1 DIP として描く）。</summary>
    private static readonly Vector s_dpi = new(DpiScaler.StandardDpi, DpiScaler.StandardDpi);

    /// <summary>ルーペ画像を生成する（切り出し元の解決・描画の合成）。</summary>
    /// <param name="source">ルーペのソース画像。</param>
    /// <param name="plan">描画層が解決した描画計画（層の順序・矩形・拡大画像の幾何を持つ）。</param>
    /// <returns>生成したルーペ画像（書き換え可能。 ホストがピクセルへ書き込める）。</returns>
    /// <exception cref="NotSupportedException"><paramref name="source"/> が <see cref="Bitmap"/>（派生の <see cref="WriteableBitmap"/> / <see cref="RenderTargetBitmap"/> を含む）/ <see cref="EsReadOnlyBitmap"/>（表示アダプタ）以外の場合。</exception>
    /// <remarks>
    /// 1. 未対応型の判定（<see cref="LoupeSourceResolver.IsSourceSupported"/>）を行い、未対応型は例外にする。<br/>
    /// 2. 切り出し元を解決する（切り出し元の寸法と、表示用の射影だけでは足りない場合のコアの画像。 表示アダプタは借用）。<br/>
    /// 3. 描画（切り出し・拡大・十字線・枠線）は <see cref="LoupeContextPainter"/> へ委譲し、UI スレッドで行う<br/>
    /// （切り出しの可否と破棄済みソースの扱いは描画層が単一所有する）。<br/>
    /// <b>UI スレッドの使い方</b>: UI スレッド上ではその場で描き、UI スレッド外からは描画フェーズ<br/>
    /// （<see cref="DispatcherPriority.Render"/>）へポストする。 再描画の通常の経路は UI スレッド上のため往復しない。
    /// </remarks>
    public static async Task<EsBitmap> RenderAsync(IImage? source, LoupeDrawPlan plan)
    {
        // 未対応型の判定は LoupeSourceResolver（単一所有）へ委譲する。
        if (!LoupeSourceResolver.IsSourceSupported(source))
        {
            throw new NotSupportedException(
                $"LoupeControl は {source!.GetType().FullName} をサポートしていません。Bitmap（WriteableBitmap を含む）または EsReadOnlyBitmap（表示アダプタ）を指定してください。");
        }

        // 切り出し元を解決する（寸法と、表示用の射影だけでは足りない場合のコアの画像。 破棄済みは寸法 null）
        var resolved = LoupeSourceResolver.Resolve(source);
        var sourceBitmap = resolved.Bitmap;

        try
        {
            // 描画（画像の切り出し・拡大・十字線・枠線）と RenderTargetBitmap への合成は UI スレッドで行う。
            // 元の IImage はそのまま元矩形つきの描画に使える（切り出しの複製と表示用の変換を作らない）
            // 再描画の通常の経路（描画フェーズのジョブ）は既に UI スレッド上のため、往復を挟まずその場で描く
            // （往復を 1 回増やすごとに、その往復が次の描画フェーズまで待たされ、ルーペ画像の反映が遅れる）
            if (Dispatcher.UIThread.CheckAccess())
            {
                return Rasterize(sourceBitmap, resolved.Size, source, in plan);
            }

            return await Dispatcher.UIThread.InvokeAsync(
                () => Rasterize(sourceBitmap, resolved.Size, source, in plan),
                DispatcherPriority.Render);
        }
        finally
        {
            // 複製を返した場合のみ破棄する（表示アダプタの共有参照は破棄しない）。
            if (resolved.IsSourceBitmap)
            {
                sourceBitmap?.Dispose();
            }
        }
    }

    /// <summary>描画計画を描画実装へ渡し、結果を書き換え可能な画像として返す。</summary>
    /// <param name="source">拡大元のコアの画像（読み取りの面。 表示用の射影をそのまま使えない場合の切り出し元）。</param>
    /// <param name="sourceSize">拡大元の寸法（ピクセル。 切り出し元の座標系。 描けない場合は null）。</param>
    /// <param name="display">拡大元の全体を写した Avalonia の画像（渡すと切り出しの複製を作らずに描く）。</param>
    /// <param name="plan">描画層が解決した描画計画（層の順序・矩形・拡大画像の幾何を持つ）。</param>
    /// <returns>描画結果（ホストがピクセルへオーバーレイを描画できる）。</returns>
    /// <remarks>
    /// オフスクリーン ビットマップは UI 固有の機構（Avalonia の <see cref="RenderTargetBitmap"/>）であるため本層が生成し、<br/>
    /// 描画の手順（層の順序・写し方）は <see cref="LoupeContextPainter"/> へ委譲する。<br/>
    /// 描画面は<b>ピクセルを取り出した後に破棄する</b>（描画ごとに 1 枚作るため、破棄しないとネイティブの描画面が
    /// GC まで残る）。
    /// </remarks>
    private static EsBitmap Rasterize(EsReadOnlyBitmap? source, EsSizeInt? sourceSize, IImage? display, in LoupeDrawPlan plan)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(plan.ClientPixelSize, plan.ClientPixelSize), s_dpi);

        using (var context = bitmap.CreateDrawingContext())
        {
            LoupeContextPainter.Paint(context, in plan, sourceSize, display, source);
        }

        return bitmap.ToEsBitmap();
    }
}
