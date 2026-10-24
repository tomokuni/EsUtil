# EsUtil.Ui.AvaloniaUI.Marquee

![Release](https://img.shields.io/nuget/v/EsUtil.Ui.AvaloniaUI.Marquee.svg)
![Build](https://github.com/tomokuni/EsUtil/actions/workflows/build.yml/badge.svg?branch=main)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%2B-0078D6)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![Avalonia](https://img.shields.io/badge/UI-Avalonia-8D46D6)
![License](https://img.shields.io/badge/License-MIT-3DA639)

> プロジェクト名: `EsUtil.Ui.AvaloniaUI.Marquee`（Avalonia UI 版）。 UI 非依存のロジックは `EsUtil.Feature.Marquee.Core`、MewUI 版は対になるコントロール（別プロジェクト）にあります。

## 目次 <!-- omit from toc -->

- [1. 概要](#1-概要)
- [2. 機能一覧](#2-機能一覧)
- [3. システム要件](#3-システム要件)
- [4. インストール](#4-インストール)
- [5. クイックスタート](#5-クイックスタート)
  - [5.1 AXAML](#51-axaml)
  - [5.2 Code-behind（設定・イベント購読・座標ポート設定）](#52-code-behind設定イベント購読座標ポート設定)
- [6. 座標系](#6-座標系)
- [7. 操作](#7-操作)
- [8. 他の UI 版との違い](#8-他の-ui-版との違い)
- [9. カスタマイズ](#9-カスタマイズ)
- [10. ログ](#10-ログ)
- [11. 未対応](#11-未対応)
- [12. サンプルアプリケーション](#12-サンプルアプリケーション)
- [13. ドキュメント](#13-ドキュメント)
- [14. プロジェクト構成](#14-プロジェクト構成)
  - [14.1 共有コア（`EsUtil.Feature.Marquee.Core`）と提示層（`EsUtil.Feature.Marquee.Presenter`）](#141-共有コアesutilfeaturemarqueecoreと提示層esutilfeaturemarqueepresenter)
  - [14.2 装飾（提示層が単一所有する）](#142-装飾提示層が単一所有する)
- [15. テスト](#15-テスト)
- [16. ライセンス](#16-ライセンス)

---

## 1. 概要

Avalonia UI 用のリサイズ可能な領域選択コントロールです。 マウスドラッグ・キーボード微調整（nudge）・描画をコントロール内で完結します。

---

## 2. 機能一覧

- マウスドラッグによる領域選択
- 選択矩形のドラッグ移動
- 8方向ドラッグハンドルによるリサイズ
- 矢印キーによる1Pixel単位のキーボード微調整（nudge）
- 対象座標系で正確な nudge（矢印キー1回 = 対象1Pixel）
- ドラッグハンドル・始点マーカーの自己描画
- **選択矩形の外側に描く装飾の描画**: 暗転マスク・外枠・ハイライト・内側の塗り（**表示可否と太さと色**はすべて外観（`EsUtil.Feature.Marquee.Presenter` の `MarqueeAppearance`）が単一所有し、装飾の矩形の導出と描画順は提示層（同層の `MarqueePresenter`）が単一所有する）。 **選択枠線はこの外枠が兼ねる**（別々に描くと線幅が二重になるため）
- **ハンドル表示の切替**: `Appearance.ShowHandles = false` でハンドルを描かず枠線と外側の装飾だけを表示する（表示可否は描画にだけ必要な設定のため外観が単一所有する）
- **外観の設定（`Appearance`）**: 色・フォントサイズ・枠線の太さ・表示可否は**描画にだけ必要な設定**のためコアには含めず、提示層（`EsUtil.Feature.Marquee.Presenter` の `MarqueeAppearance`）が単一所有する。 `Appearance` プロパティで指定し（既定は `MarqueeAppearance.Default`）、部分変更は `Appearance = MarqueeAppearance.Default with { ... }` で行う（指定しない項目は既定値を保つ）。 **描くかどうかは色のアルファではなく外観の明示フラグ（`ShowOverlay` / `ShowOuterBorder` / `ShowInnerFill` / `ShowInnerBorder` / `ShowFullOverlay`）が決める**
- カーソル座標ラベルの表示（任意設定・`(x, y)` 形式）
- **文字は UI 層が要素として配置する**: サイズラベルとカーソル座標ラベルの**文言・基準点は提示層（`EsUtil.Feature.Marquee.Presenter` の `MarqueePresenter`（`LabelTexts` partial））が、色とフォントサイズは外観（`MarqueeAppearance`）が、実寸から配置矩形を求める写像は同層の `MarqueePresenter`（`LabelPlan` partial） が単一所有**し、UI 層が `Border` + `TextBlock` を子要素として置く（子要素は親の描画より後に描かれるため常に最上位）。 サイズラベルの基準点は注目矩形の左上から**枠線の幅 + 1 DIP 内側**（枠線と背景の重なりを無くす）。 実寸測定は自 UI の API（`FormattedText`）で行う。 操作・情報ラベルはアプリ固有の付加情報のため、ホスト（上位のアプリの合成コントロール）が `Rendered` のハンドラで自前で描く（外観はホストのラベル設定が単一所有する）
- ダブルクリックの確定/キャンセル判定（矩形内 = 確定 / 矩形外 = キャンセル）
- Avalonia Binding 対応の StyledProperty
- 対象↔DIP 座標変換ポート（`ICoordTransform` 直接注入。 型は `EsUtil.Platform.Core` 提供）
- **閉区間描画（始点・終点を含む）**: ハンドルと外側の装飾（外枠）は終点（Right/Bottom）に +1 して DIP 変換（キャプチャの閉区間 +1 と一致。 規則はコアの描画入力（`MarqueeRenderRequest.ToDipRect`）が単一所有）
- **ホイール操作は本コントロールが扱わない（ホストの責務）**: ホイール（ズーム・パン）はコントロール化を前提とした処理であり、**ホストが本コントロールの `PointerWheelChanged` を購読してビューポート（上位のズーム コントロール等）へ転送**します（本コントロールは素通しし、`e.Handled` を立てません）。 表示の変化への追従もホストが `InvalidateRender()` を呼びます
- **薄い UI 層**: 入力の解釈（nudge セッション・確定/キャンセル・スナップ・クリック検出・最小サイズ判定）はコアの公開ファサード（`MarqueeCore`）が担います。 本コントロールは「入力の受付」「効果の適用」「通知の転送」「描画計画の写し」のみを行います
- **見た目のカスタマイズ（2 つの入口）**: 色・太さ・表示可否は `Appearance`、矩形の並びで表せる装飾とラベルの文言・位置は `DrawPlanResolver`（`Func<MarqueeDrawPlan, MarqueeDrawPlan>`）で差し替えます。 矩形で表せない描画（円・斜線等）は `Appearance` の `Show*` を false にして `Rendered` の描画先へ自前で描き、必要なら `DrawPlan` で計画どおりの描画を行います（派生は不要。 例はサンプルの `sample/Presentation/GuideMarqueePlan`）

---

## 3. システム要件

- .NET 10
- Avalonia 12.1 以降
- C# 14
- **Native AOT**: 対応（`dotnet publish -p:PublishAot=true -r win-x64` で発行したサンプルが起動することを実測）

---

## 4. インストール

他 UI 版と同じく、NuGet パッケージ（`EsUtil.Ui.AvaloniaUI.Marquee`）またはプロジェクト参照で利用します。

```xml
<ProjectReference Include="..\EsUtil.Ui.AvaloniaUI\Marquee\src\EsUtil.Ui.AvaloniaUI.Marquee.csproj" />
```

必要な名前空間:

```csharp
using Avalonia;
using Avalonia.Media;                        // Colors
using EsUtil.Ui.AvaloniaUI.Marquee;
using EsUtil.Feature.Marquee;          // MarqueeCore / MarqueePresenter（ファサード）・MarqueeOptions・MarqueeAppearance
using EsUtil.Platform.Core.Coord;       // ICoordTransform / CoordTransform（コア提供）
using EsUtil.Platform.Core.Geometry;         // EsPoint / EsSize（コア提供）
```

---

## 5. クイックスタート

### 5.1 AXAML

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:marquee="clr-namespace:EsUtil.Ui.AvaloniaUI.Marquee;assembly=EsUtil.Ui.AvaloniaUI.Marquee">
    <Canvas>
        <marquee:MarqueeControl x:Name="SelectionOverlay" />
    </Canvas>
</Window>
```

外観（色・フォントサイズ・枠線の太さ・表示可否）は外観の単一値（`Appearance`）、計算に必要な設定（寸法・対話パラメータ）は設定値（`Options`）にまとまっているため、AXAML の属性では指定せずコード（またはバインディング）から設定します。

### 5.2 Code-behind（設定・イベント購読・座標ポート設定）

```csharp
var marquee = new MarqueeControl
{
    // 選択矩形の設定をまとめて 1 回で渡す（同値の再設定では再描画されない。寸法と対話パラメータのみ）
    Options = MarqueeOptions.Default with
    {
        HandleSize = 10.0,
    },

    // 外観（色・フォントサイズ・枠線の太さ・表示可否）は提示層の 1 値が単一所有する
    Appearance = MarqueeAppearance.Default with
    {
        // 選択枠線は装飾（暗転マスクの外枠）が描く（色と太さをここで与える）
        OuterBorderColor = Colors.White.ToDrawingColor(),
        OuterBorderThickness = 2.0,
        ShowOverlay = true,
        ShowOuterBorder = true,
    },
};

// 結果は公開イベントで通知される（マウス・キーイベントを直接処理しない）
marquee.SelectionChanged += (_, e) => UpdatePreview(e.Rect);
marquee.SelectionCompleted += (_, e) => ConfirmSelection(e.Rect);
marquee.SelectionCancelled += (_, _) => CancelSelection();
marquee.ConfirmRequested += (_, e) => ConfirmSelection(e.Rect);
marquee.CancelRequested += (_, _) => CancelSelection();

// ラベル（サイズ・カーソル座標）はコントロールがテキスト要素として配置する。
// ホストは描画完了イベントで付加情報（操作・情報ラベル）を描ける（図形はコントロールが描き終えている）。
marquee.Rendered += (_, e) =>
{
    // e.Content: 描画計画（注目矩形（付加情報の基準）・ラベル配置・層の順序・外観）
    // e.Context: ホスト固有の付加情報を描くための描画コンテキスト
};

// 座標ポートを設定（対象 = 画像ピクセル等）。ポート生成は EsUtil.Platform.Core の CoordTransform ファクトリを使用する。
marquee.SetCoordTransform(CoordTransform.ClientToSource(() => 2.0)); // 例: DPI 200%（ToSource = DIP→対象 × 2）
// 第 2 引数に境界（画像サイズ等）を与えると選択を対象外へ出さない（省略すると無制限）

panel.Children.Add(marquee);
```

---

## 6. 座標系

MarqueeControl は 3 つの座標系を使い分けます。

| 座標系 | 用途 |
| --- | --- |
| 対象座標 | 選択状態・nudge・イベント通知 |
| コントロールローカル DIP | マウス入力・描画 |
| 画面物理 Pixel | カーソル操作・画面変換 |

対象 ↔ DIP の変換は `SetCoordTransform(ICoordTransform?)` で注入します（ToSource = DIP→対象 / ToDip = 対象→DIP / SourceArea = 対象の境界）。

---

## 7. 操作

| 操作 | 動作 |
| --- | --- |
| 左ドラッグ（矩形外） | 新規選択の開始 |
| 左ドラッグ（ハンドル上） | リサイズ（角・辺の 8 方向） |
| 左ドラッグ（矩形内） | 移動 |
| ダブルクリック（矩形内） | 確定要求（`ConfirmRequested`） |
| ダブルクリック（矩形外） | キャンセル要求（`CancelRequested`） |
| 矢印キー | nudge（1 対象ピクセル単位の微調整。 カーソルを非表示にして十字マーカーを表示） |
| Enter | 確定要求（`ConfirmRequested`） |
| Escape | キャンセル要求（`CancelRequested`） |

矢印キーの nudge はフォーカスに依存しません（本コントロールが `TopLevel` のキー入力を購読するため、フォーカスの位置に関わらず動作します）。

---

## 8. 他の UI 版との違い

| 項目 | Avalonia UI 版 | MewUI 版 | WinUI 3 版 | WinForms 版 |
| --- | --- | --- | --- | --- |
| 描画 API | Skia（`DrawingContext`） | Skia（`IGraphicsContext`） | Skia（描画層） | GDI+（`Graphics`） |
| イベント引数の座標 | `Point` / `Rect`（Avalonia の型） | `EsPoint` / `EsRect`（コアの型） | `EsPoint` / `EsRect`（コアの型） | `EsPoint` / `EsRect`（コアの型） |
| マウスダウン・アップのイベント | 公開しない | 同左 | 同左 | 同左 |
| ホイール操作 | 扱わない（ホストが購読する） | 同左 | 同左 | 同左 |
| 矢印キーによる nudge | フォーカスに依存しない（`TopLevel` を購読） | フォーカスが必要 | フォーカスが必要 | フォーカスが必要（`IsInputKey` で入力キーにする） |
| ラベル（文字）の表示 | UI 要素（`Border` + `TextBlock`） | UI 要素（`Border` + `TextBlock`） | 描画面へ描く | 描画面へ描く（GDI+） |
| 背景の透かし方 | 描画面を透明で消して下を透かす | 同左 | 同左 | 背景を透明にして親の描画を背景に使う |
| ビューポート（スクロール・ズーム）への追従 | ホストが変化を検知して `InvalidateRender()` を呼ぶ | 同左 | 同左 | 同左 |

いずれの UI 版も、選択矩形の計算・入力の解釈・描画計画は同じコアと提示層が所有します（差は UI フレームワーク固有の描画 API と入力の受け取り方だけです）。

---

## 9. カスタマイズ

差し替えの入口は 3 つです（**派生は不要**。 コントロールのプロパティへ設定します）。

| 差し替える対象 | 差し替え方 |
| --- | --- |
| 色・枠線の太さ・表示可否 | `Appearance`（`MarqueeAppearance`）を設定する |
| 矩形で並べられる装飾（枠・ハンドル・マーカー・破線）とラベルの文言・位置 | `DrawPlanResolver`（`Func<MarqueeDrawPlan, MarqueeDrawPlan>`）を設定する（計画の値の置き換え） |
| 矩形で表せない描画（円・斜線等） | `Appearance` の `Show*` を false にして `Rendered` で受け取る描画面へ自前で描く（`DrawPlan` を持つ UI 版では計画どおりの描画も呼べる） |

描画計画（`MarqueeDrawPlan`）と外観（`MarqueeAppearance`）は `EsUtil.Feature.Marquee.Presenter` が単一所有し、4 つの UI 版で共通です。 UI が持つのは UI フレームワーク固有の描画 API の呼び出しだけです（仕様は [`docs/SPEC_API.md`](docs/SPEC_API.md)）。

---

## 10. ログ

`Log` へ `ILogger` を設定すると、コントロールが委譲した操作の例外を出力します（`Error`。 既定は出力なし）。

| レベル | 出力例 |
| --- | --- |
| Error | `[Marquee] <操作> で例外が発生`（アプリは継続する） |

設定の変更・入力の判断・描画はコアと提示層が同じロガーへ記録し、接頭辞は `LogPrefix` で全層へ伝搬します（既定 `[Marquee]`）。 ログ基盤（出力先・レベルの制御）はホストが所有します。

---

## 11. 未対応

- **ホイール操作**: 扱わない（ホストが `PointerWheelChanged` を購読する）。
- **`ScrollViewer` に相当する自動連携**: 行わない（ビューポートの変化はホストが検知して `InvalidateRender()` を呼ぶ）。
- **マウスダウン・アップ専用のイベント**: 持たない（生のポインター入力は基底クラスのイベントで観測する。 本コントロールは選択の解釈だけを公開する）。
- **文字（ラベル・座標ラベル）を描画面へ直接描くこと**: 行わない（`Border` + `TextBlock` の子要素として置き、実寸の測定だけを行う）。

---

## 12. サンプルアプリケーション

`sample/` に動作確認用のサンプルアプリが含まれています。 マウスドラッグによる選択・nudge・外観変更・イベントログを確認できます。

```bash
dotnet run --project sample/EsUtil.Ui.AvaloniaUI.Marquee.Sample.csproj
```

詳細は [sample/README.md](sample/README.md) を参照してください。

---

## 13. ドキュメント

- [外部機能仕様書](docs/SPEC_API.md)
- [システム仕様書](docs/SPEC_System.md)

---

## 14. プロジェクト構成

```text
EsUtil.Ui.AvaloniaUI/Marquee/
├── src/                                     # UI 層（Avalonia 固有の描画と配線）
│   ├── EsUtil.Ui.AvaloniaUI.Marquee.csproj  # 独立プロジェクト（Core + Presenter + EsUtil.Platform.Core を参照）
│   ├── MarqueeControl.cs                    # 公開 API + 効果の適用 + 通知の転送 + ポインター入力 + 描画の起動
│   ├── MarqueeControl.Input.cs              # キーボード入力（フォーカス非依存。コア型 EsKey への変換と通知）
│   ├── MarqueeControl.Visual.cs             # 描画計画（`Layers` の順）を自 UI の描画面へ写し、ラベル要素を配置する
│   ├── MarqueeContextPainter.cs             # 描画計画を DrawingContext へ写す
│   └── SelectionRectEventArgs.cs / MarqueePointEventArgs.cs / MarqueeRenderedEventArgs.cs  # 公開イベント引数
├── docs/                                    # SPEC_API.md（API リファレンス）/ SPEC_System.md（システム仕様書）
├── sample/                                  # サンプルアプリ（sample/README.md 参照）
└── tests/                                   # 単体テスト（公開 API 経由）
```

### 14.1 共有コア（`EsUtil.Feature.Marquee.Core`）と提示層（`EsUtil.Feature.Marquee.Presenter`）

UI 非依存の幾何計算・状態機械・入力の解釈は `EsUtil/Feature.Marquee.Core` が単一所有し、描画計画（層の順序・装飾矩形・文言・配置）と外観（`MarqueeAppearance`）は `EsUtil/Feature.Marquee.Presenter` が単一所有します（Avalonia / MewUI で共有）。 UI 層は描画計画を自 UI の描画 API で描き、ラベル要素の配置を担います。

本コントロールの層構成（Core / Presenter / UI）は [`EsUtil/README.md`](../../EsUtil/README.md) を参照してください。

```text
EsUtil/Feature.Marquee.Core/
├── src/                                     # 公開面は src/ 直下（利用者の using は 1 本）
│   ├── MarqueeCore.cs / MarqueeCore.Geometry.cs / MarqueeCore.Limits.cs  # 公開ファサード（partial）
│   ├── IMarqueeEffects.cs / MarqueeGeometry.cs / MarqueeHandles.cs / MarqueeMarker.cs / MarqueeRenderRequest.cs
│   ├── MarqueeOptions.cs / MarqueeSnapshot.cs / MarqueeNotice.cs / MarqueeNoticeKind.cs
│   ├── MarqueeHitTarget.cs / MarqueeInteractionState.cs / MarqueeCursorKind.cs
│   └── Core/                                # internal（層の内側へ置く）
│       ├── Contract/                        # 効果の契約（MarqueeEffectsBase）
│       ├── Interaction/                     # 状態機械（MarqueeSession / MarqueeState / MarqueeFeedback / MarqueeCaptureAction / MarqueeCursorActions）
│       ├── Logging/                         # ログ定義（MarqueeLogMessage）
│       └── Selection/                       # 選択矩形の幾何（MarqueeSelection）
└── tests/                                   # 単体テスト（UI 非依存・並列実行）
```

### 14.2 装飾（提示層が単一所有する）

選択矩形の**外側・内側に描く装飾**（暗転マスク・外枠・ハイライト・内側の塗り）は、選択矩形コントロール単体の概念ではない（撮影オーバーレイ等へ組み込むときの表示である）ため、提示層（`EsUtil.Feature.Marquee.Presenter`）が単一所有します。 装飾の矩形は `MarqueePresenter`（`DrawPlan` partial） が計算し、表示可否と太さと色は外観（`MarqueeAppearance`）が持ち、描画順は `MarqueePresenter` が決めます（詳細は同プロジェクトの `README.md`）。

---

## 15. テスト

`EsUtil.Ui.AvaloniaUI/Marquee/tests` が本プロジェクトのテストを持ちます（xUnit v3）。 実行手順と合否基準は [検証手順.md](../../.github/検証手順.md) が定めます。

```powershell
dotnet test EsUtil.Ui.AvaloniaUI/Marquee/tests/EsUtil.Ui.AvaloniaUI.Marquee.Tests.csproj
```

---

## 16. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
