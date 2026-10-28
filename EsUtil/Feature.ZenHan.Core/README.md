# EsUtil.Feature.ZenHan.Core

![NuGet](https://img.shields.io/nuget/v/EsUtil.Feature.ZenHan.Core.svg)
![Release](https://github.com/tomokuni/EsUtil/actions/workflows/release.yml/badge.svg)
![Build](https://github.com/tomokuni/EsUtil/actions/workflows/build.yml/badge.svg?branch=main)
![Platform](https://img.shields.io/badge/Platform-Portable-6E6E6E)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![AOT](https://img.shields.io/badge/AOT-Ready-00695C)
![License](https://img.shields.io/badge/License-MIT-3DA639)

**対応 OS**: 指定なし（`net10.0`）。 ただし **Windows 以外での動作は未検証**（OS 固有の API は使わない）。

日本語テキストの**全角・半角・ひらがな・カタカナ・記号**の相互変換を提供する UI 非依存のライブラリです。
名前空間は `EsUtil.Feature.ZenHan`、アセンブリ名は `EsUtil.Feature.ZenHan.Core` です（`EsUtil.Feature.Loupe.Core` などと同じく、**層の接尾辞はアセンブリ名だけに付ける**規則です。 型名 `ZenHanConverter` はパッケージ名と一致しません）。
変換の定義は CSV から生成した不変リスト（`Define.AllList`）として保持し、入力の正規化と組み合わせた複合メソッドを提供します。

## 1. 概要

- **変換の定義**: `ConvertDefine.csv`（カテゴリ・グループ・変換前・変換後・説明）から T4（`Generated.tt`）で C# を生成します。 生成物（`Generated.cs`）はコミットするため、ビルドに T4 の実行は要りません。
- **複合メソッド**: `ToHan`（数字・英字・記号・カナを半角へ）/ `ToZenWithKatakana`（全角化し半角カナを全角カタカナへ）/ `ToNormalize`（特殊空白・ダッシュの正規化と分離した濁点の合成）など。
- **拡張メンバー**: `"ＡＢＣ".ToHan()` のように文字列のインスタンス メソッドとして呼べます（C# 14 の `extension` ブロック）。
- **部分一致の置換**: `ConvertPairs` が変換元を文字列の中から探して置き換えます（変換元がすべて 1 文字なら文字の対応表、2 文字以上を含む場合は長い並びを優先する正規表現。 `Chain` で 2 段の連鎖変換もできます）。

## 2. 使い方

```csharp
using EsUtil.Feature.ZenHan;

// 数字・英字・記号・カナを半角へ
var han = ZenHanConverter.ToHan("ＡＢＣ１２３ｱｲｳ");   // "ABC123ｱｲｳ" のカナは半角カナ

// 全角化し、半角カナを全角カタカナへ
var zen = ZenHanConverter.ToZenWithKatakana("ABC123ｱｲｳ");  // "ＡＢＣ１２３アイウ"

// 正規化（特殊空白・ダッシュの統一、分離した濁点の合成）
var normalized = ZenHanConverter.ToNormalize("カ゛　\u2010");
```

### 2.1 拡張メンバーとして呼ぶ

```csharp
using EsUtil.Feature.ZenHan;

var han = "ＡＢＣ１２３".ToHan();
var zen = "ABC123".ToZenWithKatakana();
```

### 2.2 変換の定義を引く

```csharp
using EsUtil.Feature.ZenHan;

// 変換元の文字から定義を引く（前方一致のキー）
var entry = EntryRecord.GetEntry("Ascii|Alphabet|Large|Ａ");
var all = Define.AllList;   // CSV から生成した全エントリ
```

## 3. 含まれるもの

`ZenHanConverter`（変換メソッド）・`ConvertPairs`（変換ペアの集合）・`EntryRecord`（1 件分の定義）・`Define`（生成した定義の一覧）・`Helper`（Unicode 表記の復号）・`ZenHanConverterExtensions`（拡張メンバー）。

## 4. 依存

なし（EsUtil のパッケージも NuGet パッケージも参照しません）。 規則は [`.github/依存関係.md`](../../.github/依存関係.md)。

## 5. 対応フレームワーク

| 項目 | 内容 |
| --- | --- |
| 対象フレームワーク | `net10.0` |
| 言語 | C# 14（.NET 10） |
| Native AOT | 対応（`IsAotCompatible=true`。 AOT の解析警告が 0 件であることをビルドで検査する） |

## 6. テスト

`tests/`（xUnit v3）。 変換メソッドごとの代表例（全角・半角・カナ・記号・空白）・定義の生成（CSV の件数と先頭エントリ）・キーからの取得・連鎖変換・不正なキーの例外を検証します。
手順は [`.github/検証手順.md`](../../.github/検証手順.md)。

## 7. ドキュメント

- 層の帰属 … [`../README.md`](../README.md)
- 依存の許可範囲 … [`.github/依存関係.md`](../../.github/依存関係.md)
- API リファレンス … [`docs/SPEC_API.md`](docs/SPEC_API.md)
- システム仕様書 … [`docs/SPEC_System.md`](docs/SPEC_System.md)

## 8. ライセンス

[MIT License](LICENSE)
