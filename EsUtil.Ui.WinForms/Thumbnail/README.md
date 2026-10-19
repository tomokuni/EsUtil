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

list.ClearItems();
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

`tests/`（xUnit v3）。 項目の追加・選択・クリア・範囲外の無視・レイアウト後の項目の矩形・ログ（画像と項目数）を検証します。

## 6. ログ

画像と項目数の変更を記録します（既定は出力なし）。

| レベル | 出力例 |
| --- | --- |
| Debug | `[Thumbnail] Setting: Image[0] null → 96x64` / `[Thumbnail] Setting: ItemCount 0 → 12` |

`Log` へ `ILogger` を設定します（null は `NullLogger`）。 接頭辞は `LogPrefix` で設定し、提示層・コアへ伝搬します（既定 `[Thumbnail]`）。 設定・外観の変更は提示層、選択の変更はコアが記録します。

## 7. ドキュメント

- 層の帰属 … [`../../EsUtil/README.md`](../../EsUtil/README.md)
- API リファレンス … [`docs/SPEC_API.md`](docs/SPEC_API.md)
- システム仕様書 … [`docs/SPEC_System.md`](docs/SPEC_System.md)

## 8. ライセンス

MIT（リポジトリの [LICENSE](../../LICENSE) を参照）。
