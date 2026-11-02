# EsUtil.Ui.WinUI3.ColorSelector API リファレンス

## 目次 <!-- omit from toc -->

- [1. 前提条件](#1-前提条件)
- [2. 公開 API](#2-公開-api)
  - [2.1 `ColorSelectorControl`](#21-colorselectorcontrol)
  - [2.2 入力の扱い](#22-入力の扱い)
- [3. 使用例](#3-使用例)
- [4. 座標系と単位](#4-座標系と単位)
- [5. 制限事項](#5-制限事項)

---

## 1. 前提条件

| 項目 | 内容 |
| --- | --- |
| 対象フレームワーク | `net10.0-windows10.0.22621.0`（Windows App SDK 1.x） |
| 色名と色文字列 | `EsUtil.Platform.Core` の `Palette`（`ColorNames` / `ColorString` / `ColorValidation` / `ColorEntry` / `ColorSortMode` / `ColorTextFormat`） |

## 2. 公開 API

### 2.1 `ColorSelectorControl`

| メンバー | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `SelectedColor` | `string` | `#FFFFFFFF` | 選択中の色（保存値）。 色名や `+{n}%` を設定すると正規化される |
| `SelectedThemeColor` | `string?` | `null` | 選択中の**テーマの色の識別子**（例: `背景`）。 値を持つ間は `SelectedColor` をそのときのテーマの色へ解決し直す（明暗・アクセントの変更に追随する）。 色名・カラーコードを選ぶと `null` に戻る |
| `ColorSortMode` | `ColorSortMode`（コア） | `ByHue` | 一覧（タブ「標準の色」）の並び順（`ByName` / `ByHue`）。 テーマの色は意味の順で固定 |
| 一覧のタブ | （UI の要素） | テーマカラー / 色名 | 一覧（▼）の上部に **テーマカラー** と **標準の色** のタブを置き、分類を切り替える。 テーマの色は数が多いため、色名と同じ一覧へ混ぜない。 前回選んだタブから開き、選べる項目が無いタブは開かない（`ShowThemeColors` = false のときは色名から開く） |
| `ShowThemeColors` | `bool` | `true`（取得または設定） | 一覧の先頭に **UI のテーマの色をすべて**並べる（共通の 20 色を「テーマ: 背景 / レイヤー / カード / コントロールの背景 / コントロールの枠線 / 区切り線 / 文字 / 副次テキスト / 無効の文字 / 無効の背景 / アクセント / アクセント（淡）/ アクセント（濃）/ アクセント文字 / アクセント上の文字 / 選択の背景 / ホバーの背景 / 押下の背景 / フォーカス / エラー」の順に並べ、その UI にしか無い色（三次テキスト・成功・注意・スクロール バーなど）を後ろへ足す）。 値は一覧を作り直すたびにテーマ（MewUI はパレット、WinUI 3 はテーマ資源と `SystemAccentColor`）から解決する |
| `TextFormat` | `ColorTextFormat`（コア） | `NameWithAlpha` | 入力欄と一覧（タブ「標準の色」）の表記（`NameWithAlpha` = `{色名}+{%}` / `HexCode` = `#AARRGGBB`）。 設定すると入力欄とツールチップの表記が切り替わる |
| `TextFontSize` | `double` | `NaN`（テーマの既定） | 入力欄の文字サイズ（DIP） |
| `TextWidth` | `double` | `NaN`（自動） | 入力欄の明示的な幅（DIP） |
| `TextMinWidth` | `double` | `120` | 入力欄の最小幅（DIP） |
| `TextMaxWidth` | `double` | `Infinity` | 入力欄の最大幅（DIP） |
| `HighlightBorderOnError` | `bool` | `false` | 現在の入力が無効な間、外周の枠線を赤にする（既定は無効） |
| `HasValidationError` | `bool`（取得のみ） | `false` | 現在の入力が無効か |
| `ValidationErrorMessage` | `string?`（取得のみ） | `null` | 無効な場合の理由 |
| `Log` | `ILogger` | `NullLogger` | 変更の記録先 |
| `LogPrefix` | `string?` | `null` | ログの接頭辞（null は `[ColorSelector]`） |
| `SelectedColorChanged` | `event EventHandler?` | — | 保存値が変わったときに発火する |

### 2.2 入力の扱い

| 入力 | 結果 |
| --- | --- |
| `Red` / `#FF0000` | `#FFFF0000` |
| `Red+50%` | `#80FF0000` |
| `#80FF0000` | そのまま |
| 空文字 | エラーなし |
| それ以外 | エラー（フォーカスを外すと最後の有効値へ戻る） |

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
```

## 4. 座標系と単位

寸法はすべて DIP です（スウォッチ 16、入力欄の最小幅 120、▼ と … の区画幅 26、区切りの垂直線 1 ピクセル）。 Flyout（一覧・カラーピッカー）の配置は WinUI が決めます。 一覧とカラーピッカーの Flyout は `ShouldConstrainToRootBounds = false` でウィンドウの外へ出られるようにしています。

## 5. 制限事項

- **XAML からは配置できません**（コードで生成します）。
- **色名は Windows 標準**であり、追加・変更はできません（コアの `ColorNames` が所有します）。
- **一覧へは `Items.Add` で 1 件ずつ渡します**（Native AOT では `ItemsSource` へ managed コレクションを渡せないため）。
