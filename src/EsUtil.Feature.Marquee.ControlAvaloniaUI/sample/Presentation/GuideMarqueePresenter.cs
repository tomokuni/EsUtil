using EsUtil.Platform.Core.Native;
using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Marquee.AvaloniaUI.Sample.Presentation;

/// <summary>ガイド線付きの見た目を提供する派生提示層（<see cref="MarqueePresenter"/> の拡張の見本）。</summary>
/// <remarks>
/// <b>示すこと</b>: 既定の提示層は <c>sealed</c> ではなく <see cref="MarqueePresenter.ResolveDrawPlan"/> が <c>virtual</c> であるため、<br/>
/// 外観（<see cref="MarqueePresenter.Appearance"/>）と描画計画（<see cref="MarqueeDrawPlan"/>）を派生クラスで差し替えられる。<br/>
/// 差し替えは公開 API だけで行う（コアの internal 型も描画 API も参照しない）。<br/>
/// <br/>
/// <b>見た目</b>: 外枠を破線風に分割し、内側に三分割のガイド線を引く。<br/>
/// 破線もガイド線も「塗る矩形の列」として計画へ載せるため、描画実装（<c>MarqueeContextPainter</c> / <c>MarqueeSkiaPainter</c>）は<br/>
/// 変更なしにそのまま描ける（層の順序は既定の計画から引き継ぐ）。<br/>
/// <br/>
/// <b>外観の所有</b>: 生成時に自前の外観を設定する（枠線の太さ・表示可否も含む）。<br/>
/// 計画の解決は外観から導出されるため、表示可否と実際に描く値が食い違わない。
/// </remarks>
internal sealed class GuideMarqueePresenter : MarqueePresenter
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
    private static readonly DrawingColor AccentColor = DrawingColor.FromArgb(255, 255, 196, 0);

    /// <summary>ガイド線の色（半透明の白）。</summary>
    private static readonly DrawingColor GuideColor = DrawingColor.FromArgb(160, 255, 255, 255);

    /// <summary>内側の塗りの色（選択領域をわずかに明るくする）。</summary>
    private static readonly DrawingColor InnerFillColor = DrawingColor.FromArgb(28, 255, 255, 255);

    /// <summary>見本の提示層を初期化する。</summary>
    /// <param name="effects">効果の適用先。 null 不可。</param>
    /// <param name="cursor">カーソルサービス。 null 可。</param>
    /// <param name="options">初期設定。 null 可。</param>
    /// <remarks>外観は生成時に自前の値へ差し替える（利用側から変更されても本クラスの見た目の規則は崩れない）。</remarks>
    public GuideMarqueePresenter(IMarqueeEffects effects, ICursorService? cursor = null, MarqueeOptions? options = null)
        : base(effects, cursor, options)
        => Appearance = MarqueeAppearance.Default with
        {
            OuterBorderColor = AccentColor,
            InnerBorderColor = GuideColor,
            HandleColor = AccentColor,
            MarkerColor = AccentColor,
            ShowInnerBorder = true,
            ShowInnerFill = true,
            InnerFillColor = InnerFillColor,
        };

    /// <summary>注目矩形へ破線の枠と三分割のガイド線を載せた描画計画を解決する。</summary>
    /// <param name="clientSize">描画領域のサイズ（DIP）。</param>
    /// <param name="measureText">テキストの実寸を測る関数。 null 不可。</param>
    /// <returns>装飾だけを差し替えた描画計画。 注目矩形が無い場合は既定の計画をそのまま返す。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measureText"/> が null の場合。</exception>
    /// <remarks>
    /// 注目矩形が無い場合（未選択かつハイライトも無い）は差し替える対象が無いため、既定の計画を返す<br/>
    /// （装飾の層は計画が持つ値から決まるため、値の無い層を計画へ足さない）。
    /// </remarks>
    public override MarqueeDrawPlan ResolveDrawPlan(EsSize clientSize, Func<string, double, EsSize> measureText)
    {
        var plan = base.ResolveDrawPlan(clientSize, measureText);

        return plan.FocusDipRect is { } focus
            ? plan with
            {
                OuterBorderDipRects = CreateDashedFrame(focus),
                InnerBorderDipRects = CreateThirdsGuides(focus),
            }
            : plan;
    }

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
            new EsRect(focus.X + vertical * 2, focus.Y, LineThickness, focus.Height),
            new EsRect(focus.X, focus.Y + horizontal, focus.Width, LineThickness),
            new EsRect(focus.X, focus.Y + horizontal * 2, focus.Width, LineThickness),
        ];
    }
}
