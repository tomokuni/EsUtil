# EsUtil.Ui.AvaloniaUI.ColorSelector API リファレンス

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
    - [5.1.4 入力欄のサイズと文字サイズ](#514-入力欄のサイズと文字サイズ)
  - [5.2 メソッド](#52-メソッド)
  - [5.3 イベント](#53-イベント)
  - [5.4 型](#54-型)
    - [5.4.1 ColorSelectorControl（`ColorSelectorControl.axaml.cs`）](#541-colorselectorcontrolcolorselectorcontrolaxamlcs)
    - [5.4.2 ColorSortMode（`ColorSortMode.cs`）](#542-colorsortmodecolorsortmodecs)
    - [5.4.3 StringToBrushConverter](#543-stringtobrushconverter)
    - [5.4.4 ColorTextFormat（`ColorTextFormat.cs`・コア）](#544-colortextformatcolortextformatcsコア)
    - [5.4.5 ColorString（`ColorString.cs`）](#545-colorstringcolorstringcs)
    - [5.4.6 ColorSelectorCustomTab・ColorSelectorCustomColor・ColorSelectorTabLayout](#546-colorselectorcustomtabcolorselectorcustomcolorcolorselectortablayout)
- [6. 入力動作](#6-入力動作)
  - [6.1 テキスト入力](#61-テキスト入力)
  - [6.2 一覧（タブ）の選択](#62-一覧タブの選択)
  - [6.3 無効な入力の扱い](#63-無効な入力の扱い)
  - [6.4 カラーピッカー](#64-カラーピッカー)
  - [6.5 ColorView テーマ（ホストが取り込む）](#65-colorview-テーマホストが取り込む)
- [7. 制限事項](#7-制限事項)

---

## 1. 文書概要

| 項目 | 内容 |
| --- | --- |
| 対象 | `ColorSelectorControl` |
| プロジェクト | `EsUtil.Ui.AvaloniaUI/ColorSelector/src/EsUtil.Ui.AvaloniaUI.ColorSelector.csproj` |
| 名前空間 | `EsUtil.Ui.AvaloniaUI.ColorSelector` |
| 対象フレームワーク | `net10.0-windows10.0.17763.0` |
| UIフレームワーク | Avalonia 12.1 |
| 主要基底クラス | `Avalonia.Controls.UserControl` |
| 主な用途 | 色名/16進入力 + ドロップダウン + カラーピッカーを統合した色選択コントロール |

## 2. 目的

`ColorSelectorControl` は、色の選択・入力・プレビュー・バリデーションを自己完結する独立した Avalonia コントロールです。

次の機能を提供します。

- 色名（英名）または16進カラーコードのテキスト入力
- ドロップダウンによる色一覧選択（色相順 / 色名順の切替）
- 入力欄の左に現在色の四角を出す（「■ + 色名」で表示）
- カラーピッカー（ColorView）を一覧のタブの中で表示
- 入力バリデーションと最終有効値への自動復帰

## 3. 外部利用条件

### 3.1 プロジェクト参照

```xml
<ProjectReference Include="..\EsUtil.Ui.AvaloniaUI.ColorSelector\src\EsUtil.Ui.AvaloniaUI.ColorSelector.csproj" />
```

### 3.2 必要な名前空間

```csharp
using EsUtil.Ui.AvaloniaUI.ColorSelector;
```

### 3.3 Visual Treeへの配置

`ColorSelectorControl` は `UserControl` です。

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:cp="clr-namespace:EsUtil.Ui.AvaloniaUI.ColorSelector;assembly=EsUtil.Ui.AvaloniaUI.ColorSelector">
    <StackPanel>
        <cp:ColorSelectorControl x:Name="BorderColorPicker"
                               SelectedColor="{Binding BorderColor, Mode=TwoWay}"
                               Width="300" />
    </StackPanel>
</Window>
```

## 4. 基本的な使用方法

### 4.1 コードからの生成

```csharp
var colorPicker = new ColorSelectorControl
{
    SelectedColor = "#FF0000",
    ColorSortMode = ColorSortMode.ByHue,
    HighlightBorderOnError = true,
    Width = 300,
    TextFormat = ColorTextFormat.NameWithAlpha,
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
公開型は `ColorSelectorControl` / `ColorSortMode` / `ColorTextFormat` / `StringToBrushConverter` / `ColorString` です。
`ColorEntry` と `ColorSelectorControl` の非公開実装（private / internal メンバー）は非公開です。

### 5.1 プロパティ

公開プロパティ（StyledProperty）は以下の通りです。 StyledProperty は同ファイルに定義されます（Avalonia のバインディング・スタイル設定に必要なため public）。

#### 5.1.1 選択状態

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `SelectedColor` | `string` | `"White"` | 現在選択されている色。 **保存値は `#AARRGGBB`（アルファ先頭8桁）形式**（双方向バインド）。 色名 / `#RRGGBB` / `#AARRGGBB` / `{色}+{不透明度%}` のいずれを設定しても `#AARRGGBB` へ正規化される |
| `SelectedThemeColor` | `string?` | `null` | 選択中の**テーマの色の識別子**（例: `背景`）。 値を持つ間は `SelectedColor` をそのときのテーマの色へ解決し直す（明暗・アクセントの変更に追随する）。 色名・カラーコードを選ぶと `null` に戻る |
| `ThemeColorKeys` | `IReadOnlyList<string>`（static・get） | UI ごとの一覧 | `SelectedThemeColor` へ設定できる識別子の一覧（並びは一覧と同じ）。 設定画面の選択肢と保存値の検証に使う |
| `TryResolveThemeColor(string key, out string hex)` | method（public） | - | 識別子（`背景` など）から、そのときのテーマの色（`#AARRGGBB`）を解決する。 その UI に無い色は false（一覧と同じ解決を使う） |

#### 5.1.2 表示・動作設定

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| 一覧のタブ | （UI の要素） | テーマカラー / 色名 | 一覧（展開ボタンで開く）の上部にタブを置き、分類を切り替える（`ShowThemeColorTab` / `ShowNamedColorTab` で取捨、`CustomTabs` で追加、1 つだけなら `ShowTabTitles` で題名を隠せる）。 テーマの色は数が多いため、色名と同じ一覧へ混ぜない。 **一覧を開いても枠（入力欄）の幅は変えない**（一覧だけを**枠・タブの行・内容のうち最も広い幅**へ広げる。 一覧はその幅いっぱいに置くため、**スクロール バーは右端**に出る。 タブが多いときはタブの幅を詰めて収める）。 前回選んだタブから開く |
| `ShowThemeColors` | `bool` | `true`（取得または設定） | 一覧の先頭に **UI のテーマの色をすべて**並べる（共通の 20 色を「テーマ: 背景 / レイヤー / カード / コントロールの背景 / コントロールの枠線 / 区切り線 / 文字 / 副次テキスト / 無効の文字 / 無効の背景 / アクセント / アクセント（淡）/ アクセント（濃）/ アクセント文字 / アクセント上の文字 / 選択の背景 / ホバーの背景 / 押下の背景 / フォーカス / エラー」の順に並べ、その UI にしか無い色（三次テキスト・成功・注意・スクロール バーなど）を後ろへ足す）。 値は一覧を開くたびにテーマから解決する（明暗の切替・アクセント色の変更に追随） |
| `ShowCustomTab` | `bool` | `true` | 一覧の「カスタム」タブを表示するか（`CustomTabs` が空のときは表示しない） |
| `ShowThemeColorTab` | `bool` | `true` | 一覧の「テーマカラー」タブを表示するか（中身が無いときは表示しない） |
| `ShowNamedColorTab` | `bool` | `true` | 一覧の「色名」タブを表示するか |
| `ShowPickerTab` | `bool` | `true` | 一覧の「ColorPicker」タブを表示するか（タブの中でカラーピッカーを直接表示する） |
| `ShowTabTitles` | `bool` | `true` | タブが **1 つだけ**のときに題名の行を出すか。 2 つ以上あるときは切り替えに必要なので常に出す |
| `CustomTabs` | `IReadOnlyList<ColorSelectorCustomTab>` | 空 | 一覧へ追加する「カスタム」タブ（題名 + 色名/色コードの並び + 並べ方）。 複数指定できる。 `Colors` が空のタブは表示しない。 **並べ方は一覧（`List`）と色の四角 6 列の格子（`Grid`）** |
| `TextFormat` | `ColorTextFormat` | `NameWithAlpha` | **入力欄**の表記（`NameWithAlpha` = 色名+不透明度 / `HexCode` = `#AARRGGBB`）。 一覧の項目は常に色名、ツールチップは常に 2 行（色名+不透明度 / カラーコード）を出す。 テーマの色を選んでいる間は入力欄が `テーマ: <識別子>` になる |
| `HighlightBorderOnError` | `bool` | `false` | バリデーション失敗時に外周の枠線を赤くするか |
| `Log` | `ILogger` | `NullLogger` | 内部ロガー（null 設定時は `NullLogger` へフォールバック）。 診断ログの出力先 |
| `LogPrefix` | `string?` | `null` | ログの接頭辞（ロガーと一緒に設定する。 null・空白で既定値 `[ColorSelector]` へ戻す） |

#### 5.1.3 バリデーション状態（読み取り専用）

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `HasValidationError` | `bool` | 現在の `SelectedColor` が無効な場合 `true` |
| `ValidationErrorMessage` | `string?` | 無効な場合の理由。 有効時は `null` |

#### 5.1.4 入力欄のサイズと文字サイズ

単位はすべて DIP 座標系（Avalonia 論理座標）です。 **項目と既定値は MewUI 版・WinUI 3 版と同じ**です。

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `TextFontSize` | `double` | `NaN` | 入力欄の文字サイズ（DIP 座標系。 NaN は親（テーマ）の値を継承） |

入力欄の左の余白は**現在色の四角の分だけ空けます**（文字が四角に重ならないようにする。 値は実装が持ちます）。 文字は上下の中央にそろえます。

### 5.2 メソッド

公開メソッドは定義されていません。 コントロールは StyledProperty（双方向バインド）とイベント通知のみでホストと連携します。

### 5.3 イベント

| イベント | 説明 |
| --- | --- |
| `SelectedColor` の PropertyChanged | `AvaloniaPropertyChangedEventArgs` で選択色の変更を通知（双方向バインドで利用） |
| `ColorApplied` | 一覧・カラーピッカーで色を確定したときに発生する（**同じ色を選び直したときも発生**。 `SelectedColor` は変わらないため変更通知では足りない用途向け）。 入力欄へ打ち込んだ場合は発生しない（確定はフォーカスを外したとき） |

`SelectedColor` の変更通知だけでは「同じ色を選び直した」ことを表せません（文字単位で色が混ざった範囲を一律にそろえる用途では確定の通知が要ります）。

### 5.4 型

#### 5.4.1 ColorSelectorControl（`ColorSelectorControl.axaml.cs`）

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

#### 5.4.4 ColorTextFormat（`ColorTextFormat.cs`・コア）

入力欄の表記です（`EsUtil.Platform.Core.Palette`）。

| 値 | 説明 |
| --- | --- |
| `NameWithAlpha` | 色名 + 不透明度（例: `Red` / `Orange+75%`）。 色名が無い色は 16 進で表す |
| `HexCode` | カラーコード（例: `#FFFF0000`）。 アルファが先頭（保存値と同じ形） |

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

#### 5.4.6 ColorSelectorCustomTab・ColorSelectorCustomColor・ColorSelectorTabLayout

**利用者が一覧へ足すタブ**（`CustomTabs` へ指定する）。

| 型 | メンバー | 説明 |
| --- | --- | --- |
| `ColorSelectorCustomTab` | `Title` / `Colors` / `Layout` | タブの題名（1 つだけ表示するときの見出しにも使う）/ 並べる色（並びが一覧の順序）/ 並べ方（既定は `List`） |
| `ColorSelectorCustomColor` | `Name` / `Value` | 一覧に表示する名前 / 選んだときに適用する色（色名または `#AARRGGBB`） |
| `ColorSelectorTabLayout` | `List` / `Grid` | 色名付きの一覧（スクロールする）/ 色の四角だけを **6 列**に並べる（名前はツールチップ。 スクロールしない） |

```csharp
control.CustomTabs =
[
    // 色名付きの一覧
    new ColorSelectorCustomTab("枠線", [
        new ColorSelectorCustomColor("枠線の赤", "#FFDC2626"),
    ]),

    // 色の四角を 6 列に並べる（16 色なら 3 行に収まり、スクロールせずに選べる）
    new ColorSelectorCustomTab("代表色", [
        new ColorSelectorCustomColor("黒", "#FF000000"),
        new ColorSelectorCustomColor("赤", "#FFDC2626"),
        // … 16 色まで
    ], ColorSelectorTabLayout.Grid),
];
```

- **色名は入力欄にも出ます**（選んだ色がカスタム タブの色と一致したとき。 不透明度があるときは `+{n}%` を足す）。

- `Colors` が空のタブは表示しません（設定の途中で空になっても壊れない）。
- タブは開くたびに作るため、`CustomTabs` の差し替えは次に開いた一覧から反映されます。
- 題名には `ShowTabTitles` が効きます（タブが 2 つ以上あるときは切り替えに必要なので常に表示）。

## 6. 入力動作

### 6.1 テキスト入力

- 色名（英名）・16進カラーコード（`#RRGGBB` / `#AARRGGBB`）・`{色}+{不透明度%}`（例: `Red+50%`）を直接入力できます。
- 入力値は `ColorString.ToArgbHex`（コア）により `#AARRGGBB` へ正規化されます。
- 画面表示は `{色}+{不透明度%}` 形式（色名が `Colors` にあれば色名、なければ `#RRGGBB`）。
- `+{n}%` 形式を含まない入力（`#AARRGGBB` 等）はアルファ成分を不透明度として解釈します（例: `#80FF0000` → `Red+50%`）。
- 不透明度は `0〜100%` の整数へ丸めて表示し、100% の場合は省略します。

### 6.2 一覧（タブ）の選択

- 入力欄と展開ボタンは**標準のコンボボックス**です（自前の枠・▼・… を作りません）。 展開ボタンを押すと、標準の一覧の代わりに**タブ付きのフライアウト**が出ます（`Alt+↓` / `F4` でも開きます）。
- タブの並びは **カスタム → テーマカラー → 色名 → ColorPicker** です（`ShowCustomTab` / `ShowThemeColorTab` / `ShowNamedColorTab` / `ShowPickerTab` で取捨）。 前回開いたタブから始まります。
- 一覧のタブは「色スウォッチ + 色名」、格子のタブは**色の四角だけを 6 列**に並べます（名前はツールチップ）。
- 「ColorPicker」タブは**カラーピッカーをタブの中へ直接表示**します（OK / キャンセル付き。 別のフライアウトを重ねません）。
- タブが 1 つだけのときは `ShowTabTitles` で題名の行を隠せます（2 つ以上あるときは切り替えに必要なので常に表示）。
- 一覧を開いても**コントロールの幅は変わりません**（一覧だけが**枠・タブの行・内容のうち最も広い幅**へ広がります。 一覧はその幅いっぱいに置くため、**スクロール バーは右端**に出ます。 タブが多いときはタブの幅を詰めて、フライアウトの既定の最大幅に収めます）。

### 6.3 無効な入力の扱い

- フォーカス喪失時、入力値が無効な場合は**最終有効値へ自動復帰**します。
- `HighlightBorderOnError` が `true` の場合、入力中も枠線が赤く表示されます。

### 6.4 カラーピッカー

- 一覧の「ColorPicker」タブを開くと、カラーピッカー（`ColorView`）がタブの中へ表示されます。
- 「OK」で選択色を `#AARRGGBB`（アルファ先頭8桁）の保存値として反映し、「キャンセル」で破棄します。
- 初期表示色は `SelectedColor` をパースした色（失敗時は白）です。
- カラーピッカーの成分タブにある 16 進の入力欄は `#AARRGGBB`（アルファ先頭）で、Avalonia 標準の `Color.Parse` と整合します。
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
