# EsUtil.Feature.Marquee.ControlMewUI API リファレンス

`EsUtil.Feature.Marquee.ControlMewUI` の外部仕様（公開 API・イベント・動作）を記載します。 内部実装の詳細は記載しません。

## 目次

- [1. 概要](#1-概要)
- [2. 依存関係](#2-依存関係)
- [3. 座標系](#3-座標系)
- [4. プロパティ](#4-プロパティ)
- [5. メソッド](#5-メソッド)
- [6. プロパティ（取得のみ）](#6-プロパティ取得のみ)
- [7. イベント](#7-イベント)
  - [7.1 ラベル（文字）の表示](#71-ラベル文字の表示)
- [8. 操作仕様](#8-操作仕様)
  - [8.1 制限](#81-制限)
- [9. Avalonia 版との違い](#9-avalonia-版との違い)
  - [9.1 ログ](#91-ログ)
- [10. 使用例](#10-使用例)
- [11. エラー・例外](#11-エラー例外)

## 1. 概要

リサイズ可能なドラッグ矩形のコントロール（MewUI 版）です。 画像やスクリーンの上へ重ねて使用し、
マウスドラッグ・矢印キーで選択矩形を作成・変形します。

選択矩形・ドラッグ・nudge のロジックは UI 非依存コア（`EsUtil.Feature.Marquee.Core`）が単一所有し、
Avalonia 版と同一の挙動になります。

## 2. 依存関係

| 参照 | 用途 |
| --- | --- |
| `EsUtil.Feature.Marquee.Core` | **公開ファサード（`MarqueeCore`）/ 効果の適用先（`IMarqueeEffects`）**・公開型（`MarqueeHitTarget` / `MarqueeInteractionState` / `MarqueeSnapshot` / `MarqueeRenderRequest`）。 状態管理クラス（`MarqueeSession`）・状態機械（`MarqueeState`）・選択幾何（`MarqueeSelection`）は internal（ファサードの内部実装） |
| `EsUtil.Feature.Marquee.Presenter` | 提示層（描画計画の解決・外観・ラベル計画・組み込み規則）: `MarqueePresenter`（描画計画の解決） / `MarqueeAppearance`（色・体裁・枠線の太さ・表示可否） / `MarqueeDrawPlan`（UI が見る唯一の描画値） / `MarqueePresenter`（`LabelPlan` partial）（実寸 → 配置指示）。 **描画は本 UI が自 UI の描画 API（Skia）で行う**（`MarqueeSkiaPainter`） |
| `EsUtil.Platform.Core` | コア幾何型（`EsPoint` / `EsRect` / `EsSize`）、`ICoordTransform` / `CoordTransform`、`ICursorService` |
| `EsUtil.Platform.Core`（`Native.Win`） | カーソルサービスの既定実装（Win32） |
| `Aprillz.MewUI.Skia`（0.21.1 固定） | `SkiaCanvasView`・`MewProperty`・SkiaSharp 描画 |

## 3. 座標系

| 座標系 | 意味 |
| --- | --- |
| 対象座標 | 選択対象の座標（画像ピクセル・仮想スクリーンピクセル等）。 コアの状態機械はこの座標で動作する |
| ローカル DIP | 本コントロールのローカル座標。 入力と描画はこの座標で行う |

両者の変換は座標変換ポート（`ICoordTransform`）が担います。 入力（ToSource = DIP → 対象）と
描画（ToDip = 対象 → DIP）の境界で適用されます。

## 4. プロパティ

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Options` | `MarqueeOptions` | `MarqueeOptions.Default` | 選択矩形の**計算に必要な設定**（寸法・対話パラメータ。 コアの `EsUtil.Feature.Marquee`。 **色・表示可否は含まない**） |
| `Appearance` | `MarqueeAppearance` | `MarqueeAppearance.Default` | **外観（色・フォントサイズ・枠線の太さ・表示可否）の設定**（提示層の `EsUtil.Feature.Marquee`）。 設定すると次の描画で提示層が新しい外観で描画計画を解決する |
| `SizeLabelOverrideProvider` | `Func<string?>?` | `null` | サイズラベル（1 行目）の文言を描画時に評価するプロバイダ。 **文言はホストが与える**（コアは特定の文言を持たない。 文言の生成はホストの `CaptureLabelTexts` 等が担う）。 操作・情報ラベルの文言も同様にホストが与える。 設定すると保持している描画内容を破棄して再描画を要求する |
| `ClientToPixelScaling` | `double` | `1.0` | DIP → 物理ピクセルのスケーリング係数（DPI 変更時に自動更新）。 **描画では描画面と表示領域の比（`ClientPixelScale`）を使う**ため、追加直後でこの値が未更新でも図形は画像と同じ倍率で描かれる |

**見た目のカスタマイズ**: 色・枠線の太さ・表示可否は外観（`Appearance`。 `MarqueeAppearance`）で指定します（**表示可否が描画の停止の唯一の入口**です。 表示を止めても計画は矩形と層を保ち、描画側（コントロールの描画実装）がフラグを見て描かないため、ホストは止めた項目も計算値を使って自前で描けます）。 矩形の並びで表せる装飾とラベルの文言・位置は `DrawPlanResolver`（`Func<MarqueeDrawPlan, MarqueeDrawPlan>`）で描画計画（`MarqueeDrawPlan`）を差し替えられます（差し替えは値の置き換えだけで完結し、描画実装（本 UI の `MarqueeSkiaPainter`）は変更不要です。 例は Avalonia 版サンプルの `sample/Presentation/GuideMarqueePlan`）。 矩形で表せない描画（円・斜線等）は `Rendered` のキャンバスへ自前で描き、必要なら `DrawPlan` で既定の描画を行います。

`MarqueeOptions` の項目（既定値はコアが単一所有。 Avalonia 版と一致）:

| 項目 | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `HandleSize` | `double` | `10.0` | ドラッグハンドルの一辺サイズ（DIP。 1.0〜256.0 に補正） |
| `HandleBorderThickness` | `double` | `1.5` | ドラッグハンドルの枠線の太さ（DIP。 0.5〜64.0 に補正） |
| `HandleHitRadius` | `double` | `12.0` | ドラッグハンドルのヒットテスト半径（DIP。 0.0〜256.0 に補正。 対象へ変換して判定する） |
| `MinSelectionSize` | `double` | `6.0` | 選択矩形の最小サイズ（対象座標。 1.0〜4096.0 に補正。 これ未満のドラッグはキャンセル扱い） |
| `ClampToSourceBounds` | `bool` | `true` | 対象の境界外の選択を禁止する。 ON へ切り替えた瞬間に既存の選択矩形をクリップする |
| `MarkerLength` | `double` | `16.0` | 十字マーカーの全長（DIP。 2.0〜4096.0 に補正。 中心から両端までの半幅は 1/2） |

安全域への補正は**コアの設定型の生成時**に適用されます（設定の経路によらず範囲内の値だけが保持され、UI 側に補正の登録は不要です）。

設定の一部だけを変える場合は `MarqueeOptions.Default with { ... }` / `MarqueeAppearance.Default with { ... }` で指定します（指定しない項目は既定値を保つ）。
同値の設定を再適用しても再描画・ログは発生しません。
寸法・表示可否と対話パラメータ（`HandleHitRadius` / `MinSelectionSize` / `ClampToSourceBounds`）は同じ設定値に含まれますが、
後者だけがファサードへ伝搬します（判断と変更ログはコアが単一所有する）。 **色とフォントサイズはコアの設定に含めません**（描画にだけ必要な設定のため `Paint` が単一所有します）。

`MarqueeAppearance` の項目（外観。 既定値は提示層の型が単一所有する）:

| 項目 | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `HandleColor` / `HandleBorderColor` | `System.Drawing.Color` | 白 / `#FFA9A9A9` | ハンドルの塗り色と枠線色（全成分 0 の塗り色は白へ解決される） |
| `MarkerColor` | `System.Drawing.Color` | 白 | 十字マーカーの色 |
| `SizeLabelColor` / `SizeLabelBackgroundColor` | `System.Drawing.Color` | 白 / 黒 60% | サイズラベルの文字色と背景色 |
| `CoordLabelColor` / `CoordLabelBackgroundColor` | `System.Drawing.Color` | 白 / 黒 60% | 座標ラベルの文字色と背景色 |
| `SizeLabelFontSize` / `CoordLabelFontSize` | `double` | 14.0 / 12.0 | サイズラベル（1 行目）と座標ラベルのフォントサイズ（DIP。 6〜48 に補正。 非数は既定値へ補正） |
| `OverlayColor` / `OuterBorderColor` | `System.Drawing.Color` | 黒 50% / `#FFA9A9A9` | 暗転マスクと外枠の色 |
| `InnerFillColor` / `InnerBorderColor` | `System.Drawing.Color` | 透明 / 透明 | 注目矩形の内側の塗り色と枠線色 |

## 5. メソッド

| メソッド | 戻り値 | 説明 |
| --- | --- | --- |
| `SetCoordTransform(ICoordTransform?)` | - | 対象 ↔ ローカル DIP の変換ポートを設定する。 null の場合は等価変換（1 対象ピクセル = 1 DIP・境界なし） |
| `SetSelection(EsRect)` | - | 選択矩形を外部状態から復元する。 `Options.ClampToSourceBounds` が有効な場合は境界内へクリップしてから設定する |
| `ResetSelection()` | - | 選択状態と nudge 状態を初期化する（nudge 中だった場合はカーソルを再表示する） |
| `RequestConfirm()` | - | 確定要求を処理する（Enter と同じ条件）。 ドラッグ中・未選択では何もしない |
| `RequestCancel()` | - | キャンセル要求を処理する（Escape と同じ条件）。 選択を破棄した後にイベントを発火する |
| `RefreshOverlay()` | - | 表示（パン・ズーム・表示サイズ）の変化へ追従する（再描画と配置の再評価。 選択の状態は変更しない） |
| `InvalidateRender()` | - | 描画内容の再解決と、図形・ラベルの再描画を要求する（**描画に関わる変更の唯一の入口**。 図形は子の Skia キャンバスが描くため、親の無効化だけでは更新されない） |
| `DrawPlanResolver` | `Func<MarqueeDrawPlan, MarqueeDrawPlan>?` | **描画計画の差し替え点**（矩形の並び・文言・位置）。 設定すると再描画を要求する（同じ入力では 1 フレーム 1 回だけ解決されるため、呼び出しの時点に依存しない純粋関数にすること） |
| `DrawPlan(SKCanvas, in MarqueeDrawPlan)` | `static` | 描画計画を描画面へ描く（計画どおりに描く。 表示を止めた項目はフラグを見て描かないため描かれない） |

## 6. プロパティ（取得のみ）

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `CurrentSnapshot` | `MarqueeSnapshot` | 現在の状態（State / SelectionSourceRect / ActiveTarget / MousePosition / IsMouseDown / IsNudgeActive / HasSelection。 `readonly record struct`） |

## 7. イベント

| イベント | 引数 | 発火条件 |
| --- | --- | --- |
| `SelectionStarted` | `MarqueePointEventArgs` | 新規選択のドラッグ開始（始点の決定） |
| `SelectionChanged` | `SelectionRectEventArgs` | 選択矩形の変更（ドラッグ中・nudge 中・外部復元・境界クリップ） |
| `SelectionCompleted` | `SelectionRectEventArgs` | ドラッグ終了による選択の確定 |
| `SelectionCancelled` | `EventArgs` | 小さすぎる矩形での終了 |
| `LoupePositionMoved` | `MarqueePointEventArgs` | ルーペが表示する中心座標 |
| `LoupePositionMoved` | `MarqueePointEventArgs` | ルーペが表示する中心座標（通常追従はポインタ位置、ドラッグ・nudge 中は動いている側のハンドル位置） |
| `ConfirmRequested` | `SelectionRectEventArgs` | 矩形内ダブルクリック・Enter |
| `CancelRequested` | `EventArgs` | 矩形外ダブルクリック・Escape・`RequestCancel()` |
| `Rendered` | `MarqueeRenderedEventArgs` | 描画完了の通知（描画のたびに発火）。 `Canvas`（DPI スケーリング済み・描画中のみ有効）と描画計画（`Content` = `MarqueeDrawPlan`）を渡す。 **サイズ・座標ラベルはコントロールがテキスト要素として表示する**ため、ホストは付加情報（操作・情報ラベル）の描画に使う |

座標系はすべて対象座標です。

### 7.1 ラベル（文字）の表示

サイズラベルとカーソル座標ラベルの**文言と基準点は提示層の `MarqueePresenter`（`LabelTexts` partial） が、色とフォントサイズは外観（`MarqueeAppearance`）が、実寸から配置矩形を求める写像は提示層の `MarqueePresenter`（`LabelPlan` partial） が単一所有**します。 コントロールは `Border` + `TextBlock` を子要素として置き（`SkiaText.Measure` で実寸を測って渡す）、**ラベルは常に最上位**に表示されます。 操作・情報ラベルはアプリ固有の付加情報のため、ホストが `Rendered` のハンドラで `Options.Labels` の外観に従って描きます（配置指示は `FadeMaskDragRectControl.UIBase` の `FadeMaskHostLabelRenderer` が単一所有する）。
描画のたびに `Rendered` が発火するため、ホストはそのハンドラで追加描画できます。

```csharp
using EsUtil.Feature.Marquee.MewUI;

// 付加情報（操作・情報ラベル）を自前で描く。
// サイズ・座標ラベルはコントロールがテキスト要素として表示する（図形もコントロールが描く）。
marquee.Rendered += (_, e) =>
{
    // e.Content.FocusDipRect（基準）と Options.Labels（外観）を使って描く。
};
```

- `Rendered` はコントロールの描画中に発火します。 ハンドラ内で描画を完了させてください（キャンバスを保持しない）。
- `e.Canvas` は DPI スケーリング適用済みのため、ホストは DIP 座標で描けます。

## 8. 操作仕様

| 操作 | 動作 |
| --- | --- |
| 左ドラッグ（矩形外） | 新規選択を開始する。 ハンドルヒット時は元の選択を保持したまま開始位置がハンドル点へスナップする |
| 左ドラッグ（ハンドル上） | 8 方向のリサイズ。 ハンドル位置は 1 対象ピクセル精度でカーソルに追従する |
| 左ドラッグ（矩形内） | 移動（サイズを維持する） |
| ダブルクリック（矩形内） | `ConfirmRequested` を発火して選択をクリアする |
| ダブルクリック（矩形外） | `CancelRequested` を発火する（選択は保持する） |
| 矢印キー | nudge（1 対象ピクセル単位）。 未選択時は始点、選択確定後・ドラッグ中は終点、ハンドル選択中はそのハンドルを動かす |
| Enter | `ConfirmRequested` を発火して選択をクリアする（ドラッグ中・未選択では何もしない） |
| Escape | `CancelRequested` を発火する（選択があれば破棄する） |
| ホイール（Ctrl 併用・なしとも） | **本コントロールは扱わない**（素通しする。 ズーム・パンの解釈と実処理はホストが行う） |
| （解釈の所有者） | ホイール操作はコントロール化を前提とした処理であり、**ホスト**（UI 層）がビューポートへ転送する |

### 8.1 制限

- `Options.ClampToSourceBounds` が有効な場合、選択は `ICoordTransform.SourceArea` で示される境界内に制限されます。
  新規選択・ハンドル変形・移動・nudge のすべてに適用されます。
- 選択矩形の幅・高さが `Options.MinSelectionSize` 未満の状態でドラッグを終了するとキャンセル扱いになります
  （`SelectionCancelled` を発火して選択を破棄します）。
- 矢印キーの nudge は本コントロールにフォーカスがある場合のみ動作します（マウス押下時に自動でフォーカスを取得します）。
  nudge 中は OS カーソルを非表示にし、終了時に nudge 位置へ戻して再表示します。 フォーカスを失った場合も nudge を終了します。

## 9. Avalonia 版との違い

| 項目 | 差異 |
| --- | --- |
| イベント引数の座標 | コア型（`EsPoint` / `EsRect`）で公開する（Avalonia 版は Avalonia の `Point` / `Rect`） |
| マウスダウン・アップのイベント | 持たない（基底 `UIElement` の同名メンバーが同じ情報を提供するため、本コントロールでは公開しない） |
| 表示の変化への追従 | ホストが `RefreshOverlay()` を呼ぶ（自己完結する UI 層の処理） |
| nudge のキー入力 | フォーカス依存（Avalonia 版は `TopLevel` を購読してフォーカス非依存） |
| ログ | `Log` プロパティで `ILogger` を注入して出力する（書式・レベルは Avalonia 版と同一） |
| 文字（サイズラベル・座標ラベル）の表示 | **コントロールがテキスト要素（`Border` + `TextBlock`）として配置する**（文言と基準点は提示層の `MarqueePresenter`（`LabelTexts` partial）、色とフォントサイズは `MarqueeAppearance`、実寸測定は **MewUI のテキスト エンジン**（`MewUITextMeasure.Measure`）、配置は提示層の `MarqueePresenter`（`LabelPlan` partial）） |

### 9.1 ログ

`Log` プロパティへ `ILogger` を設定すると操作ログを出力します（未設定・null の場合は `NullLogger` になり出力しません）。

| レベル | 出力内容 |
| --- | --- |
| Debug | `MouseDown`（位置・クリック数・選択有無）/ `MouseUp`（位置・ドラッグ有無・選択有無）/ `DoubleClick`（矩形内か）/ `DragStart`（ハンドル・位置）/ `KeyDown`（キー・処理有無）/ `ConfirmRequested`（矩形）/ `CancelRequested` / `Setting`（`Options.ClampToSourceBounds` の変更前後）/ 外観・表示スケーリングの変更（提示層の `MarqueePresenterLogMessage`） |
| Trace | `MouseDown 直前の位置` / `MouseUp 直後の位置` |

マウス移動の高頻度ログは出力しません（押下直前・解放直後の位置のみ記録します）。

## 10. 使用例

```csharp
var marquee = new MarqueeControl
{
    // 寸法・表示可否・対話パラメータ
    Options = MarqueeOptions.Default with
    {
        HandleHitRadius = 12.0,
    },

    // 外観（色・フォントサイズ・枠線の太さ・表示可否）
    Appearance = MarqueeAppearance.Default with
    {
        // 選択枠線は装飾（暗転マスクの外枠）が描く
        OuterBorderColor = System.Drawing.Color.FromArgb(255, 0, 120, 215),
        OuterBorderThickness = 2.0,
        ShowOverlay = true,
        ShowOuterBorder = true,
    },
};

// 対象の境界（画像座標）を設定する
marquee.SetCoordTransform(new CoordTransform(dip => dip, source => source, () => new EsSize(width, height)));

// 確定した矩形を取得する
marquee.SelectionCompleted += (_, e) => OnCaptured(e.Rect);
```

## 11. エラー・例外

| 条件 | 動作 |
| --- | --- |
| カーソルサービスが null | Windows 既定実装（Win32）を使用する |
| `SetCoordTransform(null)` | 等価変換（境界なし）を適用する |
| `Paint.CoordLabelFontSize` に NaN / ∞ | 既定値（12）へ補正する（例外は発生しない） |
| `SetSelection` に境界外の矩形 | `Options.ClampToSourceBounds` が有効なら境界内へクリップする（例外は発生しない） |
