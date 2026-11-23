using System.Text.Json.Serialization;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation.Core;

/// <summary>画像の上へ重ねる注釈 1 件の基底（枠線・吹き出し）。</summary>
/// <remarks>
/// <b>座標は元画像ピクセル</b>である（<see cref="Bounds"/>）。 表示倍率・DPI に依存させないため、
/// JSON は別の環境でも同じ意味になり、保存画像と比率が一致する。<br/>
/// <b>型は JSON の識別子で保存する</b>（<c>Kind</c> に <c>Frame</c> / <c>Bubble</c>）。 未知の種別は読み込み時に
/// 例外になる（前方互換の扱いは <see cref="AnnotationJson"/> が定める）。
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "Kind")]
[JsonDerivedType(typeof(FrameAnnotation), "Frame")]
[JsonDerivedType(typeof(BubbleAnnotation), "Bubble")]
public abstract class AnnotationItem
{
    /// <summary>注釈の識別子を取得または設定する。</summary>
    /// <remarks>選択・編集・JSON の参照に使う（利用者が書き換えない）。</remarks>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>注釈の矩形（元画像ピクセル）を取得する。</summary>
    /// <remarks>
    /// <b>設定子は Core の内部に閉じる</b>: 画像の外へ出た注釈は掴めなくなるため、変更は
    /// <see cref="AnnotationDocument.SetBounds"/>（画像の内側へ収める）を通す。 UI から直接書き換えると
    /// 版ごとに収め忘れが起きる（実測 2026-11-21: しっぽの先端と矩形が画像の外へ出た）。
    /// </remarks>
    [JsonInclude]
    public EsRect Bounds { get; internal set; }

    /// <summary>外観（枠線・内側・角丸）を取得または設定する。</summary>
    public AnnotationAppearance Appearance { get; set; } = AnnotationAppearance.Default;

    /// <summary>同じ内容の複製を作る（変更履歴の記録に使う）。</summary>
    /// <returns>複製。 識別子も同じ値を持つ（同じ注釈を指す）。</returns>
    /// <remarks>
    /// <b>複製は変更履歴のために行う</b>: 注釈の座標・しっぽはドラッグ中に書き換わるため、
    /// 記録した文書をそのまま持つと過去の状態が書き換わる。 派生型は自分が持つ参照型の値を複製する。
    /// </remarks>
    public virtual AnnotationItem Clone() => (AnnotationItem)MemberwiseClone();
}
