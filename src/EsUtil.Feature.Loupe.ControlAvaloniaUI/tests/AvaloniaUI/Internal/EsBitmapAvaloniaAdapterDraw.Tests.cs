using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Feature.Loupe.AvaloniaUI.Internal;
using Xunit;

namespace EsUtil.Feature.Loupe.AvaloniaUI.Tests.Internal;

/// <summary>
/// <see cref="EsBitmapAvaloniaAdapter"/> の描画（<see cref="EsBitmapAvaloniaAdapter.Draw"/>）のテスト。
/// </summary>
/// <remarks>
/// <see cref="EsBitmapAvaloniaAdapter.Draw"/> は <see cref="Avalonia.Media.Imaging.Bitmap"/> への射影（変換）を行い、
/// その生成には Avalonia の描画バックエンド（<c>DrawingContext</c>）が必要なため、<c>[AvaloniaFact]</c> で検証する。<br/>
/// <see cref="EsBitmapAvaloniaAdapter"/> のそれ以外（取得・サイズ・破棄）は <see cref="EsBitmapAvaloniaAdapterTests"/> が検証する。<br/>
/// 実 Skia の描画は共有状態を持つため <see cref="AvaloniaSkiaGroup"/> で直列化する。
/// </remarks>
[Collection("AvaloniaSkia")]
public class EsBitmapAvaloniaAdapterDrawTests
{
    /// <summary>真実が破棄されたら描画は no-op になり、例外にもならないことを検証する。</summary>
    /// <remarks>
    /// アダプタは真実を所有しないため、真実がアダプタより先に破棄される経路がある（共有参照の破棄）。
    /// 破棄済みの真実では射影を生成しない（破棄済みピクセルへアクセスしない）。
    /// </remarks>
    [AvaloniaFact]
    internal void Draw_真実が破棄されたらnoOpになり例外にならない()
    {
        using var bmp = EsBitmap.Create(8, 6, System.Drawing.Color.Red);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);
        var adapter = new EsBitmapAvaloniaAdapter(read);

        // 真実を先に破棄する（アダプタは非所有のため、所有者が先に破棄する経路がある）
        read.Dispose();

        // 破棄済みの真実では描画しない（例外にもしない。 射影の生成も行わない）
        using var target = new RenderTargetBitmap(new PixelSize(8, 6), new Vector(96, 96));
        using (var context = target.CreateDrawingContext())
        {
            adapter.Draw(context, new Rect(0, 0, 8, 6), new Rect(0, 0, 8, 6));
        }

        // 冪等（二重破棄しない）
        adapter.Dispose();
        adapter.Dispose();
    }

    /// <summary>繰り返しの描画で例外なく描画できることを検証する（射影の再利用経路）。</summary>
    /// <remarks>
    /// 射影（<see cref="Bitmap"/> への変換）は初回の 1 回だけ行い、2 回目以降は再利用する。<br/>
    /// 再利用の正しさを、連続描画が例外にならないことで確認する。
    /// </remarks>
    [AvaloniaFact]
    internal void Draw_繰り返し描画しても例外にならない()
    {
        using var bmp = EsBitmap.Create(8, 6, System.Drawing.Color.Blue);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);
        using var adapter = new EsBitmapAvaloniaAdapter(read);

        using var target = new RenderTargetBitmap(new PixelSize(8, 6), new Vector(96, 96));
        for (var i = 0; i < 3; i++)
        {
            using var context = target.CreateDrawingContext();
            adapter.Draw(context, new Rect(0, 0, 8, 6), new Rect(0, 0, 8, 6));
        }
    }
}
