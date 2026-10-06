# EsUtil.Feature.Marquee.ControlAvaloniaUI API リファレンス

## 目次

- [1. 文書概要](#1-文書概要)
- [2. 目的](#2-目的)
- [3. 外部利用条件](#3-外部利用条件)
  - [3.1 プロジェクト参照](#31-プロジェクト参照)
  - [3.2 必要な名前空間](#32-必要な名前空間)
  - [3.3 Visual Tree への配置](#33-visual-tree-への配置)
- [4. 基本的な使用方法](#4-基本的な使用方法)
  - [4.1 コードからの生成](#41-コードからの生成)
  - [4.2 結果イベントの購読](#42-結果イベントの購読)
    - [4.2.1 ラベル（文字）の表示](#421-ラベル文字の表示)
  - [4.3 座標ポートの設定](#43-座標ポートの設定)
  - [4.4 状態の操作](#44-状態の操作)
- [5. 公開API](#5-公開api)
  - [5.1 プロパティ](#51-プロパティ)
    - [5.1.1 設定（`Options`）](#511-設定options)
    - [5.1.2 状態](#512-状態)
    - [5.1.3 ログ](#513-ログ)
    - [5.1.4 描画](#514-描画)
  - [5.2 メソッド](#52-メソッド)
  - [5.3 イベント](#53-イベント)
  - [5.4 型](#54-型)
    - [5.4.1 MarqueeControl（`MarqueeControl.cs`）](#541-marqueecontrolmarqueecontrolcs)
    - [5.4.2 イベント引数（`SelectionRectEventArgs.cs` / `MarqueePointEventArgs.cs`）と共有コア型](#542-イベント引数selectionrecteventargscs--marqueepointeventargscsと共有コア型)
- [6. 座標系](#6-座標系)
  - [6.1 3 層の座標](#61-3-層の座標)
  - [6.2 座標ポート](#62-座標ポート)
  - [6.3 画像外選択の可否（`Options.ClampToSourceBounds`）](#63-画像外選択の可否optionsclamptosourcebounds)
- [7. Nudge（キーボード微調整）](#7-nudgeキーボード微調整)
  - [7.1 動作](#71-動作)
  - [7.2 対象 Pixel 保証](#72-対象-pixel-保証)
  - [7.3 nudge の許可条件](#73-nudge-の許可条件)
  - [7.4 十字マーカー（nudge 位置表示）の描画位置](#74-十字マーカーnudge-位置表示の描画位置)
  - [7.5 外側の装飾と選択矩形の一致（閉区間の共有）](#75-外側の装飾と選択矩形の一致閉区間の共有)
  - [7.6 nudge の終了](#76-nudge-の終了)
- [8. ポインター操作](#8-ポインター操作)
- [9. 制約事項](#9-制約事項)

---

## 1. 文書概要

| 項目 | 内容 |
| --- | --- |
| 対象 | `MarqueeControl` |
| プロジェクト | `src/EsUtil.Feature.Marquee.ControlAvaloniaUI/src/AvaloniaUI/EsUtil.Feature.Marquee.ControlAvaloniaUI.csproj` |
| 名前空間 | `EsUtil.Feature.Marquee.AvaloniaUI` |
| 対象フレームワーク | `net10.0` |
| UIフレームワーク | Avalonia 12.1 |
| 主要基底クラス | `Avalonia.Controls.Control` |
| 主な用途 | 領域選択ドラッグ矩形の UI・操作・描画を自己完結するコントロール |

## 2. 目的

`MarqueeControl` は、マウス操作・キーボード操作（nudge）・描画をコントロール内で完結する独立した Avalonia コントロールです。

次の機能を提供します。

- マウスドラッグによる領域選択
- 既存選択のドラッグ移動
- ドラッグハンドルによるリサイズ
- 矢印キーによるキーボード微調整（nudge）
- ドラッグハンドル・始点マーカー・カーソル座標ラベルの描画
- 選択矩形の**外側に描く装飾**（暗転マスク・外枠・ハイライト・内側の塗り）の描画（表示可否・太さ・色は外観（`Appearance`）が、装飾矩形と描画順は提示層（`EsUtil.Feature.Marquee.Presenter`）が単一所有する。 描画は本コントロールが行う）
- ダブルクリックの確定/キャンセル判定（矩形内 = 確定 / 矩形外 = キャンセル）
- 対象座標系での正確な nudge（矢印キー1回 = 対象1Pixel）

## 3. 外部利用条件

### 3.1 プロジェクト参照

```xml
<ProjectReference Include="..\EsUtil.Feature.Marquee.ControlAvaloniaUI\src\EsUtil.Feature.Marquee.ControlAvaloniaUI.csproj" />
```

### 3.2 必要な名前空間

```csharp
using Avalonia;
using EsUtil.Feature.Marquee.AvaloniaUI;
using EsUtil.Feature.Marquee;          // MarqueeCore / MarqueePresenter（ファサード）・MarqueeOptions・MarqueeAppearance
```

座標ポート（`ICoordTransform`）を使用する場合は、`EsUtil.Platform.Core` の名前空間も必要です。

```csharp
using EsUtil.Platform.Core.Coord; // ICoordTransform / CoordTransform
using EsUtil.Platform.Core.Geometry;   // EsPoint / EsSize
```

### 3.3 Visual Tree への配置

`MarqueeControl` は `Control` であり、フォーカス可能です。

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:marquee="clr-namespace:EsUtil.Feature.Marquee.AvaloniaUI;assembly=EsUtil.Feature.Marquee.ControlAvaloniaUI">
    <Canvas>
        <marquee:MarqueeControl x:Name="SelectionOverlay" />
    </Canvas>
</Window>
```

外観（色・フォントサイズ・枠線の太さ・表示可否）は外観の単一値（`Appearance`）、計算に必要な設定（寸法・対話パラメータ）は設定値（`Options`）にまとまっているため、AXAML の属性では指定しません（コードまたはバインディングで設定します）。

## 4. 基本的な使用方法

### 4.1 コードからの生成

```csharp
var marquee = new MarqueeControl
{
    IsHitTestVisible = true,
    ZIndex = 60,
    // 計算に必要な設定（寸法・対話パラメータ）は 1 回でまとめて渡す（同値の再設定では再描画されない）
    Options = MarqueeOptions.Default with
    {
        HandleSize = 10.0,
        HandleHitRadius = 12.0,
        MinSelectionSize = 1.0,
    },

    // 外観（色・フォントサイズ・枠線の太さ・表示可否）は提示層の 1 値が単一所有する
    Appearance = MarqueeAppearance.Default with
    {
        // 選択枠線は装飾（暗転マスクの外枠）が描く（色と太さと表示可否をここで与える）
        OuterBorderColor = Colors.White.ToDrawingColor(),
        HandleColor = Colors.White.ToDrawingColor(),
        OuterBorderThickness = 2.0,
        ShowOverlay = true,
        ShowOuterBorder = true,
    },
};

parentPanel.Children.Add(marquee);
```

### 4.2 結果イベントの購読

操作の結果は公開イベントで通知されます。 上位 AP はマウス・キーイベントを直接処理する必要はありません。

```csharp
marquee.SelectionChanged += (_, e) => UpdatePreview(e.Rect);
marquee.SelectionStarted += (_, e) => OnDragStarted(e.Point);
marquee.SelectionCompleted += (_, e) => OnSelectionCompleted(e.Rect);
marquee.SelectionCancelled += (_, _) => OnSelectionCancelled();
marquee.LoupePositionMoved += (_, e) => UpdateLoupe(e.Point);
marquee.ConfirmRequested += (_, e) => ConfirmSelection(e.Rect);
marquee.CancelRequested += (_, _) => CancelSelection();
```

`Escape` / `Enter` キーはコントロールが自己処理し、それぞれ `CancelRequested` / `ConfirmRequested` を発火します。 上位はこのイベントを購読して後始末（確定・キャンセル後の処理）を行います。

#### 4.2.1 ラベル（文字）の表示

サイズラベルとカーソル座標ラベルの**文言と基準点は提示層の `MarqueePresenter`（`LabelTexts` partial） が、色とフォントサイズは外観（`MarqueeAppearance`）が、実寸から配置矩形を求める写像は提示層の `MarqueePresenter`（`LabelPlan` partial） が単一所有**します。 コントロールは `Border` + `TextBlock` を子要素として置き（自 UI の API で実寸を測って渡す）、**ラベルは常に最上位**に表示されます。 操作・情報ラベルはアプリ固有の付加情報のためホストが `Rendered` のハンドラで自前で描きます（配置指示は `FadeMaskDragRectControl.UIBase` の `FadeMaskHostLabelRenderer` が単一所有する）。
描画のたびに `Rendered` が発火するため、ホストはそのハンドラで追加描画できます（引数の `Content` に装飾・注目矩形・ハンドル・マーカー・サイズラベル・座標ラベル・層の順序が含まれます）。

```csharp
using EsUtil.Feature.Marquee.AvaloniaUI;

// 文字は UI 層が配置したテキスト要素が表示する（図形のみコントロールが描く）
marquee.Rendered += (_, e) =>
{
    // 図形（装飾・ハンドル・マーカー）はコントロールが描く。
    // 文字は Content.Layers の順に従い、UI 層が配置したテキスト要素が表示する。
};
```

- `Rendered` はコントロールの描画中に発火します。 ハンドラ内で描画を完了させてください（`DrawingContext` を保持しない）。
- 文字が無い場合（ラベルも座標ラベルも無い）は発火しません。
- `MarqueeRenderedEventArgs` は**描画計画全体**（`Content` = `MarqueeDrawPlan`。 外観・注目矩形・装飾矩形・ハンドル・マーカー・層の順序・ラベル配置）を運びます。 ホストは付加情報（操作・情報ラベル）の描画に使えます（図形はコントロールが描き終えているため、描き直すと二重描画になります。 ラベルの有無は `Content.HasText` で判定できます）。

### 4.3 座標ポートの設定

選択状態と nudge は対象座標で動作し、マウス入力・描画はコントロールローカル DIP で動作します。 両者を変換する座標ポート（`ICoordTransform`）をホストが設定します。 軸は「対象座標 ↔ ローカル DIP」の 1 つだけで、ポートの生成は `EsUtil.Platform.Core`（`EsUtil.Platform.Core.Coord`）の `CoordTransform` ファクトリ（`Identity` / `ClientToSource`）やホスト固有の実装で行います。 座標はコア幾何型（`EsPoint` / `EsSize`）で扱うため、Avalonia 型を渡す場合は相互変換（`.ToEsSize()` 等）します。

```csharp
// AreaSelect 例: 対象 = 仮想スクリーンピクセル（= DIP × scaling・動的評価）
control.SetCoordTransform(CoordTransform.ClientToSource(() => scaling));

// 等価変換（1 対象 = 1 DIP）
control.SetCoordTransform(null);
```

未設定時は等価変換（1 対象 = 1 DIP）が使用されます。

### 4.4 状態の操作

選択状態（選択矩形・nudge 状態）は公開イベント（`SelectionChanged` 等）の Snapshot で通知されます。 ホストは状態をポーリングせず、イベント駆動で把握します。

```csharp
control.SetSelection(rect);         // 外部からの復元
control.ResetSelection();           // 状態と nudge の初期化
```

外部復元した矩形（`SetSelection`）は、**`Options.ClampToSourceBounds` が有効な場合は画像外にはみ出す矩形を境界内にクリップしてから設定**します（「画像外の選択を禁止」を外部復元でも尊重）。 また、`Options.ClampToSourceBounds` が ON に切り替わった瞬間にも、既存の選択矩形は境界内へ即時クリップされます（ドラッグ開始を待たない）。 継続的なクリップは座標ポートの `Bounds` + `Options.ClampToSourceBounds` が担います。

## 5. 公開API

公開 API は「プロパティ / メソッド / イベント / 型」の 4 分類で構成されます。
座標を扱う API は、対象座標系 / DIP 座標系のどちらであるかを明記します。

### 5.1 プロパティ

#### 5.1.1 設定（`Options`）

寸法・対話パラメータは不変の設定値 `MarqueeOptions`（コアの `EsUtil.Feature.Marquee.MarqueeOptions`）に、外観（色・フォントサイズ・枠線の太さ・表示可否）は不変の外観値 `MarqueeAppearance`（提示層の `EsUtil.Feature.Marquee.MarqueeAppearance`）にまとめて公開し、`Options` / `Appearance` プロパティで設定します。

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Options` | `MarqueeOptions` | `MarqueeOptions.Default` | 選択矩形の寸法・表示可否・対話パラメータをまとめた設定。 変更すると再描画され、対話パラメータはコア（ファサード）へ伝搬する（**色は含まない**） |
| `Options` | `MarqueeOptions` | `MarqueeOptions.Default` | 選択矩形の**計算に必要な設定**（寸法・対話パラメータ）。 変更すると再描画され、対話パラメータはコア（ファサード）へ伝搬する（**色・表示可否は含まない**） |
| `Appearance` | `MarqueeAppearance` | `MarqueeAppearance.Default` | **外観（色・フォントサイズ・枠線の太さ・表示可否）の設定**。 変更すると次の描画で提示層が新しい外観で描画計画を解決する |
| `SizeLabelOverrideProvider` | `Func<string?>?` | `null` | サイズラベル（1 行目）の文言を描画時に評価するプロバイダ。 **文言はホストが与える**（コアは特定の文言を持たない。 文言の生成はホストの `CaptureLabelTexts` 等が担う）。 null または空文字を返す場合は自動生成の文言を使う。 変更すると保持している描画内容を破棄して再描画を要求する |

`MarqueeOptions` の項目（既定値は本型が単一所有する。 すべて `init` のみで不変）:

| 項目 | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `HandleSize` | `double` | `10.0` | ドラッグハンドルの一辺サイズ（DIP 座標系。 1.0〜256.0 に補正） |
| `HandleBorderThickness` | `double` | `1.5` | ドラッグハンドルの枠線の太さ（DIP 座標系。 0.5〜64.0 に補正。 見えている大きさを決めるため `HandleHitRadius` と揃えて設定する） |
| `MarkerLength` | `double` | `16.0` | nudge 中の十字マーカーの長さ（**全長** = 中心から両端までの長さ・DIP 座標系。 2.0〜4096.0 に補正） |
| `HandleHitRadius` | `double` | `12.0` | ハンドルヒットテスト半径（**DIP 座標系**。 0.0〜256.0 に補正。 ズーム・縮小表示でも画面上の大きさが一定） |
| `MinSelectionSize` | `double` | `6.0` | 選択矩形の最小サイズ（対象座標系。 1.0〜4096.0 に補正） |
| `ClampToSourceBounds` | `bool` | `true` | 画像外の選択を禁止するかどうか（ON: 領域選択・移動・nudge を画像境界内にクリップ / OFF: 画像外も選択可能） |

安全域への補正は**コアの設定型の生成時**に適用されます（設定の経路によらず範囲内の値だけが保持され、UI 側に補正の登録は不要です）。

`Options` は変更時に即時反映されます（`AffectsRender` による再描画 + コア（ファサード）への伝搬）。
色は外観（`Appearance` の `MarqueeAppearance`）が単一所有し、UI 非依存の `System.Drawing.Color` で保持します（Avalonia 型からの変換は `EsUtil.SaveEvidence.Platform.AvaloniaUI` の `ToDrawingColor()`。 描画時に Avalonia の色型へ変換します）。 **描くかどうかは色のアルファではなく外観の明示フラグ（`ShowOverlay` / `ShowOuterBorder` / `ShowInnerFill` / `ShowInnerBorder` / `ShowFullOverlay`）が決めます**。
一部の項目だけを変える場合は `MarqueeOptions.Default with { ... }` / `MarqueeAppearance.Default with { ... }` で指定します（指定しない項目は既定値を保つ）。 フォントサイズは外観の生成時に 6〜48 へ補正されます。

`MarqueeAppearance` の項目（既定値は本型が単一所有する。 すべて `init` のみで不変）:

| 項目 | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `HandleColor` | `Color` | `White` | ドラッグハンドルの塗り色（全成分 0 を指定した場合は `White` へ解決される） |
| `HandleBorderColor` | `Color` | `#FFA9A9A9` | ドラッグハンドルの枠線色（外枠と共通の既定値） |
| `MarkerColor` | `Color` | `White` | nudge 中の十字マーカーの色 |
| `SizeLabelColor` / `SizeLabelBackgroundColor` | `Color` | `White` / `#99000000`（黒 60%） | サイズラベルの文字色と背景色 |
| `SizeLabelFontSize` / `CoordLabelFontSize` | `double` | `14.0` / `12.0` | サイズラベル（1 行目）とカーソル座標ラベルのフォントサイズ（DIP。 6〜48 に補正。 非数は既定値へ補正） |
| `CoordLabelColor` / `CoordLabelBackgroundColor` | `Color` | `White` / `#99000000`（黒 60%） | カーソル座標ラベルの文字色と背景色 |
| `OverlayColor` | `Color` | `#80000000`（黒 50%） | 暗転マスクの色 |
| `OuterBorderColor` | `Color` | `#FFA9A9A9` | 外枠（選択枠線）の色 |
| `InnerFillColor` / `InnerBorderColor` | `Color` | 透明 / 透明 | 注目矩形の内側の塗り色と枠線色 |
| `OuterBorderThickness` / `InnerBorderThickness` | `double` | `2.0` | 外枠・内側の枠線の太さ（DIP。 0.5〜64 に補正） |
| `ShowHandles` / `ShowMarker` / `ShowSizeLabel` / `ShowCoordLabel` | `bool` | `true` | ハンドル・始点マーカー・サイズラベル・カーソル座標ラベルを描くかどうか |
| `ShowOverlay` / `ShowOuterBorder` | `bool` | `true` | 暗転マスク・外枠を描くかどうか |
| `ShowFullOverlay` | `bool` | `false` | 注目矩形が無くても全面を暗転するかどうか |
| `ShowInnerFill` / `ShowInnerBorder` | `bool` | `false` | 注目矩形の内側の塗り・枠線を描くかどうか |
| `LabelPadding` | `double` | `4.0` | ラベルの内側余白（DIP。 行送りも本値から導く） |

#### 5.1.2 状態

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `CurrentSnapshot` | `MarqueeSnapshot` | - | 現在の選択状態のスナップショット（**ポーリング不要の完全状態**。 `readonly record struct`）。 `State` / `SelectionSourceRect` / `ActiveTarget` / `MousePosition` / `IsMouseDown` / `IsNudgeActive` / `HasSelection` を公開。 イベント引数の Snapshot と同一形式。 選択状態の判定（確定・キャンセル等の操作分岐）は `CurrentSnapshot.HasSelection` を使用 |
| `HasSelection` | `bool` | `false` | 選択矩形があるかどうか（スナップショットの生成を伴わない軽量な読み取り。 描画のたびに参照する） |
| `SelectionSourceRect` | `EsRect` | `0x0` | 現在の選択矩形（対象座標。 未選択時は 0x0） |

#### 5.1.3 ログ

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `Log` | `ILogger` | 操作ログ用ロガー（既定は `NullLogger`。 出力先・レベル制御はホストアプリケーションが所有） |
| `LogPrefix` | `string?` | ログの接頭辞（ロガーと一緒に設定する。 null・空白で既定値 `[Marquee]` へ戻す。 提示層・コアへ伝搬する） |

操作ログの書式・レベルは MewUI 版と同一です。 ログはコア（`EsUtil.Feature.Marquee.Core.Logging.MarqueeLogMessage`）と提示層（`EsUtil.Feature.Marquee.Presenter.Logging.MarqueeLogMessage`）が定義し、ロガーはファサード（`MarqueeCore.Log`）が保持します。 Debug で操作（MouseDown / MouseUp / DoubleClick / DragStart / KeyDown / ConfirmRequested / CancelRequested）と**設定の変更**（`[Marquee] Setting: {名前} {前} → {後}`。 変化した項目だけ）を、Trace で位置の記録を出力します。 本プロジェクト自身は、公開操作を提示層へ委譲した際の例外（`MarqueeLogMessage.LogOperationError`。 Error）だけを定義します。

#### 5.1.4 描画

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `Appearance` | `MarqueeAppearance` | 外観の単一値（既定は `MarqueeAppearance.Default`）。 外観を差し替えると次の描画から反映される |
| `DrawPlanResolver` | `Func<MarqueeDrawPlan, MarqueeDrawPlan>?` | **描画計画の差し替え点**（矩形の並び・文言・位置）。 設定すると保持中の計画を破棄して再解決する（同じ入力では 1 フレーム 1 回だけ解決されるため、呼び出しの時点に依存しない純粋関数にすること） |

```csharp
marquee.Appearance = MarqueeAppearance.Default with { OuterBorderColor = Colors.White.ToDrawingColor() };   // 外観の差し替え
```

**見た目のカスタマイズ**: 色・枠線の太さ・表示可否は外観（`Appearance`。 `MarqueeAppearance`）で指定します（**表示可否が描画の停止の唯一の入口**です。 表示を止めても計画は矩形と層を保ち、描画側（コントロールの描画実装）がフラグを見て描かないため、ホストは止めた項目も計算値を使って自前で描けます）。 矩形の並びで表せる装飾とラベルの文言・位置は `DrawPlanResolver`（`Func<MarqueeDrawPlan, MarqueeDrawPlan>`）で描画計画（`MarqueeDrawPlan`）を差し替えられます（差し替えは値の置き換えだけで完結し、描画実装（本 UI の `MarqueeContextPainter`）は変更不要です。 例は `sample/Presentation/` の `GuideMarqueePlan`）。 矩形で表せない描画（円・斜線等）を加える場合は外観の `Show*` を false にして `Rendered` の描画先へ自前で描き、必要なら `DrawPlan` で計画どおりの描画を行います。

### 5.2 メソッド

| メソッド | 説明 |
| --- | --- |
| `SetSelection(Rect)` | 選択矩形を対象座標で復元する（`SelectionChanged` で通知。 `Options.ClampToSourceBounds` 有効時は境界内へクリップ） |
| `ResetSelection()` | 選択状態と nudge 状態を初期化する |
| `SetCoordTransform(ICoordTransform?)` | 対象↔DIP 座標変換ポートを設定する（null = 等価変換） |
| `RefreshOverlay()` | 表示（パン・ズーム・表示サイズ）の変化へ追従する（再描画とルーペ位置の再計算を要求。 選択の状態は変更しない） |
| `InvalidateRender()` | 描画内容の再解決と、図形・ラベルの再描画を要求する（**描画に関わる変更の唯一の入口**。 図形は子の描画層が描くため、親の無効化だけでは更新されない） |
| `RequestCancel()` | キャンセル要求を処理する（Escape キーと同じ条件。 選択破棄後に `CancelRequested` を発火） |
| `RequestConfirm()` | 確定要求を処理する（Enter キーと同じ条件。 ドラッグ中は無視し `ConfirmRequested` を発火。 確定後に選択状態をクリアして再選択・再確定を防ぐ） |
| `DrawPlan(DrawingContext, in MarqueeDrawPlan)`（static） | 描画計画を自 UI の描画面へ描く（計画どおりに描く。 表示を止めた項目はフラグを見て描かないため描かれない） |

クリップ（画像境界内への制限）は座標ポートの `Bounds` + `Options.ClampToSourceBounds` が担います。

### 5.3 イベント

| イベント | 引数 | 説明 |
| --- | --- | --- |
| `SelectionChanged` | `SelectionRectEventArgs` | 選択矩形が変更された（`Rect` は対象座標） |
| `SelectionStarted` | `MarqueePointEventArgs` | 新規選択のドラッグ開始（始点） |
| `SelectionCompleted` | `SelectionRectEventArgs` | 選択矩形が確定した |
| `SelectionCancelled` | `EventArgs` | 領域がキャンセルされた |
| `LoupePositionMoved` | `MarqueePointEventArgs` | ルーペが表示する中心座標（対象座標） |
| `LoupePositionMoved` | `MarqueePointEventArgs` | ドラッグ・nudge 中のルーペ中心位置（対象座標。 リサイズ: アクティブハンドル点 / 移動: 掴んだマウス位置 / 新規選択・nudge: 終点） |
| `ConfirmRequested` | `SelectionRectEventArgs` | Enter キーによる確定要求 |
| `CancelRequested` | `EventArgs` | Escape キーによるキャンセル要求 |
| `Rendered` | `MarqueeRenderedEventArgs` | 描画完了の通知（描画のたびに発火）。 `Context`（`DrawingContext`）と描画計画（`Content` = `MarqueeDrawPlan`）を渡す。 **サイズ・カーソル座標ラベルは本コントロールがテキスト要素として表示する**ため、ホストは付加情報（操作・情報ラベル）の描画に使う |

すべての座標イベントは対象座標で通知されます。

### 5.4 型

公開型は `MarqueeControl` / `MarqueeHitTarget` / `MarqueeInteractionState` / `MarqueeSnapshot` とイベント引数（`SelectionRectEventArgs` / `MarqueePointEventArgs`）です。
`MarqueeHitTarget` / `MarqueeInteractionState` / `MarqueeSnapshot` は UI 非依存のコア（`EsUtil.Feature.Marquee.Core`、名前空間 `EsUtil.Feature.Marquee`）が単一所有し、各 UI 実装で共有します。
コア（`EsUtil.Feature.Marquee.Core`）の公開型は 2 層です（内部契約（状態機械・描画入力の組み立て・幾何）は internal）。

- **ホスト設定**: `MarqueeOptions` / `MarqueeHitTarget` / `MarqueeInteractionState` / `MarqueeSnapshot`
- **公開ファサード（操作の唯一の窓口）**: `MarqueeCore`（操作・設定・効果の適用・描画内容の解決）/ `IMarqueeEffects`（効果の適用先）/ `MarqueeEffectsBase`（効果の既定実装。 必要な効果だけを override できる）
- **UI アダプタ契約**: `MarqueeCursorKind` / `MarqueeNotice` / `MarqueeGeometry` / `MarqueeHandles` / `MarqueeMarker`

**装飾（暗転マスク・外枠・ハイライト・内側の塗り）は提示層（`EsUtil.Feature.Marquee.Presenter`）が単一所有**します（`MarqueeAppearance`（表示可否・太さ・色）/ `MarqueeDrawPlan`（装飾矩形）/ `MarqueePresenter`（`DrawPlan` partial）（矩形の計算）/ `MarqueePresenter`（描画計画の解決））。 本コントロールは `Appearance` で外観を設定し、`Rendered` で描画計画を受け取ります。 **描画するラベルと描画順も提示層が、実寸 → 配置矩形の写像は同層の `MarqueePresenter`（`LabelPlan` partial） が単一所有し**、実寸測定だけを UI 層が与えます。

効果（`MarqueeFeedback` / `MarqueeCursorActions`）とログ定義（`MarqueeLogMessage`）はコアの内部契約（internal）であり、本プロジェクトは参照しません（適用はファサード、記録は状態機械が行います）。

描画計画（層の順序・装飾矩形・文言・配置）と外観（`MarqueeAppearance`）は UI 非依存の提示層（`EsUtil.Feature.Marquee.Presenter` の `MarqueePresenter` / `MarqueeDrawPlan` / `MarqueePresenter`（`LabelPlan` partial））が単一所有し、**描画は本コントロールが自 UI の描画 API（`DrawingContext`）で行います**。 本プロジェクトの内部実装も internal です。

#### 5.4.1 MarqueeControl（`MarqueeControl.cs`）

公開 API は「5.1 プロパティ」「5.2 メソッド」「5.3 イベント」に記載の通りです。
StyledProperty は同ファイルに定義されます（Avalonia のバインディング・スタイル設定に必要なため public）。

#### 5.4.2 イベント引数（`SelectionRectEventArgs.cs` / `MarqueePointEventArgs.cs`）と共有コア型

| 型 | 説明 |
| --- | --- |
| `MarqueeInteractionState` enum | 操作状態（NoSelection / StartPointNudging / Dragging / DraggingNudging / Selected / SelectedNudging）。 コアが所有 |
| `MarqueeSnapshot` readonly record struct | 状態 Snapshot（State / SelectionSourceRect / ActiveTarget / MousePosition / IsMouseDown / IsNudgeActive。 `HasSelection` は導出プロパティ）。 値セマンティクスで比較可能。 **`SelectionSourceRect` は `EsRect`、`MousePosition` は `EsPoint`**（UI 非依存）。 ドラッグ中かどうかは `State`（`Dragging` / `DraggingNudging`）で判定する |
| `SelectionRectEventArgs` | 選択矩形通知（`Rect` は Avalonia 型・対象座標系。 `Snapshot` を同梱） |
| `MarqueePointEventArgs` | 座標通知（`Point` は Avalonia 型・対象座標系。 `Snapshot` を同梱） |
| `MarqueeRenderedEventArgs` | 描画完了通知（`Context` = `DrawingContext`（描画中のみ有効）/ `Content` = `MarqueeDrawPlan`（外観・注目矩形・装飾矩形・ハンドル・マーカー・層の順序・ラベル配置）） |
| `MarqueeHitTarget` enum | ドラッグハンドル種別（カーソル形状マッピング用）。 コアが所有 |

## 6. 座標系

### 6.1 3 層の座標

| 座標系 | 用途 |
| --- | --- |
| 対象座標 | 選択状態・nudge・イベント通知 |
| コントロールローカル DIP | マウス入力・描画 |
| 画面物理 Pixel | カーソル操作・画面変換 |

### 6.2 座標ポート

`ICoordTransform`（`EsUtil.Platform.Core` 提供）が対象↔DIP 変換と対象の境界を提供します。

| メンバー | 説明 |
| --- | --- |
| `ToSource(Point)` | DIP → 対象 |
| `ToDip(Point)` | 対象 → DIP |
| `SourceArea` | 対象座標の境界（null = 無制限） |
| `Bounds` | 対象のクリップ範囲（null = 無制限） |

変換関数は呼び出し毎に評価されるため、ズーム・スクロールの動的変化を反映できます。

### 6.3 画像外選択の可否（`Options.ClampToSourceBounds`）

`Options.ClampToSourceBounds` で、画像（対象の境界）の外側を矩形選択可能にするか制御します。

| 値 | 動作 |
| --- | --- |
| `true`（既定） | 選択を画像範囲内に制限する |
| `false` | 画像外も選択可能。 ドラッグで画像外に矩形を作成・移動できる |

`true` の場合のクリップ仕様:

| 操作 | クリップ動作 |
| --- | --- |
| 領域選択（新規選択ドラッグ） | 終点（ドラッグ位置）を境界内にクリップしてから矩形を作成 |
| 領域選択（ハンドル変形） | 固定角（ハンドルと反対側の角）を動かさず、動く辺のみ境界内に制限 |
| 領域移動（Move ドラッグ・nudge） | 矩形全体が境界内に収まるようにシフト（サイズは維持） |

クリップ範囲は `SetCoordTransform` のポートの `SourceArea`（`ICoordTransform.SourceArea`）で指定します（null = 無制限の場合はクリップされません）。

## 7. Nudge（キーボード微調整）

### 7.1 動作

`MarqueeControl` がフォーカスを持つ状態で矢印キーを押すと、選択状態に応じて次の動作を行います。

| 状態 | 動作 |
| --- | --- |
| 未選択 | 基点（始点）を移動 |
| ドラッグ中・矩形確定後 | 終点を移動 |
| ハンドル選択中 | ハンドル方向に矩形を変形 |

### 7.2 対象 Pixel 保証

nudge は対象座標の整数加算のみで計算します。

```text
矢印キー1回 = 対象座標の1Pixel移動
```

これは次の設計で保証されます。

- 移動量 = 差分（±1）の整数加算のみ（FP 誤差を累積しない）
- クリップ時は実移動量のみ反映

### 7.3 nudge の許可条件

- 領域作成前: 始点決めの nudge として常に許可。
- 領域作成後: マウスボタン押下中（ドラッグ中・ハンドル選択中）のみ許可。 ドラッグ完了後（マウス Down していない状態）の nudge は無効。

### 7.4 十字マーカー（nudge 位置表示）の描画位置

nudge 中は十字マーカー（色は `Appearance.MarkerColor`、全長は `Options.MarkerLength` で指定）を **ドラッグ中のハンドル位置** に描画します（`GetMarkerPosition`）。 ルーペ位置（`LoupePositionMoved`）も同じ位置を追従します。

ドラッグ中も同じ位置（`GetMarkerPosition`）をルーペ中心に通知します。 これにより、リサイズ中のハンドル点・移動中の掴んだマウス位置・新規選択の終点がルーペで拡大確認できます。

| 状態 | 十字マーカー・ルーペの追従位置 |
| --- | --- |
| リサイズ（8 方向ハンドル選択中） | アクティブハンドルの現在位置（コアが通知する追従位置） |
| 移動（Move ドラッグ中） | **掴んだマウス位置（`LastMousePosition`）**（矩形中心に飛ばず、マウスポインタに追従する） |
| 新規選択ドラッグ中・nudge | 終点（ドラッグ中の位置） |
| 未選択（始点決め nudge） | 始点 |

十字マーカーとルーペ中心は、矩形の固定位置（左上等の隅）ではなく**ドラッグ中の操作点（ハンドル / 掴んだ位置 / 終点）**を追従します。 追従位置の算出はコアが単一所有します（本コントロールはコアが通知する位置を写像するだけです）。 これにより、マーカーは常にドラッグ中の操作位置に表示されます。

### 7.5 外側の装飾と選択矩形の一致（閉区間の共有）

選択領域は **閉区間（始点・終点の両ピクセルを含む）** を意図する仕様です。

選択矩形は対象座標で半開区間（`幅 = 終点 − 始点`、.NET `Rect` と同一）で保持されますが、描画では **終点（`Right` / `Bottom`）に +1 してから** `ToDip` で DIP 変換します。

```text
描画用 DIP 矩形 = Rect(ToDip(始点), ToDip(終点 + (1, 1)))
```

ハンドルはこの矩形に対して描画され、**選択枠線は外側の装飾の外枠**（同じ閉区間 +1 の規則を装飾コアが単一所有する）が描きます。
どちらも同じ規則を使うため、枠線とハンドルの位置・キャプチャ（閉区間 +1）が一致します。

**注意**: 本コントロールは選択枠線を自分では描画しません（`MarqueeCore` 側で 2 本目を描くと線幅が二重になり見た目が変わるため）。
枠線を表示するには `Appearance = MarqueeAppearance.Default with { ShowOuterBorder = true, OuterBorderThickness = 2.0 }`（太さと表示可否）と `OuterBorderColor`（色）を設定します。

### 7.6 nudge の終了

nudge 中は OS カーソルを非表示にし、十字マーカーを追従位置へ表示します。 次のいずれかで終了し、カーソルを nudge 位置へ戻して再表示します。

| 契機 | 説明 |
| --- | --- |
| 左ボタン解放 | ドラッグの終了（nudge 位置へカーソルを戻して再表示する） |
| 左ボタン押下・Escape・Enter | 次の操作の開始（押下位置・キャンセル・確定を優先する） |
| `SetSelection` / `ResetSelection` | 外部からの状態の復元・初期化 |
| フォーカス喪失 | 別のコントロールへフォーカスが移った（nudge を継続できない文脈でカーソルを隠したままにしない） |
| ウィンドウの非アクティブ化 | Alt+Tab 等（キー入力を継続できない。 Win32 の `WM_KILLFOCUS` に相当） |
| ビジュアルツリーからの切断 | ウィンドウクローズ・Crop 終了等（`ResetSelection` を呼ぶ） |

判断（nudge を終了するか・どの位置へ戻すか）はコアが単一所有し、本コントロールは契機を通知するだけです。

## 8. ポインター操作

- 左ボタン押下: 新規選択・ハンドルドラッグ・移動ドラッグの開始（クリック回数 2 以上はダブルクリック検出）
- 左ボタン移動: ドラッグ中の選択矩形更新
- 左ボタン解放: 選択確定（最小サイズ未満はキャンセル）
- ダブルクリック（矩形内）: `ConfirmRequested` を発火し、確定後に選択状態をクリア（`ResetSelection`。 確定後も選択矩形が残るのを防ぐ）
- マウスホイール: **本コントロールは扱いません**（`e.Handled` を立てずに素通しし、上位の Canvas や祖先 `ScrollViewer` へ届きます）。 ズーム・パンの解釈と実処理は**ホスト**が行います（例: ホストが `PointerWheelChanged` を購読し、Ctrl 併用は `ZoomViewControl.ZoomByWheel`、それ以外は `PanByWheel` を呼ぶ）

## 9. 制約事項

- 矢印キーによる nudge はフォーカスに依存しません（`TopLevel` のキーイベントを**トンネル**で購読し、フォーカスがどこにあっても 1 回だけ処理します。 コントロール自身のキーハンドラは持たないため、キーが二重に処理されることはありません）。
- `Escape` / `Enter` はコントロールが自己処理し、`CancelRequested` / `ConfirmRequested` を発火します（上位はイベント購読で後始末を行う）。 プログラムからは `RequestCancel()` / `RequestConfirm()` で同一条件の要求を発行できます。
- `Options.HandleHitRadius` の単位は **DIP 座標**、`Options.MinSelectionSize` の単位は対象座標です。
- 既定のカーソルサービスは Windows 実装（`WinCursorService`）です。 他プラットフォームでは `ICursorService` を注入してください。
- リサイズハンドル（8 方向）でマウス Down すると、ポインタ（OS カーソル）がハンドル点へスナップされます（ポインタ・矩形の角・ルーペを一体にするため）。 移動（Move）の Down ではスナップされません。
- nudge 中のカーソル移動は OS カーソル位置を変更します。
