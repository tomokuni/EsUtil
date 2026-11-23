namespace EsUtil.Feature.Annotation;

/// <summary>注釈の種別（描画計画の分岐に使う）。</summary>
public enum AnnotationKind
{
    /// <summary>枠線（矩形 + 角丸）。</summary>
    Frame = 0,

    /// <summary>吹き出し（本文 + しっぽ）。</summary>
    Bubble = 1,
}
