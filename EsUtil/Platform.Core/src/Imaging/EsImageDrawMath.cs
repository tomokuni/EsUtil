
namespace EsUtil.Platform.Core.Imaging;

/// <summary>画像を描画先の領域へ配置する矩形計算（拡大・縮小の倍率を 1 箇所で決める）。</summary>
/// <remarks>
/// <b>UI 非依存</b>: 戻り値は <see cref="EsRect"/> であり、各 UI 版が自 UI の矩形型へ変換する。
/// 画像を描く処理は UI ごとに異なるため、本クラスは矩形の計算だけを担う。<br/>
/// <b>配置方法</b>: <see cref="EsImageDrawMode.Uniform"/>（縦横比を保って中央）/
/// <see cref="EsImageDrawMode.Fill"/>（領域全体へ引き伸ばし）。<br/>
/// <b>空の入力</b>: 元画像・描画先のいずれかが 0 以下なら空の矩形を返す（描画しない）。<br/>
/// <b>純粋関数</b>: UI ホストを必要としないため、倍率・中央寄せ・空の扱いをヘッドレスで検証できる。
/// </remarks>
public static class EsImageDrawMath
{
    /// <summary>元画像を描画先の領域へ配置する矩形を計算する。</summary>
    /// <param name="sourceWidth">元画像の幅（ピクセル）。</param>
    /// <param name="sourceHeight">元画像の高さ（ピクセル）。</param>
    /// <param name="targetWidth">描画先の幅。</param>
    /// <param name="targetHeight">描画先の高さ。</param>
    /// <param name="mode">配置方法（縦横比を保つ / 領域全体へ引き伸ばす）。</param>
    /// <returns>描画先の矩形。 元画像または描画先が空の場合は空の矩形（<see cref="EsRect.IsEmpty"/> が真）。</returns>
    /// <remarks>
    /// 1. 元画像・描画先のいずれかが 0 以下なら空の矩形を返す。<br/>
    /// 2. <see cref="EsImageDrawMode.Fill"/> は描画先の全域（左上を原点）を返す。<br/>
    /// 3. <see cref="EsImageDrawMode.Uniform"/> は幅・高さそれぞれの倍率の小さい方を採用し、中央へ寄せる。
    /// </remarks>
    public static EsRect ComputeDestination(
        int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, EsImageDrawMode mode)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || targetWidth <= 0 || targetHeight <= 0)
        {
            return default;
        }

        if (mode == EsImageDrawMode.Fill)
        {
            return new EsRect(0, 0, targetWidth, targetHeight);
        }

        // 縦横比を保つため、幅・高さそれぞれの倍率の小さい方（Uniform）を採用する
        var scale = Math.Min((double)targetWidth / sourceWidth, (double)targetHeight / sourceHeight);
        var width = sourceWidth * scale;
        var height = sourceHeight * scale;

        return new EsRect((targetWidth - width) / 2, (targetHeight - height) / 2, width, height);
    }
}
