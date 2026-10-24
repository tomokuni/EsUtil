# EsUtil.Ui.WinUI3.ColorComboBox API リファレンス

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
| 対象フレームワーク | `net10.0-windows10.0.22621.0`（Windows App SDK 1.x） |
| 色名と色文字列 | `EsUtil.Platform.Core` の `Palette` |

## 2. 公開 API

### 2.1 `ColorComboBoxControl`

| メンバー | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `SelectedColor` | `string` | `#FFFFFFFF` | 選択中の色（保存値）。 色名や `+{n}%` を設定すると正規化される |
| `ColorSortMode` | `ColorSortMode`（コア） | `ByHue` | 一覧の並び順 |
| `HasValidationError` | `bool`（取得のみ） | `false` | 現在の入力が無効か |
| `ValidationErrorMessage` | `string?`（取得のみ） | `null` | 無効な場合の理由 |
| `Log` | `ILogger` | `NullLogger` | 変更の記録先 |
| `LogPrefix` | `string?` | `null` | ログの接頭辞（null は `[ColorComboBox]`） |
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
using EsUtil.Ui.WinUI3.ColorComboBox;

var color = new ColorComboBoxControl
{
    SelectedColor = EsColor.ToArgbHex("Red"),
    ColorSortMode = ColorSortMode.ByName,
    Log = loggerFactory.CreateLogger("ColorComboBox"),
};
color.SelectedColorChanged += (_, _) => settings.CaptureBorderColor = color.SelectedColor;
```

## 4. 座標系と単位

寸法はすべて DIP です（スウォッチ 16、入力欄の最小幅 120）。 Flyout の配置は WinUI が決めます。

## 5. 制限事項

- **XAML からは配置できません**（コードで生成します）。
- **色名は Windows 標準**であり、追加・変更はできません（コアの `ColorNames` が所有します）。
- **一覧へは `Items.Add` で 1 件ずつ渡します**（Native AOT では `ItemsSource` へ managed コレクションを渡せないため）。
