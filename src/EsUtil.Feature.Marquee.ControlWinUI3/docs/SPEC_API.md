# EsUtil.Feature.Marquee.ControlWinUI3 API リファレンス

`EsUtil.Feature.Marquee.ControlWinUI3` の外部仕様（公開 API・イベント・動作）を記載します。 内部実装の詳細は記載しません。

## 目次 <!-- omit from toc -->

- [1. 概要](#1-概要)
- [2. 依存関係](#2-依存関係)
- [3. 座標系](#3-座標系)
- [4. プロパティ](#4-プロパティ)
- [5. メソッド](#5-メソッド)
- [6. プロパティ（取得のみ）](#6-プロパティ取得のみ)
- [7. イベント](#7-イベント)
  - [7.1 ラベルの表示](#71-ラベルの表示)
- [8. 操作仕様](#8-操作仕様)
  - [8.1 制限](#81-制限)
- [9. 他の UI 版との違い](#9-他の-ui-版との違い)
- [10. 使用例](#10-使用例)
  - [10.1 最小の構成](#101-最小の構成)
  - [10.2 見た目の差し替え（外観と描画計画）](#102-見た目の差し替え外観と描画計画)
  - [10.3 付加情報の描画とログ](#103-付加情報の描画とログ)
- [11. エラー・例外](#11-エラー例外)
- [12. 内部型（テストが参照する実装）](#12-内部型テストが参照する実装)

## 1. 概要

リサイズ可能なドラッグ矩形のコントロール（WinUI 3 版）です。 画像やスクリーンの上へ重ねて使用し、
マウス ドラッグ・矢印キーで選択矩形を作成・変形します。

選択矩形・ドラッグ・nudge のロジックは UI 非依存コア（`EsUtil.Feature.Marquee.Core`）が単一所有し、
他の UI 版（Avalonia / MewUI）と同一の挙動になります。

基底クラスは `SkiaSharp.Views.Windows.SKXamlCanvas`（`Canvas`）で、自前の描画面へ Skia で直接描画します。
サイズラベル・カーソル座標ラベルも同じ描画面へ描きます。

## 2. 依存関係

| 参照 | 用途 |
| --- | --- |
| `EsUtil.Feature.Marquee.Presenter` | 提示層（描画計画の解決・外観・ラベル配置）: `MarqueePresenter`（描画計画の解決）/ `MarqueeAppearance`（外観）/ `MarqueeDrawPlan`（UI が見る唯一の描画値）/ `MarqueeLabelPlacement`（ラベルの配置指示） |
| `EsUtil.Platform.Core` | コア幾何型（`EsPoint` / `EsRect` / `EsSize`）、`ICoordTransform` / `CoordTransform` / `DpiScaler`、`ICursorService`（`Native.Win` の既定実装） |
| `Microsoft.WindowsAppSDK` | WinUI 3（`Canvas` / ポインター イベント / `Microsoft.UI.Input` のカーソル / ウィンドウ ID） |
| `SkiaSharp.Views.WinUI` | `SKXamlCanvas`（描画面）と Skia の描画 API |
| `Microsoft.Extensions.Logging.Abstractions` | ログの出力先（`ILogger`） |

コア（`EsUtil.Feature.Marquee.Core`）は提示層の推移参照で解決されます。

## 3. 座標系

| 座標系 | 意味 |
| --- | --- |
| 対象座標 | 選択対象の座標（画像ピクセル・仮想スクリーンピクセル等）。 コアの状態機械はこの座標で動作します |
| ローカル DIP | コントロールのローカル座標（WinUI の DIP）。 入力と描画はこの座標で行います |
| 物理ピクセル | 描画面の座標。 描画の直前に表示倍率を掛けて DIP から変換します |

対象座標とローカル DIP の変換は座標変換ポート（`ICoordTransform`）が担います。 入力（DIP → 対象）と
描画（対象 → DIP）の境界で適用され、コントロールは倍率を知りません。

## 4. プロパティ

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Options` | `MarqueeOptions` | `MarqueeOptions.Default` | 計算に必要な設定（最小選択サイズ・ハンドルの寸法・ヒットテスト半径・境界外選択の禁止）。 変更すると対話パラメータがファサードへ伝搬し、再描画されます |
| `Appearance` | `MarqueeAppearance` | `MarqueeAppearance.Default` | 描画にだけ必要な外観（色・フォントサイズ・枠線の太さ・表示可否）。 部分変更は `MarqueeAppearance.Default with { ... }` で行います |
| `HighlightSourceRect` | `EsRect?` | `null` | ホストが与えるハイライト矩形（対象座標）。 値がある場合は選択矩形より優先して注目矩形になります |
| `ClientToSourceScaling` | `double` | `1.0` | 座標変換ポートを設定しない場合に使う倍率（対象座標 / ローカル DIP）。 安全域へ補正されます |
| `ClientToPixelScaling` | `double` | `1.0` | 表示倍率（DIP → 物理ピクセル）。 XAML のルートの変化（`XamlRoot.Changed`）に追従して更新されます |
| `DisplayScaleScaling` | `double` | 未設定（`0` 以下） | サイズラベルの百分率に使う倍率（マルチモニタ補正）。 未設定の場合は `ClientToSourceScaling` と同値として扱われます |
| `SizeLabelOverrideProvider` | `Func<string?>?` | `null` | サイズラベルの文言を描画時に与える関数（`null` または空文字を返す場合は自動生成）。 設定すると再描画されます |
| `SizeLabelDisplayScalingProvider` | `Func<EsRect, double>?` | `null` | 矩形ごとの表示倍率を与える関数（モニタごとに倍率が違う場合）。 `null` の場合は `DisplayScaleScaling` を使います |
| `DrawPlanResolver` | `Func<MarqueeDrawPlan, MarqueeDrawPlan>?` | `null` | 描画計画（装飾の矩形列・ラベル配置）を差し替える関数。 同じ入力に同じ出力を返す純粋関数にします |
| `Log` | `ILogger` | 出力なし | 操作ログの出力先。 保持とフォールバックは提示層・コアが単一所有します |
| `LogPrefix` | `string?` | `null`（既定 `[Marquee]`） | ログの接頭辞。 `null` または空白を設定すると既定値へ戻します |

## 5. メソッド

| メソッド | 説明 |
| --- | --- |
| `SetCoordTransform(ICoordTransform?)` | 対象 ↔ ローカル DIP の座標変換ポートを設定します。 `null` は等価変換（1 対象ピクセル = 1 DIP・無制限）です。 変換関数は呼び出しごとに評価されるため、ズーム・スクロールの変化を反映できます |
| `SetSelection(EsRect)` | 選択矩形を外部状態から復元します。 境界が有効な場合は境界内へクリップしてから設定し、`SelectionChanged` を発火します |
| `ResetSelection()` | 選択状態と nudge 状態を初期化します。 nudge 中だった場合はカーソルを再表示します |
| `RequestConfirm()` | 確定要求を処理します（Enter キーと同じ条件。 ドラッグ中は無視し、選択確定後のみ `ConfirmRequested` を発火します） |
| `RequestCancel()` | キャンセル要求を処理します（Escape キーと同じ条件。 選択を破棄してから `CancelRequested` を発火します） |
| `TryGetCurrentLoupePosition(out EsPoint)` | 追従位置（ルーペ中心。 対象座標）を実カーソルから取り直します。 追従していない場合（ドラッグ中・nudge 中）とポインターがコントロールの外にある場合は `false` を返します |
| `InvalidateRender()` | 描画計画の再解決と再描画を要求します（座標変換ポートが参照するホスト側の状態を変えたときに呼びます） |
| `DrawPlan(SKCanvas, in MarqueeDrawPlan)` | 描画計画を自前のキャンバスへ描きます（既定の描画をホストが再利用するための入口。 `static` メソッドです） |

## 6. プロパティ（取得のみ）

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `CurrentSnapshot` | `MarqueeSnapshot` | 状態・選択矩形・アクティブ ハンドル・マウス位置・操作フラグを含むスナップショット |
| `HasSelection` | `bool` | 選択矩形があるかどうか（スナップショットの生成を伴わない軽量な判定） |
| `SelectionSourceRect` | `EsRect` | 現在の選択矩形（対象座標）。 未選択時は `0x0` |
| `IsPointerOver` | `bool` | ポインターがコントロールの上にあるかどうか（ポインターの進入・退出から保持する状態） |

## 7. イベント

| イベント | 引数 | 説明 |
| --- | --- | --- |
| `SelectionStarted` | `MarqueePointEventArgs` | ドラッグの始点が決まったとき（新規選択のドラッグ開始） |
| `SelectionChanged` | `SelectionRectEventArgs` | 選択矩形が変わったとき |
| `SelectionCompleted` | `SelectionRectEventArgs` | ドラッグが終わって選択が確定したとき |
| `SelectionCancelled` | `EventArgs` | 領域がキャンセルされたとき（小さすぎる矩形・選択矩形の外側のダブルクリック） |
| `LoupePositionMoved` | `MarqueePointEventArgs` | ルーペが表示する中心（対象座標）が動いたとき。 通常追従ではポインター位置、ドラッグ・nudge 中は動いている側のハンドル位置 |
| `ConfirmRequested` | `SelectionRectEventArgs` | 確定要求（Enter または選択矩形の内側のダブルクリック） |
| `CancelRequested` | `EventArgs` | キャンセル要求（Escape または選択矩形の外側のダブルクリック） |
| `Rendered` | `MarqueeRenderedEventArgs` | コントロールが図形とラベルを描き終えたとき。 描画中のみ有効なキャンバス（表示倍率を適用済み）と描画内容を渡します |

- `MarqueePointEventArgs.Point` / `SelectionRectEventArgs.Rect` は **対象座標**です。
- 両方のイベント引数は通知時点のスナップショット（`MarqueeSnapshot`）を持ちます。
- `SelectionCancelled` / `CancelRequested` は `EventArgs` を使います（矩形が決まらない通知のため）。

### 7.1 ラベルの表示

サイズラベルとカーソル座標ラベルは、文言・基準点・配置矩形を提示層が解決し、**コントロールが描画面へ描きます**。

| 項目 | 内容 |
| --- | --- |
| 文言 | 提示層が自動生成します（`SizeLabelOverrideProvider` で差し替え可能） |
| 体裁（色・フォントサイズ・内側余白） | `Appearance` が単一所有します（`MarqueeAppearance.LabelPadding` 等） |
| 表示可否 | `Appearance` の `ShowSizeLabel` / `ShowCoordLabel` で決まります（止めても配置指示は残り、描画側だけが止まります） |
| 描画 | コントロールが背景（矩形）と文字を描きます。 実寸測定と描画が同じエンジン（Skia）です |
| ホスト固有のラベル | 操作ラベル・情報ラベルなどアプリ固有の情報はホストが `Rendered` のハンドラで描きます |

## 8. 操作仕様

| 操作 | 動作 |
| --- | --- |
| 左ボタン押下 → ドラッグ | ハンドルにヒットすればリサイズ/移動、ヒットしなければ新規選択。 ドラッグ中はポインターをキャプチャします |
| 左ボタン解放 | 最小サイズ未満ならキャンセル、満たせば確定（クリックのみの場合はキャンセル） |
| 矢印キー | nudge（1 対象ピクセル単位の微調整）。 カーソルを隠し、十字マーカーを表示します |
| フォーカス喪失 | nudge を終了してカーソルを再表示します |
| Escape | キャンセル要求（ドラッグ中または選択あり）。 ドラッグを破棄し、選択を消してから通知します |
| Enter | 確定要求（選択確定後のみ。 ドラッグ中は無視）。 確定後に選択をクリアします |
| 左ダブルクリック | 選択矩形の内側なら確定要求、外側ならキャンセル要求 |

**入力の前提**: キー入力（矢印キー・Escape・Enter）はコントロールにフォーカスがある間だけ届きます
（`IsTabStop` が `true` で、押下時にコアがフォーカスを要求します）。

**ダブルクリックの判定**: WinUI 3 のポインター イベントはクリック数を報告しないため、OS の設定値
（`GetDoubleClickTime` が返すミリ秒）と直前の押下位置（4 DIP 以内）から判定します。

### 8.1 制限

- スクロール・ズームのビューポート連携は行いません（ホストが変化を検知して `InvalidateRender()` を呼びます）。
- 矩形（4 辺）で表せない装飾は `Appearance` の `Show*` を止め、`Rendered` の描画で与えます。
- 選択の境界（`CoordTransform.SourceArea`）を設定しない場合、選択は座標空間上で無制限です。

## 9. 他の UI 版との違い

| 項目 | 本コントロール（WinUI 3） |
| --- | --- |
| ラベルの描画 | 子要素（`Border` + `TextBlock`）ではなく、**同じ描画面へ Skia で描きます**。 実寸測定と描画が同じエンジンになり、背景から文字がはみ出しません |
| 矢印キーの nudge | **フォーカスが必要**です（他 UI 版のフォーカス非依存の TopLevel 購読とは異なります） |
| ダブルクリック | OS の設定値と押下位置から本 UI が判定します（ポインター イベントがクリック数を報告しないため） |
| ポインター キャプチャ | コアからの要求で取得・解放します（押下時に記録したポインターを使います） |
| 描画倍率 | 描画面の実寸 ÷ 割り当て領域で実測し、XAML のルートの変化にも追従します |
| `IsPointerOver` | `UIElement` が公開しないため、ポインターの進入・退出からコントロールが保持します（他 UI 版は基底クラスの `IsMouseOver` / `IsPointerOver` を使います） |

## 10. 使用例

### 10.1 最小の構成

```xml
<Grid>
    <Image x:Name="Target" Stretch="None" />
    <marquee:MarqueeControl x:Name="Marquee" />
</Grid>
```

```csharp
using EsUtil.Feature.Marquee.WinUI3;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;

// 対象座標 = ローカル DIP × 倍率（倍率はホストが決める）。 境界を与えて選択を外へ出さない
Marquee.SetCoordTransform(CoordTransform.ClientToSource(() => zoom, () => new EsSize(800, 600)));

// 選択の復元と通知の購読（座標はすべて対象座標）
Marquee.SetSelection(new EsRect(100, 100, 200, 150));
Marquee.SelectionCompleted += (sender, e) => ApplySelection(e.Rect);
Marquee.ConfirmRequested += (sender, e) => ConfirmSelection(e.Rect);
Marquee.CancelRequested += (sender, e) => CancelSelection();
```

### 10.2 見た目の差し替え（外観と描画計画）

```csharp
// 外観（色・太さ・表示可否）は Appearance で与える
Marquee.Appearance = MarqueeAppearance.Default with
{
    OuterBorderColor = System.Drawing.Color.FromArgb(255, 255, 196, 0),
    ShowInnerBorder = true,
};

// 矩形の並びで表せる装飾とラベルの位置は計画を差し替えて与える（純粋関数にすること）
Marquee.DrawPlanResolver = plan => plan.FocusDipRect is { } focus
    ? plan with { OuterBorderDipRects = BuildDashedFrame(focus) }
    : plan;
```

### 10.3 付加情報の描画とログ

```csharp
// 描画の直後に呼ばれるため、ホスト固有のラベルはここで描く（キャンバスは描画中のみ有効）
Marquee.Rendered += (sender, e) =>
{
    if (e.Content.FocusDipRect is { } focus)
    {
        DrawOperationLabel(e.Canvas, focus);
    }
};

// 内部の判断と例外を確認する
Marquee.Log = logger;
Marquee.LogPrefix = "[Marquee]";
```

## 11. エラー・例外

| 状況 | 挙動 |
| --- | --- |
| 公開ファサードの操作（`SetCoordTransform` / `SetSelection` / `ResetSelection` / `RequestCancel` / `RequestConfirm`）で下位の層が例外を投げた | `Log` へ `Error` で記録し、呼び出し側へ再スローします（記録の書式は `[接頭辞] {操作名} で例外が発生`） |
| `DrawPlan` / 描画の入口へ `null` のキャンバスを渡した | `ArgumentNullException`（呼び出し側の実装誤り） |
| 座標変換ポートが未設定 | 等価変換（1 対象ピクセル = 1 DIP・無制限）へフォールバックし、例外にしません |
| カーソル位置を取得できない（未ロード・OS の取得失敗） | `TryGetCurrentLoupePosition` が `false` を返し、nudge の基点を記録しません（失敗を原点で表しません） |
| 倍率・寸法が未確定（非数・`0` 以下） | 安全域の基準値へ寄せて描画を続けます（例外にしません） |

## 12. 内部型（テストが参照する実装）

`internal` 型です（`InternalsVisibleTo` でテストからのみ参照します。 公開面ではありません）。

| 型 | 役割 |
| --- | --- |
| `MarqueeSkiaPainter`（内部・描画実装） | 描画計画（図形の層）を Skia の描画 API へ写します |
| `MarqueeLabelPainter`（内部・描画実装） | ラベルの配置指示（背景と文字）を Skia の描画 API へ写します |
| `MarqueeLogMessage`（内部・ログ定義） | 本コントロールのログ定義（接頭辞と操作の例外の記録） |
| `Internal` 名前空間のヘルパー（内部・境界変換） | 倍率の解決（`ClientPixelScale`）、座標とキーと色の相互変換、ダブルクリックの判定、Win32 の画面座標変換、テキストと矩形の描画 |
