# EsUtil.Ui.MewUI.ColorSelector API リファレンス

## 目次 <!-- omit from toc -->

- [1. 前提条件](#1-前提条件)
- [2. 公開 API](#2-公開-api)
  - [2.1 `ColorSelectorControl`](#21-colorselectorcontrol)
  - [2.2 入力の扱い](#22-入力の扱い)
  - [2.3 カスタム タブの型](#23-カスタム-タブの型)
- [3. 使用例](#3-使用例)
- [4. 座標系と単位](#4-座標系と単位)
- [5. 制限事項](#5-制限事項)

---

## 1. 前提条件

| 項目 | 内容 |
| --- | --- |
| 対象フレームワーク | `net10.0-windows10.0.17763.0` |
| UI フレームワーク | MewUI 0.22.1 |
| 色名と色文字列 | `EsUtil.Platform.Core` の `Palette`（`ColorNames` / `ColorString` / `ColorValidation` / `ColorEntry` / `ColorSortMode` / `ColorTextFormat`） |

## 2. 公開 API

### 2.1 `ColorSelectorControl`

色を選ぶ複合コントロールです（基底は `Aprillz.MewUI.Controls.Grid`）。

入力欄は**標準のコンボボックス**（枠・展開ボタン）と、その文字の位置へ重ねた**テキスト入力**（文字の表示と編集）で成ります。 MewUI 0.22.1 の `ComboBox` は読み取り専用の選択表示だけであり、AvaloniaUI 版の `IsEditable` に当たる API を持たないための代替です。

| メンバー | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `SelectedColor` | `string` | `#FFFFFFFF` | 選択中の色（**保存値** `<c>#AARRGGBB</c>`）。 色名や `+{n}%` を設定すると正規化される。 設定すると入力欄・一覧の選択が追従する |
| `SelectedThemeColor` | `string?` | `null` | 選択中の**テーマの色の識別子**（例: `背景`）。 値を持つ間は `SelectedColor` をそのときのテーマの色へ解決し直す（明暗・アクセントの変更に追随する）。 色名・カラーコードを選ぶと `null` に戻る |
| `ThemeColorKeys` | `IReadOnlyList<string>`（static・get） | UI ごとの一覧 | `SelectedThemeColor` へ設定できる識別子の一覧（並びは一覧と同じ）。 設定画面の選択肢と保存値の検証に使う |
| `TryResolveThemeColor(string key, out string hex)` | method（public） | - | 識別子（`背景` など）から、そのときのテーマの色（`#AARRGGBB`）を解決する。 その UI に無い色は false（一覧と同じ解決を使う） |
| `ColorSortMode` | `ColorSortMode`（コア） | `ByHue` | 一覧（タブ「色名」）の並び順（`ByName` / `ByHue`）。 テーマの色は意味の順で固定 |
| 一覧のタブ | （UI の要素） | 色名 / ColorPicker | 展開するとタブ付きの一覧を出す。 並びは **カスタム → テーマカラー → 色名 → ColorPicker**（`ShowCustomTab` / `ShowThemeColorTab` / `ShowNamedColorTab` / `ShowPickerTab` で取捨、`CustomTabs` で追加、1 つだけなら `ShowTabTitles` で題名を隠せる）。 テーマの色は数が多いため、色名と同じ一覧へ混ぜない。 **一覧を開いても枠（入力欄）の幅は変えない**（一覧だけを内容に合わせて広げる）。 前回選んだタブから開く |
| `ShowThemeColors` | `bool` | `true`（取得または設定） | 一覧の先頭に **UI のテーマの色をすべて**並べる（共通の 20 色を「テーマ: 背景 / レイヤー / カード / コントロールの背景 / コントロールの枠線 / 区切り線 / 文字 / 副次テキスト / 無効の文字 / 無効の背景 / アクセント / アクセント（淡）/ アクセント（濃）/ アクセント文字 / アクセント上の文字 / 選択の背景 / ホバーの背景 / 押下の背景 / フォーカス / エラー」の順に並べ、その UI にしか無い色（選択の文字・無効のアクセント・スクロール バーなど）を後ろへ足す）。 値は一覧を作り直すたびにテーマ（MewUI はパレット、WinUI 3 はテーマ資源と `SystemAccentColor`）から解決する |
| `ShowCustomTab` | `bool` | `true` | 一覧の「カスタム」タブを表示するか（`CustomTabs` が空のときは表示しない） |
| `ShowThemeColorTab` | `bool` | `true` | 一覧の「テーマカラー」タブを表示するか（中身が無いときは表示しない） |
| `ShowNamedColorTab` | `bool` | `true` | 一覧の「色名」タブを表示するか |
| `ShowPickerTab` | `bool` | `true` | 一覧の「ColorPicker」タブを表示するか。 タブを選ぶと一覧を閉じてカラーピッカーのドロップダウンを開く（MewUI の `ColorPicker` はインライン表示できない） |
| `ShowTabTitles` | `bool` | `true` | タブが **1 つだけ**のときに題名の行を出すか。 2 つ以上あるときは切り替えに必要なので常に出す |
| `CustomTabs` | `IReadOnlyList<ColorSelectorCustomTab>` | 空 | 一覧へ追加する「カスタム」タブ（題名 + 色名/色コードの並び + 並べ方）。 複数指定できる。 `Colors` が空のタブは表示しない。 **並べ方は一覧（`List`）と色の四角 6 列の格子（`Grid`）** |
| `TextFormat` | `ColorTextFormat`（コア） | `NameWithAlpha` | **入力欄**の表記（`NameWithAlpha` = `{色名}+{%}` / `HexCode` = `#AARRGGBB`）。 一覧の項目は常に色名、ツールチップは常に 2 行（色名+不透明度 / カラーコード）を出す。 テーマの色を選んでいる間は入力欄が `テーマ: <識別子>` になる。 **カスタム タブの色名が一致するときはその名前**（不透明度があるときは `+{n}%`） |
| `TextFontSize` | `double` | テーマの既定（`ThemeMetrics.FontSize`） | 入力欄の文字サイズ（DIP）。 **MewUI の文字サイズは継承されない**ため、周囲の入力欄と同じ大きさにする場合は明示する |
| `HighlightBorderOnError` | `bool` | `false` | 現在の入力が無効な間、入力欄（コンボボックス）の枠線を赤にする（既定は無効） |
| `HasValidationError` | `bool`（取得のみ） | `false` | 現在の入力が無効か |
| `ValidationErrorMessage` | `string?`（取得のみ） | `null` | 無効な場合の理由（`無効な色コードです。`） |
| `Log` | `ILogger` | `NullLogger` | 変更の記録先（null は `NullLogger` へ） |
| `LogPrefix` | `string?` | `null` | ログの接頭辞（null は `[ColorSelector]`） |
| `SelectedColorChanged` | `event EventHandler?` | — | 保存値が変わったときに発火する（同じ値では発火しない） |

### 2.2 入力の扱い

| 入力 | 結果 |
| --- | --- |
| `Red` / `red` / `#FF0000` | `#FFFF0000`（不透明度 100%） |
| `Red+50%` | `#80FF0000`（不透明度をアルファへ合成） |
| `#80FF0000` | そのまま（保存値） |
| 空文字 | エラーなし（未入力として扱う） |
| それ以外（`NotAColor` / `#FFF` / `Red+150%`） | エラー（`HasValidationError` が `true`。 フォーカスを外すと最後の有効値へ戻る） |

### 2.3 カスタム タブの型

**利用者が一覧へ足すタブ**（`CustomTabs` へ指定する）。

| 型 | メンバー | 説明 |
| --- | --- | --- |
| `ColorSelectorCustomTab` | `Title` / `Colors` / `Layout` | タブの題名（1 つだけ表示するときの見出しにも使う）/ 並べる色（並びが一覧の順序）/ 並べ方（既定は `List`） |
| `ColorSelectorCustomColor` | `Name` / `Value` | 一覧に表示する名前 / 選んだときに適用する色（色名または `#AARRGGBB`） |
| `ColorSelectorTabLayout` | `List` / `Grid` | 色名付きの一覧（スクロールする）/ 色の四角だけを **6 列**に並べる格子（スクロールしない。 名前はツールチップ） |

```csharp
color.CustomTabs =
[
    new ColorSelectorCustomTab("代表色", [
        new ColorSelectorCustomColor("赤", "#FFDC2626"),
        new ColorSelectorCustomColor("青", "#FF2563EB"),
    ]),
    new ColorSelectorCustomTab("パレット", [
        new ColorSelectorCustomColor("黒", "#FF000000"),
        new ColorSelectorCustomColor("白", "#FFFFFFFF"),
    ], ColorSelectorTabLayout.Grid),
];
```

- `Colors` が空のタブは表示しません（設定の途中で空になっても壊れない）。
- タブは開くたびに作るため、`CustomTabs` の差し替えは次に開いた一覧から反映されます。
- 題名には `ShowTabTitles` が効きます（タブが 2 つ以上あるときは切り替えに必要なので常に表示）。
- **色名は入力欄にも出ます**（選んだ色がカスタム タブの色と一致したとき。 不透明度があるときは `+{n}%` を足す）。

## 3. 使用例

```csharp
using EsUtil.Platform.Core.Palette;
using EsUtil.Ui.MewUI.ColorSelector;

var color = new ColorSelectorControl
{
    SelectedColor = EsColor.ToArgbHex("Red"),          // "#FFFF0000"
    ColorSortMode = ColorSortMode.ByName,
    TextFormat = ColorTextFormat.HexCode,               // 入力欄を #AARRGGBB で表示する
    Log = loggerFactory.CreateLogger("ColorSelector"),
};
color.SelectedColorChanged += (_, _) => settings.CaptureBorderColor = color.SelectedColor;
```

## 4. 座標系と単位

寸法はすべて DIP です（入力欄の最小幅 120、一覧のタブの幅 96、格子のセル 32・色の四角 20・列数 6、一覧の最大の高さ 360）。 一覧のポップアップはコンボボックスの実寸（`Bounds`。 MewUI はウィンドウ基準）を基準に、左端を揃えて置きます。 一覧の幅は**枠の幅と内容の幅の大きい方**であり、枠（コントロール）の幅は変えません。

## 5. 制限事項

- **色名は Windows 標準**（`System.Drawing.KnownColor`。 システム色は除く）であり、追加・変更はできません（コアの `ColorNames` が所有します）。
- **テーマの色はアプリ実行後に並びます**（`Application.Run` 前はパレットが無いため、一覧に並ばず「テーマカラー」タブも作りません）。 一覧は開くたびに作り直すため、起動後の一覧には並びます。
- **ComboBox に編集可はありません**（MewUI 0.22.1）。 文字の編集と表示は、コンボボックスの文字の位置へ重ねたテキスト入力が担います。
- **カラーピッカーは一覧の中へ埋め込めません**（MewUI の `ColorPicker` はドロップダウンです）。 「ColorPicker」タブを選ぶと一覧を閉じてからピッカーのドロップダウンを開きます（2 つのドロップダウンを重ねません）。
- **ピッカーの折り畳み時の見出しは表示しません**（1×1 の入れ物で切り取り、行の上へ「︿」が出ないようにしています）。
- **文字サイズは継承されません**（MewUI の要素は自分の値だけを使います）。 周囲の入力欄と揃える場合は `TextFontSize` を設定してください。
- **1 つの色だけを扱います**（複数色の同時編集は持ちません）。
