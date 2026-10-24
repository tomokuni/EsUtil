# EsUtil.Ui.AvaloniaUI.ColorComboBox

[![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.AvaloniaUI.ColorComboBox.svg)](https://www.nuget.org/packages/EsUtil.Ui.AvaloniaUI.ColorComboBox)

> プロジェクト名: `EsUtil.Ui.AvaloniaUI.ColorComboBox`（Avalonia UI 版）。 MewUI 側は標準の `ColorPicker` を使うため移植していません（`XxxControl.Core` / `XxxControl.MewUI` は作成しません）。

## 目次 <!-- omit from toc -->

- [1. 概要](#1-概要)
- [2. 機能一覧](#2-機能一覧)
- [3. システム要件](#3-システム要件)
- [4. インストール](#4-インストール)
- [5. クイックスタート](#5-クイックスタート)
  - [5.1 AXAML](#51-axaml)
  - [5.2 Code-behind](#52-code-behind)
- [6. 入力動作](#6-入力動作)
- [7. ColorView テーマ（ホストが取り込む）](#7-colorview-テーマホストが取り込む)
- [8. プラットフォーム](#8-プラットフォーム)
- [9. サンプル](#9-サンプル)
- [10. ドキュメント](#10-ドキュメント)
- [11. アーキテクチャ](#11-アーキテクチャ)
- [12. プロジェクト構成](#12-プロジェクト構成)
- [13. テスト](#13-テスト)
- [14. ライセンス](#14-ライセンス)

---

## 1. 概要

Avalonia UI 用の色選択コントロールです。 色名/16進コード入力の ComboBox とカラーピッカーを統合し、選択色のプレビュー・バリデーションを自己完結します。

## 2. 機能一覧

- 色名（英名）または16進カラーコードのテキスト入力
- ドロップダウンによる色一覧選択（色相順 / 色名順）
- 選択中の色をテキスト領域左端にスウォッチ表示
- カラーピッカー（ColorView）を Flyout で表示
- 入力バリデーション（無効な色はエラー通知・最終有効値への自動復帰）
- バリデーション失敗時の枠線強調（オプション）
- Avalonia Binding対応のStyledProperty（双方向バインド）
- ComboBox のサイズ・パディング・マージンを外部制御可能

## 3. システム要件

- .NET 10
- Avalonia 12.1 以降
- C# 14

## 4. インストール

NuGet パッケージ（`EsUtil.Ui.AvaloniaUI.ColorComboBox`）またはプロジェクト参照で利用します。

```xml
<ProjectReference Include="..\EsUtil.Ui.AvaloniaUI.ColorComboBox\src\EsUtil.Ui.AvaloniaUI.ColorComboBox.csproj" />
```

必要な名前空間:

```csharp
using EsUtil.Ui.AvaloniaUI.ColorComboBox;
```

## 5. クイックスタート

### 5.1 AXAML

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:cp="clr-namespace:EsUtil.Ui.AvaloniaUI.ColorComboBox;assembly=EsUtil.Ui.AvaloniaUI.ColorComboBox">
    <cp:ColorComboBoxControl SelectedColor="{Binding BorderColor, Mode=TwoWay}"
                           ColorSortMode="ByHue"
                           HighlightBorderOnError="True"
                           Width="300" />
</Window>
```

### 5.2 Code-behind

```csharp
using EsUtil.Ui.AvaloniaUI.ColorComboBox;

var colorPicker = new ColorComboBoxControl
{
    SelectedColor = "Red",
    ColorSortMode = ColorSortMode.ByHue,
    HighlightBorderOnError = true,
    Width = 300,
};

// 選択色の変更を監視（双方向バインドの代替）
colorPicker.PropertyChanged += (_, e) =>
{
    if (e.Property == ColorComboBoxControl.SelectedColorProperty)
    {
        UpdatePreview(colorPicker.SelectedColor);
    }
};

panel.Children.Add(colorPicker);
```

## 6. 入力動作

- **テキスト入力**: 色名または16進コードを直接入力できます。
- **ドロップダウン選択**: 選択後にテキストの全選択が自動解除されます。
- **無効な入力**: フォーカス喪失時に最終有効値へ自動復帰します。
- **カラーピッカー**: 右端の「…」ボタンで Flyout 形式の ColorView を開きます。 OK で適用、キャンセルで破棄。

## 7. ColorView テーマ（ホストが取り込む）

**ホストは ColorView テーマをアプリの XAML へ `StyleInclude` で取り込んでください**。

```xml
<Application.Styles>
    <FluentTheme />
    <StyleInclude Source="avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml" />
</Application.Styles>
```

- Flyout はオーバーレイ レイヤーへ表示されるため、コントロールのローカル Styles では ColorView へ伝播しません。 アプリの `Application.Styles` へ取り込む必要があります。
- 取り込みを忘れるとカラーピッカーがテーマ未適用（既定の見た目）で表示されます（例外にはなりません）。
- **本コントロールは実行時の XAML 解決を行いません**（トリミング / Native AOT では失敗するため、自動適用はしません）。

## 8. プラットフォーム

- プラットフォーム非依存（Avalonia のみに依存）。
- Windows / Linux / macOS で同一の動作を提供します。

## 9. サンプル

`sample/` に動作確認用のサンプルアプリが含まれています。 以下の機能をデモします。

- 色相順（`ByHue`）と色名順（`ByName`）の切替（並び順切替 ComboBox 連動）
- `HighlightBorderOnError` による枠線強調の有効/無効
- バリデーション状態（`HasValidationError` / `ValidationErrorMessage`）のライブ表示
- ComboBox レイアウト設定（`ComboBoxMinWidth` / `ComboBoxMaxWidth` / `ComboBoxWidth` / `ComboBoxPadding` / `ComboBoxMargin`）
- カラーピッカー（「…」ボタン）の Flyout 表示

```bash
dotnet run --project sample/EsUtil.Ui.AvaloniaUI.ColorComboBox.Sample.csproj
```

## 10. ドキュメント

- [外部機能仕様書](docs/SPEC_API.md)
- [システム仕様書](docs/SPEC_System.md)

## 11. アーキテクチャ

公開 API（公開プロパティ・StyledProperty）は `src/ColorComboBoxControl.axaml.cs`（XAML コードビハインド）に集約します。
関心事ごとに専用クラスへ委譲し、単一責任（SRP）と一方向依存を維持します。

- `ColorComboBoxControl.axaml.cs`: 公開 API（StyledProperty・公開プロパティ）＋ イベント処理 ＋ バリデーション状態
- `ColorComboBoxControl.axaml`: UI レイアウト（ComboBox + スウォッチ + Button）
- `ColorComboBoxFlyoutFactory.cs`: カラーピッカー Flyout の構築（Simple Factory パターン）
- `ColorValidator.cs`: 色文字列の妥当性検証
- `TextBoxSelectionHelper.cs`: ビジュアルツリー内の TextBox 検索と選択解除
- `ColorSortMode.cs`: 公開 enum（並び順）
- `StringToBrushConverter.cs`: 公開コンバーター（色 → Brush）
- `ColorDisplayConverter.cs`: 公開コンバーター（保存値 ↔ 表示テキスト）
- `ColorString.cs`: 公開静的ユーティリティ（色文字列の解析・整形・正規化）
- `ColorEntry.cs`: 色一覧データ（internal）

依存方向は**一方向の階層構造**（コントロール → 生成/検証 → 対象（ColorString / ColorEntry / コンバーター））で、双方向依存・内部公開・具象依存を解消します。

## 12. プロジェクト構成

```text
EsUtil.Ui.AvaloniaUI/ColorPicker/
├── src/
│   ├── EsUtil.Ui.AvaloniaUI.ColorComboBox.csproj
│   ├── ColorComboBoxControl.axaml          # 公開コントロール（UIレイアウト）
│   ├── ColorComboBoxControl.axaml.cs       # コードビハインド（公開 API・StyledProperty・イベント処理・バリデーション状態）
│   ├── ColorComboBoxFlyoutFactory.cs# カラーピッカー Flyout 構築（Simple Factory）
│   ├── ColorValidator.cs          # 色文字列の妥当性検証
│   ├── TextBoxSelectionHelper.cs  # TextBox 検索とテキスト選択解除
│   ├── ColorSortMode.cs           # 並び順 enum（公開）
│   ├── StringToBrushConverter.cs  # 色→ブラシコンバーター（公開）
│   ├── ColorDisplayConverter.cs   # 保存値↔表示テキスト変換（公開）
│   ├── ColorString.cs             # 色文字列ユーティリティ（公開）
│   ├── ColorEntry.cs              # 色名レコード・全色一覧（internal）
│   └── Logging/                   # LoggerMessage ログ定義（internal）
├── sample/                        # サンプルアプリ（sample/README.md 参照）
└── tests/                         # 単体テスト（行・ブランチ 100%）
```

## 13. テスト

`tests/` が本プロジェクトのテストを持ちます（xUnit v3）。 実行手順と合否基準は [検証手順.md](../../.github/検証手順.md) が定めます。

```powershell
dotnet test EsUtil.Ui.AvaloniaUI/ColorComboBox/tests/EsUtil.Ui.AvaloniaUI.ColorComboBox.Tests.csproj
```

## 14. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
