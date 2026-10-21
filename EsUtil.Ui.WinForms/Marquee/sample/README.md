# EsUtil.Ui.WinForms.Marquee サンプルアプリケーション

画像の上へ `MarqueeControl` を重ね、枠線・ハンドル・始点マーカー・ラベルの表示と、ドラッグによる選択・ログ表示を確認するサンプルです。

## 目次 <!-- omit from toc -->

- [1. 概要](#1-概要)
- [2. 実行方法](#2-実行方法)
  - [2.1 ビルドしてから実行する](#21-ビルドしてから実行する)
- [3. 操作方法](#3-操作方法)
- [4. 検証項目（動作確認チェックリスト）](#4-検証項目動作確認チェックリスト)
- [5. 構成](#5-構成)
- [6. カスタマイズ例](#6-カスタマイズ例)
- [7. 他の UI 版のサンプルとの違い](#7-他の-ui-版のサンプルとの違い)

## 1. 概要

サンプルは次のことを確認するために作られています。

- 画像の上へ選択矩形コントロールを重ねる（透明な背景で下の画像が透ける）。
- 左ドラッグで新規選択、ハンドルでリサイズ、矩形内のドラッグで移動ができる。
- サイズラベルとカーソル座標ラベルが表示され、十字マーカー（矢印キーの微調整中）が見える。
- 確定要求・キャンセル要求・追従位置の通知がログ欄へ流れる。
- ライブラリが出力する操作ログ（接頭辞付き）がログ欄へ流れる。
- `Rendered` イベントでホスト固有の付加情報（選択サイズと操作ガイド）を描ける。

## 2. 実行方法

```powershell
dotnet run --project EsUtil.Ui.WinForms/Marquee/sample/EsUtil.Ui.WinForms.Marquee.Sample.csproj
```

### 2.1 ビルドしてから実行する

```powershell
dotnet build EsUtil.Ui.WinForms/Marquee/sample/EsUtil.Ui.WinForms.Marquee.Sample.csproj
EsUtil.Ui.WinForms/Marquee/sample/bin/Debug/net10.0-windows10.0.17763.0/EsUtil.Ui.WinForms.Marquee.Sample.exe
```

DPI は Per-Monitor V2 で有効化しています（`Program.cs`）。 モニター間でウィンドウを移動すると、枠線・ハンドル・マーカーの画面上の大きさが保たれることを確認できます。

## 3. 操作方法

| 操作 | 動作 |
| --- | --- |
| 左ドラッグ（矩形の外） | 新規選択の開始 |
| 左ドラッグ（ハンドルの上） | リサイズ（角・辺の 8 方向） |
| 左ドラッグ（矩形の中） | 移動 |
| ダブルクリック（矩形の中） | 確定要求（`ConfirmRequested`） |
| ダブルクリック（矩形の外） | キャンセル要求（`CancelRequested`） |
| 矢印キー | 微調整（nudge。 1 対象ピクセル単位。 十字マーカーが出る） |
| Enter | 確定要求 |
| Escape | キャンセル要求 |
| 「選択をクリア」ボタン | 選択状態の初期化（`ResetSelection`） |
| 「ハイライト切替」ボタン | 固定の矩形をハイライトする（`HighlightSourceRect`）／解除する |
| 「ログを消去」ボタン | ログ欄の消去 |

矢印キーの微調整は、コントロールにフォーカスがある場合のみ動作します（マウス押下時に自動でフォーカスを取得します）。

## 4. 検証項目（動作確認チェックリスト）

- 画像の上へ選択矩形が描かれ、**暗転マスクの外側が暗くならない**（注目矩形の中だけが明るいまま残る）。
- 枠線・ハンドル・十字マーカーの**画面の大きさが拡大率によらず一定**である（DPI の反映）。
- サイズラベルの値が選択矩形の実寸と一致し、`Escape` または「選択をクリア」で消える。
- 選択矩形の上でドラッグすると**移動**、ハンドルの上では**リサイズ**になる（カーソル形状も変わる）。
- 矢印キーで 1 対象ピクセルずつ動き、**カーソルが非表示**になる（フォーカスを失うと再表示される）。
- ログ欄に `SelectionStarted` / `SelectionChanged` / `SelectionCompleted` / `ConfirmRequested` / `CancelRequested` が流れる。
- ライブラリのログが接頭辞 `[Marquee]` 付きで流れる（「ハイライト切替」で設定変更が出力される）。
- ウィンドウを別のモニターへ移しても、枠線とハンドルの大きさと位置がずれない。

## 5. 構成

```text
sample/
├── Imaging/
└── Logging/
```

| ファイル | 内容 |
| --- | --- |
| `EsUtil.Ui.WinForms/Marquee/sample/Program.cs` | エントリポイント（DPI の有効化とアプリケーションの起動）。 |
| `EsUtil.Ui.WinForms/Marquee/sample/MainForm.cs` | 画面の組み立て（画像の上へ重ねる・イベントの購読・ログ表示・付加情報の描画）。 |
| `EsUtil.Ui.WinForms/Marquee/sample/Imaging/TestImageFactory.cs` | 下地の検証用画像の生成（格子と同心円）。 |
| `EsUtil.Ui.WinForms/Marquee/sample/Logging/InMemoryLogger.cs` | ライブラリが出力したログの受け皿（表示用に 1 件ずつ取り出す）。 |

**重ね方**: 透明なコントロールは親の描画を自分の背景に使うため、下地の画像を表示する `PictureBox` の子としてコントロールを追加しています。

```csharp
pictureBox.Controls.Add(marquee);   // 画像（親）の上へ重なる
```

## 6. カスタマイズ例

外観（色・表示可否）は `Appearance` で与えます。

```csharp
marquee.Appearance = MarqueeAppearance.Default with
{
    ShowCoordLabel = false,                        // カーソル座標ラベルを消す
    OuterBorderColor = Color.FromArgb(255, 255, 214, 96),
};
```

寸法と対話パラメータは `Options` で与えます。

```csharp
marquee.Options = MarqueeOptions.Default with
{
    HandleSize = 16.0,
    MinSelectionSize = 12.0,
};
```

描画計画を差し替えると、ラベルの文言と位置を変えられます。

```csharp
marquee.DrawPlanResolver = plan => plan with
{
    Labels = [.. plan.Labels.Select(label => label with { Text = $"[{label.Text}]" })],
};
```

ホスト固有の付加情報は `Rendered` の描画面へ描きます（描画面には DPI の拡大が適用済みのため、DIP 座標で描けます）。

## 7. 他の UI 版のサンプルとの違い

| 項目 | 他の UI 版（Avalonia / MewUI） | 本サンプル（WinForms） |
| --- | --- | --- |
| 起動 | 各フレームワークの初期化を呼ぶ | **`Application.SetHighDpiMode` / `EnableVisualStyles` / `Run` を呼ぶ** |
| 画像の重ね方 | グリッドへ画像とコントロールを順に追加する | **画像を表示する `PictureBox` の子としてコントロールを追加する** |
| ログの表示 | フレームワークのリスト要素へ流す | **`ListBox` へタイマーで流す（ログは他のスレッドからも出力されるため）** |
| 文字（ラベル） | テキスト要素として子に置かれる | **コントロールが描画面へ描く** |
