namespace EsUtil.Feature.Annotation;

/// <summary>キャレットの位置（元画像ピクセル）。</summary>
/// <remarks>幅は持たない（描く側が線の太さを決める。 太さは表示倍率に依らず一定にするため DIP で与える）。</remarks>
/// <param name="X">キャレットの左端。</param>
/// <param name="Y">行の上端。</param>
/// <param name="Height">行の高さ（キャレットの高さ）。</param>
public readonly record struct AnnotationCaretRect(double X, double Y, double Height);
