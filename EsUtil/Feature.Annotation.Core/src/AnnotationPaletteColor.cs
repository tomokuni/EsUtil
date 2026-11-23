namespace EsUtil.Feature.Annotation;

/// <summary>代表色の 1 色（色名と保存値）。</summary>
/// <param name="Name">色名（日本語）。</param>
/// <param name="Value">保存値（<c>#AARRGGBB</c>）。</param>
public readonly record struct AnnotationPaletteColor(string Name, string Value);
