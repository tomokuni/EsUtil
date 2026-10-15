# EsUtil.Feature.Loupe.ControlWinUI3 API リファレンス

`LoupeControl`（WinUI 3 版ルーペ）が外部利用者へ提供する機能と公開 API の仕様です。
内部実装の詳細（Internal のヘルパー等）は、利用者へ影響する範囲だけを記載します。

## 目次 <!-- omit from toc -->

- [1. 概要](#1-概要)
- [2. 公開 API](#2-公開-api)
  - [2.1 プロパティ](#21-プロパティ)
  - [2.2 メソッド](#22-メソッド)
  - [2.3 イベント](#23-イベント)
- [3. 描画仕様](#3-描画仕様)
  - [3.1 層の順序](#31-層の順序)
  - [3.2 サイズの決定](#32-サイズの決定)
  - [3.3 座標ラベルの表示条件](#33-座標ラベルの表示条件)
- [4. レイアウト](#4-レイアウト)
- [5. DPI](#5-dpi)
- [6. エラー時の挙動](#6-エラー時の挙動)
- [7. 描画完了イベント](#7-描画完了イベント)
- [8. ログ](#8-ログ)
- [9. 制限事項](#9-制限事項)
- [10. 非対応（今後の対応）](#10-非対応今後の対応)

## 1. 概要

ルーペ（拡大鏡）は、**拡大元の 100% 画像**と**拡大中心（元画像ピクセル座標）**を受け取り、
画像外の色（無地）・拡大画像・十字線・枠線・中心座標ラベルを描画するコントロールです。

名前空間 `EsUtil.Feature.Loupe.WinUI3` / クラス `LoupeControl`（`public sealed`、`SkiaSharp.Views.Windows.SKXamlCanvas` 派生）。

**他の UI 版の `LoupeControl` と同じ UI 非依存コア（`EsUtil.Feature.Loupe.Core`）と提示層（`EsUtil.Feature.Loupe.Presenter`）を使うため、設定値と見た目は複数の UI で一致します。**

利用の前提:

- 参照する側は WinUI 3（Windows App SDK）のプロジェクトとする（対象フレームワークは `net10.0-windows10.0.22621.0`、`UseWinUI` を有効にする）。
- 対応する OS は Windows 10 19041 以降とする（本体のプロジェクト ファイルが `SupportedOSPlatformVersion` を定める）。
- 公開プロパティは **CLR プロパティ**であるため、XAML のバインディング（`{x:Bind}` / `{Binding}`）では設定できない。 コードビハインドから設定する（理由は [`SPEC_System.md`](SPEC_System.md) の設計判断を参照）。

## 2. 公開 API

| 種別 | メンバー |
| --- | --- |
| プロパティ | `Source` / `CenterPoint` / `Options` / `Appearance` / `ClientToPixelScaling` / `PositionOffset` / `DrawPlanResolver` / `Log` / `LogPrefix` |
| メソッド | `ResolveLayout` / `SetCoordTransform` / `DrawPlan`（静的） |
| イベント | `Rendered` |

### 2.1 プロパティ

| プロパティ | 型 | 既定値 | 安全域（範囲外は自動補正） | 説明 |
| --- | --- | --- | --- | --- |
| `Source` | `EsReadOnlyBitmap?` | `null` | — | 拡大元の 100% 画像（**受け取る型はコアの画像（`EsReadOnlyBitmap`）だけ**。 フレームワークのビットマップ型や Skia の `SKBitmap` は受け付けない。 借用であり破棄しない）。 null の場合は画像外の色のみ |
| `CenterPoint` | `EsPoint?` | `null` | — | 拡大中心（元画像ピクセル座標）。 null の場合は画像外の色のみ |
| `Options` | `LoupeOptions` | `LoupeOptions.Default` | 下表のとおり | 設定（基本サイズ・拡大率）。 `with` 式で部分変更する |
| `Appearance` | `LoupeAppearance` | `LoupeAppearance.Default` | 下表のとおり | 外観（枠線の太さ・表示可否）と描画に使う色（画像外・枠線・十字線・中心座標ラベルの 6 色）・ラベルの体裁。 `with` 式で部分変更する。 外観はコアの計算に入らない |
| `ClientToPixelScaling` | `double` | 1.0 | 0.1〜8.0（非有限は既定値） | DIP → 物理ピクセルの係数（環境値。 設定には含めない）。 **描画のたびに描画面と割り当て領域の比へ合わせる**（DPI の変更通知が届かない状況でも拡大画像がルーペ領域と一致する） |
| `PositionOffset` | `double` | 0 | — | 拡大中心からの右下方向へのオフセット量（DIP）。 保持と位置の補正はコアが単一所有する |
| `DrawPlanResolver` | `Func<LoupeDrawPlan, LoupeDrawPlan>?` | `null` | — | **描画計画の差し替え点**（装飾の矩形・ラベルの文言と配置）。 設定すると再描画を要求する（同じ入力では 1 フレーム 1 回だけ解決されるため、呼び出しの時点に依存しない純粋関数にすること） |
| `Log` | `ILogger` | `NullLogger` | — | 設定変更ログと描画例外の出力先（null を設定した場合は出力なし） |
| `LogPrefix` | `string?` | `[Loupe]` | — | ログの接頭辞（提示層・コアへも伝搬する。 null または空白を設定すると既定値へ戻す） |

`CenterPoint` は基底（`UIElement`）の同名プロパティ（描画変換の中心点）を隠します。 名前を揃えて移植しやすくするためで、描画変換の中心点が必要な場合は基底の実装として扱います。

**見た目のカスタマイズ**: 色・枠線の太さ・表示可否は外観（`Appearance`）で指定します（**表示可否が描画の停止の唯一の入口**です。 表示を止めても計画は矩形と層を保ち、描画側がフラグを見て描かないため、ホストは止めた項目も計算値を使って自前で描けます）。 矩形の並びで表せる装飾とラベルの文言・位置は `DrawPlanResolver`（`Func<LoupeDrawPlan, LoupeDrawPlan>`）で描画計画（`LoupeDrawPlan`）を差し替えられます（例はサンプルの `GridLoupePlan`）。 矩形で表せない描画（円・斜線等）は `Appearance` の `Show*` を false にして `Rendered` のキャンバスへ自前で描き、必要なら `DrawPlan` で計画どおりの描画を行います。

`Options`（コアの `LoupeOptions`。 UI 非依存の値）の項目:

| 項目 | 型 | 既定値 | 安全域（範囲外は自動補正） | 説明 |
| --- | --- | --- | --- | --- |
| `RequestedSize` | `int` | 160 | 1〜4096 | 一辺のサイズ（DIP） |
| `ZoomLevel` | `int` | 4 | 1〜64 | 拡大率（偶数推奨） |

`Appearance`（`LoupeAppearance`。 色は UI 非依存の `System.Drawing.Color`）の項目:

| 項目 | 型 | 既定値 | 安全域（範囲外は自動補正） | 説明 |
| --- | --- | --- | --- | --- |
| `BorderThickness` | `int` | 2 | 1〜64 | 枠線の太さ（DIP） |
| `ShowCrosshair` / `ShowBorder` | `bool` | `true` | — | 十字線・枠線の表示可否（透明色を「描かない」の代用にしない） |
| `ShowCoordLabel` | `bool` | `true` | — | 左上の座標ラベルの表示可否（中心未設定時は描画しない） |
| `ShowPasteboard` | `bool` | `true` | — | 画像外（ペーストボード）の表示可否（非表示でも描画面を透明で塗る） |
| `PasteboardColor` | `System.Drawing.Color` | 中性グレー（`#FF535353`） | — | 画像外（ペーストボード）の色。 描画面を置き換えて前フレームの残像を残さない |
| `CrossColor` | `System.Drawing.Color` | 赤 40%（`#66FF0000`） | — | 十字線の色（不透明度はアルファ） |
| `BorderColor` | `System.Drawing.Color` | LightGray | — | 枠線の色 |
| `CoordLabelColor` | `System.Drawing.Color` | 白 | — | 座標ラベルの文字色 |
| `CoordLabelBackgroundColor` | `System.Drawing.Color` | 黒 60%（`#99000000`） | — | 座標ラベルの背景色 |
| `CoordLabelFontSize` | `double` | 12.0 | 6〜48（非有限は既定値） | 座標ラベルのフォントサイズ（DIP） |
| `CoordLabelCornerRadius` | `int` | 2 | 0〜64 | 座標ラベルの背景の角丸半径（DIP。 0 は角丸なし） |
| `CoordLabelOffset` | `double` | 2.0 | — | 座標ラベルを置く基準点の、ルーペ左上からの内側への距離（DIP） |

### 2.2 メソッド

| メソッド | 戻り値 | 説明 |
| --- | --- | --- |
| `ResolveLayout(EsSize parentSize)` | `LoupeLayout` | 親領域のサイズ（DIP）から表示レイアウト（表示サイズ・表示位置）を解決する。 位置が null になるのは、拡大中心が未設定の場合と、親領域が表示サイズを収められない場合である |
| `SetCoordTransform(ICoordTransform? port)` | — | 対象 ↔ 親領域 DIP の座標変換ポートを設定する（null は等価変換）。 例外は記録して再送出する |
| `DrawPlan(SKCanvas canvas, EsReadOnlyBitmap? source, in LoupeDrawPlan plan)` | — | 描画計画を指定の描画面へ描く（**静的**）。 ホストが任意のタイミングと順序で既定の描画を行える入口である |

使用例:

```csharp
// 描画完了の直後とは別のタイミングで、既定の描画を行いたい場合
Loupe.Rendered += (_, e) =>
{
    // e.Content は実際に使われた計画（切り出し左上・拡大率・描画サイズを持つ）
    LoupeControl.DrawPlan(e.Canvas, Loupe.Source, in e.Content);
    // 追加のオーバーレイはここへ描く（キャンバスはルーペ画像のピクセル座標）
};
```

### 2.3 イベント

| イベント | 引数 | 説明 |
| --- | --- | --- |
| `Rendered` | `LoupeRenderedEventArgs` | 描画完了の通知。 `Canvas`（描画済みのキャンバス）と `Content`（実際に使われた描画計画）を渡す |

## 3. 描画仕様

**描く層とその順序は提示層が単一所有します**（`LoupeDrawLayer`）。 描画計画（`LoupeDrawPlan`）が層の順序を解決し、描画実装（`LoupeSkiaPainter`。 内部）はその順に写すだけです。

### 3.1 層の順序

層の順序は次のとおりです（後から描いたものが上に重なります）。

| 順序 | 要素 | 備考 |
| --- | --- | --- |
| 1 | 画像外（ペーストボード） | 描画面の全体を塗る（表示可否が false の場合は透明で塗る ＝ 前フレームの残像を残さない） |
| 2 | 拡大画像 | `Source` と `CenterPoint` がある場合のみ。 切り出し領域の外は透明（画像外の色が見える） |
| 3 | 十字線 | 中央の縦横 2 本（幅は拡大率と同じ物理ピクセル） |
| 4 | 枠線 | 四辺（`Appearance.BorderThickness` の太さ） |
| 5 | 中心座標ラベル | **描画面へ描く**（背景の角丸矩形 + 文字）。 常に最上位 |

中心座標ラベルは、文言と基準点を提示層（`LoupePresenter`）が、体裁（フォントサイズ・色・角丸・内側余白）を外観（`LoupeAppearance`）が単一所有し、実寸の測定と描画は同じ Internal のヘルパー（`SkiaText`）が担います（測定と描画に関数が分かれないため、背景と文字の大きさが必ず一致します）。 配置指示（`LoupeLabelPlacement`）の寸法は DIP であるため、描画時にスケーリング係数を掛けて物理ピクセルへ換算します。

### 3.2 サイズの決定

- 一辺の表示サイズ（DIP）は `Options.RequestedSize` である（親領域に合わせた縮小はしない）。
- 描画面（Skia の表面）は、要素の表示サイズへ表示倍率を掛けた大きさになる。 描画計画の矩形（描画面・拡大画像・枠線の太さ）は**物理ピクセル**で解決されるため、キャンバスは拡大せず 1:1 で描く。
- 描画のたびに、描画面の大きさと割り当て領域（DIP）の比から実効倍率を求め、`ClientToPixelScaling` と提示層へ反映する（DPI の変更通知が届かない状況でも整合する）。
- **`MeasureOverride` は一辺 `RequestedSize` の正方形を返す**（レイアウトの要求サイズ）。 表示位置はホストが `ResolveLayout` の結果で配置する。

### 3.3 座標ラベルの表示条件

| 条件 | 結果 |
| --- | --- |
| `Appearance.ShowCoordLabel` が false | 描画しない（文言と配置指示は計画に残る） |
| `CenterPoint` が未設定 | 描画しない（文言を持たない） |
| 上記以外 | `CenterPoint` の座標を `(x, y)` の形式で描く（書式はコアの値型が単一所有する） |

配置指示の `IsVisible` が表示可否を持ち、描画実装は表示が有効な指示だけを描きます。

## 4. レイアウト

本コントロールは**位置を持ちません**。 表示位置はコアのファサード（`LoupeCore`）が計算し、ホストがその結果で配置します。

```csharp
var layout = Loupe.ResolveLayout(new EsSize(host.ActualWidth, host.ActualHeight));
if (layout.Position is { } position)
{
    Loupe.Width = layout.ClientSize;
    Loupe.Height = layout.ClientSize;
    Canvas.SetLeft(Loupe, position.X);
    Canvas.SetTop(Loupe, position.Y);
}
```

- `LoupeLayout.ClientSize` は整数化しない（要素の大きさと描画の入力が同じ値になるよう、途中で切り捨てない）。
- `PositionOffset`（DIP）は拡大中心からの右下方向へのずれを与える（保持と補正はコアが単一所有する）。
- 親領域が表示サイズを収められない場合と拡大中心が未設定の場合は、位置が null になる（「配置しない」の判断はコアが単一所有する）。
- ホストは位置の変化（中心・設定・オフセット・表示の切り替え）の後に配置をやり直す（本コントロールは自身のレイアウトを要求しない）。

## 5. DPI

- 表示倍率（DIP → 物理ピクセル）は `ClientToPixelScaling` が保持する。
- ツリーへ接続された時点で `XamlRoot` の変更を購読し、表示倍率が変わったら `ClientToPixelScaling` を更新する。
- あわせて**描画のたびに**描画面の大きさと割り当て領域の比を実測し、保持値へ反映する（追加直後のコントロールへ通知が届かない場合でも、拡大画像と図形の倍率が一致する）。
- 値の補正（0.1〜8.0。 非有限は既定値）はコアの `LoupeCore.Limits` が単一所有する。

## 6. エラー時の挙動

| 状況 | 挙動 |
| --- | --- |
| 描画中の例外 | アプリを落とさない。 `Log` へ Error（`[Loupe] Update exception`）を記録し、そのフレームの描画を中断する（次の描画で復帰する） |
| 描画中に拡大元画像が破棄された場合 | 拡大画像の層だけを描かずに戻る（画像外の色のみが残る） |
| `SetCoordTransform` の例外 | Error（`[Loupe] SetCoordTransform で例外が発生`）を記録して再送出する |

例外処理の共通規則は [`例外処理規約.md`](../../../.github/例外処理規約.md) に従います。

## 7. 描画完了イベント

`Rendered` は描画の直後（同一フレーム内）に発火し、描画済みのキャンバスと実際に使われた描画計画を渡します。

- `LoupeRenderedEventArgs.Canvas`: 拡大画像のピクセル座標系（1 ピクセル = ルーペ画像の 1 ピクセル）。
- `LoupeRenderedEventArgs.Content`: `CaptureTopLeft`（対象座標の可視域の左上。 小数を含む）、`ZoomLevel`（拡大率）、`ClientPixelSize`（一辺の物理ピクセル）を持つ。
- 対象座標のオーバーレイは `(対象座標 − CaptureTopLeft) × ZoomLevel` の位置へ描く。
- DIP で見せたい寸法は `LoupeDrawPlan.ScaleDipToPixel`（拡大率とスケーリングから物理ピクセルへ換算する唯一の入口）を使う。
- 中心未設定時は `CaptureTopLeft` が null である（オーバーレイを描く位置が決まらない）。

## 8. ログ

`Log` へ `ILogger` を設定すると、次のログを出力します（既定は出力なし）。

| レベル | 出力書式 | 所有者 |
| --- | --- | --- |
| Debug | `[Loupe] Setting: Source {前} → {後}` | 本プロジェクトの `LoupeLogMessage`（拡大元画像） |
| Debug | `[Loupe] Setting: {名前} {前} → {後}` | コア（設定値・環境値・拡大中心）と提示層（外観） |
| Error | `[Loupe] Update exception` | 本プロジェクトの `LoupeLogMessage`（描画の例外） |
| Error | `[Loupe] {操作} で例外が発生` | 本プロジェクトの `LoupeLogMessage`（公開 API の例外） |

接頭辞は `LogPrefix` で変更でき、提示層・コアへも伝搬します（全層のログが同じ接頭辞になる）。 変化判定（同値・同一インスタンスなら出力しない）は出力する主体が単一所有します。

## 9. 制限事項

- **公開プロパティは CLR プロパティである**（依存関係プロパティではない）。 XAML のバインディングでは設定できないため、コードビハインドから設定する。 また、`CenterPoint` は基底（`UIElement`）の同名プロパティを隠す。
- **XAML から設定できるのは XAML が扱えるプロパティだけ**である（`x:Name` や `Visibility` 等）。 コアと描画層の型を取るプロパティはコードから設定する。
- 拡大元の画像はコアの画像（`EsReadOnlyBitmap`）だけを受け取る（フレームワークのビットマップ型や Skia の `SKBitmap` は受け付けない）。
- 表示位置は本コントロールが持たない（ホストが `ResolveLayout` の結果で配置する）。
- 連続再描画は行わない（静止画として扱う。 入力や設定の変更時にのみ描画する）。
- `Clipping`・変形（`Rotation` / `Scale` 等）は本コントロールの描画に反映されない（描画面は要素の表示サイズから作る）。

## 10. 非対応（今後の対応）

- 補間（バイリニア等）による拡大表示は持たない（最近傍で描く）。 拡大方式を選べるようにする場合は、描画の設定として外観と同じ層へ追加する。
- ポインター入力による拡大中心の移動は持たない（ホストが入力を受けて `CenterPoint` を設定する）。
- タッチ・ペン固有の入力は持たない（ポインターの種類を区別しない）。
- 印刷・画像の書き出しは持たない（`Rendered` のキャンバスへ描いた結果をホストが保存する）。
