# EsUtil.Feature.StyledText.Core

![NuGet](https://img.shields.io/nuget/v/EsUtil.Feature.StyledText.Core.svg)
![Release](https://github.com/tomokuni/EsUtil/actions/workflows/release.yml/badge.svg)
![Build](https://github.com/tomokuni/EsUtil/actions/workflows/build.yml/badge.svg?branch=main)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%20(1809%2C%2017763)%2B-0078D6)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![AOT](https://img.shields.io/badge/AOT-Ready-00695C)
![License](https://img.shields.io/badge/License-MIT-3DA639)

**対応 OS**: Windows 専用（`net10.0-windows10.0.17763.0`）。 Windows 10 バージョン 1809（ビルド 17763）以降

書式付きの文字列（**run 単位の書式・折り返し・書記素クラスタ・行と文字位置の対応**）を扱う UI 非依存の計算層です。
名前空間は `EsUtil.Feature.StyledText`、アセンブリ名は `EsUtil.Feature.StyledText.Core` です。

**文字の実寸の計測だけを UI に委ねます**（描画エンジンによって字幅が違うため、`Func<string, TextStyle, EsSize>` として受け取ります）。 折り返しの位置・行の縦位置・キャレットの位置・削除の単位は本層が単一所有します。

## 1. 概要

- **書式は run 単位**である（`RichText` は `TextRun` の並び。 1 文字だけ色を変える等ができる）。
- **文字の大きさは元画像ピクセル**である（画面の DIP ではない。 表示倍率を変えても保存画像との比率が変わらない）。
- **文字位置は書記素クラスタの境界で扱う**（サロゲート ペア（絵文字）・結合文字を半分に割らない）。
- **折り返しと行の配置は 1 か所で決める**（`TextLayout`。 折り返しの位置と行の高さが食い違わない）。
- **不変**である（すべての変更は新しい値を返し、変化が無い場合は同じ実体を返す）。

## 2. 使い方

```csharp
using EsUtil.Feature.StyledText;
using EsUtil.Feature.StyledText.Core.Defaults;
using EsUtil.Feature.StyledText.Core.Text;
using EsUtil.Feature.StyledText.Presenter.Editing;
using EsUtil.Platform.Core.Geometry;

// 1. 書式付きの文字列を作る（run が自分の文字列と書式を持つ）
var text = RichText.Create("ここが重要です。", TextDefaults.DefaultStyle)
    .ApplyStyle(2, 2, TextDefaults.DefaultStyle with { IsBold = true });

// 2. 折り返して行へ配置する（文字の実寸だけを UI が測る）
var lines = TextLayout.ResolveLines(
    text,
    new EsRect(0, 0, 380, 170),
    (value, style) => Measure(value, style),
    lineHeightFactor: 1.2);

// 3. 行と文字位置の対応を作る（キャレット・選択範囲・位置から文字の特定）
var index = TextLineIndex.Create(lines, text.PlainText);
var caret = index?.ResolveCaret(4, (value, style) => Measure(value, style));

// 4. 入力・削除（変更の単位は書記素クラスタ）
var inserted = TextStyleEdit.Insert(text, start: 4, length: 0, inserted: "とても", typingStyle: null);
var (deleteStart, deleteLength) = TextCursor.ResolveDelete(inserted.PlainText, caret: 4, forward: false);

// 5. 文字の位置をクラスタの境界へ揃える
var snapped = TextCursor.SnapBackward(text.PlainText, 3);

// 6. 編集の状態（キャレット・選択・以降の入力へ効かせる書式）を持つ
var edit = new TextEditState();
edit.Begin(text.PlainText.Length);
var afterTyping = edit.ReplaceSelection(text, "追記");
var afterDelete = edit.Delete(afterTyping, forward: false);

// 7. キーの割り当ては本層が決める（UI は自分のキーの型から写して実行するだけ）
var request = TextEditKeyMap.Resolve(TextEditKey.Left, TextEditModifiers.Shift);
if (request.Command == TextEditCommand.CaretLeft)
{
    edit.MoveTo(edit.ResolveClusterTarget(-1, request.Extend), text.PlainText.Length, request.Extend);
}
```

主な API は次のとおりです。

| 型 | 役割 |
| --- | --- |
| `RichText` / `TextRun` / `TextStyle` | 書式付きの文字列（run 単位の書式）・書式そのもの（フォント・大きさ・色・装飾） |
| `TextLayout` | 折り返しと行の配置（本文の矩形 → 行の並び。 左揃え・縦は中央揃え） |
| `TextLinePlan` / `TextSegmentPlan` / `TextLineMetrics` / `TextCaretRect` | 行の矩形と書式ごとの区間・行の実寸・キャレットの矩形（UI へ渡す値） |
| `TextLineIndex` / `TextLineLayout` | 行と文字位置の対応（キャレット・選択範囲・位置から文字の特定。 区間の原点からの距離で測る） |
| `TextCursor` | 書記素クラスタの境界（移動・選択・削除・折り返しの単位） |
| `TextStyleEdit` | 本文へ入力する文字列の挿入（以降の入力へ効かせる書式の反映） |
| `TextCompositionPreview` / `TextCaretBlink` | IME の変換中文字列を入れた描画用の値・キャレットの点滅 |
| `TextEditState` | 本文の編集状態（キャレット・選択・入力・削除・書式）。 本文を受け取って書き換えた本文を返す（編集する対象を知らない） |
| `TextEditKeyMap` / `TextEditKey` / `TextEditModifiers` / `TextEditCommand` | キーの割り当て（どのキーがどの操作になるか）。 4 つの UI 版はキーの型を写して本層の解決を呼ぶ |
| `TextDefaults` / `TextLimits` | 既定値（フォントの優先順・大きさ・既定の書式）と安全域 |

## 3. 構成（名前空間とフォルダ）

公開面（利用者が直接使う型）は `src/` 直下の `EsUtil.Feature.StyledText` です。 層の内部は層フォルダへ置き、名前空間に層名を挾みます（規則は [`.github/命名規約.md`](../../.github/命名規約.md)）。

| フォルダ | 名前空間 | 置く型 |
| --- | --- | --- |
| `src/` 直下 | `EsUtil.Feature.StyledText` | 公開面（`RichText` / `TextRun` / `TextStyle` と、引数・戻り値に現れる値） |
| `src/Core/Defaults` | `.Core.Defaults` | 既定値と安全域（`TextDefaults` / `TextLimits`） |
| `src/Core/Text` | `.Core.Text` | 折り返しと行の配置・行と文字位置の対応・書記素クラスタ・入力の挿入・点滅・変換中の文字列 |
| `src/Core/Internal` | `.Core.Internal` | 折り返しの実装（`internal`） |
| `src/Presenter/Editing` | `.Presenter.Editing` | 本文の編集状態（キャレット・選択・入力・削除・書式）と、キーの割り当て（どのキーがどの操作か） |

## 4. 依存

`EsUtil.Platform.Core` だけを参照します（値型とエラー境界）。 UI フレームワークには依存しません。 規則は [`.github/依存関係.md`](../../.github/依存関係.md)。

## 5. テスト

```powershell
dotnet test EsUtil/Feature.StyledText.Core/tests/EsUtil.Feature.StyledText.Core.Tests.csproj
```

書式の適用範囲・書記素クラスタの境界・折り返しと行の配置・行と文字位置の対応を検証します。
手順と合否基準は [`.github/検証手順.md`](../../.github/検証手順.md) が単一所有します。

## 6. ドキュメント

- API リファレンス … [`docs/SPEC_API.md`](docs/SPEC_API.md)
- システム仕様書 … [`docs/SPEC_System.md`](docs/SPEC_System.md)
- 層の帰属 … [`EsUtil/README.md`](../../README.md)
- 依存の許可範囲 … [`.github/依存関係.md`](../../.github/依存関係.md)

## 7. ライセンス

[MIT License](LICENSE)
