# EsUtil.Feature.Loupe.Core

ルーペ（選択範囲の拡大表示）の **UI 非依存のコア**です。 UI 実装が変わっても同じ計算結果を共有します。

## 1. 概要

ルーペの表示レイアウト（表示サイズ・表示位置）と描画の幾何（切り出し領域・描画面）の解決を 1 箇所に集約し、UI ごとの実装差から切り離します。

**操作の唯一の窓口は公開ファサード `LoupeCore`** です。 UI 層とホストは本型だけを参照し、表示レイアウトと描画の幾何の解決を委ねます（個々の計算クラスは内部実装です）。

**描画は本コアの関心事ではありません**。  
描画 API も描画の抽象も持たず、返すのは描画の幾何（`LoupeGeometry`）だけです。 装飾（十字線・枠線）の矩形・文言・表示可否・描画順は描画の都合として描画層（本コアを参照する上位の層）が単一所有します。

UI フレームワークに依存しないため、UI 初期化なしの `[Fact]` で検証できます。

## 2. 使い方

```csharp
using EsUtil.Feature.Loupe;
using EsUtil.Platform.Core.Geometry;

// 設定（計算に必要な値のみ。色・フォント・表示可否は描画層の外観が持つ）
var options = new LoupeOptions { RequestedSize = 160, ZoomLevel = 4 };
using var loupe = new LoupeCore(options, logger);   // ロガーはコンストラクターでも後からでも注入できる

// 拡大中心（対象座標 = 元画像ピクセル座標）と表示位置のオフセット（DIP）
loupe.CenterPoint = new EsPoint(1920, 1080);
loupe.PositionOffset = 16;

// 表示レイアウト（親領域のサイズを渡す。拡大中心が未設定の場合は位置が null）
var layout = loupe.ResolveLayout(parentSize: new EsSize(800, 600));

// 描画の幾何（1 フレーム分。UI 層は本値を組み立てず、描画層へ渡す）
// clientSize は ResolveLayout が返した値（= 要求サイズ。 領域に合わせた縮小はしない）
var geometry = loupe.ResolveGeometry(layout.ClientSize);
var capture = geometry.MagnifiedImage;   // 切り出し領域・描画元・描画先
```

### 2.1 ログの注入

ロガーは**コンストラクター（`new LoupeCore(options, logger)`）と `Log` プロパティのどちらでも注入**できます。 ロガーが確定した時点（コンストラクター注入時、または `Log` の初回設定時）で**生成の記録を 1 度だけ**出力します（それ以降は設定の変更だけが出力されます）。

### 2.2 2 つの解決メソッドの使い分け

| メソッド | 決めること | 入力 | 出力 |
| --- | --- | --- | --- |
| `ResolveLayout` | 配置（どこへ・どの大きさで置くか） | 親領域のサイズ（DIP） | 表示サイズ（要求サイズをそのまま）＋表示位置（領域内へ補正） |
| `ResolveGeometry` | 描画（何を・何ピクセルで描くか） | 表示サイズ（DIP） | 描画面（物理ピクセルへ換算）＋切り出し領域 |

**表示サイズは縮小しません**。 親パネルが表示サイズを収められない場合、`ResolveLayout` は `Position` を null として返します（配置しない = その領域には描画できません）。 描画先の表面も `ClientPixelSize` と同じ大きさで用意します（本コアは描画先を知らないため判定しません）。 拡大中心が端に寄った場合は、表示サイズを変えずに表示位置を領域内へ補正します。

## 3. 含まれるもの

| グループ | 型 |
| --- | --- |
| ファサード | `LoupeCore`（操作の唯一の窓口） / `LoupeCore.Limits`（既定値と安全域・補正） |
| 描画の幾何 | `Drawing.LoupeGeometry` / `Drawing.LoupeMagnifiedImage` |
| 設定 | `Setting.LoupeOptions`（表示サイズ・拡大率。 色・フォント・表示可否は持たない） |
| 表示レイアウト | `Placement.LoupeLayout`（表示サイズと表示位置） |
| ログ | `Logging.LoupeLogMessage`（internal） |

## 4. 依存

- 依存は `EsUtil.Platform.Core`（`Es*` 幾何型・`ICoordTransform`）と `Microsoft.Extensions.Logging.Abstractions`（ログ定義のソース生成）のみです。
- UI フレームワークを参照しません。 UI 固有の型は利用者（UI 実装）が境界で変換します。
- 依存方向は UI 実装 → 描画層（上位の層） → 本プロジェクト → `EsUtil.Platform.Core` の一方向です（コアは上位の層を参照しません）。

層の全体像は [`src/README.md`](../README.md) を参照してください。

## 5. カスタマイズ

| 差し替えたいもの | 方法 |
| --- | --- |
| 表示サイズ・拡大率 | `LoupeOptions`（`RequestedSize` / `ZoomLevel`）を変える |
| 数値の安全域 | `LoupeCore.Limits` の既定値と補正（`CoerceBorderThickness` 等）を使う |
| 見た目（色・フォント・枠線の太さ・表示可否） | 描画層の外観（上位の層）を変える（本コアは色を持たない） |
| 描画計画（層・配置） | 描画層の描画計画の解決（上位の層）を上書きする |
| 描画そのもの | 各 UI の描画実装を差し替える |

## 6. 座標型

UI フレームワークの幾何型ではなく、`EsUtil.Platform.Core` のコア幾何型 `EsPoint` / `EsSize` / `EsRect` を使用します。

UI 実装側は、境界（サービス呼び出し・イベント引数の生成箇所）で 1 行の相互変換（`.ToEsPoint()` / `.ToEsSize()` 等）を行います。  
UI 内部の状態・描画は UI 側の型のままで構いません。

## 7. ビルド

```powershell
dotnet build src/EsUtil.Feature.Loupe.Core/src/EsUtil.Feature.Loupe.Core.csproj
```

## 8. テスト

```powershell
dotnet test src/EsUtil.Feature.Loupe.Core/tests/EsUtil.Feature.Loupe.Core.Tests.csproj
```

`tests/` が単体テストを持ちます。  
UI 初期化が不要なため `[Fact]` / `[Theory]` で並列実行され、**ソースのフォルダを鏡映**します（`src/` 直下の公開型は `tests/` 直下で検証する）。

## 9. ドキュメント

- [`docs/SPEC_API.md`](docs/SPEC_API.md) — 公開 API のリファレンスと使用例
- [`docs/SPEC_System.md`](docs/SPEC_System.md) — アーキテクチャ・設計判断・ログ

## 10. ライセンス

MIT License（全文は [`LICENSE`](LICENSE) を参照）。
