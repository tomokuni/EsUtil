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
using EsUtil.Feature.Annotation.Core;
using EsUtil.Feature.Annotation.Presenter;

// 1. 文書を作る（画像の大きさを覚える）
var document = new AnnotationDocument(1920, 1080, "shot.png")
    .Add(new FrameAnnotation { Bounds = new EsRect(60, 60, 340, 220), Appearance = AnnotationDefaults.FrameAppearance })
    .Add(new BubbleAnnotation
    {
        Bounds = new EsRect(900, 120, 380, 170),
        Appearance = AnnotationDefaults.BubbleAppearance,
        Tail = new BubbleTail(new EsPoint(760, 420), BubbleEdge.Left, 0.5, 26),
        Text = new RichText([
            new TextRun("ここが重要です。", AnnotationDefaults.TextStyle),
            new TextRun("強調", AnnotationDefaults.TextStyle with { IsBold = true }),
        ]),
    });

// 2. 描画計画を解決する（実寸の計測だけ UI が渡す）
var plan = new AnnotationPresenter().ResolveDrawPlan(document, (text, style) => Measure(text, style), selectedId: null);

// 3. JSON へ保存する / JSON から読み込む
var json = AnnotationJson.Serialize(document);
var restored = AnnotationJson.Deserialize(json);

// 4. 保存する 3 つのパスのうち、注釈と合成画像のパスを求める
var annotationPath = AnnotationJson.GetDocumentPath(imagePath); // <画像名>.annotations.json
var composedPath = AnnotationJson.GetComposedPath(imagePath);   // <画像名>.annotated.<拡張子>
```

主な API は次のとおりです。

| 型 | 役割 |
| --- | --- |
| `AnnotationDocument` | 注釈の集合（並び順が重なり順）・画像の寸法・JSON の版 |
| `FrameAnnotation` / `BubbleAnnotation` | 枠線（矩形 + 角丸）/ 吹き出し（本文 + しっぽ） |
| `BubbleTail` / `BubbleEdge` | しっぽ（先端は本体と独立・付け根は本体の辺に追従） |
| `RichText` / `TextRun` / `AnnotationTextStyle` | 文字単位の書式（フォント・大きさ・色・太字・斜体・下線・取り消し線）。 範囲の書式は `ApplyStyle`（差し替え）と `TransformStyle`（1 属性だけ変える）で変える |
| `AnnotationAppearance` | 注釈の外観（枠線の色・太さ・内側の色・角丸） |
| `AnnotationPresenter` / `AnnotationDrawPlan` / `AnnotationShapePlan` | 折り返しと描画計画の解決 |
| `AnnotationHitTest` | 選択としっぽの当たり判定（最前面から探す） |
| `AnnotationJson` | JSON の読み書きとパスの解決 |
| `AnnotationDefaults` / `AnnotationLimits` | 既定値と安全域（設定・JSON・入力の共通の補正） |
| `AnnotationHistory` | 変更履歴（元に戻す・やり直す。 文書を複製して積む） |

## 3. テスト

```powershell
dotnet test EsUtil/Feature.Annotation.Core/tests/EsUtil.Feature.Annotation.Core.Tests.csproj
```

折り返し（幅・明示改行・行頭禁則）・しっぽの幾何・当たり判定・JSON の往復と保存形式を検証します。
手順と合否基準は [`.github/検証手順.md`](../../../.github/検証手順.md) が単一所有します。

## 4. ドキュメント

- API リファレンス … [`docs/SPEC_API.md`](docs/SPEC_API.md)
- システム仕様書 … [`docs/SPEC_System.md`](docs/SPEC_System.md)
- 層の帰属 … [`EsUtil/README.md`](../../README.md)
- 依存の許可範囲 … [`.github/依存関係.md`](../../../.github/依存関係.md)
- ログの規則 … [`.github/ログ規約.md`](../../../.github/ログ規約.md)

## 5. ライセンス

[MIT License](../../../LICENSE)
