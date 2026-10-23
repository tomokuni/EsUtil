using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Platform.Core.Window;

/// <summary>ウィンドウの位置・サイズの規則と、DIP ⇔ 画面物理ピクセルの換算（純粋計算）。</summary>
/// <remarks>
/// <b>なぜ共通基盤に置くか</b>: ウィンドウの位置・サイズは UI ごとに扱う単位が違う（WinForms は画面物理ピクセル、
/// MewUI / WinUI 3 / AvaloniaUI は DIP）が、<b>設定は UI 非依存の画面物理ピクセル</b>（<see cref="EsRectInt"/>）で保持する。
/// 変換と復元規則を UI ごとに複製すると往復の誤差・丸めの規則・復元の可否が食い違うため、最下層が単一所有する。<br/>
/// <b>純粋関数のみ</b>: OS を呼ばない（仮想スクリーンの照会は
/// <see cref="Native.Win.WinVirtualScreen"/> が担う）。<br/>
/// <b>復元の 2 条件</b>: 保存された配置は「幅・高さが 1 以上」と「表示領域と <see cref="MinimumVisibleSize"/> 以上重なる」を
/// 満たさないと復元しない。<b>画面外の座標が保存される経路</b>は 2 つある（撮影中にオーナー ウィンドウを画面外
/// （<c>-32000</c>）へ退避したまま終了する / モニター構成を変える）。 どちらも「画面外のまま起動して操作できない」
/// ため、復元をあきらめて既定の配置で表示する。<br/>
/// <b>最小化は復元しない</b>: 終了時に最小化していた場合、次回起動も最小化で始まるとウィンドウが表示されず
/// 「起動していない」ように見える（タスクバーからの復帰だけが手掛かりになる）。
/// </remarks>
public static class EsWindowPlacementMath
{
    /// <summary>復元を許すために表示領域と重なっていなければならない最小の幅・高さ（画面物理ピクセル）。</summary>
    /// <remarks>
    /// 1 ピクセルでも見えていれば復元可とすると、ほぼ画面外の状態を許してしまうため、
    /// タイトルバーを掴める程度（64 ピクセル）を下限にする。
    /// </remarks>
    public const int MinimumVisibleSize = 64;

    // ====================================================================
    // 換算（DIP ⇔ 画面物理ピクセル）
    // ====================================================================

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
    /// <remarks>ウィンドウの幅・高さが 0 になると復元できないため、最小 1 にクランプする（規則は <see cref="ToPhysicalLength"/>）。</remarks>
    public static EsSizeInt ToPhysicalPixelSize(EsSize dip, double scale)
        => new(ToPhysicalLength(dip.Width, scale), ToPhysicalLength(dip.Height, scale));

    /// <summary>DIP の長さを画面物理ピクセルの長さへ変換する（四捨五入・最小 1）。</summary>
    /// <param name="dip">DIP の長さ。</param>
    /// <param name="scale">DPI スケール係数（1.0 = 96 DPI）。</param>
    /// <returns>画面物理ピクセルの長さ（四捨五入・最小 1）。</returns>
    /// <remarks>
    /// WinUI 3 の <c>OverlappedPresenter.PreferredMinimumWidth</c> と WinForms の <c>Form.MinimumSize</c> は<br/>
    /// 画面物理ピクセルで指定するため、他 UI 版（DIP 指定）と同じ見た目の最小サイズにするには換算が要る。
    /// </remarks>
    public static int ToPhysicalLength(double dip, double scale)
        => Math.Max(1, (int)Math.Round(dip * DpiScaler.NormalizeScaling(scale)));

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

    /// <summary>物理ピクセルのウィンドウ枠を差し引き、外寸 DIP からクライアント DIP を求める。</summary>
    /// <param name="outerDip">枠を含む外寸（DIP）。</param>
    /// <param name="framePixels">枠の合計幅または高さ（画面物理ピクセル）。</param>
    /// <param name="scale">DPI スケール係数。</param>
    /// <returns>クライアント領域の長さ（DIP）。</returns>
    public static double ToClientLength(double outerDip, int framePixels, double scale)
        => Math.Max(0, outerDip - (Math.Max(0, framePixels) / DpiScaler.NormalizeScaling(scale)));

    /// <summary>物理ピクセルのウィンドウ枠を差し引き、外寸ピクセルからクライアント DIP を求める。</summary>
    /// <param name="outerPixels">枠を含む外寸（画面物理ピクセル）。</param>
    /// <param name="framePixels">枠の合計幅または高さ（画面物理ピクセル）。</param>
    /// <param name="scale">DPI スケール係数。</param>
    /// <returns>クライアント領域の長さ（DIP）。</returns>
    public static double ToClientLength(int outerPixels, int framePixels, double scale)
        => Math.Max(0, (outerPixels - Math.Max(0, framePixels)) / DpiScaler.NormalizeScaling(scale));

    /// <summary>クライアント DIP と物理ピクセルのウィンドウ枠から外寸ピクセルを求める。</summary>
    /// <param name="clientDip">クライアント領域の長さ（DIP）。</param>
    /// <param name="framePixels">枠の合計幅または高さ（画面物理ピクセル）。</param>
    /// <param name="scale">DPI スケール係数。</param>
    /// <returns>ウィンドウ外寸の長さ（画面物理ピクセル）。</returns>
    public static int ToOuterPhysicalLength(double clientDip, int framePixels, double scale)
        => ToPhysicalLength(clientDip, scale) + Math.Max(0, framePixels);

    // ====================================================================
    // 復元の規則
    // ====================================================================

    /// <summary>保存された配置が寸法として使えるかどうかを判定する。</summary>
    /// <param name="bounds">判定する矩形。 null 可。</param>
    /// <returns>幅と高さがともに 1 以上なら true。</returns>
    /// <remarks>
    /// 幅・高さが 0 以下の値（壊れた設定ファイル・手編集の誤り）では復元しない
    /// （0 サイズのウィンドウが作られて操作不能になるのを防ぐ）。
    /// </remarks>
    public static bool IsUsable(EsRectInt? bounds) => bounds is { Width: > 0, Height: > 0 };

    /// <summary>保存された配置を復元してよいかどうかを判定する。</summary>
    /// <param name="bounds">保存された配置（画面物理ピクセル）。 null 可。</param>
    /// <param name="visibleArea">画面の表示領域（全モニターを包含する矩形。 画面物理ピクセル）。</param>
    /// <returns>幅・高さが 1 以上で、表示領域と <see cref="MinimumVisibleSize"/> 以上重なる場合は true。</returns>
    public static bool CanRestore(EsRectInt? bounds, EsRectInt visibleArea)
    {
        if (bounds is not { } value || !IsUsable(value))
        {
            return false;
        }

        var overlap = value.Intersect(visibleArea);
        return overlap.Width >= MinimumVisibleSize && overlap.Height >= MinimumVisibleSize;
    }

    /// <summary>復元する表示状態を返す（最小化は通常表示へ置き換える）。</summary>
    /// <param name="state">保存された表示状態。</param>
    /// <returns>復元に使う表示状態。 最小化の場合は <see cref="EsWindowState.Normal"/>。</returns>
    public static EsWindowState ToRestorableState(EsWindowState state)
        => state == EsWindowState.Minimized ? EsWindowState.Normal : state;

    /// <summary>保存された配置を表示領域の内側へ収めた配置を返す。</summary>
    /// <param name="bounds">保存された配置（幅・高さは 1 以上であること）。</param>
    /// <param name="visibleArea">画面の表示領域（全モニターを包含する矩形）。</param>
    /// <returns>位置だけを表示領域の内側へ収めた配置（大きさは変えない）。</returns>
    /// <remarks>
    /// 大きさは変えない（利用者が決めた大きさを尊重する）。 画面より大きい場合は左上を表示領域へ合わせる。
    /// </remarks>
    public static EsRectInt ClampToVisibleArea(EsRectInt bounds, EsRectInt visibleArea)
    {
        var x = Math.Clamp(bounds.X, visibleArea.X, Math.Max(visibleArea.X, visibleArea.X + visibleArea.Width - bounds.Width));
        var y = Math.Clamp(bounds.Y, visibleArea.Y, Math.Max(visibleArea.Y, visibleArea.Y + visibleArea.Height - bounds.Height));
        return new EsRectInt(x, y, bounds.Width, bounds.Height);
    }
}
