# EsUtil.Ui.WinUI3.StyledText API リファレンス

書式付き文字列を表示し、その場で編集する コントロールです。 折り返し・行の配置・キャレットの位置・削除の単位は `EsUtil.Feature.StyledText.Core` が単一所有します。

## 目次

- [1. `StyledTextControl`（クラス）](#1-styledtextcontrolクラス)
- [2. 編集の API](#2-編集の-api)
- [3. 制限事項](#3-制限事項)

## 1. `StyledTextControl`（クラス）

| メンバー | 説明 |
| --- | --- |
| `Text` | 表示・編集する本文（`RichText`。 run 単位の書式） |
| `MeasureText` | 文字列と書式から実寸を測る関数（既定は描画と同じエンジンで測る） |
| `LineHeightFactor` | 行間の倍率（1 未満・非有限は 1） |
| `RenderScale` | 画面の DIP への倍率（1 未満・非有限は 1） |
| `EditState` | キャレット・選択・以降の入力へ効かせる書式の状態（`TextEditState`） |
| `PreeditText` | IME の変換中の文字列（本文へは入れず、キャレットの位置へ描く） |
| `TextChanged` / `SelectionChanged` / `EditingChanged` | 本文・選択範囲・編集の開始と終了の通知 |

- **座標は元画像ピクセル**です。 `RenderScale` を掛けて DIP へ写します。
- **本文は `Text` が単一所有**します。 編集は新しい `RichText` を `Text` へ書き戻し、`TextChanged` を発生させます。
- **状態を 2 つ持たない**ため、ホストが状態を持っている場合は `EditState` に同じ実体を設定します。

## 2. 編集の API

| メンバー | 説明 |
| --- | --- |
| `IsEditing` / `BeginEditing()` / `EndEditing()` | 編集の状態（開始時のキャレットは本文の末尾）。 本文を押すと編集が始まり、押した位置へキャレットを置く |
| `CaretIndex` / `SelectionStart` / `SelectionEnd` / `SelectionLength` / `HasSelection` | キャレットと選択 |
| `Select(start, length)` / `SelectAll()` | 選択範囲の指定 |
| `MoveCaret(index, extend)` | キャレットの移動（書記素クラスタの境界へ丸める） |
| `MoveCaretByCluster(direction, extend)` | 前後の 1 クラスタへ移動（選択は端へ畳む） |
| `MoveCaretByLine(delta, extend)` / `MoveCaretToLineEdge(first, extend)` | 上下の行・行の先頭と末尾 |
| `SetCaretFromPoint(point, extend)` | 位置（元画像ピクセル）に最も近い文字位置 |
| `ReplaceSelection(text)` / `ReplaceRange(start, end, text)` | 文字列の挿入・置き換え |
| `DeleteAtCaret(forward)` | 選択範囲、または前後の 1 クラスタの削除 |
| `ApplyTextStyle(style)` / `ApplyTextStyle(start, length, style)` | 選択範囲（指定範囲）の書式の差し替え |
| `TransformTextStyle(transform)` / `TransformTextStyle(start, length, transform)` | 1 属性だけの変換 |
| `SelectedTextStyle` / `SelectedTextMatches(match)` | 書式バーへ表示する値・押下状態 |
| `SelectedText()` / `CopyAsync()` / `CutAsync()` / `PasteAsync()` | 選択文字とクリップボード |
| `CaretRect` | キャレットの矩形（DIP。 IME の変換候補の位置） |

- **選択が無いときの書式変更は以降の入力へ回します**（既存の文字は変えない）。
- **キー入力は同じ API を呼びます**（キーの割り当ては `EsUtil.Feature.StyledText.Core` の `TextEditKeyMap` が単一所有する）。

## 3. 制限事項

- **IME は制御できません**（変換候補の表示位置と内容は OS と入力方式が決める）。 本コントロールは位置と文字列を渡すだけです。
- **キャレットと選択範囲の色・太さは変更できません**（`EsUtil.Feature.StyledText.Core` の `TextEditorAppearance` が単一所有する。 4 つの UI 版で同じ見た目にするため）。
- **書式は run 単位**です（文字単位の任意の属性は持たない）。
