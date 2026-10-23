# EsUtil.Ui.WinForms.Marquee

`MarqueeControl`（リサイズ可能なドラッグ矩形）の **WinForms 版**です。 他の UI 版と同一の UI 非依存コア（`EsUtil.Feature.Marquee.Core`）と描画層（`EsUtil.Feature.Marquee.Presenter`）を使い、
入力の受付・効果の適用・カーソル制御と、GDI+ への描画のみを WinForms で実装しています。 層構成（Core / Presenter / UI）の全体像は [`EsUtil/README.md`](../../EsUtil/README.md) を参照してください。

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

`MarqueeControl`（リサイズ可能なドラッグ矩形）の **WinForms 版**です。 UI 非依存のコア（`EsUtil.Feature.Marquee.Core`）と提示層（`EsUtil.Feature.Marquee.Presenter`）を共有し、**描画と入力の受付だけ**を WinForms で実装します。

対象フレームワークは `net10.0-windows10.0.17763.0`（`UseWindowsForms`）です。 描画は GDI+（`System.Drawing`）で行います。

---

## 2. 機能一覧

- **コア共有**: 選択矩形の幾何計算・状態機械・**入力の解釈（nudge セッション・確定/キャンセル・スナップ・クリック検出・最小サイズ判定）** は `EsUtil.Feature.Marquee.Core` が単一所有します（本コントロールは公開ファサード `MarqueePresenter` だけを参照し、内部の状態機械には触れません）。 他の UI 版と挙動は同じ計算に基づきます。
- **直接描画（GDI+）**: 中間ビットマップを経由せず、`OnPaint` で受け取った `Graphics` へ直接描画します。 描画計画（層の順序・装飾矩形・文言・配置）は提示層（`EsUtil.Feature.Marquee.Presenter`）が単一所有し、描画は自 UI の描画 API で行います。
- **描画面だけで検証できる**: 描画実装は `Graphics` だけを要求するため、ウィンドウを持たない単体テストでも `Bitmap` 上の実寸の画素を検査できます。
- **透明な背景**: 下の画像を透かすため、背景を透明にして親の描画（画像を含む）を自分の背景として描きます。 オーバーレイとして画像の上へ重ねられます。
- **外観の設定（`Appearance`）**: 色・フォントサイズ・枠線の太さ・表示可否は**描画にだけ必要な設定**のためコアには含めず、外観（`EsUtil.Feature.Marquee.Presenter` の `MarqueeAppearance`）が単一所有します。 `Appearance` プロパティで指定し（既定は `MarqueeAppearance.Default`）、部分変更は `MarqueeAppearance.Default with { ... }` で行います。 **描くかどうかは色のアルファではなく外観の明示フラグ（`ShowOverlay` / `ShowOuterBorder` / `ShowInnerFill` / `ShowInnerBorder` / `ShowFullOverlay`）が決めます**。
- **文字は本コントロールが描く**: サイズラベルとカーソル座標ラベルの**文言と基準点は提示層が、色とフォントサイズは外観が、実寸から配置矩形を求める写像も提示層が単一所有**し、本コントロールが描画計画の `Labels` に従って描きます。 実寸の測定は描画と同じ API（`Internal.GraphicsText`）で行います。 操作・情報ラベルはアプリ固有の付加情報のため、ホストが `Rendered` のハンドラーで自前で描きます。
- **DPI 追従**: 描画面の DPI からスケーリング係数を解決し、DIP 座標で描画します（枠線・ハンドルの画面上の大きさを保ちます）。 DPI の通知（`RescaleConstantsForDpi`）は代替値として使います。
- **コア型のイベント引数**: イベントの座標は UI 非依存のコア型（`EsPoint` / `EsRect`）で公開します（境界変換が不要）。
- **見た目のカスタマイズ（2 つの入口）**: 色・太さ・表示可否は `Appearance`、矩形の並びで表せる装飾とラベルの文言・位置は `DrawPlanResolver`（`Func<MarqueeDrawPlan, MarqueeDrawPlan>`）で差し替えます。 矩形で表せない描画（円・斜線等）は `Appearance` の `Show*` を false にして `Rendered` の描画面へ自前で描き、必要なら `DrawPlan` で計画どおりの描画を行います（派生は不要）。

---

## 3. システム要件

| 項目 | 内容 |
| --- | --- |
| 対象フレームワーク | `net10.0-windows10.0.17763.0`（`UseWindowsForms`。 Windows 専用） |
| 描画 API | GDI+（`System.Drawing`） |
| 言語 | C# 14（.NET 10） |

---

## 4. インストール

```xml
<ProjectReference Include="..\EsUtil.Ui.WinForms\Marquee\src\EsUtil.Ui.WinForms.Marquee.csproj" />
```

ローカル フィード（`.nupkg/`）のパッケージを使う場合は次を参照します。

```xml
<PackageReference Include="EsUtil.Ui.WinForms.Marquee" Version="0.1.0" />
```

コア（`EsUtil.Feature.Marquee.Core`）と提示層（`EsUtil.Feature.Marquee.Presenter`）は推移参照で解決されるため、明示的に参照する必要はありません。

---

## 5. クイックスタート

```csharp
using EsUtil.Ui.WinForms.Marquee;
using EsUtil.Feature.Marquee;                     // MarqueeOptions（コア）と MarqueeAppearance（描画にだけ必要な外観）
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;

// 1. 生成（カーソル サービスは省略すると Windows 既定実装になる）
var marquee = new MarqueeControl
{
    Dock = DockStyle.Fill,

    // 寸法・対話パラメータは 1 つの設定値で渡す
    Options = MarqueeOptions.Default with { HandleSize = 12.0 },

    // 色と表示可否は描画の設定値（外観）が単一所有する（部分変更は with 式で行う）
    Appearance = MarqueeAppearance.Default with { OuterBorderColor = System.Drawing.Color.White },
};

// 2. 下地の画像の子として重ねる（透明の背景は親の描画を背景に使う）
pictureBox.Controls.Add(marquee);

// 3. 座標変換ポートを設定する（ToSource = ローカル DIP → 対象 / SourceArea = 対象の境界）
marquee.SetCoordTransform(CoordTransform.ClientToSource(() => 1.0, () => new EsSize(1920, 1080)));

// 4. イベントを購読する
marquee.SelectionChanged += (_, e) => Console.WriteLine($"選択: {e.Rect}");
marquee.SelectionCompleted += (_, e) => Console.WriteLine($"確定: {e.Rect}");
marquee.LoupePositionMoved += (_, e) => Console.WriteLine($"追従位置: {e.Point}");

// 5. 描画完了イベントで付加情報（操作ラベル等）を描く
marquee.Rendered += (_, e) =>
{
    // e.Graphics は DPI の拡大が適用済みのため、DIP 座標で描ける
    e.Graphics.DrawString("ドラッグで範囲を選択", Font, Brushes.White, 12f, 12f);
};

// 6. 外部状態から選択を復元する（境界外は自動でクリップされる）
marquee.SetSelection(new EsRect(100, 100, 400, 300));
```

**重ね方**: 透明のコントロールは**親の描画**だけを自分の背景に使います。 画像を持つコントロール（`PictureBox` 等）の子として追加してください。 兄弟として重ねると画像が隠れます。

```csharp
pictureBox.Controls.Add(marquee);   // 画像の上に重なる（画像は親が描く）
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

矢印キーの nudge は本コントロールにフォーカスがある場合のみ動作します（マウス押下時に自動でフォーカスを取得します）。 矢印キーは入力キー（`IsInputKey` が true を返すキー）として扱うため、既定のフォーカス移動（ダイアログ キー）には使われません。

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

```csharp
marquee.Log = loggerFactory.CreateLogger("Marquee");
marquee.LogPrefix = "[MyApp]";
```

本コントロールが記録するのは**委譲した操作の例外**（`Error`）だけです。 設定の変更・入力の判断・描画はコアと提示層が同じロガーへ記録します（接頭辞は `LogPrefix` で全層へ伝搬します）。

---

## 11. 未対応

- **ホイール操作**: 扱わない（ホストが `MouseWheel` を購読する。 未処理のホイールは WinForms が親へ転送するため、親で購読すれば子の上でも届く）。
- **`ScrollViewer` に相当する自動連携**: 行わない（ビューポートの変化はホストが検知して `InvalidateRender()` を呼ぶ）。
- **マウスダウン・アップ専用のイベント**: 持たない（生のマウス入力は基底クラスのイベントで観測する。 本コントロールは選択の解釈だけを公開する）。
- **フォーカス非依存の矢印キー nudge**: 行わない（本コントロールにフォーカスがある間だけキーが届く）。
- **デザイナーによる設定の保存**: 公開プロパティは実行時にコードで与える値のため、デザイナーのコード生成の対象にしない。
- **他の UI 版との字形の差**: 文字の描画は GDI+ の書式（`StringFormat.GenericTypographic`）に従うため、字形の送りがわずかに異なる。

---

## 12. サンプルアプリケーション

[`sample/README.md`](sample/README.md) に起動方法と操作方法を示します。

```powershell
dotnet run --project EsUtil.Ui.WinForms/Marquee/sample/EsUtil.Ui.WinForms.Marquee.Sample.csproj
```

---

## 13. ドキュメント

- [API リファレンス](docs/SPEC_API.md)
- [システム仕様書](docs/SPEC_System.md)
- [サンプル アプリケーション](sample/README.md)

---

## 14. プロジェクト構成

```text
EsUtil.Ui.WinForms/Marquee/
├── src/                                     # UI 層（GDI+ による描画と入力）
│   ├── EsUtil.Ui.WinForms.Marquee.csproj     # 独立プロジェクト（Core + Presenter + Platform.Core を参照）
│   ├── MarqueeControl.cs                     # 公開 API・入力の受付・描画の起動
│   ├── MarqueeGraphicsPainter.cs             # 描画計画を GDI+ の描画面へ写す
│   ├── MarqueePointEventArgs.cs / SelectionRectEventArgs.cs / MarqueeRenderedEventArgs.cs
│   └── MarqueeLogMessage.cs
├── docs/                                     # SPEC_API.md / SPEC_System.md
├── sample/                                   # サンプルアプリ（sample/README.md 参照）
└── tests/                                    # 単体テスト（GDI+ の画素を検査）
```

---

## 15. テスト

```powershell
dotnet test EsUtil.Ui.WinForms/Marquee/tests/EsUtil.Ui.WinForms.Marquee.Tests.csproj
```

検証は**ハンドル（ウィンドウ）を作らない範囲**で行います。 描画は `Bitmap` 上の実寸の画素を検査し、内部型（キー変換・色・矩形塗り・テキスト測定）も併せて検証します。 ハンドルを要求する操作（実際の描画・マウス入力・フォーカス・追従位置の取り直し）は対象外です。

---

## 16. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
