# EsUtil.Feature.ZenHanConverter API リファレンス

`EsUtil.Feature.ZenHanConverter` の公開 API です。

## 目次 <!-- omit from toc -->

- [1. 前提条件](#1-前提条件)
- [2. 公開 API](#2-公開-api)
  - [2.1 `ZenHanConverter`（変換メソッド）](#21-zenhanconverter変換メソッド)
  - [2.2 `ZenHanConverterExtensions`（拡張メンバー）](#22-zenhanconverterextensions拡張メンバー)
  - [2.3 `ConvertPairs`（変換ペアの集合）](#23-convertpairs変換ペアの集合)
  - [2.4 `EntryRecord`（1 件分の定義）](#24-entryrecord1-件分の定義)
  - [2.5 `Define`（生成した定義の一覧）](#25-define生成した定義の一覧)
  - [2.6 `Helper`](#26-helper)
- [3. 契約（利用者が守る・受け取る値）](#3-契約利用者が守る受け取る値)
- [4. 使用例](#4-使用例)
- [5. 制限事項](#5-制限事項)

## 1. 前提条件

- **対象の文字**: 日本語（全角英数字・半角カナ・全角カナ・ひらがな・記号）です。 絵文字や結合文字の正規化は行いません。
- **文字列の正規化**: `ToNormalize` は特殊空白・各種ダッシュの統一と、分離した濁点・半濁点の合成だけを行います（Unicode の NFKC 正規化ではありません）。
- **スレッド**: 変換の定義（`Define` と `EntryRecord` のキャッシュ）は不変またはスレッド セーフです。 `ConvertPairs` は内部でキャッシュを遅延初期化します。

## 2. 公開 API

名前空間は `EsUtil.Feature.Text` です（アセンブリ名は `EsUtil.Feature.ZenHanConverter`。 型名 `ZenHanConverter` と名前空間セグメントを一致させない規則 `MA0049` のため、名前空間は `EsUtil.Feature.Text` です）。

### 2.1 `ZenHanConverter`（変換メソッド）

すべて静的メソッドで、文字列を返します。

| メソッド | 内容 |
| --- | --- |
| `ToNormalize(string text)` | 特殊空白・各種ダッシュを正規化し、分離した濁点・半濁点を合成します。 |
| `ToHan(string text)` | 数字・英字・記号・カナを半角へ統一します。 |
| `ToZenWithKatakana(string text)` | 全角化し、半角カナを全角カタカナへ統一します。 |
| `ToZenWithHiragana(string text)` | 全角化し、カナをひらがなへ統一します。 |
| `ToHanOnlyAscii(string text)` | ASCII（英数字・記号）だけを半角へ統一します。 |
| `ToZenOnlyAscii(string text)` | ASCII（英数字・記号）だけを全角へ統一します。 |
| `ToHanOnlyKana(string text)` | カナ（ひらがな・カタカナ）だけを半角カナへ統一します。 |
| `ToHanOnlyKatakana(string text)` | カタカナだけを半角カナへ統一します。 |
| `ToZenOnlyKatakana(string text)` | カタカナだけを全角カタカナへ統一します。 |
| `ToZenKatakanaOnlyKana(string text)` | カナだけを全角カタカナへ統一します。 |
| `ToZenHiraganaOnlyKana(string text)` | カナだけをひらがなへ統一します。 |
| `ToUpperCase(string text)` | 英字を大文字へ統一します。 |
| `ToLowerCase(string text)` | 英字を小文字へ統一します。 |
| `ConvertTabToSpace(string text)` | タブを空白へ置き換えます。 |
| `ConvertBackslashToHanYen(string text)` | バックスラッシュを半角の円記号（`¥`）へ置き換えます。 |

### 2.2 `ZenHanConverterExtensions`（拡張メンバー）

`string` のインスタンス メソッドとして同じ変換を呼べます（C# 14 の `extension` ブロック。 `using EsUtil.Feature.Text;` だけで使えます）。 メソッド名は 2.1 と同じです（例: `"ＡＢＣ".ToHan()`）。

### 2.3 `ConvertPairs`（変換ペアの集合）

変換元 → 変換先の組を保持し、文字列の置換を行います（`IEnumerable<(string Source, string Target)>`）。

| メンバー | 内容 |
| --- | --- |
| `ConvertPairs(IEnumerable<(string Source, string Target)> pairs)` | 組を渡して生成します。 |
| `Empty` | 空の集合。 |
| `FromForward(EntryRecord entry)` / `FromForward(string key)` | 定義の「変換前 → 変換後」の組を作ります。 |
| `FromInverse(EntryRecord entry)` / `FromInverse(string key)` | 定義の「変換後 → 変換前」の組を作ります。 |
| `FromFunc(string key, Func<IEnumerable<(string Source, string Target)>> func)` | 遅延生成の組を作ります。 |
| `Concat(params IEnumerable<(string Source, string Target)>[] pairs)` | 複数の組を連結します。 |
| `operator +` / `Add(left, right)` | 2 つの集合を連結します。 |
| `Chain(ConvertPairs second, bool includeUnmatchedFirst, bool includeUnmatchedSecond)` | 2 段の連鎖変換を組み立てます（1 段目の変換先を 2 段目の変換元として使う）。 |
| `Chain(ConvertPairs second)` / `ChainMerge(ConvertPairs second)` | 連鎖の簡易形（前者は一致しない要素を捨て、後者は残す）。 |
| `Convert(string text)` | 保持している組で文字列を置換します（長い変換元から順に一致させます）。 |

### 2.4 `EntryRecord`（1 件分の定義）

1 件分の変換定義です（`Category` / `Group` / `SubGroup` / `Forward` / `Inverse` / `Source` / `Target` / `Name` / `Summary`）。

| メンバー | 内容 |
| --- | --- |
| `EntryRecord(string Category, string Group, string SubGroup, string Forward, string Inverse, string Source, string Target, string Name, string Summary)` | 各項目を渡して生成します。 |
| `EntryRecord((string, …) t)` / `implicit operator EntryRecord` / `ToEntryRecord(t)` | タプルから生成します。 |
| `Deconstruct(out string Source, out string Target)` | 変換元と変換先だけを取り出します。 |
| `Deconstruct(out string Category, … out string Summary)` | すべての項目を取り出します。 |
| `GetEntryList(string entryKey)` | キーに一致する定義の一覧を返します（`Forward\|Category\|Group\|SubGroup\|Name` の階層）。 |
| `GetEntry(string entryKey)` | キーに一致する定義を 1 件返します。 |
| `GetEntryKey(string forward, …)` | 定義を引くためのキーを組み立てます（引数の数だけ階層を深くします）。 |

### 2.5 `Define`（生成した定義の一覧）

CSV から生成した定義です（`Generated.cs`。 T4 で生成）。

| メンバー | 内容 |
| --- | --- |
| `Define.AllList` | CSV の全エントリ（`ImmutableList<EntryRecord>`）。 初回アクセス時に生成します。 |
| `Define.GroupOf_*` | カテゴリ・グループごとの定義の集まり（例: `Define.GroupOf_Ascii_Alphabet_Large`）。 変換の対象を絞るときに使います。 |

### 2.6 `Helper`

| メンバー | 内容 |
| --- | --- |
| `DecodeUnicodeNotation(string s)` | `"U+3042"` 形式の表記を対応する文字へ復号します（CSV の定義で使う表記）。 |

## 3. 契約（利用者が守る・受け取る値）

- **`null` は受け付けません**（`ArgumentNullException`）。
- **変換は冪等ではありません**。 例: `ToHan` の後に `ToZenWithKatakana` を適用すると、元の文字と一致しない場合があります（半角カナは全角カタカナへ寄るため）。
- **`GetEntry` はキーが見つからない場合に例外を送出します**（`KeyNotFoundException`）。 存在が不確かな場合は `GetEntryList` を使います。
- **キーの区切りは `|`** です（例: `Ascii|Alphabet|Large|Ａ`）。

## 4. 使用例

```csharp
using EsUtil.Feature.Text;

var pairs = ConvertPairs.FromForward(Define.GroupOf_Ascii_Alphabet_Large);
var han = pairs.Convert("ＡＢＣ");          // "ABC"

// 2 段の連鎖（全角英字 → 半角英字 → 大文字）
var chained = ConvertPairs.FromForward(Define.GroupOf_Ascii)
    .Chain(ConvertPairs.FromForward(Define.GroupOf_Ascii_Alphabet_Large));
```

## 5. 制限事項

- **Unicode の正規化（NFKC など）は行いません**。 正規化が必要な場合は `string.Normalize` を併用してください。
- **変換の定義は CSV が単一所有します**。 定義を変えるときは `ConvertDefine.csv` を直して `Generated.tt` を再生成します（生成物もコミットします）。
- **`Define.GroupOf_*` の型名は CSV から機械的に作られます**（`_` を含みます）。 名前はデータに追随するため、識別子の命名規則の例外として扱います（[`.editorconfig`](../../../.editorconfig) のファイル スコープの抑止）。
