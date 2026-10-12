# EsUtil.Feature.Marquee.Core

マーキー選択（ドラッグで矩形を選び、移動・リサイズできる操作）の **UI 非依存の計算層**です。 UI 実装が変わっても同じ状態管理・判断・幾何を共有します。

## 1. 概要

選択矩形の幾何計算・状態機械・ **入力の解釈（判断）** を 1 箇所に集約し、UI ごとの実装差から切り離します。

**操作の唯一の窓口は公開ファサード `MarqueeCore`** です。 UI 層とホストは本型だけを参照します（状態機械と幾何の導出は内部実装です）。

**描画は本コアの関心事ではありません**。 描画 API も描画ポートも持たず、返すのは幾何（注目矩形・ハンドル・始点マーカー）だけです。 色・フォント・枠線の太さ・表示可否・文言・描画順は描画層（本コアを参照する上位の層）が単一所有します。

**装飾（暗転マスク・外枠・内側の塗り）の概念を持ちません**。 装飾は選択矩形単体の概念ではなく、組み込み先の表示だからです（描画層が単一所有します）。

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

**入力の解釈（DIP → 対象座標の換算・境界のクランプ）は `CreateRequest` が担い、幾何の導出（どのハンドルを表示するか・マーカーの位置）は `ResolveGeometry` が担います**。 色・フォント・枠線の太さ・表示可否・文言は本コアが持たず、描画層（本コアを参照する上位の層）が単一所有します。

## 3. 含まれるもの

| グループ | 型 |
| --- | --- |
| ファサード | `MarqueeCore`（操作の唯一の窓口） / `MarqueeCore.Limits`（既定値・安全域・未設定の解決規則） |
| 幾何 | `MarqueeGeometry` / `MarqueeHandles` / `MarqueeMarker` / `MarqueeRenderRequest` |
| 効果と通知 | `IMarqueeEffects` / `Contract.MarqueeEffectsBase` / `MarqueeNotice` / `MarqueeNoticeKind` / `MarqueeCursorKind` |
| 状態の値 | `MarqueeHitTarget` / `MarqueeInteractionState` / `MarqueeSnapshot` |
| 設定 | `MarqueeOptions`（寸法・対話パラメータ。 色・フォント・表示可否は持たない） |
| ログ | `Logging.MarqueeLogMessage`（internal） |

## 4. 依存

- 直接参照するのは UI 非依存のコア型（`EsUtil.Platform.Core` の `Es*` 幾何型 / `EsKey` / `ICoordTransform` / `ICursorService`）と `Microsoft.Extensions.Logging.Abstractions`（ログ定義のソース生成）のみです。
- **カーソル操作は `ICursorService` 抽象にのみ依存**し、未指定（`null`）の場合は何もしません（既定実装の解決は UI 側の責務です）。
- UI フレームワークを参照しません。
- 依存方向は UI 実装 → 描画層（上位の層） → 本プロジェクト → `EsUtil.Platform.Core` の一方向です（コアは上位の層を参照しません）。

層の全体像は [`src/README.md`](../README.md) を参照してください。

## 5. カスタマイズ

| 差し替えたいもの | 方法 |
| --- | --- |
| 寸法・対話パラメータ | `MarqueeOptions`（`HandleSize` / `HandleBorderThickness` / `MarkerLength` / `HandleHitRadius` / `MinSelectionSize` / `ClampToSourceBounds`）を変える |
| UI への効果の適用 | `IMarqueeEffects` を実装する（不要なメンバーは `MarqueeEffectsBase` を継承して省略できる） |
| 見た目（色・フォント・枠線の太さ・表示可否）と文言 | 描画層の外観・描画計画（上位の層）を変える（本コアは色も文言も持たない） |
| 対象の境界 | `SetCoordTransform` に `ICoordTransform`（`Bounds` 付き）を設定し、`ClampToSourceBounds` でクリップを有効にする |

## 6. 座標型

`EsRect` / `EsPoint` は常に対象座標です。 UI 論理座標（DIP）との変換は `ICoordTransform`（`Port`）が担い、各 UI のイベント引数は自前の型（例: Avalonia の `Rect`）で公開して境界で 1 行変換します。 キーは `EsKey` を介して通知します。

## 7. ビルド

```powershell
dotnet build src/EsUtil.Feature.Marquee.Core/src/EsUtil.Feature.Marquee.Core.csproj
```

## 8. テスト

```powershell
dotnet test src/EsUtil.Feature.Marquee.Core/tests/EsUtil.Feature.Marquee.Core.Tests.csproj
```

`tests/` が単体テストを持ちます。 UI 初期化が不要なため `[Fact]` / `[Theory]` で並列実行されます。

## 9. ドキュメント

- [`docs/SPEC_API.md`](docs/SPEC_API.md) — 公開 API のリファレンスと使用例
- [`docs/SPEC_System.md`](docs/SPEC_System.md) — アーキテクチャ・設計判断・ログ・名称の由来

## 10. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
