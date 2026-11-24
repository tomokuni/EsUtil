# EsUtil.Ui.WinForms.StyledText

![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinForms.StyledText.svg)
![Release](https://github.com/tomokuni/EsUtil/actions/workflows/release.yml/badge.svg)
![Build](https://github.com/tomokuni/EsUtil/actions/workflows/build.yml/badge.svg?branch=main)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%20(1809%2C%2017763)%2B-0078D6)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![AOT](https://img.shields.io/badge/AOT-Ready-00695C)
![UI](https://img.shields.io/badge/UI-WinForms-8B44AC)
![License](https://img.shields.io/badge/License-MIT-3DA639)

書式付きの文字列（run 単位の色・太字・斜体・下線・取り消し線）を**表示し、その場で編集する** Avalonia UI 版のコントロールです。
入力欄を重ねず、本文を自分で描きます（書式が反映され、書式バーを操作してもフォーカスと選択が外れません）。

## 1. 概要

- **本文は `RichText`**（run 単位の書式）で受け渡しします。
- **座標と文字の大きさは元画像ピクセル**です（`RenderScale` で画面の DIP へ写す）。
- **折り返し・行の配置・キャレットの位置・削除の単位は `EsUtil.Feature.StyledText.Core` が単一所有**します（本コントロールは描画・入力・IME・クリップボードの配線だけを行います）。
- **すべての操作を API で行えます**（キー入力は同じ API を呼びます）。 IME だけは例外です（変換候補の表示は OS が決めるため）。

## 2. 機能一覧

| 機能 | 内容 |
| --- | --- |
| 表示 | run 単位の書式（色・太字・斜体・下線・取り消し線）で本文を描く |
| 編集 | 入力・削除・改行・キャレット移動・選択・すべて選択 |
| 書式 | 選択範囲への適用と 1 属性だけの切り替え（選択が無ければ以降の入力へ回す）。 書式バーは 1 文字以上を選んだときだけ出る |
| クリップボード | コピー・切り取り・貼り付け |
| 日本語入力 | IME の変換中の文字列をキャレットの位置へ描く（本文へは入れない） |
| 状態 | 本文の値とキャレット・選択は `EsUtil.Feature.StyledText.Core` の `TextEditor` が所有する（状態は `TextEditState`） |

## 3. システム要件

| 項目 | 内容 |
| --- | --- |
| 対象フレームワーク | `net10.0-windows10.0.17763.0` |
| UI フレームワーク | Windows Forms（.NET 10） |

## 4. インストール

```powershell
dotnet add package EsUtil.Ui.WinForms.StyledText
```

## 5. クイックスタート

```csharp
using EsUtil.Feature.StyledText;
using EsUtil.Feature.StyledText.Core.Defaults;
using EsUtil.Ui.WinForms.StyledText;

var editor = new StyledTextControl
{
    RichTextValue = RichText.Create("ここが重要です。", TextDefaults.DefaultStyle),
    LineHeightFactor = 1.2,
};

// 本文が変わったらホストの値を更新する
editor.RichTextChanged += (_, _) => Save(editor.RichTextValue);

// 編集を始める（キャレットは本文の末尾）
editor.BeginEditing();

// すべての操作は API からも行える（キー入力は同じ API を呼ぶ）
editor.Select(2, 4);
editor.ApplyTextStyle(TextDefaults.DefaultStyle with { IsBold = true });
```

## 6. 操作

| 操作 | 内容 |
| --- | --- |
| クリック | キャレットを置く（`Shift` を押しながらで選択を広げる） |
| ドラッグ | 選択する |
| `Enter` / `Control` + `Enter` | 改行 / 編集の終了 |
| `Escape` | 編集の終了 |
| `Control` + `B` / `I` / `U` | 太字 / 斜体 / 下線の切替 |
| `Alt` + `Shift` + `5` | 取り消し線の切替 |
| `Control` + `A` / `C` / `X` / `V` | すべて選択 / コピー / 切り取り / 貼り付け |
| 矢印・`Home`・`End` | キャレットの移動（`Shift` で選択を広げる） |

## 7. カスタマイズ

| メンバー | 内容 |
| --- | --- |
| `MeasureText` | 文字列と書式から実寸を測る関数（既定は描画と同じエンジンで測る） |
| `LineHeightFactor` | 行間の倍率 |
| `RenderScale` | 画面の DIP への倍率（拡大表示するとき） |
| `EditState` | キャレット・選択の状態（ホストが既に持っている場合は同じ実体を設定する） |
| `ShowFormatBarWhileEditing` | 編集中に書式バーを自動で表示するか（既定は `true`。 ホストが独自にバーを出す場合は `false`） |

キャレットと選択範囲の色・太さは `EsUtil.Feature.StyledText.Core` の `TextEditorAppearance` が単一所有します（4 つの UI 版で同じ見た目にするため）。

## 8. ログ

本コントロールはログを出力しません（表示と入力の部品であり、記録は利用側が行います）。 規則は [`.github/ログ規約.md`](../../../.github/ログ規約.md)。

## 9. 未対応

制限事項は [`docs/SPEC_API.md`](docs/SPEC_API.md) を参照してください。

## 10. サンプルアプリケーション

注釈のサンプル（[`../Annotation/sample`](../Annotation/sample)）が本コントロールを使って本文を表示・編集します。

## 11. ドキュメント

- API リファレンス … [`docs/SPEC_API.md`](docs/SPEC_API.md)
- システム仕様書 … [`docs/SPEC_System.md`](docs/SPEC_System.md)
- 層の帰属 … [`../../README.md`](../../README.md)
- 依存の許可範囲 … [`.github/依存関係.md`](../../../.github/依存関係.md)

## 12. プロジェクト構成

| フォルダ | 内容 |
| --- | --- |
| `src/` | コントロール本体 |
| `tests/` | 単体テスト（xUnit v3・Avalonia ヘッドレス） |
| `docs/` | API リファレンスとシステム仕様書 |

## 13. テスト

```powershell
dotnet test EsUtil.Ui.WinForms/StyledText/tests/EsUtil.Ui.WinForms.StyledText.Tests.csproj
```

手順と合否基準は [`.github/検証手順.md`](../../../.github/検証手順.md) が単一所有します。

## 14. ライセンス

[MIT License](LICENSE)
