using EsUtil.Platform.Core.Coord;

namespace EsUtil.Ui.WinUI3.Interop.Imaging;

/// <summary>物理ピクセルと表示領域（DIP）から、描画に使うスケーリング係数を解決する。</summary>
/// <remarks>
/// <b>位置付け</b>: 描画面（Composition のサーフェス）は要素の表示サイズ（DIP）へ表示倍率を掛けた大きさになるため、<br/>
/// 両者の比がその時点で実際に使うべき倍率になる。<br/>
/// <br/>
/// <b>なぜ保持値（DPI の通知）だけに頼らないか</b>: 描画の前に <c>XamlRoot</c> が確定していない要素<br/>
/// （追加直後の要素・初期化前の要素）ではスケーリングが既定値（1.0）のままになり、<br/>
/// その状態で描画すると図形が 100% で描かれて DPI を反映済みの画像とずれる。<br/>
/// 描画面から求めた倍率を使えば、通知の有無によらず画像と同じ倍率で描ける。
/// </remarks>
public static class ClientPixelScale
{
    /// <summary>描画面の実サイズと表示領域の比からスケーリング係数を解決する。</summary>
    /// <param name="clientPixels">クライアント領域の一辺の長さ（物理ピクセル）。</param>
    /// <param name="clientDip">同じ辺の長さ（DIP）。 0 以下の場合は <paramref name="fallback"/> を使う。</param>
    /// <param name="fallback">表示領域が未確定のときに使う係数（DPI から与えられる値）。</param>
    /// <returns>スケーリング係数（0.1〜8.0 へ補正済み）。</returns>
    /// <remarks>
    /// 表示領域が確定していれば常に実測値（<paramref name="clientPixels"/> ÷ <paramref name="clientDip"/>）を返す。<br/>
    /// 補正（非数・範囲外は基準値）は <see cref="DpiScaler.NormalizeScaling"/> が単一所有する。
    /// </remarks>
    public static double Resolve(double clientPixels, double clientDip, double fallback)
        => clientDip > 0
            ? DpiScaler.NormalizeScaling(clientPixels / clientDip)
            : DpiScaler.NormalizeScaling(fallback);
}
