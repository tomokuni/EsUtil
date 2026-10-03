# EsUtil.Feature.Loupe.Core API リファレンス

UI 非依存のコア（選択範囲を拡大表示するルーペの計算）が提供する公開 API のリファレンスです。  
使い方（概要・クイックスタート・カスタマイズ）は [`../README.md`](../README.md)、アーキテクチャと設計判断は [`SPEC_System.md`](SPEC_System.md) を参照してください。

## 目次

- [1. 前提条件](#1-前提条件)
- [2. 公開 API](#2-公開-api)
  - [2.1 `EsUtil.Feature.Loupe`](#21-esutilfeatureloupe)
  - [2.2 `LoupeCore`（ファサード）のメンバー](#22-loupecoreファサードのメンバー)
  - [2.3 `LoupeOptions` のメンバー](#23-loupeoptions-のメンバー)
  - [2.4 `LoupeCore.Limits` のメンバー](#24-loupecorelimits-のメンバー)
  - [2.5 描画の契約（本コアには置かない）](#25-描画の契約本コアには置かない)
- [3. 契約（利用者が守る・受け取る値）](#3-契約利用者が守る受け取る値)
  - [3.1 描画の幾何（`LoupeGeometry`）](#31-描画の幾何loupegeometry)
  - [3.2 切り出し領域の導出（`LoupeGeometry` のメンバー）](#32-切り出し領域の導出loupegeometry-のメンバー)
  - [3.3 表示可否・装飾・文言（描画層が単一所有）](#33-表示可否装飾文言描画層が単一所有)
- [4. 使用例](#4-使用例)
- [5. 座標系](#5-座標系)
- [6. 制限事項](#6-制限事項)

## 1. 前提条件

- 本コアは UI フレームワークに依存しません（依存は `EsUtil.Platform.Core` と `Microsoft.Extensions.Logging.Abstractions`（ログ定義のソース生成）のみ）。 UI 固有の型は利用者（UI 実装）が境界で変換します。
- 座標は `EsPoint` / `EsSize` / `EsRect`（`EsUtil.Platform.Core`）、拡大元画像は `EsBitmap` で扱います。
- 描画の可否は**外観（描画にだけ必要な設定を所有する上位層）の明示フラグ**（`ShowCrosshair` / `ShowBorder` / `ShowCoordLabel`）が単一所有します（描画にだけ必要な設定のため、コアの設定には含めません）。
- 描画の順序（下地の塗り → 拡大画像 → 十字線 → 枠線 → テキスト）と色は描画層（本コアを参照する上位の層）が単一所有します（本コアは塗りも順序も持たず、幾何だけを返します）。
- コントロール化にだけ必要な処理（再描画要求のバッチ化・世代番号管理）は上位のコントロール実装が所有します。
- ラベルの**文言・基準点**は描画層（本コアを参照する上位の層）が単一所有しますが、**描画と実寸測定**は利用者が行います（実寸は描画 API でしか測れないため）。

## 2. 公開 API

可視性は public のみを記載します（`internal` は [`SPEC_System.md`](SPEC_System.md) の可視性ポリシーを参照）。  
**公開面（ファサードと、ファサードのメンバーの型）はすべてルート名前空間 `EsUtil.Feature.Loupe` に属します**（`internal` の型だけが層フォルダ `src/Core/` の名前空間に残ります）。

### 2.1 `EsUtil.Feature.Loupe`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `LoupeCore` | class | 操作の唯一の窓口となるファサード |
| `LoupeCore.Limits`（`LoupeCore` の入れ子） | static class | 数値パラメータの既定値・安全域と補正 |
| `LoupeGeometry` | record struct | 1 フレーム分の描画の幾何（コア⇔UI の契約）。</br>UI 層は値の組を自分で組み立てず、`LoupeCore.ResolveGeometry` が返した値を受け取る |
| `LoupeMagnifiedImage` | record struct | 拡大画像の切り出しと描画の矩形（切り出し領域・描画元・描画先）。</br>`LoupeGeometry` の導出値 |
| `LoupeOptions` | record | 計算に必要な設定（表示サイズ・拡大率）と既定値・補正。</br>**色・フォント・枠線の太さ・表示可否は持たない**（描画層の外観が単一所有する） |
| `LoupeLayout` | readonly record struct | 表示レイアウトの値（表示サイズ（DIP）と表示位置。</br>配置しない場合は位置が null） |

### 2.2 `LoupeCore`（ファサード）のメンバー

ルーペの**操作の唯一の窓口**（適用パターン: Facade）。  
UI 層とホストは本型だけを参照し、配置計算・幾何の計算は internal のため外部からは直接呼べません。

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `LoupeCore(LoupeOptions? options = null, ILogger? logger = null)` | ctor | 生成（設定を省略した場合は `LoupeOptions.Default`）。 **ロガーを注入した場合は、その場で生成の記録を出力する**（注入しない場合は `Log` の初回設定時に出力する）。 描画実装は受け取らない（描画は UI の関心事であり、本コアは描画 API を持たない） |
| `Log` | property | ロガー。 null は `NullLogger` へ吸収される。 **コンストラクターで注入されていない場合、初回の設定時に生成の記録を 1 度だけ出力する** |
| `Options` | property | 設定（表示サイズ・拡大率）。 **変化したときだけ**変更ログ（Debug）を出力する |
| `ClientToPixelScaling` | property | 表示 DIP → 物理ピクセルのスケーリング係数（環境値。 補正は `LoupeCore.Limits` が単一所有する）。 **変化したときだけ**変更ログ（Debug）を出力する |
| `CenterPoint` | property | 拡大中心（対象座標 = 元画像ピクセル座標）。 未設定（null）は切り出し領域を決められないため、`LoupeGeometry` は拡大画像の幾何（`MagnifiedImage` / `CaptureTopLeft`）を持たず、配置も行わない。 **変更は最も低いレベル（Trace）で出力する**（マウス移動ごとに変わるため） |
| `PositionOffset` | property | 拡大中心からの右下オフセット（DIP。 既定は 0）。 **変化したときだけ**変更ログ（Debug）を出力する |
| `SetCoordTransform(ICoordTransform?)` | method | 対象 ↔ 親領域 DIP の座標変換ポートの設定（null は等価変換）。 **変化したときだけ**変更ログ（Debug）を出力する |
| `ResolveLayout(EsSize)` | method | **どこへ・どの大きさで置くか**を決める（親領域 DIP の世界）。 親領域サイズから表示レイアウト（`LoupeLayout` = 表示サイズ + 位置）を解決する（拡大中心未設定時と、親領域が収められない場合は位置が null）。 毎フレーム呼ばれるためログは出力しない |
| `ResolveGeometry(double clientSize)` | method | **何を・何ピクセルで描くか**を決める（物理ピクセルの世界）。 描画 1 フレーム分の幾何（`LoupeGeometry`）の解決（DIP → 物理ピクセルの換算・切り出し領域の算出を含む）。 毎フレーム呼ばれるためログは出力しない |

**本型が単一所有するもの**: 表示レイアウトの解決（表示サイズの決定と表示位置の補正）・描画の幾何の解決・**設定変更の検出とログ出力**。

**ログの出力方針**: 生成（コンストラクター注入時、または `Log` の初回設定時に 1 度だけ。 Information）・低頻度の設定変更とメソッド呼び出し（Debug）・高頻度の設定値（`CenterPoint`。 Trace）を出力します。 毎フレーム呼ばれる解決メソッドは出力しません（詳細は [`SPEC_System.md`](SPEC_System.md) の「ログ」を参照）。

| メソッド | 決めること | 入力 | 出力 |
| --- | --- | --- | --- |
| `ResolveLayout` | 配置（どこへ・どの大きさで置くか） | 親領域のサイズ（DIP） | 表示サイズ（要求サイズをそのまま）＋表示位置（拡大中心 → 親領域 DIP → オフセット加算 → 親領域内へ補正） |
| `ResolveGeometry` | 描画（何を・何ピクセルで描くか） | 表示サイズ（DIP） | 描画面（DIP → 物理ピクセルへ換算）＋切り出し領域（拡大中心から導出） |

**表示サイズは縮小しません**（領域に収めるためにサイズを変えると、要素サイズ・描画ビットマップ・切り出し領域の対応が崩れるため）。 領域が収められるかどうかは次のように扱います。

| 領域 | 収められない場合の扱い |
| --- | --- |
| 親パネル（配置） | 配置しない（`LoupeLayout.Position` が null） |
| 描画先の表面（描画） | 本コアは描画先を知らないため判定しない（`ClientPixelSize` と同じ大きさの表面を呼び出し側が用意する） |

**はみ出しの補正**: 表示サイズは変えず、拡大中心が領域の端に寄った場合に表示位置を `EsSize.ClampOffset` で領域内へ補正します（0〜領域の長さ − 表示サイズ）。 表示位置は `ResolveLayout(parentSize).Position` が返す値をそのまま使います（要素サイズ・描画サイズ・位置は同じ表示サイズから導出します）。

`ResolveGeometry` へは `ResolveLayout(parentSize).ClientSize`（= 要求サイズ）を渡します。 表示サイズを縮小しないためどちらを渡しても同じ値になりますが、実際に描く要素の大きさをそのまま渡す形に統一します。

### 2.3 `LoupeOptions` のメンバー

計算に必要な設定（表示サイズ・拡大率）を 1 つの値として所有します。 両 UI は本型を 1 プロパティとして公開するだけであり、UI ごとに既定値がずれたり設定プロパティが増えたりしません。

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `RequestedSize` | `int` | `160` | ルーペの基本サイズ（DIP・一辺）。 安全域 1〜4096。 本値がそのまま表示サイズになる（親領域に合わせた縮小はしない） |
| `ZoomLevel` | `int` | `4` | 拡大率（偶数推奨）。 安全域 1〜64 |

`record` のため `with` 式による部分変更と値の等価比較ができます。 等価比較は UI のプロパティシステムと併せて「同値の再設定では再描画しない」動作を保証します。

| メンバー | 説明 |
| --- | --- |
| `Default` | すべての項目を既定値にした設定（部分変更の基点: `LoupeOptions.Default with { ZoomLevel = 8 }`） |

数値項目（`RequestedSize` / `ZoomLevel`）は **`init` アクセサで補正**します（規則は `LoupeCore.Limits` が単一所有する）。 したがってオブジェクト初期化子・`with` 式・逆シリアル化などの **どの設定経路でも**範囲内の値だけが保持され、UI 側に補正の登録を要求しません。

**色・フォント・枠線の太さ・表示可否は本型に含みません**（描画にだけ必要な設定のため、描画層の外観が単一所有します。 枠線の太さの補正はその外観が `LoupeCore.Limits.CoerceBorderThickness` へ委譲します）。 ホストは UI の `Appearance` プロパティへ外観を指定します。

**含めない値**: 拡大元画像・拡大中心（マウス移動ごとに変わるデータ）、DPI スケーリング係数（環境値）、表示位置のオフセット（UI 固有のホスト結線）。 これらは UI の個別プロパティとして公開します。

### 2.4 `LoupeCore.Limits` のメンバー

ルーペの数値パラメータの既定値と安全域を単一所有し、範囲外の値を補正します（非有限は既定値）。 **枠線の太さの安全域は描画層の外観が参照します**。

| 定数 | 値 | 説明 |
| --- | --- | --- |
| `RequestedSizeMin` / `RequestedSizeMax` / `RequestedSizeDefault` | `1` / `4096` / `160` | 表示サイズ要求値（DIP） |
| `ZoomLevelMin` / `ZoomLevelMax` / `ZoomLevelDefault` | `1` / `64` / `4` | 拡大率 |
| `BorderThicknessMin` / `BorderThicknessMax` / `BorderThicknessDefault` | `1` / `64` / `2` | 枠線の太さ（ピクセル。 描画層の外観が参照する） |
| `ClientToPixelScalingMin` / `ClientToPixelScalingMax` / `ClientToPixelScalingDefault` | `0.1` / `8.0` / `1.0` | スケーリング係数（環境値。 他の数値より後に置く） |

| メソッド | 説明 |
| --- | --- |
| `CoerceRequestedSize(int)` | 表示サイズ要求値を 1〜4096 へ補正 |
| `CoerceZoomLevel(int)` | 拡大率を 1〜64 へ補正 |
| `CoerceBorderThickness(int)` | 枠線の太さを 1〜64 へ補正（描画層の外観が呼ぶ） |
| `CoerceClientToPixelScaling(double)` | スケーリング係数を 0.1〜8.0 へ補正。 非有限値は `ClientToPixelScalingDefault` |

### 2.5 描画の契約（本コアには置かない）

描画の契約は本コアにありません（**本コアは描画 API も描画の抽象も持ちません**）。 描画計画の解決は描画層（本コアを参照する上位の層）が、描画は各 UI の描画実装が担います。 層の順序は描画層が単一所有し、UI はその順に写すだけです。

```csharp
var loupe = new LoupeCore(options) { CenterPoint = cursorSource, ClientToPixelScaling = 1.25 };
loupe.SetCoordTransform(port);

var layout = loupe.ResolveLayout(parentSize);          // 表示サイズ（要求サイズ）+ 表示位置（配置しない場合は Position が null）
var geometry = loupe.ResolveGeometry(layout.ClientSize); // 幾何（物理ピクセル・切り出し領域を解決済み）

// 描画完了通知（ホストが描画済み画像へオーバーレイを描くための情報）は幾何から読む
var captureTopLeft = geometry.CaptureTopLeft;   // 切り出し領域の左上（対象座標。中心未設定は null）
var scale = geometry.ZoomLevel;                 // 対象座標 → ルーペ画像ピクセルの変換係数
```

## 3. 契約（利用者が守る・受け取る値）

### 3.1 描画の幾何（`LoupeGeometry`）

1 フレーム分の描画の幾何（**コア⇔UI の契約**）です。 **UI 層は本値を自分で組み立てません**（設定・環境値から描画に必要な形へ写す組み立て順は `LoupeCore.ResolveGeometry` が単一所有します）。 UI 層は本値を描画層（上位の層）へ渡すだけで、描画に必要な値の単位と換算（DIP → 物理ピクセル）を持ちません。

**役割の分担**: ホストが設定する入力（設定値・環境値）は UI の公開プロパティ（`Options` / `ClientToPixelScaling`）が持ち、本値はそれらから**描画 1 フレーム分に必要な形へ解決したスナップショット**です（提示層は公開プロパティを読まない）。

| メンバー | 型 / 値 | 説明 |
| --- | --- | --- |
| `Center` | `EsPoint?` | 拡大中心（対象座標 = 元画像ピクセル座標）。 null の場合は拡大画像を描画しない |
| `ClientSize` | `double` | ルーペの一辺の表示サイズ（DIP。 ラベルの位置の基準であり、物理ピクセルサイズの換算元） |
| `ClientPixelSize` | `int` | ルーペの一辺のサイズ（物理ピクセル。 表示サイズ × `ClientToPixelScaling` を切り上げ・最低 1）。 描画先の表面は本値と同じ大きさで用意する |
| `ClientToPixelScaling` | `double` | 表示 DIP → 物理ピクセルのスケーリング係数（描画計画の解決（枠線の太さの換算）にも使う） |
| `ZoomLevel` | `int` | 拡大率（1〜64。 設定 `Options.ZoomLevel` の値。 描画完了通知のスケールにも使う） |
| `CaptureSize` | `int`（導出） | 切り出し領域の一辺（対象ピクセル）。 物理ピクセルサイズを拡大率で割った切り上げ。 位置パラメータではなく `ClientPixelSize` / `ZoomLevel` からの導出であり、切り上げの規則も本値が単一所有する |
| `ClientPixelRect` | `EsRect` | 描画先の全領域（左上を原点とする一辺 `ClientPixelSize` の正方形・物理ピクセル）。 描画層がこの矩形へ下地を塗る（UI 層は矩形を組み立てない） |
| `CaptureTopLeft` | `EsPoint?` | 拡大中心から切り出し領域の左上（対象座標）。 中心未設定の場合は null（描画しない） |
| `MagnifiedImage` | `LoupeMagnifiedImage?` | 拡大画像の切り出し・描画元・描画先の矩形。 中心未設定の場合は null |

| メソッド | 説明 |
| --- | --- |
| （なし） | ファサードの `LoupeCore.ResolveGeometry` が唯一の生成元である（UI 層は本値を組み立てない） |

拡大元画像は UI ごとに型が異なるため本値に含めず、描画実装が解決した `EsBitmap` を別引数として渡します（複数の UI が同じ手順で本値を受け取ります）。

### 3.2 切り出し領域の導出（`LoupeGeometry` のメンバー）

描画の幾何から切り出し領域を導出します（`LoupeGeometry` の値だけから決まるため、独立したクラスを持ちません）。

| メンバー | 説明 |
| --- | --- |
| `CaptureSize` | 切り出し領域の一辺（対象ピクセル）。 物理ピクセルサイズを拡大率で割った切り上げ。 十字線の交点も本値を使う |
| `CaptureTopLeft` | 拡大中心から切り出し領域の左上（対象座標）。 中心未設定の場合は null（描画しない）。 **ホストが描画済み画像へオーバーレイを描くために public** |
| `MagnifiedImage` | 拡大画像の切り出し・描画元・描画先の矩形（`LoupeMagnifiedImage`）。 **拡大倍率と描画先の大きさの関係を単一所有**し、両 UI が同じ拡大結果になるようにする。 中心未設定の場合は null |

**切り出しの実行はありません**: 切り出しは描画実装（各 UI の painter）が `EsBitmap.Crop` を呼んで行います。 本メンバーは「どこを切り出すか」を解決する値だけを持ちます。

### 3.3 表示可否・装飾・文言（描画層が単一所有）

以下はコアには含みません（描画にだけ必要な設定のため、描画層（本コアを参照する上位の層）が単一所有します）。 「所有者」は 1 つの層（描画層）であり、**外観はその層が持つ設定値**、**描画計画はその層が解決する描画の指示**を指します。

| 項目 | 所有者（描画層の内訳） |
| --- | --- |
| 表示可否（十字線・枠線・中心座標ラベル） | 外観（設定値。 透明色を代用せず明示フラグで持つ） |
| 色・フォントサイズ・角丸・枠線の太さ | 外観（設定値） |
| 描画の層とその順序・十字線 / 枠線の矩形 | 描画計画（解決する手順） |
| 中心座標ラベルの文言・基準点 | 描画計画 |
| 実寸 → ラベル要素の配置 | 描画計画 |
| 枠線の太さの物理ピクセル換算 | `EsUtil.Platform.Core` の `DpiScaler`（呼び出すのは描画層） |

## 4. 使用例

```csharp
using EsUtil.Feature.Loupe;
using EsUtil.Platform.Core.Geometry;

// 設定（計算に必要な値のみ。色・フォント・表示可否は描画層の外観が持つ）
var options = new LoupeOptions { RequestedSize = 160, ZoomLevel = 4 };
using var loupe = new LoupeCore(options);

// 拡大中心（対象座標 = 元画像ピクセル座標）と表示位置のオフセット
loupe.CenterPoint = new EsPoint(1920, 1080);
loupe.PositionOffset = 16;

// 表示レイアウト（親領域のサイズを渡す。拡大中心が未設定の場合は位置が null）
var layout = loupe.ResolveLayout(parentSize: new EsSize(800, 600));

// 描画の幾何（1 フレーム分。UI 層は本値を組み立てず、描画層へ渡す）
// clientSize は ResolveLayout が返した値（= 要求サイズ。 領域に合わせた縮小はしない）
var geometry = loupe.ResolveGeometry(layout.ClientSize);
var capture = geometry.MagnifiedImage;   // 切り出し領域・描画元・描画先
```

<!-- 描画計画（層の順序・装飾矩形・文言・配置）の解決は描画層（本コアを参照する上位の層）が行う。 本コアは幾何と表示レイアウトだけを提供する。 -->

## 5. 座標系

| 座標系 | 用途 |
| --- | --- |
| 対象座標 | 拡大中心・切り出し領域。 元画像ピクセル座標 |
| 親領域 DIP | 表示レイアウト（表示サイズ・表示位置）。 `ICoordTransform.ToDip` で得る |
| 物理ピクセル | 描画の幾何のサイズ・寸法。 表示 DIP × `ClientToPixelScaling` |

- 拡大中心は `ICoordTransform` を通して親領域 DIP へ変換し、`PositionOffset` を加えてから親領域内へ補正します。
- 切り出し領域の一辺は物理ピクセルサイズを拡大率で割った切り上げです。

## 6. 制限事項

- 描画 API は提供しません（描画計画の解決は描画層が単一所有し、描画は各 UI が自 UI の描画 API で行います。 描画 API の抽象は持ちません）。
- 色（下地・十字線・枠線・ラベル）は本コアに含みません（描画にだけ必要な設定であるため、描画層の外観が単一所有します）。
- フォントサイズ・角丸・内側余白は本コアに含みません（描画層の外観が単一所有します）。
- 表示可否（十字線・枠線・中心座標ラベル）は本コアに含みません（描画にだけ必要な設定であるため、描画層の外観が単一所有します）。
- 拡大元画像の取得・切り出しの実行は本コアに含みません（UI ごとに型が異なるため、UI 層が担います）。
- 再描画のバッチ化・世代番号管理は本コアに含みません（コントロール化にだけ必要な処理のため、上位のコントロール実装が所有します）。
