using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation.Core;

/// <summary>注釈の幾何（矩形・しっぽの先端）を画像の内側へ収める規則（単一所有）。</summary>
/// <remarks>
/// <b>注釈は元画像の外へ出さない</b>: 画像の外へ出た部分は表示されず、掴めない（移動できない・
/// しっぽを掴めない）注釈になる。 追加・移動・サイズ変更・しっぽの移動のすべてが本クラスを通る。<br/>
/// <b>UI は座標を書き換えられない</b>: <see cref="AnnotationItem.Bounds"/> と
/// <see cref="BubbleAnnotation.Tail"/> の設定子は Core の内部に閉じてあるため、UI は
/// <see cref="AnnotationDocument.SetBounds"/> / <see cref="AnnotationDocument.SetTailTip"/> か
/// 本条の生成メソッドを使うしかない（規則の適用漏れが構造的に起きない）。<br/>
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
    /// 判定を UI ごとに書くと、片方だけ座標系を取り違えて「メニューが出ない」不具合になる（実測 2026-11-21）。
    /// </remarks>
    public static bool Contains(EsPoint point, EsSize imageSize)
        => point.X >= 0 && point.Y >= 0 && point.X < imageSize.Width && point.Y < imageSize.Height;

    /// <summary>しっぽの先端を画像の内側へ収める（掴める余白を残す）。</summary>
    /// <param name="point">収める点（元画像ピクセル）。</param>
    /// <param name="imageSize">画像の大きさ（元画像ピクセル）。</param>
    /// <returns>画像の内側に収まった点。</returns>
    /// <remarks>
    /// <b>辺の上には置かない</b>: 先端が画像の端ちょうどにあると、ハンドルが画像の外へはみ出して
    /// 切られ、掴めない（移動できない）。 ハンドルの半径ぶんの余白（<see cref="TailGrabMargin"/>）を
    /// 残した内側へ収める（実測 2026-11-21）。
    /// </remarks>
    public static EsPoint ClampTailTip(EsPoint point, EsSize imageSize)
    {
        var margin = TailGrabMargin;
        var maxX = Math.Max(margin, imageSize.Width - margin);
        var maxY = Math.Max(margin, imageSize.Height - margin);
        return new EsPoint(
            Math.Clamp(point.X, Math.Min(margin, maxX), maxX),
            Math.Clamp(point.Y, Math.Min(margin, maxY), maxY));
    }

    /// <summary>しっぽの先端を画像の端から離しておく距離（元画像ピクセル）。</summary>
    /// <remarks>しっぽのハンドル（半径 5〜6 DIP）が画像の内側に収まる大きさ。</remarks>
    internal const double TailGrabMargin = 10;

    /// <summary>枠線の注釈を作る（矩形を画像の内側へ収める）。</summary>
    /// <param name="bounds">矩形（元画像ピクセル）。</param>
    /// <param name="appearance">外観。</param>
    /// <param name="imageSize">画像の大きさ（元画像ピクセル）。</param>
    /// <returns>画像の内側に収まった枠線。</returns>
    public static FrameAnnotation CreateFrame(EsRect bounds, AnnotationAppearance appearance, EsSize imageSize)
        => new() { Bounds = ClampRect(bounds, imageSize), Appearance = appearance };

    /// <summary>吹き出しの注釈を作る（本文の矩形もしっぽの先端も画像の内側へ収める）。</summary>
    /// <param name="bounds">本文の矩形（元画像ピクセル）。</param>
    /// <param name="text">本文。</param>
    /// <param name="defaults">新規作成時の既定値（外観・余白・しっぽの幅と辺）。</param>
    /// <param name="imageSize">画像の大きさ（元画像ピクセル）。</param>
    /// <param name="tail">しっぽ（省略すると本文の下辺の中央から少し下）。 先端は常に画像の内側へ収める。</param>
    /// <returns>画像の内側に収まった吹き出し。</returns>
    /// <remarks>
    /// <b>しっぽの先端も収める</b>: 本文の下 40 px を既定にすると、本文が画像の下端にある場合に
    /// 先端が画像の外へ出る（表示されず掴めない。 実測 2026-11-21）。
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
            Appearance = defaults.BubbleAppearance,
            BodyPadding = defaults.BodyPadding,
            Text = text,
            Tail = resolved with { Tip = ClampTailTip(resolved.Tip, imageSize) },
        };
    }

    /// <summary>新規作成時にしっぽの先端を本文の下辺から離す距離（元画像ピクセル）。</summary>
    private const double TailGapFromBottom = 40;
}
