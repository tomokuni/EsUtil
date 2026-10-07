# EsUtil.Feature.Marquee.ControlAvaloniaUI サンプルアプリケーション

## 目次 <!-- omit from toc -->

- [1. 概要](#1-概要)
- [2. 実行方法](#2-実行方法)
  - [2.1 バッチファイル（リリースビルド＆起動）](#21-バッチファイルリリースビルド起動)
- [3. 操作方法](#3-操作方法)
- [4. 背景画像のズーム・スクロール](#4-背景画像のズームスクロール)
- [5. 使い方（初めての方向け）](#5-使い方初めての方向け)
- [6. ログ表示について](#6-ログ表示について)
- [7. 検証項目（動作確認チェックリスト）](#7-検証項目動作確認チェックリスト)
- [8. 座標ポートについて](#8-座標ポートについて)
- [9. 構成](#9-構成)
- [10. 拡張例（計画の差し替え）](#10-拡張例計画の差し替え)

---

## 1. 概要

`MarqueeControl/sample/` にある **MarqueeControl 動作確認用の独立したサンプルアプリケーション**です。

- `MarqueeControl`（`EsUtil.Feature.Marquee.AvaloniaUI.MarqueeControl`）を実際に Visual Tree へ配置して動作を確認できます。
- マウスドラッグによる新規選択、選択矩形の移動・リサイズ（8方向ハンドル）を確認できます。
- 矢印キーによる nudge（対象 1Pixel 移動）を確認できます。
- ダブルクリック検出（選択内=確定 / 外=キャンセル）を確認できます。
- 公開イベント（`SelectionChanged` / `SelectionCompleted` / `SelectionCancelled` / `LoupePositionMoved` / `ConfirmRequested` / `CancelRequested`）の購読パターンを示します。
- `SetSelection` / `ResetSelection` / `RequestConfirm` / `RequestCancel` の API 呼び出しをボタンで確認できます。
- `MarqueeControl.Log` に `ILogger` を設定し、コントロール内部の操作ログを選択面の下のログ表示領域で確認できます。
- 左側の設定パネルで、コントロールの外観・動作プロパティ（枠線・ハンドル・判定半径・最小サイズ）をリアルタイムに変更できます。

## 2. 実行方法

```powershell
dotnet run --project src/EsUtil.Feature.Marquee.ControlAvaloniaUI/sample/EsUtil.Feature.Marquee.ControlAvaloniaUI.Sample.csproj
```

リリース構成で実行する場合:

```powershell
dotnet run -c Release --project src/EsUtil.Feature.Marquee.ControlAvaloniaUI/sample/EsUtil.Feature.Marquee.ControlAvaloniaUI.Sample.csproj
```

### 2.1 バッチファイル（リリースビルド＆起動）

`src/EsUtil.Feature.Marquee.ControlAvaloniaUI/sample/RunSample.bat` をダブルクリックすると、リリースビルドを行った後、サンプルアプリケーションを起動します。

```text
RunSample.bat  →  リリースビルド（dotnet build -c Release）→ EsUtil.Feature.Marquee.ControlAvaloniaUI.Sample.exe を起動
```

> 本サンプルは独立したプロジェクトです。
> `EsUtil.Feature.Marquee.ControlAvaloniaUI.csproj` は `sample/**` をコンパイル対象から除外しています。

## 3. 操作方法

| 操作 | 内容 |
| --- | --- |
| 選択面をドラッグ | 新規選択の作成（左ボタン） |
| 選択矩形の枠内をドラッグ | 選択矩形の移動 |
| ハンドルをドラッグ | 8方向リサイズ |
| Ctrl+マウスホイール | 背景画像の拡大・縮小（ズーム） |
| マウスホイール / スクロールバー | 背景画像のスクロール |
| 矢印キー | nudge（対象1Pixel移動。 領域作成前は始点決めとして常に有効、領域作成後は**マウスボタン押下中のみ**有効。 十字マーカーはドラッグ中のハンドル位置に表示） |
| Escape | キャンセル要求（選択破棄はコントロールが実施し `CancelRequested` を発火） |
| Enter | 確定要求（選択確定後に `ConfirmRequested` を発火） |
| ダブルクリック（選択内） | 確定扱いのデモ |
| ダブルクリック（選択外） | キャンセル扱いのデモ |
| 「選択をクリア」ボタン | `ResetSelection()` を呼び出し |
| 「固定矩形」ボタン | `SetSelection()` による外部復元 |
| 「確定要求」ボタン | `RequestConfirm()`（Enter キーと同じ条件）を呼び出し |
| 「キャンセル要求」ボタン | `RequestCancel()`（Escape キーと同じ条件）を呼び出し |
| 「画像外の選択を禁止」チェックボックス | `Options.ClampToSourceBounds` を ON/OFF（ON: 領域選択・移動・nudge を画像境界内にクリップ / OFF: 画像外も選択可能） |
| 「ログクリア」ボタン | ログ表示領域をクリア |
| ログ表示領域 | `MarqueeControl` 内部の操作ログ（MouseDown / KeyDown / Nudge 等）を時分秒ミリ秒付きで表示 |
| 選択面とログ表示の間の分割バー | 上下にドラッグして表示サイズを変更 |

## 4. 背景画像のズーム・スクロール

ドラッグ領域の背景にはグリッド画像（1200×800ピクセル）が敷かれています。

- **Ctrl+マウスホイール**: ズーム倍率 10%〜500%（0.1〜5.0）で拡大・縮小
- **マウスホイール / スクロールバー**: ズーム後の画像をスクロール

ズーム時はカーソル位置を中心に拡大・縮小されます。

`MarqueeControl` の座標ポートはズーム倍率に応じて動的に更新されます。

```csharp
// 座標ポート: 対象 = 画像ピクセル（800x500）、DIP = 表示座標（ズーム倍率で変換）
// ToSource = DIP→対象（_zoom で除算）、ToDip = 対象→DIP（_zoom で乗算）
MarqueeCore.SetCoordTransform(new CoordTransform(
    p => new Point(p.X / _zoom, p.Y / _zoom),
    p => new Point(p.X * _zoom, p.Y * _zoom),
    () => new Size(ImageWidth, ImageHeight)));
```

これにより、ズーム後の表示位置でも選択矩形・ハンドル・nudge が正しい対象座標（画像ピクセル）で動作します。

## 5. 使い方（初めての方向け）

1. **起動**: グリッド画像（1200×800）が選択面に表示されます。
2. **新規選択**: 選択面上を左ボタンでドラッグすると選択矩形が作成され、枠線・8方向ハンドルが描画されます。
3. **移動・リサイズ**: 枠内をドラッグで移動、ハンドルをドラッグでリサイズします。
4. **ズーム・スクロール**: Ctrl+マウスホイールで拡大・縮小、マウスホイール / スクロールバーでスクロールします（ズーム倍率に応じて座標ポートが動的更新）。
5. **nudge**: 矢印キーで選択矩形を対象 1Pixel 単位で移動します（領域作成後はマウスボタン押下中のみ有効）。
6. **確定・キャンセル**: Enter で確定（`ConfirmRequested`）、Escape でキャンセル（`CancelRequested`）です。 ダブルクリックは選択内 = 確定 / 選択外 = キャンセルとして扱われます。
7. **API 呼び出し**: 「選択をクリア」/「固定矩形」/「確定要求」/「キャンセル要求」ボタンで各公開 API の動作を確認できます。
8. **ログ**: 選択面下のログ表示領域に、`MarqueeControl` 内部の操作ログ（`Log` に設定した `ILogger` 経由）が表示されます。

## 6. ログ表示について

`MarqueeControl.Log` にサンプル用の `InMemoryLogger` を設定し、コントロール内部の操作ログを選択面の下の `ListBox` へ表示します。

```csharp
MarqueeCore.Log = new InMemoryLogger(line => Dispatcher.UIThread.Post(() => _vm.AddLog(line)));
```

ログには時分秒ミリ秒（`HH:mm:ss.fff`）が付与されます。

コントロールからのイベント（`SelectionChanged` / `SelectionStarted` / `SelectionCompleted` / `SelectionCancelled` / `LoupePositionMoved` / `ConfirmRequested` / `CancelRequested`）を受信した場合も、コードビハインドでログへ出力します。

実アプリでは `ILogger` を NLog 等へ注入することで、同じログをファイルやコンソールへ出力できます。

## 7. 検証項目（動作確認チェックリスト）

- [ ] ドラッグで選択矩形が作成され、枠線・ハンドルが描画される（コントロール自身の描画）
- [ ] 選択矩形の移動・リサイズが動作する
- [ ] 選択中に矢印キーを押すと矩形が対象1Pixel単位で移動する
- [ ] 選択矩形内のダブルクリックで、選択内 = `ConfirmRequested` / 選択外 = `CancelRequested` が Marquee 自身から発火する
- [ ] 「選択をクリア」で状態が初期化され、`SelectionCancelled` 系の処理が動作する
- [ ] 「固定矩形を設定」で外部から矩形が復元される
- [ ] 「確定要求」ボタンで `ConfirmRequested`、「キャンセル要求」ボタンで `CancelRequested` が発火する
- [ ] ステータステキストに選択矩形の対象座標が表示される

## 8. 座標ポートについて

対象 ↔ DIP の変換はズーム倍率に応じて動的に更新されます（対象 = 画像ピクセル 1200×800）。

```csharp
// 座標ポート: 対象 = 画像ピクセル（1200x800）、DIP = 表示座標（ズーム倍率で変換）
// ToSource = DIP→対象（_zoom で除算 = 画像ピクセルへ）、ToDip = 対象→DIP（_zoom で乗算）
MarqueeCore.SetCoordTransform(new CoordTransform(
    p => new Point(p.X / _zoom, p.Y / _zoom),
    p => new Point(p.X * _zoom, p.Y * _zoom),
    () => new Size(ImageWidth, ImageHeight)));
```

実際のアプリでは、モードに応じて次の変換を設定します。

- **AreaSelect**: 対象 = 仮想スクリーンピクセル（= DIP × scaling）
- **Crop**: 対象 = 元画像ピクセル（ズーム・スクロール・レターボックス考慮）

nudge は対象座標の整数加算のみで計算されるため、ズーム倍率・座標変換が1:1でない場合でも「矢印キー1回 = 対象1Pixel」が保証されます。

## 9. 構成

```text
MarqueeControl/sample/
├── EsUtil.Feature.Marquee.ControlAvaloniaUI.Sample.csproj # サンプルアプリのプロジェクト（net10.0 / Avalonia 12.1）
├── app.manifest                   # Windows DPI 設定（PerMonitorV2）
├── Program.cs                     # エントリポイント
├── App.axaml / App.axaml.cs       # アプリケーション定義（FluentTheme）
├── AvaloniaUI/Logging/
│   └── InMemoryLogger.cs          # MarqueeControl.Log の設定例（メモリ内ログ）
├── Presentation/                  # 拡張例（計画の差し替え）
│   ├── GuideMarqueePlan.cs       # 外観と描画計画（破線枠・三分割ガイド）を与える見本
├── ViewModels/
│   ├── ColorOption.cs             # 色選択肢
│   └── MainWindowViewModel.cs     # 選択状態・ステータス表示（CommunityToolkit.Mvvm の ObservableObject）
└── Views/
    └── MainWindow.axaml(.cs)      # 選択面 + 操作ガイド + ボタン
```

## 10. 拡張例（計画の差し替え）

`Presentation/` に、外観（`Appearance`）と描画計画（`DrawPlanResolver`）で見た目を差し替える見本があります（設定パネル上部の見本）。

- `GuideMarqueePlan` — 外観（色・枠線の太さ・表示可否）と、注目矩形の装飾の矩形（破線枠・三分割ガイド・内側の塗り）を与えます。 計画を差し替える純粋関数（`Resolve(in plan)`）として提供します。

差し替えの入口は次の 3 つです（**派生は不要**。 XAML のコントロールへコードビハインドから設定します）。

| 差し替える対象 | 差し替え方 |
| --- | --- |
| 外観（色・枠線の太さ・表示可否） | `Appearance`（`MarqueeAppearance`）を設定する |
| 装飾（暗転・外枠・内側の塗り）の矩形 / ラベルの文言・位置 | `DrawPlanResolver`（`Func<MarqueeDrawPlan, MarqueeDrawPlan>`）を設定する（計画の値の置き換え） |
| 矩形で表せない描画（円・斜線等） | `Appearance` の `Show*` を false にして `Rendered` の描画先へ自前で描く（必要なら `DrawPlan` で計画どおりの描画も呼べる） |

差し替えは値の置き換えだけで完結するため、UI ごとの描画実装（Avalonia は `MarqueeContextPainter`、MewUI は `MarqueeSkiaPainter`。 いずれも内部）は変更不要です。
