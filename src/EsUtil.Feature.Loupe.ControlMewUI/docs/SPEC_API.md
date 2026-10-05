# EsUtil.Feature.Loupe.ControlMewUI API リファレンス

`LoupeControl`（MewUI 版ルーペ）が外部利用者へ提供する機能と公開 API の仕様です。
内部実装の詳細（描画手順・キャッシュ等）は記載しません。

## 目次

- [1. 概要](#1-概要)
- [2. 公開 API](#2-公開-api)
  - [2.1 プロパティ](#21-プロパティ)
- [3. 描画仕様](#3-描画仕様)
  - [3.1 サイズの決定](#31-サイズの決定)
  - [3.2 座標ラベルの表示条件](#32-座標ラベルの表示条件)
- [4. レイアウト](#4-レイアウト)
- [5. DPI](#5-dpi)
- [6. エラー時の挙動](#6-エラー時の挙動)
- [7. 描画完了イベント](#7-描画完了イベント)
- [8. ログ](#8-ログ)
- [9. 非対応（今後の対応）](#9-非対応今後の対応)

## 1. 概要

ルーペ（拡大鏡）は、**拡大元の 100% 画像**と**拡大中心（元画像ピクセル座標）**を受け取り、
画像外の色（無地）・拡大画像・十字線・枠線を描画するコントロールです（中心座標ラベルはテキスト要素として表示します）。

**Avalonia 版 `LoupeControl` と同じ UI 非依存コア（`EsUtil.Feature.Loupe.Core`）を使うため、設定値と見た目は両 UI で一致します。**

## 2. 公開 API

名前空間 `EsUtil.Feature.Loupe.MewUI` / クラス `LoupeControl`（`public sealed`、`Panel` 派生。 図形は子の `SkiaCanvasView` が描画する）。

### 2.1 プロパティ

| プロパティ | 型 | 既定値 | 安全域（範囲外は自動補正） | 説明 |
| --- | --- | --- | --- | --- |
| `Source` | `EsBitmap?` | `null` | — | 拡大元の 100% 画像。 null の場合は画像外の色のみ |
| `CenterPoint` | `EsPoint?` | `null` | — | 拡大中心（元画像ピクセル座標）。 null の場合は画像外の色のみ |
| `Options` | `LoupeOptions` | `LoupeOptions.Default` | 下表のとおり | 設定（基本サイズ・拡大率）。 `with` 式で部分変更する |
| `Appearance` | `LoupeAppearance` | `LoupeAppearance.Default` | 下表のとおり | 外観（枠線の太さ・表示可否）と描画に使う色（ペーストボード（画像外）・枠線・十字線・中心座標ラベルの 6 色）・ラベルの体裁。 `with` 式で部分変更する。 外観はコアの計算に入らない |
| `ClientToPixelScaling` | `double` | 1.0 | 0.1〜8.0（非有限は既定値） | DIP → 物理ピクセルの係数（環境値。 設定には含めない）。 **描画のたびに描画面と割り当て領域の比へ合わせる**（DPI の変更通知が届かない状況でも拡大画像がルーペ領域と一致する） |

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
| `PasteboardColor` | `DrawingColor` | 中性グレー（`#FF535353`） | — | 画 像外（ペーストボード）の色。 描画面を置き換えて前フレームの残像を残さない（非表示時は透明で塗る） |
| `CrossColor` | `DrawingColor` | 赤 40%（`#66FF0000`） | — | 十字線の色（不透明度はアルファ） |
| `BorderColor` | `DrawingColor` | LightGray | — | 枠線の色 |
| `CoordLabelColor` | `DrawingColor` | 白 | — | 座標ラベルの文字色 |
| `CoordLabelBackgroundColor` | `DrawingColor` | 黒 60%（`#99000000`） | — | 座標ラベルの背景色 |
| `CoordLabelFontSize` | `double` | 12.0 | 6〜48（非有限は既定値） | 座標ラベルのフォントサイズ（DIP） |
| `CoordLabelCornerRadius` | `int` | 2 | 0〜64 | 座標ラベルの背景の角丸半径（DIP。 0 は角丸なし） |

`Color` は `Aprillz.MewUI.Color`、`DrawingColor` は `System.Drawing.Color`（`Appearance` の色）です。

## 3. 描画仕様

**描く層とその順序は提示層が単一所有します**（`LoupeDrawLayer`）。 描画計画（`LoupeDrawPlan`）が層の順序を解決し、描画実装（`LoupeSkiaPainter`）はその順に写すだけです。 層の順序は次のとおりです（後から描いたものが上に重なります）。

| 順序 | 要素 | 備考 |
| --- | --- | --- |
| 1 | 拡大画像 | `Source` と `CenterPoint` がある場合のみ。 切り出し領域の外は透明（画像外の色が見える） |
| 4 | 十字線 | 中央の縦横 2 本（幅は拡大率と同じ物理ピクセル） |
| 5 | 枠線 | 四辺（`Appearance.BorderThickness` の太さ） |

中心座標ラベルはコントロールが **`Border` + `TextBlock` の子要素として配置**します（上記の後ろに重なります。 文言は提示層の `LoupePresenter`（`LabelTexts` partial）、配置は同層の `LoupePresenter`（`LabelPlan` partial）、体裁は外観（`LoupeAppearance`）、実寸測定は **MewUI のテキスト エンジン**（`MewUITextMeasure.Measure`）が単一所有します）。 子要素は親より後に描かれるため、ラベルは常に最上位になります。

### 3.1 サイズの決定

| 項目 | 決定方法 |
| --- | --- |
| 表示サイズ（物理ピクセル） | `Options.RequestedSize`（DIP ） × `ClientToPixelScaling`。 キャンバスより大きい場合はキャンバスに合わせる。 **描画時の `ClientToPixelScaling` は描画面と割り当て領域の比である**ため、画像の大きさは常にルーペ領域（描画面）と一致する |
| 拡大率 | `Options.ZoomLevel` 倍（最近傍で拡大する） |
| 切り出し領域の一辺 | `ceil(表示サイズ / Options.ZoomLevel)` 対象ピクセル |
| 切り出し領域の左上 | 拡大中心 − 切り出し一辺 / 2（整数化）。 画像の外へはみ出した部分（左・上・右・下）は透明パディングとなる |

### 3.2 座標ラベルの表示条件

`Appearance.ShowCoordLabel` が `true` かつ `CenterPoint` が設定済みの場合のみ表示します（判断は外観の `LoupeAppearance.ShouldShowCoordLabel` が単一所有する）。 ラベルの色は外観（`Appearance` の `CoordLabelColor` / `CoordLabelBackgroundColor`）が単一所有します。

## 4. レイアウト

| 項目 | 仕様 |
| --- | --- |
| 要求サイズ | 一辺 `Options.RequestedSize`（DIP）の正方形（`Options` の変更はレイアウトを再評価する） |
| 表示位置 | **本コントロールは位置を決定しない**。 ホストが親のレイアウト（`HorizontalAlignment` / `VerticalAlignment` / `Margin` / `Canvas` 等）で配置する |

## 5. DPI

モニター間の移動で DPI が変わると、`ClientToPixelScaling` は DPI から換算した値（DIP 基準 96 DPI ≠ 1.0）へ自動更新されます（換算規則は `DpiScaler.ToScaling` が単一所有します）。

**ただし DPI の変更通知は追加直後のコントロールへ届かない**ため（実測: ウィンドウ表示後に追加したコントロールは登録時の既定値 1.0 のままになる）、描画のたびに**描画面（物理ピクセル）と割り当て領域（DIP）の比**を実効倍率として使います。
この値を保持値にも反映するため、画像の大きさは常にルーペ領域と一致します（125% の環境で 1/1.25 の画像を描く不具合を防ぐ）。
ホストがスケーリング係数を設定する必要はありません（明示設定した値も DPI 変更時は上書きされます）。

## 6. エラー時の挙動

| 状況 | 挙動 |
| --- | --- |
| `Source` が `null` | 画像外の色のみを描画する（例外にしない） |
| `CenterPoint` が `null` | 画像外の色のみを描画する（例外にしない） |
| `Source` が破棄済み | 画像外の色のみを描画する（例外にしない） |
| 色のアルファが 0 | 描画の可否には使わない（表示可否は外観（`LoupeAppearance`）の明示フラグが単一所有し、非表示の項目は空の描画計画として渡る）。 アルファ 0 の色は描画結果として見えなくなるだけである |
| 安全域外の数値 | 安全域へ補正する（例外にしない） |

## 7. 描画完了イベント

`Rendered` イベントは、ルーペを描画した直後（同一フレーム内）に引数 `LoupeRenderedEventArgs` で通知します。 ホストはハンドラ内で描画先キャンバスへ追加描画（選択矩形の破線等のオーバーレイ）を行えます。

中心座標ラベルは**コントロールが子要素として表示します**（`Border` + `TextBlock`）。 文言は提示層の `LoupePresenter`（`LabelTexts` partial）、表示可否と体裁は外観（`LoupeAppearance`）、実寸からの配置は同層の `LoupePresenter`（`LabelPlan` partial） が単一所有するため、コントロールは自 UI の API（`SkiaText.Measure`）で測った実寸を渡し、返された配置を要素へ反映するだけです。

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `Canvas` | `SKCanvas` | ルーペを描画したキャンバス（座標系はルーペ画像のピクセル） |
| `Content` | `LoupeDrawPlan` | 実際に描画した描画計画（切り出し領域の左上 `CaptureTopLeft`・拡大率 `ZoomLevel`・描画面のサイズ `ClientPixelSize` を持つ） |

描画入力（`LoupeGeometry`）の組み立てはコアのファサード（`LoupeCore.ResolveGeometry`）が単一所有し、コントロールは現在の状態を渡すだけです。

オーバーレイの描画位置は `e.Content.CaptureTopLeft` と `e.Content.ZoomLevel` から `(対象座標 − CaptureTopLeft) × ZoomLevel` で求めます （切り出し領域と倍率の計算は Avalonia 版と同じコアの計算を共有します）。

## 8. ログ

`Log` プロパティへ `ILogger` を設定すると、描画の例外と `Source` の変更を出力します（未設定・null の場合は `NullLogger` になり出力しません）。 設定（`Options`）・環境値（`ClientToPixelScaling`）・拡大中心（`CenterPoint`）の変更ログはコアが出力するため、本コントロールは同じロガーをコアへも渡します。

| レベル | 出力内容 |
| --- | --- |
| Debug | `[Loupe] Setting: {名前} {前} → {後}`（コアの設定）/ `[Loupe] Setting: Source {前} → {後}`。 補正後の値を渡し、変化判定（同一値・同一インスタンスなら出力しない）は出力する主体のログ定義（Source は `EsUtil.Feature.Loupe.Presenter`、他はコア）が単一所有する。 設定の変更は変化した項目だけを出力する） |
| Error | `[Loupe] Update exception`（描画の例外。 スタックトレース付き。 アプリは継続する） |

数値の補正（`coerce`）と変更ログ（`changed`）は**プロパティの登録（`MewProperty.Register`）**へ渡します。 したがって setter を経由しない設定（バインディング・`MewObject.SetValue` 相当）でも安全域が保たれ、変更が記録されます（Avalonia 版の `coerce` コールバック + `Changed` クラスハンドラと同じ契約）。

設定（`Options`）と寸法（`Options`）の数値項目は**コアの型が生成時に補正**するため、本コントロールは補正を登録しません（値の設定経路（オブジェクト初期化子・`with` 式・逆シリアル化・バインディング）によらず範囲内の値だけが保持されます）。

Avalonia 版の再描画パイプライン（RequestRender / RenderCore / GenerateAsyncDone）に相当する Trace ログは持ちません（MewUI 版は同期描画のため）。

## 9. 非対応（今後の対応）

| 項目 | 状況 |
| --- | --- |
| 座標変換ポート（`SetCoordTransform`） | 実装済み（`ToDip` = 対象 → 親領域 DIP。 表示位置の計算はコアのファサードが行い、コントロールは本ポートをファサードへ渡す） |
| 再生成の要求メソッド（`Clear` / `RequestRender`） | 未実装（プロパティ変更で自動再描画される） |
