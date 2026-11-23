using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation;

/// <summary>注釈レイヤー 1 フレーム分の描画計画（提示層から UI へ渡す唯一の契約）。</summary>
/// <remarks>
/// <b>描く値はすべて解決済み</b>である（配置・折り返し・しっぽの形・色）。 UI は自分の描画 API へ写すだけにする。<br/>
/// <b>選択枠（ハンドル）は含まない</b>: 選択中の枠とハンドルは Marquee の提示層が
/// 解決する（選択枠の見た目を 4 つの UI 版で揃えるため、同じ仕組みを使う）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で検証できる。
/// </remarks>
/// <param name="ImageSize">元画像の大きさ（元画像ピクセル）。</param>
/// <param name="SelectedId">選択中の注釈の識別子（無い場合は <see langword="null"/>）。</param>
/// <param name="Shapes">注釈の描画計画（先頭が最背面、末尾が最前面）。</param>
public sealed record AnnotationDrawPlan(
    EsSize ImageSize,
    string? SelectedId,
    IReadOnlyList<AnnotationShapePlan> Shapes);
