# EsUtil.Ui.WinForms.Thumbnail

![Release](https://img.shields.io/nuget/v/EsUtil.Ui.WinForms.Thumbnail.svg)
![Build](https://github.com/tomokuni/EsUtil/actions/workflows/build.yml/badge.svg?branch=main)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%2B-0078D6)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![WinForms](https://img.shields.io/badge/UI-WinForms-512BD4)
![License](https://img.shields.io/badge/License-MIT-3DA639)

## 目次 <!-- omit from toc -->

- [1. 概要](#1-概要)
- [2. 機能一覧](#2-機能一覧)
- [3. システム要件](#3-システム要件)
- [4. インストール](#4-インストール)
- [5. クイックスタート](#5-クイックスタート)
- [6. 含まれるもの](#6-含まれるもの)
- [7. 依存](#7-依存)
- [8. カスタマイズ](#8-カスタマイズ)
- [9. ログ](#9-ログ)
- [10. 未対応](#10-未対応)
- [11. サンプルアプリケーション](#11-サンプルアプリケーション)
- [12. ドキュメント](#12-ドキュメント)
- [13. プロジェクト構成](#13-プロジェクト構成)
- [14. テスト](#14-テスト)
- [15. ライセンス](#15-ライセンス)

---

## 1. 概要

サムネイル一覧の **WinForms 版コントロール**（`ThumbnailControl`）です。
`ScrollableControl` を継承し、内容（背景・影・画像・名前）を自分で描きます。

| 何 | 型 |
| --- | --- |
| 一覧 | `ThumbnailControl` |
| 2 回押しの通知 | `ThumbnailActivatedEventArgs` |

---

## 2. 機能一覧

- 折り返し配置（幅に応じて列数が変わる）とスクロール
- 項目の追加（`Add`）・画像の差し替え（`SetImage`）・名前の変更（`SetCaption`）・全消去（`ClearItems`）
- ホバー帯・選択・現在項目の強調（`IsDarkTheme` で明暗を切り替える）
- 1 回押しの選択（`SelectionChanged`）と 2 回押しの通知（`ItemActivated`）
- 可視範囲（`VisibleRange`）と項目の矩形（`ItemBounds`）の公開（見えている項目だけを先に作れる）
- 末尾へのスクロール（`ScrollToIndex`）と配置のやり直し（`RefreshLayout`）
- 画像の幅（`ImageWidth`）・配置（`Options`）・外観（`Appearance`）の変更
- 描画計画の差し替え（`DrawPlanResolver`）
- `ILogger` によるログ（`Log` / `LogPrefix`）

---

## 3. システム要件

| 項目 | 内容 |
| --- | --- |
| 対象フレームワーク | `net10.0-windows10.0.17763.0`（`UseWindowsForms`。 Windows 専用） |
| Native AOT | 非対応（Windows Forms 自体がトリミング/AOT に対応しない。 `PublishAot` は `NETSDK1175` で失敗する） |
| 言語 | C# 14（.NET 10） |

---

## 4. インストール

```xml
<ProjectReference Include="..\EsUtil.Ui.WinForms\Thumbnail\src\EsUtil.Ui.WinForms.Thumbnail.csproj" />
```

必要な名前空間:

```csharp
using EsUtil.Feature.Thumbnail;   // コアと提示層の型（Options・Appearance・DrawPlan 等）
using EsUtil.Ui.WinForms.Thumbnail;
```

---

## 5. クイックスタート

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

---

## 6. 含まれるもの

`ThumbnailControl` / `ThumbnailActivatedEventArgs`。

---

## 7. 依存

`EsUtil.Feature.Thumbnail.Core` / `EsUtil.Feature.Thumbnail.Presenter` / `EsUtil.Platform.Core`。
規則は [`.github/依存関係.md`](../../.github/依存関係.md)。

---

## 8. カスタマイズ

差し替えの入口は 3 つです（**派生は不要**。 コントロールのプロパティへ設定します）。

| 差し替える対象 | 差し替え方 |
| --- | --- |
| 配置（列の幅・余白・名前の行） | `Options`（`ThumbnailOptions`）を設定する |
| 色・名前の体裁・表示可否・影 | `Appearance`（`ThumbnailAppearance`）を設定する（影の算法は `Appearance.Shadow` へ `IThumbnailShadowPlan` を渡して差し替える） |
| 1 件分の描画（矩形・層の順序・背景色・影・名前） | `DrawPlanResolver`（`Func<ThumbnailDrawPlan, ThumbnailDrawPlan>`）を設定する（計画の値の置き換え） |

描画計画（`ThumbnailDrawPlan`）と外観（`ThumbnailAppearance`）は `EsUtil.Feature.Thumbnail.Presenter` が単一所有し、4 つの UI 版で共通です（仕様は [`docs/SPEC_API.md`](docs/SPEC_API.md)）。

---

## 9. ログ

画像と項目数の変更を記録します（既定は出力なし）。

| レベル | 出力例 |
| --- | --- |
| Debug | `[Thumbnail] Setting: Image[0] null → 96x64` / `[Thumbnail] Setting: ItemCount 0 → 12` |

`Log` へ `ILogger` を設定します（null は `NullLogger`）。 接頭辞は `LogPrefix` で設定し、提示層・コアへ伝搬します（既定 `[Thumbnail]`）。 設定・外観の変更は提示層、選択の変更はコアが記録します。

---

## 10. 未対応

本コントロールが対象外とする操作・機能は次のとおりです（制限の詳細は [`docs/SPEC_API.md`](docs/SPEC_API.md) を参照）。

- **複数選択**: 持たない（単一選択だけ）。
- **項目の並べ替え（ドラッグ）**: 持たない（追加した順に並ぶ）。
- **項目の要素の差し替え**: 内部型（`ThumbnailTile`）は公開しない（`Add` / `SetImage` / `SetCaption` を使う）。

---

## 11. サンプルアプリケーション

動作確認用のサンプル アプリケーションを同梱しています。 起動方法と確認項目は [sample/README.md](sample/README.md) を参照してください。

```powershell
dotnet run --project EsUtil.Ui.WinForms/Thumbnail/sample/EsUtil.Ui.WinForms.Thumbnail.Sample.csproj
```

---

## 12. ドキュメント

- 層の帰属 … [`../../EsUtil/README.md`](../../EsUtil/README.md)
- API リファレンス … [`docs/SPEC_API.md`](docs/SPEC_API.md)
- システム仕様書 … [`docs/SPEC_System.md`](docs/SPEC_System.md)

---

## 13. プロジェクト構成

```text
EsUtil.Ui.WinForms/Thumbnail/
├── src/                                          # UI 層（GDI+ による表示と入力）
│   ├── EsUtil.Ui.WinForms.Thumbnail.csproj        # 独立プロジェクト（Presenter + Platform.Core を参照）
│   ├── ThumbnailControl.cs                        # 公開 API（ScrollableControl 派生）と描画計画の解決
│   └── ThumbnailLogMessage.cs / ThumbnailActivatedEventArgs.cs
├── docs/                                          # SPEC_API.md / SPEC_System.md
├── sample/                                        # サンプルアプリ（sample/README.md 参照）
└── tests/                                         # 単体テスト（GDI+ の画素を検査）
```

---

## 14. テスト

`tests/`（xUnit v3）。 項目の追加・選択・クリア・範囲外の無視・レイアウト後の項目の矩形・ログ（画像と項目数）・描画（`Bitmap` 上の画素）を検証します。

---

## 15. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
