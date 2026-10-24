# EsUtil.Ui.MewUI.ColorComboBox API リファレンス

## 目次 <!-- omit from toc -->

- [1. 前提条件](#1-前提条件)
- [2. 公開 API](#2-公開-api)
  - [2.1 `ColorComboBoxControl`](#21-colorcomboboxcontrol)
  - [2.2 入力の扱い](#22-入力の扱い)
- [3. 使用例](#3-使用例)
- [4. 座標系と単位](#4-座標系と単位)
- [5. 制限事項](#5-制限事項)

---

## 1. 前提条件

| 項目 | 内容 |
| --- | --- |
| 対象フレームワーク | `net10.0` |
| UI フレームワーク | MewUI 0.21.1 |
| 色名と色文字列 | `EsUtil.Platform.Core` の `Palette`（`ColorNames` / `ColorString` / `ColorValidation` / `ColorEntry` / `ColorSortMode`） |

## 2. 公開 API

### 2.1 `ColorComboBoxControl`

色を選ぶ複合コントロールです（基底は `Aprillz.MewUI.Controls.Grid`）。

| メンバー | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `SelectedColor` | `string` | `#FFFFFFFF` | 選択中の色（**保存値** `<c>#AARRGGBB</c>`）。 色名や `+{n}%` を設定すると正規化される。 設定すると入力欄・スウォッチ・一覧の選択が追従する |
| `ColorSortMode` | `ColorSortMode`（コア） | `ByHue` | 一覧の並び順（`ByName` / `ByHue`） |
| `HasValidationError` | `bool`（取得のみ） | `false` | 現在の入力が無効か |
| `ValidationErrorMessage` | `string?`（取得のみ） | `null` | 無効な場合の理由（`無効な色コードです。`） |
| `Log` | `ILogger` | `NullLogger` | 変更の記録先（null は `NullLogger` へ） |
| `LogPrefix` | `string?` | `null` | ログの接頭辞（null は `[ColorComboBox]`） |
| `SelectedColorChanged` | `event EventHandler?` | — | 保存値が変わったときに発火する（同じ値では発火しない） |

### 2.2 入力の扱い

| 入力 | 結果 |
| --- | --- |
| `Red` / `red` / `#FF0000` | `#FFFF0000`（不透明度 100%） |
| `Red+50%` | `#80FF0000`（不透明度をアルファへ合成） |
| `#80FF0000` | そのまま（保存値） |
| 空文字 | エラーなし（未入力として扱う） |
| それ以外（`NotAColor` / `#FFF` / `Red+150%`） | エラー（`HasValidationError` が `true`。 フォーカスを外すと最後の有効値へ戻る） |

## 3. 使用例

```csharp
using EsUtil.Platform.Core.Palette;
using EsUtil.Ui.MewUI.ColorComboBox;

var color = new ColorComboBoxControl
{
    SelectedColor = EsColor.ToArgbHex("Red"),          // "#FFFF0000"
    ColorSortMode = ColorSortMode.ByName,
    Log = loggerFactory.CreateLogger("ColorComboBox"),
};
color.SelectedColorChanged += (_, _) => settings.CaptureBorderColor = color.SelectedColor;
```

## 4. 座標系と単位

寸法はすべて DIP です（スウォッチ 16、入力欄の最小幅 120）。 ポップアップの配置はコントロールの実寸から決めます。

## 5. 制限事項

- **色名は Windows 標準**（`System.Drawing.KnownColor`。 システム色は除く）であり、追加・変更はできません（コアの `ColorNames` が所有します）。
- **ドロップダウン内での編集はできません**（MewUI の `ComboBox` が編集可能でないため、テキスト入力は常設の入力欄が担います）。
- **1 つの色だけを扱います**（複数色の同時編集は持ちません）。
