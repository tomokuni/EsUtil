namespace EsUtil.Feature.Annotation.Presenter;

/// <summary>注釈レイヤーの上でポインターが示す操作の種類。</summary>
/// <remarks>
/// <b>UI は形への写像だけを行う</b>: 「どこで何ができるか」を UI ごとに書くと、版によって
/// ポインターの形（＝ドラッグできる合図）が変わります。 判断は <see cref="AnnotationPointerPolicy"/> が単一所有します。
/// </remarks>
public enum AnnotationPointerKind
{
    /// <summary>注釈の上ではない（プレビューなど背面へ委ねる）。</summary>
    None = 0,

    /// <summary>注釈の本体（移動できる）。</summary>
    Move,

    /// <summary>左上のハンドル（サイズ変更）。</summary>
    ResizeTopLeft,

    /// <summary>上のハンドル（高さの変更）。</summary>
    ResizeTop,

    /// <summary>右上のハンドル（サイズ変更）。</summary>
    ResizeTopRight,

    /// <summary>左のハンドル（幅の変更）。</summary>
    ResizeLeft,

    /// <summary>右のハンドル（幅の変更）。</summary>
    ResizeRight,

    /// <summary>左下のハンドル（サイズ変更）。</summary>
    ResizeBottomLeft,

    /// <summary>下のハンドル（高さの変更）。</summary>
    ResizeBottom,

    /// <summary>右下のハンドル（サイズ変更）。</summary>
    ResizeBottomRight,

    /// <summary>しっぽの先端（吹き出し口を動かせる）。</summary>
    Tail,
}
