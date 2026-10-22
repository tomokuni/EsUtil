using System.Drawing.Imaging;
using EsUtil.Platform.Core.Imaging;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinForms.Interop.Tests;

/// <summary>テストで共用するユーティリティ。</summary>
internal static class TestHelper
{
    /// <summary>GDI+ のビットマップで「未描画」を表す画素色（透明。 成分はすべて 0）。</summary>
    /// <remarks>
    /// <see cref="DrawingColor.Transparent"/> は <c>ARGB(0, 255, 255, 255)</c> であり、<br/>
    /// 透明で消した画素（<c>ARGB(0, 0, 0, 0)</c>）と一致しないため、比較用の値を本クラスが単一所有する。
    /// </remarks>
    public static DrawingColor EmptyPixel => DrawingColor.FromArgb(0, 0, 0, 0);

    /// <summary>指定サイズの単色画像を生成する。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="color">塗りつぶす色。</param>
    /// <returns>生成した画像（読み取りの面）。 破棄は呼び出し側。</returns>
    /// <remarks>
    /// 生成した画像は書き込まず読むだけ（表示・描画の入力）であるため、凍結して 0 コピーで
    /// 読み取り専用の面へ渡す。
    /// </remarks>
    public static EsReadOnlyBitmap CreateBitmap(int width, int height, DrawingColor color)
    {
        using var created = EsBitmap.Create(width, height, color);
        created.SetReadOnly();
        return EsReadOnlyBitmap.FromBitmap(created);
    }

    /// <summary>指定サイズの画像を、画素を書き込んでから生成する。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="draw">画素を書き込む処理（書き込み可能な画像を受け取る）。</param>
    /// <returns>生成した画像（読み取りの面）。 破棄は呼び出し側。</returns>
    /// <remarks>行の並び（Stride）や色成分の順序を検証するため、隅ごとに異なる色を置ける入口を提供する。</remarks>
    public static EsReadOnlyBitmap CreateBitmap(int width, int height, Action<EsBitmap> draw)
    {
        using var created = EsBitmap.Create(width, height);
        draw(created);
        created.SetReadOnly();
        return EsReadOnlyBitmap.FromBitmap(created);
    }

    /// <summary>描画先（描画面とその画素を読む手段）を生成する。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成した描画先。 破棄は呼び出し側。</returns>
    public static RenderTarget CreateTarget(int width, int height) => new(width, height);
}

/// <summary>テスト用の描画先（GDI+ の描画面と画素読み取り）。</summary>
/// <remarks>
/// ウィンドウを必要としないため UI 初期化なしで検証できる。<br/>
/// 初期状態は透明（コントロールの下地を塗らない前提と同じ）。<br/>
/// 内部は乗算済みアルファで保持されるが、<c>Bitmap.GetPixel</c> は非乗算の色を返す。
/// </remarks>
internal sealed class RenderTarget : IDisposable
{
    /// <summary>実際の画素を保持するビットマップ。</summary>
    private readonly Bitmap _bitmap;

    /// <summary>描画に使用する描画面。</summary>
    private readonly Graphics _graphics;

    /// <summary>指定サイズの描画先を初期化する（透明で初期化する）。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    public RenderTarget(int width, int height)
    {
        _bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        _graphics = Graphics.FromImage(_bitmap);
        _graphics.Clear(DrawingColor.Transparent);
    }

    /// <summary>描画に使用する描画面。</summary>
    public Graphics Graphics => _graphics;

    /// <summary>描画先のサイズ。</summary>
    public Size Size => new(_bitmap.Width, _bitmap.Height);

    /// <summary>指定座標の画素色を取得する。</summary>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <returns>画素色（非乗算）。</returns>
    public DrawingColor GetPixel(int x, int y) => _bitmap.GetPixel(x, y);

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

    /// <summary>内部の資源を破棄する。</summary>
    public void Dispose()
    {
        _graphics.Dispose();
        _bitmap.Dispose();
    }
}
