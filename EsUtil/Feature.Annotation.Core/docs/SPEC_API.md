# EsUtil.Feature.Annotation.Core API リファレンス

`EsUtil.Feature.Annotation` は、画像に重ねる注釈（枠線・吹き出し）の Core と Presenter です。 UI フレームワークに依存しません。

## 目次

- [1. 公開 API の可視性](#1-公開-api-の可視性)
- [2. `AnnotationDocument`（クラス）](#2-annotationdocumentクラス)
- [3. `AnnotationItem` / `FrameAnnotation` / `BubbleAnnotation`（クラス）](#3-annotationitem--frameannotation--bubbleannotationクラス)
- [4. `BubbleTail` / `BubbleEdge`（レコード・列挙型）](#4-bubbletail--bubbleedgeレコード列挙型)
- [5. `RichText` / `TextRun` / `AnnotationTextStyle`（クラス・レコード）](#5-richtext--textrun--annotationtextstyleクラスレコード)
- [6. `AnnotationAppearance`（レコード）](#6-annotationappearanceレコード)
- [7. `AnnotationPresenter` / `AnnotationDrawPlan`（クラス・レコード）](#7-annotationpresenter--annotationdrawplanクラスレコード)
- [8. `AnnotationHitTest`（静的クラス）](#8-annotationhittest静的クラス)
- [9. `AnnotationJson`（静的クラス）](#9-annotationjson静的クラス)
- [10. `AnnotationDefaults` / `AnnotationLimits`（静的クラス）](#10-annotationdefaults--annotationlimits静的クラス)
- [11. `AnnotationDraftDefaults`（レコード）](#11-annotationdraftdefaultsレコード)
- [12. `AnnotationHistory`（クラス）](#12-annotationhistoryクラス)

## 1. 公開 API の可視性

| 型 | 可視性 | 参照可能なアセンブリ |
| --- | --- | --- |
| 注釈のモデル（`AnnotationDocument` / `AnnotationItem` / `FrameAnnotation` / `BubbleAnnotation` / `BubbleTail` / `RichText` / `TextRun` / `AnnotationTextStyle` / `AnnotationAppearance`） | `public` | 任意 |
| 描画計画（`AnnotationPresenter` / `AnnotationDrawPlan` / `AnnotationShapePlan` / `AnnotationTextLinePlan` / `AnnotationTextSegmentPlan` / `AnnotationKind`） | `public` | 任意 |
| 判断・保存（`AnnotationHitTest` / `AnnotationJson` / `AnnotationDefaults` / `AnnotationLimits` / `BubbleEdge`） | `public` | 任意 |
| `AnnotationJsonContext` / 折り返し / JSON 変換器 | `internal` | 同一アセンブリとテスト |

## 2. `AnnotationDocument`（クラス）

注釈の集合です。 **並び順が重なり順**（先頭が最背面、末尾が最前面）です。

| メンバー | 説明 |
| --- | --- |
| `SchemaVersion` | JSON のスキーマ版（既定は `CurrentSchemaVersion`） |
| `ImageWidth` / `ImageHeight` / `ImageFileName` | 元画像の寸法とファイル名（再編集の手掛かり） |
| `Items` | 注釈の並び（不変。 変更は下のメソッドが新しい文書を返す） |
| `Add(item)` / `Replace(item)` / `Remove(id)` | 追加・差し替え・削除（**元の文書は変更しない**） |
| `Find(id)` / `IndexOf(id)` | 識別子からの取得（無い場合は null / -1） |
| `BringToFront(id)` / `SendToBack(id)` | 重なり順の変更（最前面・最背面へ移す） |
| `WithText(bubble, text)` | 吹き出しの本文だけを差し替える |
| `Clone()` | 同じ内容の複製を作る（注釈も複製する。 変更履歴に使う） |

## 3. `AnnotationItem` / `FrameAnnotation` / `BubbleAnnotation`（クラス）

| 型 | 内容 |
| --- | --- |
| `AnnotationItem`（抽象） | `Id` / `Bounds`（元画像ピクセル）/ `Appearance`。 JSON では `Kind`（`Frame` / `Bubble`）で型を判別する。 `Clone()` は複製を返す（派生型は参照型の値も複製する） |
| `FrameAnnotation` | 枠線の注釈（矩形 + 角丸。 入れ子の二重枠・破線・楕円・矢印は持たない） |
| `BubbleAnnotation` | 吹き出し（`Text` / `Tail` / `BodyPadding` / `ResolveBodyRect()`） |

- **本文の矩形**は枠線の太さの半分と余白を内側へ寄せて求めます（`ResolveBodyRect`）。
- **型の追加は JSON の `Kind` を増やす**ことになります（保存済みファイルとの互換に注意）。

## 4. `BubbleTail` / `BubbleEdge`（レコード・列挙型）

| メンバー | 説明 |
| --- | --- |
| `Tip` | しっぽの先端（絶対座標。 **本体と独立して動かせる**） |
| `BaseEdge` / `BaseOffset` / `Width` | 付け根の辺・辺上の比率（0〜1）・付け根の幅（本体に追従する） |
| `ResolveBase(body)` | 本体の矩形から付け根の 2 点を求める |
| `Normalized()` | 幅と比率を安全域へ補正する |

## 5. `RichText` / `TextRun` / `AnnotationTextStyle`（クラス・レコード）

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

## 6. `AnnotationAppearance`（レコード）

| メンバー | 説明 |
| --- | --- |
| `Border` / `BorderThickness` | 枠線の色と太さ（元画像ピクセル） |
| `Fill` | 内側の色（透明なら塗らない） |
| `CornerRadius` | 角丸の半径（元画像ピクセル。 設定可能） |
| `Default` / `Normalized()` | 既定の外観 / 太さと角丸の補正 |

## 7. `AnnotationPresenter` / `AnnotationDrawPlan`（クラス・レコード）

| メンバー | 説明 |
| --- | --- |
| `ResolveDrawPlan(document, measureText, selectedId)` | 1 フレーム分の描画計画を解決する。 `measureText` は文字列と書式から実寸を測る関数（UI が渡す） |
| `LineHeightFactor` | 行間の倍率（既定 `LineHeightFactorDefault`） |
| `AnnotationDrawPlan` | `ImageSize` / `SelectedId` / `Shapes`（先頭が最背面） |
| `AnnotationShapePlan` | 角丸・塗り・枠線・しっぽの多角形・本文の行（すべて解決済み） |
| `AnnotationTextLinePlan` / `AnnotationTextSegmentPlan` | 行の矩形と、書式ごとの区間（下線・取り消し線は書式のフラグから UI が描く） |

- **折り返しは本層が単一所有**します（文字単位・明示改行・行頭禁則）。 4 つの UI 版で同じ位置に折り返します。
- **選択枠（ハンドル）は含みません**（Marquee の提示層が解決する。 選択枠の見た目を 4 つの UI 版で揃えるため）。

## 8. `AnnotationHitTest`（静的クラス）

| メンバー | 説明 |
| --- | --- |
| `HitTest(document, point)` | 指定位置の**最も前面**の注釈（無ければ null） |
| `HitTestTail(document, point, radius)` | しっぽの先端を持つ吹き出し（**本体より優先**して掴む） |
| `TailHandleRadiusDefault` | 先端を掴む既定の半径（元画像ピクセル） |

## 9. `AnnotationJson`（静的クラス）

| メンバー | 説明 |
| --- | --- |
| `Serialize(document)` / `Deserialize(json)` | JSON の読み書き（ソース生成 + 明示の変換器。 反射を使わない） |
| `GetDocumentPath(imagePath)` | `<画像名>.annotations.json` のパス（拡張子を置き換える） |
| `GetComposedPath(imagePath)` | `<画像名>.annotated.<拡張子>` のパス（拡張子を保つ） |
| `FileSuffix` | 注釈ファイルの接尾辞（`.annotations.json`） |

- **矩形・点は `X` / `Y` / `Width` / `Height` だけ**、色は **`#AARRGGBB`** で保存します。
- **未知の種別は読み込み時に例外**になります（前方互換はスキーマ版で扱う）。

## 10. `AnnotationDefaults` / `AnnotationLimits`（静的クラス）

| メンバー | 説明 |
| --- | --- |
| `AnnotationDefaults.PreferredFontFamilies` | 使うフォントの優先順（`Yu Gothic UI` → `Segoe UI`） |
| `AnnotationDefaults.FrameAppearance` / `BubbleAppearance` / `TextStyle` | 新規作成時の既定（設定ページの初期値と同じ値） |
| `AnnotationDefaults.*Default` | 文字の大きさ・枠線の太さ・角丸・余白・しっぽの幅の既定値 |
| `AnnotationLimits` | 各値の安全域と補正（枠線 `1〜64`・角丸 `0〜4096`・文字 `6〜512`） |

## 11. `AnnotationDraftDefaults`（レコード）

**新規作成時の既定値**（設定ページ・サンプルが変える値）。 既存の注釈には影響しません（各注釈が自分の外観・書式を持ちます）。

| メンバー | 説明 |
| --- | --- |
| `FrameAppearance` / `BubbleAppearance` | 枠線・吹き出しの既定の外観（枠線の色・太さ・内側の色・角丸） |
| `TextStyle` | 本文の既定の書式 |
| `BodyPadding` / `TailWidth` / `TailEdge` | 本文の余白・しっぽの幅・しっぽを出す辺 |
| `Default` | `AnnotationDefaults` と同じ値 |

## 12. `AnnotationHistory`（クラス）

**注釈の変更履歴**（元に戻す・やり直す）。 文書の複製を積むため、ドラッグ中の書き換えで過去の状態が変わりません。

| メンバー | 説明 |
| --- | --- |
| `Capacity` / `UndoCount` / `RedoCount` | 保持数（既定 50）と、戻せる・やり直せる回数 |
| `CanUndo` / `CanRedo` | 戻せる・やり直せるかどうか |
| `Record(document)` | **変更の直前**の文書を記録する（やり直しは消える） |
| `Undo(current)` / `Redo(current)` | 戻した・やり直した文書を返す（できない場合は `null`。 現在の文書は反対側へ複製して保持する） |
| `Clear()` | 履歴を空にする（別の画像を読み込んだとき） |

- **記録の単位は呼び出し側が決めます**（UI は「追加・削除・移動・サイズ変更・しっぽの移動・外観・書式・重なり順」の 1 操作 = 1 手）。
- 同じ内容の操作（文字を変えずに確定しただけ等）は記録しません。
