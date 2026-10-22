
// 複製元: EsUtil.Ui.MewUI.Loupe.Internal（独立パッケージ用の意図的な複製。 .github/依存関係.md §4.1）
namespace EsUtil.Ui.MewUI.Thumbnail.Internal;

/// <summary>Skia の描画面（物理ピクセル）と表示領域（DIP）から、描画に使うスケーリング係数を解決する。</summary>
/// <remarks>
/// <b>なぜ保持値（DPI の通知）だけに頼らないか</b>: DPI の通知は追加直後の要素へ届かないことがある<br/>
/// （実測）。 描画面から求めた倍率を使えば、通知の有無によらず同じ倍率で描ける。
/// </remarks>
internal static class ClientPixelScale
{
    /// <summary>描画面の実サイズと表示領域の比からスケーリング係数を解決する。</summary>
    /// <param name="clientPixels">クライアント領域の一辺の長さ（物理ピクセル）。</param>
    /// <param name="clientDip">同じ辺の長さ（DIP）。 0 以下の場合は <paramref name="fallback"/> を使う。</param>
    /// <param name="fallback">表示領域が未確定のときに使う係数（DPI から与えられる値）。</param>
    /// <returns>スケーリング係数（0.1〜8.0 へ補正済み）。</returns>
    public static double Resolve(double clientPixels, double clientDip, double fallback)
        => clientDip > 0
            ? DpiScaler.NormalizeScaling(clientPixels / clientDip)
            : DpiScaler.NormalizeScaling(fallback);
}
