# EsUtil.Feature.Thumbnail.Presenter

![NuGet](https://img.shields.io/nuget/v/EsUtil.Feature.Thumbnail.Presenter.svg)
![Release](https://github.com/tomokuni/EsUtil/actions/workflows/release.yml/badge.svg)
![Build](https://github.com/tomokuni/EsUtil/actions/workflows/build.yml/badge.svg?branch=main)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%2B-0078D6)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![AOT](https://img.shields.io/badge/AOT-Ready-00695C)
![License](https://img.shields.io/badge/License-MIT-3DA639)

## 1. 概要

サムネイルの **外観・影・描画計画・ラベル計画** を単一所有する UI 非依存の提示層です。
「何を・どこへ・どの順で描くか」を計画として返し、描画 API の呼び出しは UI 層が行います。

| 何 | 型 |
| --- | --- |
| 外観（色・名前の体裁・影の有無） | `ThumbnailAppearance` |
| 影の計画（4 辺・4 隅・画素単位の帯） | `ThumbnailShadowPlan` |
| 1 件分の描画計画 | `ThumbnailDrawPlan` / `ThumbnailLabelPlan` |
| 提示のファサード | `ThumbnailPresenter` |

## 2. 使い方

```csharp
var presenter = new ThumbnailPresenter(appearance: ThumbnailAppearance.Default with
{
    HighlightedLabelArgb = 0xFF0078D4,
    HideShadowWhenHighlighted = true,
});

var layout = presenter.ResolveLayout(availableWidth, imageWidth, imageHeights);
var plan = presenter.ResolveDrawPlan(
    layout, index, caption, isCurrent, selectedIndex, hoveredIndex, isDark);

foreach (var layer in plan.Layers)
{
    // Background → Shadow → Image → Label の順に UI が描く
}
```

- 層の順序は `ThumbnailDrawLayer` が単一所有します（背景 → 影 → 画像 → 名前）。
- 選択の背景はホバーより優先します。 強調中に影を隠すかどうかは外観が決めます。
- 影は 4 辺とも同じ濃さで外側へ薄くし、4 隅は **画像の隅の頂点からの斜めの距離** で濃さを決めます。
  `BuildDeviceBands` の 1 画素の帯を単色で塗れば、どの UI 版でも同じ結果になります。

### 2.1 差し替え（ホストが独自の見た目にする）

差し替え点は 3 つです。

1. **外観の値**（`ThumbnailAppearance`）— 色・名前の体裁・影の有無（`Shadow = null`）と、既定の影の色・濃さ。
2. **影の算法**（`IThumbnailShadowPlan`）— 帯の作り方そのものを差し替えます。 実装を `ThumbnailAppearance.Shadow`
   へ渡すと、4 UI すべてがその帯を塗ります（AvaloniaUI は既定の実装だけ `BoxShadow` に任せ、それ以外は帯を塗ります）。
   余白（`Insets`）が `ThumbnailOptions.SurroundExtent` より広い場合は、レイアウトの項目の大きさが自動で広がります。
3. **描画計画**（`ThumbnailDrawPlan` は公開の `readonly record struct`）— `ThumbnailPresenter` を派生して
   `ResolveDrawPlan` を上書きし、背景・層の順序・名前の体裁・影の有無を変えます。

```csharp
sealed class OutlineOnlyPresenter(ThumbnailOptions? options = null)
    : ThumbnailPresenter(options, ThumbnailAppearance.Default)
{
    public override ThumbnailDrawPlan ResolveDrawPlan(
        ThumbnailLayoutResult layout, int index, string text, bool isCurrent,
        int selectedIndex, int hoveredIndex, bool isDark)
    {
        var plan = base.ResolveDrawPlan(layout, index, text, isCurrent, selectedIndex, hoveredIndex, isDark);

        // 影を外し、背景と画像と名前だけを描く計画へ差し替える
        return plan with { Shadow = null, Layers = [ThumbnailDrawLayer.Background, ThumbnailDrawLayer.Image, ThumbnailDrawLayer.Label] };
    }
}
```

```csharp
// 影の算法の差し替え（帯の作り方そのものを変える）
sealed class NoShadowPlan : IThumbnailShadowPlan
{
    public double BlurRadius => 0;
    public uint ColorArgb => 0;
    public ThumbnailShadowInsets Insets => default;
    public IReadOnlyList<ThumbnailShadowEdge> Edges => [];
    public IReadOnlyList<ThumbnailShadowStop> CornerStops => [];
    public IReadOnlyList<ThumbnailShadowBand> BuildDeviceBands(
        double x, double y, double width, double height, double scaling) => [];
}

presenter.Appearance = ThumbnailAppearance.Default with { Shadow = new NoShadowPlan() };
```

**帯を塗る手順（描画 API の呼び出し）は UI が持ちます**。 計画は「どの矩形をどの色で塗るか」までを返し、
描画 API 固有の呼び出しは UI に残ります（描画 API の抽象を持たないという層の規則）。

## 3. 含まれるもの

`ThumbnailAppearance` / `ThumbnailShadowPlan`（と `ThumbnailShadow*` の値型） / `ThumbnailDrawPlan` /
`ThumbnailLabelPlan` / `ThumbnailDrawLayer` / `ThumbnailPresenter`。

## 4. 依存

`EsUtil.Feature.Thumbnail.Core` / `EsUtil.Platform.Core`。 規則は [`.github/依存関係.md`](../../.github/依存関係.md)。

## 5. テスト

`tests/`（xUnit v3）。 影の濃さ（実測値）・帯の生成・層の間引き・強調時の色・太字の規則と、設定・外観の変更ログ（変化した項目だけを出力する）を検証します。

## 6. ログ

設定と外観の変更を記録します（既定は出力なし）。

| レベル | 出力例 |
| --- | --- |
| Information | `[Thumbnail] Presenter created`（ロガーの初回設定時に 1 度だけ） |
| Debug | `[Thumbnail] Setting: ItemSpacing 0 → 4` / `[Thumbnail] Setting: LabelFontSize 11 → 13`（変化した項目だけ） |
| Trace | `[Thumbnail] Logger attached` |

`ThumbnailPresenter.Log` へ `ILogger` を設定します（null は `NullLogger`）。 接頭辞は `LogPrefix` で設定します（既定 `[Thumbnail]`）。

## 7. ドキュメント

- 層の帰属 … [`../README.md`](../README.md)
- API リファレンス … [`docs/SPEC_API.md`](docs/SPEC_API.md)
- システム仕様書 … [`docs/SPEC_System.md`](docs/SPEC_System.md)

## 8. ライセンス

MIT（リポジトリの [LICENSE](../../LICENSE) を参照）。
