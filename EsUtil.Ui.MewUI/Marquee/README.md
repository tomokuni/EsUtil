# EsUtil.Ui.MewUI.Marquee

`MarqueeControl`（リサイズ可能なドラッグ矩形）の **MewUI 版**です。 Avalonia 版と同一の UI 非依存コア（`EsUtil.Feature.Marquee.Core`）と描画層（`EsUtil.Feature.Marquee.Presenter`）を使い、
入力の受付と効果の適用・カーソル制御・描画ポートの適合のみを MewUI で実装しています。 層構成（Core / Presenter / UI）の全体像は [`EsUtil/README.md`](../../EsUtil/README.md) を参照してください。

## 目次 <!-- omit from toc -->

- [1. 概要](#1-概要)
- [2. 機能一覧](#2-機能一覧)
- [3. システム要件](#3-システム要件)
- [4. インストール](#4-インストール)
- [5. クイックスタート](#5-クイックスタート)
- [6. 座標系](#6-座標系)
- [7. 操作](#7-操作)
- [8. 他の UI 版との違い](#8-他の-ui-版との違い)
- [9. カスタマイズ](#9-カスタマイズ)
- [10. ログ](#10-ログ)
- [11. 未対応](#11-未対応)
- [12. サンプルアプリケーション](#12-サンプルアプリケーション)
- [13. ドキュメント](#13-ドキュメント)
- [14. プロジェクト構成](#14-プロジェクト構成)
- [15. テスト](#15-テスト)
- [16. ライセンス](#16-ライセンス)

---

## 1. 概要

`MarqueeControl`（リサイズ可能なドラッグ矩形）の **MewUI 版**です。 UI 非依存のコア（`EsUtil.Feature.Marquee.Core`）と提示層（`EsUtil.Feature.Marquee.Presenter`）を共有し、**描画と入力の受付だけ**を MewUI で実装します。

---

## 2. 機能一覧

- **コア共有**: 選択矩形の幾何計算・状態機械・**入力の解釈（nudge セッション・確定/キャンセル・スナップ・クリック検出・最小サイズ判定）**は `EsUtil.Feature.Marquee.Core` が単一所有します（UI 層は公開ファサード `MarqueeCore` だけを参照し、内部の状態機械・フレーム解決には触れません）。
  Avalonia 版と MewUI 版の挙動は同じ計算に基づきます。
- **直接描画**: 中間ビットマップを経由せず、`描画の層` のキャンバスへ直接描画します。 描画計画（層の順序・装飾矩形・文言・配置）は提示層（`EsUtil.Feature.Marquee.Presenter`）が単一所有し、描画はフレームワーク標準の描画 API で行います。
- **外観の設定（`Appearance`）**: 色・フォントサイズ・枠線の太さ・表示可否は**描画にだけ必要な設定**のためコアには含めず、外観（`EsUtil.Feature.Marquee.Presenter` の `MarqueeAppearance`）が単一所有します。 `Appearance` プロパティで指定し（既定は `MarqueeAppearance.Default`）、部分変更は `MarqueeAppearance.Default with { ... }` で行います。 **描くかどうかは色のアルファではなく外観の明示フラグ（`ShowOverlay` / `ShowOuterBorder` / `ShowInnerFill` / `ShowInnerBorder` / `ShowFullOverlay`）が決めます**。
- **文字は UI 層が要素として配置する**: サイズラベルとカーソル座標ラベルの**文言と基準点は提示層の `MarqueePresenter`（`LabelTexts` partial） が、色とフォントサイズは外観（`MarqueeAppearance`）が、実寸から配置矩形を求める写像は提示層の `MarqueePresenter`（`LabelPlan` partial） が単一所有**し、UI 層が `Border` + `TextBlock` を子要素として置く（子要素は親の描画より後に描かれるため常に最上位）。 サイズラベルの基準点は注目矩形の左上から**枠線の幅 + 1 DIP 内側**（枠線と背景の重なりを無くす）。 実寸測定は自 UI の API（`MewUITextMeasure.Measure`）で行う。 操作・情報ラベルはアプリ固有の付加情報のため、ホスト（上位のアプリの合成コントロール）が `Rendered` のハンドラで自前で描く（外観はホストのラベル設定が単一所有する）
- **DPI 追従**: `OnDpiChanged` でスケーリング係数を更新し、DIP 座標で描画します（枠線・ハンドルの画面上の大きさを保ちます）。
- **コア型のイベント引数**: イベントの座標は UI 非依存のコア型（`EsPoint` / `EsRect`）で公開します（境界変換が不要）。
- **見た目のカスタマイズ（2 つの入口）**: 色・太さ・表示可否は `Appearance`、矩形の並びで表せる装飾とラベルの文言・位置は `DrawPlanResolver`（`Func<MarqueeDrawPlan, MarqueeDrawPlan>`）で差し替えます。 矩形で表せない描画（円・斜線等）は `Appearance` の `Show*` を false にして `Rendered` のキャンバスへ自前で描き、必要なら `DrawPlan` で計画どおりの描画を行います（派生は不要）

---

## 3. システム要件

| 項目 | 内容 |
| --- | --- |
| 対象フレームワーク | `net10.0-windows10.0.17763.0` |
| UI フレームワーク | MewUI 0.21.1（`MewUIVersion` プロパティで固定） |
| 言語 | C# 14（.NET 10） |

---

## 4. インストール

```xml
<PackageReference Include="Aprillz.MewUI.Core" Version="0.21.1" />
<ProjectReference Include="..\EsUtil.Ui.MewUI\Marquee\src\EsUtil.Ui.MewUI.Marquee.csproj" />
```

MewUI は API が安定化中（0.x）のため、パッケージのバージョンは固定してください（`MewUIVersion` プロパティで一元管理）。

---

## 5. クイックスタート

```csharp
using EsUtil.Ui.MewUI.Marquee;
using EsUtil.Feature.Marquee;                     // MarqueeOptions（コア）と MarqueeAppearance（描画にだけ必要な外観）
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;

// 1. 生成（カーソルサービスは省略すると Windows 既定実装になる）
var marquee = new MarqueeControl
{
    // 寸法・対話パラメータは 1 つの設定値で渡す（同値の再設定では再描画されない）
    Options = MarqueeOptions.Default with { HandleSize = 12.0 },

    // 色と表示可否は描画の設定値（外観）が単一所有する（部分変更は with 式で行う）
    Appearance = MarqueeAppearance.Default with { OuterBorderColor = System.Drawing.Color.White },
};

// 2. 座標変換ポートを設定する（ToSource = ローカル DIP → 対象 / SourceArea = 対象の境界）
marquee.SetCoordTransform(new CoordTransform(dip => dip, source => source, () => new EsSize(1920, 1080)));

// 3. イベントを購読する
marquee.SelectionChanged += (_, e) => Console.WriteLine($"選択: {e.Rect}");
marquee.LoupePositionMoved += (_, e) => UpdateLoupe(e.Point);
marquee.SelectionCompleted += (_, e) => Capture(e.Rect);

// 3.5 ラベル（サイズ・カーソル座標）はコントロールがテキスト要素として配置する。
// ホストは描画完了イベントで付加情報（操作・情報ラベル）を描ける（図形はコントロールが描き終えている）。
marquee.Rendered += (_, e) =>
{
    // 図形（装飾・ハンドル・マーカー）はコントロールが描く。
    // 文字は Content.Layers の順に従い、UI 層が配置したテキスト要素が表示する。
};

// 4. 外部状態から選択を復元する（境界外は自動でクリップされる）
marquee.SetSelection(new EsRect(100, 100, 400, 300));
```

画像の上へ重ねる場合は、親の `Grid` に追加したうえで入力の優先順を管理します（本コントロールが入力を受け取ります）。

```csharp
grid.Add(cropView);     // 画像（下）
grid.Add(marquee);     // 選択オーバーレイ（上。入力を受け取る）
```

---

## 6. 座標系

MarqueeControl は 3 つの座標系を使い分けます。

| 座標系 | 用途 |
| --- | --- |
| 対象座標 | 選択状態・nudge・イベント通知 |
| コントロールローカル DIP | 入力・描画 |
| 画面物理 Pixel | カーソル操作・画面変換 |

対象 ↔ DIP の変換は `SetCoordTransform(ICoordTransform?)` で注入します（ToSource = DIP→対象 / ToDip = 対象→DIP / SourceArea = 対象の境界）。

---

## 7. 操作

| 操作 | 動作 |
| --- | --- |
| 左ドラッグ（矩形外） | 新規選択の開始 |
| 左ドラッグ（ハンドル上） | リサイズ（角・辺の 8 方向） |
| 左ドラッグ（矩形内） | 移動 |
| ダブルクリック（矩形内） | 確定要求（`ConfirmRequested`） |
| ダブルクリック（矩形外） | キャンセル要求（`CancelRequested`） |
| 矢印キー | nudge（1 対象ピクセル単位の微調整。 カーソルを非表示にして十字マーカーを表示） |
| Enter | 確定要求（`ConfirmRequested`） |
| Escape | キャンセル要求（`CancelRequested`） |

矢印キーの nudge は本コントロールにフォーカスがある場合のみ動作します（マウス押下時に自動でフォーカスを取得します）。

---

## 8. 他の UI 版との違い

| 項目 | Avalonia UI 版 | MewUI 版 | WinUI 3 版 | WinForms 版 |
| --- | --- | --- | --- | --- |
| 描画 API | Skia（`DrawingContext`） | Skia（`IGraphicsContext`） | Skia（描画層） | GDI+（`Graphics`） |
| イベント引数の座標 | `Point` / `Rect`（Avalonia の型） | `EsPoint` / `EsRect`（コアの型） | `EsPoint` / `EsRect`（コアの型） | `EsPoint` / `EsRect`（コアの型） |
| マウスダウン・アップのイベント | 公開しない | 同左 | 同左 | 同左 |
| ホイール操作 | 扱わない（ホストが購読する） | 同左 | 同左 | 同左 |
| 矢印キーによる nudge | フォーカスに依存しない（`TopLevel` を購読） | フォーカスが必要 | フォーカスが必要 | フォーカスが必要（`IsInputKey` で入力キーにする） |
| ラベル（文字）の表示 | UI 要素（`Border` + `TextBlock`） | UI 要素（`Border` + `TextBlock`） | 描画面へ描く | 描画面へ描く（GDI+） |
| 背景の透かし方 | 描画面を透明で消して下を透かす | 同左 | 同左 | 背景を透明にして親の描画を背景に使う |
| ビューポート（スクロール・ズーム）への追従 | ホストが変化を検知して `InvalidateRender()` を呼ぶ | 同左 | 同左 | 同左 |

いずれの UI 版も、選択矩形の計算・入力の解釈・描画計画は同じコアと提示層が所有します（差は UI フレームワーク固有の描画 API と入力の受け取り方だけです）。

---

## 9. カスタマイズ

差し替えの入口は 3 つです（**派生は不要**。 コントロールのプロパティへ設定します）。

| 差し替える対象 | 差し替え方 |
| --- | --- |
| 色・枠線の太さ・表示可否 | `Appearance`（`MarqueeAppearance`）を設定する |
| 矩形で並べられる装飾（枠・ハンドル・マーカー・破線）とラベルの文言・位置 | `DrawPlanResolver`（`Func<MarqueeDrawPlan, MarqueeDrawPlan>`）を設定する（計画の値の置き換え） |
| 矩形で表せない描画（円・斜線等） | `Appearance` の `Show*` を false にして `Rendered` で受け取る描画面へ自前で描く（`DrawPlan` を持つ UI 版では計画どおりの描画も呼べる） |

描画計画（`MarqueeDrawPlan`）と外観（`MarqueeAppearance`）は `EsUtil.Feature.Marquee.Presenter` が単一所有し、4 つの UI 版で共通です。 UI が持つのは UI フレームワーク固有の描画 API の呼び出しだけです（仕様は [`docs/SPEC_API.md`](docs/SPEC_API.md)）。

---

## 10. ログ

`Log` プロパティへ `ILogger` を設定すると、操作ログを出力します（既定は出力なし）。 書式・レベルは Avalonia 版と同一です。  
ログはコア（`EsUtil.Feature.Marquee.Core.Logging.MarqueeLogMessage`）と提示層（`EsUtil.Feature.Marquee.Presenter.Logging.MarqueeLogMessage`）が定義し、ロガーはファサード（`MarqueeCore.Log`）が保持します。 本プロジェクトは、公開操作を委譲した際の例外（`EsUtil.Ui.MewUI.Marquee.MarqueeLogMessage.LogOperationError`。 Error）のみを定義します。

| レベル | 出力内容 |
| --- | --- |
| Debug | MouseDown / MouseUp / DoubleClick / DragStart / KeyDown / ConfirmRequested / CancelRequested / 設定変更（コア）/ 外観・表示スケーリングの変更（提示層） |
| Trace | MouseDown 直前の位置 / MouseUp 直後の位置 |
| Error | 公開操作（`SetCoordTransform` / `SetSelection` / `ResetSelection` / `RequestCancel` / `RequestConfirm`）を委譲した際の例外（本プロジェクト） |

定義はコア（`EsUtil.Feature.Marquee.Core.Logging.MarqueeLogMessage`）と提示層（`EsUtil.Feature.Marquee.Presenter.Logging.MarqueeLogMessage`）、本プロジェクト（`EsUtil.Ui.MewUI.Marquee.MarqueeLogMessage`。 公開操作の例外のみ）が持ちます。

---

## 11. 未対応

- `ScrollViewer` の自動購読を行わない（MewUI の入力はポインター下の要素が受け取るため、祖先の `ScrollViewer` を探索しない）。 表示の変化はホストが検知して `InvalidateRender()` を呼ぶ。
- **フォーカス非依存の矢印キー nudge**: 行わない（本コントロールにフォーカスがある間だけキーが届く）。
- **文字（ラベル・座標ラベル）の要素配置**: UI 層の責務である（配置矩形は描画層 `EsUtil.Feature.Marquee.Presenter` の `MarqueePresenter`（`LabelPlan` partial）が求める）。

---

## 12. サンプルアプリケーション

動作確認用の独立サンプルアプリケーションを同梱しています（MewUI は XAML を持たないため、UI は C# のコードで組み立てます）。

```powershell
dotnet run --project EsUtil.Ui.MewUI/Marquee/sample/EsUtil.Ui.MewUI.Marquee.Sample.csproj
```

サンプルで確認できる内容:

- ドラッグによる選択矩形の作成・移動・リサイズと、矢印キーによる nudge（微調整）
- ズーム（1〜4 倍）と座標変換ポート（`SetCoordTransform`）
- 画像内へのクリップ（`Options.ClampToSourceBounds`）
- 外観（枠線・ハンドル・マーカー・暗転マスク）と寸法・対話パラメータの変更
- `SetSelection` / `ResetSelection` / `RequestConfirm` / `RequestCancel`
- 選択・確定・キャンセルのイベントと `Log`（操作ログの出力先 ILogger 設定例）
- 外観と描画計画の差し替え（見本。 `Appearance` と `DrawPlanResolver`）

詳細は [sample/README.md](sample/README.md) を参照してください。

---

## 13. ドキュメント

- [docs/SPEC_API.md](docs/SPEC_API.md) — 公開 API のリファレンス
- [docs/SPEC_System.md](docs/SPEC_System.md) — アーキテクチャ・依存・設計判断
- [sample/README.md](sample/README.md) — サンプルアプリケーション

---

## 14. プロジェクト構成

```text
EsUtil.Ui.MewUI/Marquee/
├── src/                                    # UI 層（MewUI 固有の描画と入力）
│   ├── EsUtil.Ui.MewUI.Marquee.csproj       # 独立プロジェクト（Core + Presenter + Platform.Core を参照）
│   ├── MarqueeControl.cs                    # 公開 API・入力の受付・描画の起動
│   ├── MarqueePainter.cs                    # 描画計画を MewUI の描画面へ写す
│   ├── Internal/                            # 画像の結び付け・塗り・文字の測定
│   ├── MarqueePointEventArgs.cs / SelectionRectEventArgs.cs / MarqueeRenderedEventArgs.cs
│   └── MarqueeLogMessage.cs
├── docs/                                    # SPEC_API.md / SPEC_System.md
├── sample/                                  # サンプルアプリ（sample/README.md 参照）
└── tests/                                   # 単体テスト
```

---

## 15. テスト

```powershell
dotnet test EsUtil.Ui.MewUI/Marquee/tests/EsUtil.Ui.MewUI.Marquee.Tests.csproj
```

- 描画（ハンドル・十字マーカー・外側/内側の装飾）の画素検証は UI 非依存の描画層（`EsUtil.Feature.Marquee.Presenter`）のテストが担うため、本プロジェクトは描画のテストを持たない。
- ラベル要素の配置（実寸 → 配置矩形）の検証は描画層の `MarqueePresenter.LabelPlan.Tests.cs` が担う。 UI 層の要素配置は `MarqueeControl.Tests.cs` が検証する。
- `MarqueeControl.Tests.cs`: 依存プロパティの既定値・補正、`CurrentSnapshot`、選択の復元・境界クリップ、確定・キャンセル、`Log` のフォールバックと設定変更ログ。
- ログの定義（操作ログ・設定変更）はコアの `MarqueeLogMessage` にあり、その検証はコアのテストが行います（設定（外観・表示スケーリング係数）の変更ログは提示層の `MarqueeLogMessage` が所有し、`EsUtil.Feature.Marquee.Presenter.Tests` が検証します）。 本プロジェクトの `MarqueeLogMessage` は公開操作の例外だけを定義します（記録は `MarqueeControl` の委譲の catch が行います）。

入力の解釈（nudge セッション・確定/キャンセル・スナップ・クリック検出・最小サイズ判定）はコアが担うため、その検証は `EsUtil.Feature.Marquee.Core.Tests` が行います。

---

## 16. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
