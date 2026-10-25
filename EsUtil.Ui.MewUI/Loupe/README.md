# EsUtil.Ui.MewUI.Loupe

![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.MewUI.Loupe.svg)
![Release](https://github.com/tomokuni/EsUtil/actions/workflows/release.yml/badge.svg)
![Build](https://github.com/tomokuni/EsUtil/actions/workflows/build.yml/badge.svg?branch=main)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%2B-0078D6)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![AOT](https://img.shields.io/badge/AOT-Ready-00695C)
![MewUI](https://img.shields.io/badge/UI-MewUI-00BFFF)
![License](https://img.shields.io/badge/License-MIT-3DA639)

対になる Avalonia UI 実装と同じ **UI 非依存コア（`EsUtil.Feature.Loupe.Core`）と提示層（`EsUtil.Feature.Loupe.Presenter`）** を使い、描画（`SKCanvas`）と入力の受付のみを **MewUI** で実装したルーペ（拡大鏡）コントロールです。

## 目次 <!-- omit from toc -->

- [1. 概要](#1-概要)
- [2. 機能一覧](#2-機能一覧)
- [3. システム要件](#3-システム要件)
- [4. インストール](#4-インストール)
- [5. クイックスタート](#5-クイックスタート)
- [6. カスタマイズ](#6-カスタマイズ)
- [7. ログ](#7-ログ)
- [8. 未対応](#8-未対応)
- [9. サンプルアプリケーション](#9-サンプルアプリケーション)
- [10. ドキュメント](#10-ドキュメント)
- [11. プロジェクト構成](#11-プロジェクト構成)
- [12. テスト](#12-テスト)
- [13. ライセンス](#13-ライセンス)

---

## 1. 概要

ルーペのロジック（数値の安全域補正・切り出し領域の計算・表示レイアウト）は `EsUtil.Feature.Loupe.Core` が単一所有し、描画計画（層の順序・装飾の矩形・ラベルの文言と配置）と外観（色・枠線の太さ・表示可否）は UI 非依存の提示層（`EsUtil.Feature.Loupe.Presenter` の `LoupePresenter` / `LoupeAppearance`）が単一所有します。
本プロジェクトはその計画を `IGraphicsContext` へ **フレームワーク標準の描画で写すだけ** の層です。
そのため Avalonia 版と**同じ設定・同じ見た目**になり、同じ外観の設定を両 UI で共用できます。 層構成（Core / Presenter / UI）の全体像は [`EsUtil/README.md`](../../EsUtil/README.md) を参照してください。

---

## 2. 機能一覧

| 項目 | 内容 |
| --- | --- |
| 基底クラス | `Aprillz.MewUI.Controls.Panel`（子の `Control` が `OnRender` で描画する） |
| 中間バッファ | **生成しない**。 画面のキャンバスへ直接描画する（Avalonia 版は `RenderTargetBitmap` を経由する） |
| 描画サイズ | `Options.RequestedSize` × `ClientToPixelScaling`（物理ピクセル）。 表示サイズはキャンバスの大きさから導くため常に一致する（領域に合わせた縮小はしない） |
| 拡大方式 | 最近傍（画素をそのまま拡大表示する） |
| DPI 追従 | `OnDpiChanged` で表示スケーリング係数を DPI から換算した値へ自動更新する（換算規則は `DpiScaler.ToScaling` が単一所有。 ホストの設定は不要） |
| 連続再描画 | **無効**。 プロパティ変更時にのみ再描画する（静止画のため） |
| 位置決め | **本コントロールは位置を持たない**。 ホストが `ResolveLayout`（コアの計算を委譲した公開メソッド）の結果で配置する |
| 見た目のカスタマイズ | 色・太さ・表示可否は `Appearance`、矩形の並びで表せる装飾とラベルの文言・位置は `DrawPlanResolver`、矩形で表せない描画は `Appearance` の `Show*` を false にして `Rendered` のキャンバスへ自前で描く（`DrawPlan` で計画どおりの描画もできる。 派生は不要 ） |

---

## 3. システム要件

| 項目 | 内容 |
| --- | --- |
| 対象フレームワーク | `net10.0` |
| UI フレームワーク | MewUI 0.21.1（`MewUIVersion` プロパティで固定） |
| 言語 | C# 14（.NET 10） |
| Native AOT | 対応（`dotnet publish -p:PublishAot=true -r win-x64` で発行したサンプルが起動することを実測） |

---

## 4. インストール

```xml
<ProjectReference Include="..\EsUtil.Ui.MewUI\Loupe\src\EsUtil.Ui.MewUI.Loupe.csproj" />
```

パッケージは `Aprillz.MewUI.Core` を参照します（バージョンは `MewUIVersion` プロパティで 0.21.1 に固定）。

---

## 5. クイックスタート

```csharp
using EsUtil.Feature.Loupe;                    // LoupeOptions（コア）と LoupeAppearance（描画にだけ必要な外観）
using EsUtil.Ui.MewUI.Loupe;
using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

var loupe = new LoupeControl
{
    Source = readOnlyImage,                  // 拡大元の 100% 画像（コアの EsReadOnlyBitmap）
    CenterPoint = new EsPoint(320, 240),     // 拡大中心（元画像ピクセル座標）
    // 寸法（基本サイズ・拡大率）はコアの Options（with 式で部分変更する）
    Options = LoupeOptions.Default with
    {
        RequestedSize = 160,                 // 一辺のサイズ（DIP）
        ZoomLevel = 4,                       // 拡大率
    },
    // 外観（色・枠線の太さ・表示可否）は提示層の Appearance で指定する
    Appearance = LoupeAppearance.Default with
    {
        BorderThickness = 2,                 // 枠線の太さ（DIP）
        CrossColor = DrawingColor.FromArgb(102, 255, 0, 0),
        PasteboardColor = DrawingColor.FromArgb(255, 53, 53, 53),
        BorderColor = DrawingColor.FromArgb(255, 0, 0, 0),
    },
};
```

`Source` と `CenterPoint` を更新すると自動的に再描画されます（`InvalidateVisual`）。 表示位置はホストが決めます。

```csharp
// 位置決めはコアのファサードが計算する（中心 + オフセット、親領域内へ補正）
var layout = loupe.ResolveLayout(parentSize);
if (layout.Position is { } position)
{
    loupe.Arrange(new Rect(x + position.X, y + position.Y, layout.ClientSize, layout.ClientSize));
}
```

> **注意**: `ResolveLayout` は本コントロールの公開メソッドであり、計算はコアのファサード（`EsUtil.Feature.Loupe.Core` の `LoupeCore`、`public` 型）へ委譲しています（汎用コアとして公開されており、`InternalsVisibleTo` は不要です）。

---

## 6. カスタマイズ

差し替えの入口は 3 つです（**派生は不要**。 コントロールのプロパティへ設定します）。

| 差し替える対象 | 差し替え方 |
| --- | --- |
| 色・枠線の太さ・表示可否・ラベルの体裁 | `Appearance`（`LoupeAppearance`）を設定する |
| 矩形で並べられる装飾（十字線・枠線）とラベルの文言・位置 | `DrawPlanResolver`（`Func<LoupeDrawPlan, LoupeDrawPlan>`）を設定する（計画の値の置き換え） |
| 矩形で表せない描画（円・斜線等） | `Appearance` の `Show*` を false にして `Rendered` で受け取る描画面へ自前で描く（`DrawPlan` を持つ UI 版では計画どおりの描画も呼べる） |

描画計画（`LoupeDrawPlan`）と外観（`LoupeAppearance`）は `EsUtil.Feature.Loupe.Presenter` が単一所有し、4 つの UI 版で共通です。 UI が持つのは UI フレームワーク固有の描画 API の呼び出しだけです（仕様は [`docs/SPEC_API.md`](docs/SPEC_API.md)）。

---

## 7. ログ

`Log` プロパティへ `ILogger` を設定すると、設定変更と描画の例外を出力します（既定は出力なし）。 書式は Avalonia 版と同一です。

| レベル | 出力例 |
| --- | --- |
| Debug | `[Loupe] Setting: ZoomLevel 4 → 8`（コアの設定。 Source は `[Loupe] Setting: Source ...`、外観は `[Loupe] Setting: ...`）/ `CenterPoint` / `RequestedSize` / `ClientToPixelScaling` / `ZoomLevel`。 変化判定（同一値・同一インスタンスなら出力しない）は出力する主体のログ定義（Source は本プロジェクトの `LoupeLogMessage`、外観は提示層の `LoupeLogMessage`、他はコアの `LoupeLogMessage`）が単一所有し、UI 層は同値判定を持たない。 設定の変更は変化した項目だけを出力する） |
| Error | `[Loupe] Update exception`（描画の例外。 アプリは継続する） |

Avalonia 版の Trace ログ（再描画パイプライン）は持ちません（MewUI 版は中間ビットマップを作らず同期描画するため）。

---

## 8. 未対応

本コントロールが対象外とする操作・機能は次のとおりです（制限の詳細は [`docs/SPEC_API.md`](docs/SPEC_API.md) を参照）。

- **拡大元の画像**: コアの画像（`EsReadOnlyBitmap`）だけを受け取る（UI フレームワークのビットマップ型は受け付けない）。
- **ポインター入力**: 拡大中心はホストが決める（入力の購読はホストが行う）。
- **印刷・画像の書き出し**: 持たない（`Rendered` で受け取る描画面へ描いた結果をホストが保存する）。

---

## 9. サンプルアプリケーション

動作確認用の独立サンプルアプリケーションを同梱しています（MewUI は XAML を持たないため、UI は C# のコードで組み立てます）。

```powershell
dotnet run --project EsUtil.Ui.MewUI/Loupe/sample/EsUtil.Ui.MewUI.Loupe.Sample.csproj
```

サンプルで確認できる内容:

- マウス追従による拡大表示（`Source` / `CenterPoint`）
- 画像端から外れた部分の画像外の色（無地）
- 拡大率・サイズ・描画スケール（DPI）の変更
- 十字線・枠線・座標ラベルの変更（「なし」で非表示）
- `Source` の解除（未設定時の下地）と再設定
- `Log`（設定変更ログの出力先 ILogger 設定例）
- 外観と描画計画の差し替え（見本。 `Appearance` と `DrawPlanResolver`）

詳細は [sample/README.md](sample/README.md) を参照してください。

---

## 10. ドキュメント

- [共有する UI 非依存ロジック](../../EsUtil/Feature.Loupe.Core/README.md)
- [共有する UI 非依存の描画層](../../EsUtil/Feature.Loupe.Presenter/README.md)
- [対になる Avalonia 版ルーペ](../../EsUtil.Ui.AvaloniaUI/Loupe/README.md)
- [サンプルアプリケーション](sample/README.md)

---

## 11. プロジェクト構成

```text
EsUtil.Ui.MewUI/Loupe/
├── README.md                # 本ファイル
├── docs/                    # SPEC_API.md（API リファレンス）/ SPEC_System.md（システム仕様書）
├── sample/                  # 独立サンプルアプリ（sample/README.md 参照）
├── src/
│   ├── EsUtil.Ui.MewUI.Loupe.csproj   # net10.0 / MewUI 0.21.1 固定
│   ├── LoupeControl.cs             # Panel 派生のコントロール（描画は LoupePainter へ委譲する）
│   ├── LoupeRenderedEventArgs.cs   # 描画完了の通知（キャンバスと表示情報）
│   ├── LoupePainter.cs             # 描画計画（LoupeDrawPlan）を IGraphicsContext へ写す描画実装
└── tests/
    ├── EsUtil.Ui.MewUI.Loupe.Tests.csproj
    ├── TestHelper.cs                   # テスト共通（描画先・画像生成）
    ├── RecordingLogger.cs              # ログ収集（テスト用）
    ├── LoupeControl.Tests.cs           # プロパティ補正・レイアウト
    └── LoupeRenderedEventArgs.Tests.cs # 描画完了通知の引数
```

---

## 12. テスト

```powershell
dotnet build EsUtil.Ui.MewUI/Loupe/src/EsUtil.Ui.MewUI.Loupe.csproj
dotnet test EsUtil.Ui.MewUI/Loupe/tests/EsUtil.Ui.MewUI.Loupe.Tests.csproj
```

本プロジェクトは `GenerateDocumentationFile` を有効にし、**存在しないメンバーへの `<see cref>` をビルド警告（CS1574）で検出**します（XML は検証にのみ使い、配布物へは出しません）。

---

## 13. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
