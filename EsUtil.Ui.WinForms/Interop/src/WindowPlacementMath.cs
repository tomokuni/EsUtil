using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.WinForms.Interop;

/// <summary>ウィンドウの位置・サイズの相互変換（DIP ⇔ 画面物理ピクセル）を提供する純粋クラス。</summary>
/// <remarks>
/// <para>
/// WinForms のウィンドウ位置・サイズは<b>画面物理ピクセル</b>で扱われるが、レイアウトの計算（コントロールの寸法・余白）は DIP で扱う。<br/>
/// 設定（アプリの <c>Settings.MainWindowBounds</c>）は UI 非依存の画面物理ピクセル（<see cref="EsRectInt"/>）で保持する。<br/>
/// 本クラスがその変換を単一所有し、往復の誤差と丸めの規則を 1 箇所に閉じる。
/// </para>
/// <para>
/// 純粋関数のみのため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </para>
/// </remarks>
public static class WindowPlacementMath
{
    /// <summary>DIP の座標を画面物理ピクセルの座標へ変換する。</summary>
    /// <param name="dip">DIP の座標。</param>
    /// <param name="scale">DPI スケール係数（1.0 = 96 DPI）。 0 以下または非数の場合は基準値として扱う。</param>
    /// <returns>画面物理ピクセルの座標（四捨五入）。</returns>
    /// <remarks>DPI スケールは 1 未満（縮小表示）もあり得るため、0 以下だけを無効として扱う。</remarks>
    public static EsPointInt ToPhysicalPixelPoint(EsPoint dip, double scale)
    {
        var factor = NormalizeScale(scale);
        return new EsPointInt(
            (int)Math.Round(dip.X * factor),
            (int)Math.Round(dip.Y * factor));
    }

    /// <summary>DIP のサイズを画面物理ピクセルのサイズへ変換する。</summary>
    /// <param name="dip">DIP のサイズ。</param>
    /// <param name="scale">DPI スケール係数（1.0 = 96 DPI）。</param>
    /// <returns>画面物理ピクセルのサイズ（四捨五入・最小 1）。</returns>
    /// <remarks>
    /// ウィンドウの幅・高さが 0 になると復元できないため、最小 1 にクランプする。<br/>
    /// 戻り値は設定が使う整数型（<c>System.Drawing.Size</c>）とする（<see cref="EsRectInt"/> へそのまま渡せる）。
    /// </remarks>
    public static Size ToPhysicalPixelSize(EsSize dip, double scale)
    {
        var factor = NormalizeScale(scale);
        return new Size(
            Math.Max(1, (int)Math.Round(dip.Width * factor)),
            Math.Max(1, (int)Math.Round(dip.Height * factor)));
    }

    /// <summary>画面物理ピクセルの座標を DIP の座標へ変換する。</summary>
    /// <param name="physical">画面物理ピクセルの座標。</param>
    /// <param name="scale">DPI スケール係数（1.0 = 96 DPI）。</param>
    /// <returns>DIP の座標。</returns>
    /// <remarks>成分ごとの換算は <see cref="DpiScaler.ToDip(int, double)"/>（UI 共通の単一所有者）へ委譲する。</remarks>
    public static EsPoint ToDipPoint(EsPointInt physical, double scale)
        => new(DpiScaler.ToDip(physical.X, scale), DpiScaler.ToDip(physical.Y, scale));

    /// <summary>画面物理ピクセルの長さを DIP の長さへ変換する。</summary>
    /// <param name="physical">画面物理ピクセルの長さ。</param>
    /// <param name="scale">DPI スケール係数（1.0 = 96 DPI）。</param>
    /// <returns>DIP の長さ。</returns>
    /// <remarks>換算は <see cref="DpiScaler.ToDip(int, double)"/>（UI 共通の単一所有者）へ委譲する。</remarks>
    public static double ToDipLength(int physical, double scale)
        => DpiScaler.ToDip(physical, scale);

    /// <summary>物理ピクセルの矩形が復元に使えるかどうかを判定する。</summary>
    /// <param name="bounds">判定する矩形。 null 可。</param>
    /// <returns>幅と高さがともに 1 以上なら true。</returns>
    /// <remarks>
    /// 幅・高さが 0 以下の値（壊れた設定ファイル・手編集の誤り）では復元しない<br/>
    /// （0 サイズのウィンドウが作られて操作不能になるのを防ぐ）。
    /// </remarks>
    public static bool IsUsable(EsRectInt? bounds)
        => bounds is { Width: > 0, Height: > 0 };

    /// <summary>DPI スケール係数を有効値へ正規化する。</summary>
    /// <param name="scale">DPI スケール係数。</param>
    /// <returns>正の値の場合はその値、0 以下または非数の場合は 1.0。</returns>
    /// <remarks>
    /// スケールを取得できない場合（ウィンドウ未生成・API 失敗）に座標が 0 へ潰れるのを防ぐ。<br/>
    /// 判定と基準値は UI 共通の <see cref="DpiScaler.NormalizeScaling"/> が単一所有する（本クラスは委譲する）。
    /// </remarks>
    private static double NormalizeScale(double scale) => DpiScaler.NormalizeScaling(scale);
}
