using EsUtil.Feature.Loupe;
using EsUtil.Feature.Loupe.Presenter.Drawing;
using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Loupe.AvaloniaUI.Sample.Presentation;

/// <summary>格子と四隅の飾りを提供する派生提示層（<see cref="LoupePresenter"/> の拡張の見本）。</summary>
/// <remarks>
/// <b>示すこと</b>: 既定の提示層は <c>sealed</c> ではなく <see cref="LoupePresenter.ResolveDrawPlan"/> が <c>virtual</c> であるため、<br/>
/// 外観（<see cref="LoupePresenter.Appearance"/>）と描画計画（<see cref="LoupeDrawPlan"/>）を派生クラスで差し替えられる。<br/>
/// 差し替えは公開 API だけで行う（コアの internal 型も描画 API も参照しない）。<br/>
/// <br/>
/// <b>見た目</b>: 十字線の代わりに中心の格子線、枠線の代わりに四隅の鉤（コーナーブラケット）を描く。<br/>
/// どちらも「塗る矩形の列」として計画へ載せるため、描画実装（<c>LoupeContextPainter</c> / <c>LoupeSkiaPainter</c>）は<br/>
/// 変更なしにそのまま描ける（層の順序は既定の計画から引き継ぐ）。<br/>
/// <br/>
/// <b>寸法の換算</b>: 描画面の矩形は物理ピクセルであるため、DIP の太さは <see cref="LoupePresenter.ClientToPixelScaling"/> で換算する<br/>
/// （換算の所有者はコア側にあり、提示層は同じ倍率を参照して自前の装飾へ適用する）。
/// </remarks>
internal sealed class GridLoupePresenter : LoupePresenter
{
    /// <summary>格子線の太さ（DIP）。</summary>
    private const double GridThickness = 1.0;

    /// <summary>四隅の鉤の腕の長さ（DIP）。</summary>
    private const double BracketArm = 18.0;

    /// <summary>枠線の太さ（DIP）。</summary>
    private const double BracketThickness = 3.0;

    /// <summary>格子と鉤の色（アンバー）。</summary>
    private static readonly DrawingColor AccentColor = DrawingColor.FromArgb(255, 255, 196, 0);

    /// <summary>格子線の色（半透明の白）。</summary>
    private static readonly DrawingColor GridColor = DrawingColor.FromArgb(150, 255, 255, 255);

    /// <summary>見本の提示層を初期化する。</summary>
    /// <remarks>外観は生成時に自前の値へ差し替える（利用側から変更されても本クラスの見た目の規則は崩れない）。</remarks>
    public GridLoupePresenter()
        => Appearance = LoupeAppearance.Default with
        {
            BorderColor = AccentColor,
            CrossColor = GridColor,
            CoordLabelBackgroundColor = AccentColor,
            CoordLabelColor = DrawingColor.White,
            BorderThickness = (int)BracketThickness,
        };

    /// <summary>中心の格子線と四隅の鉤を載せた描画計画を解決する。</summary>
    /// <param name="displaySize">ルーペの一辺の表示サイズ（DIP）。</param>
    /// <param name="measureText">テキストの実寸を測る関数。 null 不可。</param>
    /// <returns>十字線と枠線だけを差し替えた描画計画。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measureText"/> が null の場合。</exception>
    /// <remarks>
    /// 元の拡大画像・切り出し領域・ラベル配置は既定の解決をそのまま引き継ぐ<br/>
    /// （差し替えるのは装飾の値だけであり、差し替えても他の値の整合は崩れない）。
    /// </remarks>
    public override LoupeDrawPlan ResolveDrawPlan(
        double displaySize, Func<string, double, EsSize> measureText)
    {
        var plan = base.ResolveDrawPlan(displaySize, measureText);
        var surface = plan.ClientPixelRect;
        var scaling = ClientToPixelScaling;

        return plan with
        {
            CrosshairPixelRects = CreateCenterGrid(surface, GridThickness * scaling),
            BorderPixelRects = CreateCornerBrackets(surface, BracketArm * scaling, BracketThickness * scaling),
        };
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
