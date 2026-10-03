# EsUtil.Feature.Loupe.ControlAvaloniaUI サンプルアプリケーション

## 目次

- [EsUtil.Feature.Loupe.ControlAvaloniaUI サンプルアプリケーション](#esutilfeatureloupecontrolavaloniaui-サンプルアプリケーション)
  - [目次](#目次)
  - [1. 概要](#1-概要)
  - [2. 実行方法](#2-実行方法)
  - [3. 操作方法](#3-操作方法)
  - [4. 検証項目（動作確認チェックリスト）](#4-検証項目動作確認チェックリスト)
  - [5. 構成](#5-構成)
  - [6. 使い方（初めての方向け）](#6-使い方初めての方向け)
  - [7. 拡張例（派生 Presenter）](#7-拡張例派生-presenter)

---

## 1. 概要

`EsUtil.Feature.Loupe.ControlAvaloniaUI/sample/` にある **LoupeControl 動作確認用の独立したサンプルアプリケーション**です。

- `LoupeControl`（`EsUtil.Feature.Loupe.AvaloniaUI`）を実際に配置して動作を確認できます。
- テスト画像（グラデーション＋グリッド＋マーカー）を生成し、マウス追従で拡大表示します。
- 外観設定（拡大率・サイズ・十字線・画像外の色・枠線・描画スケール）をスライダー/コンボボックスで変更できます。
- `LoupeControl` をVisual Treeへ配置し、内部の `Image` 要素で表示します。
- `Clear()` / `RequestRender()` の API 呼び出しをボタンで確認できます。
- `LoupeControl.Log` に `ILogger`（デバッグ出力）を設定し、描画エラー通知を確認できます。

## 2. 実行方法

```powershell
dotnet run --project sample/EsUtil.Feature.Loupe.ControlAvaloniaUI.Sample.csproj
```

リリース構成で実行する場合:

```powershell
dotnet run -c Release --project sample/EsUtil.Feature.Loupe.ControlAvaloniaUI.Sample.csproj
```

### バッチファイル（リリースビルド＆起動）

`sample/RunSample.bat` をダブルクリックすると、リリースビルドを行った後、サンプルアプリケーションを起動します。

```text
RunSample.bat  →  リリースビルド（dotnet build -c Release）→ EsUtil.Feature.Loupe.ControlAvaloniaUI.Sample.exe を起動
```

> 本サンプルは独立したプロジェクトです。
> `EsUtil.Feature.Loupe.ControlAvaloniaUI.csproj` は `sample/**` をコンパイル対象から除外しています。

## 3. 操作方法

| 操作 | 内容 |
| --- | --- |
| テスト画像上をマウス移動 | ルーペがカーソルの右下に追従して拡大表示 |
| 画像の外へマウス移動 | 画像外の領域のみ無地の色（画像外の色。 視野全体が外に出た場合は全面がその色） |
| 「ルーペを表示」チェック | `IsVisible` の切り替え（非表示時は Bitmap 解放） |
| 拡大率のスライダー | `Options.ZoomLevel`（拡大倍率） |
| ルーペサイズのスライダー | `Options.RequestedSize`（基本サイズ。 そのまま表示サイズになる） |
| 色コンボボックス | `Appearance`（`CrossColor` / `PasteboardColor` / `BorderColor`。 「なし」で描画なし） |
| 枠線の太さ | `Appearance.BorderThickness` |
| 描画スケール | `ClientToPixelScaling`（DIP→物理ピクセル変換の確認） |
| 選択矩形オーバーレイ | `SelectionRectVisible` チェック・`SelectionSourceRect`（X / Y / W / H）の編集（対象座標 = 元画像ピクセル）。 テスト画像上に破線の枠＋座標ラベルが表示され、ルーペ内にも破線が描画される |
| Clear() ボタン | `Clear()` を呼び出し、非表示＋Bitmap 解放 |
| RequestRender() ボタン | `RequestRender()` を呼び出し、プロパティ変更を伴わない再生成を要求 |

## 4. 検証項目（動作確認チェックリスト）

- [ ] マウス追従で拡大画像が表示される（グラデーション・円マーカー・グリッド）
- [ ] 10px グリッドがズーム倍率どおりに拡大される（倍率の検証）
- [ ] ズーム・サイズ変更が即時反映される
- [ ] 十字線・画像外の色・枠線の色が反映される（不透明度は色のアルファに統合）
- [ ] 「なし」を選ぶと該当要素が描画されなくなる
- [ ] `ClientToPixelScaling` を 0.5〜2.0 に変更すると Bitmap の物理ピクセルサイズが変化する
- [ ] `Options.RequestedSize` を変更すると表示サイズが変化する（基本サイズがそのまま表示サイズになる）
- [ ] `SelectionRectVisible` を ON にすると、テスト画像上に選択矩形の破線枠＋座標ラベルが表示され、場所が一目でわかる
- [ ] `SelectionRectVisible` を ON にすると、`SelectionSourceRect`（対象座標）の枠線がルーペ画像上にも破線で描画される（ズーム 2 倍以上）
- [ ] `SelectionSourceRect` の X / Y / W / H を変更すると、テスト画像上の枠とルーペ内の破線の位置・サイズが変化する
- [ ] `SelectionRectColorA` / `SelectionRectColorB`（「なし」で非表示・不透明度は色のアルファ）が破線に反映される
- [ ] チェック OFF / `Clear()` で Bitmap が解放され、チェック ON で再表示される
- [ ] `RequestRender()` で再生成が要求され、`Rendered` が発火する
- [ ] ルーペの左上に中心座標ラベル（`(x, y)`）が表示され、マウス移動で文言が追従する（コントロールがテキスト要素として表示する）
- [ ] 画像の外では画像外の色（無地）が表示される（画像の透明部分と区別できる）

## 5. 構成

```text
EsUtil.Feature.Loupe.ControlAvaloniaUI/sample/
├── EsUtil.Feature.Loupe.ControlAvaloniaUI.Sample.csproj   # サンプルアプリのプロジェクト（net10.0 / Avalonia 12.1）
├── app.manifest                 # Windows DPI 設定（PerMonitorV2）
├── Program.cs                   # エントリポイント
├── App.axaml / App.axaml.cs     # アプリケーション定義（FluentTheme）
├── AvaloniaUI/Logging/
│   └── DebugLogger.cs           # LoupeControl.Log の設定例（デバッグ出力へ転送）
├── Imaging/
│   └── TestImageFactory.cs      # テスト画像生成（グラデーション＋グリッド＋マーカー）
├── Presentation/                # 拡張例（提示層の派生）
│   ├── GridLoupePresenter.cs    # LoupePresenter を派生し、格子線と四隅の鉤へ装飾を差し替える
│   └── GridLoupePreview.cs      # 派生提示層の描画計画を LoupeContextPainter で描く見本
├── ViewModels/
│   ├── BrushOption.cs           # 色選択肢
│   └── MainWindowViewModel.cs   # ルーペ設定・中心座標
└── Views/
    ├── MainWindow.axaml(.cs)    # メイン画面（設定パネル＋テスト画像＋ルーペ）
```

## 6. 使い方（初めての方向け）

1. **起動**: テスト画像（グラデーション＋グリッド＋マーカー）が表示されます。
2. **マウス追従**: テスト画像上をマウスで動かすと、カーソルの右下にルーペが追従して拡大表示されます。 画像の外では、画像外の色（無地）が表示されます。
3. **ルーペの表示/非表示**: 「ルーペを表示」チェックで切り替えます（非表示時は Bitmap 解放）。
4. **外観の変更**: ズーム・サイズ・色・タイルサイズ・枠の太さ・描画スケールをスライダー/コンボボックスで変更します（即時反映）。
5. **基本サイズ（Options.RequestedSize）**: スライダーで基本サイズを変更すると、その値がそのまま表示サイズになります。
6. **選択矩形オーバーレイ**: 「選択矩形を表示」を ON にすると、`SelectionSourceRect`（対象座標 = 元画像ピクセル）の枠線がルーペ画像上に 2 色破線（色A/色B）で表示されます（ズーム 2 倍以上）。 X / Y / W / H・色A / 色B を変更できます（不透明度は色のアルファに統合）。
7. **API 呼び出し**: `Clear()` / `RequestRender()` ボタンで各公開 API の動作を確認できます。
8. **拡張例**: 設定パネル上部の見本は、提示層を派生して見た目を差し替えた例です（次節）。

## 7. 拡張例（派生 Presenter）

`Presentation/` に、既定の提示層（`LoupePresenter`）を派生して見た目を差し替える見本があります（設定パネル上部の見本）。

- `GridLoupePresenter` — 外観（色・枠線の太さ・表示可否）を生成時に自前で設定し、`ResolveDrawPlan` を上書きして装飾の矩形（中心の格子線・四隅の鉤）を差し替えます。
- `GridLoupePreview` — 派生提示層が解決した描画計画を `LoupeContextPainter` で描くだけのコントロールです。

提示層（`EsUtil.Feature.Loupe.Presenter`）と描画実装（`EsUtil.Feature.Loupe.ControlAvaloniaUI` の `LoupeContextPainter`）はともに公開型であるため、コントロールを通さずに公開 API だけで組み合わせられます（拡大元の画像は見本が自前で用意します）。

| 差し替える対象 | 差し替え方 |
| --- | --- |
| 外観（色・枠線の太さ・表示可否） | 生成時に `Appearance`（`LoupeAppearance`）を設定する |
| 装飾（十字線・枠線）の矩形 | `ResolveDrawPlan` を上書きし、計画（`LoupeDrawPlan`）の矩形を差し替える |
| ラベルの文言・配置 | 同上（`LoupeDrawPlan.Labels` を差し替える） |

差し替えは値の置き換えだけで完結するため、UI ごとの描画実装（Avalonia は `LoupeContextPainter`、MewUI は `LoupeSkiaPainter`）は変更不要です。
