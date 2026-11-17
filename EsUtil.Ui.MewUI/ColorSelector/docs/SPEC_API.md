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

入力欄は**標準のコンボボックス**（枠・展開ボタン）と、その文字の位置へ重ねた**テキスト入力**（文字の表示と編集）で成ります。 MewUI 0.22.1 の `ComboBox` は読み取り専用の選択表示だけであり、AvaloniaUI 版の `IsEditable` に当たる API を持たないための代替です。 入力欄の左には**現在色の四角**を重ねて「■ + 色名」で表示し、文字はその右から始まります（文字は上下の中央にそろえる）。

| メンバー | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `SelectedColor` | `string` | `#FFFFFFFF` | 選択中の色（**保存値** `<c>#AARRGGBB</c>`）。 色名や `+{n}%` を設定すると正規化される。 設定すると入力欄・一覧の選択が追従する |
| `SelectedThemeColor` | `string?` | `null` | 選択中の**テーマの色の識別子**（例: `背景`）。 値を持つ間は `SelectedColor` をそのときのテーマの色へ解決し直す（明暗・アクセントの変更に追随する）。 色名・カラーコードを選ぶと `null` に戻る |
| `ThemeColorKeys` | `IReadOnlyList<string>`（static・get） | UI ごとの一覧 | `SelectedThemeColor` へ設定できる識別子の一覧（並びは一覧と同じ）。 設定画面の選択肢と保存値の検証に使う |
| `TryResolveThemeColor(string key, out string hex)` | method（public） | - | 識別子（`背景` など）から、そのときのテーマの色（`#AARRGGBB`）を解決する。 その UI に無い色は false（一覧と同じ解決を使う） |
| `ColorSortMode` | `ColorSortMode`（コア） | `ByHue` | 一覧（タブ「色名」）の並び順（`ByName` / `ByHue`）。 テーマの色は意味の順で固定 |
| 一覧のタブ | （UI の要素） | 色名 / ColorPicker | 展開ボタン（右端 30 DIP）の押下、または Alt+↓ / F4 でタブ付きの一覧を出す（標準の一覧は開かせない）。 並びは **カスタム → テーマカラー → 色名 → ColorPicker**（`ShowCustomTab` / `ShowThemeColorTab` / `ShowNamedColorTab` / `ShowPickerTab` で取捨、`CustomTabs` で追加、1 つだけなら `ShowTabTitles` で題名を隠せる）。 テーマの色は数が多いため、色名と同じ一覧へ混ぜない。 **一覧を開いても枠（入力欄）の幅は変えない**（一覧だけを、枠・タブの行・内容のうち最も広い幅へ広げ、スクロール バーを右端へそろえる）。 前回選んだタブから開く。 タブの中身は選んだものだけを作る（開いていないカラーピッカーのタブを作らない） |
| `ShowThemeColors` | `bool` | `true`（取得または設定） | 一覧の先頭に **UI のテーマの色をすべて**並べる（共通の 20 色を「テーマ: 背景 / レイヤー / カード / コントロールの背景 / コントロールの枠線 / 区切り線 / 文字 / 副次テキスト / 無効の文字 / 無効の背景 / アクセント / アクセント（淡）/ アクセント（濃）/ アクセント文字 / アクセント上の文字 / 選択の背景 / ホバーの背景 / 押下の背景 / フォーカス / エラー」の順に並べ、その UI にしか無い色（選択の文字・無効のアクセント・スクロール バーなど）を後ろへ足す）。 値は一覧を作り直すたびにテーマ（MewUI はパレット、WinUI 3 はテーマ資源と `SystemAccentColor`）から解決する |
| `ShowCustomTab` | `bool` | `true` | 一覧の「カスタム」タブを表示するか（`CustomTabs` が空のときは表示しない） |
| `ShowThemeColorTab` | `bool` | `true` | 一覧の「テーマカラー」タブを表示するか（中身が無いときは表示しない） |
| `ShowNamedColorTab` | `bool` | `true` | 一覧の「色名」タブを表示するか |
| `ShowPickerTab` | `bool` | `true` | 一覧の「ColorPicker」タブを表示するか。 タブを選ぶと一覧を閉じてカラーピッカーのドロップダウンを開く（MewUI の `ColorPicker` はインライン表示できない） |
| `ShowTabTitles` | `bool` | `true` | タブが **1 つだけ**のときに題名の行を出すか。 2 つ以上あるときは切り替えに必要なので常に出す |
| `CustomTabs` | `IReadOnlyList<ColorSelectorCustomTab>` | 空 | 一覧へ追加する「カスタム」タブ（題名 + 色名/色コードの並び + ■ の列数）。 複数指定できる。 `Colors` が空のタブは表示しない。 **列数 0 は色名付きの一覧、1 以上は ■ だけの格子（その列数）** |
| `TextFormat` | `ColorTextFormat`（コア） | `NameWithAlpha` | **入力欄**の表記（`NameWithAlpha` = `{色名}+{%}` / `HexCode` = `#AARRGGBB`）。 一覧の項目は常に色名、ツールチップは常に 2 行（色名+不透明度 / カラーコード）を出す。 テーマの色を選んでいる間は入力欄が `テーマ: <識別子>` になる。 **カスタム タブの色名が一致するときはその名前**（不透明度があるときは `+{n}%`） |
| `TextFontSize` | `double` | テーマの既定（`ThemeMetrics.FontSize`） | 入力欄の文字サイズ（DIP）。 **MewUI の文字サイズは継承されない**ため、周囲の入力欄と同じ大きさにする場合は明示する |
| `HighlightBorderOnError` | `bool` | `false` | 現在の入力が無効な間、入力欄（コンボボックス）の枠線を赤にする（既定は無効） |
| `HasValidationError` | `bool`（取得のみ） | `false` | 現在の入力が無効か |
| `ValidationErrorMessage` | `string?`（取得のみ） | `null` | 無効な場合の理由（`無効な色コードです。`） |
| `Log` | `ILogger` | `NullLogger` | 変更の記録先（null は `NullLogger` へ） |
| `LogPrefix` | `string?` | `null` | ログの接頭辞（null は `[ColorSelector]`） |
| `SelectedColorChanged` | `event EventHandler?` | — | 保存値が変わったときに発火する（同じ値では発火しない） |
| `ColorApplied` | `event EventHandler?` | — | 一覧・カラーピッカーで色を確定したときに発火する（**同じ色を選び直したときも発火**。 `SelectedColorChanged` は値が変わらないと発火しないため、確定の通知が要る用途で使う）。 入力欄へ打ち込んだ場合は発火しない（確定はフォーカスを外したとき） |

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
| `ColorSelectorCustomTab` | `Title` / `Colors` / `Columns` | タブの題名（1 つだけ表示するときの見出しにも使う）/ 並べる色（並びが一覧の順序）/ **■ の列数**（既定は 0 = 一覧） |
| `ColorSelectorCustomColor` | `Name` / `Value` | 一覧に表示する名前 / 選んだときに適用する色（色名または `#AARRGGBB`） |
| `ColorSelectorCustomTab.Columns` | `int` | **■ の列数**。 `0` は色名付きの一覧（スクロールする）、`1` 以上は ■ だけをその列数で並べる格子（名前はツールチップ。 スクロールしない） |

```csharp
color.CustomTabs =
[
    new ColorSelectorCustomTab("代表色", [
        new ColorSelectorCustomColor("赤", "#FFDC2626"),
        new ColorSelectorCustomColor("青", "#FF2563EB"),
    ]),
    // ■ を 2 列に並べる（少ない色を色で選ぶとき。 名前はツールチップで示す）
    new ColorSelectorCustomTab("パレット", [
        new ColorSelectorCustomColor("黒", "#FF000000"),
        new ColorSelectorCustomColor("白", "#FFFFFFFF"),
    ], 2),
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
color.ColorApplied += (_, _) => ApplyToSelection(color.SelectedColor);   // 同じ色を選び直したときも届く
```

## 4. 座標系と単位

寸法はすべて DIP です（入力欄の最小幅 120、現在色の四角 12・左の余白 7・四角と文字の間 5（文字は 24 から始まる）、一覧のタブの幅 96・最小 56・間隔 4、格子のセル 32・色の四角 20・列数はタブごとの指定（`Columns`。 既定 0 = 一覧）、一覧の最大の高さ 360）。 一覧のポップアップはコンボボックスの実寸（`Bounds`。 MewUI はウィンドウ基準）を基準に、左端を揃えて置きます。 一覧の幅は**枠の幅・タブの行の幅・内容の幅のうち最も広いもの**（上限はウィンドウのクライアント領域から影の 16 を引いた値。 ただしタブを最小幅で並べた行までは広げる）であり、枠（コントロール）の幅は変えません。 タブの幅は `(幅 - 間隔 × (数 - 1)) / 数` を 96 と 56 の間に収め、数が多くても右端が切れません。 一覧（`ListBox`）はその幅いっぱいに置くため、**スクロール バーはポップアップの右端**に来ます。

## 5. 制限事項

- **色名は Windows 標準**（`System.Drawing.KnownColor`。 システム色は除く）であり、追加・変更はできません（コアの `ColorNames` が所有します）。
- **テーマの色はアプリ実行後に並びます**（`Application.Run` 前はパレットが無いため、一覧に並ばず「テーマカラー」タブも作りません）。 一覧は開くたびに作り直すため、起動後の一覧には並びます。
- **ComboBox に編集可はありません**（MewUI 0.22.1）。 文字の編集と表示は、コンボボックスの文字の位置へ重ねたテキスト入力が担います。
- **カラーピッカーは一覧の中へ埋め込めません**（MewUI の `ColorPicker` はドロップダウンです）。 「ColorPicker」タブを選ぶと一覧を閉じてからピッカーのドロップダウンを開きます（2 つのドロップダウンを重ねません）。
- **ピッカーの折り畳み時の見出しは表示しません**（1×1 の入れ物で切り取り、行の上へ「︿」が出ないようにしています）。
- **文字サイズは継承されません**（MewUI の要素は自分の値だけを使います）。 周囲の入力欄と揃える場合は `TextFontSize` を設定してください。
- **1 つの色だけを扱います**（複数色の同時編集は持ちません）。
- **タブが非常に多いとポップアップがウィンドウより広がります**（最後のタブを欠かさないため、幅の上限はタブを最小 56 DIP で並べた行まで広げます。 画面の幅を超える数では収まりません）。
