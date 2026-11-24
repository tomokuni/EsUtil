# EsUtil.Ui.WinUI3.Annotation

![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinUI3.Annotation.svg)
![Release](https://github.com/tomokuni/EsUtil/actions/workflows/release.yml/badge.svg)
![Build](https://github.com/tomokuni/EsUtil/actions/workflows/build.yml/badge.svg?branch=main)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%20(1809%2C%2017763)%2B-0078D6)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![AOT](https://img.shields.io/badge/AOT-Ready-00695C)
![WinUI3](https://img.shields.io/badge/UI-WinUI%203-0067C0)
![License](https://img.shields.io/badge/License-MIT-3DA639)

> プロジェクト名: `EsUtil.Ui.WinUI3.Annotation`（WinUI 3 版）。 UI 非依存のロジックは `EsUtil.Feature.Annotation.Core`、他 UI 版は対になるコントロール（別プロジェクト）にあります。

## 目次 <!-- omit from toc -->

- [1. 概要](#1-概要)
- [2. 機能一覧](#2-機能一覧)
- [3. システム要件](#3-システム要件)
- [4. インストール](#4-インストール)
- [5. クイックスタート](#5-クイックスタート)
- [6. 画像の受け渡し](#6-画像の受け渡し)
- [7. 所有権とライフサイクル](#7-所有権とライフサイクル)
- [8. 座標系](#8-座標系)
- [9. 操作](#9-操作)
- [10. 他の UI 版との違い](#10-他の-ui-版との違い)
- [11. 含まれるもの](#11-含まれるもの)
- [12. カスタマイズ](#12-カスタマイズ)
- [13. ログ](#13-ログ)
- [14. 未対応](#14-未対応)
- [15. サンプルアプリケーション](#15-サンプルアプリケーション)
- [16. ドキュメント](#16-ドキュメント)
- [17. プロジェクト構成](#17-プロジェクト構成)
- [18. テスト](#18-テスト)
- [19. ライセンス](#19-ライセンス)

---

## 1. 概要

画像の上へ注釈（枠線・吹き出し）を重ねる WinUI 3 版のレイヤー コントロールです。

- **`AnnotationEditorPanel`**: 本文の表示・その場編集と書式バーを 1 つにまとめたコントロールです（内側の `Layer` で画像・文書・選択を操作します）。
- **`AnnotationLayerControl`**: 画像の表示・注釈の描画・入力の受付を担います（`AnnotationEditorPanel.Layer` として使います）。
- **判断は `EsUtil.Feature.Annotation`（機能アセンブリ）が単一所有**します（当たり判定・折り返し・描画計画。 層の内部は `EsUtil.Feature.Annotation.Core.*` / `.Presenter.*`）。 本コントロールは XAML の図形と文字へ写すだけです。
- **選択枠と 8 方向のハンドルは `EsUtil.Feature.Marquee.Core` の提示層を再利用**します。
- **本文はその場で編集します**（表示中のレイアウトのままキャレットを描く）。 日本語入力（IME）は `CoreTextEditContext` を本文へ繋ぎ、変換中の文字列は文書へ入れずキャレットの位置へ下線付きで描きます。

**色の選択は `EsUtil.Ui.WinUI3.ColorSelector` を使います**（自前の色入力を作らない）。 書式バーと右クリックのフライアウトは「代表色」と「ColorPicker」のタブを出します。

---

## 2. 機能一覧

- 枠線・吹き出しの追加・移動・サイズ変更・削除
- しっぽ（吹き出し口）の先端のドラッグ（本体と独立）
- 本文のその場編集（キャレット・選択範囲・IME 対応）
- 文字単位の書式（フォント・大きさ・色・太字・斜体・下線・取り消し線）
- 右クリックによる外観・重なり順の編集
- 元に戻す・やり直す（ドラッグ 1 回 = 1 手）
- 保存用の合成画像（元画像ピクセルの 1:1）

---

## 3. システム要件

| 項目 | 要件 |
| --- | --- |
| OS | Windows 10 バージョン 1809（ビルド 17763）以降 |
| .NET | .NET 10 |
| UI | Windows App SDK 2.0.1 以降（Windows App Runtime が必要） |
| 対象フレームワーク | `net10.0-windows10.0.17763.0` |
| Native AOT | 対応 |

---

## 4. インストール

```powershell
dotnet add package EsUtil.Ui.WinUI3.Annotation
```

---

## 5. クイックスタート

```csharp
using EsUtil.Feature.Annotation;
using EsUtil.Ui.WinUI3.Annotation;

// 本文の表示・編集と書式バーをまとめたコントロール（これ 1 つを置く）
var editor = new AnnotationEditorPanel();
var layer = editor.Layer;

layer.Source = image;           // EsReadOnlyBitmap
layer.SourcePixelWidth = 1920;
layer.SourcePixelHeight = 1080;
layer.Document = new AnnotationDocument(1920, 1080, "shot.png");

// 追加（既定の外観を使う）
layer.AddFrame(new EsRect(60, 60, 340, 220));
layer.AddBubble(new EsRect(900, 120, 380, 170), "ここが重要です。");

// 保存（注釈 JSON と合成画像の書き出し）はホストが行う
var path = AnnotationJson.GetDocumentPath(imagePath);
File.WriteAllText(path, AnnotationJson.Serialize(layer.Document), new UTF8Encoding(false));

using var composed = layer.RenderComposedImage();
composed.Save(AnnotationJson.GetComposedPath(imagePath)); // PNG
```

---

## 6. 画像の受け渡し

- **表示する画像はコアの具象型（`EsReadOnlyBitmap`）で渡します**（`Source`）。
- **元画像の画素数を `SourcePixelWidth` / `SourcePixelHeight` で渡します**。
- **保存用の合成は `RenderComposedImage()`** が元画像ピクセルの 1:1 で作ります。

---

## 7. 所有権とライフサイクル

- **`Source` の画像はホストが所有します**（コントロールは破棄しません）。
- **`Document` は不変です**（変更は新しい文書を返し、`Document` へ設定し直します）。
- **`RenderComposedImage()` の戻り値は呼び出し側が破棄します**。

---

## 8. 座標系

- **注釈の座標・文字の大きさ・枠線の太さは元画像ピクセル**です（表示倍率・DPI に依存しません）。
- 入力は DIP で届くため、コントロールが表示倍率を 1 回だけ掛けて元画像ピクセルへ変換します。

---

## 9. 操作

| 操作 | 動作 |
| --- | --- |
| クリック | 選択（**8 方向のハンドル**が Marquee の提示層で描かれる） |
| 本体をドラッグ | 移動 |
| ハンドルをドラッグ | サイズ変更 |
| しっぽの先端（■）をドラッグ | 吹き出し口の移動（本体と独立） |
| ダブルクリック / Enter | 本文のその場編集 |
| 右クリック | 外観・重なり順の編集 |
| Delete | 選択中の注釈の削除 |
| 方向キー | nudge |
| `Ctrl+Z` / `Ctrl+Y` | 元に戻す・やり直す |

---

## 10. 他の UI 版との違い

| 項目 | 本 UI 版 |
| --- | --- |
| 描画 | XAML の図形と `TextBlock` で描く |
| 本文の編集 | 入力欄を重ねず、表示中のレイアウトのままキャレットを描く（`CoreTextEditContext` で IME を受ける） |
| 色の選択 | `ColorSelectorControl` を使う |
| 斜体 | 行の高さとベースラインを変えない（行の高さは斜体を外した書式で測る） |

---

## 11. 含まれるもの

`EsUtil.Feature.Annotation.Core`（コアと提示層）、`EsUtil.Feature.Marquee.Core`（選択枠）、`EsUtil.Ui.WinUI3.ColorSelector`（色の選択）、`EsUtil.Ui.WinUI3.Interop`（画像の変換）、`EsUtil.Platform.Core`。

---

## 12. カスタマイズ

- **書式バーの自動表示**: `AnnotationEditorPanel.ShowFormatBarWhileEditing`（既定は `true`。 `false` にすると自動では出さない）。 表示の条件（1 文字以上を選択）は本文の編集コントロールが単一所有します。

- **色の表記**: `AnnotationEditorPanel.TextFormat`。
- **新規作成時の既定値**: `AnnotationLayerControl.DraftDefaults`。

---

## 13. ログ

`Log`（`ILogger`）と `LogPrefix` で出力先と接頭辞を設定します（既定の接頭辞は `[Annotation]`）。 記録する内容とレベルは頻度で決めます（規則は [`ログ規約.md`](../../../.github/ログ規約.md) §2・§6）。

| レベル | 記録する内容 |
| --- | --- |
| `Information` | 注釈ファイルの読み込み・保存・削除、設定（行間・書式バーの自動表示）の変更 |
| `Debug` | 生成（ファサード・提示層・文書・注釈）、選択の変更、本文の編集の開始・終了、本文の変更（文字数・先頭 100 文字）、書式の適用（範囲と文字）、コピー・切り取り・貼り付け（キャレット・選択・先頭 100 文字）、パネルの表示・終了・位置、押下と解放 |
| `Trace` | 本文の選択（キャレット・選択範囲）とドラッグ中の文書の変更（マウス移動のたびに届くため既定では出力しない） |
| `Error` | 公開ファサードの操作で発生した例外（読み込み・保存の失敗） |

**障害の切り分け**: 既定（`Information`）では操作の記録だけが出ます。 症状の報告を受けたら **`Debug`** で「選択・編集・書式・コピーと貼り付け・パネル・押下が起きたか」、**`Trace`** で「ドラッグが文書へ反映されているか」を確認できます。 描画計画の解決（毎フレーム）と当たり判定は記録しません。

定義はコア（`EsUtil.Feature.Annotation.Core` の `AnnotationLogMessage`）が単一所有し、本コントロールは同じロガーをコアと提示層へ渡します。

---

## 14. 未対応

- 入れ子の二重枠・破線・楕円・矢印の注釈は持ちません（枠線と吹き出しだけです）。
- 本文の書式は文字単位であり、段落・行単位の書式は持ちません。

制限事項の詳細は [`docs/SPEC_API.md`](docs/SPEC_API.md) を参照してください。

---

## 15. サンプルアプリケーション

[`sample/`](sample/README.md) に動作を確認するサンプルがあります。

```powershell
dotnet run --project EsUtil.Ui.WinUI3/Annotation/sample/EsUtil.Ui.WinUI3.Annotation.Sample.csproj
```

---

## 16. ドキュメント

- API リファレンス … [`docs/SPEC_API.md`](docs/SPEC_API.md)
- システム仕様書 … [`docs/SPEC_System.md`](docs/SPEC_System.md)
- コア（モデル・描画計画） … [`EsUtil.Feature.Annotation.Core`](../../EsUtil/Feature.Annotation.Core/README.md)
- Avalonia UI 版 … [`EsUtil.Ui.AvaloniaUI.Annotation`](../../EsUtil.Ui.AvaloniaUI/Annotation/README.md)

---

## 17. プロジェクト構成

| フォルダ | 内容 |
| --- | --- |
| `src/` | コントロール本体（`AnnotationLayerControl` / `AnnotationEditorPanel` / 書式バー / 描画） |
| `tests/` | 単体テスト（色・フォント・実寸の近似） |
| `sample/` | 動作を確認するサンプル アプリ |
| `docs/` | API リファレンスとシステム仕様書 |

---

## 18. テスト

```powershell
dotnet test EsUtil.Ui.WinUI3/Annotation/tests/EsUtil.Ui.WinUI3.Annotation.Tests.csproj
```

判断（当たり判定・折り返し・JSON）のテストは [`EsUtil.Feature.Annotation.Core`](../../EsUtil/Feature.Annotation.Core/README.md) が持ちます。 手順と合否基準は [`.github/検証手順.md`](../../../.github/検証手順.md) が単一所有します。

---

## 19. ライセンス

[MIT License](../../../LICENSE)
