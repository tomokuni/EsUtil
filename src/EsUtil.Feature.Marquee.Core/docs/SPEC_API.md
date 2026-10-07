# EsUtil.Feature.Marquee.Core API リファレンス

マーキー選択（ドラッグで矩形を選び、移動・リサイズできる操作）の **UI 非依存の計算層**が提供する公開 API のリファレンスです。 使い方（概要・クイックスタート・カスタマイズ）は [`../README.md`](../README.md)、アーキテクチャと設計判断は [`SPEC_System.md`](SPEC_System.md) を参照してください。

## 目次 <!-- omit from toc -->

- [1. 前提条件](#1-前提条件)
- [2. 公開 API](#2-公開-api)
  - [2.1 `EsUtil.Feature.Marquee`](#21-esutilfeaturemarquee)
  - [2.2 `EsUtil.Feature.Marquee.Core.Contract`](#22-esutilfeaturemarqueecorecontract)
  - [2.3 `MarqueeCore`（ファサード）のメンバー](#23-marqueecoreファサードのメンバー)
  - [2.4 `MarqueeOptions` のメンバー](#24-marqueeoptions-のメンバー)
  - [2.5 `MarqueeCore.Limits` のメンバー](#25-marqueecorelimits-のメンバー)
  - [2.6 `IMarqueeEffects` のメンバー](#26-imarqueeeffects-のメンバー)
  - [2.7 `MarqueeNoticeKind` の値](#27-marqueenoticekind-の値)
- [3. 契約（利用者が守る・受け取る値）](#3-契約利用者が守る受け取る値)
  - [3.1 幾何（`MarqueeGeometry`）](#31-幾何marqueegeometry)
  - [3.2 操作の受理条件](#32-操作の受理条件)
- [4. 使用例](#4-使用例)
- [5. 座標系](#5-座標系)
- [6. 制限事項](#6-制限事項)

## 1. 前提条件

- 本コアは UI フレームワークに依存しません。 UI 固有の型は利用者（UI 実装）が境界で変換します。
- 座標は `EsPoint` / `EsRect` / `EsSize` / `EsKey`（`EsUtil.Platform.Core`）で扱います。
- カーソル操作は `ICursorService` 抽象にのみ依存します（`null` の場合は何もしません）。
- **本コアが返すのは幾何だけです**。 文言・表示可否・描画順・色は描画層（本コアを参照する上位の層）が単一所有します。 **描画と実寸測定**は利用者（UI 実装）が行います（実寸は描画 API でしか測れないため）。
- **装飾（暗転マスク・外枠・内側の塗り）の概念を持ちません**。 装飾は選択矩形単体の概念ではなく、組み込み先の表示であるため、描画層が単一所有します。

## 2. 公開 API

可視性は public のみを記載します（`internal` は [`SPEC_System.md`](SPEC_System.md) の可視性ポリシーを参照）。  
**公開面（ファサードと、ファサードのメンバーの型）はすべてルート名前空間 `EsUtil.Feature.Marquee` に属します**（`internal` の型だけが層フォルダ `src/Core/` の名前空間に残ります）。

### 2.1 `EsUtil.Feature.Marquee`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `MarqueeCore` | class | 操作の唯一の窓口となるファサード |
| `IMarqueeEffects` | interface | コアが要求する効果の適用先（UI 実装が実装する） |
| `MarqueeGeometry` | record struct | 1 フレーム分の幾何（`ClientSize` / `FocusDipRect` / `FocusSourceRect` / `Handles` / `Marker` / `MouseSourcePosition` / `MouseDipPosition` ＋ `ClientRect`）。 **コアが責任を持つ値の全量** |
| `MarqueeRenderRequest` | record struct | 1 フレーム分の幾何を解決する入力（設定・状態と、描画時に決まる値）。 `MarqueeCore.CreateRequest` が生成し、`MarqueeCore.ResolveGeometry` へ渡す。 値の組は `MarqueeRenderRequest.Create` が 1 箇所に閉じる。 **`Create` を public にする理由**: ファサード（`MarqueeCore.CreateRequest`）と独自 UI が入力を組み立てる唯一の入口である（描画先の領域を知るのは呼び出し側だけである） |
| `MarqueeHandles` / `MarqueeMarker` | record struct | ハンドル・始点マーカーの幾何（位置と寸法はコアが解決済み。 **色と線の太さは描画層の外観が持つ**） |
| `MarqueeSnapshot` | readonly record struct | 通知時点の状態（状態・選択矩形・アクティブターゲット・マウス位置・操作フラグ）。 値型のため通知のたびにヒープを確保しない |
| `MarqueeNotice` / `MarqueeNoticeKind` | record / enum | 通知（種別・通知座標・スナップショット） |
| `MarqueeHitTarget` | enum | ヒット対象（8 方向のハンドル + `Move` + `None`） |
| `MarqueeCursorKind` | enum | カーソル形状の種別（`Default` / `DiagonalNWSE` / `DiagonalNESW` / `Vertical` / `Horizontal` / `Move`） |
| `MarqueeInteractionState` | enum | 対話状態（`NoSelection` / `StartPointNudging` / `Dragging` / `DraggingNudging` / `Selected` / `SelectedNudging`） |
| `MarqueeOptions` | record | 設定（**計算に必要な値のみ**＝寸法・対話パラメータ）と既定値・補正。 **色・フォントサイズ・枠線の太さ・表示可否は持たない**（描画層の外観が単一所有する） |

### 2.2 `EsUtil.Feature.Marquee.Core.Contract`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `MarqueeEffectsBase` | abstract class | `IMarqueeEffects` の全メンバーを virtual の空実装で提供する基底クラス（必要な効果だけ override する）。 **ファサードから返らないため公開面ではない**（ホストが継承して使う） |

### 2.3 `MarqueeCore`（ファサード）のメンバー

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `MarqueeCore(IMarqueeEffects, ICursorService?, MarqueeOptions?)` | ctor | `effects` は null 不可。 `cursor` は null 可（カーソル操作を行わない）。 描画実装も外観も受け取らない（描画は UI の関心事であり、本コアは描画 API を持たない） |
| `Subscribe` | `IDisposable Subscribe(Action<MarqueeNotice>)` | 通知（`MarqueeNotice`）の購読。 解除は戻り値の `Dispose`（値型の通知は `event` を使わない） |
| `Log` | property | ロガー。 null は `NullLogger` へ吸収される |
| `Options` | property | 設定。 設定すると対話パラメータを反映し、**変化した項目をログ出力**し、境界外選択の禁止の変化時のみ即時クリップを行う |
| `ClientToSourceScaling` | property | 座標変換ポート未指定時に適用する倍率。 既定は `Limits.ClientToSourceScalingDefault`（1.0）。 設定時は 0.1〜8.0 へ補正される |
| `HighlightSourceRect` | property | ハイライト矩形（対象座標）。 null が「ハイライトしない」。 **変更は最も低いレベル（Trace）で記録する**（ホバーのたびに変わりうる最も頻度の高い設定のため） |
| `HasSelection` / `SelectionSourceRect` | property | 選択の有無と選択矩形（対象座標） |
| `CreateSnapshot(EsPoint?)` | method | 状態スナップショットの生成 |
| `SetCoordTransform(ICoordTransform?)` | method | 座標変換ポートの設定（null は等価変換） |
| `ToSource(EsPoint)` / `ToDip(EsPoint)` | method | 座標変換 |
| `Press(EsPoint, int)` / `Move(EsPoint)` / `Release(EsPoint)` | method | ポインター入力の通知（対象座標） |
| `KeyDown(EsKey, EsPoint?)` | method | キー入力の通知。 処理済みの真偽を返す（`Escape` / `Enter`（`Return` 含む）/ 矢印キー） |
| `EndNudge()` | method | nudge の終了（フォーカス喪失で使用） |
| `SetSelection(EsRect)` / `ResetSelection()` | method | 選択の復元・対話状態の初期化 |
| `RequestConfirm()` / `RequestCancel()` | method | 確定/キャンセルの要求（キー・ダブルクリックと同一経路） |
| `CreateRequest(EsSize)` | method | 幾何 1 フレーム分の入力（`MarqueeRenderRequest`）の生成（設定・状態と、描画時に決まる値） |
| `ResolveGeometry(MarqueeRenderRequest)` | method | 1 フレーム分の幾何（`MarqueeGeometry`）の解決 |

**設定では変更しない固定値はありません**（すべて `MarqueeOptions` の設定として公開します）。 ハンドルの一辺サイズと枠線の太さ・十字マーカーの長さは `MarqueeOptions` が持ち、**十字マーカーの線の太さは体裁（描画にだけ必要な設定）のため描画層の外観が持ちます**。 UI 層は解決済みの値（`MarqueeHandles.BorderThickness` 等）や描画層が返す配置指示を受け取って使います。

### 2.4 `MarqueeOptions` のメンバー

| 分類 | メンバー | 既定値 | 安全域（生成時に補正） |
| --- | --- | --- | --- |
| 寸法 | `HandleSize` | 10.0 | 1.0〜256.0 |
| 寸法 | `HandleBorderThickness` | 1.5（ハンドル枠の線の太さ。 見えている大きさを決めるため、`HandleHitRadius` と揃えて設定する） | 0.5〜64.0 |
| 寸法 | `MarkerLength` | 16.0（幾何の半幅は 1/2） | 2.0〜4096.0 |
| 対話 | `HandleHitRadius` | 12.0（DIP） | 0.0〜256.0 |
| 対話 | `MinSelectionSize` | 6.0（対象座標。 これ未満はキャンセル扱い） | 1.0〜4096.0 |
| 対話 | `ClampToSourceBounds` | `true` | — |

数値項目の補正は**本型の生成時に適用**します（`init` アクセサ。 安全域は `MarqueeCore.Limits` が単一所有する）。 設定の経路（オブジェクト初期化子・`with` 式・逆シリアル化）によらず範囲内の値だけが保持されます。

**本型は表示可否を持ちません**。 ハンドル・サイズラベル・カーソル座標ラベル・装飾を描くかどうかは、**描画の外観（描画層が単一所有する）の明示フラグ**が単一所有します（述語は `ShouldShowSizeLabel(bool)` / `ShouldShowCoordLabel()` / `ShouldDrawOverlay(bool)`）。

**本型は色もラベルのフォントサイズも持ちません**。 これらは描画にだけ必要な設定であるため、描画層の外観が単一所有します（部分変更の基点は外観の `Default`。 ハンドル枠と外枠の既定は外観が単一所有する共通の既定枠線色 = `#FFA9A9A9`、フォントサイズはサイズラベル 14.0 / カーソル座標ラベル 12.0、枠線の太さは外枠 / 内枠（既定 2.0）、内側余白は `LabelPadding`（既定 4.0））。 ラベルの文字色・背景色の既定値（白 / 黒 60%）も外観が単一所有するため、サイズラベル・カーソル座標ラベル・ルーペが同じ見た目になります。

### 2.5 `MarqueeCore.Limits` のメンバー

選択矩形の数値パラメータの既定値と安全域を単一所有し、範囲外の値を補正します（非有限は既定値）。 **数値スキーマに加えて「未設定（0 以下）の解決規則」も持ちます**（`ResolveDisplayScale`）。

| 定数 | 値 | 説明 |
| --- | --- | --- |
| `HandleSizeMin` / `HandleSizeMax` / `HandleSizeDefault` | `1.0` / `256.0` / `10.0` | ドラッグハンドルの一辺サイズ（DIP） |
| `MarkerLengthMin` / `MarkerLengthMax` / `MarkerLengthDefault` | `2.0` / `4096.0` / `16.0` | 十字マーカーの全長（DIP） |
| `HandleHitRadiusMin` / `HandleHitRadiusMax` / `HandleHitRadiusDefault` | `0.0` / `256.0` / `12.0` | ハンドルのヒットテスト半径（DIP） |
| `MinSelectionSizeMin` / `MinSelectionSizeMax` / `MinSelectionSizeDefault` | `1.0` / `4096.0` / `6.0` | 選択矩形の最小サイズ（対象座標） |
| `HandleBorderThicknessMin` / `HandleBorderThicknessMax` / `HandleBorderThicknessDefault` | `0.5` / `64.0` / `1.5` | ドラッグハンドルの枠線の太さ（DIP） |
| `BorderThicknessMin` / `BorderThicknessMax` / `BorderThicknessDefault` | `0.5` / `64.0` / `2.0` | 枠線の太さ（DIP。 描画層の外観の既定値も本値を参照する） |
| `ClientToSourceScalingMin` / `ClientToSourceScalingMax` / `ClientToSourceScalingDefault` | `0.1` / `8.0` / `1.0` | ポート未指定時に適用する倍率（環境値。 他の数値より後に置く） |

| メソッド | 説明 |
| --- | --- |
| `CoerceHandleSize(double)` / `CoerceMarkerLength(double)` / `CoerceHandleHitRadius(double)` / `CoerceMinSelectionSize(double)` | 各寸法を安全域へ補正（非有限は既定値） |
| `CoerceHandleBorderThickness(double)` | ハンドルの枠線の太さを 0.5〜64.0 へ補正（0 以下の指定を無効化する） |
| `CoerceBorderThickness(double)` | 枠線の太さを 0.5〜64.0 へ補正（0 以下の指定を無効化する。 描画層の外観が使う） |
| `CoerceClientToSourceScaling(double)` | 倍率を 0.1〜8.0 へ補正 |
| `ResolveDisplayScale(double, double)` | 表示スケールの解決（**public**。 未設定（0 以下）は倍率を使う。 描画層のサイズラベル書式が使う） |

### 2.6 `IMarqueeEffects` のメンバー

| メンバー | 説明 |
| --- | --- |
| `RequestFocus()` | コントロールへフォーカスを要求する |
| `RequestRedraw()` | 再描画を要求する |
| `SetCursorShape(MarqueeCursorKind)` | カーソル形状を設定する（UI が自 UI のカーソル型へ写像する） |
| `SetPointerCapture(bool)` | ポインターキャプチャを取得/解放する |
| `ToScreenPoint(EsPoint)` | ローカル DIP を画面座標へ変換する（nudge のカーソル移動にのみ使う） |

`MarqueeEffectsBase` はこれらの全メンバーを virtual の空実装として提供する `abstract class` です。 独自の UI アダプタは本クラスを継承し、必要な効果（例: `RequestRedraw`）だけを override します（不要なメンバーの空実装を書かなくて済む）。

### 2.7 `MarqueeNoticeKind` の値

| 値 | 発生条件 |
| --- | --- |
| `SelectionStarted` | 新規選択のドラッグを開始した |
| `SelectionChanged` | 選択矩形が変化した |
| `SelectionCompleted` | 選択が確定した（確定要求と同じ契機） |
| `SelectionCancelled` | 選択が破棄された |
| `MarkerPositionMoved` | 十字マーカーが追従する位置が変化した（汎用名。 ルーペ中心への写像は UI 層が行う） |
| `ConfirmRequested` | 確定要求を受理した |
| `CancelRequested` | キャンセル要求を受理した |

## 3. 契約（利用者が守る・受け取る値）

### 3.1 幾何（`MarqueeGeometry`）

| 項目 | 内容 |
| --- | --- |
| `ClientSize` / `ClientRect` | 描画領域のサイズ（DIP）と、原点（0, 0）からの描画面全体 |
| `FocusDipRect` | 注目矩形（DIP。 選択もハイライトも無い場合は null）。 終点の `+1` は選択した矩形の `Interval` が決める（ドラッグの選択 = 閉区間、ハイライト矩形 = 半開区間）。 暗転マスクを抜く領域である |
| `FocusSourceRect` | 注目矩形（対象座標。 選択した矩形をそのまま渡すため `Interval` も保持する）。 ホストが矩形ごとの表示スケールを解決するために使う |
| `Handles` | ドラッグハンドルの幾何（`HandleDipRects` 8 方向・`BorderThickness`）。 選択が無い場合は null |
| `Marker` | 始点マーカーの幾何（`Center` / `Size`＝半幅）。 nudge 中でない場合は null。 線の太さは描画層の外観（`MarkerThickness`）が持つ |
| `MouseSourcePosition` | 直近のマウス位置（対象座標）。 カーソル座標ラベルの文言の基準 |
| `MouseDipPosition` | 直近のマウス位置（DIP。 座標変換済み）。 カーソル座標ラベルの配置の基準 |

**幾何は「描く値の無い項目は null / 空」で返します**（注目矩形なし・選択なし・nudge 中でない）。 **表示可否（ハンドル・ラベルを描くかどうか）は本コアの責務ではありません**（描画層の外観が単一所有します）。

### 3.2 操作の受理条件

| 操作 | 受理条件 |
| --- | --- |
| `Press` のダブルクリック | 選択矩形の内側なら確定、外側ならキャンセル |
| `RequestConfirm` | ドラッグ中は無視。 選択があるときのみ確定し、確定後は選択を破棄する |
| `RequestCancel` | ドラッグ中または選択がある場合は選択を破棄してから通知する |
| `KeyDown` の矢印キー | 選択済みかつマウス非押下の場合は nudge しない（領域作成後の nudge はドラッグ中のみ） |
| `Release` | 最小サイズ未満の矩形はキャンセル扱い |

## 4. 使用例

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
```

```csharp
// 描画計画（装飾・文言・層の順序）の解決は描画層（本コアを参照する上位の層）が行う
var plan = ResolveDrawPlanByHost(new EsSize(800, 600), MeasureText);
```

## 5. 座標系

| 座標系 | 用途 |
| --- | --- |
| 対象座標 | 選択矩形・入力座標・通知座標・ハイライト矩形。 利用者が座標変換ポートで定義する |
| ローカル DIP | 幾何の座標（`FocusDipRect` / `Handles` / `Marker`）。 `ICoordTransform.ToDip` で得る |

- 選択矩形は**閉区間**（始点・終点とも含む）として扱い、DIP への変換時に終点へ +1 します。 **ハイライト矩形はホストが全ピクセルを含む閉区間として与えるため +1 しません**（優先順と閉区間の規則は `MarqueeRenderRequest` が単一所有します）。
- 対象の境界（`ICoordTransform.SourceArea`）は `ClampToSourceBounds` が有効な場合のクリップ範囲です。
- **コアは描画面の原点が常に (0, 0) である前提**で幾何を返します（親の座標を混ぜるとハンドルがずれるため、描画領域は原点を持たない `EsSize` で受けます）。

## 6. 制限事項

- ホイール操作（Ctrl+ズーム / スクロール）は本コアに含みません（組み込み先が存在して初めて意味を持つ処理であるため、UI 層（ホスト）がビューポートへ転送します）。
- ビューポートの変化の通知は本コアに含みません（同じ理由で UI 層（ホスト）が持ち、変化を検知したら `RefreshOverlay()` を呼びます）。
- 描画 API は提供しません。 描画計画（外観・文言・層の順序）は描画層（本コアを参照する上位の層）が単一所有し、描画は各 UI が行います。
- **装飾（暗転マスク・外枠・内側の塗り）の概念を持ちません**。 装飾は選択矩形単体の概念ではなく、組み込み先の表示であるため、描画層が単一所有します（本コアは注目矩形という幾何だけを返します）。
- **始点マーカーの線の太さを含みません**（描画にだけ必要な体裁であるため、描画層の外観が単一所有します。 本コアは中心と半幅だけを返します）。
- 色（ハンドル・始点マーカー・ラベル・装飾）は本コアに含みません（描画にだけ必要な設定であるため、描画層の外観が単一所有します）。
- 文言（サイズラベルの書式・カーソル座標ラベルの文字列）は本コアに含みません（描画層が単一所有します。 本コアはマウス位置という素材だけを返します）。
- 操作ラベル・情報ラベル（アプリ固有の付加情報）は本コアが関知しません。 ホストが文言の生成と描画を行います。
