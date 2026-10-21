using System.Runtime.Versioning;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Platform.Core.Coord;

/// <summary>ウィンドウの位置・サイズの相互変換（DIP ⇔ 画面物理ピクセル）を提供する純粋計算。</summary>
/// <remarks>
/// <b>なぜ本層に置くか</b>: ウィンドウの位置・サイズは UI ごとに扱う単位が違う（WinForms は画面物理ピクセル、
/// MewUI / WinUI 3 は DIP）が、<b>設定は UI 非依存の画面物理ピクセル</b>（<see cref="EsRectInt"/>）で保持する。
/// 変換を UI ごとに複製すると往復の誤差と丸めの規則が食い違うため、最下層が単一所有する。<br/>
/// <b>純粋関数のみ</b>: UI を初期化せずに検証できる（<see cref="GetVirtualScreenBounds"/> だけが OS を呼ぶ）。
/// </remarks>
public static class EsWindowPlacementMath
{
    /// <summary>DIP の座標を画面物理ピクセルの座標へ変換する。</summary>
    /// <param name="dip">DIP の座標。</param>
    /// <param name="scale">DPI スケール係数（1.0 = 96 DPI）。 0 以下または非数は基準値として扱う。</param>
    /// <returns>画面物理ピクセルの座標（四捨五入）。</returns>
    /// <remarks>DPI スケールは 1 未満（縮小表示）もあり得るため、0 以下だけを無効として扱う。</remarks>
    public static EsPointInt ToPhysicalPixelPoint(EsPoint dip, double scale)
    {
        var factor = DpiScaler.NormalizeScaling(scale);
        return new EsPointInt(
            (int)Math.Round(dip.X * factor),
            (int)Math.Round(dip.Y * factor));
    }

    /// <summary>DIP のサイズを画面物理ピクセルのサイズへ変換する。</summary>
    /// <param name="dip">DIP のサイズ。</param>
    /// <param name="scale">DPI スケール係数（1.0 = 96 DPI）。</param>
    /// <returns>画面物理ピクセルのサイズ（四捨五入・最小 1）。</returns>
    /// <remarks>ウィンドウの幅・高さが 0 になると復元できないため、最小 1 にクランプする。</remarks>
    public static EsSizeInt ToPhysicalPixelSize(EsSize dip, double scale)
    {
        var factor = DpiScaler.NormalizeScaling(scale);
        return new EsSizeInt(
            Math.Max(1, (int)Math.Round(dip.Width * factor)),
            Math.Max(1, (int)Math.Round(dip.Height * factor)));
    }

    /// <summary>画面物理ピクセルの座標を DIP の座標へ変換する。</summary>
    /// <param name="physical">画面物理ピクセルの座標。</param>
    /// <param name="scale">DPI スケール係数（1.0 = 96 DPI）。</param>
    /// <returns>DIP の座標。</returns>
    /// <remarks>成分ごとの換算は <see cref="DpiScaler.ToDip(int, double)"/> へ委譲する（丸めの規則を 1 箇所に閉じる）。</remarks>
    public static EsPoint ToDipPoint(EsPointInt physical, double scale)
        => new(DpiScaler.ToDip(physical.X, scale), DpiScaler.ToDip(physical.Y, scale));

    /// <summary>画面物理ピクセルの長さを DIP の長さへ変換する。</summary>
    /// <param name="physical">画面物理ピクセルの長さ。</param>
    /// <param name="scale">DPI スケール係数（1.0 = 96 DPI）。</param>
    /// <returns>DIP の長さ。</returns>
    /// <remarks>換算は <see cref="DpiScaler.ToDip(int, double)"/> へ委譲する。</remarks>
    public static double ToDipLength(int physical, double scale) => DpiScaler.ToDip(physical, scale);

    /// <summary>物理ピクセルの矩形が復元に使えるかどうかを判定する。</summary>
    /// <param name="bounds">判定する矩形。 null 可。</param>
    /// <returns>幅と高さがともに 1 以上なら true。</returns>
    /// <remarks>
    /// 幅・高さが 0 以下の値（壊れた設定ファイル・手編集の誤り）では復元しない
    /// （0 サイズのウィンドウが作られて操作不能になるのを防ぐ）。
    /// </remarks>
    public static bool IsUsable(EsRectInt? bounds) => bounds is { Width: > 0, Height: > 0 };

    /// <summary>全モニターを包含する仮想スクリーンの矩形（画面物理ピクセル）を取得する。</summary>
    /// <returns>仮想スクリーンの矩形。 取得できない場合は空の矩形。</returns>
    /// <remarks>
    /// <b><c>DisplayArea.FindAll()</c> を使わない理由</b>: ウィンドウの表示前に列挙すると、WinRT の射影で
    /// <c>InvalidCastException</c> になり起動できない（実測 2026-10-17）。 <c>GetSystemMetrics</c> は
    /// ウィンドウに依存せず、画面キャプチャと同じ値の出所になる。<br/>
    /// <b>プライマリ モニタが (0,0) にない場合は負値</b>になる（仮想スクリーンの座標そのものである）。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public static EsRectInt GetVirtualScreenBounds()
    {
        var (x, y, width, height) = Native.Win.WinNative.GetVirtualScreenBounds();
        return width <= 0 || height <= 0
            ? new EsRectInt(0, 0, 0, 0)
            : new EsRectInt(x, y, width, height);
    }
}
