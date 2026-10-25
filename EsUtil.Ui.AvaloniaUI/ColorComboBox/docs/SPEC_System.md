# EsUtil.Ui.AvaloniaUI.ColorComboBox システム仕様書

## 目次 <!-- omit from toc -->

- [1. システム概要](#1-システム概要)
- [2. 技術スタック](#2-技術スタック)
- [3. アーキテクチャ](#3-アーキテクチャ)
  - [3.1 レイヤー構造](#31-レイヤー構造)
  - [3.2 依存方向](#32-依存方向)
- [4. 例外処理ポリシー](#4-例外処理ポリシー)
  - [4.1 基本方針](#41-基本方針)
  - [4.2 本プロジェクト固有の例外処理](#42-本プロジェクト固有の例外処理)
- [5. コンポーネント図](#5-コンポーネント図)
- [6. コンポーネント一覧](#6-コンポーネント一覧)
  - [6.1 ColorComboBoxControl](#61-colorcomboboxcontrol)
  - [6.2 ColorComboBoxControl.axaml](#62-colorcomboboxcontrolaxaml)
  - [6.3 ColorEntry](#63-colorentry)
  - [6.4 ColorSortMode](#64-colorsortmode)
  - [6.5 StringToBrushConverter](#65-stringtobrushconverter)
  - [6.6 ColorString](#66-colorstring)
  - [6.7 入力欄の表記（`TextFormat`）](#67-入力欄の表記textformat)
  - [6.8 ColorValidator](#68-colorvalidator)
  - [6.9 区画（スウォッチ・入力欄・▼・…）の構成](#69-区画スウォッチ入力欄の構成)
  - [6.10 ColorComboBoxFlyoutFactory](#610-colorcomboboxflyoutfactory)
- [7. 依存ルール](#7-依存ルール)
- [8. バリデーション状態遷移](#8-バリデーション状態遷移)
- [9. ライフサイクル](#9-ライフサイクル)
- [10. ログ](#10-ログ)
  - [10.1 LogLevel 設定ポリシー](#101-loglevel-設定ポリシー)
  - [10.2 ログ定義一覧](#102-ログ定義一覧)
- [11. テスト](#11-テスト)

---

## 1. システム概要

Avalonia UI の色選択コントロールです（.NET 10.0 / C# 14 / MVVM）。 色名・16 進コードの入力とカラーピッカー（`ColorView` の Flyout）を統合し、選択色のプレビューと検証を自己完結します。

本プロジェクト（`EsUtil.Ui.AvaloniaUI.ColorComboBox`）は、このシステムのうち**色選択 UI**（色名 / 16進入力 + ドロップダウン + カラーピッカー）を担う再利用可能なコントロールです。 app の設定画面などから双方向バインディングで利用されます。

## 2. 技術スタック

| 項目 | 内容 |
| --- | --- |
| **プロジェクト** | `EsUtil.Ui.AvaloniaUI.ColorComboBox` |
| **ルート名前空間** | `EsUtil.Ui.AvaloniaUI.ColorComboBox` |
| **言語** | C# 14（.NET 10.0） |
| **UI フレームワーク** | Avalonia UI 12.1（UserControl / ComboBox / ColorView / Flyout） |
| **ターゲットフレームワーク** | net10.0 |
| **ビルドツール** | MSBuild / .NET SDK 10.0 |
| **ログ** | Microsoft.Extensions.Logging.Abstractions（LoggerMessage ソース生成） |
| **テスト** | xUnit v3 / Avalonia.Headless.XUnit |
| **依存プロジェクト** | なし（Avalonia.Controls.ColorPicker の ColorView を利用） |

## 3. アーキテクチャ

### 3.1 レイヤー構造

```text
公開コントロール（ColorComboBoxControl: ColorComboBoxControl.axaml.cs）
  → 生成（ColorComboBoxFlyoutFactory）
  → 検証（ColorValidator）
  → 対象（ColorEntry / ColorSortMode / ColorString / ColorTextFormat）
  → コンバーター（StringToBrushConverter）
```

### 3.2 依存方向

- 依存は**一方向の階層構造**（XAML → コントロール → 生成/検証 → 対象 / コンバーター）。
- 公開 API（StyledProperty・公開プロパティ）は `ColorComboBoxControl.axaml.cs` に集約する。
- 関心事ごとに専用クラスへ委譲し、単一責任（SRP）と疎結合を維持する:
  - Flyout 構築: `ColorComboBoxFlyoutFactory`（Simple Factory）
  - 色の妥当性検証: `ColorValidator`
- 状態管理はすべて StyledProperty（Avalonia プロパティシステム）に集約する。
- 対象ロジック（`ColorString` / `ColorEntry`）は UI 非依存（Avalonia.Media のみ）。
- 上位APへは双方向バインド（StyledProperty）と PropertyChanged のみで連携。

## 4. 例外処理ポリシー

### 4.1 基本方針

- ライブラリは例外を原則伝播し、上位 AP（app）で処理する。
- UI レイヤー（app）ではユーザー操作起因の例外を各ハンドラで catch し、UI の応答性を維持する。
- ログは LoggerMessage ソース生成（`EsUtil.Ui.AvaloniaUI/ColorComboBox/src/Logging/ColorComboBoxLogMessage.cs`）で出力する。 高頻度イベントは Debug / Trace、障害は Error で記録する。

### 4.2 本プロジェクト固有の例外処理

- 無効な色入力は例外にせず、バリデーション状態（`HasValidationError` / `ValidationErrorMessage`）で通知し、フォーカス喪失時に最終有効値へ復帰する。
- `StringToBrushConverter.ConvertBack` は未サポートのため `NotSupportedException` を投げる（双方向変換は行わない）。
- 解釈不能な色文字列は `Brushes.Transparent`（スウォッチ背景）として安全に扱う。

## 5. コンポーネント図

```mermaid
flowchart TB
    subgraph Host["上位AP（消費側）"]
        AP["上位AP<br/>SelectedColor 双方向バインド"]
    end
    subgraph CPC["EsUtil.Ui.AvaloniaUI.ColorComboBox プロジェクト"]
        direction TB
        Ctl["ColorComboBoxControl<br/>public sealed partial : UserControl<br/>（公開 API ＋ イベント処理 ＋ バリデーション状態）"]
        Xaml["ColorComboBoxControl.axaml<br/>外周の枠 + スウォッチ + 入力欄 + ▼ + …"]
        Fac["ColorComboBoxFlyoutFactory<br/>Simple Factory（カラーピッカー構築）"]
        Impl["内部実装<br/>ColorValidator / ColorEntry / ColorString / ColorTextFormat<br/>StringToBrushConverter / ColorSortMode"]
    end
    AP -->|"SelectedColor (TwoWay)"| Ctl
    AP -->|"ColorSortMode / HighlightBorderOnError"| Ctl
    Ctl -->|"HasValidationError / ValidationErrorMessage"| AP
    Xaml --> Ctl
    Ctl --> Fac
    Ctl --> Impl
    Fac --> Impl
```

## 6. コンポーネント一覧

### 6.1 ColorComboBoxControl

- **分類**: 公開コントロール（`public sealed partial class : UserControl`）
- **責務**:
  - `SelectedColor`（色名/16進コード）の双方向バインド対応の保持（公開 API は ColorComboBoxControl.axaml.cs に集約）
  - 入力バリデーション状態の管理（`UpdateValidationStatus`。 検証自体は `ColorValidator` に委譲）
  - 一覧のポップアップ（▼）の構築と並び順切替（`ColorSortMode`）
  - 入力欄の文字と保存値の同期（`OnTextValueChanged`。 プログラムからの `Text` 変更でも発火させるため、`Text` プロパティの変更を購読する）
  - 無効入力の最終有効値復帰（`OnTextLostFocus`）
  - カラーピッカー Flyout の表示（`OnColorPickerButtonClick` → `ColorComboBoxFlyoutFactory.Create`）
  - 入力欄の余白調整（`ApplyTextLayout`。 左へスウォッチ分を確保する）
  - スウォッチと入力欄のツールチップ（色名+不透明度 / `#AARRGGBB` の 2 行）
  - バリデーション失敗時の枠線強調（`UpdateBorderValidationVisual`）
- **公開API**: `SelectedColor` / `ColorSortMode` / `TextFormat` / `HighlightBorderOnError` / `HasValidationError` / `ValidationErrorMessage` / `TextMinWidth` / `TextMaxWidth` / `TextWidth` / `TextPadding` / `TextMargin` / `Log`（StyledProperty も同ファイルに定義）
- **構成**: 公開 API（StyledProperty・公開プロパティ）とイベント処理・バリデーション状態を保持し、関心事ごとに専用クラスへ委譲（単一責任・疎結合）
- **内部メソッド（internal）**: `UpdateValidationStatus`（テストからの検証用に公開）
- **依存**: ColorComboBoxFlyoutFactory / ColorValidator / ColorEntry / ColorSortMode / ColorString / ColorTextFormat

### 6.2 ColorComboBoxControl.axaml

- **分類**: UI レイアウト（XAML）
- **責務**:
  - 全体を覆う 1 つの枠（`PART_Frame`）の配置（検証エラーの強調もここへ付ける）
  - 色スウォッチ（`PART_Swatch`）を入力欄の内側へ配置
  - 入力欄（`PART_Text`）の配置
  - 区切りの垂直線（枠より薄い 1 ピクセル）と区画（`PART_ListButton` / `PART_PickerButton`）の配置
  - 一覧の行とスウォッチのブラシ（`StringToBrushConverter`）のリソース登録
- **依存**: ColorComboBoxControl / StringToBrushConverter

### 6.3 ColorEntry

- **分類**: 内部レコード（色名 + 色を表す文字列の組。 ドロップダウンの選択肢データ）
- **可視性**: `internal sealed record`（公開 API ではなく内部型。 外部から直接扱わない）
- **責務**:
  - 色の英語名・色名（`Color.TryParse` で解釈可能な文字列）の保持
  - 全色一覧の静的提供（`All` / `AllByHue`）
  - 色相順ソートキーの計算（`GetSortKey`）
- **特徴**: 色一覧は `ColorString.ColorNames`（`ColorString` が構築済みの辞書）を再利用する（リフレクション走査の重複排除・単一所有）。 `Value` パラメータには現在の実装では色名（= `Colors` のプロパティ名）が保持される
- **依存**: ColorString / Avalonia.Media

### 6.4 ColorSortMode

- **分類**: 公開 enum
- **責務**: ドロップダウンの並び順の指定（`ByName` / `ByHue`）
- **依存**: なし

### 6.5 StringToBrushConverter

- **分類**: 公開 IValueConverter（`public sealed class`）
- **責務**: 色名/16進コード → `IBrush`（スウォッチ背景）
- **特徴**: 解釈不能な値は `Brushes.Transparent`。 `ConvertBack` は `NotSupportedException`。 解析済み色のブラシはカラー選択で再利用され、容量管理は `ColorString.AddWithCapacityLimit` に委譲（単一所有）
- **依存**: Avalonia.Media / Avalonia.Data.Converters / ColorString

### 6.6 ColorString

- **分類**: 公開静的ユーティリティ（`public static class`）
- **責務**: 色文字列（色名 / #RRGGBB / #AARRGGBB / +% 形式）の解析・整形・正規化
- **特徴**:
  - 保存値は `ToArgbHex` で `#AARRGGBB`（アルファ先頭 8 桁）へ正規化
  - 画面表示は `ToDisplayText` / `Format` で `{色}+{不透明度%}` 形式へ整形（不透明度 100% は省略）
  - `TryParse` で両形式を受け付ける（色と不透明度の合成）
  - 色 → 色名 の逆引き辞書（ARGB 完全一致 / RGB 一致）を保持し、色名の検索に利用する
  - 容量管理は `AddWithCapacityLimit` ヘルパーに一元化（単一所有。 `StringToBrushConverter` のブラシキャッシュも委譲）
  - `Colors` のリフレクション列挙を単一所有し、`ColorEntry` が再利用する
- **依存**: Avalonia.Media

### 6.7 入力欄の表記（`TextFormat`）

- **分類**: 公開プロパティ（StyledProperty。 値はコアの `ColorTextFormat`）
- **責務**: 入力欄へ出す文字列の形を決める（色名 + 不透明度 / `#AARRGGBB`）
- **特徴**: 変換は `ColorString.ToText`（コア）が単一所有する。 保存値は表記に依らず常に `#AARRGGBB`（入力欄は編集できるため、表記と異なる形の入力も受理する）
- **依存**: EsUtil.Platform.Core.Palette（`ColorTextFormat` / `ColorString`）

### 6.8 ColorValidator

- **分類**: 内部静的ユーティリティ（`internal static class`）
- **責務**: 色文字列（色名 / #RRGGBB / #AARRGGBB / +% 形式）の妥当性検証（`IsValid`）
- **特徴**: 検証は `ColorString.TryParse` に一元委譲（解析ロジックの二重所有を排除）。 UI 非依存のため他プロジェクトでも流用可能
- **依存**: ColorString

### 6.9 区画（スウォッチ・入力欄・▼・…）の構成

- **分類**: XAML のレイアウト
- **責務**: 4 つの区画を隙間なく並べ、1 つの入力欄に見せる
- **特徴**: 枠線は外周の 1 つだけが持ち、区画のボタンは枠も背景も持たない（`Button.segment`）。 区切りは枠より薄い 1 ピクセルの垂直線で表す（上下に 5 ピクセルの余白を取る）。 行は `Auto` にして入力欄の高さへ揃える（行を伸ばすとスウォッチだけが下へずれる）
- **依存**: ColorComboBoxControl.axaml.cs

### 6.10 ColorComboBoxFlyoutFactory

- **分類**: 内部静的ファクトリ（`internal static class`。 Simple Factory パターン）
- **責務**: カラーピッカー（ColorView）を格納した Flyout の構築（`Create`）と OK / キャンセルイベントの配線
- **特徴**: 初期色は `SelectedColor` を `ColorString.TryParse` で解析（失敗時は白）。 OK は `ColorString.ToArgbHex` で `#AARRGGBB` へ変換し、コールバック（`Action<string>`）経由で `SelectedColor` へ反映。 OK / キャンセルボタンの共通外観は生成ヘルパー（`CreateActionButton`）に集約し、重複を排除
- **依存**: ColorString / ColorPickerLog / Avalonia.Controls.ColorPicker（ColorView）

## 7. 依存ルール

- 依存方向は**一方向の階層構造**（XAML → コントロール → 生成/検証 → 対象/コンバーター）。
- 公開 API（StyledProperty・公開プロパティ）は `ColorComboBoxControl.axaml.cs` に集約する。
- 関心事ごとの専用クラス（Factory / Validator）は internal とし、コントロールからのみ参照する（外部へは公開しない）。
- 依存は 1 方向のみ（コントロール → Factory/Validator → ColorString）。 双方向依存・循環参照は持たない。
- 状態管理はすべて StyledProperty（Avalonia プロパティシステム）に集約し、private フィールドは補助（`_lastValidColor`）のみ。
- 対象ロジック（`ColorString` / `ColorEntry`）は UI 非依存（Avalonia.Media のみ）。
- 上位APへは双方向バインド（StyledProperty）と PropertyChanged のみで連携。

## 8. バリデーション状態遷移

```mermaid
stateDiagram-v2
    [*] --> Valid: SelectedColor 有効
    Valid --> Empty: 空文字
    Valid --> Invalid: 無効な色コード
    Invalid --> Valid: 有効な色コード
    Invalid --> Valid: フォーカス喪失（最終有効値へ復帰）
    Empty --> Valid: 有効な色コード
    Empty --> Invalid: 無効な色コード
    Valid --> [*]
```

- `HasValidationError` / `ValidationErrorMessage` は `SelectedColor` の変更に応じて更新されます。

## 9. ライフサイクル

```mermaid
sequenceDiagram
    participant VM as ViewModel
    participant Ctl as ColorComboBoxControl
    participant Cmb as ComboBox
    participant Fly as Flyout(ColorView)

    VM->>Ctl: SelectedColor 設定（TwoWay）
    Ctl->>Ctl: バリデーション判定・最終有効値更新
    Ctl-->>VM: HasValidationError / ValidationErrorMessage

    VM->>Ctl: ColorSortMode 変更
    Ctl->>Cmb: ItemsSource 切替（AllByHue / All）

    Ctl->>Cmb: テキスト入力・ドロップダウン選択
    Cmb-->>Ctl: SelectionChanged / LostFocus
    Ctl->>Ctl: バリデーション・復帰処理

    VM->>Ctl: カラーピッカー起動
    Ctl->>Fly: ShowAt（OK / キャンセル）
    Fly-->>Ctl: OK → SelectedColor 反映
    Ctl-->>VM: SelectedColor 更新（TwoWay）
```

## 10. ログ

ログ定義は `EsUtil.Ui.AvaloniaUI/ColorComboBox/src/Logging/ColorComboBoxLogMessage.cs`（internal・LoggerMessage ソース生成）に集約し、`ILogger` の拡張メソッドとして定義する。
各メソッドは `this ILogger` 第一引数で定義し、呼び出し側では logger 引数を省略する（例: `_log.LogSelectedColorChanged(old, new)`）。
ログ出力先の `ILogger` は公開プロパティ `Log` で注入する（未設定時は `NullLogger` のため出力なし）。
出力先・レベル制御はホストアプリケーションが所有する（本クラスは定義のみ）。

### 10.1 LogLevel 設定ポリシー

トレースログは LogLevel.Trace に限定せず、「重要度 × 発生頻度」でレベルを使い分ける。

| LogLevel | 対象 | 本番での意図 |
| --- | --- | --- |
| `Error` | 例外・失敗（処理継続不可） | 常時出力し、障害を確実に残す |
| `Warning` | 異常だがフォールバックで継続 | 常時出力し、異常発生の痕跡を残す |
| `Information` | ユーザー操作の主要イベント（低頻度） | 常時出力し、操作手順の再現を可能にする |
| `Debug` | 操作イベントの詳細（色選択 / Flyout / 設定変更） | 障害時のみ有効化して解析 |
| `Trace` | 高頻度・極詳細（フォーカス喪失） | 特別な解析時のみ有効化 |

判断基準:

1. 例外・警告は常時残す（`Error` / `Warning`）
2. 操作の起点イベント（低頻度）は `Information`
3. マウス・キー等の操作イベント（中頻度）は `Debug`
4. 高頻度・極詳細は `Trace`
5. 設定変更（変更前後）は `Debug`

### 10.2 ログ定義一覧

| レベル | メソッド | 出力内容 |
| --- | --- | --- |
| `Debug` | `ColorComboBoxLogMessage.LogSelectedColorChanged` | 選択色変更（変更前後） |
| `Debug` | `ColorComboBoxLogMessage.LogOpenFlyout` | カラーピッカー Flyout を開く（現在の選択色） |
| `Debug` | `ColorComboBoxLogMessage.LogFlyoutOk` | Flyout の OK で色確定 |
| `Debug` | `ColorComboBoxLogMessage.LogFlyoutCancel` | Flyout のキャンセル |
| `Trace` | `ColorComboBoxLogMessage.LogLostFocus` | フォーカス喪失（色と有効性） |

## 11. テスト

`tests/` プロジェクト（xUnit v3 + Avalonia.Headless）で、行・ブランチカバレッジ 100% を達成しています。

- 公開プロパティ・バリデーション・並び順切替
- イベントハンドラ（SelectionChanged / LostFocus / カラーピッカー）
- コンバーター・ColorEntry の全分岐
- 防御分岐（内部メソッド直接テスト）
