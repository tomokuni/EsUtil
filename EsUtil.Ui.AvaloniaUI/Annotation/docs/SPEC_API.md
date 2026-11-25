# EsUtil.Ui.AvaloniaUI.Annotation API リファレンス

## 目次

- [1. `AnnotationEditorPanel`（クラス）](#1-annotationeditorpanelクラス)
- [2. `AnnotationLayerControl`（クラス）](#2-annotationlayercontrolクラス)
- [3. `AnnotationEditorControls`（クラス）](#3-annotationeditorcontrolsクラス)
- [4. 書式バー（`StyledTextFormatBar`）](#4-書式バーstyledtextformatbar)
- [5. `AnnotationPalette`（静的クラス）](#5-annotationpalette静的クラス)

## 1. `AnnotationEditorPanel`（クラス）

**本文の表示・その場編集と書式バーを 1 つにまとめたコントロール**です（`Panel` の派生）。 ホストはこれを 1 つ置けば、本文の編集と文字単位の書式変更ができます。

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `Layer` | property | 内側の `AnnotationLayerControl`（画像・文書・選択・元に戻すはここから操作する） |
| `IsEditingText` | property | 本文を編集中かどうか（書式バーは 1 文字以上を選んでいる間だけ出る） |
| `ShowFormatBarWhileEditing` | property | 編集中に書式バーを自動で表示するか（既定は `true`。 ホストが独自にバーを出す場合は `false`） |
| `IsFormatBarOpen` | property | 書式バーを表示中かどうか（編集中で 1 文字以上を選んでいる間だけ `true`） |
| `TextFormat` | property | 書式バーと右クリックのフライアウトの色の表記（色名 + 不透明度 / カラーコード）。 カラーコードは `#AARRGGBB` |
| `Log` / `LogPrefix` | property | ログの出力先と接頭辞（コアと提示層で共有する。 既定 `[Annotation]`） |
| `Presenter` | property | 描画計画を解決する提示層（ログと接頭辞を共有する） |
| `Core` | property | 文書・保存形式・ログの入口（ファサード。 アプリは `Core.Load` / `Core.Save` を使う） |

- **書式バーは内側で出し入れします**（`Layer.EditingChanged` と `Layer.TextSelectionChanged` を購読し、**1 文字以上を選んでいる間だけ**出す）。 ホストがバーを置く必要はありません。
- **配置**: レイヤーは与えられた領域いっぱい、書式バーは**本文に被さらない位置**（本文の上・下・左右の外側）へ出す。 バーの大きさは**中身に合わせる**（説明の行と「完了」は置かない）。
- **書式バーと右クリックのフライアウトは枠なしのウィンドウへ載せます**: アプリのウィンドウの内側（画面の内側）へ収まる位置へ置きます（規則は `AnnotationPanelPlacement.ResolveAvailableArea` が単一所有する）。
- **文字を選んでいない間は操作を無効にします**（適用先が決まらないため。 `Layer.HasTextSelection`）。
- 文書の変更・選択の変更（`Layer.TextSelectionChanged`）でバーの表示（現在の書式・装飾の押下状態・有効無効）と位置を追随させます。

## 2. `AnnotationLayerControl`（クラス）

画像と注釈を重ねて表示し、選択・移動・サイズ変更・しっぽの移動・本文の編集要求を行うコントロールです。

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `Source` | property | 表示する画像（`IImage`）。 null の場合は注釈だけを描く |
| `SourcePixelWidth` / `SourcePixelHeight` | property | 元画像の画素数（縦横比を保って中央へ表示する） |
| `Document` | property | 注釈の文書。 差し替えると選択を解除する |
| `SelectedId` / `Selected` | property | 選択中の注釈（識別子・実体） |
| `DraftDefaults` | property | 新規作成時の既定値（`AnnotationDraftDefaults`。 既存の注釈は変わらない） |
| `ColorTextFormat` | property | 色の入力欄へ出す表記（色名 + 不透明度 / カラーコード）。 `AnnotationEditorPanel.TextFormat` が同じ値を写す |
| `Log` / `LogPrefix` | property | ログの出力先と接頭辞（コアと提示層で共有する） |
| `Presenter` | property | 描画計画を解決する提示層（ログと接頭辞を共有する） |
| `Core` | property | 文書・保存形式・ログの入口（ファサード。 アプリは `Core.Load` / `Core.Save` を使う） |
| `LineHeightFactor` | property | 行間の倍率（既定 1.25） |
| `HandleHitRadius` / `HandleSize` | property | ハンドルを掴む半径・見えている一辺の大きさ（DIP。 描画と当たり判定で同じ値を共有する） |
| `TextSelectionRange` | property | 本文の書式を適用する範囲（開始位置・長さ。 長さ 0 は選択なし） |
| `SelectText(start, length)` | method | 本文の選択範囲を指定する（ホストが本文の選択範囲を写す） |
| `SelectedTextStyle` | property | 選択中の本文の書式（範囲の先頭の文字。 フライアウトの初期表示） |
| `SelectedTextMatches(match)` | method | 選択範囲の**すべての文字**が条件を満たすか（書式バーの押下状態。 選択が無ければキャレットの位置で判定） |
| `AddFrame(bounds)` / `AddBubble(bounds, text)` | method | 注釈を追加し、追加した注釈を選択する（戻り値は識別子） |
| `DeleteSelected()` | method | 選択中の注釈を削除する |
| `SetSelectedText(text)` | method | 選択中の吹き出しの本文を置き換える（**書式は文字位置で引き継ぐ**） |
| `ApplyTextStyle(style)` / `ApplyTextStyle(start, length, style)` / `TransformTextStyle(transform)` / `TransformTextStyle(start, length, transform)` | method | 本文へ書式を適用する（`TextSelectionRange` の範囲、または指定した範囲。 **文字単位**。 長さ 0 は全体）。 `ApplyTextStyle` は**書式を丸ごと差し替える**、`TransformTextStyle` は**指定した属性だけを書く**（他の属性を保つ） |
| `UpdateSelectedAppearance(transform)` | method | 外観（枠線・内側・角丸）を差し替える |
| `SetSelectedBodyPadding(padding)` / `UpdateSelectedTail(transform)` | method | 吹き出しの本文の余白・しっぽ（幅・辺）を差し替える |
| `BringSelectedToFront()` / `SendSelectedToBack()` | method | 重なり順を変える |
| `Undo()` / `Redo()` / `CanUndo` / `CanRedo` | method / property | 元に戻す・やり直す（変更の単位は 1 操作。 ドラッグは押下から離すまでで 1 手。 選択は識別子で引き継ぐ） |
| `ShowStyleEditor()` | method | 外観・書式・重なり順の編集フライアウトを表示する（右クリックと同じ） |
| `BeginTextEditing()` / `EndTextEditing()` / `IsEditingText` | method / property | 本文を**その場で編集**する（キャレット・選択範囲を本文のレイアウトのまま描く。 入力欄を重ねない） |
| `CaretIndex` / `SelectionStart` / `SelectionEnd` / `SelectionLength` | property | キャレットと選択範囲（文字数） |
| `EditingChanged` | event | 本文の編集の開始・終了（書式バーの出し入れはパネルが行う） |
| `TextSelectionChanged` | event | 本文の選択範囲（キャレット）の変更（書式バーの現在値を追随させる） |
| `GetSelectedBodyRect()` | method | 本文の矩形（DIP）。 本文編集コントロールを重ねる位置と書式バーの位置を決める |
| `RenderComposedImage()` | method | 画像と注釈を元画像ピクセルの 1:1 で合成した `EsBitmap` を作る（保存用。 選択枠・ハンドル・キャレットは描かない。 所有権は呼び出し側） |
| `InvalidateRender()` | method | 今の状態で再描画する（ホストが画像の位置や表示倍率を変えたときに呼ぶ） |
| `DocumentChanged` / `SelectionChanged` / `EditTextRequested` | event | 文書の変更・選択の変更・本文の編集要求 |

操作の割り当ては次のとおりです。

| 入力 | 動作 |
| --- | --- |
| クリック | 選択（最前面の注釈。 背景は選択解除） |
| **ハンドルの外周をクリック** | サイズ変更の開始（**カーソルが変わる範囲と同じ判定**。 選択は解除されない） |
| ドラッグ（本体） | 移動 |
| ドラッグ（8 方向のハンドル） | サイズ変更 |
| ドラッグ（しっぽの先端のハンドル） | しっぽの移動（本体と独立。 **本体と同じ外観のハンドル**。 上ではカーソルが手の形になる） |
| ダブルクリック / Enter | 本文の編集を始める（その場で編集。 キャレットと選択範囲を本文の上に描く） |
| **右クリック** | **外観・本文の書式・重なり順の編集フライアウト**（選択していない注釈なら先に選択する） |
| Delete | 選択中の注釈を削除（**本文の編集中は 1 文字削除**） |
| Ctrl+Z / Ctrl+Y | 元に戻す・やり直す（本文の編集中は入力を取り消す） |
| Ctrl+B / Ctrl+I / Ctrl+U（編集中） | 選択範囲の太字・斜体・下線を切り替える |
| Alt+Shift+5（編集中） | 選択範囲の取り消し線を切り替える |
| 方向キー（`Shift` 併用） | nudge（1 px / 10 px） |

**注意**: 保存（注釈 JSON・合成画像）は行いません。 ホストが `AnnotationJson` と `RenderComposedImage()` で書き出します。

## 3. `AnnotationEditorControls`（クラス）

注釈の編集 UI で使う行の部品です（右クリックのフライアウトとホストの設定ペインが共有する）。

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `LabelWidth` | const | 項目名のラベルの幅（DIP。 既定 104） |
| `ColorSelectorWidth` | const | 色の選択の幅（DIP。 既定 210 = 項目名 + 選択の行が収まる幅。 **横いっぱいへ広げない**） |
| `Heading(text)` | method | 見出しを作る |
| `Label(text)` | method | 項目名のラベルを作る |
| `ColorRow(label, initial, apply, textFormat)` | method | 色の行を作る（**`ColorSelectorControl` を使う**。 幅は `ColorSelectorWidth` へ固定する。 `apply` へは検証済みの色だけを渡す。 `textFormat` は入力欄の表記） |
| `ColorRow(label, selector, apply)` | method | 構成済みの色の選択を並べた行を作る（タブの構成を呼び出し側が決めるときに使う） |
| `CreateSelector(initial, columns, allPalettes, textFormat)` | method | 注釈向けに構成した色の選択を作る（`allPalettes` は代表色 + 標準のタブ。 設定画面向け。 `textFormat` は入力欄の表記） |
| `NumberRow(label, initial, minimum, maximum, apply, increment)` | method | 数値の行を作る（`NumericUpDown`） |
| `ChoiceRow(label, options, selectedIndex, apply)` | method | 選択肢の行を作る（`ComboBox`） |
| `Toggle(text, isChecked, apply, bold, italic, underline, strike)` | method | 装飾の切り替え（B / I / U / S）を作る |
| `ToHex(color)` | method | 色を `#AARRGGBB` の文字列へ変換する |

**前提**: `ColorRow` は `Avalonia.Controls.ColorPicker` のテーマを要求します（ホストが
`avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml` を読み込む）。

## 4. 書式バー（`StyledTextFormatBar`）

本文を編集中に本文の上へ出す書式バーは **`EsUtil.Ui.AvaloniaUI.StyledText` の `StyledTextFormatBar`** が提供します（文字単位の書式）。 注釈はバーを持たず、代表色の並びを渡して使います。

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `FormatBarFactory` | property | 書式バーを作る処理（`IStyledTextFormatBarFactory`）。 **独自の GUI へ差し替える入口**である |
| `FormatBarOptions` | property | バーの構成（フォントの一覧・色の選択・色の表記） |

バーの API と差し替えの規則は [`../../StyledText/docs/SPEC_API.md`](../../StyledText/docs/SPEC_API.md) が所有します。

## 5. `AnnotationPalette`（静的クラス）

注釈の色選択で使う**代表色（色名付きの 40 色）**です。

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `RepresentativeColors` | property | 代表色の並び（`IReadOnlyList<ColorSelectorCustomColor>`。 無彩色 → 赤系 → 橙・黄系 → 緑系 → 青系 → 紫・桃系の順。 40 色すべて異なる値） |
| `DefaultPaletteColumns` | const | 代表色の既定の列数（8。 40 色なら 8 列 × 5 行） |
| `PaletteTabTitle` | const | タブの題名（`代表色`） |
| `CreatePaletteTab(columns)` | method | ■ だけを指定した列数で並べるタブの定義を作る |
| `ConfigureAll(selector, columns)` | method | 色の選択を「代表色 + テーマカラー・色名・ColorPicker」の構成にする（設定画面向け） |
| `GetHex(name)` / `TryGetColor(name, out color)` | method | 色名から保存値（`#AARRGGBB`）・色を取得する |

- **書式バー・右クリックのフライアウト・設定ペインが同じ並びを使います**（画面ごとに色が違わない）。
- 色の選択の入力欄は**「■ + 色名」**（現在色の四角 + 色名）で表示します。
- タブは**「代表色」（■ の格子）と「ColorPicker」の 2 つ**を出し、テーマ・色名のタブは隠します。 設定ペインはテーマカラー・色名のタブも出します。
- 代表色の列数は既定 8 列です（`AnnotationPalette.DefaultPaletteColumns`。 40 色なら 8 列 × 5 行）。
- 16 進や色名を直接打ち込む入力欄は残します（代表色に無い色を指定できる）。
