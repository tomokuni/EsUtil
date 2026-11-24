using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.Annotation.Core.Defaults;
using EsUtil.Feature.Text;

namespace EsUtil.Feature.Annotation.Core.Geometry;

/// <summary>注釈の幾何（矩形・しっぽの先端）を画像の内側へ収める規則（単一所有）。</summary>
/// <remarks>
/// <b>注釈は元画像の外へ出さない</b>: 画像の外へ出た部分は表示されず、掴めない（移動できない・
/// しっぽを掴めない）注釈になる。 追加・移動・サイズ変更・しっぽの移動のすべてが本クラスを通る。<br/>
/// <b>UI は座標を書き換えられない</b>: 注釈は不変であり、変更は
/// <see cref="AnnotationDocument.WithBounds"/> / <see cref="AnnotationDocument.WithTail"/> か
/// 本条の生成メソッドを通すしかない（規則の適用漏れが構造的に起きない）。<br/>
/// <b>位置は動かし、大きさも収める</b>: 画像より大きい矩形は画像の大きさへ縮める（はみ出したまま
/// 置くと、どこを掴んでも画像の外になる）。
/// </remarks>
public static class AnnotationGeometry
{
    /// <summary>文書が対象とする画像の大きさを取得する（1 未満は 1 として扱う）。</summary>
    /// <param name="document">対象の文書。</param>
    /// <returns>画像の大きさ（元画像ピクセル）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> が null の場合。</exception>
    public static EsSize ResolveImageSize(AnnotationDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new EsSize(Math.Max(1, document.ImageWidth), Math.Max(1, document.ImageHeight));
    }

    /// <summary>矩形を画像の内側へ収める（大きさを収めたうえで位置を寄せる）。</summary>
    /// <param name="rect">収める矩形（元画像ピクセル）。</param>
    /// <param name="imageSize">画像の大きさ（元画像ピクセル）。</param>
    /// <returns>画像の内側に収まった矩形。</returns>
    public static EsRect ClampRect(EsRect rect, EsSize imageSize)
    {
        var imageWidth = Math.Max(1, imageSize.Width);
        var imageHeight = Math.Max(1, imageSize.Height);
        var width = Math.Clamp(rect.Width, 1, imageWidth);
        var height = Math.Clamp(rect.Height, 1, imageHeight);
        var x = Math.Clamp(rect.X, 0, Math.Max(0, imageWidth - width));
        var y = Math.Clamp(rect.Y, 0, Math.Max(0, imageHeight - height));
        return new EsRect(x, y, width, height);
    }

    /// <summary>点を画像の内側へ収める。</summary>
    /// <param name="point">収める点（元画像ピクセル）。</param>
    /// <param name="imageSize">画像の大きさ（元画像ピクセル）。</param>
    /// <returns>画像の内側に収まった点。</returns>
    /// <remarks>辺の上は内側として扱う（しっぽの先端が画像の端に来ることを許す）。</remarks>
    public static EsPoint ClampPoint(EsPoint point, EsSize imageSize)
        => new(
            Math.Clamp(point.X, 0, Math.Max(0, imageSize.Width)),
            Math.Clamp(point.Y, 0, Math.Max(0, imageSize.Height)));

    /// <summary>点が画像の内側かどうかを判定する。</summary>
    /// <param name="point">判定する点（元画像ピクセル）。</param>
    /// <param name="imageSize">画像の大きさ（元画像ピクセル）。</param>
    /// <returns>内側の場合は <see langword="true"/>（右端・下端の辺は外側）。</returns>
    /// <remarks>
    /// <b>画像の外の操作は注釈の対象外</b>にする（プレビューの余白を右クリックしても追加メニューを出さない）。
    /// 判定を UI ごとに書くと、片方だけ座標系を取り違えて「メニューが出ない」不具合になる。
    /// </remarks>
    public static bool Contains(EsPoint point, EsSize imageSize)
        => point.X >= 0 && point.Y >= 0 && point.X < imageSize.Width && point.Y < imageSize.Height;

    /// <summary>しっぽの先端を画像の内側へ収める。</summary>
    /// <param name="point">収める点（元画像ピクセル）。</param>
    /// <param name="imageSize">画像の大きさ（元画像ピクセル）。</param>
    /// <param name="margin">画像の端から残す距離（元画像ピクセル。 0 は端まで許す）。</param>
    /// <returns>画像の内側に収まった点。</returns>
    /// <remarks>
    /// <b>画像の外へは出さない</b>: 先端が画像の外にあると、しっぽが表示されず掴めない。<br/>
    /// <b>端の余白は UI の都合である</b>: 画面の DIP で一定の大きさのハンドルを掴めるようにする距離は、
    /// 注釈の値ではなく表示の都合であり、ズーム率で変わる。 保存する値へ持ち込まないよう、注釈の文書は
    /// <paramref name="margin"/> を 0 として扱い、UI が自分の表示倍率で DIP の距離を元画像ピクセルへ換算して
    /// （<see cref="AnnotationViewTransform.ToSourceRadius"/>）渡す。
    /// </remarks>
    public static EsPoint ClampTailTip(EsPoint point, EsSize imageSize, double margin = 0)
    {
        var width = Math.Max(1, imageSize.Width);
        var height = Math.Max(1, imageSize.Height);
        var inset = double.IsFinite(margin) && margin > 0 ? margin : 0;
        var minX = Math.Min(inset, width);
        var minY = Math.Min(inset, height);
        var maxX = Math.Max(minX, width - inset);
        var maxY = Math.Max(minY, height - inset);
        return new EsPoint(
            Math.Clamp(point.X, minX, maxX),
            Math.Clamp(point.Y, minY, maxY));
    }

    /// <summary>新規作成する注釈の矩形を求める（左上を指定位置へ寄せ、画像の内側へ収める）。</summary>
    /// <param name="anchor">左上にしたい位置（元画像ピクセル）。</param>
    /// <param name="imageSize">画像の大きさ（元画像ピクセル）。</param>
    /// <returns>新規作成する注釈の矩形（既定の大きさ。 画像が小さい場合は画像の大きさ）。</returns>
    /// <remarks>
    /// <b>追加位置の規則は Core が単一所有する</b>: 追加メニュー・サンプル・アプリが同じ位置へ同じ大きさで
    /// 注釈を作る（UI ごとに計算すると、画像の端でだけはみ出す）。<br/>
    /// 大きさは <see cref="AnnotationDefaults.NewItemWidthDefault"/> / <see cref="AnnotationDefaults.NewItemHeightDefault"/>
    /// と画像の小さいほうを使い、位置は画像の内側へ収める。
    /// </remarks>
    public static EsRect ResolveDraftBounds(EsPoint anchor, EsSize imageSize)
    {
        var imageWidth = Math.Max(1, imageSize.Width);
        var imageHeight = Math.Max(1, imageSize.Height);
        var width = Math.Min(AnnotationDefaults.NewItemWidthDefault, imageWidth);
        var height = Math.Min(AnnotationDefaults.NewItemHeightDefault, imageHeight);

        return new EsRect(
            Math.Clamp(anchor.X, 0, Math.Max(0, imageWidth - width)),
            Math.Clamp(anchor.Y, 0, Math.Max(0, imageHeight - height)),
            width,
            height);
    }

    /// <summary>枠線の注釈を作る（矩形を画像の内側へ収める）。</summary>
    /// <param name="bounds">矩形（元画像ピクセル）。</param>
    /// <param name="appearance">外観。</param>
    /// <param name="imageSize">画像の大きさ（元画像ピクセル）。</param>
    /// <returns>画像の内側に収まった枠線。</returns>
    public static FrameAnnotation CreateFrame(EsRect bounds, AnnotationAppearance appearance, EsSize imageSize)
        => new() { Bounds = ClampRect(bounds, imageSize), Appearance = appearance.Normalized() };

    /// <summary>吹き出しの注釈を作る（本文の矩形もしっぽの先端も画像の内側へ収める）。</summary>
    /// <param name="bounds">本文の矩形（元画像ピクセル）。</param>
    /// <param name="text">本文。</param>
    /// <param name="defaults">新規作成時の既定値（外観・余白・しっぽの幅と辺）。</param>
    /// <param name="imageSize">画像の大きさ（元画像ピクセル）。</param>
    /// <param name="tail">しっぽ（省略すると本文の下辺の中央から少し下）。 先端は常に画像の内側へ収める。</param>
    /// <returns>画像の内側に収まった吹き出し。</returns>
    /// <remarks>
    /// <b>しっぽの先端も収める</b>: 本文の下 40 px を既定にすると、本文が画像の下端にある場合に
    /// 先端が画像の外へ出る（表示されず掴めない）。
    /// </remarks>
    public static BubbleAnnotation CreateBubble(
        EsRect bounds,
        RichText text,
        AnnotationDraftDefaults defaults,
        EsSize imageSize,
        BubbleTail? tail = null)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        ArgumentNullException.ThrowIfNull(text);

        var clamped = ClampRect(bounds, imageSize);
        var resolved = tail ?? new BubbleTail(
            new EsPoint(clamped.Center.X, clamped.Bottom + TailGapFromBottom),
            defaults.TailEdge,
            AnnotationDefaults.TailBaseOffsetDefault,
            defaults.TailWidth);

        return new BubbleAnnotation
        {
            Bounds = clamped,
            Appearance = defaults.BubbleAppearance.Normalized(),
            BodyPadding = AnnotationLimits.CoerceBodyPadding(defaults.BodyPadding),
            Text = text.Normalized(),
            Tail = (resolved with { Tip = ClampTailTip(resolved.Tip, imageSize) }).Normalized(),
        };
    }

    /// <summary>新規作成時にしっぽの先端を本文の下辺から離す距離（元画像ピクセル）。</summary>
    private const double TailGapFromBottom = 40;
}
