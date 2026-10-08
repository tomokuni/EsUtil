using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Loupe.AvaloniaUI.Sample.Presentation;

/// <summary>格子線と四隅の鉤を与える見本の定義（外観と描画計画の差し替え）。</summary>
/// <remarks>
/// <b>示すこと</b>: 提示層を派生しなくても、外観（<see cref="LoupeAppearance"/>）と描画計画（<see cref="LoupeDrawPlan"/>）で<br/>
/// 見た目を差し替えられる（<see cref="LoupeControl.Appearance"/> と <see cref="LoupeControl.DrawPlanResolver"/> へ設定する）。<br/>
/// 差し替えは公開 API だけで行う（コアの internal 型も描画 API も参照しない）。<br/>
/// <br/>
/// <b>見た目</b>: 十字線の代わりに中心の格子線、枠線の代わりに四隅の鉤（コーナーブラケット）を描く。<br/>
/// どちらも「塗る矩形の列」として計画へ載せるため、描画実装は計画のとおり描く（層の順序は既定の計画から引き継ぐ）。<br/>
/// <br/>
/// <b>寸法の換算</b>: 描画面の矩形は物理ピクセルであるため、DIP の太さは計画が持つ倍率で換算する<br/>
/// （<see cref="LoupeDrawPlan.ScaleDipToPixel(double)"/>＝提示層が枠線の太さへ適用するのと同じ規則）。
/// </remarks>
internal static class GridLoupePlan
{
    /// <summary>格子線の太さ（DIP）。</summary>
    private const double GridThickness = 1.0;

    /// <summary>四隅の鉤の腕の長さ（DIP）。</summary>
    private const double BracketArm = 18.0;

    /// <summary>枠線の太さ（DIP）。</summary>
    private const int BracketThickness = 3;

    /// <summary>格子と鉤の色（アンバー）。</summary>
    private static readonly DrawingColor s_accentColor = DrawingColor.FromArgb(255, 255, 196, 0);

    /// <summary>格子線の色（半透明の白）。</summary>
    private static readonly DrawingColor s_gridColor = DrawingColor.FromArgb(150, 255, 255, 255);

    /// <summary>見本の外観を取得する。</summary>
    /// <value>格子と鉤の色・枠線の太さを設定した外観。</value>
    public static LoupeAppearance Appearance { get; } = LoupeAppearance.Default with
    {
        BorderColor = s_accentColor,
        CrossColor = s_gridColor,
        CoordLabelBackgroundColor = s_accentColor,
        CoordLabelColor = DrawingColor.White,
        BorderThickness = BracketThickness,
    };

    /// <summary>中心の格子線と四隅の鉤を載せた描画計画を返す。</summary>
    /// <param name="plan">既定の解決結果（幾何・外観・層の順序・ラベル配置を持つ）。</param>
    /// <returns>十字線と枠線だけを差し替えた描画計画。</returns>
    /// <remarks>
    /// 元の拡大画像・切り出し領域・ラベル配置は既定の解決をそのまま引き継ぐ<br/>
    /// （差し替えるのは装飾の値だけであり、差し替えても他の値の整合は崩れない）。<br/>
    /// DIP で書いた寸法は計画が持つ倍率（<see cref="LoupeDrawPlan.ScaleDipToPixel(double)"/>）で物理ピクセルへ換算するため、<br/>
    /// 表示スケーリング（DPI）が変わっても見た目は DIP 基準のままになる。<br/>
    /// 本メソッドは同じ入力では 1 フレーム 1 回だけ呼ばれるが、状態を持たない（純粋関数）。
    /// </remarks>
    public static LoupeDrawPlan Resolve(in LoupeDrawPlan plan)
    {
        var surface = plan.ClientPixelRect;

        return plan with
        {
            CrosshairPixelRects = CreateCenterGrid(surface, plan.ScaleDipToPixel(GridThickness)),
            BorderPixelRects = CreateCornerBrackets(
                surface, plan.ScaleDipToPixel(BracketArm), plan.ScaleDipToPixel(BracketThickness)),
        };
    }

    /// <summary>見本の元画像（グラデーション）を生成する。</summary>
    /// <returns>幅 240 × 高さ 160 の画素バッファ。</returns>
    /// <remarks>ルーペのソースは UI 非依存の画素バッファ（<see cref="EsWritableBitmap"/>）として本見本が自前で用意する。</remarks>
    public static EsWritableBitmap CreateSource()
    {
        var bitmap = EsWritableBitmap.Create(240, 160);

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                bitmap.SetPixel(x, y, 60, (byte)(y * 255 / bitmap.Height), (byte)(x * 255 / bitmap.Width), 255);
            }
        }

        return bitmap;
    }

    /// <summary>描画面の中心へ十字の格子線を作る。</summary>
    /// <param name="surface">描画面の矩形（物理ピクセル）。</param>
    /// <param name="thickness">線の太さ（物理ピクセル）。</param>
    /// <returns>格子線の矩形（縦 1 本 + 横 1 本）。</returns>
    private static IReadOnlyList<EsRect> CreateCenterGrid(EsRect surface, double thickness)
    {
        var half = thickness / 2;

        return
        [
            new EsRect(surface.Center.X - half, surface.Y, thickness, surface.Height),
            new EsRect(surface.X, surface.Center.Y - half, surface.Width, thickness),
        ];
    }

    /// <summary>描画面の四隅へ鉤（コーナーブラケット）を作る。</summary>
    /// <param name="surface">描画面の矩形（物理ピクセル）。</param>
    /// <param name="arm">鉤の腕の長さ（物理ピクセル）。</param>
    /// <param name="thickness">鉤の太さ（物理ピクセル）。</param>
    /// <returns>鉤の矩形（四隅 × 2 本 = 8 本）。</returns>
    private static IReadOnlyList<EsRect> CreateCornerBrackets(EsRect surface, double arm, double thickness)
        =>
        [
            new EsRect(surface.X, surface.Y, arm, thickness),
            new EsRect(surface.X, surface.Y, thickness, arm),
            new EsRect(surface.Right - arm, surface.Y, arm, thickness),
            new EsRect(surface.Right - thickness, surface.Y, thickness, arm),
            new EsRect(surface.X, surface.Bottom - thickness, arm, thickness),
            new EsRect(surface.X, surface.Bottom - arm, thickness, arm),
            new EsRect(surface.Right - arm, surface.Bottom - thickness, arm, thickness),
            new EsRect(surface.Right - thickness, surface.Bottom - arm, thickness, arm),
        ];
}
