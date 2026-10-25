# EsUtil.Ui.WinUI3.Thumbnail

![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinUI3.Thumbnail.svg)
![Release](https://github.com/tomokuni/EsUtil/actions/workflows/release.yml/badge.svg)
![Build](https://github.com/tomokuni/EsUtil/actions/workflows/build.yml/badge.svg?branch=main)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%20(2004)%2B-0078D6)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![AOT](https://img.shields.io/badge/AOT-Ready-00695C)
![WinUI3](https://img.shields.io/badge/UI-WinUI%203-0078D4)
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

サムネイル一覧の **WinUI 3 版コントロール**（`ThumbnailControl`）です。
スクロール・ホバー・選択・2 回押しを受け付け、配置と見た目はコアと提示層へ委ねます。

| 何 | 型 |
| --- | --- |
| 一覧（スクロールを含む） | `ThumbnailControl`（`Grid` を継承し、内側にスクロールを持つ） |
| 項目（背景・影・画像・名前） | `ThumbnailTile`（内部型。 ホストは `Add` / `SetImage` / `SetCaption` を使う） |
| 2 回押しの通知 | `ThumbnailActivatedEventArgs` |

**WinUI 3 の標準パネルには任意の大きさの子を折り返すパネルが無い**ため、コアが返す矩形を
`Canvas` へ絶対配置します。

---

## 2. 機能一覧

- 折り返し配置（幅に応じて列数が変わる）とスクロール（`ScrollChanged` で通知）
- 項目の追加（`Add`）・画像の差し替え（`SetImage`）・名前の変更（`SetCaption`）・全消去（`ClearItems`）
- ホバー帯・選択・現在項目の強調（`IsDarkTheme` で明暗を切り替える）
- 1 回押しの選択（`SelectionChanged`）と 2 回押しの通知（`ItemActivated`）
- 可視範囲（`VisibleRange`）と項目の矩形（`SlotBounds`）の公開（見えている項目だけを先に作れる）
- 末尾へのスクロール（`ScrollToIndex`）
- 画像の幅（`ImageWidth`）・配置（`Options`）・外観（`Appearance`）の変更
- 描画計画の差し替え（`DrawPlanResolver`）
- `ILogger` によるログ（`Log` / `LogPrefix`）

---

## 3. システム要件

| 項目 | 内容 |
| --- | --- |
| 対象フレームワーク | `net10.0-windows10.0.22621.0`（Windows App SDK 1.x） |
| 対応 OS | Windows 10 バージョン 2004（ビルド 19041）以降 |
| 言語 | C# 14（.NET 10） |
| Native AOT | 対応（`dotnet publish -p:PublishAot=true -r win-x64` で発行したサンプルが起動することを実測） |

---

## 4. インストール

```xml
<ProjectReference Include="..\EsUtil.Ui.WinUI3\Thumbnail\src\EsUtil.Ui.WinUI3.Thumbnail.csproj" />
```

必要な名前空間:

```csharp
using EsUtil.Feature.Thumbnail;   // コアと提示層の型（Options・Appearance・DrawPlan 等）
using EsUtil.Ui.WinUI3.Thumbnail;
```

---

## 5. クイックスタート

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

- 影は計画の 4 辺の停止点を `LinearGradientBrush`、4 隅の停止点を `RadialGradientBrush` へ写した
  `Rectangle` 8 個で塗ります（1 画素の帯を要素にすると 1 件あたり数百個になり、一覧の初回描画が
  待たされます。 濃さの変化は画像の位置・大きさに依存しないため、ブラシは全項目で共有します）。
- 画像は `EsReadOnlyBitmap` を非所有ビューで描画します（ピクセルを複製しない）。

---

## 6. 含まれるもの

`ThumbnailControl` / `ThumbnailActivatedEventArgs`（項目の要素 `ThumbnailTile` と Skia 相互変換は内部型）。

---

## 7. 依存

`EsUtil.Feature.Thumbnail.Core` / `EsUtil.Feature.Thumbnail.Presenter` / `EsUtil.Platform.Core` / Windows App SDK /
SkiaSharp.Views.WinUI。 規則は [`.github/依存関係.md`](../../.github/依存関係.md)。

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
dotnet run --project EsUtil.Ui.WinUI3/Thumbnail/sample/EsUtil.Ui.WinUI3.Thumbnail.Sample.csproj
```

---

## 12. ドキュメント

- 層の帰属 … [`../../EsUtil/README.md`](../../EsUtil/README.md)
- API リファレンス … [`docs/SPEC_API.md`](docs/SPEC_API.md)
- システム仕様書 … [`docs/SPEC_System.md`](docs/SPEC_System.md)

---

## 13. プロジェクト構成

```text
EsUtil.Ui.WinUI3/Thumbnail/
├── src/                                          # UI 層（WinUI 3 固有の表示と入力）
│   ├── EsUtil.Ui.WinUI3.Thumbnail.csproj          # 独立プロジェクト（Presenter + Platform.Core を参照）
│   ├── ThumbnailControl.cs                        # 公開 API（Grid 派生）と描画計画の解決
│   ├── ThumbnailTile.cs                           # 項目の要素（内部型）
│   ├── Internal/                                  # 描画層（ThumbnailVisualLayer）と画像のキャッシュ
│   └── ThumbnailLogMessage.cs / ThumbnailActivatedEventArgs.cs
├── docs/                                          # SPEC_API.md / SPEC_System.md
├── sample/                                        # サンプルアプリ（sample/README.md 参照）
└── tests/                                         # 単体テスト
```

---

## 14. テスト

`tests/`（xUnit v3）。 ログ定義（画像と項目数）を検証します（コントロールは XAML の初期化を必要とするため、配布物の起動で確認します）。

---

## 15. ライセンス

[MIT License](LICENSE)
