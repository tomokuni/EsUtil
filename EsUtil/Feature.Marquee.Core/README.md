# EsUtil.Feature.Marquee.Core

![NuGet](https://img.shields.io/nuget/v/EsUtil.Feature.Marquee.Core.svg)
![Release](https://github.com/tomokuni/EsUtil/actions/workflows/release.yml/badge.svg)
![Build](https://github.com/tomokuni/EsUtil/actions/workflows/build.yml/badge.svg?branch=main)
![Platform](https://img.shields.io/badge/Platform-Portable-6E6E6E)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![AOT](https://img.shields.io/badge/AOT-Ready-00695C)
![License](https://img.shields.io/badge/License-MIT-3DA639)

**対応 OS**: Windows 専用（`net10.0-windows10.0.17763.0`）。 Windows 10 バージョン 1703（ビルド 15063）以降

マーキー選択（ドラッグで矩形を選び、移動・リサイズできる操作）の **UI 非依存の計算層**です。 UI 実装が変わっても同じ状態管理・判断・幾何を共有します。

## 1. 概要

選択矩形の幾何計算・状態機械・**入力の解釈（判断）**を行う **計算層（Core）**と、外観・描画計画を解決する **提示層（Presenter）**を 1 つのパッケージ（`EsUtil.Feature.Marquee.Core`）にまとめ、UI ごとの実装差から切り離します。

**操作の唯一の窓口は公開ファサード `MarqueeCore`** です。 UI 層とホストは本型だけを参照します（状態機械と幾何の導出は内部実装です）。

**描画は本コアの関心事ではありません**。 描画 API も描画ポートも持たず、返すのは幾何（注目矩形・ハンドル・始点マーカー）だけです。 色・フォント・枠線の太さ・表示可否・文言・描画順は描画層（本コアを参照する上位の層）が単一所有します。

**装飾（外側マスク・枠線・内側マスク）の概念を持ちません**。 装飾は選択矩形単体の概念ではなく、組み込み先の表示だからです（描画層が単一所有します）。

UI 非依存のためヘッドレス実行が不要で、`[Fact]` で並列実行できます。

## 2. 使い方

```csharp
using EsUtil.Feature.Marquee;

var marquee = new MarqueeCore(effects, cursorService, new MarqueeOptions { HandleSize = 8 });
marquee.Log = logger;                                          // null は NullLogger へ吸収される
marquee.SetCoordTransform(CoordTransform.Identity);            // 対象の境界（画像サイズ等）

// 入力の通知（効果の適用までファサードが行う）
marquee.Press(new EsPoint(100, 100), clickCount: 1);
marquee.Move(new EsPoint(400, 300));
var handled = marquee.KeyDown(EsKey.Right, cursorPosition: null);   // nudge・Escape・Enter
marquee.Release(new EsPoint(400, 300));

// 1 フレーム分の幾何（描画領域のサイズを渡す。注目矩形・ハンドル・マーカー）
var geometry = marquee.ResolveGeometry(marquee.CreateRequest(new EsSize(800, 600)));

// 通知（公開イベントへ転送する）
marquee.Subscribe(notice => Raise(notice));
```

**ホイール操作とビューポートの変化の追従は本コアにありません**（組み込み先が存在して初めて意味を持つ処理のため、ホストがビューポートへ転送します）。

### 2.1 ログの注入

ロガーは**コンストラクター（`new MarqueeCore(effects, cursorService, options, logger)`）と `Log` プロパティのどちらでも注入**できます。 ロガーが確定した時点（コンストラクター注入時、または `Log` の初回設定時）で**生成の記録を 1 度だけ**出力します（それ以降は設定の変更だけが出力されます）。

### 2.2 2 つの解決メソッドの使い分け

| メソッド | 決めること | 入力 | 出力 |
| --- | --- | --- | --- |
| `CreateRequest` | 入力の解釈と対象の境界 | 描画領域のサイズ（DIP） | 幾何を求める要求（対象座標へ換算済みの値） |
| `ResolveGeometry` | 1 フレーム分の幾何（何をどこへ描くか） | 要求（`CreateRequest` が返した値） | 幾何（注目矩形・ハンドル・始点マーカー） |

**入力の解釈（DIP → 対象座標の換算・境界のクランプ）は `CreateRequest` が担い、幾何の導出（どのハンドルを表示するか・マーカーの位置）は `ResolveGeometry` が担います**。 色・フォント・枠線の太さ・表示可否・文言は本コアが持たず、提示層が単一所有します。

### 2.3 提示層（外観・描画計画）

コントロールへ組み込む場合は **`MarqueePresenter`** を使います。 コアの幾何と外観から描画 1 フレーム分の計画を解決します。

```csharp
var presenter = new MarqueePresenter(effects, cursorService, options, logger);
presenter.Appearance = MarqueeAppearance.Default with { ShowHandles = true, BorderThickness = 1 };

var plan = presenter.ResolveDrawPlan(clientSize, measureText, pointerOver: true);
```

- 外観（`MarqueeAppearance`）と描画計画（`MarqueeDrawPlan`）は提示層が単一所有します。
- `Press` / `Move` / `Release` / `KeyDown` などの入力と `ResolveLayout` は提示層からコアへ委譲されます（UI は提示層だけを参照します）。

## 3. 含まれるもの

| グループ | 型 |
| --- | --- |
| ファサード | `MarqueeCore`（操作の唯一の窓口） / `MarqueeCore.Limits`（既定値・安全域・未設定の解決規則） |
| 幾何 | `MarqueeGeometry` / `MarqueeHandles` / `MarqueeMarker` / `MarqueeRenderRequest` |
| 効果と通知 | `IMarqueeEffects` / `Contract.MarqueeEffectsBase` / `MarqueeNotice` / `MarqueeNoticeKind` / `MarqueeCursorKind` |
| 状態の値 | `MarqueeHitTarget` / `MarqueeInteractionState` / `MarqueeSnapshot` |
| 設定 | `MarqueeOptions`（寸法・対話パラメータ。 色・フォント・表示可否は持たない） |
| 提示（Presenter） | `MarqueePresenter` / `MarqueeDrawPlan` / `MarqueeDrawLayer` / `MarqueeHandleRects` / `MarqueeLabelPlacements` |
| 外観 | `MarqueeAppearance`（色・フォント・枠線の太さ・表示可否） |
| ラベル | `MarqueeLabelPlacement`（配置指示） / `Drawing.MarqueeTextPlacement`（internal） |
| ログ | `Logging.MarqueeLogMessage`（internal。 アセンブリに 1 つ） |

## 4. 依存

- 直接参照するのは UI 非依存のコア型（`EsUtil.Platform.Core` の `Es*` 幾何型 / `EsKey` / `ICoordTransform`）と `Microsoft.Extensions.Logging.Abstractions`（ログ定義のソース生成）のみです。
- **カーソル操作は効果（`IMarqueeEffects` の `TryMoveCursor` / `ShowCursor`）越しにだけ要求**します（本コアは OS のカーソル API を参照しません。 実行するのは UI 側の責務です）。
- UI フレームワークを参照しません。
- **Core と Presenter は同じアセンブリ**にあり、依存方向は「Presenter → Core → `EsUtil.Platform.Core`」の一方向です（コアは提示層を参照しません）。

層の全体像は [`../README.md`](../README.md) を参照してください。

## 5. カスタマイズ

| 差し替えたいもの | 方法 |
| --- | --- |
| 寸法・対話パラメータ | `MarqueeOptions`（`HandleSize` / `HandleBorderThickness` / `MarkerLength` / `HandleHitRadius` / `MinSelectionSize` / `ClampToSourceBounds`）を変える |
| UI への効果の適用 | `IMarqueeEffects` を実装する（不要なメンバーは `MarqueeEffectsBase` を継承して省略できる） |
| 見た目（色・フォント・枠線の太さ・表示可否）と文言 | `MarqueeAppearance` を変える（UI の `Appearance` プロパティへ設定する） |
| 描画計画（層・配置） | コントロールの `DrawPlanResolver`（`Func<MarqueeDrawPlan, MarqueeDrawPlan>`）で差し替える（`MarqueePresenter` は `sealed` のため派生できない） |
| 対象の境界 | `SetCoordTransform` に `ICoordTransform`（`Bounds` 付き）を設定し、`ClampToSourceBounds` でクリップを有効にする |

## 6. 座標型

`EsRect` / `EsPoint` は常に対象座標です。 UI 論理座標（DIP）との変換は `ICoordTransform`（`Port`）が担い、各 UI のイベント引数は自前の型（例: Avalonia の `Rect`）で公開して境界で 1 行変換します。 キーは `EsKey` を介して通知します。

## 7. ビルド

```powershell
dotnet build EsUtil/Feature.Marquee.Core/src/EsUtil.Feature.Marquee.Core.csproj
```

## 8. テスト

```powershell
dotnet test EsUtil/Feature.Marquee.Core/tests/EsUtil.Feature.Marquee.Core.Tests.csproj
```

`tests/` が単体テストを持ちます。 UI 初期化が不要なため `[Fact]` / `[Theory]` で並列実行されます。

## 9. ドキュメント

- [`docs/SPEC_API.md`](docs/SPEC_API.md) — コアの公開 API のリファレンスと使用例
- [`docs/SPEC_Presenter_API.md`](docs/SPEC_Presenter_API.md) — 提示層（外観・描画計画）の公開 API
- [`docs/SPEC_System.md`](docs/SPEC_System.md) — アーキテクチャ・設計判断・ログ・名称の由来
- [`docs/SPEC_Presenter_System.md`](docs/SPEC_Presenter_System.md) — 提示層の構造・設計判断・ログ

## 10. ライセンス

[MIT License](LICENSE)
