# EsUtil.Feature.Loupe.ControlWinUI3 サンプルアプリケーション

## 目次 <!-- omit from toc -->

- [1. 概要](#1-概要)
- [2. 実行方法](#2-実行方法)
  - [2.1 バッチファイル（リリースビルド＆起動）](#21-バッチファイルリリースビルド起動)
- [3. 操作方法](#3-操作方法)
- [4. 検証項目（動作確認チェックリスト）](#4-検証項目動作確認チェックリスト)
- [5. 構成](#5-構成)
- [6. 使い方（初めての方向け）](#6-使い方初めての方向け)
- [7. 拡張例（計画の差し替え）](#7-拡張例計画の差し替え)
- [8. WinUI 3 版サンプルの注意点](#8-winui-3-版サンプルの注意点)

---

## 1. 概要

`EsUtil.Feature.Loupe.ControlWinUI3/sample/` にある **LoupeControl（WinUI 3 版）の動作確認用の独立したサンプル アプリケーション**です。

- `LoupeControl`（`EsUtil.Feature.Loupe.WinUI3`）をウィンドウへ配置して動作を確認できます（構成は XAML、結線はコードビハインド）。
- テスト画像（グラデーション＋グリッド＋円マーカー。 800×600）を生成し、マウス追従で拡大表示します。
- 外観（拡大率・サイズ・描画スケール・十字線・画像外の色・枠線・枠線の太さ・座標ラベル）をスライダー/コンボ ボックス/チェック ボックスで変更できます。
- 見本として、外観と描画計画を差し替えたルーペ（格子線＋四隅の鉤）を同時に表示します。
- 選択矩形の破線を、テスト画像の上とルーペ画像の中の両方に表示します（ルーペ内は `Rendered` のキャンバスへ描きます）。
- `LoupeControl.Log` に `ILogger` を設定し、コントロール内部の設定変更ログを画面で確認できます。

## 2. 実行方法

```powershell
dotnet run --project src/EsUtil.Feature.Loupe.ControlWinUI3/sample/EsUtil.Feature.Loupe.ControlWinUI3.Sample.csproj
```

リリース構成で実行する場合:

```powershell
dotnet run -c Release --project src/EsUtil.Feature.Loupe.ControlWinUI3/sample/EsUtil.Feature.Loupe.ControlWinUI3.Sample.csproj
```

### 2.1 バッチファイル（リリースビルド＆起動）

`src/EsUtil.Feature.Loupe.ControlWinUI3/sample/RunSample.bat` を実行すると、リリース ビルドを行った後、サンプル アプリケーションを起動します。

```text
RunSample.bat  →  リリースビルド（dotnet build -c Release）→ EsUtil.Feature.Loupe.ControlWinUI3.Sample.exe を起動
```

> 本サンプルは独立したプロジェクトです。
> コントロール本体の `EsUtil.Feature.Loupe.ControlWinUI3.csproj` は `sample/` をコンパイル対象に含めません（`sample/` は本体のプロジェクト フォルダの外にあるため）。

## 3. 操作方法

| 操作 | 内容 |
| --- | --- |
| テスト画像上をマウス移動 | ルーペがカーソルの右下（+24 DIP）に追従して拡大表示（`CenterPoint` の更新とホスト側での再配置） |
| 画像の外へマウス移動 | 画像外の領域のみ無地の色（画像外の色。 視野全体が外に出た場合は全面がその色） |
| 「ルーペを表示」チェック | ルーペの表示の切り替え（非表示時は `ResolveLayout` が位置を返さないため描画されない） |
| 拡大率のスライダー | `Options.ZoomLevel`（1〜16 倍） |
| ルーペ サイズのスライダー | `Options.RequestedSize`（80〜480 DIP。 そのまま表示サイズになる） |
| 描画スケールのスライダー | `ClientToPixelScaling`（0.5〜2.0。 環境値の補正と再描画の確認） |
| 色のコンボ ボックス | `Appearance`（十字線 / 画像外 / 枠線。 「なし」で描画なし） |
| 枠線の太さのスライダー | `Appearance.BorderThickness`（0〜8 DIP） |
| 「座標ラベルを表示」チェック | `Appearance.ShowCoordLabel`（ルーペ左上の中心座標ラベル） |
| 「再描画を要求」ボタン | `Loupe.Invalidate()`（プロパティ変更を伴わない再描画要求） |
| 「非表示にして解放」ボタン | ルーペを非表示にして `Source` を null にする（未設定時の下地の確認） |
| 「ソースを再設定」ボタン | `Source` へテスト画像を設定し、表示を戻す |
| 「選択矩形を表示」チェック | 選択矩形の破線オーバーレイの表示切り替え（画像側とルーペ内の両方） |
| 選択矩形の X / Y / W / H 入力 | 破線の位置・サイズ（対象座標 = 元画像ピクセル座標） |
| 「格子線と四隅の鉤を描く見本を表示」チェック | 差し替えた外観と描画計画のルーペ（見本）の表示切り替え |

## 4. 検証項目（動作確認チェックリスト）

- [ ] マウス追従で拡大画像が表示される（グラデーション・円マーカー・グリッド）
- [ ] 10px グリッドが拡大率どおりに拡大される（倍率の検証）
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
- [ ] モニター間を移動すると（DPI が変わっても）拡大画像と図形の倍率が一致したままになる

## 5. 構成

```text
src/EsUtil.Feature.Loupe.ControlWinUI3/sample/
├── EsUtil.Feature.Loupe.ControlWinUI3.Sample.csproj  # サンプル アプリのプロジェクト（WinUI 3・アンパッケージ）
├── RunSample.bat                # リリース ビルド＆起動
├── app.manifest                 # 実行ファイルのマニフェスト（DPI 対応・対応 OS）
├── App.xaml                     # アプリケーションのリソース（WinUI 3 のスタイル）
├── App.xaml.cs                  # エントリ（メイン ウィンドウの生成）
├── Imaging/
│   └── TestImageFactory.cs      # テスト画像生成（グラデーション＋グリッド＋マーカー）
├── Internal/
│   └── EsBitmapSkiaExtensions.cs # コアの画像を Skia で読む（サンプルが使う分だけの複製）
├── Logging/
│   └── InMemoryLogger.cs        # 画面のログ欄へ出力する ILogger
├── Presentation/
│   ├── GridLoupePlan.cs         # 外観と描画計画の差し替え（見本）
│   ├── LoupeSelectionPainter.cs # ルーペ画像上の破線（Rendered の使用例）
│   ├── SelectionRectPainter.cs  # 破線枠の描画（画像側とルーペ側で共用）
│   └── SelectionState.cs        # 選択矩形の状態（表示可否と対象座標の矩形）
└── Views/
    ├── MainWindow.xaml          # メイン ウィンドウの構成（設定パネル＋テスト画像＋ルーペ）
    ├── MainWindow.xaml.cs       # 結線（プロパティの設定・配置・入力の受付）
    └── SourceCanvas.cs          # テスト画像と選択矩形の破線枠を描くキャンバス
```

## 6. 使い方（初めての方向け）

1. **起動**: テスト画像（グラデーション＋グリッド＋マーカー）が表示されます。
2. **マウス追従**: テスト画像上をマウスで動かすと、カーソルの右下にルーペが追従して拡大表示されます。
3. **外観の変更**: 拡大率・サイズ・描画スケール・色・枠線の太さ・座標ラベルを変更すると、ルーペの描画が即時に変わります。
4. **画像の切り替え**: 「非表示にして解放」で下地（画像外の色のみ）を確認し、「ソースを再設定」で元へ戻せます。
5. **選択矩形**: 選択矩形の X / Y / W / H を変えると、テスト画像側の破線枠とルーペ内の破線が連動します（ルーペ内の破線は `Rendered` が描いています）。
6. **見本**: 「格子線と四隅の鉤を描く見本を表示」で、外観と描画計画を差し替えたルーペを確認できます。
7. **ログ**: 左側のログ欄に、`LoupeControl.Log` へ設定したロガーの出力（接頭辞・レベル・メッセージ）が表示されます。

## 7. 拡張例（計画の差し替え）

見本のルーペは、描画層を派生せずに公開 API だけで見た目を差し替えています。

```csharp
// 外観（色・枠線の太さ・表示可否）は Appearance で差し替える
Preview.Appearance = GridLoupePlan.Appearance;

// 装飾の矩形とラベルの文言・位置は DrawPlanResolver で差し替える（純粋関数にすること）
Preview.DrawPlanResolver = static plan => GridLoupePlan.Resolve(in plan);
```

`GridLoupePlan.Resolve` は中心の格子線と四隅の鉤を「塗る矩形の列」として計画へ載せ替えるだけです（DIP の寸法は計画の `ScaleDipToPixel` で物理ピクセルへ換算します）。

## 8. WinUI 3 版サンプルの注意点

- **前提**: WinUI 3（Windows App SDK）の `net10.0-windows10.0.22621.0` のプロジェクトです。 ビルドには .NET 10 SDK と Windows App SDK のパッケージ（NuGet）が必要です。
- **対応 OS**: Windows 10 19041 以降（`SupportedOSPlatformVersion`）です。
- **実行形式**: **アンパッケージ**（`WindowsPackageType=None`）で実行します。 Windows App SDK は**自己完結**（`WindowsAppSDKSelfContained=true`）で配置するため、Windows App SDK ランタイムを別途インストールしなくても起動できます。 自己完結の配置は Windows のアーキテクチャ指定を要求するため、本プロジェクトは実行時識別子 `win-x64` を持ちます。
- **起動方法**: `dotnet run --project src/EsUtil.Feature.Loupe.ControlWinUI3/sample/EsUtil.Feature.Loupe.ControlWinUI3.Sample.csproj` または `RunSample.bat`（リリース ビルド＆起動）です。
- **サンプルが確認する項目**: [`3. 操作方法`](#3-操作方法)と[`4. 検証項目（動作確認チェックリスト）`](#4-検証項目動作確認チェックリスト)のとおりです（マウス追従・外観・計画の差し替え・描画完了イベント）。
- **プロジェクト参照**: サンプルは本体（`src/EsUtil.Feature.Loupe.ControlWinUI3/src/WinUI3/EsUtil.Feature.Loupe.ControlWinUI3.csproj`）を `ProjectReference` で参照します。 コアと提示層は本体のパッケージ参照を通して解決されます。
- **公開 API と実装の境界**: サンプルが使う本体の公開 API は `LoupeControl`（`Source` / `CenterPoint` / `Options` / `Appearance` / `ClientToPixelScaling` / `Log` / `LogPrefix` / `DrawPlanResolver` / `Rendered` / `ResolveLayout`）です。 本体の `internal`（描画実装と Internal のヘルパー）は参照できないため、**テスト画像を Skia で描くための変換だけを `sample/Internal/` へ複製**しています（`Rendered` のキャンバスへ描く破線は、サンプル自身の `src/EsUtil.Feature.Loupe.ControlWinUI3/sample/Presentation/SelectionRectPainter.cs` が描きます）。
- **コントロール本体の動作（プロパティの補正・変更ログ・DPI 追従）**は、XAML 型がヘッドレス環境で生成できないため自動テストの対象外です（理由は [`../docs/SPEC_System.md`](../docs/SPEC_System.md) のテスト方針を参照）。 本サンプルがその確認の場になります。
