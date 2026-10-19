# EsUtil.Ui.WinForms.Thumbnail

## 1. 概要

サムネイル一覧の **WinForms 版コントロール**（`ThumbnailControl`）です。
`ScrollableControl` を継承し、内容（背景・影・画像・名前）を自分で描きます。

| 何 | 型 |
| --- | --- |
| 一覧 | `ThumbnailControl` |
| 2 回押しの通知 | `ThumbnailActivatedEventArgs` |

## 2. 使い方

```csharp
var list = new ThumbnailControl { ImageWidth = iconSize, IsDarkTheme = isDark, Dock = DockStyle.Fill };
list.ItemActivated += (_, e) => Open((string)e.Tag!);

list.Clear();
foreach (var file in files)
{
    list.Add(placeholder, Path.GetFileName(file), toolTip: file, tag: file);
}

// 生成が終わった画像は添字で差し替える（渡した画像の所有権はコントロールへ移る）
list.SetImage(index, bitmap);
```

- 描画計画の層の順序どおりに、GDI+ で背景・影・画像・名前を描きます。
- 影は `ThumbnailShadowPlan.BuildDeviceBands` の 1 画素の帯を単色で塗ります（GDI+ のぼかしの端の扱いに依存しない）。
- スクロールは `AutoScroll` に任せ、内容の寸法はコアの配置が決めます。

## 3. 含まれるもの

`ThumbnailControl` / `ThumbnailActivatedEventArgs`。

## 4. 依存

`EsUtil.Feature.Thumbnail.Core` / `EsUtil.Feature.Thumbnail.Presenter` / `EsUtil.Platform.Core`。
規則は [`.github/依存関係.md`](../../.github/依存関係.md)。

## 5. テスト

自動テストは未整備です（ビルドの警告 0 と、利用側アプリの配布物の起動で確認しています）。

## 6. ドキュメント

- 層の帰属 … [`../../EsUtil/README.md`](../../EsUtil/README.md)

## 7. ライセンス

MIT（リポジトリの [LICENSE](../../LICENSE) を参照）。
