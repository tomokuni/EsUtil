# EsUtil.Ui.AvaloniaUI.ColorPicker API リファレンス

## 目次 <!-- omit from toc -->

- [1. 文書概要](#1-文書概要)
- [2. 目的](#2-目的)
- [3. 外部利用条件](#3-外部利用条件)
  - [3.1 プロジェクト参照](#31-プロジェクト参照)
  - [3.2 必要な名前空間](#32-必要な名前空間)
  - [3.3 Visual Treeへの配置](#33-visual-treeへの配置)
- [4. 基本的な使用方法](#4-基本的な使用方法)
  - [4.1 コードからの生成](#41-コードからの生成)
  - [4.2 選択色の取得・監視](#42-選択色の取得監視)
  - [4.3 バリデーション状態の利用](#43-バリデーション状態の利用)
- [5. 公開API](#5-公開api)
  - [5.1 プロパティ](#51-プロパティ)
    - [5.1.1 選択状態](#511-選択状態)
    - [5.1.2 表示・動作設定](#512-表示動作設定)
    - [5.1.3 バリデーション状態（読み取り専用）](#513-バリデーション状態読み取り専用)
    - [5.1.4 ComboBox レイアウト](#514-combobox-レイアウト)
  - [5.2 メソッド](#52-メソッド)
  - [5.3 イベント](#53-イベント)
  - [5.4 型](#54-型)
    - [5.4.1 ColorPickerControl（`ColorPicker.axaml.cs`）](#541-colorpickercontrolcolorpickeraxamlcs)
    - [5.4.2 ColorSortMode（`ColorSortMode.cs`）](#542-colorsortmodecolorsortmodecs)
    - [5.4.3 StringToBrushConverter](#543-stringtobrushconverter)
    - [5.4.4 ColorDisplayConverter](#544-colordisplayconverter)
    - [5.4.5 ColorString（`ColorString.cs`）](#545-colorstringcolorstringcs)
- [6. 入力動作](#6-入力動作)
  - [6.1 テキスト入力](#61-テキスト入力)
  - [6.2 ドロップダウン選択](#62-ドロップダウン選択)
  - [6.3 無効な入力の扱い](#63-無効な入力の扱い)
  - [6.4 カラーピッカー](#64-カラーピッカー)
  - [6.5 ColorView テーマ（ホストが取り込む）](#65-colorview-テーマホストが取り込む)
- [7. 制限事項](#7-制限事項)

---

## 1. 文書概要

| 項目 | 内容 |
| --- | --- |
| 対象 | `ColorPickerControl` |
| プロジェクト | `EsUtil.Ui.AvaloniaUI/ColorPicker/src/EsUtil.Ui.AvaloniaUI.ColorPicker.csproj` |
| 名前空間 | `EsUtil.Ui.AvaloniaUI.ColorPicker` |
| 対象フレームワーク | `net10.0` |
| UIフレームワーク | Avalonia 12.1 |
| 主要基底クラス | `Avalonia.Controls.UserControl` |
| 主な用途 | 色名/16進入力 + ドロップダウン + カラーピッカーを統合した色選択コントロール |

## 2. 目的

`ColorPickerControl` は、色の選択・入力・プレビュー・バリデーションを自己完結する独立した Avalonia コントロールです。

次の機能を提供します。

- 色名（英名）または16進カラーコードのテキスト入力
- ドロップダウンによる色一覧選択（色相順 / 色名順の切替）
- 選択中の色のスウォッチ表示（テキスト領域左端）
- カラーピッカー（ColorView）の Flyout 表示
- 入力バリデーションと最終有効値への自動復帰

## 3. 外部利用条件

### 3.1 プロジェクト参照

```xml
<ProjectReference Include="..\EsUtil.Ui.AvaloniaUI.ColorPicker\src\EsUtil.Ui.AvaloniaUI.ColorPicker.csproj" />
```

### 3.2 必要な名前空間

```csharp
using EsUtil.Ui.AvaloniaUI.ColorPicker;
```

### 3.3 Visual Treeへの配置

`ColorPickerControl` は `UserControl` です。

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:cp="clr-namespace:EsUtil.Ui.AvaloniaUI.ColorPicker;assembly=EsUtil.Ui.AvaloniaUI.ColorPicker">
    <StackPanel>
        <cp:ColorPickerControl x:Name="BorderColorPicker"
                               SelectedColor="{Binding BorderColor, Mode=TwoWay}"
                               Width="300" />
    </StackPanel>
</Window>
```

## 4. 基本的な使用方法

### 4.1 コードからの生成

```csharp
var colorPicker = new ColorPickerControl
{
    SelectedColor = "#FF0000",
    ColorSortMode = ColorSortMode.ByHue,
    HighlightBorderOnError = true,
    Width = 300,
    ComboBoxMinWidth = 200,
};

parentPanel.Children.Add(colorPicker);
```

### 4.2 選択色の取得・監視

`SelectedColor` は双方向バインド対応です。 ViewModel のプロパティと直接バインドできます。

```csharp
// モデル保存用（常に #AARRGGBB 形式へ正規化済み）
var color = colorPicker.SelectedColor;
```

### 4.3 バリデーション状態の利用

```csharp
if (colorPicker.HasValidationError)
{
    var reason = colorPicker.ValidationErrorMessage; // "無効な色コードです。"
}
```

## 5. 公開API

公開 API は「プロパティ / メソッド / イベント / 型」の 4 分類で構成されます。
公開型は `ColorPickerControl` / `ColorSortMode` / `StringToBrushConverter` / `ColorDisplayConverter` / `ColorString` です。
`ColorEntry` と `ColorPickerControl` の非公開実装（private / internal メンバー）は非公開です。

### 5.1 プロパティ

公開プロパティ（StyledProperty）は以下の通りです。 StyledProperty は同ファイルに定義されます（Avalonia のバインディング・スタイル設定に必要なため public）。

#### 5.1.1 選択状態

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `SelectedColor` | `string` | `"White"` | 現在選択されている色。 **保存値は `#AARRGGBB`（アルファ先頭8桁）形式**（双方向バインド）。 色名 / `#RRGGBB` / `#AARRGGBB` / `{色}+{不透明度%}` のいずれを設定しても `#AARRGGBB` へ正規化される |

#### 5.1.2 表示・動作設定

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `ColorSortMode` | `ColorSortMode` | `ByHue` | ドロップダウンの色の並び順（`ByHue` / `ByName`） |
| `HighlightBorderOnError` | `bool` | `false` | バリデーション失敗時に ComboBox の枠線を赤くするか |
| `Log` | `ILogger` | `NullLogger` | 内部ロガー（null 設定時は `NullLogger` へフォールバック）。 診断ログの出力先 |
| `LogPrefix` | `string?` | `null` | ログの接頭辞（ロガーと一緒に設定する。 null・空白で既定値 `[ColorPicker]` へ戻す） |

#### 5.1.3 バリデーション状態（読み取り専用）

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `HasValidationError` | `bool` | 現在の `SelectedColor` が無効な場合 `true` |
| `ValidationErrorMessage` | `string?` | 無効な場合の理由。 有効時は `null` |

#### 5.1.4 ComboBox レイアウト

単位はすべて DIP 座標系（Avalonia 論理座標）です。

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `ComboBoxMinWidth` | `double` | `120` | ComboBox の最小幅（DIP 座標系） |
| `ComboBoxMaxWidth` | `double` | `Infinity` | ComboBox の最大幅（DIP 座標系） |
| `ComboBoxWidth` | `double` | `NaN` | ComboBox の明示的な幅（DIP 座標系。 NaN は自動） |
| `ComboBoxPadding` | `Thickness?` | `null` | 内部パディング（DIP 座標系。 null はテーマ標準。 スウォッチ分が自動加算される） |
| `ComboBoxMargin` | `Thickness?` | `null` | マージン（DIP 座標系。 null はテーマ標準） |

### 5.2 メソッド

公開メソッドは定義されていません。 コントロールは StyledProperty（双方向バインド）とイベント通知のみでホストと連携します。

### 5.3 イベント

| イベント | 説明 |
| --- | --- |
| `SelectedColor` の PropertyChanged | `AvaloniaPropertyChangedEventArgs` で選択色の変更を通知（双方向バインドで利用） |

コントロールは公開イベントを独自に定義せず、StyledProperty の変更通知（`PropertyChanged`）と双方向バインドでホストと連携します。

### 5.4 型

#### 5.4.1 ColorPickerControl（`ColorPicker.axaml.cs`）

公開コントロール（`public sealed partial class : UserControl`）。
公開 API は「5.1 プロパティ」「5.2 メソッド」「5.3 イベント」に記載の通りです。

#### 5.4.2 ColorSortMode（`ColorSortMode.cs`）

| 値 | 説明 |
| --- | --- |
| `ByName` | 色名（英名）のアルファベット順 |
| `ByHue` | 色相順（無彩色は輝度昇順で先頭） |

#### 5.4.3 StringToBrushConverter

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `Convert(object?, Type, object?, CultureInfo)` | `object?` | 色名/16進コード（`{色}+{不透明度%}` 形式も可） → `IBrush`。 不透明度はアルファへ合成。 解釈不能な場合は `Brushes.Transparent` |
| `ConvertBack(object?, Type, object?, CultureInfo)` | `object?` | 未サポート。 常に `NotSupportedException` |

#### 5.4.4 ColorDisplayConverter

保存値（`#AARRGGBB`）と画面表示テキスト（`{色}+{不透明度%}`）を相互変換する双方向コンバーター。
ComboBox の `Text` プロパティとのバインドに使用する（`SelectedColor` のバインドに内蔵）。

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `Convert(object?, Type, object?, CultureInfo)` | `object?` | 保存値（例: `#80FF0000`）→ 表示テキスト（例: `Red+50%`）。 不透明度 100% は `{色}` のみ。 解釈不能な場合は入力をそのまま返す |
| `ConvertBack(object?, Type, object?, CultureInfo)` | `object?` | 入力テキスト（例: `Red+50%`）→ 保存値（例: `#80FF0000`）。 解釈不能な場合は入力をそのまま返す |

#### 5.4.5 ColorString（`ColorString.cs`）

色と不透明度を統合した文字列形式（`{色}+{不透明度%}`）の解析・整形・正規化を提供する静的ユーティリティ。

- **形式**: `{色}` は色名（Avalonia `Colors` の 140 色）が定義されていれば色名、未定義なら `#RRGGBB`。 不透明度はパーセント（0〜100%）で表し、100% の場合は省略
- **保存値の正規化**: `ToArgbHex` で `#AARRGGBB`（アルファ先頭・常時アルファ含む）へ統一
- **表示**: `ToDisplayText` で `{色}+{不透明度%}` 形式へ整形（画面表示用）

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `IsDefinedColorName(string)` | `bool` | 色名が `Colors` に定義されているか（大文字小文字無視） |
| `TryParse(string?, out Color, out double)` | `bool` | 色文字列を解析して色と不透明度を取得（`+{n}%` はアルファへ合成） |
| `Format(Color, double)` | `string` | 色と不透明度を `{色}+{不透明度%}` へ整形 |
| `ToDisplayText(string?)` | `string` | 保存値 → 表示テキストへ変換 |
| `ToArgbHex(string?)` | `string` | 任意の色文字列 → `#AARRGGBB` へ正規化 |
| `ToArgbHex(Color)` | `string` | 色 → `#AARRGGBB` へ変換（アルファ含む） |

**解析・整形の規則**:

- 不透明度の合成は **四捨五入**（例: `Red+50%` → アルファ `128`（`#80`）。 `Red+33%` → `84`）。 浮動小数誤差を避けるため `pct × 255 ÷ 100` の整数演算で算出する
- 色名表示は以下の優先順で決定する:  
  1. アルファも含めて完全一致する色名  
  2. RGB のみ一致する色名（同 RGB で複数ある場合は不透明（A=255）の色名を優先。 例: 半透明の白は `White+50%` と表示され `Transparent` は使われない）  
  3. 一致する色名が無い場合は `#RRGGBB`

## 6. 入力動作

### 6.1 テキスト入力

- 色名（英名）・16進カラーコード（`#RRGGBB` / `#AARRGGBB`）・`{色}+{不透明度%}`（例: `Red+50%`）を直接入力できます。
- 入力値は ComboBox の双方向バインディング（`ColorDisplayConverter`）により `#AARRGGBB` へ正規化されます。
- 画面表示は `{色}+{不透明度%}` 形式（色名が `Colors` にあれば色名、なければ `#RRGGBB`）。
- `+{n}%` 形式を含まない入力（`#AARRGGBB` 等）はアルファ成分を不透明度として解釈します（例: `#80FF0000` → `Red+50%`）。
- 不透明度は `0〜100%` の整数へ丸めて表示し、100% の場合は省略します。

### 6.2 ドロップダウン選択

- ドロップダウンの各項目は「色スウォッチ + 色名（英語名）」で表示されます。
- 選択後はテキストの全選択状態が自動解除されます。

### 6.3 無効な入力の扱い

- フォーカス喪失時、入力値が無効な場合は**最終有効値へ自動復帰**します。
- `HighlightBorderOnError` が `true` の場合、入力中も枠線が赤く表示されます。

### 6.4 カラーピッカー

- 右端の「…」ボタンで Flyout 形式のカラーピッカー（`ColorView`）が開きます。
- 「OK」で選択色を `#AARRGGBB`（アルファ先頭8桁）の保存値として反映し、「キャンセル」で破棄します。
- 初期表示色は `SelectedColor` をパースした色（失敗時は白）です。
- 3 つ目のタブ（Hex 入力）の表示形式は `#AARRGGBB`（アルファ先頭）で、Avalonia 標準の `Color.Parse` と整合します。
- 不透明度は ColorView のアルファスライダーで設定でき、保存値のアルファ成分として統合されます（画面表示では `+{不透明度%}`）。

### 6.5 ColorView テーマ（ホストが取り込む）

**ホストは `ColorView`（カラーピッカー）のテーマを `Application.Styles` へ `StyleInclude` で取り込んでください**。

```xml
<StyleInclude Source="avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml" />
```

- Flyout はオーバーレイ レイヤーへ表示されるため、コントロールのローカル Styles では ColorView へ伝播しません。
- 取り込みを忘れるとカラーピッカーがテーマ未適用（既定の見た目）で表示されます（例外にはなりません）。
- 本コントロールは実行時の XAML 解決を行いません（トリミング / Native AOT では失敗するため、自動適用はしません）。

## 7. 制限事項

- `ColorView` を Flyout で表示するため、ヘッドレス環境（テスト）ではポップアップ表示ができない場合があります。
- 色一覧は `Avalonia.Media.Colors` の静的プロパティをリフレクションで取得して構築します（トリマー対策として `DynamicDependency` を付与済み）。
