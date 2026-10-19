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

list.Clear();
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
- `Options` / `Appearance` を変えたら `Refresh()` を呼びます。

## 3. 含まれるもの

`ThumbnailControl` / `ThumbnailTile` / `ThumbnailActivatedEventArgs`。

## 4. 依存

`EsUtil.Feature.Thumbnail.Core` / `EsUtil.Feature.Thumbnail.Presenter` / `EsUtil.Platform.Core` / Avalonia。
規則は [`.github/依存関係.md`](../../.github/依存関係.md)。

## 5. テスト

自動テストは未整備です（ビルドの警告 0 と、利用側アプリの配布物の起動で確認しています）。
検証の手順は [`.github/検証手順.md`](../../.github/検証手順.md)。

## 6. ドキュメント

- 層の帰属 … [`../../EsUtil/README.md`](../../EsUtil/README.md)
- 配置の規則 … [`../EsUtil.Feature.Thumbnail.Core/README.md`](../../EsUtil/Feature.Thumbnail.Core/README.md)

## 7. ライセンス

MIT（リポジトリの [LICENSE](../../LICENSE) を参照）。
