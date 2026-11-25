# EsUtil.Feature.Annotation.Core API リファレンス

`EsUtil.Feature.Annotation` は、画像に重ねる注釈（枠線・吹き出し）の Core と Presenter です。 UI フレームワークに依存しません。

## 目次

- [1. 公開 API の可視性](#1-公開-api-の可視性)
- [2. `AnnotationCore`（クラス）](#2-annotationcoreクラス)
- [3. `AnnotationDocument`（クラス）](#3-annotationdocumentクラス)
- [4. `AnnotationItem` / `FrameAnnotation` / `BubbleAnnotation`（クラス）](#4-annotationitem--frameannotation--bubbleannotationクラス)
- [5. `BubbleTail` / `BubbleEdge`（レコード・列挙型）](#5-bubbletail--bubbleedgeレコード列挙型)
- [6. 文字の書式（`EsUtil.Feature.StyledText`）](#6-文字の書式esutilfeaturestyledtext)
- [7. `AnnotationAppearance`（レコード）](#7-annotationappearanceレコード)
- [8. `AnnotationPresenter` / `AnnotationDrawPlan`（クラス・レコード）](#8-annotationpresenter--annotationdrawplanクラスレコード)
- [9. `AnnotationHitTest`（静的クラス）](#9-annotationhittest静的クラス)
- [10. `AnnotationJson`（静的クラス）](#10-annotationjson静的クラス)
- [11. `AnnotationDefaults` / `AnnotationLimits`（静的クラス）](#11-annotationdefaults--annotationlimits静的クラス)
- [12. `AnnotationDraftDefaults`（レコード）](#12-annotationdraftdefaultsレコード)
- [13. `AnnotationHistory`（クラス）](#13-annotationhistoryクラス)
- [14. 層が共有する値（クラス）](#14-層が共有する値クラス)
- [15. `AnnotationEditSession` / `AnnotationEditChange`（クラス・列挙型）](#15-annotationeditsession--annotationeditchangeクラス列挙型)

## 1. 公開 API の可視性

公開面（ファサードのメンバーに現れる型）は名前空間 `EsUtil.Feature.Annotation` に置きます。 4 つの UI 版が共通で使う型も `public` ですが、層の内部の名前空間（`EsUtil.Feature.Annotation.Core.<カテゴリ>` / `.Presenter.<カテゴリ>`）へ置きます（フォルダと名前空間の一覧は [`../README.md`](../README.md) §3）。

| 型 | 可視性 | 参照可能なアセンブリ |
| --- | --- | --- |
| ファサード（`AnnotationCore` / `AnnotationPresenter`） | `public` | 任意 |
| 注釈のモデル（`AnnotationDocument` / `AnnotationItem` / `FrameAnnotation` / `BubbleAnnotation` / `BubbleTail` / `AnnotationAppearance`） | `public` | 任意 |
| 描画計画（`AnnotationDrawPlan` / `AnnotationShapePlan` / `AnnotationKind`） | `public` | 任意 |
| 判断・保存（`AnnotationHitTest` / `AnnotationJson` / `AnnotationDefaults` / `AnnotationLimits` / `BubbleEdge`） | `public` | 任意 |
| UI との契約（`AnnotationPreviewGesture` / `AnnotationPreviewGestureEventArgs` / `AnnotationStyleEditorRequestedEventArgs` / `AnnotationAddMenuRequestedEventArgs` / `AnnotationPointerKind`） | `public` | UI 層 |
| 編集セッション（`AnnotationEditSession` / `AnnotationEditChange`） | `public` | UI 層 |
| `AnnotationJsonContext` / 折り返し / JSON 変換器 | `internal` | 同一アセンブリとテスト |

## 2. `AnnotationCore`（クラス）

**注釈の操作を UI 層・ホストへ提供するファサード**です。 文書と注釈の生成・JSON の読み書きを行い、ログの定義（`AnnotationLogMessage`）の接頭辞と出力先を所有します。 描画計画の解決は `Presenter`（`AnnotationPresenter`）が担います。

| メンバー | 説明 |
| --- | --- |
| `Presenter` | 描画計画を解決する提示層（アセンブリに 1 つ。 UI と共有する） |
| `Log` / `LogPrefix` | ログの出力先と接頭辞（コアと提示層で共有する。 既定 `[Annotation]`） |
| `CreateDocument(width, height, fileName)` | 注釈を持たない文書を作る |
| `CreateFrame(document, bounds, defaults)` | 枠線の注釈を作る（文書へは追加しない。 矩形は画像の内側へ収める） |
| `CreateBubble(document, bounds, text, defaults, tail)` | 吹き出しの注釈を作る（文書へは追加しない。 矩形としっぽを画像の内側へ収める） |
| `Load(documentPath)` / `Save(documentPath, document)` | 注釈ファイルの読み書き（UTF-8・BOM なし。 件数を `Information` で記録する） |
| `SaveForImage(imagePath, document)` | 元画像のパスを指定して書き出す（注釈ファイルのパスは本クラスが決める。 ホストはパスを組み立てない） |
| `SaveOrRemoveCompanionFiles(imagePath, document, saveComposedImage)` | 注釈が 0 件なら付随ファイルを削除し、あれば注釈ファイルを書いてから合成画像を書き出す（描画は `saveComposedImage` が行う） |
| `RemoveCompanionFiles(imagePath)` | 注釈ファイルと合成画像を削除する（注釈が 0 件になったときの後始末。 1 つでも消した場合は `true`） |

- **文書の値操作（差し替え・削除・並び順・しっぽの移動）は `AnnotationDocument`** が、**当たり判定は `AnnotationHitTest`** が持ちます（ファサードは入口を集約するだけで、値の操作を写し取らない）。
- **ログ**: 生成（`Debug`。 ファサードも提示層もプログラムが作るもの）・文書の作成（`Debug`）・文書の読み書きと付随ファイルの削除（`Information`。 利用者の明示的な操作）・注釈の作成（`Debug`）を記録します。 毎フレームの描画計画の解決と当たり判定は記録しません。

## 3. `AnnotationDocument`（クラス）

注釈の集合です。 **並び順が重なり順**（先頭が最背面、末尾が最前面）です。

| メンバー | 説明 |
| --- | --- |
| `SchemaVersion` | JSON のスキーマ版（既定は `CurrentSchemaVersion`） |
| `CurrentSchemaVersion` | 現在の JSON スキーマの版（`1`） |
| `ImageWidth` / `ImageHeight` / `ImageFileName` | 元画像の寸法とファイル名（再編集の手掛かり） |
| `ImageSize` | 画像の大きさ（1 未満は 1 として扱う。 JSON には書かない） |
| `Items` | 注釈の並び（先頭が最背面、末尾が最前面） |
| `Add(item)` / `Replace(item)` / `Remove(id)` | 追加・差し替え・削除（**元の文書は変更しない**） |
| `Find(id)` / `IndexOf(id)` | 識別子からの取得（無い場合は null / -1） |
| `BringToFront(id)` / `SendToBack(id)` | 重なり順の変更（最前面・最背面へ移す。 新しい文書を返す） |
| `WithText(bubbleId, text)` | 吹き出しの本文だけを差し替える（新しい文書を返す） |
| `WithBounds(id, bounds)` | 矩形を画像の内側へ収めた文書を返す（しっぽも同じ移動量だけ追従する） |
| `WithTailTip(id, tip)` / `WithTail(id, tail)` | しっぽの先端・しっぽ全体を画像の内側へ収めた文書を返す（幅と付け根の比率も安全域へ補正する） |
| `WithAppearance(id, appearance)` / `WithBodyPadding(id, padding)` | 外観・本文の余白を安全域へ補正した文書を返す |

- **文書は不変です**。 すべての変更は新しい文書を返し、変化が無い場合は**同じ実体**を返します（`ReferenceEquals` で変更の有無を判定できる）。 矩形・しっぽ・外観・余白の検証（画像の内側へ収める・安全域へ補正する）は `WithBounds` / `WithTail` / `WithAppearance` / `WithBodyPadding` が単一所有します。

## 4. `AnnotationItem` / `FrameAnnotation` / `BubbleAnnotation`（クラス）

| 型 | 内容 |
| --- | --- |
| `AnnotationItem`（抽象・不変） | `Id` / `Bounds`（元画像ピクセル）/ `Appearance`。 JSON では `Kind`（`Frame` / `Bubble`）で型を判別する。 変更は `with` 式で新しい値を作る |
| `FrameAnnotation` | 枠線の注釈（矩形 + 角丸。 入れ子の二重枠・破線・楕円・矢印は持たない） |
| `BubbleAnnotation` | 吹き出し（`Text` / `Tail` / `BodyPadding` / `ResolveBodyRect()`） |

- **本文の矩形**は枠線の太さの半分と余白を内側へ寄せて求めます（`ResolveBodyRect`）。
- **型の追加は JSON の `Kind` を増やす**ことになります（保存済みファイルとの互換に注意）。

## 5. `BubbleTail` / `BubbleEdge`（レコード・列挙型）

| メンバー | 説明 |
| --- | --- |
| `Tip` | しっぽの先端（絶対座標。 **本体と独立して動かせる**） |
| `BaseEdge` / `BaseOffset` / `Width` | 付け根の辺・辺上の比率（0〜1）・付け根の幅（本体に追従する） |
| `ResolveBase(body)` | 本体の矩形から付け根の 2 点を求める |
| `Normalized()` | 幅と比率を安全域へ補正する |

## 6. 文字の書式（`EsUtil.Feature.StyledText`）

**本文の文字は `EsUtil.Feature.StyledText.Core`（名前空間 `EsUtil.Feature.StyledText`）が単一所有します**。 本アセンブリは型を写さず、そのまま公開面に載せます。

| 型 | 内容 |
| --- | --- |
| `RichText` / `TextRun` / `TextStyle` | 書式付きの文字列（run 単位の書式）・書式そのもの |
| `TextLinePlan` / `TextSegmentPlan` | 行の矩形と書式ごとの区間（`AnnotationShapePlan.Lines` の要素） |

- メンバー単位の仕様は [`../../Feature.StyledText.Core/docs/SPEC_API.md`](../../Feature.StyledText.Core/docs/SPEC_API.md) が所有します。
- **本アセンブリの値（本文・描画計画）に現れる型は上表の 5 つだけ**です（書記素クラスタ・折り返し・行の対応は提示層が呼びます）。

## 7. `AnnotationAppearance`（レコード）

| メンバー | 説明 |
| --- | --- |
| `Border` / `BorderThickness` | 枠線の色と太さ（元画像ピクセル） |
| `Fill` | 内側の色（透明なら塗らない） |
| `CornerRadius` | 角丸の半径（元画像ピクセル。 設定可能） |
| `Default` / `Normalized()` | 既定の外観 / 太さと角丸の補正 |

## 8. `AnnotationPresenter` / `AnnotationDrawPlan`（クラス・レコード）

| メンバー | 説明 |
| --- | --- |
| `ResolveDrawPlan(document, measureText, selectedId)` | 1 フレーム分の描画計画を解決する。 `measureText` は文字列と書式から実寸を測る関数（UI が渡す） |
| `LineHeightFactor` | 行間の倍率（既定 `LineHeightFactorDefault`。 0 以下・非有限は既定値へ補正する） |
| `AnnotationDrawPlan` | `ImageSize` / `SelectedId` / `Shapes`（先頭が最背面） |
| `AnnotationShapePlan` | 角丸・塗り・枠線・しっぽの多角形・本文の行（すべて解決済み） |
| `TextLinePlan` / `TextSegmentPlan` | 行の矩形と、書式ごとの区間（`EsUtil.Feature.StyledText.Core` が所有する。 下線・取り消し線は書式のフラグから UI が描く） |

- **折り返しは `EsUtil.Feature.StyledText.Core` の `TextLayout` が単一所有**します（文字単位・明示改行・行頭禁則）。 4 つの UI 版で同じ位置に折り返します。
- **選択枠（ハンドル）は含みません**（Marquee の提示層が解決する。 選択枠の見た目を 4 つの UI 版で揃えるため）。

## 9. `AnnotationHitTest`（静的クラス）

| メンバー | 説明 |
| --- | --- |
| `HitTest(document, point)` | 指定位置の**最も前面**の注釈（無ければ null） |
| `HitTestTail(document, point, radius)` | しっぽの先端を持つ吹き出し（**本体より優先**して掴む） |
| `TailHandleRadiusDefault` | 先端を掴む既定の半径（元画像ピクセル） |

## 10. `AnnotationJson`（静的クラス）

| メンバー | 説明 |
| --- | --- |
| `Serialize(document)` / `Deserialize(json)` | JSON 文字列への変換（ソース生成 + 明示の変換器。 反射を使わない） |
| `GetDocumentPath(imagePath)` | `<画像名>.annotations.json` のパス（拡張子を置き換える） |
| `GetComposedPath(imagePath)` | `<画像名>.annotated.<拡張子>` のパス（拡張子を保つ） |
| `IsCompanionFile(path)` | 注釈の付随ファイル（注釈ファイル・合成画像）かどうか（名前の規則だけで判定する） |
| `ResolveImagePath(documentPath, document)` | 注釈ファイルから元画像のパスを求める（同じフォルダの `ImageFileName` → 注釈ファイルの名前から逆算。 見つからない場合は `null`） |
| `FileSuffix` / `ComposedInfix` | 注釈ファイルの接尾辞（`.annotations.json`）/ 合成画像へ挟む目印（`.annotated`） |

- **ファイルの入出力は `AnnotationCore.Load` / `AnnotationCore.Save`** が行います（文字コードは UTF-8・BOM なし。 件数を記録し、失敗は `Error` で記録して再スローする）。 本クラスは変換・パスの解決・付随ファイルの判定だけを持ちます（`Load` / `Save` は `internal`）。
- **`ResolveImagePath` だけはファイルの存在を確かめます**（元画像が無ければ注釈を表示できないため、候補を順に試す必要がある）。 書き込みは行いません。

- **矩形・点は `X` / `Y` / `Width` / `Height` だけ**、色は **`#AARRGGBB`** で保存します。
- **未知の種別は読み込み時に例外**になります（前方互換はスキーマ版で扱う）。

## 11. `AnnotationDefaults` / `AnnotationLimits`（静的クラス）

| メンバー | 説明 |
| --- | --- |
| `AnnotationDefaults.FrameAppearance` / `BubbleAppearance` | 新規作成時の既定の外観（設定ページの初期値と同じ値） |
| `AnnotationDefaults.*Default` | 枠線の太さ・角丸・余白・しっぽの幅・新規作成する注釈の大きさ（`NewItemWidthDefault` / `NewItemHeightDefault`）の既定値（文字は `TextDefaults` が所有する） |
| `AnnotationLimits` | 各値の安全域と補正（枠線 `1〜64`・角丸 `0〜4096`・余白 `0〜256`・しっぽの幅 `2〜512`）。 文字の安全域は `TextLimits`（`6〜512`）が所有する。 数値入力コントロールへ渡す値のために `…Decimal`（`double` と同じ値）も持つ |

## 12. `AnnotationDraftDefaults`（レコード）

**新規作成時の既定値**（設定ページ・サンプルが変える値）。 既存の注釈には影響しません（各注釈が自分の外観・書式を持ちます）。

| メンバー | 説明 |
| --- | --- |
| `FrameAppearance` / `BubbleAppearance` | 枠線・吹き出しの既定の外観（枠線の色・太さ・内側の色・角丸） |
| `TextStyle` | 本文の既定の書式 |
| `BodyPadding` / `TailWidth` / `TailEdge` | 本文の余白・しっぽの幅・しっぽを出す辺 |
| `Default` | `AnnotationDefaults` と同じ値 |

## 13. `AnnotationHistory`（クラス）

**注釈の変更履歴**（元に戻す・やり直す）。 文書は不変であるため、記録した文書をそのまま積みます（ドラッグ中の書き換えで過去の状態が変わりません）。

| メンバー | 説明 |
| --- | --- |
| `Capacity` / `UndoCount` / `RedoCount` | 保持数（既定 50）と、戻せる・やり直せる回数 |
| `CanUndo` / `CanRedo` | 戻せる・やり直せるかどうか |
| `BeginChange(document)` / `CommitChange(document)` | 操作の開始で直前の文書を控え、終了で 1 手として積む（控え中の再呼び出しは何もしない） |
| `CancelChange()` | 控えを捨てる（操作を取り消したとき） |
| `Record(document)` | **変更の直前**の文書を記録する（やり直しは消える） |
| `Undo(current)` / `Redo(current)` | 戻した・やり直した文書を返す（できない場合は `null`。 現在の文書は反対側へ保持する） |
| `Clear()` | 履歴を空にする（別の画像を読み込んだとき） |

- **記録の単位は `BeginChange` 〜 `CommitChange` の 1 組**です（UI は「押下から解放まで」を 1 手として囲む）。 ドラッグ中の細かな書き換えは `Trace` の記録だけを残し、履歴は 1 手にまとまります。
- 同じ内容の操作（文字を変えずに確定しただけ等）は `CommitChange` が保存形式の比較で判定し、記録しません。

## 14. 層が共有する値（クラス）

**複数の層・4 つの UI 版が同じ値を使うための単一所有**です。 値を UI ごとに書くと、片方だけ色や対応付けがずれます。

| 型 | 層 | 説明 |
| --- | --- | --- |
| `AnnotationDraftDefaultsFactory` | Core（`.Core.Defaults`） | 設定の値（色の文字列・数値）から `AnnotationDraftDefaults` を組み立てる（`FromText`）。 解釈できない色は透明として扱い、数値は `AnnotationLimits` の安全域へ補正する |
| `AnnotationEditorAppearance` | Presenter（`.Presenter.Drawing`） | 本文の編集中の見た目（`CaretThicknessDip` / `CaretColor` / `SelectionColor`）。 UI は自分の描画 API の色へ変換して使う |
| `TextEditor` | `EsUtil.Feature.StyledText.Core`（`.Presenter.Editing`） | 本文の値と編集の状態（キャレット・選択・入力・削除・書式）。 UI は同じ実体を `StyledTextControl.Editor` へ渡し、通知と描画だけを行う（注釈の識別子を持たない） |
| `AnnotationViewTransform` | Core（`.Core.Geometry`） | 表示倍率・左上の位置と、元画像ピクセルとローカル DIP の相互変換（`ToSourceRadius` で画面上の掴む距離を元画像ピクセルへ換算する）。 UI は点の型へ写すだけにする |
| `TextCursor` / `TextLayout` / `TextLineIndex` / `TextLineLayout` / `TextStyleEdit` / `TextCompositionPreview` / `TextCaretBlink` | `EsUtil.Feature.StyledText.Core`（`.Core.Text`） | 書記素クラスタの境界・折り返しと行の配置・行と文字位置の対応・入力の挿入・変換中の文字列・点滅。 UI は位置を計算せず、この規則に従う |

- **`AnnotationDraftDefaultsFactory` は「設定の項目名」を知りません**（`AnnotationFrameBorderColor` のような設定の項目名はホストのもの）。 引数は値だけを受け取り、対応付け（枠線の太さ・角丸を枠線と吹き出しの両方へ使う等）だけを持ちます。
- **`AnnotationEditorAppearance` の値は元画像ピクセルではありません**（画面の DIP。 倍率は UI が掛ける）。
- **文字位置は書記素クラスタの境界で扱います**（`TextCursor`）。 サロゲート ペア（絵文字）と結合文字を半分に割ると文字列が壊れるため、キャレットの移動・選択・削除・折り返し・書式の範囲はすべて同じ境界に揃えます。

## 15. `AnnotationEditSession` / `AnnotationEditChange`（クラス・列挙型）

**4 つの UI 版が同じ順序で文書を変更するための編集セッション**です。 文書・選択・変更履歴を単一所有し、本文の操作は `TextEditor` に委譲します（本型は編集対象の本文を選択中の注釈へ揃え、結果を文書へ書き戻します）。 UI は入力の受付と描画だけを行います。

| メンバー | 説明 |
| --- | --- |
| `Document` | 表示・編集する文書。 差し替えると選択・編集・履歴を解除する |
| `SelectedId` / `Selected` | 選択中の注釈（設定すると本文の編集を終え、書式の適用先を切り替える） |
| `DraftDefaults` | 新規作成時の既定値（既存の注釈には影響しない） |
| `Subscribe(notify)` | 変更（`Document` / `Selection` / `Editing` / `TextSelection`）の購読。 戻り値の破棄で解除する |
| `AddFrame(bounds)` / `AddBubble(bounds, text)` | 注釈の追加（画像の内側へ収め、追加した注釈を選択する） |
| `DeleteSelected()` / `SetSelectedText(text)` | 選択中の注釈の削除・本文の差し替え |
| `ApplyTextStyle(...)` / `TransformTextStyle(...)` | 選択範囲の書式の差し替え・変換（選択が無ければ以降の入力へ回す） |
| `UpdateSelectedAppearance(transform)` / `SetSelectedBodyPadding(padding)` / `UpdateSelectedTail(transform)` | 選択中の注釈の外観・余白・しっぽの変更 |
| `BringSelectedToFront()` / `SendSelectedToBack()` | 重なり順の変更（変わらない場合は `false` を返し、履歴に積まない） |
| `BeginChange()` / `SetBounds(bounds)` / `SetTailTip(tip)` / `CommitChange()` | ドラッグ 1 手の記録（押下で控え、移動のたびに反映し、最初の変更で 1 手として積む） |
| `Undo()` / `Redo()` / `CanUndo` / `CanRedo` | 変更履歴（編集中はキャレットを本文の範囲へ収める） |
| `BeginTextEditing()` / `EndTextEditing()` / `IsEditingText` | 本文の編集の開始・終了 |
| `Editor` | 本文の値と編集の状態（`TextEditor`。 キャレット・選択・入力・削除・書式）。 UI は同じ実体を本文コントロールへ渡す |
| `CommitTextEdit()` | 本文コントロールが `Editor` へ加えた変更を文書へ書き戻す（1 手として履歴へ積む） |
| `CaretIndex` / `SelectionStart` / `SelectionEnd` / `SelectionLength` / `HasTextSelection` | 本文のキャレットと選択（`TextEditor` の値） |
| `TextSelectionRange` | 書式の適用先の範囲（文字数）。 ホストが本文の入力欄の選択位置を写すときに使う |
| `NotifyTextSelectionChanged()` | 本文コントロールが行った選択の変更を記録して購読者へ通知する（同じ範囲では繰り返さない） |
| `SelectedTextStyle` / `SelectedTextMatches(match)` | 書式バーの表示値・押下状態 |

- **`AnnotationEditChange` はビットフラグ**です（1 回の操作で文書と選択が同時に変わることがあるため、種類ごとに購読を作らない）。
- **ログ**: 選択・本文の編集・本文の変更・書式の適用・履歴の移動を記録します。 コピー・切り取り・貼り付けは UI 層が `AnnotationLogMessage.LogTextOperation` で記録します（操作名・キャレット・選択・本文の先頭）。 毎フレームの描画計画の解決は記録しません。
