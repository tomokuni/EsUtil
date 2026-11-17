# EsUtil.Ui.WinUI3.ColorSelector API リファレンス

## 目次 <!-- omit from toc -->

- [1. 前提条件](#1-前提条件)
- [2. 公開 API](#2-公開-api)
  - [2.1 `ColorSelectorControl`](#21-colorselectorcontrol)
  - [2.2 入力の扱い](#22-入力の扱い)
  - [2.3 型](#23-型)
- [3. 使用例](#3-使用例)
- [4. 座標系と単位](#4-座標系と単位)
- [5. 制限事項](#5-制限事項)

---

## 1. 前提条件

| 項目 | 内容 |
| --- | --- |
| 対象フレームワーク | `net10.0-windows10.0.17763.0`（Windows App SDK 2.0.1 以降） |
| 色名と色文字列 | `EsUtil.Platform.Core` の `Palette`（`ColorNames` / `ColorString` / `ColorValidation` / `ColorEntry` / `ColorSortMode` / `ColorTextFormat`） |

## 2. 公開 API

### 2.1 `ColorSelectorControl`

| メンバー | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `SelectedColor` | `string` | `#FFFFFFFF` | 選択中の色（保存値）。 色名や `+{n}%` を設定すると正規化される |
| `SelectedThemeColor` | `string?` | `null` | 選択中の**テーマの色の識別子**（例: `背景`）。 値を持つ間は `SelectedColor` をそのときのテーマの色へ解決し直す（明暗・アクセントの変更に追随する）。 色名・カラーコードを選ぶと `null` に戻る |
| `ThemeColorKeys` | `IReadOnlyList<string>`（static・get） | UI ごとの一覧 | `SelectedThemeColor` へ設定できる識別子の一覧（並びは一覧と同じ）。 設定画面の選択肢と保存値の検証に使う |
| `TryResolveThemeColor(string key, out string hex)` | method（public） | - | 識別子（`背景` など）から、そのときのテーマの色（`#AARRGGBB`）を解決する。 その UI に無い色は false（一覧と同じ解決を使う） |
| `ColorSortMode` | `ColorSortMode`（コア） | `ByHue` | 一覧（タブ「色名」）の並び順（`ByName` / `ByHue`）。 テーマの色は意味の順で固定 |
| 一覧のタブ | （UI の要素） | カスタム / テーマカラー / 色名 / ColorPicker | 標準のコンボボックスの展開で、標準の一覧の代わりに**タブ付きの一覧**を出す。 タブの並びは **カスタム → テーマカラー → 色名 → ColorPicker** で、`ShowCustomTab` / `ShowThemeColorTab` / `ShowNamedColorTab` / `ShowPickerTab` で取捨する。 テーマの色は数が多いため、色名と同じ一覧へ混ぜない。 **一覧の幅は枠（入力欄）の幅と揃える**（内容のほうが広い場合は一覧だけを広げ、枠の幅は変えない）。 前回選んだタブから開き、選べる項目が無いタブは開かない。 表示できるタブが 1 つも無ければ一覧を開かない |
| `ShowThemeColors` | `bool` | `true`（取得または設定） | 一覧の先頭に **UI のテーマの色をすべて**並べる（共通の 20 色を「テーマ: 背景 / レイヤー / カード / コントロールの背景 / コントロールの枠線 / 区切り線 / 文字 / 副次テキスト / 無効の文字 / 無効の背景 / アクセント / アクセント（淡）/ アクセント（濃）/ アクセント文字 / アクセント上の文字 / 選択の背景 / ホバーの背景 / 押下の背景 / フォーカス / エラー」の順に並べ、その UI にしか無い色（三次テキスト・成功・注意・スクロール バーなど）を後ろへ足す）。 値は一覧を作り直すたびにテーマ資源と `SystemAccentColor` から解決する |
| `ShowCustomTab` | `bool` | `true` | 一覧の「カスタム」タブを表示するか（`CustomTabs` が空のときは表示しない） |
| `ShowThemeColorTab` | `bool` | `true` | 一覧の「テーマカラー」タブを表示するか（中身が無いときは表示しない） |
| `ShowNamedColorTab` | `bool` | `true` | 一覧の「色名」タブを表示するか |
| `ShowPickerTab` | `bool` | `true` | 一覧の「ColorPicker」タブを表示するか（タブの中でカラーピッカーを直接表示する） |
| `ShowTabTitles` | `bool` | `true` | タブが **1 つだけ**のときに題名の行を出すか。 2 つ以上あるときは切り替えに必要なので常に出す |
| `CustomTabs` | `IReadOnlyList<ColorSelectorCustomTab>` | 空 | 一覧へ追加する「カスタム」タブ（題名 + 色名/色コードの並び + 並べ方）。 複数指定できる。 `Colors` が空のタブは表示しない。 **並べ方は一覧（`List`）と色の四角 6 列の格子（`Grid`）** |
| `TextFormat` | `ColorTextFormat`（コア） | `NameWithAlpha` | **入力欄**の表記（`NameWithAlpha` = `{色名}+{%}` / `HexCode` = `#AARRGGBB`）。 カスタム タブに登録した色名があるときは、その名前を優先する。 一覧の項目は常に色名、ツールチップは常に 2 行（色名+不透明度 / カラーコード）を出す。 テーマの色を選んでいる間は入力欄が `テーマ: <識別子>` になる |
| `TextFontSize` | `double` | `NaN`（テーマの既定） | 入力欄の文字サイズ（DIP） |
| `HighlightBorderOnError` | `bool` | `false` | 現在の入力が無効な間、外周の枠線を赤にする（既定は無効） |
| `HasValidationError` | `bool`（取得のみ） | `false` | 現在の入力が無効か |
| `ValidationErrorMessage` | `string?`（取得のみ） | `null` | 無効な場合の理由 |
| `Log` | `ILogger` | `NullLogger` | 変更の記録先 |
| `LogPrefix` | `string?` | `null` | ログの接頭辞（null は `[ColorSelector]`） |
| `SelectedColorChanged` | `event EventHandler?` | — | 保存値が変わったときに発火する |
| `ColorApplied` | `event EventHandler?` | — | 一覧・カラーピッカーで色を確定したときに発火する（**同じ色を選び直したときも発火**。 `SelectedColor` は変わらないため `SelectedColorChanged` では足りない用途向け）。 入力欄へ打ち込んだ場合は発火しない（確定はフォーカスを外したとき） |
| 入力欄の左の四角 | （UI の要素） | 現在色（無効な入力の間は非表示） | 入力欄の左へ**現在色の四角**（12 DIP・角の丸み 2・半透明の灰色の 1 DIP の枠線・左の余白 7 DIP・上下の中央）を重ね、文字はその右（左から 24 DIP）から始める。 標準のコンボボックスは文字しか出せないため、「■ + 色名」に見せる。 無効な入力の間は隠す（前の色を出すと、その色が選ばれていると誤解される） |

### 2.2 入力の扱い

| 入力 | 結果 |
| --- | --- |
| `Red` / `#FF0000` | `#FFFF0000` |
| `Red+50%` | `#80FF0000` |
| `#80FF0000` | そのまま |
| 空文字 | エラーなし |
| それ以外 | エラー（フォーカスを外すと最後の有効値へ戻る） |

### 2.3 型

| 型 | 可視性 | 説明 |
| --- | --- | --- |
| `ColorSelectorControl` | public | 色を選ぶコントロール（基底は `Microsoft.UI.Xaml.Controls.UserControl`。 中身は標準のコンボボックス + 現在色の四角） |
| `ColorSelectorCustomTab` | public | 一覧へ追加するタブ（`Title` + `Colors` + `Layout`）。 `Colors` が空のタブは表示しない |
| `ColorSelectorCustomColor` | public | カスタム タブの 1 色（`Name` + `Value`）。 `Value` は色名または `#AARRGGBB` |
| `ColorSelectorTabLayout` | public | カスタム タブの並べ方（`List` = 色名付きの一覧 / `Grid` = 色の四角だけを 6 列に並べる格子） |
| `ColorSelectorItem` | internal | 一覧の 1 件（表示名・適用値・分類・テーマの色の識別子） |
| `ColorSelectorCategory` | internal | 一覧の項目の分類（`Theme` / `Named` / `Custom`） |
| `ColorSelectorTabSpec` | internal | 一覧の 1 タブ（題名・識別子・並べ方・並べる項目・カラーピッカーかどうか）。 表示の設定と中身から作り、空のタブは作らない |
| `ColorApplyPlan` | internal | 一覧・カラーピッカーで色を確定したときの適用の計画（適用する値・テーマの色の識別子・確定を通知するか） |

## 3. 使用例

```csharp
using EsUtil.Platform.Core.Palette;
using EsUtil.Ui.WinUI3.ColorSelector;

var color = new ColorSelectorControl
{
    SelectedColor = EsColor.ToArgbHex("Red"),
    ColorSortMode = ColorSortMode.ByName,
    TextFormat = ColorTextFormat.HexCode,               // 入力欄を #AARRGGBB で表示する
    Log = loggerFactory.CreateLogger("ColorSelector"),
};
color.SelectedColorChanged += (_, _) => settings.CaptureBorderColor = color.SelectedColor;

// 用途ごとのタブ（「代表色」）を追加する。 空のタブは表示されない
color.CustomTabs =
[
    // 色名付きの一覧
    new ColorSelectorCustomTab("枠線", [
        new ColorSelectorCustomColor("枠線の赤", "#FFDC2626"),
        new ColorSelectorCustomColor("枠線の青", "#FF2563EB"),
    ]),

    // 色の四角を 6 列に並べる（16 色なら 3 行に収まり、スクロールせずに選べる）
    new ColorSelectorCustomTab("代表色", [
        new ColorSelectorCustomColor("黒", "#FF000000"),
        new ColorSelectorCustomColor("赤", "#FFDC2626"),
        // … 16 色まで
    ], ColorSelectorTabLayout.Grid),
];
```

## 4. 座標系と単位

寸法はすべて DIP です（一覧のタブの幅 112、一覧の最大の高さ 360、格子のセル 32、色の四角 20、ピッカーのタブの幅 320、操作ボタンの幅 70、入力欄の左の現在色の四角 12・左の余白 7・文字の開始位置 24）。 入力欄と展開ボタンは標準の `ComboBox` が持つ寸法に従います。 Flyout（一覧）の配置は WinUI が決めます。 一覧の Flyout は `ShouldConstrainToRootBounds = false` でウィンドウの外へ出られるようにし、外側をスクロールさせません（一覧は自分の高さでスクロールし、格子はスクロールしません）。

一覧の幅は `max(枠の幅, 内容の幅)` です（内容のほうが広い場合は一覧だけが広がり、**コントロールの幅と最小幅は変わりません**）。 一覧を開くたびに枠が広がると、開閉のたびに周りのレイアウトが動くためです。

## 5. 制限事項

- **XAML からは配置できません**（コードで生成します）。
- **色名は Windows 標準**であり、追加・変更はできません（コアの `ColorNames` が所有します）。
- **一覧へは `Items.Add` で 1 件ずつ渡します**（Native AOT では `ItemsSource` へ managed コレクションを渡せないため）。
