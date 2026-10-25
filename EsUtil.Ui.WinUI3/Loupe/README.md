# EsUtil.Ui.WinUI3.Loupe

![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinUI3.Loupe.svg)
![Release](https://github.com/tomokuni/EsUtil/actions/workflows/release.yml/badge.svg)
![Build](https://github.com/tomokuni/EsUtil/actions/workflows/build.yml/badge.svg?branch=main)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%2B-0078D6)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![AOT](https://img.shields.io/badge/AOT-Ready-00695C)
![WinUI3](https://img.shields.io/badge/UI-WinUI%203-0078D4)
![License](https://img.shields.io/badge/License-MIT-3DA639)

対になる他 UI 実装と同じ **UI 非依存コア（`EsUtil.Feature.Loupe.Core`）と提示層（`EsUtil.Feature.Loupe.Presenter`）** を使い、描画（XAML の要素）のみを **WinUI 3（Windows App SDK）** で実装したルーペ（拡大鏡）コントロールです。

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
本プロジェクトはその計画を **XAML の要素（`Rectangle` と `WriteableBitmap` の画像）へ写すだけ** の層です。
そのため他の UI 版と**同じ設定・同じ見た目**になり、同じ外観の設定を複数の UI で共用できます。 層構成（Core / Presenter / UI）の全体像は [`EsUtil/README.md`](../../EsUtil/README.md) を参照してください。

---

## 2. 機能一覧

| 項目 | 内容 |
| --- | --- |
| 基底クラス | `Microsoft.UI.Xaml.Controls.Canvas`（図形は `Rectangle`、拡大画像は `Image` + `WriteableBitmap`） |
| 中間バッファ | **生成しない**。 画面のキャンバスへ直接描画する |
| 描画サイズ | `Options.RequestedSize` × `ClientToPixelScaling`（物理ピクセル）。 表示サイズは描画面の大きさから導くため常に一致する（領域に合わせた縮小はしない） |
| 拡大方式 | 最近傍（画素をそのまま拡大表示する） |
| 中心座標ラベル | **要素として重ねる**（`Border` + `TextBlock`）。 実寸の測定と表示に同じ XAML のテキスト機能を使うため、背景と文字の大きさが常に一致する |
| DPI 追従 | `XamlRoot` の変更通知で表示倍率を取り込み、あわせて**描画のたびに描画面（物理ピクセル）と割り当て領域（DIP）の比へ合わせる**（ホストの設定は不要） |
| 連続再描画 | **無効**。 プロパティ変更時にのみ再描画する（静止画のため） |
| 位置決め | **本コントロールは位置を持たない**。 ホストが `ResolveLayout`（コアの計算を委譲した公開メソッド）の結果で配置する |
| プロパティ | **CLR プロパティ**（依存関係プロパティではない。 理由は [`docs/SPEC_System.md`](docs/SPEC_System.md) の設計判断を参照） |
| 見た目のカスタマイズ | 色・太さ・表示可否は `Appearance`、矩形の並びで表せる装飾とラベルの文言・位置は `DrawPlanResolver`、矩形で表せない描画は `Appearance` の `Show*` を false にして `Rendered` のキャンバスへ自前で描く（`DrawPlan` で計画どおりの描画もできる。 派生は不要） |

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
<ProjectReference Include="..\EsUtil.Ui.WinUI3\Loupe\src\EsUtil.Ui.WinUI3.Loupe.csproj" />
```

参照する側は **WinUI 3（Windows App SDK）** のプロジェクトにします（対象フレームワークは `net10.0-windows10.0.22621.0`、`UseWinUI` を有効にする）。
パッケージは `Microsoft.WindowsAppSDK` / `Microsoft.Windows.SDK.BuildTools` を参照します（バージョンの範囲は本体のプロジェクト ファイルが単一所有します）。

---

## 5. クイックスタート

```csharp
using EsUtil.Feature.Loupe;                    // LoupeOptions（コア）と LoupeAppearance（描画にだけ必要な外観）
using EsUtil.Ui.WinUI3.Loupe;
using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

// XAML で <loupe:LoupeControl x:Name="Loupe" /> を置き、コードビハインドから設定する
Loupe.Source = readOnlyImage;                  // 拡大元の 100% 画像（コアの EsReadOnlyBitmap）
Loupe.CenterPoint = new EsPoint(320, 240);     // 拡大中心（元画像ピクセル座標）
// 寸法（基本サイズ・拡大率）はコアの Options（with 式で部分変更する）
Loupe.Options = LoupeOptions.Default with
{
    RequestedSize = 160,                       // 一辺のサイズ（DIP）
    ZoomLevel = 4,                             // 拡大率
};
// 外観（色・枠線の太さ・表示可否）は提示層の Appearance で指定する
Loupe.Appearance = LoupeAppearance.Default with
{
    BorderThickness = 2,                       // 枠線の太さ（DIP）
    CrossColor = DrawingColor.FromArgb(102, 255, 0, 0),
    PasteboardColor = DrawingColor.FromArgb(255, 53, 53, 53),
    BorderColor = DrawingColor.FromArgb(255, 0, 0, 0),
};
```

`Source` と `CenterPoint` を更新すると自動的に再描画されます。 表示位置はホストが決めます。

```csharp
// 位置決めはコアのファサードが計算する（中心 + オフセット、親領域内へ補正）
var layout = Loupe.ResolveLayout(new EsSize(host.ActualWidth, host.ActualHeight));
if (layout.Position is { } position)
{
    Loupe.Width = layout.ClientSize;
    Loupe.Height = layout.ClientSize;
    Canvas.SetLeft(Loupe, position.X);
    Canvas.SetTop(Loupe, position.Y);
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

`Log` プロパティへ `ILogger` を設定すると、設定変更と描画の例外を出力します（既定は出力なし）。

| レベル | 出力例 |
| --- | --- |
| Debug | `[Loupe] Setting: Source 800x600 → null`（拡大元画像。 本プロジェクトのログ定義） / `[Loupe] Setting: ZoomLevel 4 → 8`（コアの設定値。 `RequestedSize` / `ClientToPixelScaling` も同じ書式） / `[Loupe] Setting: ShowCrosshair True → False`（外観。 変化した項目だけを出力する） |
| Error | `[Loupe] Update exception`（描画の例外。 アプリは継続する） / `[Loupe] SetCoordTransform で例外が発生`（公開 API の例外。 記録して再送出する） |

変化判定（同一値・同一インスタンスなら出力しない）は出力する主体のログ定義が単一所有するため、ホストは同値判定を持ちません。

---

## 8. 未対応

本コントロールが対象外とする操作・機能は次のとおりです（制限の詳細は [`docs/SPEC_API.md`](docs/SPEC_API.md) を参照）。

- **拡大元の画像**: コアの画像（`EsReadOnlyBitmap`）だけを受け取る（UI フレームワークのビットマップ型は受け付けない）。
- **ポインター入力**: 拡大中心はホストが決める（入力の購読はホストが行う）。
- **印刷・画像の書き出し**: 持たない（`Rendered` で受け取る描画面へ描いた結果をホストが保存する）。

---

## 9. サンプルアプリケーション

動作確認用の独立サンプル アプリケーションを同梱しています（WinUI 3 のアンパッケージ アプリ）。

```powershell
dotnet run --project EsUtil.Ui.WinUI3/Loupe/sample/EsUtil.Ui.WinUI3.Loupe.Sample.csproj
```

サンプルで確認できる内容:

- マウス追従による拡大表示（`Source` / `CenterPoint`）
- 画像端から外れた部分の画像外の色（無地）
- 拡大率・サイズ・描画スケールの変更
- 十字線・枠線・座標ラベルの変更（「なし」で非表示）
- `Source` の解除（未設定時の下地）と再設定
- `Log`（設定変更ログの出力先 `ILogger` の設定例）
- 外観と描画計画の差し替え（見本。 `Appearance` と `DrawPlanResolver`）
- 選択矩形の破線オーバーレイ（`Rendered` へ追加描画する例）

詳細は [sample/README.md](sample/README.md) を参照してください。

---

## 10. ドキュメント

- [共有する UI 非依存ロジック](../../EsUtil/Feature.Loupe.Core/README.md)
- [共有する UI 非依存の描画層](../../EsUtil/Feature.Loupe.Presenter/README.md)
- [対になる MewUI 版ルーペ](../../EsUtil.Ui.MewUI/Loupe/README.md)
- [対になる Avalonia 版ルーペ](../../EsUtil.Ui.AvaloniaUI/Loupe/README.md)
- [API リファレンス](docs/SPEC_API.md)
- [システム仕様書](docs/SPEC_System.md)
- [サンプルアプリケーション](sample/README.md)

---

## 11. プロジェクト構成

```text
EsUtil.Ui.WinUI3/Loupe/
├── LICENSE                    # MIT（リポジトリ直下の LICENSE と同じ）
├── README.md                  # 本ファイル
├── docs/                      # SPEC_API.md（API リファレンス）/ SPEC_System.md（システム仕様書）
├── sample/                    # 独立サンプル アプリ（sample/README.md 参照）
├── src/
│   ├── EsUtil.Ui.WinUI3.Loupe.csproj  # net10.0-windows10.0.22621.0 / WinUI 3
│   ├── LoupeControl.cs             # Canvas 派生のコントロール（計画を要素と画像へ写す）
│   ├── LoupeRenderedEventArgs.cs   # 描画完了の通知（キャンバスと描画計画）
│   ├── LoupeLogMessage.cs          # 画像の差し替え・描画例外のログ定義
│   └── Internal/                   # 描画の補助（スケーリングの解決）
└── tests/
    ├── EsUtil.Ui.WinUI3.Loupe.Tests.csproj
    ├── TestHelper.cs                   # テスト共通（テスト画像の生成）
    ├── RecordingLogger.cs              # ログ収集（テスト用）
    ├── LogPrefixGroup.cs               # ログの接頭辞を設定するテストの直列化
    ├── LoupeShapePlan.Tests.cs         # 矩形の並び（形状の計画）の検証
    ├── LoupeLogMessage.Tests.cs        # ログの出力書式
    └── LoupeRenderedEventArgs.Tests.cs # 描画完了通知の引数
```

---

## 12. テスト

```powershell
dotnet build EsUtil.Ui.WinUI3/Loupe/src/EsUtil.Ui.WinUI3.Loupe.csproj
dotnet test EsUtil.Ui.WinUI3/Loupe/tests/EsUtil.Ui.WinUI3.Loupe.Tests.csproj
```

本プロジェクトは `GenerateDocumentationFile` を有効にし、**存在しないメンバーへの `<see cref>` をビルド警告（CS1574）で検出**します。

---

## 13. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
