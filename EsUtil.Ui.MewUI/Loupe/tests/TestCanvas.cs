using Aprillz.MewUI;
using Aprillz.MewUI.Rendering;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.MewUI.Loupe.Tests;

/// <summary>描画 API を使うテストの土台（オフスクリーンへ描いて画素を読む）。</summary>
/// <remarks>
/// <b>なぜ GDI バックエンドか</b>: 画素を読み戻せるのは GDI の CPU 表面だけである（Direct2D と MewVG の
/// オフスクリーンは読み戻せない、または OpenGL の現在のコンテキストを要求する）。<br/>
/// <b>ウィンドウは要らない</b>: 表面と描画コンテキストだけで描けるため、ヘッドレスで検証できる。
/// </remarks>
internal static class TestCanvas
{
    /// <summary>テストの実行前にバックエンドを登録する（テキストの実寸測定もバックエンドを必要とするため）。</summary>
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Initialize() => _ = Factory;

    /// <summary>バックエンドを登録済みかどうか。</summary>
    private static bool s_registered;

    /// <summary>グラフィックス ファクトリを取得する（初回に GDI バックエンドを登録する）。</summary>
    /// <returns>登録済みのファクトリ。</returns>
    public static IGraphicsFactory Factory
    {
        get
        {
            if (!s_registered)
            {
                GdiBackend.Register();
                s_registered = true;
            }

            return Application.DefaultGraphicsFactory;
        }
    }

    /// <summary>指定の大きさの表面を作り、描画してから画素を読める状態で返す。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="draw">描画する処理。</param>
    /// <returns>描画結果。 破棄は呼び出し側。</returns>
    public static RenderedPixels Render(int width, int height, Action<IGraphicsContext> draw)
    {
        ArgumentNullException.ThrowIfNull(draw);

        var factory = Factory;
        var surface = factory.CreateSurface(RenderSurfaceDescriptor.CpuPixels(width, height, 1.0, true, "test"));
        using (var context = factory.CreateContext(surface))
        {
            context.BeginFrame(surface);
            draw(context);
            context.EndFrame();
        }

        var stride = width * 4;
        var pixels = new byte[stride * height];
        var ok = factory.TryReadPixels(surface, pixels, stride);
        return new RenderedPixels(pixels, width, height, stride, ok);
    }
}

/// <summary>描画結果の画素（BGRA8888・読み取り専用）。</summary>
internal sealed class RenderedPixels(byte[] pixels, int width, int height, int stride, bool isValid)
{
    /// <summary>読み戻しに成功したかどうか。</summary>
    public bool IsValid { get; } = isValid;

    /// <summary>幅（ピクセル）。</summary>
    public int Width => width;

    /// <summary>高さ（ピクセル）。</summary>
    public int Height => height;

    /// <summary>描画結果の範囲内かどうか。</summary>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <returns>範囲内の場合は true。</returns>
    public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;

    /// <summary>指定座標の画素色を取得する（アルファは無視する）。</summary>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <returns>画素色。</returns>
    public DrawingColor GetPixel(int x, int y)
    {
        var i = (y * stride) + (x * 4);
        return DrawingColor.FromArgb(255, pixels[i + 2], pixels[i + 1], pixels[i]);
    }

    /// <summary>指定領域に指定色の画素が何個あるかを数える。</summary>
    /// <param name="x0">左上 X 座標。</param>
    /// <param name="y0">左上 Y 座標。</param>
    /// <param name="x1">右下 X 座標（含まない）。</param>
    /// <param name="y1">右下 Y 座標（含まない）。</param>
    /// <param name="color">数える色。</param>
    /// <returns>一致する画素数。</returns>
    public int CountColor(int x0, int y0, int x1, int y1, DrawingColor color)
    {
        var count = 0;
        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                if (GetPixel(x, y) == color)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>指定座標の画素のアルファを取得する。</summary>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <returns>アルファ（0〜255）。</returns>
    public int GetAlpha(int x, int y) => pixels[(y * stride) + (x * 4) + 3];
}
