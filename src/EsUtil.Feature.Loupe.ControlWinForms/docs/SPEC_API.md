# EsUtil.Feature.Loupe.ControlWinForms API リファレンス

`EsUtil.Feature.Loupe.ControlWinForms`（WinForms 版ルーペ コントロール）の公開 API を記載します。 対象フレームワークは `net10.0-windows`、名前空間は `EsUtil.Feature.Loupe.WinForms` です。

## 目次 <!-- omit from toc -->

- [1. 概要](#1-概要)
- [2. 名前空間と型](#2-名前空間と型)
  - [2.1 公開面の型](#21-公開面の型)
  - [2.2 内部の型](#22-内部の型)
- [3. LoupeControl](#3-loupecontrol)
  - [3.1 コンストラクター](#31-コンストラクター)
  - [3.2 プロパティ](#32-プロパティ)
  - [3.3 メソッド](#33-メソッド)
  - [3.4 イベント](#34-イベント)
  - [3.5 継承するメンバーの上書き](#35-継承するメンバーの上書き)
- [4. LoupeRenderedEventArgs](#4-louperenderedeventargs)
- [5. 使用例](#5-使用例)
- [6. 前提条件（座標系・単位）](#6-前提条件座標系単位)
- [7. 制限事項](#7-制限事項)

---

## 1. 概要

`LoupeControl` は `System.Windows.Forms.Control` を継承し、`OnPaint` でルーペを描画します。
`EsUtil.Platform.Core` の画像（`EsReadOnlyBitmap`）を拡大元として受け取り、`EsUtil.Feature.Loupe.Presenter` が解決した描画計画（`LoupeDrawPlan`）を GDI+ へ写して描きます。

## 2. 名前空間と型

### 2.1 公開面の型

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `LoupeControl` | `sealed class : Control` | ルーペ本体（設定・描画・DPI 追従・描画完了通知） |
| `LoupeRenderedEventArgs` | `sealed class : EventArgs` | 描画完了通知の引数（描画面と描画計画） |

`LoupeOptions` / `LoupeAppearance` / `LoupeDrawPlan` / `LoupeLayout` などの公開面は提示層（`EsUtil.Feature.Loupe.Presenter`）が所有します。

### 2.2 内部の型

本プロジェクトの内部型は、同一ソリューションのテスト（`InternalsVisibleTo`）だけが参照します。 利用側は参照できません。

| 型 | 説明 |
| --- | --- |
| `LoupeGraphicsPainter`（内部・描画実装） | 描画計画を GDI+ へ写す（`LoupeControl.DrawPlan` の実体） |
| `LoupeLogMessage`（内部・ログ定義） | 拡大元画像の変更と描画の例外のログ定義（`internal`） |
| `ClientPixelScale`（内部・DPI 換算） | クライアント領域（物理ピクセル）と DIP から実効倍率を解決する |
| `EsBitmapDrawingInterop`（内部・画像変換） | コアの画像の一部を GDI+ のビットマップへ複製する |
| `EsGeometryDrawingInterop`（内部・幾何変換） | コアの幾何型を GDI+ の型へ変換する |
| `DrawingLabelText`（内部・文字） | ラベルの文字の実寸測定と描画（測る API と描く API を揃える） |

## 3. LoupeControl

`public sealed class LoupeControl : System.Windows.Forms.Control`

コントロールのクライアント領域は**物理ピクセル**です。 描画計画の描画面（`LoupeDrawPlan.ClientPixelRect`）とクライアント領域は一致します（`Options.RequestedSize`（DIP）と `ClientToPixelScaling` の積）。
拡大元画像（`Source`）の所有権はホストにあり、本コントロールは破棄しません。

### 3.1 コンストラクター

| シグネチャ | 説明 |
| --- | --- |
| `LoupeControl()` | インスタンスを初期化します。 `ControlStyles.UserPaint` / `AllPaintingInWmPaint` / `OptimizedDoubleBuffer` / `ResizeRedraw` を有効にします（ハンドルは生成しません） |

### 3.2 プロパティ

| シグネチャ | 既定値 | 説明 |
| --- | --- | --- |
| `EsReadOnlyBitmap? Source { get; set; }` | `null` | 拡大元の 100% 画像（借用）。 別インスタンスへの差し替えはログへ記録されます |
| `EsPoint? CenterPoint { get; set; }` | `null` | 拡大中心（元画像ピクセル座標）。 設定すると描画計画を破棄して再描画します |
| `LoupeOptions Options { get; set; }` | `LoupeOptions.Default` | 設定（表示サイズ（DIP）・拡大率）。 値の補正はコアの型が行います |
| `LoupeAppearance Appearance { get; set; }` | `LoupeAppearance.Default` | 外観（色・枠線の太さ・表示可否・ラベルの体裁） |
| `double ClientToPixelScaling { get; set; }` | `1.0` | DIP → 物理ピクセルの係数。 0.1〜8.0 へ補正（非有限は既定値）。 ハンドルの生成時と DPI の変更時に実測値へ更新されます |
| `double PositionOffset { get; set; }` | `0` | ルーペ中心からの右下オフセット（DIP）。 位置の計算はコアが行います |
| `Func<LoupeDrawPlan, LoupeDrawPlan>? DrawPlanResolver { get; set; }` | `null` | 描画計画の差し替え（純粋関数）。 設定すると保持中の計画を破棄します |
| `ILogger Log { get; set; }` | `NullLogger` | ログの出力先（null は `NullLogger`）。 提示層とコアへ伝搬します |
| `string? LogPrefix { get; set; }` | `[Loupe]` | ログの接頭辞（空白または null は既定値へ戻す）。 提示層とコアへ伝搬します |

公開プロパティはいずれもデザイナーのコード生成の対象外です（`DesignerSerializationVisibility.Hidden`。 値が実行時のオブジェクトであるため）。

### 3.3 メソッド

| シグネチャ | 説明 |
| --- | --- |
| `void SetCoordTransform(ICoordTransform? port)` | 対象座標 → 親領域 DIP の変換ポートを設定します（null は等価変換）。 内部で発生した例外は記録して再スローします |
| `LoupeLayout ResolveLayout(EsSize parentSize)` | 親領域のサイズ（DIP）から表示レイアウト（表示サイズ（DIP）と表示位置（DIP））を解決します。 位置は「拡大中心が未設定」「親領域が表示サイズを収められない」の場合に `null` になります |
| `int ToClientPixel(double dipValue)` | DIP の長さを物理ピクセルへ換算します（切り上げ・最低 1）。 `ResolveLayout` の結果を配置へ写す入口です |
| `EsSize ToDipSize(Size clientPixelSize)` | クライアント座標（物理ピクセル）のサイズを DIP へ換算します。 `ResolveLayout` へ渡す親領域のサイズを作る入口です |
| `static void DrawPlan(Graphics graphics, EsReadOnlyBitmap? source, in LoupeDrawPlan plan)` | 描画計画を指定の描画面へ描きます（既定の描画をホストが任意の順序で行うための入口）。 `graphics` が null の場合は `ArgumentNullException` |
| `double ApplyClientPixelScaling()`（内部・テストが参照） | クライアント領域と DPI から実効倍率を解決し、`ClientToPixelScaling` へ反映します。 保持値の更新は自動で行われるため、ホストが呼ぶ必要はありません |
| `LoupeDrawPlan ResolveCurrentPlan()`（内部・テストが参照） | 直近の状態から描画計画（1 フレーム分）を解決します。 同じ入力では再解決しません |

### 3.4 イベント

| シグネチャ | 説明 |
| --- | --- |
| `event EventHandler<LoupeRenderedEventArgs>? Rendered` | 描画の直後（同一フレーム内）に発火します。 引数の描画面は描画済みであり、ハンドラー内でオーバーレイを追加描画できます |

### 3.5 継承するメンバーの上書き

| シグネチャ | 説明 |
| --- | --- |
| `protected override void OnPaint(PaintEventArgs e)` | ルーペを描画します。 描画の例外は記録して継続します（アプリを落としません） |
| `protected override void OnHandleCreated(EventArgs e)` | スケーリング係数を DPI へ追随させます |
| `protected override void OnDpiChangedAfterParent(EventArgs e)` | 親の DPI の変更後へ追随し、再描画します |

## 4. LoupeRenderedEventArgs

`public sealed class LoupeRenderedEventArgs : EventArgs`

| シグネチャ | 説明 |
| --- | --- |
| `LoupeRenderedEventArgs(Graphics graphics, LoupeDrawPlan content)` | 描画面と描画計画を保持します |
| `Graphics Graphics { get; }` | ルーペを描画した描画面（描画済み。 座標系は物理ピクセル）。 描画の前に保存した状態へ復帰済みです |
| `LoupeDrawPlan Content { get; }` | 実際に使われた描画計画（切り出し領域の左上・拡大率・描画面のサイズ） |

## 5. 使用例

```csharp
using EsUtil.Feature.Loupe;
using EsUtil.Feature.Loupe.WinForms;
using EsUtil.Platform.Core.Geometry;

var loupe = new LoupeControl
{
    Source = source,                          // EsReadOnlyBitmap（読み取りの面）
    CenterPoint = new EsPoint(320, 240),
    Options = LoupeOptions.Default with { RequestedSize = 240, ZoomLevel = 4 },
};

parent.Controls.Add(loupe);

// 配置（親領域は物理ピクセルであるため、ToDipSize で換算して渡す）
var layout = loupe.ResolveLayout(loupe.ToDipSize(parent.ClientSize));
if (layout.Position is { } position)
{
    var size = loupe.ToClientPixel(layout.ClientSize);
    loupe.Bounds = new Rectangle(loupe.ToClientPixel(position.X), loupe.ToClientPixel(position.Y), size, size);
}

// 描画完了通知（Rendered）へオーバーレイを追加描画する
loupe.Rendered += (_, e) =>
{
    var plan = e.Content;
    PaintOverlay(e.Graphics, in plan);
};

// 描画計画を差し替える（矩形の並びで表せる装飾とラベルの文言・位置）
loupe.DrawPlanResolver = static plan => plan with { CrosshairPixelRects = [new EsRect(0, 0, 4, 40)] };
```

## 6. 前提条件（座標系・単位）

| 値 | 単位 | 説明 |
| --- | --- | --- |
| `Source` / `CenterPoint` | 元画像ピクセル | 拡大元画像の座標（左上が原点） |
| `Options.RequestedSize` / `PositionOffset` | DIP | 表示サイズとオフセット。 `ClientToPixelScaling` で物理ピクセルへ換算します |
| `ResolveLayout` の引数と戻り値 | DIP | 親領域のサイズと表示レイアウト（配置はホストが `ToClientPixel` で換算します） |
| `LoupeDrawPlan.ClientPixelRect` / `CrosshairPixelRects` / `BorderPixelRects` | 物理ピクセル | 描画面の座標（`ScaleDipToPixel` で DIP から換算できます） |
| `LoupeDrawPlan.Labels` の `DipRect` | DIP | 描画層が物理ピクセルへ換算して描きます（`ClientToPixelScaling`） |
| `LoupeRenderedEventArgs.Graphics` | 物理ピクセル | 描き込み先の描画面（`1` ピクセル = ルーペ画像の `1` ピクセル） |

## 7. 制限事項

- **Windows 専用**です（`System.Drawing` を使うため）。 対象フレームワークは `net10.0-windows` です。
- **透明を画面へ伝えられません**: ペーストボード（画像外の色）を非表示（`ShowPasteboard = false`）にすると、描画面は置き換えられず**下地（コントロールの `BackColor`）**が見えます。 他の UI のように親の内容が透けることはありません。
- **コントロール自身は位置を持ちません**: 表示位置と表示サイズはホストが `ResolveLayout` の結果で決めます。
- **拡大元画像は借用です**: ホストが破棄した画像を描画しようとした場合、画像の層は描かれず下地のみが残ります（例外は記録され、アプリは継続します）。
- **マウス入力を持ちません**: 拡大中心の更新はホストが行います（本コントロールは `MouseMove` などを処理しません）。
- **デザイナーには出しません**: 公開プロパティはデザイナーのコード生成の対象外です。
