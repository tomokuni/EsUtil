# EsUtil.Feature.Annotation.Core

![NuGet](https://img.shields.io/nuget/v/EsUtil.Feature.Annotation.Core.svg)
![Release](https://github.com/tomokuni/EsUtil/actions/workflows/release.yml/badge.svg)
![Build](https://github.com/tomokuni/EsUtil/actions/workflows/build.yml/badge.svg?branch=main)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%20(1809%2C%2017763)%2B-0078D6)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![AOT](https://img.shields.io/badge/AOT-Ready-00695C)
![License](https://img.shields.io/badge/License-MIT-3DA639)

画像に重ねる注釈（枠線・吹き出し）の **Core（何を描くか・判断・計算）と Presenter（描画計画と外観）** を提供します。
UI フレームワークに依存せず、4 つの UI 版（Avalonia UI / MewUI / WinUI 3 / WinForms）が共有します。

## 1. 概要

- **注釈は画像に焼き込まない**（レイヤーとして保持し、いつでも編集できる）。
- **座標と文字の大きさは元画像ピクセル**である（表示倍率・DPI に依存しない。 保存画像と比率が一致する）。
- **注釈は JSON で保存できる**（`<画像名>.annotations.json`。 スキーマ版を持つ）。
- **描画計画（何を描くか）は本層が解決し、UI は自分の描画 API へ写すだけ**にする（4 つの UI 版で見た目がずれない）。

UI 層は 4 つの UI 版（Avalonia UI / MewUI / WinUI 3 / WinForms）が参照します。

## 2. 使い方

```csharp
using EsUtil.Feature.Annotation;

// 0. ファサードを用意する（ロガーを渡すと生成・読み書き・追加を記録する）
var core = new AnnotationCore { LogPrefix = "[Annotation]" };
core.Log = logger;

// 1. 文書を作る（画像の大きさを覚える）
var document = core.CreateDocument(1920, 1080, "shot.png");

// 2. 注釈を追加する（矩形は画像の内側へ収められる）
document = document.Add(core.CreateFrame(document, new EsRect(60, 60, 340, 220)));
document = document.Add(core.CreateBubble(
    document,
    new EsRect(900, 120, 380, 170),
    new RichText([
        new TextRun("ここが重要です。", TextDefaults.DefaultStyle),
        new TextRun("強調", TextDefaults.DefaultStyle with { IsBold = true }),
    ]),
    tail: new BubbleTail(new EsPoint(760, 420), BubbleEdge.Left, 0.5, 26)));

// 3. 描画計画を解決する（実寸の計測だけ UI が渡す）
var plan = core.Presenter.ResolveDrawPlan(document, (text, style) => Measure(text, style), selectedId: null);

// 4. JSON へ保存する / JSON から読み込む（文字コードは UTF-8・BOM なし）
var json = AnnotationJson.Serialize(document);
var restored = AnnotationJson.Deserialize(json);

// 5. 保存する 3 つのパスのうち、注釈と合成画像のパスを求める
var annotationPath = AnnotationJson.GetDocumentPath(imagePath); // <画像名>.annotations.json
var composedPath = AnnotationJson.GetComposedPath(imagePath);   // <画像名>.annotated.<拡張子>
```

主な API は次のとおりです。

| 型 | 役割 |
| --- | --- |
| `AnnotationCore` | **ファサード**（文書の生成・注釈の生成・JSON の読み書きと付随ファイルの保存・削除・ログ。 提示層を `Presenter` で共有する） |
| `AnnotationDocument` | 注釈の集合（並び順が重なり順）・画像の寸法・JSON の版。 不変（変更は `With...` が新しい文書を返す） |
| `FrameAnnotation` / `BubbleAnnotation` | 枠線（矩形 + 角丸）/ 吹き出し（本文 + しっぽ） |
| `BubbleTail` / `BubbleEdge` | しっぽ（先端は本体と独立・付け根は本体の辺に追従） |
| `RichText` / `TextRun` / `TextStyle` | 文字単位の書式（`EsUtil.Feature.StyledText.Core` が所有する。 フォント・大きさ・色・太字・斜体・下線・取り消し線） |
| `AnnotationAppearance` | 注釈の外観（枠線の色・太さ・内側の色・角丸） |
| `AnnotationPresenter` / `AnnotationDrawPlan` / `AnnotationShapePlan` | 折り返しと描画計画の解決 |
| `TextEditor` | 本文の値と編集の状態（キャレット・選択・入力・削除・書式。 `EsUtil.Feature.StyledText.Core` が所有する）。 4 つの UI 版は同じ実体を本文コントロールへ渡すだけにし、**書式バーを表示する条件（編集中・1 文字以上を選択）も本型が単一所有する** |
| `AnnotationEditSession` | 文書・選択・変更履歴・本文の編集状態を単一所有する編集セッション。 4 つの UI 版は本型の操作を呼び、変更を購読する |
| `AnnotationViewTransform` | 表示倍率・位置と、元画像ピクセルとローカル DIP の相互変換 |
| `TextLayout` / `TextLineIndex` / `TextCursor` | 折り返しと行の配置・行と文字位置の対応・書記素クラスタ（絵文字・結合文字）の境界（`EsUtil.Feature.StyledText.Core` が所有する） |
| `AnnotationHitTest` | 選択としっぽの当たり判定（最前面から探す） |
| `AnnotationJson` | JSON 文字列への変換・保存パスの解決・付随ファイルの判定（ファイル入出力は `AnnotationCore`） |
| `AnnotationDefaults` / `AnnotationLimits` | 既定値と安全域（設定・JSON・入力の共通の補正） |
| `AnnotationHistory` | 変更履歴（元に戻す・やり直す。 1 操作を `BeginChange` / `CommitChange` で囲むと 1 手にまとまる。 不変の文書をそのまま積む） |
| `AnnotationDraftDefaultsFactory` / `AnnotationEditorAppearance` | 層と 4 UI が共有する値（設定からの既定値の組み立て・本文の編集中の見た目） |

## 3. 構成（名前空間とフォルダ）

公開面（ファサードとそのメンバーの型）は `src/` 直下の `EsUtil.Feature.Annotation` です。 層の内部は層フォルダへ置き、名前空間に層名を挾みます（規則は [`.github/命名規約.md`](../../../.github/命名規約.md)）。

| フォルダ | 名前空間 | 置く型 |
| --- | --- | --- |
| `src/` 直下 | `EsUtil.Feature.Annotation` | ファサード（`AnnotationCore` / `AnnotationPresenter`）と、そのメンバーに現れる型（文書・注釈・書式・外観・描画計画） |
| `src/Core/Contract` | `.Core.Contract` | 4 つの UI 版と共有する通知（追加メニュー・プレビュー操作・書式編集の要求）と、ポインターの種類 |
| `src/Core/Defaults` | `.Core.Defaults` | 既定値と安全域（`AnnotationDefaults` / `AnnotationLimits` / `AnnotationDraftDefaultsFactory`） |
| `src/Core/Geometry` | `.Core.Geometry` | 画像内へ収める規則と、表示倍率・位置・座標変換（`AnnotationViewTransform`） |
| `src/Core/History` | `.Core.History` | 変更履歴（元に戻す・やり直す） |
| `src/Core/Interaction` | `.Core.Interaction` | 当たり判定とポインターの形の判断 |
| `src/Core/Internal` | `.Core.Internal` | JSON の読み書き（`internal`） |
| `src/Presenter/Drawing` | `.Presenter.Drawing` | キャレットと選択範囲の見た目（`AnnotationEditorAppearance`）・編集中の枠 |
| `src/Presenter/Editor` | `.Presenter.Editor` | 編集 UI の文言・寸法・代表色・書式バーの並び |
| `src/Presenter/Panel` | `.Presenter.Panel` | フローティング パネルの外観・配置・開閉 |
| `src/Presenter/Selection` | `.Presenter.Selection` | 選択中の注釈の編集入口と、本文の編集状態（キャレット・選択・入力・書式）・編集セッション（文書・選択・履歴の単一所有） |
| `src/Logging` | `.Logging` | ログ定義（`internal`。 UI が呼ぶ定義だけ `public`） |

文字の書式・折り返し・行と文字位置の対応は `EsUtil.Feature.StyledText.Core`（名前空間 `EsUtil.Feature.StyledText`）が単一所有します。 本アセンブリは `EsUtil.Platform.Core` と `EsUtil.Feature.StyledText.Core` だけを参照します（UI フレームワークに依存しない）。

## 4. テスト

```powershell
dotnet test EsUtil/Feature.Annotation.Core/tests/EsUtil.Feature.Annotation.Core.Tests.csproj
```

折り返し（幅・明示改行・行頭禁則）・しっぽの幾何・当たり判定・JSON の往復と保存形式を検証します。
手順と合否基準は [`.github/検証手順.md`](../../../.github/検証手順.md) が単一所有します。

## 5. ドキュメント

- API リファレンス … [`docs/SPEC_API.md`](docs/SPEC_API.md)
- システム仕様書 … [`docs/SPEC_System.md`](docs/SPEC_System.md)
- 層の帰属 … [`EsUtil/README.md`](../../README.md)
- 依存の許可範囲 … [`.github/依存関係.md`](../../../.github/依存関係.md)
- ログの規則 … [`.github/ログ規約.md`](../../../.github/ログ規約.md)

## 6. ライセンス

[MIT License](LICENSE)
