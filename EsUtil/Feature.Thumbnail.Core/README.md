# EsUtil.Feature.Thumbnail.Core

[![NuGet](https://img.shields.io/nuget/v/EsUtil.Feature.Thumbnail.Core.svg)](https://www.nuget.org/packages/EsUtil.Feature.Thumbnail.Core)

## 1. 概要

サムネイル一覧（折り返し配置・選択・可視範囲）の **UI 非依存の計算層** です。
UI フレームワークごとに「列数・行の高さ・下端揃え・当たり判定」を書き分けると、UI によって並びと操作がずれます。
本層はその規則を 1 か所に置きます。

| 何 | 型 |
| --- | --- |
| 折り返し・行の高さ・下端揃え・項目の矩形 | `ThumbnailLayout` / `ThumbnailLayoutResult` |
| 一覧の設定（余白・名前の行の高さ・影の余地） | `ThumbnailOptions` |
| 単一選択の状態 | `ThumbnailSelection` |
| 見えている項目の範囲 | `ThumbnailRange` |

**色・フォント・描画 API は持ちません**（描画にだけ必要な設定は提示層、描画 API の呼び出しは UI 層）。

## 2. 使い方

```csharp
var layout = ThumbnailLayout.Resolve(availableWidth: 800, imageWidth: 96, imageHeights);
var index = layout.IndexAt(pointerPosition);
var visible = layout.VisibleRange(scrollOffset, viewportHeight);
```

- 項目の外枠 = 画像 + 影の余地（`SlotInset`） + 名前の行。 外枠の幅で折り返します。
- 行の高さは **その行の最大アイテム高さ** で決め、項目は行内で **下端揃え** にします（名前の位置が揃う）。
- 利用可能な幅が未確定（0 以下）の場合は 1 列で並べます（幅の確定後に呼び直す）。

## 3. 含まれるもの

`ThumbnailLayout` / `ThumbnailLayoutResult` / `ThumbnailOptions` / `ThumbnailSelection` / `ThumbnailRange`。

## 4. 依存

`EsUtil.Platform.Core` のみ（`EsRect` / `EsPoint` / `EsSize`）。 規則は [`.github/依存関係.md`](../../.github/依存関係.md)。

## 5. テスト

`tests/`（xUnit v3）。 折り返しの列数・下端揃え・当たり判定・可視範囲・縦横比の高さと、単一選択の規則（範囲外は解除・同値は通知しない）・選択のログを検証します。
手順は [`.github/検証手順.md`](../../.github/検証手順.md)。

## 6. ログ

選択の変更を記録します（既定は出力なし）。

| レベル | 出力例 |
| --- | --- |
| Debug | `[Thumbnail] Selection: -1 → 3`（選択の変更。 同じ添字の再設定では出力しない） |

`ThumbnailSelection.Log` へ `ILogger` を設定します（null は `NullLogger`）。 接頭辞は `LogPrefix` で設定します（既定 `[Thumbnail]`）。

## 7. ドキュメント

- 層の帰属 … [`../README.md`](../README.md)
- 依存の許可範囲 … [`.github/依存関係.md`](../../.github/依存関係.md)
- API リファレンス … [`docs/SPEC_API.md`](docs/SPEC_API.md)
- システム仕様書 … [`docs/SPEC_System.md`](docs/SPEC_System.md)

## 8. ライセンス

MIT（リポジトリの [LICENSE](../../LICENSE) を参照）。
