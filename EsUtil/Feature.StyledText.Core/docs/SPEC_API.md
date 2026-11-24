# EsUtil.Feature.StyledText.Core API リファレンス

`EsUtil.Feature.StyledText` は、書式付きの文字列（run 単位の書式・折り返し・書記素クラスタ・行と文字位置の対応）を扱う計算層です。 UI フレームワークに依存しません。 **文字の実寸の計測だけを UI に委ねます**（`Func<string, TextStyle, EsSize>` として受け取ります）。

## 目次

- [1. 公開 API の可視性](#1-公開-api-の可視性)
- [2. `RichText`（クラス）](#2-richtextクラス)
- [3. `TextRun` / `TextStyle`（レコード）](#3-textrun--textstyleレコード)
- [4. `TextLayout`（静的クラス）](#4-textlayout静的クラス)
- [5. `TextLinePlan` / `TextSegmentPlan` / `TextLineMetrics` / `TextCaretRect`（レコード）](#5-textlineplan--textsegmentplan--textlinemetrics--textcaretrectレコード)
- [6. `TextLineIndex`（クラス）](#6-textlineindexクラス)
- [7. `TextLineLayout`（静的クラス）](#7-textlinelayout静的クラス)
- [8. `TextCursor`（静的クラス）](#8-textcursor静的クラス)
- [9. `TextStyleEdit`（静的クラス）](#9-textstyleedit静的クラス)
- [10. `TextCompositionPreview` / `TextCaretBlink`（静的クラス）](#10-textcompositionpreview--textcaretblink静的クラス)
- [11. `TextInput`（静的クラス）](#11-textinput静的クラス)
- [12. `TextEditState`（クラス）](#12-texteditstateクラス)
- [13. `TextEditor`（クラス）](#13-texteditorクラス)
- [14. `TextEditKeyMap` / `TextEditKey` / `TextEditModifiers` / `TextEditCommand`（静的クラス・列挙型）](#14-texteditkeymap--texteditkey--texteditmodifiers--texteditcommand静的クラス列挙型)
- [15. `TextDefaults` / `TextLimits`（静的クラス）](#15-textdefaults--textlimits静的クラス)

## 1. 公開 API の可視性

公開面は名前空間 `EsUtil.Feature.StyledText` に置きます。 計算の種類ごとの型は層の内部の名前空間（`EsUtil.Feature.StyledText.Core.<カテゴリ>`）へ置きます（フォルダと名前空間の一覧は [`../README.md`](../README.md) §3）。

| 型 | 可視性 | 参照可能なアセンブリ |
| --- | --- | --- |
| 書式付きの文字列（`RichText` / `TextRun` / `TextStyle`） | `public` | 任意 |
| 行の値（`TextLinePlan` / `TextSegmentPlan` / `TextLineMetrics` / `TextCaretRect`） | `public` | 任意 |
| 計算（`TextLayout` / `TextLineIndex` / `TextLineLayout` / `TextCursor` / `TextStyleEdit` / `TextCompositionPreview` / `TextCaretBlink`） | `public` | 任意 |
| 編集の状態（`TextEditState` / `TextEditor` / `TextEditChange`）とキーの割り当て（`TextEditKeyMap` / `TextEditKey` / `TextEditModifiers` / `TextEditCommand` / `TextEditRequest`） | `public` | UI 層 |
| 既定値と安全域（`TextDefaults` / `TextLimits`） | `public` | 任意 |
| 折り返しの実装（`TextWrap`） | `internal` | 同一アセンブリとテスト |

- **文字位置はすべて「本文の先頭からの文字数」**（0 起点）です。 バイト数でもクラスタ数でもありません。
- **座標は元画像ピクセル**です（画面の DIP ではない）。 倍率の適用は UI が行います。
- **`measureText` / `measureMetrics` は UI が渡します**（描画に使うテキスト エンジンと測定を一致させるため）。 本層は測り方を持ちません。

## 2. `RichText`（クラス）

**書式付きの文字列**です。 **run が自分の文字列と書式を持ちます**（1 文字だけ色を変える等ができる）。 不変であり、すべての変更は新しい値を返します（変化が無い場合は同じ実体）。

| メンバー | 説明 |
| --- | --- |
| `Runs` | `TextRun` の並び（**run が自分の文字列を持つ**ため、文字の挿入・削除が他の書式へ影響しない） |
| `PlainText` / `IsEmpty` | 書式を除いた文字列・空判定 |
| `Empty` / `RichText()` / `RichText(runs)` / `Create(text, style)` | 空の値・run の並びからの生成・単一書式からの生成 |
| `ApplyStyle(start, length, style)` | **指定範囲の書式を差し替える**（run を境界で分割し、同じ書式は連結する。 長さ 0 以下は全体） |
| `TransformStyle(start, length, transform)` | **指定範囲の書式を 1 属性だけ変換する**（変えない属性を保つ。 「太字にする」「色だけ変える」はこちら） |
| `StyleAt(index)` | 指定位置の文字の書式（範囲外は末尾へフォールバック） |
| `AllMatch(start, length, match)` | 指定範囲の書式がすべて条件を満たすかどうか（書式ボタンの押下状態に使う） |
| `WithPlainText(text)` | 書式を**文字位置で引き継いで**文字列だけを差し替える |
| `Insert(index, text)` | 指定位置へ文字列を挿入する（**直前の文字**の書式を引き継ぐ） |
| `Remove(start, length)` | 指定範囲の文字を除く（残りの書式は保つ） |
| `Normalized()` | 各 run の書式を安全域へ補正し、同じ書式の run を連結する（作成・読み込みの入口で 1 回だけ使う） |

- **範囲の端は書記素クラスタの境界へ揃えます**（`TextCursor.SnapBackward` / `SnapForward`）。 半分に割れたサロゲート ペアを作りません。
- **切り替えの判断は呼び出し側で 1 回だけ行います**（`TransformStyle` は run ごとに `transform` を呼ぶため、範囲の先頭で決めた値を渡す）。

## 3. `TextRun` / `TextStyle`（レコード）

| メンバー | 説明 |
| --- | --- |
| `TextRun.Text` / `TextRun.Style` | 1 つの書式で連続する文字列と、その書式 |
| `TextStyle.FontFamily` | フォント ファミリ名（空の場合は `TextDefaults.PreferredFontFamilies` の先頭） |
| `TextStyle.FontSize` | 文字の大きさ（**元画像ピクセル**） |
| `TextStyle.Color` | 文字色（不透明度を含む） |
| `TextStyle.IsBold` / `IsItalic` / `IsUnderline` / `IsStrike` | 装飾（下線・取り消し線は UI が描く） |
| `TextStyle.Default` / `Normalized()` | 既定の書式 / 安全域への補正（フォント名が空なら既定、大きさは `TextLimits.CoerceFontSize`） |

- **書式は解決済みで保存します**（既定の書式を後から変えても、保存済みの値の見た目は変わりません）。

## 4. `TextLayout`（静的クラス）

**本文を折り返し、行の矩形と書式ごとの区間へ配置します**（単一所有）。

| メンバー | 説明 |
| --- | --- |
| `ResolveLines(text, body, measureText, lineHeightFactor)` | 本文を本文の矩形へ折り返して配置する（左揃え・縦は中央揃え）。 本文が空の場合は空 |

- **折り返しと行の縦位置を 1 か所で決めます**（折り返しの位置と行の高さは対であり、別々に実装するとキャレットの高さと食い違う）。
- **折り返しの規則**: 文字単位（クラスタ単位）・明示改行（`\n`）・行頭禁則（行頭へ置かない文字は前の行へ送る）。
- **行の高さは行内で最も大きい文字に合わせます**（書式が混ざっても行の間隔が揃う）。 空行は本文の既定の書式で高さを決めます。
- **`lineHeightFactor` は行間の倍率**です（1 未満・非有限は 1）。

## 5. `TextLinePlan` / `TextSegmentPlan` / `TextLineMetrics` / `TextCaretRect`（レコード）

| 型 | 内容 |
| --- | --- |
| `TextLinePlan(Bounds, Segments)` | 行の矩形（元画像ピクセル）と、書式ごとの区間 |
| `TextSegmentPlan(Text, Style, Bounds)` | 区間の文字列・書式・矩形（**区間の原点**。 キャレットはこの原点からの距離で求める） |
| `TextLineMetrics(Height, Baseline)` | 行の実寸（高さとベースライン）。 UI が測って返す |
| `TextCaretRect(X, Y, Height)` | キャレットの矩形（元画像ピクセル） |

## 6. `TextLineIndex`（クラス）

**本文の行と文字位置の対応**（キャレット・選択範囲・位置から文字の特定）です。 折り返しは呼び出し側が解決し、本クラスは解決済みの行と本文の文字列から対応だけを求めます。

| メンバー | 説明 |
| --- | --- |
| `Create(lines, plainText)` | 行と本文から対応を作る（行が無い場合は `null`） |
| `Lines` / `TextLength` | 行の並び（上から下）・本文の文字数 |
| `LineTextLength(line)` | 行に含まれる文字数（改行は含めない） |
| `LineStart(lineIndex)` / `LineIndexOf(index)` | 行の開始位置 / 文字位置が入る行 |
| `MeasurePrefix(line, count, measureText)` | 行の先頭から指定した文字数までの幅（区間の原点からの距離の積み上げ） |
| `IndexAt(point, measureText)` | 位置に最も近い文字位置 |
| `ResolveCaret(index, measureText)` / `ResolveSelectionRects(start, end, measureText)` | キャレットの矩形 / 選択範囲の矩形（行ごとに分割する） |

- **区間の原点からの距離で測ります**（行頭から積み上げると、区間の境界・末尾の空白・丸めの分だけ文字からずれる）。
- **改行は 1 文字を消費します**（行の文字数には含めないが、文字位置としては 1 つ進む）。

## 7. `TextLineLayout`（静的クラス）

**行の中の文字位置と座標の対応**（単一所有）です。 `TextLineIndex` と `TextLineLayout` は同じ規則を使います。

| メンバー | 説明 |
| --- | --- |
| `MeasurePrefix`（デリゲート） | 区間と文字数から幅を返す関数（元画像ピクセル） |
| `Length(line)` | 行に含まれる文字数（区間の数ではない） |
| `Locate(line, offsetInLine)` | 行の先頭からの文字位置が入る区間と、その区間の先頭からの文字数 |
| `ResolveX(line, offsetInLine, measurePrefix)` | 行の先頭からの文字位置の X 座標 |
| `ResolveOffset(line, x, measurePrefix)` | X 座標に最も近い文字位置 |
| `ResolveBaseline(line, measureMetrics)` | 行のベースライン（行内で最も大きい文字を基準にする） |

- **区間の境界は次の区間へ属させます**（境界のキャレットは次に描かれる文字の左端）。
- **候補はクラスタの境界だけ**にします（半分に割れた位置へキャレットを置かない）。

## 8. `TextCursor`（静的クラス）

**文字位置を書記素クラスタ（絵文字・結合文字）で扱う規則**（単一所有）です。 キャレットの移動・選択・削除・折り返し・書式の範囲はすべてこの境界へ揃えます。

| メンバー | 説明 |
| --- | --- |
| `IsClusterBoundary(text, index)` | 指定した位置がクラスタの境界かどうか |
| `SnapBackward(text, index)` / `SnapForward(text, index)` | 指定した位置以下の・以上の最も近い境界 |
| `Next(text, index)` / `Previous(text, index)` | 次・前のクラスタの境界 |
| `ResolveDelete(text, caret, forward)` | 削除する範囲（開始位置と長さ）。 前方は次の 1 クラスタ、後方は前の 1 クラスタ |

- **サロゲート ペア・結合文字を半分に割りません**（`char` 単位で削ると文字列が壊れるため）。

## 9. `TextStyleEdit`（静的クラス）

**本文へ入力する文字列の挿入**です。

| メンバー | 説明 |
| --- | --- |
| `ResolveRange(selectionStart, selectionLength)` | 書式を適用する範囲（選択が無い場合は `null`） |
| `Insert(text, start, length, inserted, typingStyle)` | 指定範囲を文字列で置き換える（**以降の入力へ効かせる書式**があれば挿入した文字へ適用する） |

- **選択が無いときの書式変更は「以降の入力」へ効かせます**（既存の文字は変えない）。 選択の有無を持つのは呼び出し側です。

## 10. `TextCompositionPreview` / `TextCaretBlink`（静的クラス）

| メンバー | 説明 |
| --- | --- |
| `TextCompositionPreview.Apply(text, caret, preedit)` | IME の変換中の文字列を入れた描画用の値（**本文は変えない**） |
| `TextCompositionPreview.ResolveCaret(caret, preedit)` | 変換中の文字列を入れたときのキャレットの位置 |
| `TextCaretBlink.IntervalMilliseconds` / `IsVisible(elapsed)` | キャレットの点滅の間隔（`530` ms）と、経過時間からの表示の有無 |

## 11. `TextInput`（静的クラス）

**本文へ入れる文字列の正規化**（単一所有）です。 本文の改行は `\n` だけであり、折り返し・行の対応・保存する JSON が同じ規則を使います。 入力・貼り付け・IME の確定文字列は改行の表し方が異なるため、本文へ入れる入口（`TextEditor.ReplaceSelection` / `ReplaceRange`）がここで揃えます（呼び出し側は揃えません）。

| メンバー | 説明 |
| --- | --- |
| `Normalize(text)` | 改行を本文の規則（`\n`）へ揃える（`\r\n` と `\r` を `\n` にする） |

## 12. `TextEditState`（クラス）

**本文の編集（キャレット・選択・入力・削除・書式）の状態と規則**です。 **編集する対象（文書・識別子）を知りません**。

| メンバー | 説明 |
| --- | --- |
| `Begin(textLength)` / `End()` / `Reset()` | 編集の開始（キャレットは本文の末尾）・終了 / 状態の破棄（対象が変わったとき） |
| `IsActive` | 編集中かどうか |
| `FormatBarMinimumSelectionLength` / `ShouldShowFormatBar(autoShow)` | 書式バーを表示する最小の選択文字数（1）と、表示すべきかどうか（編集中・自動表示が有効・1 文字以上を選択） |
| `CaretIndex` / `AnchorIndex` / `SelectionStart` / `SelectionEnd` / `SelectionLength` / `SelectionRange` / `HasSelection` | キャレットと選択 |
| `TypingStyle` | 以降の入力へ効かせる書式（未設定は `null`） |
| `MoveTo(index, textLength, extend)` / `MoveToCluster(text, index, extend)` | キャレットを移す（`extend` なら選択を広げる）。 `MoveToCluster` は書記素クラスタの境界へ揃える |
| `ResolveClusterTarget(direction, extend)` | 前後の 1 クラスタへ動くときの移動先（**選択があるときは選択の端へ畳む**） |
| `MoveByLine(lines, textLength, delta, extend)` | 上下の行の同じ桁へ移す |
| `MoveToLineEdge(lines, textLength, first, extend)` | 行の先頭・末尾へ移す（行が無ければ本文の先頭・末尾） |
| `SetCaretFromPoint(lines, point, textLength, extend, measureText)` | 位置に最も近い文字位置へ移す |
| `SnapCaretToCluster(text, extend)` | キャレットを書記素クラスタの境界へ揃える |
| `Select(start, length, textLength)` / `Select(text, start, length)` / `SelectAll(text)` | 選択範囲の指定（本文を渡す版は端を書記素クラスタの境界へ揃える） / 本文全体の選択 |
| `Clamp(textLength)` | 選択範囲を本文の範囲へ収める |
| `Remember(style)` | 以降の入力へ効かせる書式を覚える |
| `ReplaceSelection(text, inserted)` / `ReplaceRange(text, start, end, inserted)` | 選択範囲（指定範囲）を文字列で置き換えた本文を返す |
| `Delete(text, forward)` | 選択範囲、またはキャレットの前後の **1 書記素クラスタ**を削除した本文を返す |
| `ApplyStyle(text, style)` / `TransformStyle(text, transform)` | 選択範囲の書式を差し替える / 1 属性だけ変換する（選択が無ければ以降の入力へ回す） |
| `AllMatch(text, match)` / `ResolveDisplayStyle(text)` | 書式ボタンの押下状態 / 書式バーへ表示する書式 |
| `GetSelectedText(text)` | 選択中の文字列（選択が無ければ空） |

- **本文は不変**です。 編集は新しい `RichText` を返し、変化が無い場合は**同じ実体**を返します。
- **選択が無いときの書式変更は以降の入力へ回します**（既存の文字は変えない）。
- **削除・移動の単位は書記素クラスタ**です（サロゲート ペア・結合文字を半分に割らない）。 本文を渡す版（`MoveToCluster` / `Select` / `SnapCaretToCluster` / `GetSelectedText`）は境界への揃えを本クラスが行い、UI と注釈の両方が同じ規則を使う。
- **書式バーの表示条件は本クラスが単一所有**します（`ShouldShowFormatBar`。 UI へは `TextEditor.ShouldShowFormatBar` が公開する）。 編集中・自動表示が有効・1 文字以上を選択の 3 つを満たす間だけ表示し、適用先が決まらない間は出しません。

## 13. `TextEditor`（クラス）

**編集中の本文（値・キャレット・選択・書式・変換中の文字列）の状態と操作**（単一所有）です。 UI は本クラスを操作し、描画と通知だけを行います（値と状態の写しを UI ごとに持ちません）。

| メンバー | 説明 |
| --- | --- |
| `Text` / `EditState` | 編集している本文 / キャレット・選択・書式の状態（ホストと共有できる） |
| `PreeditText` / `DisplayText` / `DisplayCaretIndex` | IME の変換中の文字列（本文へは入れない）・描画用の本文・描画用のキャレット |
| `ShowFormatBarWhileEditing` / `ShouldShowFormatBar` | 書式バーの自動表示の設定 / 表示すべきか（編集中・自動表示が有効・1 文字以上を選択） |
| `Begin()` / `End()` | 編集の開始（キャレットは本文の末尾）と終了 |
| `Select` / `SelectRange` / `SelectAll` / `MoveCaret` / `MoveCaretByCluster` / `MoveCaretByLine` / `MoveCaretToLineEdge` / `SetCaretFromPoint` / `ClampCaret` | キャレットと選択（端は書記素クラスタの境界へ揃える） |
| `ReplaceSelection` / `ReplaceRange` / `DeleteAtCaret` | 文字列の挿入・置き換え・削除 |
| `ApplyStyle` / `TransformStyle` / `SelectedText` / `SelectedTextMatches` / `SelectedTextStyle` | 書式の適用・選択文字の取得・書式ボタンの状態 |
| `TryExecute(request, resolveLines)` | キーの割り当てが返した操作のうち本文の操作を実行する（クリップボードと編集の終了は UI が行う） |
| `ResolveLines` / `ResolveLineIndex` / `ResolveCaretRect` | 折り返しと行の配置・行と文字位置の対応・キャレットの矩形（元画像ピクセル） |

- **操作は変わった内容（`TextEditChange`）を返します**（`Text` / `Selection` / `Editing`）。 UI は値に応じて描画の更新と通知を行います。
- **本文は不変**です。 編集は新しい `RichText` を `Text` へ書き戻し、変化が無ければ同じ実体のままです。
- **改行の正規化は挿入の入口が行います**（`ReplaceSelection` / `ReplaceRange` が `TextInput.Normalize` を適用する）。 入力・IME・貼り付けのどの経路からも同じ本文になります。
- **変換中の文字列は本文へ入れません**（`DisplayText` と `DisplayCaretIndex` にだけ反映する）。

## 14. `TextEditKeyMap` / `TextEditKey` / `TextEditModifiers` / `TextEditCommand`（静的クラス・列挙型）

**キーの割り当て（どのキーがどの操作になるか）の単一所有**です。 UI は自分のキーの型を `TextEditKey` / `TextEditModifiers` へ写し、`Resolve` が返した操作を実行します。

| メンバー | 説明 |
| --- | --- |
| `TextEditKeyMap.Resolve(key, modifiers)` | 押されたキーと修飾キーから `TextEditRequest` を解決する（割り当てが無ければ `None`） |
| `TextEditRequest.Command` / `Extend` / `IsHandled` | 実行する操作 / 選択を広げるか / 操作があるか |
| `TextEditKey` | 本層が扱うキー（`A` / `B` / `C` / `I` / `U` / `V` / `X` / `Digit5` / `Escape` / `Enter` / `Back` / `Delete` / `Left` / `Right` / `Up` / `Down` / `Home` / `End`） |
| `TextEditModifiers` | `Control` / `Shift` / `Alt`（ビットフラグ） |
| `TextEditCommand` | `SelectAll` / `Copy` / `Cut` / `Paste` / `EndEditing` / `InsertLineBreak` / `DeleteBackward` / `DeleteForward` / `CaretLeft` / `CaretRight` / `CaretUp` / `CaretDown` / `CaretLineStart` / `CaretLineEnd` / `ToggleBold` / `ToggleItalic` / `ToggleUnderline` / `ToggleStrike` |

| キー | 操作 |
| --- | --- |
| `Control` + `B` / `I` / `U` | 太字 / 斜体 / 下線の切替（`Shift`・`Alt` を伴うときは割り当てなし） |
| `Alt` + `Shift` + `5` | 取り消し線の切替 |
| `Escape` / `Control` + `Enter` | 編集の終了 |
| `Enter` | 改行の挿入 |
| `Back` / `Delete` | 前 / 次の 1 クラスタの削除 |
| 矢印 / `Home` / `End` | キャレットの移動（`Shift` を押すと選択を広げる） |
| `Control` + `A` / `C` / `X` / `V` | すべて選択 / コピー / 切り取り / 貼り付け |

- **クリップボードと編集の終了は UI が実行します**（UI の仕組みを要するため、本層は要求だけを返す）。

## 15. `TextDefaults` / `TextLimits`（静的クラス）

| メンバー | 説明 |
| --- | --- |
| `TextDefaults.PreferredFontFamilies` | 使うフォントの優先順（`Yu Gothic UI` → `Segoe UI`） |
| `TextDefaults.FontSizeDefault` / `DefaultStyle` | 文字の大きさの既定値（20 px）・既定の書式 |
| `TextLimits.FontSizeMin` / `FontSizeMax` / `CoerceFontSize(value)` | 文字の大きさの安全域（`6〜512`）と補正。 数値入力コントロールへ渡す値のために `FontSizeMinDecimal` / `FontSizeMaxDecimal`（`decimal`）も持つ |
| `TextLimits.CoercePositive(value, fallback)` | 正の大きさを返す（0 以下・非有限は `fallback`） |

- **値は元画像ピクセル**です（画面の DIP ではない）。
- **既定値は 1 か所に置きます**（設定ページの初期値・新規作成時の値・JSON の欠落値が同じ値になる）。
