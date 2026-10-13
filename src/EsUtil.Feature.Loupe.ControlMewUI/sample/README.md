# EsUtil.Feature.Loupe.ControlMewUI サンプルアプリケーション

## 目次 <!-- omit from toc -->

- [1. 概要](#1-概要)
- [2. 実行方法](#2-実行方法)
  - [2.1 バッチファイル（リリースビルド＆起動）](#21-バッチファイルリリースビルド起動)
- [3. 操作方法](#3-操作方法)
- [4. 検証項目（動作確認チェックリスト）](#4-検証項目動作確認チェックリスト)
- [5. 構成](#5-構成)
- [6. 使い方（初めての方向け）](#6-使い方初めての方向け)
- [7. 拡張例（計画の差し替え）](#7-拡張例計画の差し替え)
- [8. Avalonia 版サンプルとの違い](#8-avalonia-版サンプルとの違い)

---

## 1. 概要

`EsUtil.Feature.Loupe.ControlMewUI/sample/` にある **LoupeControl（MewUI 版）の動作確認用の独立したサンプルアプリケーション**です。

- `LoupeControl`（`EsUtil.Feature.Loupe.MewUI`）をウィンドウへ配置して動作を確認できます。
- テスト画像（グラデーション＋グリッド＋マーカー。 800×600）を生成し、マウス追従で拡大表示します。
- 外観（拡大率・サイズ・十字線・画像外の色・枠線・描画スケール・座標ラベル）をスライダー/コンボボックスで変更できます。
- 見本として、外観と描画計画を差し替えたルーペ（格子線＋四隅の鉤）を同時に表示します。
- 選択矩形の破線を、テスト画像の上とルーペ画像の中の両方に表示します（ルーペ内は `Rendered` のキャンバスへ描きます）。
- `LoupeControl.Log` に `ILogger` を設定し、コントロール内部の設定変更ログを画面で確認できます。

## 2. 実行方法

```powershell
dotnet run --project src/EsUtil.Feature.Loupe.ControlMewUI/sample/EsUtil.Feature.Loupe.ControlMewUI.Sample.csproj
```

リリース構成で実行する場合:

```powershell
dotnet run -c Release --project src/EsUtil.Feature.Loupe.ControlMewUI/sample/EsUtil.Feature.Loupe.ControlMewUI.Sample.csproj
```

### 2.1 バッチファイル（リリースビルド＆起動）

`src/EsUtil.Feature.Loupe.ControlMewUI/sample/RunSample.bat` をダブルクリックすると、リリースビルドを行った後、サンプルアプリケーションを起動します。

```text
RunSample.bat  →  リリースビルド（dotnet build -c Release）→ EsUtil.Feature.Loupe.ControlMewUI.Sample.exe を起動
```

> 本サンプルは独立したプロジェクトです。
> コントロール本体の `EsUtil.Feature.Loupe.ControlMewUI.csproj` は `sample/` をコンパイル対象に含めません（`sample/` は本体のプロジェクト フォルダの外にあるため）。

## 3. 操作方法

| 操作 | 内容 |
| --- | --- |
| テスト画像上をマウス移動 | ルーペがカーソルの右下（+24 DIP）に追従して拡大表示 |
| 画像の外へマウス移動 | 画像外の領域のみ無地の色（画像外の色。 視野全体が外に出た場合は全面がその色） |
| 「ルーペを表示」チェック | `IsVisible` の切り替え（非表示時は `ResolveLayout` が位置を返さないため描画されない） |
| 拡大率のスライダー | `Options.ZoomLevel`（1〜16 倍） |
| ルーペ サイズのスライダー | `Options.RequestedSize`（80〜480 DIP。 そのまま表示サイズになる） |
| 描画スケールのスライダー | `ClientToPixelScaling`（0.5〜2.0。 DIP → 物理ピクセル変換の確認） |
| 色コンボボックス | `Appearance`（十字線 / 画像外 / 枠線。 「なし」で描画なし） |
| 枠線の太さのスライダー | `Appearance.BorderThickness`（0〜8 DIP） |
| 「座標ラベルを表示」チェック | `Appearance.ShowCoordLabel`（ルーペ左上の中心座標ラベル） |
| 「再描画を要求」ボタン | `InvalidateVisual()`（プロパティ変更を伴わない再描画要求） |
| 「非表示にして解放」ボタン | Avalonia 版の `Clear()` と同じ結果（`IsVisible` を false にして `Source` を null にする） |
| 「ソースを再設定」ボタン | `Source` へテスト画像を設定（未設定時の下地の確認） |
| 「選択矩形を表示」チェック | 選択矩形の破線オーバーレイの表示切り替え（画像側とルーペ内の両方） |
| 選択矩形の X / Y / W / H 入力 | 破線の位置・サイズ（対象座標 = 元画像ピクセル座標）。 変更は `InvalidateVisual()` でルーペへ反映する |
| 「格子線と四隅の鉤を描く見本を表示」チェック | 差し替えた外観と描画計画のルーペ（見本）の表示切り替え |

## 4. 検証項目（動作確認チェックリスト）

- [ ] マウス追従で拡大画像が表示される（グラデーション・円マーカー・グリッド）
- [ ] 10px グリッドがズーム倍率どおりに拡大される（倍率の検証）
- [ ] 拡大率・サイズの変更が即時反映される
- [ ] 十字線・画像外の色・枠線の色が反映される（不透明度は色のアルファ）
- [ ] 「なし」を選ぶと該当要素が描画されなくなる
- [ ] 描画スケールを 0.5〜2.0 に変更するとルーペの描画が粗く/細かくなる（物理ピクセル数の変化）
- [ ] 「ルーペを表示」を OFF にすると描画されず、ON で再表示される
- [ ] 「非表示にして解放」で描画が消え、「ソースを再設定」で元に戻る
- [ ] 「再描画を要求」で描画が更新される
- [ ] ルーペの左上に中心座標ラベル（`(x, y)`）が表示され、マウス移動で文言が追従する
- [ ] 「選択矩形を表示」を ON にすると、テスト画像上に選択矩形の破線枠＋座標ラベルが表示される
- [ ] 選択矩形の X / Y / W / H を変更すると、画像上の枠とルーペ内の破線の位置・サイズが変化する
- [ ] 見本は十字線の代わりに中心の格子線、枠線の代わりに四隅の鉤を描く（アンバー）
- [ ] ログ欄に設定・環境値・拡大中心の変更ログが表示される（接頭辞 `[Loupe]` / `[Preview]`）

## 5. 構成

```text
EsUtil.Feature.Loupe.ControlMewUI/sample/
├── EsUtil.Feature.Loupe.ControlMewUI.Sample.csproj  # サンプルアプリのプロジェクト（net10.0-windows / MewUI 0.21.1）
├── RunSample.bat                # リリースビルド＆起動
├── Program.cs                   # エントリポイント（バックエンドとプラットフォーム ホストの登録）
├── Imaging/
│   └── TestImageFactory.cs      # テスト画像生成（グラデーション＋グリッド＋マーカー）
├── Logging/
│   └── InMemoryLogger.cs        # 画面のログ欄へ出力する ILogger
├── Presentation/
│   └── GridLoupePlan.cs         # 外観と描画計画の差し替え（見本）
└── Views/
    ├── LoupeHostPanel.cs           # 画像・選択矩形・ルーペを重ねるホスト（配置とマウス位置の通知）
    ├── PointerMovedEventArgs.cs    # マウス位置の通知
    ├── SelectionState.cs           # 選択矩形の状態（表示可否と対象座標の矩形）
    ├── SelectionRectOverlay.cs     # 画像上へ重ねる選択矩形の破線枠
    ├── LoupeSelectionPainter.cs    # ルーペ画像上の破線（Rendered の使用例）
    └── MainWindow.cs               # メイン ウィンドウ（設定パネル＋テスト画像＋ルーペ）
```

## 6. 使い方（初めての方向け）

1. **起動**: テスト画像（グラデーション＋グリッド＋マーカー）が表示されます。
2. **マウス追従**: テスト画像上をマウスで動かすと、カーソルの右下にルーペが追従して拡大表示されます。
3. **外観の変更**: 拡大率・サイズ・描画スケール・色・枠線の太さを変更します（即時反映）。
4. **表示の切り替え**: 「ルーペを表示」のチェックで表示/非表示を切り替えます。
5. **ソースの確認**: 「ソースを解除」で下地（未設定時）を確認し、「ソースを再設定」で戻します。

## 7. 拡張例（計画の差し替え）

見本のルーペは提示層を派生せずに、公開 API だけで見た目を差し替えています。

- 外観: `LoupeAppearance` の `with` 式で色・枠線の太さを変更する（`Appearance` へ設定）。
- 描画計画: `DrawPlanResolver` へ関数を設定し、十字線・枠線の矩形の列を差し替える（`GridLoupePlan.Resolve`）。

DIP で書いた寸法は計画が持つ倍率（`LoupeDrawPlan.ScaleDipToPixel`）で物理ピクセルへ換算するため、DPI が変わっても見た目は DIP 基準のままです。

## 8. Avalonia 版サンプルとの違い

同じコア・提示層を使うため、設定と見た目の意味は同じです。 UI フレームワークの違いにより次の点が異なります。

| 項目 | 本サンプル（MewUI） | Avalonia 版サンプル |
| --- | --- | --- |
| UI の記述 | C# のコードのみ（MewUI は XAML を持たない） | XAML + コードビハインド |
| 画像の受け渡し | `Source` へ `EsReadOnlyBitmap` を直接設定する | 非所有アダプタ（`IImage` ↔ `EsReadOnlyBitmap`）を経由する |
| ルーペの配置 | ホストが `ResolveLayout` の結果で配置する（`Arrange`）。 コントロールは位置を持たない | コントロール自身が `RenderTransform` と `PositionOffset` で配置する |
| 選択矩形の破線（画像側） | 破線を描く専用のキャンバス（`SelectionRectOverlay`）を重ねる | `Rectangle`（`StrokeDashArray`）とラベル要素を重ねる |
| 選択矩形の破線（ルーペ内） | `Rendered` の Skia キャンバスへ描く | `Rendered` の書き込み可能な画像のピクセルへ描く |
| 再描画の要求 | `InvalidateVisual()`（プロパティ変更を伴わない再描画要求） | `RequestRender()`（同左。 計画の破棄を含む） |
| 文字の描画 | 実寸測定（`MewUITextMeasure`）した結果を要素として置く | `Border` + `TextBlock`（`FormattedText`）を置く |
| 実行ファイルの対象 | `net10.0-windows`（Win32 のウィンドウ連携を使う） | `net10.0-windows10.0.17763.0`（`Avalonia.Desktop` を使う） |

`Clear()` / `RequestRender()` は Avalonia 版の `LoupeControl` の公開 API です（MewUI 版は中間ビットマップを持たず同期描画するため、同 API を持ちません）。 本サンプルは同じ結果になる操作（`IsVisible` + `Source` の解除、`InvalidateVisual()`）をボタンへ割り当てています。
