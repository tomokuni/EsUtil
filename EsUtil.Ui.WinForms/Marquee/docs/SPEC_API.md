# EsUtil.Ui.WinForms.Marquee API リファレンス

`MarqueeControl`（リサイズ可能なドラッグ矩形のコントロール）の公開 API の仕様です。 座標系と単位、型ごとのメンバー、使用例、制限事項を扱います。

## 目次 <!-- omit from toc -->

- [1. 概要](#1-概要)
- [2. 依存関係](#2-依存関係)
- [3. 座標系と単位](#3-座標系と単位)
- [4. `MarqueeControl`](#4-marqueecontrol)
  - [4.1 プロパティ](#41-プロパティ)
  - [4.2 メソッド](#42-メソッド)
  - [4.3 イベント](#43-イベント)
- [5. イベント引数](#5-イベント引数)
  - [5.1 `MarqueePointEventArgs`](#51-marqueepointeventargs)
  - [5.2 `SelectionRectEventArgs`](#52-selectionrecteventargs)
  - [5.3 `MarqueeRenderedEventArgs`](#53-marqueerenderedeventargs)
- [6. 内部型（本アセンブリの実装）](#6-内部型本アセンブリの実装)
  - [6.1 `MarqueeGraphicsPainter`（内部）](#61-marqueegraphicspainter内部)
  - [6.2 `MarqueeLogMessage`（内部）](#62-marqueelogmessage内部)
  - [6.3 描画に使うスケーリング係数の解決（コア）](#63-描画に使うスケーリング係数の解決コア)
  - [6.4 `Internal.EsGeometryWinFormsInterop`（内部）](#64-internalesgeometrywinformsinterop内部)
  - [6.5 `Internal.EsKeyWinFormsInterop`（内部）](#65-internaleskeywinformsinterop内部)
  - [6.6 `Internal.GraphicsFill`（内部）](#66-internalgraphicsfill内部)
  - [6.7 `Internal.GraphicsText`（内部）](#67-internalgraphicstext内部)
- [7. 使用例](#7-使用例)
- [8. 制限事項](#8-制限事項)
- [9. エラー・例外](#9-エラー例外)

## 1. 概要

本アセンブリは WinForms 用の選択矩形コントロールとその通知の型を公開します。

| 名前空間 | 型 |
| --- | --- |
| `EsUtil.Ui.WinForms.Marquee` | `MarqueeControl` / `MarqueePointEventArgs` / `SelectionRectEventArgs` / `MarqueeRenderedEventArgs` |
| `EsUtil.Ui.WinForms.Marquee.Internal` | 本アセンブリの実装（内部） |

- **公開面はコントロールとイベント引数の 4 型**です。 幾何型（`EsPoint` / `EsRect` / `EsSize`）・状態（`MarqueeSnapshot`）・設定（`MarqueeOptions`）・外観（`MarqueeAppearance`）・描画計画（`MarqueeDrawPlan`）は、コア（`EsUtil.Feature.Marquee.Core`）と提示層（`EsUtil.Feature.Marquee.Presenter`）の型をそのまま使います。
- **`internal` の実装型は §6 に「（内部）」と明記して示します**（可視性は `internal` であり、利用側から参照できません）。

## 2. 依存関係

| 種別 | 依存 |
| --- | --- |
| フレームワーク | `net10.0-windows10.0.17763.0`（`UseWindowsForms`） |
| パッケージ | `EsUtil.Feature.Marquee.Presenter` / `EsUtil.Platform.Core`（推移参照でコアも解決する） / `Microsoft.Extensions.Logging.Abstractions` |
| 標準ライブラリ | `System.Drawing`（GDI+ による描画） |

コア（`EsUtil.Feature.Marquee.Core`）と提示層（`EsUtil.Feature.Marquee.Presenter`）の API は各プロジェクトの API リファレンスを参照してください。

## 3. 座標系と単位

| 座標系 | 単位 | 使う場所 |
| --- | --- | --- |
| 対象座標 | 対象の 1 ピクセル（画像ピクセル・仮想スクリーン ピクセル等） | コアの状態機械・選択矩形・イベント引数 |
| ローカル DIP | 表示の DIP（96 DPI 基準の論理ピクセル） | 入力（マウス座標）と描画 |
| 物理ピクセル | 画面の実ピクセル | 描画面の実サイズ（内部の換算にだけ現れる） |

- **境界変換は座標変換ポート（`ICoordTransform`）が担います**。 ポート未設定の場合は `ClientToSourceScaling` を倍率とした等価変換を使います（1 対象ピクセル = 1 DIP・境界なし）。
- **描画は DIP 座標で行います**。 描画面へは DPI の拡大（スケーリング係数）を適用してから描画するため、利用側は DPI を意識する必要がありません。
- nudge（矢印キー）は**対象座標の整数 1 単位**で動きます（DIP ではありません）。

## 4. `MarqueeControl`

リサイズ可能なドラッグ矩形のコントロールです。 `System.Windows.Forms.Control` を継承します。

- **継承**: `Control` → `MarqueeControl`（`sealed`）
- **生成**: `new MarqueeControl()` または `new MarqueeControl(ICursorService?)`（カーソル サービスを注入する場合）
- **描画**: `ControlStyles.UserPaint` / `AllPaintingInWmPaint` / `OptimizedDoubleBuffer` / `ResizeRedraw` / `SupportsTransparentBackColor` / `Selectable` / `StandardClick` / `StandardDoubleClick` を有効にし、背景を `Color.Transparent` にします。

### 4.1 プロパティ

| プロパティ | 型 | 既定 | 説明 |
| --- | --- | --- | --- |
| `Options` | `MarqueeOptions` | `MarqueeOptions.Default` | 寸法・対話パラメータ（ハンドルの寸法・十字マーカーの長さ・最小選択サイズ・ヒットテスト半径・境界外選択の禁止）。 設定すると再描画し、対話パラメータをコアへ伝搬する。 |
| `Appearance` | `MarqueeAppearance` | `MarqueeAppearance.Default` | 描画にだけ必要な外観（色・ラベル体裁・表示可否）。 |
| `HighlightSourceRect` | `EsRect?` | null | ハイライトする矩形（対象座標）。 指定がある場合は選択矩形より優先して注目矩形になる。 |
| `ClientToSourceScaling` | `double` | 1.0 | 座標変換ポートへ適用する倍率（対象座標 / ローカル DIP）。 設定時は 0.1〜8.0 へ補正する。 ポート未指定時のみ使う。 |
| `DisplayScaleScaling` | `double` | 1.0 | サイズラベルの表示スケーリング係数（マルチ モニタ補正）。 0 以下を設定した場合は `ClientToSourceScaling` の値として解決される。 |
| `ClientToPixelScaling` | `double` | 1.0 | 表示 DIP を物理ピクセルへ変換する係数。 DPI の通知（`RescaleConstantsForDpi`）が更新する。 描画面の DPI を解決できない場合の代替値。 |
| `SizeLabelOverrideProvider` | `Func<string?>?` | null | サイズラベルの文言を返す関数。 null または空文字を返す場合は自動生成する。 |
| `SizeLabelDisplayScalingProvider` | `Func<EsRect, double>?` | null | サイズラベルの表示スケーリングを矩形ごとに解決する関数。 |
| `DrawPlanResolver` | `Func<MarqueeDrawPlan, MarqueeDrawPlan>?` | null | 描画計画を差し替える関数。 矩形の並びで表せる装飾とラベルの文言・位置を差し替える入口。 |
| `Log` | `ILogger` | `NullLogger` | 操作ログの出力先。 null を設定すると出力なしへフォールバックする。 |
| `LogPrefix` | `string?` | null | ログの接頭辞（null または空白の場合は既定値 `[Marquee]`）。 設定するとコアと提示層へ伝搬する。 |
| `HasSelection` | `bool` | false | 選択矩形があるかどうか（取得のみ）。 スナップショットの生成を伴わない。 |
| `SelectionSourceRect` | `EsRect` | 0x0 | 現在の選択矩形（対象座標。 取得のみ）。 未選択時は空の矩形。 |
| `CurrentSnapshot` | `MarqueeSnapshot` | — | 現在の状態のスナップショット（取得のみ）。 状態・選択矩形・アクティブ ハンドル・マウス位置・操作フラグを含む。 |

- **設定は不変の値**です。 一部の項目だけを変える場合は `MarqueeOptions.Default with { ... }` / `MarqueeAppearance.Default with { ... }` の形で指定します。
- `Options` の `ClampToSourceBounds`（既定 true）が有効な場合、選択矩形は座標変換ポートの境界（`ICoordTransform.SourceArea`）内へクリップされます。
- 公開プロパティはデザイナーのコード生成の対象外です（`DesignerSerializationVisibility.Hidden`）。

### 4.2 メソッド

| メソッド | 説明 |
| --- | --- |
| `SetCoordTransform(ICoordTransform?)` | 対象 ↔ ローカル DIP の座標変換ポートを設定する。 null は等価変換（1 対象ピクセル = 1 DIP・境界なし）。 変換関数は呼び出し毎に評価されるため、ズーム・スクロール等の動的変化を反映できる。 |
| `SetSelection(EsRect)` | 選択矩形を外部状態から復元する。 境界がある場合はクリップしてから設定し、`SelectionChanged` を発火する。 |
| `ResetSelection()` | 選択状態と nudge 状態を初期化する。 nudge 中だった場合はカーソルを再表示する。 |
| `RequestConfirm()` | 確定要求を処理する（Enter キーと同じ条件）。 選択確定後のみ `ConfirmRequested` を発火し、選択状態をクリアする。 |
| `RequestCancel()` | キャンセル要求を処理する（Escape キーと同じ条件）。 ドラッグ中 / 選択ありの場合は選択を破棄した後に `CancelRequested` を発火する。 |
| `TryGetCurrentLoupePosition(out EsPoint)` | 追従位置（ルーペ中心。 対象座標）を実カーソルから取り直す。 追従していない場合（ドラッグ中・nudge 中・ポインターがコントロールの外）とカーソル位置を取得できない場合は false を返す。 |
| `InvalidateRender()` | 描画計画の再解決と再描画を要求する（描画に関わる変更の唯一の入口）。 |
| `DrawPlan(Graphics, in MarqueeDrawPlan)` | 描画計画を描画面へ描く（静的）。 計画どおりに描くため、外観で止めている項目は描かれない。 自前の描画と組み合わせる場合に使う。 |

本コントロールが上書きする保護メンバーは次のとおりです（`sealed` のため利用側が派生することはありませんが、挙動の契約を示します）。

| メンバー | 説明 |
| --- | --- |
| `IsInputKey(Keys)` | 矢印キー・Escape・Enter を入力キーとして扱う（既定では矢印キーがダイアログ キーになるため）。 |
| `OnPaint(PaintEventArgs)` | 描画計画を解決し、DPI の拡大を適用して描画した後、`Rendered` を発火する。 |
| `OnMouseDown` / `OnMouseMove` / `OnMouseUp` | 左ボタンのドラッグをコアへ通知する（マウス イベントの物理ピクセル座標を DIP へ換算してから対象座標へ変換する）。 |
| `OnMouseEnter` / `OnMouseLeave` | ポインターがコントロールの上にあるかどうかを記録する（追従位置の取り直しの判断に使う）。 |
| `OnKeyDown(KeyEventArgs)` | キーをコア型（`EsKey`）へ変換して通知し、処理したキーの押下音と既定の処理を抑止する。 |
| `OnLostFocus(EventArgs)` | nudge を終了し、非表示にしたカーソルを再表示する。 |
| `OnHandleCreated(EventArgs)` | `ClientToPixelScaling` を `DeviceDpi` から設定して再描画する（入力座標の換算と描画の倍率を一致させる）。 |
| `RescaleConstantsForDpi(int, int)` | DPI が変わったときに `ClientToPixelScaling` を更新して再描画する。 |

### 4.3 イベント

| イベント | 引数 | 説明 |
| --- | --- | --- |
| `SelectionStarted` | `EventHandler<MarqueePointEventArgs>` | 新規選択のドラッグ開始（始点の決定。 座標は対象座標）。 |
| `SelectionChanged` | `EventHandler<SelectionRectEventArgs>` | 選択矩形の変更。 |
| `SelectionCompleted` | `EventHandler<SelectionRectEventArgs>` | ドラッグ後の終点が決定され、選択矩形が確定したとき。 |
| `SelectionCancelled` | `EventHandler` | 領域のキャンセル（小さすぎる矩形・枠外ダブルクリック等）。 |
| `LoupePositionMoved` | `EventHandler<MarqueePointEventArgs>` | ルーペが表示する中心座標の移動（通常追従ではポインター位置、ドラッグ・nudge 中は動いている側のハンドル位置）。 |
| `ConfirmRequested` | `EventHandler<SelectionRectEventArgs>` | 確定要求（Enter・矩形内ダブルクリック・`RequestConfirm`）。 ドラッグ中は発火しない。 |
| `CancelRequested` | `EventHandler` | キャンセル要求（Escape・枠外ダブルクリック・`RequestCancel`）。 選択の破棄は実施済み。 |
| `Rendered` | `EventHandler<MarqueeRenderedEventArgs>` | 描画完了。 ホスト固有の付加情報を描く入口。 描画中に発火するため、ハンドラー内で描画を完了させる（描画面を保持しない）。 |

- **生のポインター入力のイベントは持ちません**（押下・解放・移動は自 UI のイベントで観測します）。 押下状態は `MarqueeSnapshot.IsMouseDown` でも取得できます。

## 5. イベント引数

### 5.1 `MarqueePointEventArgs`

座標に関する通知のイベント引数です（`EventArgs` を継承）。

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `Point` | `EsPoint` | 通知対象の座標（対象座標）。 |
| `Snapshot` | `MarqueeSnapshot` | 通知時点のスナップショット（状態・選択矩形・アクティブ ハンドル・マウス位置・操作フラグ）。 |

### 5.2 `SelectionRectEventArgs`

選択矩形に関する通知のイベント引数です（`EventArgs` を継承）。

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `Rect` | `EsRect` | 通知対象の矩形（対象座標）。 |
| `Snapshot` | `MarqueeSnapshot` | 通知時点のスナップショット。 |

### 5.3 `MarqueeRenderedEventArgs`

描画完了を通知するイベント引数です（`EventArgs` を継承）。

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `Graphics` | `Graphics` | 描画先（DPI の拡大が適用済み・描画中のみ有効）。 ホストはここへ付加情報を描く。 |
| `Content` | `MarqueeDrawPlan` | 1 フレーム分の描画内容（装飾・ハンドル・始点マーカー・ラベル・層の順序）。 文字の有無は `HasText` で判定できる。 |

## 6. 内部型（本アセンブリの実装）

**本節の型は `internal` です**（利用側から参照できません）。 本アセンブリの単体テストだけが `InternalsVisibleTo` で参照します。

### 6.1 `MarqueeGraphicsPainter`（内部）

描画計画（`MarqueeDrawPlan`）を GDI+ の描画 API へ写す描画実装です。

| メソッド | 説明 |
| --- | --- |
| `Paint(Graphics, in MarqueeDrawPlan)` | 計画の `Layers` の順に層を描く。 描くかどうかは外観の `Show*` で判断する。 ラベル（`MarqueeDrawLayer.Label`）は本クラスが描く。 |

- 与えられた `Graphics` の座標系（DIP）で描きます。 DPI の拡大は呼び出し側（`MarqueeControl`）が適用します。
- 枠線・十字線に使う `Pen` と塗りに使う `SolidBrush` はスレッドごとに 1 つを使い回します。

### 6.2 `MarqueeLogMessage`（内部）

本アセンブリが出力するログの定義（`LoggerMessage` ソース生成）です。

| メンバー | 説明 |
| --- | --- |
| `DefaultPrefix` | 接頭辞の既定値（`[Marquee]`）。 |
| `Prefix` | 適用する接頭辞（`MarqueeControl.LogPrefix` が設定する）。 |
| `LogOperationError(ILogger, Exception, string)` | 公開操作の例外を `Error` として記録する（出力書式は `[Marquee] {Operation} で例外が発生`）。 |

### 6.3 描画に使うスケーリング係数の解決（コア）

描画面（物理ピクセル）と表示領域（DIP）から描画に使う倍率を求める処理は
**`EsUtil.Platform.Core.Coord.DpiScaler.ResolveClientScale` が単一所有**します（2026-10-23 に各 UI が持っていた `internal` の複製を削除）。

| メソッド | 説明 |
| --- | --- |
| `ResolveClientScale(double clientPixels, double clientDip, double fallback)` | 表示領域が確定していれば `clientPixels ÷ clientDip` を、未確定（0 以下）なら `fallback` を返す。 結果は基準値へ補正する。 |

### 6.4 `Internal.EsGeometryWinFormsInterop`（内部）

コア幾何型と GDI+ の座標型を相互変換する拡張メソッドを提供します。

| 変換 | 説明 |
| --- | --- |
| `EsPoint` ⇔ `PointF` | 単精度へ丸められる（整数と 0.5 刻みの値は損失しない）。 |
| `EsPoint` → `Point` / `EsPointInt` → `Point` | 整数座標へ変換する（丸めは 0.5 を 0 から遠い側へ寄せる）。 |
| `PointF` → `EsPointInt` / `Point` → `EsPointInt` | 画面物理ピクセルの座標へ変換する。 |
| `EsSize` ⇔ `SizeF` | 単精度へ丸められる。 |
| `EsVector` ⇔ `PointF` | 単精度へ丸められる。 |
| `EsRect` ⇔ `RectangleF` | 負のサイズもそのまま保持する。 |

### 6.5 `Internal.EsKeyWinFormsInterop`（内部）

UI フレームワーク非依存の `EsKey` と WinForms の `Keys` を**名前ベース**で相互変換します。

| メソッド | 説明 |
| --- | --- |
| `ToWinFormsKeys(EsKey)` | 対応先がない場合は `Keys.None` を返す（別のキーへ化けない）。 |
| `ToEsKey(Keys)` | 対応先がない場合（キー以外の入力）は `EsKey.None` を返す。 |
| `IsRepresentable(EsKey)` | WinForms に対応するキーがあるかどうかを判定する。 |

- 左右を表す名前（`LeftShift` ⇔ `LShiftKey`）と綴りが異なる名前（`OemTilde` ⇔ `Oemtilde`）は別名表で解決します。
- 未定義値（列挙に無い値）は `None` へフォールバックします。

### 6.6 `Internal.GraphicsFill`（内部）

GDI+ の矩形塗り（単色）を単一所有します。

| メソッド | 説明 |
| --- | --- |
| `Rect(Graphics, EsRect, Color)` | 1 つの矩形を塗る。 幅・高さが 0 以下の矩形は塗らない。 |
| `Rects(Graphics, IReadOnlyList<EsRect>, Color)` | 複数の矩形を同じ色で塗る。 |

### 6.7 `Internal.GraphicsText`（内部）

GDI+ のテキスト描画（フォント解決・実寸測定・描画）を単一所有します。

| メソッド | 説明 |
| --- | --- |
| `Measure(string, double)` | テキストの実寸（DIP）を返す。 測定は基準 DPI（96）の専用描画面で行う。 |
| `Draw(Graphics, string, double, double, double, Color)` | テキストを左上原点で描く。 描画先の変換（DPI の拡大）に従う。 |

## 7. 使用例

画像の上へ重ねて範囲を選択し、確定時に矩形を受け取る例です。

```csharp
using EsUtil.Feature.Marquee;
using EsUtil.Ui.WinForms.Marquee;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;

var marquee = new MarqueeControl
{
    Dock = DockStyle.Fill,
    Options = MarqueeOptions.Default with { HandleSize = 12.0 },
    Appearance = MarqueeAppearance.Default with { OuterBorderColor = Color.White },
};

// 対象座標 = 画像ピクセル（表示は等倍）
marquee.SetCoordTransform(CoordTransform.ClientToSource(() => 1.0, () => new EsSize(1920, 1080)));

marquee.SelectionCompleted += (_, e) => Console.WriteLine($"確定: {e.Rect}");
marquee.ConfirmRequested += (_, e) => Console.WriteLine($"Enter で確定: {e.Rect}");
marquee.CancelRequested += (_, _) => Console.WriteLine("キャンセル");
marquee.LoupePositionMoved += (_, e) => UpdateLoupeCenter(e.Point);

pictureBox.Controls.Add(marquee);
```

描画計画を差し替えてラベルの文言を変える例です。

```csharp
marquee.DrawPlanResolver = plan => plan with
{
    Labels = [.. plan.Labels.Select(label => label with { Text = $"選択 {label.Text}" })],
};
```

## 8. 制限事項

- **1 つの対象に対して 1 つのコントロール**を想定します（同じ対象へ重ねる用途は想定していません）。
- **矢印キーの nudge はフォーカスを必要とします**。 フォーカスを失うと nudge を終了し、カーソルを再表示します。
- **文字の実寸は GDI+ のテキスト レイアウトで測ります**。 字形の送りは他の描画エンジンと一致しないため、`DrawPlanResolver` でラベルの矩形を差し替える場合は実寸を再測定してください。
- **`Rendered` の `Graphics` は描画中のみ有効**です（保持しないでください）。
- **デザイナーによる設定の保存は行いません**（公開プロパティは実行時にコードで与えます）。

## 9. エラー・例外

| 状況 | 挙動 |
| --- | --- |
| 公開操作（`SetCoordTransform` / `SetSelection` / `ResetSelection` / `RequestConfirm` / `RequestCancel`）で内部の例外が発生した場合 | ログへ `Error` として記録し、呼び出し側へ伝搬する（例外処理規約 §4）。 |
| `DrawPlan` / `MarqueeGraphicsPainter.Paint` の `graphics` が null | `ArgumentNullException` を送出する。 |
| `GraphicsText.Measure` / `Draw` のテキストが null | `ArgumentNullException` を送出する。 |
| `GraphicsFill.Rect` / `Rects` の `graphics` が null | `ArgumentNullException` を送出する。 |
| 設定値が安全域を外れている場合 | 例外にせずコアの安全域へ補正する（例: `ClientToSourceScaling` は 0.1〜8.0）。 |
| カーソル位置を取得できない場合 | 例外にせず失敗（`false` / 既定値）を返す。 nudge の基点を記録しない。 |
