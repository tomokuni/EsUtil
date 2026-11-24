using System.Text.Json.Serialization;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation;

/// <summary>画像の上へ重ねる注釈 1 件の基底（枠線・吹き出し）。</summary>
/// <remarks>
/// <b>座標は元画像ピクセル</b>である（<see cref="Bounds"/>）。 表示倍率・DPI に依存させないため、
/// JSON は別の環境でも同じ意味になり、保存画像と比率が一致する。<br/>
/// <b>不変である</b>: 変更は <c>with</c> 式で新しい値を作り、<see cref="AnnotationDocument"/> へ差し替える。
/// 変更履歴・描画中の値・保存した値が同じ実体を指して書き換わる事故を型で防ぐ。<br/>
/// <b>型は JSON の識別子で保存する</b>（<c>Kind</c> に <c>Frame</c> / <c>Bubble</c>）。 未知の種別は読み込み時に
/// 例外になる（前方互換の扱いは <see cref="AnnotationJson"/> が定める）。
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "Kind")]
[JsonDerivedType(typeof(FrameAnnotation), "Frame")]
[JsonDerivedType(typeof(BubbleAnnotation), "Bubble")]
public abstract record AnnotationItem
{
    /// <summary>注釈の識別子を取得する。</summary>
    /// <remarks>選択・編集・JSON の参照に使う（利用者が書き換えない）。</remarks>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>注釈の矩形（元画像ピクセル）を取得する。</summary>
    /// <remarks>
    /// <b>変更は <see cref="AnnotationDocument.WithBounds"/> を通す</b>: 画像の外へ出た注釈は掴めなくなるため、
    /// 矩形は画像の内側へ収めてから差し替える（収める規則は Core が単一所有する）。
    /// </remarks>
    public EsRect Bounds { get; init; }

    /// <summary>外観（枠線・内側・角丸）を取得する。</summary>
    public AnnotationAppearance Appearance { get; init; } = AnnotationAppearance.Default;
}
