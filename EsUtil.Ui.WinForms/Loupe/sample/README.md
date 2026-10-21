# EsUtil.Ui.WinForms.Loupe サンプルアプリケーション

## 目次 <!-- omit from toc -->

- [1. 概要](#1-概要)
- [2. 実行方法](#2-実行方法)
  - [2.1 バッチファイル（リリースビルド＆起動）](#21-バッチファイルリリースビルド起動)
- [3. 操作方法](#3-操作方法)
- [4. 検証項目（動作確認チェックリスト）](#4-検証項目動作確認チェックリスト)
- [5. 構成](#5-構成)
- [6. 使い方（初めての方向け）](#6-使い方初めての方向け)
- [7. 拡張例（計画の差し替え）](#7-拡張例計画の差し替え)
- [8. 他の UI 版サンプルとの違い](#8-他の-ui-版サンプルとの違い)

---

## 1. 概要

`EsUtil.Ui.WinForms/Loupe/sample/` にある **LoupeControl（WinForms 版）の動作確認用の独立したサンプルアプリケーション**です。

- `LoupeControl`（`EsUtil.Ui.WinForms.Loupe`）をウィンドウへ配置して動作を確認できます。
- テスト画像（グラデーション＋グリッド＋マーカー。 800×600）を生成し、マウス追従で拡大表示します。
- 外観（拡大率・サイズ・十字線・画像外の色・枠線・描画スケール・座標ラベル）をスライダー／コンボボックスで変更できます。
- 見本として、外観と描画計画を差し替えたルーペ（格子線＋四隅の鉤）を同時に表示します。
- 選択矩形の破線を、テスト画像の上とルーペ画像の中の両方に表示します（ルーペ内は `Rendered` の描画面へ描きます）。
- `LoupeControl.Log` に `ILogger` を設定し、コントロール内部の設定変更ログを画面で確認できます。

## 2. 実行方法

```powershell
dotnet run --project EsUtil.Ui.WinForms/Loupe/sample/EsUtil.Ui.WinForms.Loupe.Sample.csproj
```

リリース構成で実行する場合:

```powershell
dotnet run -c Release --project EsUtil.Ui.WinForms/Loupe/sample/EsUtil.Ui.WinForms.Loupe.Sample.csproj
```

### 2.1 バッチファイル（リリースビルド＆起動）

`EsUtil.Ui.WinForms/Loupe/sample/RunSample.bat` をダブルクリックすると、リリースビルドを行った後、サンプルアプリケーションを起動します。

```text
RunSample.bat  →  リリースビルド（dotnet build -c Release）→ EsUtil.Ui.WinForms.Loupe.Sample.exe を起動
```

> 本サンプルは独立したプロジェクトです。
> コントロール本体の `EsUtil.Ui.WinForms.Loupe.csproj` は `sample/` をコンパイル対象に含めません（`sample/` は本体のプロジェクト フォルダの外にあるため）。

## 3. 操作方法

| 操作 | 内容 |
| --- | --- |
| テスト画像上をマウス移動 | ルーペがカーソルの右下（+24 DIP）に追従して拡大表示 |
| 画像の外へマウス移動 | 画像外の領域のみ無地の色（画像外の色。 視野全体が外に出た場合は全面がその色） |
| 「ルーペを表示」チェック | `Visible` の切り替え（非表示時は `ResolveLayout` の結果も使わないため描画されない） |
| 拡大率のスライダー | `Options.ZoomLevel`（1〜16 倍） |
| ルーペ サイズのスライダー | `Options.RequestedSize`（80〜480 DIP。 ホストが DIP から物理ピクセルへ換算して配置する） |
| 描画スケールのスライダー | `ClientToPixelScaling`（0.5〜2.0。 DIP → 物理ピクセル変換の確認） |
| 色コンボボックス | `Appearance`（十字線 / 画像外 / 枠線。 「なし」で描画なし） |
| 枠線の太さのスライダー | `Appearance.BorderThickness`（0〜8 DIP） |
| 「座標ラベルを表示」チェック | `Appearance.ShowCoordLabel`（ルーペ左上の中心座標ラベル） |
| 「再描画を要求」ボタン | `Invalidate()`（プロパティ変更を伴わない再描画要求） |
| 「非表示にして解放」ボタン | `Visible` を false にして `Source` を null にする（未設定時の下地の確認） |
| 「ソースを再設定」ボタン | `Source` へテスト画像を設定 |
| 「選択矩形を表示」チェック | 選択矩形の破線オーバーレイの表示切り替え（画像側とルーペ内の両方） |
| 選択矩形の X / Y / W / H 入力 | 破線の位置・サイズ（対象座標 = 元画像ピクセル座標）。 変更は `Invalidate()` でルーペへ反映する |
| 「格子線と四隅の鉤を描く見本を表示」チェック | 差し替えた外観と描画計画のルーペ（見本）の表示切り替え |

## 4. 検証項目（動作確認チェックリスト）

- [ ] マウス追従で拡大画像が表示される（グラデーション・円マーカー・グリッド）
- [ ] 10px グリッドがズーム倍率どおりに拡大される（拡大の検証。 画素がぼやけない）
- [ ] 拡大率・サイズの変更が即時反映される
- [ ] 十字線・画像外の色・枠線の色が反映される（不透明度は色のアルファ）
- [ ] 「なし」を選ぶと該当要素が描画されなくなる
- [ ] 「なし」を選んだ画像外の色では下地（パネルの背景色）が見える（透明を画面へ伝えられないための仕様）
- [ ] 描画スケールを 0.5〜2.0 に変更するとルーペの装飾・ラベルの大きさが変わる（物理ピクセル換算の確認）
- [ ] 「ルーペを表示」を OFF にすると描画されず、ON で再表示される
- [ ] 「非表示にして解放」で描画が消え、「ソースを再設定」で元に戻る
- [ ] 「再描画を要求」で描画が更新される
- [ ] ルーペの左上に中心座標ラベル（`(x, y)`）が表示され、マウス移動で文言が追従する
- [ ] 「選択矩形を表示」を ON にすると、テスト画像上に選択矩形の破線枠＋座標ラベルが表示される
- [ ] 選択矩形の X / Y / W / H を変更すると、画像上の枠とルーペ内の破線の位置・サイズが変化する
- [ ] 見本は十字線の代わりに中心の格子線、枠線の代わりに四隅の鉤を描く（アンバー）
- [ ] ログ欄に設定・環境値・拡大中心の変更ログが表示される（接頭辞 `[Loupe]` / `[Preview]`）

## 5. 構成

| パス | 内容 |
| --- | --- |
| `EsUtil.Ui.WinForms/Loupe/sample/EsUtil.Ui.WinForms.Loupe.Sample.csproj` | サンプルアプリのプロジェクト（`net10.0-windows10.0.17763.0` / `UseWindowsForms` / `WinExe`） |
| `EsUtil.Ui.WinForms/Loupe/sample/RunSample.bat` | リリースビルド＆起動 |
| `EsUtil.Ui.WinForms/Loupe/sample/Program.cs` | エントリポイント（高 DPI（PerMonitorV2）の指定と起動） |
| `EsUtil.Ui.WinForms/Loupe/sample/Imaging/TestImageFactory.cs` | テスト画像生成（グラデーション＋グリッド＋マーカー） |
| `EsUtil.Ui.WinForms/Loupe/sample/Logging/InMemoryLogger.cs` | 画面のログ欄へ出力する `ILogger` |
| `EsUtil.Ui.WinForms/Loupe/sample/Presentation/GridLoupePlan.cs` | 外観と描画計画の差し替え（見本） |
| `EsUtil.Ui.WinForms/Loupe/sample/Views/MainForm.cs` | メイン ウィンドウ（設定パネル＋テスト画像＋ルーペ） |
| `EsUtil.Ui.WinForms/Loupe/sample/Views/LoupeHostPanel.cs` | 画像・選択矩形・ルーペを重ねるホスト（配置とマウス位置の通知） |
| `EsUtil.Ui.WinForms/Loupe/sample/Views/PointerMovedEventArgs.cs` | マウス位置の通知 |
| `EsUtil.Ui.WinForms/Loupe/sample/Views/SelectionState.cs` | 選択矩形の状態（表示可否と対象座標の矩形） |
| `EsUtil.Ui.WinForms/Loupe/sample/Views/LoupeSelectionPainter.cs` | ルーペ画像上の破線（`Rendered` の使用例） |
| `EsUtil.Ui.WinForms/Loupe/sample/Internal/EsBitmapDrawingExtensions.cs` | 背景のテスト画像を描くための画像変換（`Internal` の複製。 依存関係 §4.1） |

## 6. 使い方（初めての方向け）

1. **起動**: テスト画像（グラデーション＋グリッド＋マーカー）が表示されます。
2. **マウス追従**: テスト画像上をマウスで動かすと、カーソルの右下にルーペが追従して拡大表示されます。
3. **外観の変更**: 拡大率・サイズ・描画スケール・色（十字線・画像外・枠線）・枠線の太さ・座標ラベルを変更できます。
4. **見本**: 右上の見本のルーペは、外観と描画計画を差し替えて格子線と四隅の鉤を描きます。
5. **ログ**: 左のログ欄に、コントロールが出力する変更ログが表示されます。

## 7. 拡張例（計画の差し替え）

`GridLoupePlan` は、提示層を派生せずに **外観（`LoupeAppearance`）と描画計画（`LoupeDrawPlan`）だけで見た目を差し替える**例です。

```csharp
var preview = new LoupeControl
{
    Source = previewSource,
    Appearance = GridLoupePlan.Appearance,                          // 色・枠線の太さ
    DrawPlanResolver = static plan => GridLoupePlan.Resolve(in plan), // 十字線と枠線の矩形を差し替える
};
```

差し替える値は「塗る矩形の列」であるため、DIP で書いた寸法は計画が持つ倍率（`LoupeDrawPlan.ScaleDipToPixel`）で物理ピクセルへ換算します（DPI が変わっても DIP 基準の見た目を保つ）。

## 8. 他の UI 版サンプルとの違い

| 項目 | 内容 |
| --- | --- |
| 配置 | ルーペは親（ホスト パネル）の子コントロールである。 ホストが `ResolveLayout` の結果を `ToClientPixel` で物理ピクセルへ換算して `Bounds` を設定する |
| 座標 | 背景のテスト画像を 1:1（物理ピクセル）で描くため、ホストのクライアント座標がそのまま元画像ピクセル座標になる |
| ルーペ上のマウス | ルーペは子であるため、ルーペ上の `MouseMove` を親の座標へ写して追従を続ける（`LoupeHostPanel` の使用方法を参照） |
| 半透明の扱い | 画像外の色を「なし」にすると下地（パネルの背景色）が見える（WinForms は画面へアルファを伝えられない） |
