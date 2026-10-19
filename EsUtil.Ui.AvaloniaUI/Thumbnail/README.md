# EsUtil.Ui.AvaloniaUI.Thumbnail

## 1. 概要

サムネイル一覧の **Avalonia UI 版コントロール**（`ThumbnailControl`）です。
スクロール・ホバー・選択・2 回押しを受け付け、配置と見た目はコアと提示層へ委ねます。

| 何 | 型 |
| --- | --- |
| 一覧 | `ThumbnailControl`（`ScrollViewer` を継承） |
| 項目（背景・影・画像・名前） | `ThumbnailTile` |
| 2 回押しの通知 | `ThumbnailActivatedEventArgs` |

## 2. 使い方

```csharp
var list = new ThumbnailControl { ImageWidth = iconSize, IsDarkTheme = isDark };
list.ItemActivated += (_, e) => Open((string)e.Tag!);

list.ClearItems();
foreach (var file in files)
{
    list.Add(placeholder, Path.GetFileName(file), toolTip: file, tag: file);
}

// 生成が終わった画像は添字で差し替える
list.SetImage(index, bitmap);

// 見えている項目を先に作る
var visible = list.VisibleRange;
var bounds = list.SlotBounds(index);
```

- 影は既定の計画（`ThumbnailShadowPlan`）だけ Avalonia の `BoxShadow` に任せます（そのぼかしが影の濃さの
  プロファイルの元である）。 `IThumbnailShadowPlan` を差し替えた場合は、計画が返す帯を自前で塗ります
  （`ThumbnailShadowBandsView`）。
- 入力は一覧が位置から判定します（子要素のヒット テストに依存しない）。
- `Options` / `Appearance` / `ImageWidth` は設定すると自動で反映されます（`IsDarkTheme` だけは `Refresh()` を呼びます）。

## 3. 含まれるもの

`ThumbnailControl` / `ThumbnailTile` / `ThumbnailActivatedEventArgs`。

## 4. 依存

`EsUtil.Feature.Thumbnail.Core` / `EsUtil.Feature.Thumbnail.Presenter` / `EsUtil.Platform.Core` / Avalonia。
規則は [`.github/依存関係.md`](../../.github/依存関係.md)。

## 5. テスト

`tests/`（xUnit v3 + Avalonia ヘッドレス）。 項目の追加・選択・クリア・範囲外の無視・計測後の項目の矩形・ログ（画像と項目数）を検証します。
検証の手順は [`.github/検証手順.md`](../../.github/検証手順.md)。

## 6. ログ

画像と項目数の変更を記録します（既定は出力なし）。

| レベル | 出力例 |
| --- | --- |
| Debug | `[Thumbnail] Setting: Image[0] null → 96x64` / `[Thumbnail] Setting: ItemCount 0 → 12` |

`Log` へ `ILogger` を設定します（null は `NullLogger`）。 接頭辞は `LogPrefix` で設定し、提示層・コアへ伝搬します（既定 `[Thumbnail]`）。 設定・外観の変更は提示層、選択の変更はコアが記録します。

## 7. ドキュメント

- 層の帰属 … [`../../EsUtil/README.md`](../../EsUtil/README.md)
- 配置の規則 … [`EsUtil.Feature.Thumbnail.Core`](../../EsUtil/Feature.Thumbnail.Core/README.md)
- API リファレンス … [`docs/SPEC_API.md`](docs/SPEC_API.md)
- システム仕様書 … [`docs/SPEC_System.md`](docs/SPEC_System.md)

## 8. ライセンス

MIT（リポジトリの [LICENSE](../../LICENSE) を参照）。
