using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Marquee.MewUI.Sample.Presentation;

/// <summary>ガイド線付きの見た目を与える見本の定義（外観と描画計画の差し替え）。</summary>
/// <remarks>
/// <b>示すこと</b>: 提示層を派生しなくても、外観（<see cref="MarqueeAppearance"/>）と描画計画（<see cref="MarqueeDrawPlan"/>）で<br/>
/// 見た目を差し替えられる（<c>MarqueeControl.Appearance</c> と <c>MarqueeControl.DrawPlanResolver</c> へ設定する）。<br/>
/// 差し替えは公開 API だけで行う（コアの internal 型も描画 API も参照しない）。<br/>
/// <br/>
/// <b>見た目</b>: 外枠を破線風に分割し、内側に三分割のガイド線を引く。<br/>
/// 破線もガイド線も「塗る矩形の列」として計画へ載せるため、描画実装は計画のとおり描く（層の順序は既定の計画から引き継ぐ）。<br/>
/// <br/>
/// <b>外観の所有</b>: 外観（色・枠線の太さ・表示可否）も本クラスが与える（計画の解決は外観から導出されるため、表示可否と実際に描く値が食い違わない）。
/// </remarks>
internal static class GuideMarqueePlan
{
    /// <summary>破線 1 本の長さ（DIP）。</summary>
    private const double DashLength = 9.0;

    /// <summary>破線の間隔（DIP）。</summary>
    private const double DashGap = 6.0;

    /// <summary>破線とガイド線の太さ（DIP）。</summary>
    private const double LineThickness = 2.0;

    /// <summary>三分割の分母（1/3 と 2/3 の位置に引く）。</summary>
    private const int ThirdsDenominator = 3;

    /// <summary>破線とガイド線の色（アンバー）。</summary>
    private static readonly DrawingColor s_accentColor = DrawingColor.FromArgb(255, 255, 196, 0);

    /// <summary>ガイド線の色（半透明の白）。</summary>
    private static readonly DrawingColor s_guideColor = DrawingColor.FromArgb(160, 255, 255, 255);

    /// <summary>内側の塗りの色（選択領域をわずかに明るくする）。</summary>
    private static readonly DrawingColor s_innerFillColor = DrawingColor.FromArgb(28, 255, 255, 255);

    /// <summary>見本の外観を取得する。</summary>
    /// <value>破線とガイド線の色・表示可否を設定した外観。</value>
    public static MarqueeAppearance Appearance { get; } = MarqueeAppearance.Default with
    {
        OuterBorderColor = s_accentColor,
        InnerBorderColor = s_guideColor,
        HandleColor = s_accentColor,
        MarkerColor = s_accentColor,
        ShowInnerBorder = true,
        ShowInnerFill = true,
        InnerFillColor = s_innerFillColor,
    };

    /// <summary>注目矩形へ破線の枠と三分割のガイド線を載せた描画計画を返す。</summary>
    /// <param name="plan">既定の解決結果（幾何・外観・層の順序・ラベル配置を持つ）。</param>
    /// <returns>装飾だけを差し替えた描画計画。 注目矩形が無い場合は受け取った計画をそのまま返す。</returns>
    /// <remarks>
    /// 注目矩形が無い場合（未選択かつハイライトも無い）は差し替える対象が無いため、受け取った計画を返す<br/>
    /// （装飾の層は計画が持つ値から決まるため、値の無い層を計画へ足さない）。<br/>
    /// 本メソッドは同じ入力では 1 フレーム 1 回だけ呼ばれるが、状態を持たない（純粋関数）。
    /// </remarks>
    public static MarqueeDrawPlan Resolve(in MarqueeDrawPlan plan)
        => plan.FocusDipRect is { } focus
            ? plan with
            {
                OuterBorderDipRects = CreateDashedFrame(focus),
                InnerBorderDipRects = CreateThirdsGuides(focus),
            }
            : plan;

    /// <summary>矩形の 4 辺を破線へ分割する。</summary>
    /// <param name="focus">注目矩形（DIP）。</param>
    /// <returns>破線 1 本ずつの矩形（DIP）。</returns>
    private static IReadOnlyList<EsRect> CreateDashedFrame(EsRect focus)
    {
        var rects = new List<EsRect>();

        AddHorizontalDashes(rects, focus.X, focus.Y, focus.Width);
        AddHorizontalDashes(rects, focus.X, focus.Bottom - LineThickness, focus.Width);
        AddVerticalDashes(rects, focus.X, focus.Y, focus.Height);
        AddVerticalDashes(rects, focus.Right - LineThickness, focus.Y, focus.Height);

        return rects;
    }

    /// <summary>水平方向へ破線を並べる。</summary>
    /// <param name="rects">追加先の一覧。</param>
    /// <param name="left">始点の X 座標（DIP）。</param>
    /// <param name="top">Y 座標（DIP）。</param>
    /// <param name="length">並べる長さ（DIP）。</param>
    private static void AddHorizontalDashes(List<EsRect> rects, double left, double top, double length)
    {
        var pitch = DashLength + DashGap;

        for (var offset = 0.0; offset < length; offset += pitch)
        {
            var width = Math.Min(DashLength, length - offset);
            rects.Add(new EsRect(left + offset, top, width, LineThickness));
        }
    }

    /// <summary>垂直方向へ破線を並べる。</summary>
    /// <param name="rects">追加先の一覧。</param>
    /// <param name="left">X 座標（DIP）。</param>
    /// <param name="top">始点の Y 座標（DIP）。</param>
    /// <param name="length">並べる長さ（DIP）。</param>
    private static void AddVerticalDashes(List<EsRect> rects, double left, double top, double length)
    {
        var pitch = DashLength + DashGap;

        for (var offset = 0.0; offset < length; offset += pitch)
        {
            var height = Math.Min(DashLength, length - offset);
            rects.Add(new EsRect(left, top + offset, LineThickness, height));
        }
    }

    /// <summary>注目矩形の内側へ三分割のガイド線を引く。</summary>
    /// <param name="focus">注目矩形（DIP）。</param>
    /// <returns>ガイド線の矩形（縦 2 本 + 横 2 本。 DIP）。</returns>
    private static IReadOnlyList<EsRect> CreateThirdsGuides(EsRect focus)
    {
        var horizontal = focus.Height / ThirdsDenominator;
        var vertical = focus.Width / ThirdsDenominator;

        return
        [
            new EsRect(focus.X + vertical, focus.Y, LineThickness, focus.Height),
            new EsRect(focus.X + (vertical * 2), focus.Y, LineThickness, focus.Height),
            new EsRect(focus.X, focus.Y + horizontal, focus.Width, LineThickness),
            new EsRect(focus.X, focus.Y + (horizontal * 2), focus.Width, LineThickness),
        ];
    }
}
