# EsUtil.Ui.WinUI3.StyledText

![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinUI3.StyledText.svg)
![Release](https://github.com/tomokuni/EsUtil/actions/workflows/release.yml/badge.svg)
![Build](https://github.com/tomokuni/EsUtil/actions/workflows/build.yml/badge.svg?branch=main)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%20(1809%2C%2017763)%2B-0078D6)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![AOT](https://img.shields.io/badge/AOT-Ready-00695C)
![UI](https://img.shields.io/badge/UI-WinUI%203-0067C0)
![License](https://img.shields.io/badge/License-MIT-3DA639)

書式付きの文字列（run 単位の色・太字・斜体・下線・取り消し線）を**表示し、その場で編集する** WinUI 3 版のコントロールです。
入力欄を重ねず、本文を自分で描きます（書式が反映され、書式バーを操作してもフォーカスと選択が外れません）。

## 1. 概要

- **本文は `RichText`**（run 単位の書式）で受け渡しします。
- **座標と文字の大きさは元画像ピクセル**です（`SourceToClientScaling` で画面の DIP へ写す）。
- **折り返し・行の配置・キャレットの位置・削除の単位は `EsUtil.Feature.StyledText.Core` が単一所有**します（本コントロールは描画・入力・IME・クリップボードの配線だけを行います）。
- **すべての操作を API で行えます**（キー入力は同じ API を呼びます）。 IME だけは例外です（変換候補の表示は OS が決めるため）。

## 2. 機能一覧

| 機能 | 内容 |
| --- | --- |
| 表示 | run 単位の書式（色・太字・斜体・下線・取り消し線）で本文を描く |
| 編集 | 入力・削除・改行・キャレット移動・選択・すべて選択 |
| 書式 | 選択範囲への適用と 1 属性だけの切り替え（選択が無ければ以降の入力へ回す）。 書式バーは 1 文字以上を選んだときだけ出る（ホストが置くか、コントロールがポップアップで出すかを選べる） |
| クリップボード | コピー・切り取り・貼り付け |
| 日本語入力 | IME の変換中の文字列をキャレットの位置へ描く（本文へは入れない） |
| 状態 | 本文の値とキャレット・選択は `EsUtil.Feature.StyledText.Core` の `TextEditor` が所有する（状態は `TextEditState`） |

## 3. システム要件

| 項目 | 内容 |
| --- | --- |
| 対象フレームワーク | `net10.0-windows10.0.17763.0` |
| UI フレームワーク | WinUI 3（Windows App SDK） |

## 4. インストール

```powershell
dotnet add package EsUtil.Ui.WinUI3.StyledText
```

## 5. クイックスタート

```csharp
using EsUtil.Feature.StyledText;
using EsUtil.Feature.StyledText.Core.Defaults;
using EsUtil.Ui.WinUI3.StyledText;

var editor = new StyledTextControl
{
    RichTextValue = RichText.Create("ここが重要です。", TextDefaults.DefaultStyle),
    LineHeightFactor = 1.2,
};

// 本文が変わったらホストの値を更新する
editor.RichTextChanged += (_, _) => Save(editor.RichTextValue);

// 編集を始める（キャレットは本文の末尾）
editor.BeginEdit();

// すべての操作は API からも行える（キー入力は同じ API を呼ぶ）
editor.Select(2, 4);
editor.ApplyStyle(TextDefaults.DefaultStyle with { IsBold = true });
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
| `SourceToClientScaling` | 元画像ピクセルから画面の DIP への倍率（拡大表示するとき） |
| `Editor` | 本文の値と編集の状態（ホストが既に持っている場合は同じ実体を設定する） |
| `ShowFormatBarWhileEditing` | 編集中に書式バーを自動で表示するか（既定は `true`。 ホストが独自にバーを出す場合は `false`） |
| `FormatBarItems` | 書式バーに出す項目の組み合わせ（太字・斜体・下線・取り消し線・色・大きさ・フォント名）。 非表示の項目は詰めて並べ、すべて非表示のときはバー自体を出さない |
| `FormatBar` | コントロールが単一所有する書式バー（初回の取得で作る）。 ホストは `View` を置くだけでよい |
| `FormatBarHosting` | バーをどこへ出すか（`Custom` = ホストが置く（既定） / `Popup` = 選択範囲の近くへコントロールが出す） |
| `FormatBarFactory` / `FormatBarOptions` | バーの生成と構成（フォント・色・表記）。 差し替えると作り直す |
| `IsFormatBarOpen` | ポップアップで表示中か（`Custom` のときは常に `false`） |

キャレットと選択範囲の色・太さは `EsUtil.Feature.StyledText.Core` の `TextEditorAppearance` が単一所有します（4 つの UI 版で同じ見た目にするため）。

## 8. ログ

本コントロールは `Log` / `LogPrefix` を受け取り、コアの `TextLogMessage`（接頭辞 `[StyledText]`）で編集・選択・クリップボード操作を記録します。 生成と設定の変更はコントロールが記録します。 規則は [`.github/ログ規約.md`](../../../.github/ログ規約.md)。

## 9. 未対応

制限事項は [`docs/SPEC_API.md`](docs/SPEC_API.md) を参照してください。

## 10. サンプルアプリケーション

[`sample/`](sample/README.md) が本コントロールの操作を一通り確認できるサンプルです。 注釈のサンプル（[`../Annotation/sample`](../Annotation/sample)）も本コントロールを使って本文を表示・編集します。

## 11. ドキュメント

- API リファレンス … [`docs/SPEC_API.md`](docs/SPEC_API.md)
- システム仕様書 … [`docs/SPEC_System.md`](docs/SPEC_System.md)
- 層の帰属 … [`../../README.md`](../../README.md)
- 依存の許可範囲 … [`.github/依存関係.md`](../../../.github/依存関係.md)

## 12. プロジェクト構成

| フォルダ | 内容 |
| --- | --- |
| `src/` | コントロール本体 |
| `sample/` | 操作を確認できるサンプル |
| `docs/` | API リファレンスとシステム仕様書 |

## 13. テスト

本プロジェクトは単体テストを持ちません（WinUI 3 のコントロールは XAML の実行環境を要するため、ヘッドレスで生成できません）。 編集の規則は `EsUtil.Feature.StyledText.Core` のテストが固定し、外観と入力は [docs/SPEC_ManualTest.md](../Annotation/docs/SPEC_ManualTest.md) で確認します。 手順と合否基準は [`.github/検証手順.md`](../../../.github/検証手順.md) が単一所有します。

## 14. ライセンス

[MIT License](LICENSE)
