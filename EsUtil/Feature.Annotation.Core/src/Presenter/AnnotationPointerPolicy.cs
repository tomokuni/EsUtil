using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation.Presenter;

/// <summary>注釈の上でポインターが示す操作の種類を決める（単一所有）。</summary>
/// <remarks>
/// <b>ポインターの形は「何ができるか」の合図</b>である: 形が変われば、画像ではなく**重ねたオブジェクト**の
/// 上にいることと、ドラッグで動かせることが伝わります（実測 2026-11-22: 形が変わらず、掴めることが分からない）。<br/>
/// <b>優先順は しっぽ → ハンドル → 本体</b>である: しっぽの先端は本体の内側にもあるため、先に判定しないと
/// 先端を掴めません（<see cref="AnnotationHitTest.HitTestTail"/> と同じ順）。<br/>
/// <b>座標系は 2 つ受け取る</b>: ハンドルは画面の DIP、注釈は元画像ピクセルで持つため、UI は両方を渡します。
/// </remarks>
public static class AnnotationPointerPolicy
{
    /// <summary>ポインターが示す操作の種類を決める。</summary>
    /// <param name="document">文書。</param>
    /// <param name="selectedId">選択中の注釈の識別子（無い場合は <see langword="null"/>）。</param>
    /// <param name="sourcePoint">ポインターの位置（元画像ピクセル）。</param>
    /// <param name="dipPoint">ポインターの位置（画面の DIP）。</param>
    /// <param name="handleDipRects">選択枠の 8 ハンドルの矩形（DIP。 左上・上・右上・左・右・左下・下・右下の順）。 選択が無い場合は <see langword="null"/>。</param>
    /// <param name="tailRadiusSource">しっぽの先端を掴む半径（元画像ピクセル）。 0 以下は既定値。</param>
    /// <returns>操作の種類。 注釈の上でない場合は <see cref="AnnotationPointerKind.None"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> が <see langword="null"/> の場合。</exception>
    public static AnnotationPointerKind Resolve(
        AnnotationDocument document,
        string? selectedId,
        EsPoint sourcePoint,
        EsPoint dipPoint,
        IReadOnlyList<EsRect>? handleDipRects,
        double tailRadiusSource)
    {
        ArgumentNullException.ThrowIfNull(document);

        // **しっぽの先端を最優先**（本体の内側にある先端も掴めるようにする）
        if (AnnotationHitTest.HitTestTail(document, sourcePoint, tailRadiusSource) is { } tail
            && (selectedId is null || string.Equals(tail.Id, selectedId, StringComparison.Ordinal)))
        {
            return AnnotationPointerKind.Tail;
        }

        if (handleDipRects is { Count: 8 } && !string.IsNullOrEmpty(selectedId))
        {
            for (var i = 0; i < handleDipRects.Count; i++)
            {
                if (Contains(handleDipRects[i], dipPoint))
                {
                    return HandleKind(i);
                }
            }
        }

        return AnnotationHitTest.HitTest(document, sourcePoint) is not null
            ? AnnotationPointerKind.Move
            : AnnotationPointerKind.None;
    }

    /// <summary>ハンドルの位置（0〜7）を操作の種類へ写す。</summary>
    /// <param name="index">ハンドルの位置（左上・上・右上・左・右・左下・下・右下の順）。</param>
    /// <returns>操作の種類。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> が 0〜7 の範囲外の場合。</exception>
    public static AnnotationPointerKind HandleKind(int index) => index switch
    {
        0 => AnnotationPointerKind.ResizeTopLeft,
        1 => AnnotationPointerKind.ResizeTop,
        2 => AnnotationPointerKind.ResizeTopRight,
        3 => AnnotationPointerKind.ResizeLeft,
        4 => AnnotationPointerKind.ResizeRight,
        5 => AnnotationPointerKind.ResizeBottomLeft,
        6 => AnnotationPointerKind.ResizeBottom,
        7 => AnnotationPointerKind.ResizeBottomRight,
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, "ハンドルの位置は 0〜7 である必要があります。"),
    };

    /// <summary>矩形の内側に点があるかどうかを判定する（境界を含む）。</summary>
    /// <param name="rect">矩形。</param>
    /// <param name="point">点。</param>
    /// <returns>内側（境界を含む）の場合は <see langword="true"/>。</returns>
    private static bool Contains(EsRect rect, EsPoint point)
        => point.X >= rect.X && point.X <= rect.Right && point.Y >= rect.Y && point.Y <= rect.Bottom;
}
