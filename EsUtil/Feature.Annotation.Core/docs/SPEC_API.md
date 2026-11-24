# EsUtil.Feature.Annotation.Core API リファレンス

`EsUtil.Feature.Annotation` は、画像に重ねる注釈（枠線・吹き出し）の Core と Presenter です。 UI フレームワークに依存しません。

## 目次

- [1. 公開 API の可視性](#1-公開-api-の可視性)
- [2. `AnnotationCore`（クラス）](#2-annotationcoreクラス)
- [3. `AnnotationDocument`（クラス）](#3-annotationdocumentクラス)
- [4. `AnnotationItem` / `FrameAnnotation` / `BubbleAnnotation`（クラス）](#4-annotationitem--frameannotation--bubbleannotationクラス)
- [5. `BubbleTail` / `BubbleEdge`（レコード・列挙型）](#5-bubbletail--bubbleedgeレコード列挙型)
- [6. `RichText` / `TextRun` / `AnnotationTextStyle`（クラス・レコード）](#6-richtext--textrun--annotationtextstyleクラスレコード)
- [7. `AnnotationAppearance`（レコード）](#7-annotationappearanceレコード)
- [8. `AnnotationPresenter` / `AnnotationDrawPlan`（クラス・レコード）](#8-annotationpresenter--annotationdrawplanクラスレコード)
- [9. `AnnotationHitTest`（静的クラス）](#9-annotationhittest静的クラス)
- [10. `AnnotationJson`（静的クラス）](#10-annotationjson静的クラス)
- [11. `AnnotationDefaults` / `AnnotationLimits`（静的クラス）](#11-annotationdefaults--annotationlimits静的クラス)
- [12. `AnnotationDraftDefaults`（レコード）](#12-annotationdraftdefaultsレコード)
- [13. `AnnotationHistory`（クラス）](#13-annotationhistoryクラス)
- [14. 層が共有する値（クラス）](#14-層が共有する値クラス)

## 1. 公開 API の可視性

公開面（ファサードのメンバーに現れる型）は名前空間 `EsUtil.Feature.Annotation` に置きます。 4 つの UI 版が共通で使う型も `public` ですが、層の内部の名前空間（`EsUtil.Feature.Annotation.Core.<カテゴリ>` / `.Presenter.<カテゴリ>`）へ置きます（フォルダと名前空間の一覧は [`../README.md`](../README.md) §3）。

| 型 | 可視性 | 参照可能なアセンブリ |
| --- | --- | --- |
| ファサード（`AnnotationCore` / `AnnotationPresenter`） | `public` | 任意 |
| 注釈のモデル（`AnnotationDocument` / `AnnotationItem` / `FrameAnnotation` / `BubbleAnnotation` / `BubbleTail` / `RichText` / `TextRun` / `AnnotationTextStyle` / `AnnotationAppearance`） | `public` | 任意 |
| 描画計画（`AnnotationDrawPlan` / `AnnotationShapePlan` / `AnnotationTextLinePlan` / `AnnotationTextSegmentPlan` / `AnnotationKind`） | `public` | 任意 |
| 判断・保存（`AnnotationHitTest` / `AnnotationJson` / `AnnotationDefaults` / `AnnotationLimits` / `BubbleEdge`） | `public` | 任意 |
| UI との契約（`AnnotationPreviewGesture` / `AnnotationPreviewGestureEventArgs` / `AnnotationStyleEditorRequestedEventArgs` / `AnnotationAddMenuRequestedEventArgs` / `AnnotationPointerKind`） | `public` | UI 層 |
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
| `SaveDocument(imagePath, document)` | 元画像のパスを指定して書き出す（注釈ファイルのパスは本クラスが決める。 ホストはパスを組み立てない） |
| `SaveOrRemoveCompanionFiles(imagePath, document, saveComposedImage)` | 注釈が 0 件なら付随ファイルを削除し、あれば注釈ファイルを書いてから合成画像を書き出す（描画は `saveComposedImage` が行う） |
| `RemoveCompanionFiles(imagePath)` | 注釈ファイルと合成画像を削除する（注釈が 0 件になったときの後始末。 1 つでも消した場合は `true`） |

- **文書の値操作（差し替え・削除・並び順・しっぽの移動）は `AnnotationDocument`** が、**当たり判定は `AnnotationHitTest`** が持ちます（ファサードは入口を集約するだけで、値の操作を写し取らない）。
- **ログ**: 生成（`Information`）・文書の作成（`Debug`）・文書の読み書きと付随ファイルの削除（`Information`）・注釈の作成（`Debug`）を記録します。 毎フレームの描画計画の解決と当たり判定は記録しません。

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
| `WithText(bubble, text)` | 吹き出しの本文だけを差し替える（新しい文書を返す） |
| `WithBounds(id, bounds)` | 矩形を画像の内側へ収めた文書を返す（しっぽも同じ移動量だけ追従する） |
| `WithTailTip(id, tip)` / `WithTail(id, tail)` | しっぽの先端・しっぽ全体を画像の内側へ収めた文書を返す（幅と付け根の比率も安全域へ補正する） |
| `WithAppearance(id, appearance)` / `WithBodyPadding(id, padding)` | 外観・本文の余白を安全域へ補正した文書を返す |
| `WithText(bubbleId, text)` | 吹き出しの本文を差し替えた文書を返す |

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

## 6. `RichText` / `TextRun` / `AnnotationTextStyle`（クラス・レコード）

| メンバー | 説明 |
| --- | --- |
| `RichText.Runs` | `TextRun` の並び（**run が自分の文字列を持つ**。 文字の挿入・削除が他の書式へ影響しない） |
| `RichText.PlainText` / `IsEmpty` / `Create(text, style)` | 書式を除いた文字列・空判定・単一書式からの生成 |
| `RichText.ApplyStyle(start, length, style)` | **指定範囲の書式を差し替える**（run を境界で分割し、同じ書式は連結する。 長さ 0 は全体） |
| `RichText.TransformStyle(start, length, transform)` | **指定範囲の書式を 1 文字ずつ変換する**（変えない属性を保つ。 「太字にする」「色だけ変える」はこちら） |
| `RichText.StyleAt(index)` | 指定位置の文字の書式（範囲外は末尾へフォールバック。 書式ツールバーの初期表示に使う） |
| `RichText.WithPlainText(text)` | 書式を**文字位置で引き継いで**文字列だけを差し替える（本文の編集に使う） |
| `RichText.Insert(index, text)` | 指定位置へ文字列を挿入する（**直前の文字**の書式を引き継ぐ） |
| `RichText.Remove(start, length)` | 指定範囲の文字を除く（残りの書式は保つ） |
| `AnnotationTextStyle` | `FontFamily` / `FontSize`（元画像ピクセル）/ `Color` / `IsBold` / `IsItalic` / `IsUnderline` / `IsStrike` |
| `AnnotationTextStyle.Default` / `Normalized()` | 既定の書式（`Yu Gothic UI` 優先）/ 安全域への補正 |

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
| `AnnotationTextLinePlan` / `AnnotationTextSegmentPlan` | 行の矩形と、書式ごとの区間（下線・取り消し線は書式のフラグから UI が描く） |

- **折り返しは本層が単一所有**します（文字単位・明示改行・行頭禁則）。 4 つの UI 版で同じ位置に折り返します。
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
| `FileSuffix` | 注釈ファイルの接尾辞（`.annotations.json`） |

- **ファイルの入出力は `AnnotationCore.Load` / `AnnotationCore.Save`** が行います（文字コードは UTF-8・BOM なし。 件数を記録し、失敗は `Error` で記録して再スローする）。 本クラスは変換・パスの解決・付随ファイルの判定だけを持ちます（`Load` / `Save` は `internal`）。

- **矩形・点は `X` / `Y` / `Width` / `Height` だけ**、色は **`#AARRGGBB`** で保存します。
- **未知の種別は読み込み時に例外**になります（前方互換はスキーマ版で扱う）。

## 11. `AnnotationDefaults` / `AnnotationLimits`（静的クラス）

| メンバー | 説明 |
| --- | --- |
| `AnnotationDefaults.PreferredFontFamilies` | 使うフォントの優先順（`Yu Gothic UI` → `Segoe UI`） |
| `AnnotationDefaults.FrameAppearance` / `BubbleAppearance` / `TextStyle` | 新規作成時の既定（設定ページの初期値と同じ値） |
| `AnnotationDefaults.*Default` | 文字の大きさ・枠線の太さ・角丸・余白・しっぽの幅・新規作成する注釈の大きさ（`NewItemWidthDefault` / `NewItemHeightDefault`）の既定値 |
| `AnnotationLimits` | 各値の安全域と補正（枠線 `1〜64`・角丸 `0〜4096`・文字 `6〜512`） |

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
| `AnnotationTextEdit` | Presenter（`.Presenter.Selection`） | 本文の編集状態（キャレット・選択・入力・削除・書式）。 UI は入力イベントを渡し、通知と描画だけを行う |
| `AnnotationViewTransform` | Core（`.Core.Geometry`） | 表示倍率・左上の位置と、元画像ピクセルとローカル DIP の相互変換。 UI は点の型へ写すだけにする |

- **`AnnotationDraftDefaultsFactory` は「設定の項目名」を知りません**（`AnnotationFrameBorderColor` のような設定の項目名はホストのもの）。 引数は値だけを受け取り、対応付け（枠線の太さ・角丸を枠線と吹き出しの両方へ使う等）だけを持ちます。
- **`AnnotationEditorAppearance` の値は元画像ピクセルではありません**（画面の DIP。 倍率は UI が掛ける）。
