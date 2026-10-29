# EsUtil.Ui.WinUI3.Marquee サンプルアプリケーション

## 目次 <!-- omit from toc -->

- [1. 概要](#1-概要)
- [2. 実行方法](#2-実行方法)
  - [2.1 前提](#21-前提)
- [3. 操作方法](#3-操作方法)
- [4. 検証項目（動作確認チェックリスト）](#4-検証項目動作確認チェックリスト)
- [5. 構成](#5-構成)
- [6. 使い方（初めての方向け）](#6-使い方初めての方向け)
- [7. 拡張例（計画の差し替え）](#7-拡張例計画の差し替え)
- [8. 他の UI 版サンプルとの違い](#8-他の-ui-版サンプルとの違い)
- [9. ドキュメント](#9-ドキュメント)

## 1. 概要

`EsUtil.Ui.WinUI3.Marquee` の使い方を、**動くアプリ**として確認するためのサンプルです（WinUI 3 / アンパッケージ）。

| 示すこと | 内容 |
| --- | --- |
| 重ね方 | 背景のテスト パターン（別のキャンバス）の上へコントロールを重ねる（コントロールの背景は透明のまま） |
| 枠線・ハンドル・ラベル | 起動直後に選択が 1 つあり、外枠・8 方向のハンドル・サイズ ラベル・カーソル座標ラベルが見える |
| マーカー | 選択の後に矢印キーを押すと nudge（微調整）が始まり、十字マーカーが出る |
| ドラッグ選択 | 左ドラッグで新規選択、ハンドルのドラッグでリサイズ・移動 |
| 確定・キャンセル | Enter / Escape と「確定」「キャンセル」ボタン、選択矩形の内側・外側のダブルクリック |
| 表示倍率 | `CoordTransform.ClientToSource` による倍率の変更（コントロールは倍率を知らない） |
| ログ表示 | コントロールの `Log` へ設定した `ILogger` の出力と、サンプルが受けた通知 |
| 見た目の差し替え | 外観（`Appearance`）と描画計画（`DrawPlanResolver`）だけで破線とガイド線へ差し替える |

## 2. 実行方法

```powershell
dotnet run --project EsUtil.Ui.WinUI3/Marquee/sample/EsUtil.Ui.WinUI3.Marquee.Sample.csproj
```

**アンパッケージ**（MSIX を使わない）で動かします。 プロジェクトは **AnyCPU**（アーキテクチャを固定しない）で、通常のビルドはインストール済みの Windows App SDK ランタイムを使います。

Windows App SDK を同封する（自己完結）ときは `-r` を渡します（`-r` があるときだけ自己完結になります）。

```powershell
dotnet run --project EsUtil.Ui.WinUI3/Marquee/sample/EsUtil.Ui.WinUI3.Marquee.Sample.csproj -r win-x64
```

### 2.1 前提

| 項目 | 要件 |
| --- | --- |
| OS | Windows 10 ビルド 19041 以降 |
| .NET | .NET 10 SDK |
| 実行時の配置 | 通常のビルドはインストール済みの Windows App SDK ランタイムを使う（`-r` を渡したときだけ同封。 MSIX でのインストールは不要） |

## 3. 操作方法

| 操作 | 動作 |
| --- | --- |
| 左ドラッグ（ハンドルの外） | 新しい選択矩形を作る |
| 左ドラッグ（ハンドルの上） | リサイズ・移動（ハンドルは 8 方向） |
| 矢印キー | nudge（1 対象ピクセル単位の微調整。 十字マーカーが出る） |
| Enter / 「確定」ボタン | 確定（`ConfirmRequested` が発火し、選択がクリアされる） |
| Escape / 「キャンセル」ボタン | キャンセル（`CancelRequested` が発火し、選択が消える） |
| 選択矩形の内側をダブルクリック | 確定 |
| 選択矩形の外側をダブルクリック | キャンセル |
| 「クリア」ボタン | 選択を消す（`ResetSelection()`） |
| 「表示倍率」 | 対象座標と表示 DIP の倍率を変える（選択の座標は対象座標のまま） |
| 「見本（破線・ガイド線）」 | 外観と描画計画を差し替える |

**キー入力の前提**: 矢印キー・Enter・Escape はコントロールにフォーカスがある間だけ届きます（ステージを一度クリックしてから押してください）。

## 4. 検証項目（動作確認チェックリスト）

| 項目 | 期待 |
| --- | --- |
| 起動直後 | 枠線・8 個のハンドル・サイズ ラベルが見える（カーソル座標ラベルはポインターを乗せると見える） |
| ドラッグ選択 | 選択の外からドラッグすると選択が作り直され、ログへ `SelectionStarted` / `SelectionChanged` / `SelectionCompleted` が出る |
| 最小サイズ未満 | 小さすぎる矩形はキャンセルされ、ログへ `SelectionCancelled` が出る |
| ハンドルのリサイズ | ハンドルをドラッグすると矩形の辺が動き、カーソル形状が方向に応じて変わる |
| 矢印キー | nudge が始まり十字マーカーが出る。 フォーカスを外すと nudge が終わりカーソルが戻る |
| Enter | `ConfirmRequested` が出て選択が消える |
| Escape | ドラッグ中なら `SelectionCancelled`、選択ありなら `CancelRequested` が出る |
| 表示倍率 | 倍率を変えると選択矩形の表示位置・寸法が追随する（ラベルの数値は対象座標のまま） |
| 見本 | 外枠が破線、内側に三分割のガイド線が出る（チェックを外すと既定に戻る） |
| ログ | コントロール内部のログ（`[Marquee]` の接頭辞）とサンプルの通知が同じ表示に並ぶ |

**自動テストで検証できない理由**: コントロール本体（`SKXamlCanvas` 派生）の生成・入力・描画は、WinUI 3 の XAML ランタイム初期化
（`Application` と `DispatcherQueue`）を必要とするため、テスト ホストでは実行できません。 そのため入力経路（ポインター・キー・カーソル・画面座標）
は本サンプルでの手動確認に委ねています。 自動テストが検証する範囲は [`../README.md`](../README.md) の「テスト」と
[`../docs/SPEC_System.md`](../docs/SPEC_System.md) の「未実装（今後の判断）」を参照してください。

## 5. 構成

```text
sample/
    EsUtil.Ui.WinUI3.Marquee.Sample.csproj
    app.manifest
    App.xaml
    App.xaml.cs
    MainWindow.xaml
    MainWindow.xaml.cs
    Logging/
        InMemoryLogger.cs
    Presentation/
        GuideMarqueePlan.cs
    Views/
        TestPatternCanvas.cs
```

| ファイル | 役割 |
| --- | --- |
| `EsUtil.Ui.WinUI3/Marquee/sample/App.xaml` / `EsUtil.Ui.WinUI3/Marquee/sample/App.xaml.cs` | アプリケーションのエントリ（ウィンドウの生成と表示） |
| `EsUtil.Ui.WinUI3/Marquee/sample/MainWindow.xaml` | 操作の配置、背景・コントロール・ルーペの目印の重ね方 |
| `EsUtil.Ui.WinUI3/Marquee/sample/MainWindow.xaml.cs` | 座標変換ポート・通知の購読・ログ表示・見本の切り替え |
| `EsUtil.Ui.WinUI3/Marquee/sample/Views/TestPatternCanvas.cs` | 背景のテスト パターン（格子・色見本・中心の十字）を描くキャンバス |
| `EsUtil.Ui.WinUI3/Marquee/sample/Presentation/GuideMarqueePlan.cs` | 見本（外観と描画計画の差し替え）の定義 |
| `EsUtil.Ui.WinUI3/Marquee/sample/Logging/InMemoryLogger.cs` | 画面のログ表示へ出力する `ILogger` 実装 |

## 6. 使い方（初めての方向け）

1. サンプルを起動します（起動直後に選択が 1 つ見えます）。
2. ステージ（背景）の中をドラッグして、新しい選択を作ります。
3. 選択のハンドルをドラッグして寸法を変え、内側をドラッグして位置を動かします。
4. ステージをクリックしてから矢印キーを押し、nudge（微調整）で十字マーカーを確認します。
5. Enter で確定、Escape でキャンセルします（ログ表示で通知の順序を確認できます）。
6. 「表示倍率」と「見本」を切り替え、コントロールへ与える設定だけで見た目と座標の扱いが変わることを確認します。

## 7. 拡張例（計画の差し替え）

`EsUtil.Ui.WinUI3/Marquee/sample/Presentation/GuideMarqueePlan.cs` は、
**提示層を派生せずに**見た目を差し替える例です。

```csharp
// 外観（色・枠線の太さ・表示可否）と、矩形の並びで表せる装飾を差し替える
GuideMarqueePlan.Apply(control, show: true);
```

| 差し替えるもの | 入口 |
| --- | --- |
| 色・枠線の太さ・ラベルの体裁・表示可否 | `Appearance` |
| 装飾の矩形列（破線・ガイド線）とラベルの文言・位置 | `DrawPlanResolver` |
| 矩形で表せない描画（円・斜線等） | `Appearance` の `Show*` を止め、`Rendered` のキャンバスへ描く |

## 8. 他の UI 版サンプルとの違い

| 項目 | 本サンプル（WinUI 3） |
| --- | --- |
| 実行形態 | アンパッケージ（MSIX を使わない）。 AnyCPU（自己完結にするときだけ `-r` を渡す） |
| 設定パネル | 表示倍率・見本・選択操作のボタンに絞っている（色の選択は `Appearance` の差し替えで確認する） |
| 背景 | 画像ファイルを読み込まず、`TestPatternCanvas`（`SKXamlCanvas`）が格子・色見本・十字を描く |
| ルーペ | 拡大表示は行わず、`LoupePositionMoved` の位置を円の目印で示す |

## 9. ドキュメント

| 文書 | 内容 |
| --- | --- |
| [`../README.md`](../README.md) | コントロールの概要・使い方・API の要約 |
| [`../docs/SPEC_API.md`](../docs/SPEC_API.md) | 公開 API・操作仕様・使用例 |
| [`../docs/SPEC_System.md`](../docs/SPEC_System.md) | 構造・依存・設計判断 |
