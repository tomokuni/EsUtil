# EsUtil.Ui.WinForms.Marquee

`MarqueeControl`（リサイズ可能なドラッグ矩形）の **WinForms 版**です。 他の UI 版と同一の UI 非依存コア（`EsUtil.Feature.Marquee.Core`）と描画層（`EsUtil.Feature.Marquee.Presenter`）を使い、
入力の受付・効果の適用・カーソル制御と、GDI+ への描画のみを WinForms で実装しています。 層構成（Core / Presenter / UI）の全体像は [`EsUtil/README.md`](../../EsUtil/README.md) を参照してください。

## 1. 概要

`MarqueeControl`（リサイズ可能なドラッグ矩形）の **WinForms 版**です。 UI 非依存のコア（`EsUtil.Feature.Marquee.Core`）と提示層（`EsUtil.Feature.Marquee.Presenter`）を共有し、**描画と入力の受付だけ**を WinForms で実装します。

対象フレームワークは `net10.0-windows`（`UseWindowsForms`）です。 描画は GDI+（`System.Drawing`）で行います。

## 2. 特徴

- **コア共有**: 選択矩形の幾何計算・状態機械・**入力の解釈（nudge セッション・確定/キャンセル・スナップ・クリック検出・最小サイズ判定）** は `EsUtil.Feature.Marquee.Core` が単一所有します（本コントロールは公開ファサード `MarqueePresenter` だけを参照し、内部の状態機械には触れません）。 他の UI 版と挙動は同じ計算に基づきます。
- **直接描画（GDI+）**: 中間ビットマップを経由せず、`OnPaint` で受け取った `Graphics` へ直接描画します。 描画計画（層の順序・装飾矩形・文言・配置）は提示層（`EsUtil.Feature.Marquee.Presenter`）が単一所有し、描画は自 UI の描画 API で行います。
- **描画面だけで検証できる**: 描画実装は `Graphics` だけを要求するため、ウィンドウを持たない単体テストでも `Bitmap` 上の実寸の画素を検査できます。
- **透明な背景**: 下の画像を透かすため、背景を透明にして親の描画（画像を含む）を自分の背景として描きます。 オーバーレイとして画像の上へ重ねられます。
- **外観の設定（`Appearance`）**: 色・フォントサイズ・枠線の太さ・表示可否は**描画にだけ必要な設定**のためコアには含めず、外観（`EsUtil.Feature.Marquee.Presenter` の `MarqueeAppearance`）が単一所有します。 `Appearance` プロパティで指定し（既定は `MarqueeAppearance.Default`）、部分変更は `MarqueeAppearance.Default with { ... }` で行います。 **描くかどうかは色のアルファではなく外観の明示フラグ（`ShowOverlay` / `ShowOuterBorder` / `ShowInnerFill` / `ShowInnerBorder` / `ShowFullOverlay`）が決めます**。
- **文字は本コントロールが描く**: サイズラベルとカーソル座標ラベルの**文言と基準点は提示層が、色とフォントサイズは外観が、実寸から配置矩形を求める写像も提示層が単一所有**し、本コントロールが描画計画の `Labels` に従って描きます。 実寸の測定は描画と同じ API（`Internal.GraphicsText`）で行います。 操作・情報ラベルはアプリ固有の付加情報のため、ホストが `Rendered` のハンドラーで自前で描きます。
- **DPI 追従**: 描画面の DPI からスケーリング係数を解決し、DIP 座標で描画します（枠線・ハンドルの画面上の大きさを保ちます）。 DPI の通知（`RescaleConstantsForDpi`）は代替値として使います。
- **コア型のイベント引数**: イベントの座標は UI 非依存のコア型（`EsPoint` / `EsRect`）で公開します（境界変換が不要）。
- **見た目のカスタマイズ（2 つの入口）**: 色・太さ・表示可否は `Appearance`、矩形の並びで表せる装飾とラベルの文言・位置は `DrawPlanResolver`（`Func<MarqueeDrawPlan, MarqueeDrawPlan>`）で差し替えます。 矩形で表せない描画（円・斜線等）は `Appearance` の `Show*` を false にして `Rendered` の描画面へ自前で描き、必要なら `DrawPlan` で計画どおりの描画を行います（派生は不要）。

## 3. インストール

```xml
<ProjectReference Include="..\EsUtil.Ui.WinForms\Marquee\src\EsUtil.Ui.WinForms.Marquee.csproj" />
```

ローカル フィード（`.nupkg/`）のパッケージを使う場合は次を参照します。

```xml
<PackageReference Include="EsUtil.Ui.WinForms.Marquee" Version="0.1.0" />
```

コア（`EsUtil.Feature.Marquee.Core`）と提示層（`EsUtil.Feature.Marquee.Presenter`）は推移参照で解決されるため、明示的に参照する必要はありません。

## 4. 使い方

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

## 5. 操作

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

## 6. 主なプロパティ

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `Options` | `MarqueeOptions` | 寸法・対話パラメータ（ハンドルの寸法・十字マーカーの長さ・最小選択サイズ・ヒットテスト半径・境界外選択の禁止）。 既定は `MarqueeOptions.Default`。 |
| `Appearance` | `MarqueeAppearance` | 描画にだけ必要な外観（色・ラベル体裁・表示可否）。 既定は `MarqueeAppearance.Default`。 |
| `HighlightSourceRect` | `EsRect?` | ハイライトする矩形（対象座標）。 指定がある場合は選択矩形より優先して注目矩形になる。 |
| `ClientToSourceScaling` | `double` | 座標変換ポートへ適用する倍率（対象座標 / ローカル DIP）。 ポート未指定時のみ使う。 |
| `DisplayScaleScaling` | `double` | サイズラベルの表示スケーリング係数（マルチ モニタ補正）。 |
| `ClientToPixelScaling` | `double` | 表示 DIP を物理ピクセルへ変換する係数（ハンドル生成時と DPI 変更時に自動更新。 入力の物理ピクセル → DIP の換算と、描画面の DPI を解決できない場合の描画倍率の代替値に使う）。 |
| `SizeLabelOverrideProvider` | `Func<string?>?` | サイズラベルの文言を差し替える関数。 |
| `SizeLabelDisplayScalingProvider` | `Func<EsRect, double>?` | サイズラベルの表示スケーリングを矩形ごとに解決する関数。 |
| `DrawPlanResolver` | `Func<MarqueeDrawPlan, MarqueeDrawPlan>?` | 描画計画を差し替える関数。 |
| `Log` | `ILogger` | 操作ログの出力先（既定は `NullLogger`）。 |
| `LogPrefix` | `string?` | ログの接頭辞（既定は `[Marquee]`）。 |
| `HasSelection` | `bool` | 選択矩形があるかどうか（取得のみ）。 |
| `SelectionSourceRect` | `EsRect` | 現在の選択矩形（対象座標。 取得のみ）。 |
| `CurrentSnapshot` | `MarqueeSnapshot` | 現在の状態のスナップショット（取得のみ）。 |

## 7. 主なメソッド

| メソッド | 説明 |
| --- | --- |
| `SetCoordTransform(ICoordTransform?)` | 対象 ↔ ローカル DIP の座標変換ポートを設定する（null は等価変換）。 |
| `SetSelection(EsRect)` | 選択矩形を外部状態から復元する。 |
| `ResetSelection()` | 選択状態と nudge 状態を初期化する。 |
| `RequestConfirm()` | 確定要求を処理する（Enter キーと同じ条件）。 |
| `RequestCancel()` | キャンセル要求を処理する（Escape キーと同じ条件）。 |
| `TryGetCurrentLoupePosition(out EsPoint)` | 追従位置を実カーソルから取り直す。 取り直した場合は true。 |
| `InvalidateRender()` | 描画計画の再解決と再描画を要求する。 |
| `DrawPlan(Graphics, in MarqueeDrawPlan)` | 描画計画を描画面へ描く（静的。 自前の描画と組み合わせる場合に使う）。 |

## 8. イベント

| イベント | 説明 |
| --- | --- |
| `SelectionStarted` | 新規選択のドラッグ開始（始点の決定）。 |
| `SelectionChanged` | 選択矩形の変更。 |
| `SelectionCompleted` | 選択矩形の確定（ドラッグの終了）。 |
| `SelectionCancelled` | 領域のキャンセル（小さすぎる矩形・枠外ダブルクリック等）。 |
| `LoupePositionMoved` | ルーペが表示する中心座標の移動（通常追従ではポインター位置）。 |
| `ConfirmRequested` | 確定要求（Enter・ダブルクリック・`RequestConfirm`）。 |
| `CancelRequested` | キャンセル要求（Escape・枠外ダブルクリック・`RequestCancel`）。 |
| `Rendered` | 描画完了（図形とラベルを描き終えたこと）。 ホスト固有の付加情報を描く入口。 |

## 9. 他の UI 版との違い

| 項目 | 他の UI 版（Avalonia / MewUI） | WinForms 版 |
| --- | --- | --- |
| 描画 API | Skia（キャンバス） | **GDI+（`Graphics`）** |
| 背景の透かし方 | 描画面を透明で消して下の画像を透かす | **背景を透明（`SupportsTransparentBackColor`）にして親の描画を背景に使う** |
| 文字（ラベル） | テキスト要素として子に置く UI 版がある | **描画計画に従って本コントロールが描く** |
| 入力キーの扱い | フレームワークの既定で矢印キーが届く | **`IsInputKey` で矢印キー・Escape・Enter を入力キーにする** |
| マウス キャプチャ | ウィンドウへ要求する | **`Control.Capture` を設定する** |
| カーソル形状 | フレームワークのカーソル型へ写す | **`Cursors` へ写す** |
| 座標変換ポート | 同じ（`ICoordTransform`） | 同じ（`ICoordTransform`） |

## 10. テスト

```powershell
dotnet test EsUtil.Ui.WinForms/Marquee/tests/EsUtil.Ui.WinForms.Marquee.Tests.csproj
```

検証は**ハンドル（ウィンドウ）を作らない範囲**で行います。 描画は `Bitmap` 上の実寸の画素を検査し、内部型（キー変換・色・矩形塗り・テキスト測定）も併せて検証します。 ハンドルを要求する操作（実際の描画・マウス入力・フォーカス・追従位置の取り直し）は対象外です。

## 11. 操作ログ

```csharp
marquee.Log = loggerFactory.CreateLogger("Marquee");
marquee.LogPrefix = "[MyApp]";
```

本コントロールが記録するのは**委譲した操作の例外**（`Error`）だけです。 設定の変更・入力の判断・描画はコアと提示層が同じロガーへ記録します（接頭辞は `LogPrefix` で全層へ伝搬します）。

## 12. 未対応

- **ホイール操作**: 扱いません（ホストが `MouseWheel` を購読してください）。
- **`ScrollViewer` に相当する自動連携**: 行いません（ビューポートの変化はホストが検知して `InvalidateRender()` を呼んでください）。
- **デザイナーによる設定の保存**: 公開プロパティは実行時にコードで与える値のため、デザイナーのコード生成の対象にしていません。
- **描画のピクセル検査を伴わない見た目の差**: 文字の描画は GDI+ の書式（`StringFormat.GenericTypographic`）に従うため、他の UI 版と字形の送りがわずかに異なります。

## 13. サンプル アプリケーション

[`sample/README.md`](sample/README.md) に起動方法と操作方法を示します。

```powershell
dotnet run --project EsUtil.Ui.WinForms/Marquee/sample/EsUtil.Ui.WinForms.Marquee.Sample.csproj
```

## 14. ドキュメント

- [API リファレンス](docs/SPEC_API.md)
- [システム仕様書](docs/SPEC_System.md)
- [サンプル アプリケーション](sample/README.md)

## 15. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
