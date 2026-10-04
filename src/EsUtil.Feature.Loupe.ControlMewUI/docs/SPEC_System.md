# EsUtil.Feature.Loupe.ControlMewUI システム仕様書

`LoupeControl`（MewUI 版ルーペ）のシステム概要・アーキテクチャ・依存関係・設計判断を記載します。

## 目次

- [1. システム概要](#1-システム概要)
- [2. 技術スタック](#2-技術スタック)
- [3. アーキテクチャ](#3-アーキテクチャ)
  - [3.1 依存方向](#31-依存方向)
  - [3.2 責務の分担](#32-責務の分担)
- [4. 例外処理ポリシー](#4-例外処理ポリシー)
- [5. コンポーネント構成](#5-コンポーネント構成)
- [6. クラス図](#6-クラス図)
- [7. シーケンス図（描画）](#7-シーケンス図描画)
- [8. 設計判断](#8-設計判断)
- [9. テスト方針](#9-テスト方針)
- [10. 未実装・今後の対応](#10-未実装今後の対応)
- [11. ログ](#11-ログ)

## 1. システム概要

`EsUtil.Feature.Loupe.ControlMewUI` は、Avalonia 版 `LoupeControl` と**同一の UI 非依存コア（`EsUtil.Feature.Loupe.Core`）と描画層（`EsUtil.Feature.Loupe.Presenter`）** を使い、
描画（`SKCanvas`）と入力の受付のみを MewUI（SkiaSharp）で実装したルーペコントロールです。

コアが担う範囲（数値の安全域補正・切り出し領域の計算・装飾矩形の計算）は両 UI で完全に共通であり、
本プロジェクトはその計算結果をキャンバスへ描画するだけです。
これにより **UI ごとの見た目の差を作らずに済み**、UI 追加時の再実装は描画 API の呼び出しのみになります。

## 2. 技術スタック

| 区分 | 採用技術 |
| --- | --- |
| 対象フレームワーク | .NET 10（`net10.0`） |
| 言語 | C# 14 |
| UI フレームワーク | MewUI（`Aprillz.MewUI.Skia` **0.21.1 固定**） |
| 描画 | SkiaSharp（描画実装 `LoupeSkiaPainter` が `SKCanvas` へ描く） |
| 基底コントロール | `Aprillz.MewUI.Controls.Panel`（図形は子の `SkiaCanvasView` が描画する） |
| プロパティシステム | `MewProperty<T>`（`MewPropertyOptions.AffectsRender` / `AffectsLayout`） |

## 3. アーキテクチャ

### 3.1 依存方向

層の全体像（Core / Presenter / UI）は [`src/README.md`](../README.md) を参照してください。

```text
UI 層（Avalonia / MewUI の実装）
  入力の受付・効果の適用・通知の転送・描画先の用意・ラベル要素の配置
    └──> EsUtil.Feature.Loupe.Presenter   どう描くか（描画計画の解決と外観の単一所有）
           └──> EsUtil.Feature.Loupe.Core   何を描くか + 判断（状態・値・純粋計算・幾何）
                  └──> EsUtil.Platform.Core（Es* 値型・ICoordTransform）
※ Core は Presenter / UI を参照しない（依存は UI → Presenter → Core の一方向）。
```

Avalonia 版と MewUI 版の間に依存はありません（互いを参照しません）。
両者に共通する計算は必ず `EsUtil.Feature.Loupe.Core` を経由します。

### 3.2 責務の分担

| 層 | 責務 |
| --- | --- |
| `EsUtil.Feature.Loupe.Core` | 数値の安全域補正、表示レイアウト、切り出し領域の幾何（`LoupeGeometry`）。 設定（`LoupeOptions`）の単一所有 |
| `EsUtil.Feature.Loupe.Presenter` | 描画計画（層の順序・装飾の矩形・ラベルの文言と配置）と外観（`LoupeAppearance`）の単一所有。 UI 非依存であり、UI フレームワークを参照しない |
| `EsUtil.Feature.Loupe.ControlMewUI` | 提示層が解決した描画計画を `LoupeSkiaPainter` で `SKCanvas` へ写す。 `MewProperty` による入力の受付。 DPI の自動追従 |
| ホスト（上位のアプリ） | `Source` / `CenterPoint` の設定、表示位置の決定（コアのファサード `ResolveLayout` を利用） |

## 4. 例外処理ポリシー

| 状況 | 挙動 |
| --- | --- |
| 描画中に拡大元が破棄された（`ObjectDisposedException`） | 何も描かずに戻る（描画実装 `LoupeSkiaPainter` が吸収する。 [例外処理規約.md](../../../.github/例外処理規約.md) §4「破棄済みソースの吸収は描画実装の責務」） |

共通方針（送出・捕捉・翻訳）は [例外処理規約.md](../../../.github/例外処理規約.md) §2・§3 に従います。

## 5. コンポーネント構成

```text
src/EsUtil.Feature.Loupe.ControlMewUI/
├── src/
│   ├── LoupeControl.cs         MewProperty の定義・DPI 追従・描画の起動（描画計画の解決）
│   ├── LoupeSkiaPainter.cs     描画計画（LoupeDrawPlan）を SKCanvas へ写す描画実装
│   └── LoupeRenderedEventArgs.cs  描画完了の通知引数（中心座標ラベルはコントロールが表示する）
└── tests/
    ├── TestHelper.cs                テスト用の描画先（SKBitmap + SKCanvas）・画像生成・setter を経由しない設定
    ├── RecordingLogger.cs           ログ収集
    ├── LoupeRenderedEventArgs.Tests.cs      イベント引数の保持
    └── LoupeControl.Tests.cs        プロパティ補正・レイアウト・全設定経路の補正・ログ・要求サイズ
```

| コンポーネント | 責務 |
| --- | --- |
| `LoupeControl` | 入力プロパティ（`MewProperty`）の定義（補正と変更ログもここへ登録する）、DPI 変更の追従、子（図形キャンバス・ラベル要素）の計測と配置、ラベル要素（`Border` + `TextBlock`）の追加・更新。 描画は提示層（`LoupePresenter.ResolveDrawPlan`）が解決した計画を `LoupeSkiaPainter` で `SKCanvas` へ写す（層の順序も描く値も計画が単一所有する）。 **描画のたびに描画面（物理ピクセル）と割り当て領域（DIP）の比を実効倍率として `ClientToPixelScaling` へ反映する**（DPI の変更通知は追加直後のコントロールへ届かないため、保持値だけに頼るとルーペ領域より小さい拡大画像を描く） |
| `LoupeSkiaPainter`（描画実装） | 描画計画（`LoupeDrawPlan`。 層の順序・矩形・拡大画像の切り出しと描画の矩形）を Skia の描画 API へ写す。 **描く層とその順序は計画が単一所有する**（`LoupeDrawLayer` を先頭から処理し、中心座標ラベルは処理しない）。 色は計画の外観（`LoupeAppearance`）を使う |
| `LoupeRenderTarget`（テスト） | ウィンドウ不要の描画先。 `SKBitmap` と `SKCanvas` を保持し、画素を読み取る |

## 6. クラス図

```mermaid
classDiagram
    class SkiaCanvasView {
        <<Aprillz.MewUI.Skia>>
        +PaintSurface
        +ContinuousAnimation
        +MeasureContent(size) Size
    }
    class LoupeControl {
        +EsBitmap? Source
        +EsPoint? CenterPoint
        +LoupeOptions Options
        +LoupeAppearance Appearance
        +double ClientToPixelScaling
        +LoupeLayout ResolveLayout(parentSize)
        +SetCoordTransform(port) void
        +OnDpiChanged(oldDpi, newDpi) void
        +MeasureContent(size) Size
        +ArrangeContent(bounds) void
        -SyncLabelElements(count) void
        -OnPaintSurface(canvas, info) void
    }
    class ShapeCanvas {
        <<子の図形キャンバス>>
        +PaintSurface
    }
    class LoupeOptions {
        <<EsUtil.Feature.Loupe.Core>>
        +int RequestedSize
        +int ZoomLevel
    }
    class LoupeAppearance {
        <<EsUtil.Feature.Loupe.Presenter>>
        +int BorderThickness
        +bool ShowCrosshair / ShowBorder / ShowCoordLabel
        +ShouldShowCoordLabel(hasCenter) bool
        +Color PasteboardColor
        +Color CrossColor
        +Color BorderColor
        +Color CoordLabelColor
        +Color CoordLabelBackgroundColor
        +double CoordLabelFontSize / int CoordLabelCornerRadius
        +Default LoupeAppearance$
    }
    class LoupePresenter {
        <<EsUtil.Feature.Loupe.Presenter / 提示層>>
        +LoupeAppearance Appearance
        +ResolveDrawPlan(displaySize, measureText) LoupeDrawPlan
    }
    class LoupeSkiaPainter {
        <<EsUtil.Feature.Loupe.ControlMewUI / 描画実装>>
        +Paint(canvas, source, plan) void$
    }
    class LoupeCore.Limits {
        <<EsUtil.Feature.Loupe.Core>>
        +CoerceRequestedSize(v) int$
        +CoerceZoomLevel(v) int$
        +CoerceClientToPixelScaling(v) double$
    }
    class LoupeDrawPlan {
        <<EsUtil.Feature.Loupe.Presenter>>
        +EsRect ClientPixelRect
                +CrosshairPixelRects / BorderPixelRects
        +Layers
    }
    class LoupeGeometry {
        <<EsUtil.Feature.Loupe.Core>>
        +EsRect ClientPixelRect
        +int CaptureSize
        +EsPoint? CaptureTopLeft
        +LoupeMagnifiedImage? MagnifiedImage
        +int ClientPixelSize
    }
    class LoupeLabelPlacement {
        <<EsUtil.Feature.Loupe.Presenter>>
        +EsRect Bounds
        +double PaddingX / PaddingY
        +string Text
        +double FontSize
        +double CornerRadius
    }
    class LoupeCore {
        <<EsUtil.Feature.Loupe.Core / 公開ファサード>>
        +Options / ClientToPixelScaling / CenterPoint / PositionOffset
        +SetCoordTransform(port) void
        +ResolveLayout(parentSize) LoupeLayout
        +ResolveGeometry(displaySize) LoupeGeometry
    }
    class EsPoint {
        <<EsUtil.Platform.Core>>
        +Format(decimals) string$
    }

    SkiaCanvasView <|-- ShapeCanvas
    Panel <|-- LoupeControl
    LoupeControl *-- ShapeCanvas : 子（図形の描画）
    LoupeControl *-- LoupeCore : 操作の唯一の窓口（公開ファサード）
    LoupeControl ..> LoupePresenter : 描画計画の解決を委譲する
    LoupeControl ..> LoupeAppearance : 外観（Appearance プロパティ）
    LoupeControl ..> LoupeSkiaPainter : 描画の写し（SKCanvas）
    LoupePresenter ..> LoupeAppearance : 外観
    LoupePresenter ..> LoupeDrawPlan : 層の順序・矩形・ラベル配置（自ら解決する）
    LoupeSkiaPainter ..> LoupeDrawPlan : 描く値（計画が解決済み）
    Loupe ..> LoupeOptions : 設定の単一値
    LoupeControl ..> LoupeCore.Limits : 値の補正（プロパティ登録の coerce）
    Loupe ..> LoupeGeometry : 表示レイアウト・描画サイズ・幾何の解決
    Loupe ..> EsPoint : ラベル文字列（Format）
```

## 7. シーケンス図（描画）

```mermaid
sequenceDiagram
    participant MW as MewUI
    participant LC as LoupeControl
    participant R as LoupeSkiaPainter（描画実装）
    participant P as LoupePresenter（提示層）
    participant C as EsUtil.Feature.Loupe.Core
    participant SK as SkiaSharp

    MW->>LC: PaintSurface(canvas, info)
    LC->>C: ResolveLayout / ResolveGeometry（表示サイズ・幾何）
    LC->>P: ResolveDrawPlan(displaySize, SkiaText.Measure)（提示層が幾何・外観・層の順序・ラベル配置を解決する）
    LC->>R: LoupeSkiaPainter.Paint(SKCanvas, Source, plan)（層の順に写す）
    R->>SK: 画像外の色（plan.Appearance.PasteboardColor）を表面全体へ塗る
    alt Source と切り出し領域が揃っている
        R->>R: EsBitmap.Crop(plan.MagnifiedImage.CropSourceRect)（描画実装が実行する）
        R->>SK: DrawImage(cropped, Source, Destination)（最近傍で拡大）
    else 未設定または破棄済み
        R-->>R: 画像外の色のみを残す
    end
    R->>SK: 十字線（plan.CrosshairPixelRects）を描画
    R->>SK: 枠線（plan.BorderPixelRects）を描画
    alt plan.Labels が空でない
        R-->>R: 座標ラベル層は描画しない（コントロールがテキスト要素として表示する）
    else 表示しない
        R-->>R: 座標ラベルを表示しない
    end
```

## 8. 設計判断

1. **中間ビットマップを持たない**。 Avalonia 版は `RenderTargetBitmap` に描いて `EsBitmap` へコピーするが、
   MewUI 版は `SkiaCanvasView` のキャンバスへ直接描画する（コピーとメモリ確保を排除）。
2. **拡大は最近傍で行う**。 ルーペは画素をそのまま拡大する用途であり、補間すると画素が判別できなくなる。
3. **装飾矩形の導出は提示層が単一所有する**。 両 UI で同じ見た目を保証するため、
   Avalonia 版と MewUI 版は同じ描画計画を使う（描画実装は計画を写すだけである）。
4. **設定（基本サイズ・拡大率）はコアの `LoupeOptions` が、外観（枠線の太さ・表示可否・色）は提示層の `LoupeAppearance` が単一所有する**。 UI 層は `Options` と `Appearance` を公開して描画時に読むだけであり、既定値・数値項目の補正もそれぞれの型が持つ。
   表示可否と枠線の太さが外観に集約されているため、両 UI の見た目が一致する
   （従来は Avalonia が既定で十字線を描き MewUI は描かないという既定値のドリフトがあった）。
5. **位置を持たない**。 表示位置は「親領域サイズ」「中心座標」「オフセット」に依存するホスト側の関心事であり、
   計算はコアの公開ファサード（`LoupeCore.ResolveLayout`）が単一所有する。 コントロールは入力と描画のみを担う。
6. **DPI を自動追従させる**。 `OnDpiChanged` で `ClientToPixelScaling` を更新し、ホストが DPI を扱わずに済むようにした。
7. **`Options` はレイアウトにも影響する**。 `MeasureContent` が要求サイズを返すため、
プロパティへ `MewPropertyOptions.AffectsLayout` を付与して再測定を促す（未付与では変更が反映されない）。
描画サイズは `Options.RequestedSize` × `ClientToPixelScaling` を物理ピクセルへ換算する
   （DIP → 物理ピクセルの換算はコアのファサード（`LoupeCore.ResolveGeometry`）が単一所有する。 本コントロールは表示サイズをキャンバスの大きさから導くため、
   キャンバスと描画サイズは常に一致し、領域に合わせた縮小は行わない）。
8. **静的画像では連続再描画を無効化する**。 `ContinuousAnimation` を `false` にし、プロパティ変更時のみ再描画する。
9. **座標ラベルはコントロールがテキスト要素として表示する**（両 UI 共通）。 文言は提示層の `LoupePresenter`（`LabelTexts` partial）、表示可否と体裁（フォントサイズ・色・角丸）は外観（`LoupeAppearance`）、実寸 → 配置の写像は同層の `LoupePresenter`（`LabelPlan` partial） が単一所有し、コントロールは自 UI の実寸（`SkiaText.Measure`）を渡して要素を配置する（ラベル要素は子の末尾に置かれ、子は親より後に描かれるため常に最上位）。 Avalonia 版も同じ契約を使い、測定 API（`FormattedText`）だけが異なる。
10. **値の補正と変更ログはプロパティの登録が所有する**。 `MewProperty.Register` の `coerce` / `changed` へ渡すため、
   setter を経由しない設定（バインディング等）でも安全域が保たれ、変更が記録される
   （Avalonia 版の `coerce` コールバック + `Changed` クラスハンドラと同じ契約。 規則と書式はコアが単一所有する）。
設定（`Options`）の数値項目はコアの型が、外観（`Appearance`）の数値項目は提示層の型が生成時に補正するため、コントロールは `coerce` を登録しない（環境値の `ClientToPixelScaling` のみ登録する）。

## 9. テスト方針

| 対象 | 方針 |
| --- | --- |
| `LoupeSkiaPainter`（描画実装） | `SKBitmap` + `SKCanvas`（`TestHelper.LoupeSkiaPainter` 経由）へ実際に描画し、**画素を実測**して検証する（ウィンドウ不要で並行実行できる） |
| `LoupeControl` | `MewProperty` の補正・既定値・要求サイズを検証する（MewUI の要素生成はホスト登録を要しない） |
| コアロジック | `EsUtil.Feature.Loupe.Core.Tests` が担保する（本プロジェクトでは再検証しない） |
| アプリ組み込み | 上位のアプリの実機検証（撮影した画素の実測）で確認する |

## 10. 未実装・今後の対応

| 項目 | 内容 |
| --- | --- |
| 座標変換ポート | 実装済み（`SetCoordTransform`。 ホストが設定し、コントロールはファサードへ転送する。 Avalonia 版と同じ API） |
| 再生成の要求メソッド | `Clear` / `RequestRender` 相当（現在はプロパティ変更で自動再描画） |
| 座標ラベル以外の文字描画 | 日本語のフォントフォールバック（ラベルは別機能の選択矩形コントロール（外側の装飾）側で対応済み。 本コントロールの座標ラベルは数値のみのため未適用） |

## 11. ログ

設定変更を Debug、描画の例外を Error で記録します（Avalonia 版と同一の書式）。

| レベル | 出力元 |
| --- | --- |
| Debug | `Source` / `CenterPoint` / `RequestedSize` / `ClientToPixelScaling` / `ZoomLevel` の変更（補正後の値を渡す。 変化判定（同一値・同一インスタンスなら出力しない）は出力する主体のログ定義（Source は本プロジェクトの `LoupeUiLogMessage`、外観は提示層の `LoupePresenterLogMessage`、他はコアの `LoupeLogMessage`）が単一所有し、UI 層は設定の前後を渡すだけ。 設定の変更は変化した項目だけを出力する） |
| Error | 描画処理の例外（アプリを落とさずログへ記録して描画を中止する） |

Avalonia 版の Trace ログ（再描画パイプライン）は持ちません（MewUI 版は中間ビットマップを作らず同期描画するため）。  
出力先は `Log` プロパティへ注入します（既定は `NullLogger`）。
