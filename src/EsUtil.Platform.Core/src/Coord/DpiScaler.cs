using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Platform.Core.Coord;

/// <summary>DIP 値をスケーリング係数で物理ピクセルへ変換する計算ロジック（純粋関数）。</summary>
/// <remarks>
/// DIP 値をスケーリング係数（表示 DIP → 物理ピクセル）で変換する（切り上げ・最低 1 に正規化）。<br/>
/// <c>表示サイズ・市松模様のサイズ・外枠の太さ</c>の物理ピクセル換算と、ウィンドウの位置・サイズの換算で共用する。<br/>
/// UI スレッドに依存しないため <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class DpiScaler
{
    /// <summary>DIP（デバイス非依存ピクセル）の基準 DPI（1 ピクセル = 1 DIP となる DPI）。</summary>
    /// <remarks>
    /// スケーリング係数（<see cref="ToScaling"/>）と、DIP を 1:1 で描く描画先（<c>RenderTargetBitmap</c> 等）の DPI が共用する。<br/>
    /// 各 UI が <c>96</c> を直書きすると、同じ「DIP 基準」の規則が複数箇所へ散る（片方だけ変更される）。
    /// </remarks>
    public const double StandardDpi = 96.0;

    /// <summary>スケーリング係数の基準値（DIP と物理ピクセルが 1:1 となる係数）。</summary>
    /// <remarks>描画先のスケーリングが未確定（0 以下）のときに使う値である（<see cref="NormalizeScaling"/>）。</remarks>
    public const double StandardScaling = 1.0;

    /// <summary>スケーリング係数が有効（描画・変換に使える）かどうかを判定する。</summary>
    /// <param name="scaling">スケーリング係数（表示 DIP → 物理ピクセル）。</param>
    /// <returns>有限の正の値の場合は true。</returns>
    /// <remarks>
    /// 「0 以下は未確定」というしきい値の規則を単一所有する（<see cref="NormalizeScaling"/> と、<br/>
    /// 有効なモニターだけを集める呼び出し側が同じ判定を使う）。<br/>
    /// <b>非数（NaN・無限大）も無効</b>とする（乗算すると座標が整数の上限へ溢れてウィンドウが壊れるため、<br/>
    /// 取得に失敗した値として基準値へ寄せる）。
    /// </remarks>
    public static bool IsValidScaling(double scaling) => double.IsFinite(scaling) && scaling > 0;

    /// <summary>スケーリング係数を有効値へ正規化する（無効な値は <see cref="StandardScaling"/> とする）。</summary>
    /// <param name="scaling">スケーリング係数（表示 DIP → 物理ピクセル）。</param>
    /// <returns>正規化したスケーリング係数（常に有限の正の値）。</returns>
    /// <remarks>描画先の係数が未確定（無効な値）のまま使用すると描画が消えるため、有効値へ寄せる規則を単一所有する。</remarks>
    public static double NormalizeScaling(double scaling) => IsValidScaling(scaling) ? scaling : StandardScaling;

    /// <summary>DPI をスケーリング係数（表示 DIP → 物理ピクセル）へ換算する。</summary>
    /// <param name="dpi">DPI（画面・モニターの実効 DPI）。</param>
    /// <returns>スケーリング係数（<see cref="StandardDpi"/> を 1.0 とする倍率）。</returns>
    /// <remarks>DPI の変化（モニター間の移動）を <see cref="ScaleToPixel(int, double)"/> へ渡す倍率へ変換する規則を単一所有する。</remarks>
    public static double ToScaling(double dpi) => dpi / StandardDpi;

    /// <summary>DIP 値をスケーリング係数で物理ピクセルへ変換する（切り上げ・最低 1）。</summary>
    /// <param name="dipValue">変換する DIP 値。</param>
    /// <param name="scaling">スケーリング係数（表示 DIP → 物理ピクセル）。</param>
    /// <returns>物理ピクセル値（切り上げ・最低 1）。</returns>
    /// <remarks>浮動小数の誤差（例: <c>100 × 1.1 = 110.00000000000001</c>）を吸収するため、整数に極めて近い値は切り上げない。</remarks>
    public static int ScaleToPixel(int dipValue, double scaling)
        => ScaleToPixel((double)dipValue, scaling);

    /// <summary>DIP 値（浮動小数）をスケーリング係数で物理ピクセルへ変換する（切り上げ・最低 1）。</summary>
    /// <param name="dipValue">変換する DIP 値（浮動小数）。</param>
    /// <param name="scaling">スケーリング係数（表示 DIP → 物理ピクセル）。</param>
    /// <returns>物理ピクセル値（切り上げ・最低 1）。</returns>
    /// <remarks>浮動小数の誤差（例: <c>100 × 1.1 = 110.00000000000001</c>）を吸収するため、整数に極めて近い値は切り上げない。</remarks>
    public static int ScaleToPixel(double dipValue, double scaling)
    {
        var scaled = dipValue * scaling;
        // 浮動小数誤差（1e-9 未満）を吸収する（100 × 1.1 が 110.00000000000001 になる等の誤差で誤切り上げしない）
        var rounded = Math.Round(scaled);
        if (Math.Abs(scaled - rounded) < 1e-9)
        {
            return Math.Max(1, (int)rounded);
        }

        return Math.Max(1, (int)Math.Ceiling(scaled));
    }
    /// <summary>物理ピクセルの長さを DIP へ変換する（<see cref="ScaleToPixel(int, double)"/> の逆変換）。</summary>
    /// <param name="physical">変換する物理ピクセルの長さ。</param>
    /// <param name="scaling">スケーリング係数（表示 DIP → 物理ピクセル）。 0 以下は基準値へ正規化する。</param>
    /// <returns>DIP の長さ。</returns>
    /// <remarks>
    /// ウィンドウの位置・サイズを物理ピクセルから DIP へ戻す場面（全モニタの包含矩形をウィンドウへ適用する等）で使う。<br/>
    /// <b>切り上げ・最低 1 の正規化は行わない</b>（座標の換算では値をそのまま保つ。 DIP 値へ変換する方向では
    /// 最低値の規則を持ち込むと、ウィンドウの幅が 1 以上に化けてレイアウトが崩れる）。<br/>
    /// 係数の正規化（0 以下は基準値）は <see cref="NormalizeScaling"/> と単一所有であり、変換の対称性を保つ。
    /// </remarks>
    public static double ToDip(int physical, double scaling) => ToDip((double)physical, scaling);

    /// <summary>物理ピクセルの長さを DIP へ変換する（<see cref="ScaleToPixel(double, double)"/> の逆変換）。</summary>
    /// <param name="physical">変換する物理ピクセルの長さ（浮動小数）。</param>
    /// <param name="scaling">スケーリング係数（表示 DIP → 物理ピクセル）。 0 以下は基準値へ正規化する。</param>
    /// <returns>DIP の長さ。</returns>
    /// <remarks>丸めを行わない（切り上げ・最低 1 の規則は物理ピクセルへ変換する方向のものであり、逆方向には適用しない）。</remarks>
    public static double ToDip(double physical, double scaling) => physical / NormalizeScaling(scaling);

    /// <summary>物理ピクセルのサイズを DIP のサイズへ変換する（<see cref="ScaleToPixel(int, double)"/> の逆変換）。</summary>
    /// <param name="physicalSize">変換する物理ピクセルのサイズ。</param>
    /// <param name="scaling">スケーリング係数（表示 DIP → 物理ピクセル）。 0 以下は基準値へ正規化する。</param>
    /// <returns>DIP のサイズ。</returns>
    /// <remarks>
    /// 成分ごとの換算は <see cref="ToDip(double, double)"/>（単一所有者）へ委譲する（両 UI で同じ丸め規則になる）。<br/>
    /// コア型同士の換算を UI 側で組み立てないよう、サイズ型の入口を本クラスが提供する。
    /// </remarks>
    public static EsSize ToDip(EsSize physicalSize, double scaling)
        => new(ToDip(physicalSize.Width, scaling), ToDip(physicalSize.Height, scaling));
}
