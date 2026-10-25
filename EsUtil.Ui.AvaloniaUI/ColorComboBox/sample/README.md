# EsUtil.Ui.AvaloniaUI.ColorComboBox サンプルアプリケーション

## 目次 <!-- omit from toc -->

- [1. 概要](#1-概要)
- [2. 実行方法](#2-実行方法)
  - [2.1 バッチファイル（リリースビルド＆起動）](#21-バッチファイルリリースビルド起動)
- [3. 操作方法](#3-操作方法)
- [4. 検証項目（動作確認チェックリスト）](#4-検証項目動作確認チェックリスト)
- [5. 構成](#5-構成)
- [6. 使い方（初めての方向け）](#6-使い方初めての方向け)
- [7. バリデーション動作の確認](#7-バリデーション動作の確認)
- [8. AXAML での利用パターン](#8-axaml-での利用パターン)

---

## 1. 概要

`ColorComboBox/sample/` にある **ColorComboBoxControl 動作確認用の独立したサンプルアプリケーション**です。

- `ColorComboBoxControl`（`EsUtil.Ui.AvaloniaUI.ColorComboBox.ColorComboBoxControl`）を実際に Visual Tree へ配置して動作を確認できます。
- 色名 / 16進コードのテキスト入力、ドロップダウン選択、カラーピッカー（ColorView Flyout）の動作を確認できます。
- 公開プロパティ（`SelectedColor` / `ColorSortMode` / `TextFormat` / `HighlightBorderOnError` / `TextMinWidth` / `TextMaxWidth` / `TextWidth` / `TextPadding` / `TextMargin`）のバインドと動的変更を確認できます。
- バリデーション状態（`HasValidationError` / `ValidationErrorMessage`）の読み取りを確認できます。

---

## 2. 実行方法

```powershell
dotnet run --project EsUtil.Ui.AvaloniaUI/ColorComboBox/sample/EsUtil.Ui.AvaloniaUI.ColorComboBox.Sample.csproj
```

リリース構成で実行する場合:

```powershell
dotnet run -c Release --project EsUtil.Ui.AvaloniaUI/ColorComboBox/sample/EsUtil.Ui.AvaloniaUI.ColorComboBox.Sample.csproj
```

### 2.1 バッチファイル（リリースビルド＆起動）

`EsUtil.Ui.AvaloniaUI/ColorComboBox/sample/RunSample.bat` をダブルクリックすると、リリースビルドを行った後、サンプルアプリケーションを起動します。

```text
RunSample.bat  →  リリースビルド（dotnet build -c Release）→ EsUtil.Ui.AvaloniaUI.ColorComboBox.Sample.exe を起動
```

> 本サンプルは独立したプロジェクトです（`sample/` はコントロール本体のプロジェクト フォルダの外にあり、本体の `csproj` のコンパイル対象には含まれません）。

---

## 3. 操作方法

| 要素 | 内容 |
| --- | --- |
| 選択色プレビュー | `SelectedColor` を公開コンバーター（`StringToBrushConverter`）で Brush 化して表示 |
| 色相順（ByHue）の ColorComboBoxControl | `ColorSortMode="ByHue"` でドロップダウンが色相順 |
| 色名順（ByName）の ColorComboBoxControl | `ColorSortMode="ByName"` でドロップダウンが色名順 |
| 入力欄のレイアウト設定 | `TextMinWidth` / `TextMaxWidth` / `TextWidth` / `TextPadding` / `TextMargin` を外部制御 |
| バリデーション状態表示 | メインの ColorComboBoxControl から `HasValidationError` / `ValidationErrorMessage` を読み取り表示 |
| 設定パネル | `ColorSortMode` 切替 / `TextFormat` 切替 / `HighlightBorderOnError` ON/OFF / `TextMinWidth` スライダー / 現在値の表示 |
| カラーピッカーボタン | 各 ColorComboBoxControl 右側の「…」ボタンから ColorView Flyout を起動 |

---

## 4. 検証項目（動作確認チェックリスト）

- [ ] テキスト入力で色名（`Red`）と16進コード（`#FF0000`）の両方が反映される
- [ ] ドロップダウンで色を選択すると `SelectedColor` が更新され、スウォッチ・プレビューに反映される
- [ ] `ColorSortMode` 切替でドロップダウンの並び順が変わる（色相順 / 色名順）
- [ ] 「…」ボタンでカラーピッカー（ColorView Flyout）が開き、OK で選択色が反映される
- [ ] 無効な色を入力してフォーカスを外すと最終有効値へ復帰し、枠線が赤くなる（HighlightBorderOnError 時）
- [ ] `TextMinWidth` / `TextMaxWidth` / `TextWidth` の変更がレイアウトに反映される
- [ ] `TextPadding` / `TextMargin` の設定が入力欄に反映される

---

## 5. 構成

```text
EsUtil.Ui.AvaloniaUI/ColorComboBox/sample/
├── EsUtil.Ui.AvaloniaUI.ColorComboBox.Sample.csproj   # サンプルアプリのプロジェクト（net10.0）
├── app.manifest                 # Windows DPI 設定（PerMonitorV2）
├── Program.cs                   # エントリポイント
├── App.axaml / App.axaml.cs     # アプリケーション定義（FluentTheme）
├── ViewModels/
│   └── MainWindowViewModel.cs   # 色の保持（各コントロールへ双方向バインド）
├── Views/
│   └── MainWindow.axaml(.cs)    # メイン画面（3 つの ColorComboBoxControl とバリデーション表示）
└── RunSample.bat                # リリースビルド＆起動
```

---

## 6. 使い方（初めての方向け）

1. **色名 / 16進コードの入力**: 任意の ColorComboBoxControl のテキスト欄に色名（`Red`）または16進コード（`#FF0000`）を入力します。 入力のたびに選択色が更新され、プレビュー・スウォッチへ反映されます。
2. **ドロップダウン選択**: ▼ を開いて色を選ぶと、テキスト選択状態が自動解除されて選択色が反映されます。
3. **カラーピッカー（…ボタン）**: 右側の「…」ボタンから ColorView の Flyout が開きます。 色を選んで **OK** で16進コードとして反映、**キャンセル** で破棄します。
4. **並び順の切替**: 設定パネルの「並び順」で色相順（ByHue）と色名順（ByName）を切り替えます（全コントロールへ連動）。
5. **バリデーション**: 無効な色（例: `zzz`）を入力してフォーカスを外すと、最終有効値へ自動復帰します。 `HighlightBorderOnError` を ON にすると枠線が赤くなります。
6. **入力欄のレイアウト**: 設定パネルの `TextMinWidth` で幅を変更できます（レイアウト確認用）。

---

## 7. バリデーション動作の確認

無効な色（例: `zzz`）を入力してフォーカスを外すと:

1. `HasValidationError` が `true` になり、`ValidationErrorMessage` に `"無効な色コードです。"` が設定される
2. `HighlightBorderOnError="True"` の場合、外周の枠線が赤くなる
3. フォーカス喪失時に**最終有効値**へ自動復帰する（`SelectedColor` が最後に有効だった色に戻る）

---

## 8. AXAML での利用パターン

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:cp="clr-namespace:EsUtil.Ui.AvaloniaUI.ColorComboBox;assembly=EsUtil.Ui.AvaloniaUI.ColorComboBox">
    <StackPanel>
        <cp:ColorComboBoxControl SelectedColor="{Binding BorderColor, Mode=TwoWay}"
                               ColorSortMode="ByHue"
                               HighlightBorderOnError="True"
                               Width="300" />
    </StackPanel>
</Window>
```
