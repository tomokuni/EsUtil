using Aprillz.MewUI;
using Aprillz.MewUI.Rendering;
using Aprillz.MewUI.Text;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.MewUI.Marquee.Internal;

/// <summary>MewUI のテキスト描画（レイアウトの生成・キャッシュ・描画）を単一所有する。</summary>
/// <remarks>
/// 実寸の測定は <see cref="MewUITextMeasure"/> が単一所有する（同じテキスト エンジンで測る）。<br/>
/// 本クラスは <b>描画</b>の入口だけを提供する（レイアウトは文字列とサイズごとに作り直さず、使い回す）。<br/>
/// レイアウト規則（余白・行送り・配置）は各コントロールのコアが単一所有し、本クラスは描画のプリミティブのみを提供する。
/// </remarks>
internal static class MewText
{
    /// <summary>レイアウトのキャッシュ（文字列・フォントサイズ・DPI ごとに 1 つ）。</summary>
    /// <remarks>
    /// キーが増え続けないよう上限を設ける（超えたら作り直す）。 ラベルは数件しかないため、
    /// 上限を超えるのは文字列が毎フレーム変わる異常な場合だけである。
    /// </remarks>
    private static readonly Dictionary<(string Text, double FontSize, uint Dpi), ITextLayout> s_layouts = [];

    /// <summary>キャッシュに保持するレイアウトの上限。</summary>
    private const int CacheLimit = 64;

    /// <summary>テキストを左上原点で描画する。</summary>
    /// <param name="context">描画先のコンテキスト。</param>
    /// <param name="text">描画するテキスト。</param>
    /// <param name="left">描画領域の左端。</param>
    /// <param name="top">描画領域の上端。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <param name="color">文字色。</param>
    public static void Draw(
        IGraphicsContext context, string text, double left, double top, double fontSize, DrawingColor color)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (text.Length == 0)
        {
            return;
        }

        var layout = ResolveLayout(context, text, fontSize);
        var options = new TextDrawOptions { Foreground = color.ToMewUIColor() };
        context.Text.Draw(layout, new Point(left, top), in options);
    }

    /// <summary>文字列とサイズに対応するレイアウトを取得する（初回だけ生成する）。</summary>
    /// <param name="context">描画先のコンテキスト（DPI の取得元）。</param>
    /// <param name="text">描画するテキスト。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <returns>レイアウト。</returns>
    /// <exception cref="InvalidOperationException">グラフィックス バックエンドが登録されていない場合。</exception>
    private static ITextLayout ResolveLayout(IGraphicsContext context, string text, double fontSize)
    {
        var dpi = (uint)Math.Round(context.DpiScale * 96);
        var key = (text, fontSize, dpi);
        if (s_layouts.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (s_layouts.Count >= CacheLimit)
        {
            s_layouts.Clear();
        }

        var request = new TextLayoutRequest
        {
            Text = text.AsMemory(),
            Dpi = dpi,
            DefaultStyle = new TextRunStyle
            {
                FontFamily = "Segoe UI",
                FontSize = fontSize,
                Weight = FontWeight.Normal,
            },
        };

        var layout = Application.DefaultGraphicsFactory.TextEngine.CreateLayout(request);
        s_layouts[key] = layout;
        return layout;
    }
}
