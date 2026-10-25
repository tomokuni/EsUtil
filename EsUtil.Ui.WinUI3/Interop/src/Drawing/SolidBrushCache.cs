using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace EsUtil.Ui.WinUI3.Interop.Drawing;

/// <summary>ARGB ごとに <see cref="SolidColorBrush"/> を使い回すキャッシュ。</summary>
/// <remarks>
/// <b>なぜ必要か</b>: 図形の層は毎フレーム数十個の矩形へ色を設定する。 反映のたびに
/// <c>new SolidColorBrush(...)</c> で作ると WinRT オブジェクトの割り当てと解放がフレーム数だけ積み上がり、
/// ドラッグ中の描画が目に見えて重くなる（実測 2026-10-25: マーキーを動かすと描画が追随しない）。<br/>
/// 色は有限（枠・ハンドル・暗転・十字線・ラベル）なので、ARGB をキーに 1 つだけ保持すれば足りる。<br/>
/// <br/>
/// <b>UI スレッド専用</b>: <see cref="SolidColorBrush"/> は XAML のオブジェクトであり、UI スレッド以外から
/// 触れない。 本キャッシュも UI スレッドからのみ使う（ロックを持たない）。
/// </remarks>
public static class SolidBrushCache
{
    /// <summary>ARGB ごとのブラシ（UI スレッド専用）。</summary>
    private static readonly Dictionary<uint, SolidColorBrush> s_cache = [];

    /// <summary>指定の色のブラシを取得する（同じ色は同じインスタンス）。</summary>
    /// <param name="argb">色（<c>0xAARRGGBB</c>）。</param>
    /// <returns>ブラシ。</returns>
    /// <remarks>キャッシュしたブラシは破棄しない（色は有限であり、アプリの寿命と同じでよい）。</remarks>
    public static SolidColorBrush Get(uint argb)
    {
        if (s_cache.TryGetValue(argb, out var cached))
        {
            return cached;
        }

        var brush = new SolidColorBrush(ToColor(argb));
        s_cache[argb] = brush;
        return brush;
    }

    /// <summary>指定の色のブラシを取得する（同じ色は同じインスタンス）。</summary>
    /// <param name="color">色（不透明度はアルファ）。</param>
    /// <returns>ブラシ。</returns>
    public static SolidColorBrush Get(System.Drawing.Color color)
        => Get(((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B);

    /// <summary><c>0xAARRGGBB</c> を WinUI の色へ変換する。</summary>
    /// <param name="argb">色（<c>0xAARRGGBB</c>）。</param>
    /// <returns>WinUI の色。</returns>
    private static Color ToColor(uint argb) => Color.FromArgb(
        (byte)((argb >> 24) & 0xFF), (byte)((argb >> 16) & 0xFF), (byte)((argb >> 8) & 0xFF), (byte)(argb & 0xFF));
}
