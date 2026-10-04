# EsUtil.Feature.Marquee.ControlMewUI

`MarqueeControl`（リサイズ可能なドラッグ矩形）の **MewUI 版**です。 Avalonia 版と同一の UI 非依存コア（`EsUtil.Feature.Marquee.Core`）と描画層（`EsUtil.Feature.Marquee.Presenter`）を使い、
入力の受付と効果の適用・カーソル制御・描画ポートの適合のみを MewUI で実装しています。 層構成（Core / Presenter / UI）の全体像は [`src/README.md`](../README.md) を参照してください。

## 1. 特徴

- **コア共有**: 選択矩形の幾何計算・状態機械・**入力の解釈（nudge セッション・確定/キャンセル・スナップ・クリック検出・最小サイズ判定）**は `EsUtil.Feature.Marquee.Core` が単一所有します（UI 層は公開ファサード `MarqueeCore` だけを参照し、内部の状態機械・フレーム解決には触れません）。
  Avalonia 版と MewUI 版の挙動は同じ計算に基づきます。
- **直接描画**: 中間ビットマップを経由せず、`SkiaCanvasView` のキャンバスへ直接描画します。 描画計画（層の順序・装飾矩形・文言・配置）は提示層（`EsUtil.Feature.Marquee.Presenter`）が単一所有し、描画は自 UI の描画 API（Skia）で行います。
- **外観の設定（`Appearance`）**: 色・フォントサイズ・枠線の太さ・表示可否は**描画にだけ必要な設定**のためコアには含めず、外観（`EsUtil.Feature.Marquee.Presenter` の `MarqueeAppearance`）が単一所有します。 `Appearance` プロパティで指定し（既定は `MarqueeAppearance.Default`）、部分変更は `MarqueeAppearance.Default with { ... }` で行います。 **描くかどうかは色のアルファではなく外観の明示フラグ（`ShowOverlay` / `ShowOuterBorder` / `ShowInnerFill` / `ShowInnerBorder` / `ShowFullOverlay`）が決めます**。
- **文字は UI 層が要素として配置する**: サイズラベルとカーソル座標ラベルの**文言と基準点は提示層の `MarqueePresenter`（`LabelTexts` partial） が、色とフォントサイズは外観（`MarqueeAppearance`）が、実寸から配置矩形を求める写像は提示層の `MarqueePresenter`（`LabelPlan` partial） が単一所有**し、UI 層が `Border` + `TextBlock` を子要素として置く（子要素は親の描画より後に描かれるため常に最上位）。 サイズラベルの基準点は注目矩形の左上から**枠線の幅 + 1 DIP 内側**（枠線と背景の重なりを無くす）。 実寸測定は自 UI の API（`SkiaText.Measure`）で行う。 操作・情報ラベルはアプリ固有の付加情報のため、ホスト（`FadeMaskDragRectControl`）が `Rendered` のハンドラで自前で描く（外観は `FadeMaskDragRectOptions.Labels` が単一所有する）
- **DPI 追従**: `OnDpiChanged` でスケーリング係数を更新し、DIP 座標で描画します（枠線・ハンドルの画面上の大きさを保ちます）。
- **コア型のイベント引数**: イベントの座標は UI 非依存のコア型（`EsPoint` / `EsRect`）で公開します（境界変換が不要）。

## 2. インストール

```xml
<PackageReference Include="Aprillz.MewUI.Skia" Version="0.21.1" />
<ProjectReference Include="..\EsUtil.Feature.Marquee.ControlMewUI\src\EsUtil.Feature.Marquee.ControlMewUI.csproj" />
```

MewUI は API が安定化中（0.x）のため、パッケージのバージョンは固定してください（`MewUIVersion` プロパティで一元管理）。

## 3. 使い方

```csharp
using EsUtil.Feature.Marquee.MewUI;
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

## 4. 操作

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

## 5. Avalonia 版との違い

| 項目 | Avalonia 版 | MewUI 版 |
| --- | --- | --- |
| イベント引数の座標 | Avalonia の `Point` / `Rect` | **コア型の `EsPoint` / `EsRect`** |
| マウスダウン・アップのイベント | `MouseDown` / `MouseUp` | **持たない**（生のポインター入力はホストが自 UI のイベントで観測するため） |
| ホイール操作 | **扱わない**（ホストが `PointerWheelChanged` を購読） | **扱わない**（ホストが `MouseWheel` を購読） |
| 表示の変化への追従 | ホストが `RefreshOverlay()` を呼ぶ（`ZoomViewControl.ViewportChanged` 等を購読） | 同左（ホストが変化を検知して呼ぶ） |
| nudge のキー入力 | フォーカス非依存（`TopLevel` を購読） | フォーカス依存（本コントロールにフォーカスがある場合のみ） |
| ログ | `ILogger` を注入して出力 | `Log` プロパティで同様に出力する（書式・レベルは同一） |
| 描画 | 中間ビットマップ経由（自 UI の描画 API） | キャンバスへ直接描画（自 UI の描画 API） |
| 文字（ラベル・座標ラベル）の表示 | UI 層が配置したテキスト要素（`Border` + `TextBlock`） | 同左（図形は `Rendered` の `Content.Layers` の順にコントロールが描く） |

## 6. 主なプロパティ

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Options` | `MarqueeOptions` | `MarqueeOptions.Default` | 選択矩形の**計算に必要な設定**（寸法・対話パラメータ）。 変更すると再描画され、対話パラメータはコアへ伝搬する（**色・表示可否は含まない**） |
| `Appearance` | `MarqueeAppearance` | `MarqueeAppearance.Default` | **外観（色・フォントサイズ・枠線の太さ・表示可否）の設定**（`EsUtil.Feature.Marquee`）。 変更すると次の描画で提示層が新しい外観で描画計画を解決する |
| `ClientToPixelScaling` | `double` | `1.0` | DIP → 物理ピクセルの倍率（DPI 変更で自動更新）。 描画は描画面と表示領域の比を使うため、追加直後で未更新でも画像と同じ倍率になる |

`MarqueeOptions` の項目（既定値はコアが単一所有。 Avalonia 版と同一）:

| 項目 | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `HandleSize` | `double` | `10.0` | ハンドルの一辺サイズ（DIP） |
| `HandleBorderThickness` | `double` | `1.5` | ハンドル枠の線の太さ（DIP。 0.5〜64.0 に補正） |
| `HandleHitRadius` | `double` | `12.0` | ハンドルのヒットテスト半径（DIP。 0.0〜256.0 に補正） |
| `MinSelectionSize` | `double` | `6.0` | 選択矩形の最小サイズ（対象座標） |
| `ClampToSourceBounds` | `bool` | `true` | 対象の境界外の選択を禁止する |
| `MarkerLength` | `double` | `16.0` | 十字マーカーの全長（DIP） |

色とラベルの体裁（フォントサイズ・内側余白）に加え、**表示可否（ハンドル・ラベル・暗転・外枠・内側の塗りと枠線）も外観の単一値 `MarqueeAppearance`（`EsUtil.Feature.Marquee`）が単一所有します**。

`MarqueeAppearance` の項目（既定値は本型が単一所有する）:

| 項目 | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `HandleColor` / `HandleBorderColor` | `System.Drawing.Color` | 白 / `#FFA9A9A9` | ハンドルの塗り色と枠線色（全成分 0 の塗り色は白へ解決される） |
| `MarkerColor` | `System.Drawing.Color` | 白 | 十字マーカーの色 |
| `SizeLabelColor` / `SizeLabelBackgroundColor` | `System.Drawing.Color` | 白 / 黒 60% | サイズラベルの文字色と背景色 |
| `CoordLabelColor` / `CoordLabelBackgroundColor` | `System.Drawing.Color` | 白 / 黒 60% | ラベルの文字色と背景色 |
| `OverlayColor` / `OuterBorderColor` | `System.Drawing.Color` | 黒 50% / `#FFA9A9A9` | 暗転マスクと外枠の色 |
| `InnerFillColor` / `InnerBorderColor` | `System.Drawing.Color` | 透明 / 透明 | 注目矩形の内側の塗り色と枠線色 |
| `ShowHandles` / `ShowSizeLabel` / `ShowCoordLabel` | `bool` | `true` | ハンドル・サイズラベル・カーソル座標ラベルの表示可否 |
| `ShowOverlay` / `ShowOuterBorder` | `bool` | `true` | 暗転マスクと外枠の表示可否 |
| `ShowFullOverlay` / `ShowInnerFill` / `ShowInnerBorder` | `bool` | `false` | 全面暗転・内側の塗り・内側の枠線の表示可否 |

色は UI 非依存の `System.Drawing.Color` で指定します。 設定の一部だけを変える場合は `MarqueeOptions.Default with { ... }` / `MarqueeAppearance.Default with { ... }` で指定します（指定しない項目は既定値を保つ）。

## 7. 主なメソッド

| メソッド | 説明 |
| --- | --- |
| `SetCoordTransform(ICoordTransform?)` | 対象 ↔ ローカル DIP の変換ポートを設定する（null は等価変換） |
| `SetSelection(EsRect)` | 選択矩形を復元する（`Options.ClampToSourceBounds` 有効時は境界内へクリップ） |
| `ResetSelection()` | 選択状態と nudge 状態を初期化する |
| `RequestConfirm()` / `RequestCancel()` | Enter / Escape と同じ条件で確定・キャンセルを要求する |
| `RefreshOverlay()` | 表示（パン・ズーム・表示サイズ）の変化へ追従する（再描画と配置の再評価を要求。 選択の状態は変更しない） |
| `InvalidateRender()` | 描画に関わる変更の唯一の入口（図形の Skia キャンバス・ラベル・配置をまとめて無効化する） |
| `CurrentSnapshot` | 現在の状態のスナップショット（`MarqueeSnapshot`） |

## 8. イベント

`SelectionStarted` / `SelectionChanged` / `SelectionCompleted` / `SelectionCancelled` /
`LoupePositionMoved` /
`ConfirmRequested` / `CancelRequested` / `Rendered`

**生のポインター入力（Mouse 系）の公開イベントは持ちません**（ホストは自 UI の入力イベントを使います。 押下状態は `CurrentSnapshot.IsMouseDown`）。

`Rendered` は描画完了の通知です（描画のたびに発火し、引数の `Content` に装飾・ハンドル・マーカー・ラベルと層の順序が含まれます）。 コントロールの描画中に発火するため、ハンドラ内で描画を完了させます。

## 9. テスト

```powershell
dotnet test src/EsUtil.Feature.Marquee.ControlMewUI/tests/MewUI/EsUtil.Feature.Marquee.ControlMewUI.Tests.csproj
```

- 描画（ハンドル・十字マーカー・外側/内側の装飾）の画素検証は UI 非依存の描画層（`EsUtil.Feature.Marquee.Presenter`）のテストが担うため、本プロジェクトは描画のテストを持たない。
- ラベル要素の配置（実寸 → 配置矩形）の検証は描画層の `MarqueePresenter.LabelPlan.Tests.cs` が担う。 UI 層の要素配置は `MarqueeControl.Tests.cs` が検証する。
- `MarqueeControl.Tests.cs`: 依存プロパティの既定値・補正、`CurrentSnapshot`、選択の復元・境界クリップ、確定・キャンセル、`Log` のフォールバックと設定変更ログ。
- ログの定義（操作ログ・設定変更）はコアの `MarqueeLogMessage` にあり、その検証はコアのテストが行います（設定（外観・表示スケーリング係数）の変更ログは提示層の `MarqueePresenterLogMessage` が所有し、`EsUtil.Feature.Marquee.Presenter.Tests` が検証します）。

入力の解釈（nudge セッション・確定/キャンセル・スナップ・クリック検出・最小サイズ判定）はコアが担うため、その検証は `EsUtil.Feature.Marquee.Core.Tests` が行います。

## 10. 操作ログ

`Log` プロパティへ `ILogger` を設定すると、操作ログを出力します（既定は出力なし）。 書式・レベルは Avalonia 版と同一です。  
ログはコア（`EsUtil.Feature.Marquee.Core.Logging.MarqueeLogMessage`）と提示層（`EsUtil.Feature.Marquee.Presenter.Logging.MarqueePresenterLogMessage`）が定義し、ロガーはファサード（`MarqueeCore.Log`）が保持します。 本プロジェクトはログ定義を持ちません。

| レベル | 出力内容 |
| --- | --- |
| Debug | MouseDown / MouseUp / DoubleClick / DragStart / KeyDown / ConfirmRequested / CancelRequested / 設定変更（コア）/ 外観・表示スケーリングの変更（提示層） |
| Trace | MouseDown 直前の位置 / MouseUp 直後の位置 |

定義はコア（`EsUtil.Feature.Marquee.Core.Logging.MarqueeLogMessage`）と提示層（`EsUtil.Feature.Marquee.Presenter.Logging.MarqueePresenterLogMessage`）が持ち、本プロジェクトは定義を持ちません。

## 11. 未対応

- `ScrollViewer` の自動連携を行わない（ホストが変化を検知して `RefreshOverlay()` を呼ぶ）。
- フォーカス非依存の矢印キー nudge。
  - 文字（ラベル・座標ラベル）の要素配置（UI 層の責務。 配置矩形は描画層 `EsUtil.Feature.Marquee.Presenter` の `MarqueePresenter`（`LabelPlan` partial） が求める）。

## 12. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
