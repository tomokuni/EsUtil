using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation;

/// <summary>本文を編集中の注釈を囲む破線の枠（見た目と形の規則。 単一所有）。</summary>
/// <remarks>
/// <b>編集中の注釈を 1 つに絞って示す</b>: 本文の編集中はドラッグ ハンドルを出しません（動かせないため）。
/// そのままでは「どの注釈を編集しているか」がキャレットだけでは分かりません（実測 2026-11-22）。<br/>
/// <b>ハンドルを出さず破線で示す</b>: 破線は「操作できる枠」ではなく「いま対象になっている枠」を表します。
/// 移動できない状態でハンドルを出すと、掴めるのに動かない枠に見えます。<br/>
/// <b>規則は 1 か所</b>: 太さ・破線の刻み・注釈の枠からの張り出しを 4 つの UI 版が同じ値で描きます。
/// </remarks>
public static class AnnotationEditingOutline
{
    /// <summary>破線の太さ（DIP）。</summary>
    public const double Thickness = 2;

    /// <summary>破線の刻み（DIP。 線 → 空白 の順）。</summary>
    public static IReadOnlyList<double> DashPattern { get; } = [5, 4];

    /// <summary>注釈の枠から外側へ張り出す量（DIP）。</summary>
    /// <remarks>注釈自身の枠線と重なると、どちらの線か分からなくなるため外側へずらします。</remarks>
    public const double Offset = 3;

    /// <summary>破線で囲む対象の矩形（元画像ピクセル）を求める。</summary>
    /// <param name="item">選択中の注釈。</param>
    /// <returns>矩形（元画像ピクセル）。 吹き出しは本文の矩形（文字を編集している範囲）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> が null の場合。</exception>
    /// <remarks>
    /// <b>本文を囲む</b>: 吹き出しの <c>Bounds</c> はしっぽを含むため、しっぽの先まで破線が伸びて
    /// 「本文を編集している」ことが伝わりません。 編集中は本文の矩形を囲みます。
    /// </remarks>
    public static EsRect ResolveSourceRect(AnnotationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item is BubbleAnnotation bubble ? bubble.ResolveBodyRect() : item.Bounds;
    }

    /// <summary>破線を描く矩形（DIP）を求める。</summary>
    /// <param name="sourceBounds">注釈の矩形（元画像ピクセル）。</param>
    /// <param name="scale">画像の表示倍率。</param>
    /// <param name="offset">画像の左上の位置（DIP）。</param>
    /// <returns>外側へ張り出した矩形（DIP）。</returns>
    public static EsRect Resolve(EsRect sourceBounds, double scale, EsPoint offset)
        => Resolve(new EsRect(
            (sourceBounds.X * scale) + offset.X,
            (sourceBounds.Y * scale) + offset.Y,
            sourceBounds.Width * scale,
            sourceBounds.Height * scale));

    /// <summary>破線を描く矩形（DIP）を求める。</summary>
    /// <param name="focusDipRect">選択中の注釈の矩形（DIP）。</param>
    /// <returns>外側へ張り出した矩形（DIP）。</returns>
    public static EsRect Resolve(EsRect focusDipRect)
        => new(
            focusDipRect.X - Offset,
            focusDipRect.Y - Offset,
            focusDipRect.Width + (Offset * 2),
            focusDipRect.Height + (Offset * 2));
}
