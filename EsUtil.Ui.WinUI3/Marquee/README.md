# EsUtil.Ui.WinUI3.Marquee

画像やスクリーンの上へ重ねて使う、リサイズ可能なドラッグ矩形（マーキー）の WinUI 3 コントロールです。

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

選択矩形の状態機械・入力の解釈・描画計画の解決は UI 非依存の層（`EsUtil.Feature.Marquee.Core` / `EsUtil.Feature.Marquee.Presenter`）が
単一所有し、本コントロールは **入力の受付・効果の適用・通知の転送・描画** だけを行います。
そのため、他の UI 版（`EsUtil.Ui.AvaloniaUI.Marquee` / `EsUtil.Ui.MewUI.Marquee`）と同じ操作感・同じ計算結果になります。

描画には SkiaSharp の `SkiaSharp.Views.Windows.SKXamlCanvas`（`Canvas`）を基底クラスとして使い、自前の描画面へ直接描きます
（中間ビットマップを作りません）。 サイズ ラベル・カーソル座標ラベルも同じ描画面へ描きます。

---

## 2. 機能一覧

- **UI 非依存コアの共有**: 選択・ハンドルのヒットテスト・nudge・確定/キャンセルの判断はコアが単一所有します（本コントロールは判断を持ちません）。
- **描画面へ直接描く**: `SKXamlCanvas` の描画面へ Skia で描きます。 描画の倍率は **描画面の実寸 ÷ 割り当て領域** から実測するため、XAML のルートが未確定の間も図形がずれません。
- **ラベルも同じ描画面へ描く**: サイズラベル・カーソル座標ラベルの実寸測定と描画が同じエンジン（Skia）になるため、背景から文字がはみ出して切れません。
- **入力の内蔵**: 左ドラッグでの新規選択・ハンドルのリサイズ/移動、矢印キーによる nudge（1 対象ピクセル単位の微調整）、Escape/Enter、左ダブルクリックを扱います。
- **ポインター キャプチャ**: ドラッグ中はポインターをキャプチャするため、コントロールの外へ移動しても選択を続けられます。
- **外観と描画計画の 2 つの入口**: 色・太さ・表示可否は `Appearance`、矩形の並びで表せる装飾とラベルの文言・位置は `DrawPlanResolver` で差し替えられます（派生は不要です）。
- **ログの注入**: `Log` へ `ILogger` を設定すると、コントロール内部（コア・提示層）の判断と例外を確認できます（接頭辞の既定は `[Marquee]`）。
- **コア型のイベント引数**: イベントの座標は UI 非依存のコア型（`EsPoint` / `EsRect`）で公開します（境界変換が不要です）。

---

## 3. システム要件

| 項目 | 要件 |
| --- | --- |
| 対象フレームワーク | `net10.0-windows10.0.22621.0` |
| OS | Windows 10 ビルド 19041 以降（`Microsoft.UI.Input.InputSystemCursor` を使うため） |
| UI フレームワーク | WinUI 3（Windows App SDK 1.x。 パッケージ参照 `Microsoft.WindowsAppSDK`） |
| 描画 | WinUI 3 の要素（`Microsoft.UI.Xaml.Shapes.Rectangle`）と Composition の描画面（SkiaSharp を使わない） |
| プラットフォーム | x64 / x86 / arm64（アンパッケージで自己完結配置する場合は対象アーキテクチャの明示が必要です） |
| Native AOT | 対応（AOT で発行したサンプルが起動することを実測）。 ただし AOT では managed コレクションを `ItemsSource` へ渡せない（`Items.Add` を使う） |

---

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

---

## 5. クイックスタート

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
| 左ボタン押下 → ドラッグ | 選択矩形の内側のハンドルに当たればリサイズ/移動、外側なら新規選択 |
| 矢印キー | nudge（1 対象ピクセル単位の微調整。 カーソルを隠して十字マーカーを表示） |
| Escape | キャンセル要求（ドラッグ中または選択あり。 選択はコントロールが破棄してから通知） |
| Enter | 確定要求（選択確定後のみ。 ドラッグ中は無視） |
| 左ダブルクリック | 選択矩形の内側なら確定要求、外側ならキャンセル要求 |
| フォーカス喪失 | nudge を終了し、隠したカーソルを再表示 |

矢印キーと Escape/Enter は **コントロールにフォーカスがある間だけ**届きます（他の UI 版のフォーカス非依存 nudge とは異なります）。

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

| 目的 | 入口 |
| --- | --- |
| 色・枠線の太さ・ラベルの体裁・表示可否を変える | `Appearance`（`MarqueeAppearance.Default with { ... }`） |
| 矩形の並びで表せる装飾（破線・ガイド線等）とラベルの文言・位置を差し替える | `DrawPlanResolver`（同じ入力に同じ出力を返す純粋関数） |
| 矩形で表せない描画（円・斜線等）を加える | `Appearance` の `Show*` を `false` にして `Rendered` の `AddRect` で加える |

ラベルはコントロールが描画面へ描くため、`Rendered` でホストが描いた内容はラベルより前面に置かれます。
見本（外観と描画計画の差し替え）はサンプルの `sample/Presentation/GuideMarqueePlan` が参照実装です。

---

## 10. ログ

`Log` へ `ILogger` を設定すると、コントロール内部のログ（設定の変更・入力の判断・環境値）と、本コントロールが集約した例外が出力されます。

| 項目 | 内容 |
| --- | --- |
| 接頭辞 | `LogPrefix` で集約（既定 `[Marquee]`。 `null`/空白を設定すると既定へ戻ります） |
| 例外の記録 | 公開ファサードの操作（`SetCoordTransform` 等）で発生した例外を `Error` で記録し、呼び出し側へ再スローします |
| 出力先の保持 | 提示層・コアが単一所有します（本コントロールは委譲するだけです） |

ログ基盤（ファイル出力・レベル制御・保持期間）はホスト アプリケーションが所有します。

---

## 11. 未対応

- **ホイール操作**: 扱わない（ホストが `PointerWheelChanged` を購読する）。
- **`ScrollViewer` に相当する自動連携**: 行わない（ビューポートの変化はホストが検知して `InvalidateRender()` を呼ぶ）。
- **マウスダウン・アップ専用のイベント**: 持たない（生のポインター入力は基底クラスのイベントで観測する。 本コントロールは選択の解釈だけを公開する）。
- **フォーカス非依存の矢印キー nudge**: 行わない（コントロールにフォーカスがある間だけキーが届く）。

---

## 12. サンプルアプリケーション

`EsUtil.Ui.WinUI3/Marquee/sample/EsUtil.Ui.WinUI3.Marquee.Sample.csproj` が、
背景のテスト パターン・ドラッグ選択・表示倍率・見本（破線とガイド線）・ログ表示を組み合わせたアンパッケージの WinUI 3 アプリです。

```powershell
dotnet run --project EsUtil.Ui.WinUI3/Marquee/sample/EsUtil.Ui.WinUI3.Marquee.Sample.csproj
```

詳細は [`sample/README.md`](sample/README.md) を参照してください。

---

## 13. ドキュメント

| 文書 | 内容 |
| --- | --- |
| [`docs/SPEC_API.md`](docs/SPEC_API.md) | 公開 API（プロパティ・メソッド・イベント・操作仕様・使用例） |
| [`docs/SPEC_System.md`](docs/SPEC_System.md) | 構造・依存・設計判断（開発者向け） |
| [`sample/README.md`](sample/README.md) | サンプル アプリの実行方法と確認項目 |
| [`../../EsUtil/README.md`](../../EsUtil/README.md) | 層の帰属（Core / Presenter / UI のどこに何を置くか） |

---

## 14. プロジェクト構成

```text
EsUtil.Ui.WinUI3/Marquee/
├── src/                                     # UI 層（WinUI 3 固有の描画と入力）
│   ├── EsUtil.Ui.WinUI3.Marquee.csproj       # 独立プロジェクト（Core + Presenter + Platform.Core を参照）
│   ├── MarqueeControl.cs                     # 公開 API とイベント
│   ├── MarqueeControl.Input.cs               # ポインター・キーボード入力の解釈
│   ├── MarqueeControl.Visual.cs              # 描画計画の解決と描画層への反映
│   ├── Internal/                             # 描画層・ダブルクリック判定・Win32 照会
│   ├── MarqueePointEventArgs.cs / SelectionRectEventArgs.cs / MarqueeRenderedEventArgs.cs
│   └── MarqueeLogMessage.cs
├── docs/                                     # SPEC_API.md / SPEC_System.md
├── sample/                                   # サンプルアプリ（sample/README.md 参照）
└── tests/                                    # 単体テスト
```

---

## 15. テスト

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

---

## 16. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
