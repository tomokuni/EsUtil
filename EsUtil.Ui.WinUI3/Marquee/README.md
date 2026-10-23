# EsUtil.Ui.WinUI3.Marquee

画像やスクリーンの上へ重ねて使う、リサイズ可能なドラッグ矩形（マーキー）の WinUI 3 コントロールです。

## 1. 概要

選択矩形の状態機械・入力の解釈・描画計画の解決は UI 非依存の層（`EsUtil.Feature.Marquee.Core` / `EsUtil.Feature.Marquee.Presenter`）が
単一所有し、本コントロールは **入力の受付・効果の適用・通知の転送・描画** だけを行います。
そのため、他の UI 版（`EsUtil.Ui.AvaloniaUI.Marquee` / `EsUtil.Ui.MewUI.Marquee`）と同じ操作感・同じ計算結果になります。

描画には SkiaSharp の `SkiaSharp.Views.Windows.SKXamlCanvas`（`Canvas`）を基底クラスとして使い、自前の描画面へ直接描きます
（中間ビットマップを作りません）。 サイズ ラベル・カーソル座標ラベルも同じ描画面へ描きます。

## 2. 特徴

- **UI 非依存コアの共有**: 選択・ハンドルのヒットテスト・nudge・確定/キャンセルの判断はコアが単一所有します（本コントロールは判断を持ちません）。
- **描画面へ直接描く**: `SKXamlCanvas` の描画面へ Skia で描きます。 描画の倍率は **描画面の実寸 ÷ 割り当て領域** から実測するため、XAML のルートが未確定の間も図形がずれません。
- **ラベルも同じ描画面へ描く**: サイズラベル・カーソル座標ラベルの実寸測定と描画が同じエンジン（Skia）になるため、背景から文字がはみ出して切れません。
- **入力の内蔵**: 左ドラッグでの新規選択・ハンドルのリサイズ/移動、矢印キーによる nudge（1 対象ピクセル単位の微調整）、Escape/Enter、左ダブルクリックを扱います。
- **ポインター キャプチャ**: ドラッグ中はポインターをキャプチャするため、コントロールの外へ移動しても選択を続けられます。
- **外観と描画計画の 2 つの入口**: 色・太さ・表示可否は `Appearance`、矩形の並びで表せる装飾とラベルの文言・位置は `DrawPlanResolver` で差し替えられます（派生は不要です）。
- **ログの注入**: `Log` へ `ILogger` を設定すると、コントロール内部（コア・提示層）の判断と例外を確認できます（接頭辞の既定は `[Marquee]`）。
- **コア型のイベント引数**: イベントの座標は UI 非依存のコア型（`EsPoint` / `EsRect`）で公開します（境界変換が不要です）。

## 3. システム要件

| 項目 | 要件 |
| --- | --- |
| 対象フレームワーク | `net10.0-windows10.0.22621.0` |
| OS | Windows 10 ビルド 19041 以降（`Microsoft.UI.Input.InputSystemCursor` を使うため） |
| UI フレームワーク | WinUI 3（Windows App SDK 1.x。 パッケージ参照 `Microsoft.WindowsAppSDK`） |
| 描画 | SkiaSharp 3.x（`SkiaSharp.Views.WinUI`） |
| プラットフォーム | x64 / x86 / arm64（アンパッケージで自己完結配置する場合は対象アーキテクチャの明示が必要です） |

## 4. インストール

同じソリューションから使う場合はプロジェクト参照を追加します（型の変更が即座に反映されます）。

```xml
<ProjectReference Include="..\EsUtil.Ui.WinUI3\Marquee\src\EsUtil.Ui.WinUI3.Marquee.csproj" />
```

ローカル フィード（`.nupkg/`）のパッケージとして使う場合は、パッケージ参照と WinUI 3 の設定を追加します。

```xml
<PropertyGroup>
  <TargetFramework>net10.0-windows10.0.22621.0</TargetFramework>
  <UseWinUI>true</UseWinUI>
  <SupportedOSPlatformVersion>10.0.19041.0</SupportedOSPlatformVersion>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="EsUtil.Ui.WinUI3.Marquee" Version="$(EsUtil_Ui_WinUI3_Marquee_Version)" />
  <PackageReference Include="Microsoft.WindowsAppSDK" Version="[1.*,2.0.0)" />
  <PackageReference Include="SkiaSharp.Views.WinUI" Version="[3.*,4.0.0)" />
</ItemGroup>
```

コア（`EsUtil.Feature.Marquee.Core`）と提示層（`EsUtil.Feature.Marquee.Presenter`）は推移参照で解決されるため、明示する必要はありません。

## 5. 使い方（クイックスタート）

XAML へコントロールを置きます（選択の対象の上へ重ねます）。

```xml
<Window
    x:Class="EsUtil.Ui.WinUI3.Marquee.Sample.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:marquee="using:EsUtil.Ui.WinUI3.Marquee">
    <Grid>
        <Image x:Name="Target" Stretch="None" />
        <marquee:MarqueeControl x:Name="Marquee" />
    </Grid>
</Window>
```

コードビハインドで、座標変換ポート・選択の初期値・通知の購読を設定します。

```csharp
using EsUtil.Ui.WinUI3.Marquee;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;

var transform = CoordTransform.ClientToSource(
    sourcePerDip: () => 1.0,                                  // 対象座標 = ローカル DIP × 倍率（ズームはホストが決める）
    sourceArea: () => new EsSize(800, 600));                  // 選択の境界（元画像の寸法）

Marquee.SetCoordTransform(transform);
Marquee.SetSelection(new EsRect(100, 100, 200, 150));         // 対象座標で与える
Marquee.SelectionCompleted += (sender, e) => Apply(e.Rect);   // ドラッグ確定（対象座標）
Marquee.ConfirmRequested += (sender, e) => Apply(e.Rect);     // Enter またはダブルクリックでの確定
```

**座標系**: コントロールへ与える矩形・イベントで受け取る座標はすべて **対象座標**（画像ピクセル・仮想スクリーンピクセル等）です。
表示のズームとスクロールは座標変換ポート（`ICoordTransform`）が担うため、コントロールは倍率を知りません。

## 6. 操作

| 操作 | 動作 |
| --- | --- |
| 左ボタン押下 → ドラッグ | 選択矩形の内側のハンドルに当たればリサイズ/移動、外側なら新規選択 |
| 矢印キー | nudge（1 対象ピクセル単位の微調整。 カーソルを隠して十字マーカーを表示） |
| Escape | キャンセル要求（ドラッグ中または選択あり。 選択はコントロールが破棄してから通知） |
| Enter | 確定要求（選択確定後のみ。 ドラッグ中は無視） |
| 左ダブルクリック | 選択矩形の内側なら確定要求、外側ならキャンセル要求 |
| フォーカス喪失 | nudge を終了し、隠したカーソルを再表示 |

矢印キーと Escape/Enter は **コントロールにフォーカスがある間だけ**届きます（他の UI 版のフォーカス非依存 nudge とは異なります）。

## 7. 主なプロパティ

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Options` | `MarqueeOptions` | `MarqueeOptions.Default` | 計算に必要な設定（最小選択サイズ・ハンドルの寸法・ヒットテスト半径・境界外選択の禁止） |
| `Appearance` | `MarqueeAppearance` | `MarqueeAppearance.Default` | 描画にだけ必要な外観（色・フォントサイズ・枠線の太さ・表示可否） |
| `HighlightSourceRect` | `EsRect?` | `null` | ホストが与えるハイライト矩形（対象座標）。 選択矩形より優先して注目矩形になります |
| `ClientToSourceScaling` | `double` | `1.0` | 座標変換ポートを設定しない場合に使う倍率（対象座標 / ローカル DIP） |
| `ClientToPixelScaling` | `double` | `1.0` | 表示倍率（DIP → 物理ピクセル）。 XAML のルートの変化に追従して更新されます |
| `DisplayScaleScaling` | `double` | 未設定（`0` 以下） | サイズラベルの百分率に使う倍率（マルチモニタ補正） |
| `SizeLabelOverrideProvider` | `Func<string?>?` | `null` | サイズラベルの文言を描画時に与える関数（`null` または空文字なら自動生成） |
| `SizeLabelDisplayScalingProvider` | `Func<EsRect, double>?` | `null` | 矩形ごとの表示倍率を与える関数（モニタごとに倍率が違う場合） |
| `DrawPlanResolver` | `Func<MarqueeDrawPlan, MarqueeDrawPlan>?` | `null` | 描画計画（装飾の矩形列・ラベル配置）を差し替える関数 |
| `Log` | `ILogger` | 出力なし | 操作ログの出力先 |
| `LogPrefix` | `string?` | `null`（既定 `[Marquee]`） | ログの接頭辞（`null`/空白で既定へ戻す） |
| `CurrentSnapshot` | `MarqueeSnapshot` | — | 状態・選択矩形・アクティブ ハンドル・マウス位置・操作フラグ（取得のみ） |
| `HasSelection` | `bool` | `false` | 選択があるかどうか（取得のみ） |
| `SelectionSourceRect` | `EsRect` | `0x0` | 現在の選択矩形（対象座標。 取得のみ） |
| `IsPointerOver` | `bool` | `false` | ポインターがコントロールの上にあるかどうか（取得のみ） |

## 8. 主なメソッド

| メソッド | 説明 |
| --- | --- |
| `SetCoordTransform(ICoordTransform?)` | 対象 ↔ ローカル DIP の座標変換ポートを設定します（`null` は等価変換） |
| `SetSelection(EsRect)` | 選択矩形を外部状態から復元します（境界が有効なら内側へクリップされます） |
| `ResetSelection()` | 選択状態と nudge 状態を初期化します（隠したカーソルを再表示します） |
| `RequestConfirm()` | 確定要求を処理します（Enter キーと同じ条件） |
| `RequestCancel()` | キャンセル要求を処理します（Escape キーと同じ条件） |
| `TryGetCurrentLoupePosition(out EsPoint)` | 追従位置（ルーペ中心。 対象座標）を実カーソルから取り直します |
| `InvalidateRender()` | 描画計画の再解決と再描画を要求します（座標変換ポートの内部状態を変えたときに呼びます） |
| `DrawPlan(SKCanvas, in MarqueeDrawPlan)` | 描画計画を自前のキャンバスへ描きます（既定の描画をホストが再利用する入口） |

## 9. イベント

| イベント | 引数 | 説明 |
| --- | --- | --- |
| `SelectionStarted` | `MarqueePointEventArgs` | ドラッグの始点が決まったとき（新規選択の開始） |
| `SelectionChanged` | `SelectionRectEventArgs` | 選択矩形が変わったとき |
| `SelectionCompleted` | `SelectionRectEventArgs` | ドラッグが終わって選択が確定したとき |
| `SelectionCancelled` | `EventArgs` | 領域がキャンセルされたとき（小さすぎる矩形・枠外ダブルクリック） |
| `LoupePositionMoved` | `MarqueePointEventArgs` | ルーペが表示する中心（対象座標）が動いたとき |
| `ConfirmRequested` | `SelectionRectEventArgs` | 確定要求（Enter または選択矩形の内側のダブルクリック） |
| `CancelRequested` | `EventArgs` | キャンセル要求（Escape または選択矩形の外側のダブルクリック） |
| `Rendered` | `MarqueeRenderedEventArgs` | コントロールが図形とラベルを描き終えたとき（描画中のみ有効なキャンバスと描画計画を渡します） |

`MarqueePointEventArgs` / `SelectionRectEventArgs` は通知時点のスナップショット（`MarqueeSnapshot`）も持ちます。

## 10. カスタマイズ（外観と描画計画）

| 目的 | 入口 |
| --- | --- |
| 色・枠線の太さ・ラベルの体裁・表示可否を変える | `Appearance`（`MarqueeAppearance.Default with { ... }`） |
| 矩形の並びで表せる装飾（破線・ガイド線等）とラベルの文言・位置を差し替える | `DrawPlanResolver`（同じ入力に同じ出力を返す純粋関数） |
| 矩形で表せない描画（円・斜線等）を加える | `Appearance` の `Show*` を `false` にして `Rendered` のキャンバスへ描き、必要なら `DrawPlan` で既定の描画を行います |

ラベルはコントロールが描画面へ描くため、`Rendered` でホストが描いた内容はラベルより前面に置かれます。
見本（外観と描画計画の差し替え）はサンプルの `sample/Presentation/GuideMarqueePlan` が参照実装です。

## 11. 操作ログ

`Log` へ `ILogger` を設定すると、コントロール内部のログ（設定の変更・入力の判断・環境値）と、本コントロールが集約した例外が出力されます。

| 項目 | 内容 |
| --- | --- |
| 接頭辞 | `LogPrefix` で集約（既定 `[Marquee]`。 `null`/空白を設定すると既定へ戻ります） |
| 例外の記録 | 公開ファサードの操作（`SetCoordTransform` 等）で発生した例外を `Error` で記録し、呼び出し側へ再スローします |
| 出力先の保持 | 提示層・コアが単一所有します（本コントロールは委譲するだけです） |

ログ基盤（ファイル出力・レベル制御・保持期間）はホスト アプリケーションが所有します。

## 12. 他の UI 版との違い

| 項目 | 本コントロール（WinUI 3） |
| --- | --- |
| ラベルの描画 | `Border` + `TextBlock` を子要素として置かず、**同じ描画面へ Skia で描きます**（実寸測定と描画が同じエンジンになります） |
| 矢印キーの nudge | **フォーカスが必要**です（コントロールが `IsTabStop` で、フォーカスがある間だけキーが届きます） |
| ダブルクリック | WinUI 3 のポインター イベントはクリック数を報告しないため、**OS の設定値（`GetDoubleClickTime`）と押下位置**から判定します |
| ビューポート連携 | スクロール・ズームの追従はホストが行います（変化を検知して `InvalidateRender()` を呼びます） |
| 描画倍率の追従 | XAML のルートの変化（`XamlRoot.Changed`）で `ClientToPixelScaling` を更新し、描画のたびに描画面の実寸からも実測します |

## 13. 未対応

- **ホイール操作**: 扱わない（ホストが `PointerWheelChanged` を購読する）。
- **`ScrollViewer` に相当する自動連携**: 行わない（ビューポートの変化はホストが検知して `InvalidateRender()` を呼ぶ）。
- **マウスダウン・アップのイベント**: 公開しない（生のポインター入力はホストが自 UI のイベントで観測する）。
- **フォーカス非依存の矢印キー nudge**: 行わない（コントロールにフォーカスがある間だけキーが届く）。

## 14. テスト

```powershell
dotnet test EsUtil.Ui.WinUI3/Marquee/tests/EsUtil.Ui.WinUI3.Marquee.Tests.csproj
```

**検証する範囲**（XAML の初期化を必要としない範囲）:

- 描画計画の Skia への写し（装飾・ハンドル・マーカー・ラベル。 層の順序と表示可否の両方）
- 実寸の測定と描画（ASCII・日本語のフォント フォールバック、色、位置）
- 内部ヘルパー（DIP と物理ピクセルの倍率、コア型と WinUI/Skia 型の相互変換、キーの変換、ダブルクリックの判定）
- ログ定義（接頭辞・レベル・書式）

**検証できない範囲と理由**: `MarqueeControl`（`SKXamlCanvas` 派生）のインスタンス生成・入力・描画は、
WinUI 3 の XAML ランタイム（`Application` と `DispatcherQueue`）を必要とするため、テスト ホストでは生成できません。
そのため、コントロール本体の入力経路はサンプル アプリでの手動確認に委ねています（サンプルの `sample/README.md` に確認項目を並べています）。

## 15. サンプルアプリケーション

`EsUtil.Ui.WinUI3/Marquee/sample/EsUtil.Ui.WinUI3.Marquee.Sample.csproj` が、
背景のテスト パターン・ドラッグ選択・表示倍率・見本（破線とガイド線）・ログ表示を組み合わせたアンパッケージの WinUI 3 アプリです。

```powershell
dotnet run --project EsUtil.Ui.WinUI3/Marquee/sample/EsUtil.Ui.WinUI3.Marquee.Sample.csproj
```

詳細は [`sample/README.md`](sample/README.md) を参照してください。

## 16. ドキュメント

| 文書 | 内容 |
| --- | --- |
| [`docs/SPEC_API.md`](docs/SPEC_API.md) | 公開 API（プロパティ・メソッド・イベント・操作仕様・使用例） |
| [`docs/SPEC_System.md`](docs/SPEC_System.md) | 構造・依存・設計判断（開発者向け） |
| [`sample/README.md`](sample/README.md) | サンプル アプリの実行方法と確認項目 |
| [`../../EsUtil/README.md`](../../EsUtil/README.md) | 層の帰属（Core / Presenter / UI のどこに何を置くか） |

## 17. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
