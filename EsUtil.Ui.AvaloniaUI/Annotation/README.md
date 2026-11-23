# EsUtil.Ui.AvaloniaUI.Annotation

![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.AvaloniaUI.Annotation.svg)
![Release](https://github.com/tomokuni/EsUtil/actions/workflows/release.yml/badge.svg)
![Build](https://github.com/tomokuni/EsUtil/actions/workflows/build.yml/badge.svg?branch=main)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%20(1809%2C%2017763)%2B-0078D6)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![AOT](https://img.shields.io/badge/AOT-Ready-00695C)
![Avalonia](https://img.shields.io/badge/UI-Avalonia-8D46D6)
![License](https://img.shields.io/badge/License-MIT-3DA639)

> プロジェクト名: `EsUtil.Ui.AvaloniaUI.Annotation`（Avalonia UI 版）。 UI 非依存のロジックは `EsUtil.Feature.Annotation.Core`、他 UI 版は対になるコントロール（別プロジェクト）にあります。

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

画像の上へ注釈（枠線・吹き出し）を重ねる Avalonia UI 版のレイヤー コントロールです。

- **`AnnotationTextEditor`**: 本文の表示・その場編集と書式バーを 1 つにまとめたコントロールです（内側の `Layer` で画像・文書・選択を操作します）。 ホストはこれ 1 つを置けば、本文の編集と書式の変更ができます。
- **`AnnotationLayerControl`**: 画像の表示・注釈の描画・入力の受付を担います（`AnnotationTextEditor.Layer` として使います）。
- **判断は `EsUtil.Feature.Annotation`（機能アセンブリ）が単一所有**します（当たり判定・折り返し・描画計画。 層の内部は `EsUtil.Feature.Annotation.Core.*` / `.Presenter.*`）。 本コントロールは自 UI の描画 API へ写すだけです。
- **選択枠と 8 方向のハンドルは `EsUtil.Feature.Marquee.Core` の提示層を再利用**します（移動・サイズ変更・方向キーの nudge・カーソル形状が 4 つの UI 版で同じになる）。
- **本文はその場で編集します**（ダブルクリックまたは Enter でキャレットが立ち、表示中のレイアウトのまま入力できます）。 IME（日本語入力）にも対応します。

**書式（文字単位）は選択範囲への適用**で変わり、**適用するとその場で反映**されます（右クリックのフライアウト、`AnnotationTextEditor` が出す書式バー）。
描画は run ごとの書式（太字・斜体・下線・取り消し線・色・大きさ・フォント）に対応しています。

**色の選択は `EsUtil.Ui.AvaloniaUI.ColorSelector` を使います**（自前の色入力を作らない）。 代表色は 40 色で、書式バーと右クリックのフライアウトは「代表色」と「ColorPicker」のタブを出します。 テーマ・色名のタブは隠します。

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
| UI | Avalonia 12.1.6 |
| 対象フレームワーク | `net10.0-windows10.0.17763.0` |
| Native AOT | 対応 |

---

## 4. インストール

```powershell
dotnet add package EsUtil.Ui.AvaloniaUI.Annotation
```

`ColorSelectorControl` は ColorPicker のテーマを要求するため、アプリは `Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml` を読み込みます。

```csharp
Styles.Add(new StyleInclude(new Uri("avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml"))
{
    Source = new Uri("avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml"),
});
```

---

## 5. クイックスタート

```csharp
using EsUtil.Feature.Annotation;
using EsUtil.Ui.AvaloniaUI.Annotation;

// 本文の表示・その場編集と書式バーをまとめたコントロール（これ 1 つを置く）
var editor = new AnnotationTextEditor { Width = 1920, Height = 1080 };
var layer = editor.Layer;
layer.Source = image;           // IImage（例: EsBitmapAdapter）
layer.SourcePixelWidth = 1920;
layer.SourcePixelHeight = 1080;
layer.Document = new AnnotationDocument(1920, 1080, "shot.png");

// 追加（既定の外観を使う）
layer.AddFrame(new EsRect(60, 60, 340, 220));
layer.AddBubble(new EsRect(900, 120, 380, 170), "ここが重要です。");

// 本文の編集（書式バーは AnnotationTextEditor が出し入れする）
layer.BeginTextEditing();
layer.EndTextEditing();

// 保存（注釈 JSON と合成画像の書き出し）はホストが行う
var path = AnnotationJson.GetDocumentPath(imagePath);
File.WriteAllText(path, AnnotationJson.Serialize(layer.Document), new UTF8Encoding(false));

using var composed = layer.RenderComposedImage();
composed.Save(AnnotationJson.GetComposedPath(imagePath)); // PNG
```

---

## 6. 画像の受け渡し

- **表示する画像は UI の型（`IImage`）で渡します**（`Source`）。 表示用の射影であり、コントロールは画素を読みません。
- **元画像の画素数を `SourcePixelWidth` / `SourcePixelHeight` で渡します**（注釈は元画像ピクセルで保持するため）。 画像は縦横比を保って中央へ表示します。
- **保存用の合成は `RenderComposedImage()`** が元画像ピクセルの 1:1 で行います（選択枠・キャレットは描きません）。

---

## 7. 所有権とライフサイクル

- **`Source` の画像はホストが所有します**（コントロールは破棄しません）。
- **`Document` は不変です**（変更は新しい文書を返し、`Document` へ設定し直します）。 変更のたびに `DocumentChanged` を通知します。
- **`RenderComposedImage()` の戻り値は呼び出し側が破棄します**。

---

## 8. 座標系

- **注釈の座標・文字の大きさ・枠線の太さは元画像ピクセル**です（表示倍率・DPI に依存しません）。
- 入力は DIP で届くため、コントロールが表示倍率を 1 回だけ掛けて元画像ピクセルへ変換します。
- **本文は行のベースラインで揃えます**（行の高さは斜体を外した書式で測る。 斜体にした区間だけが上下へずれない）。

---

## 9. 操作

| 操作 | 動作 |
| --- | --- |
| クリック | 選択（**8 方向のハンドル**が Marquee の提示層で描かれる） |
| 本体をドラッグ | 移動 |
| ハンドルをドラッグ | サイズ変更 |
| しっぽの先端（円）をドラッグ | 吹き出し口の移動（本体と独立） |
| ダブルクリック / Enter | 本文のその場編集（書式バーが出る） |
| 右クリック | 外観・重なり順の編集（本文の書式は書式バーで編集） |
| Delete | 選択中の注釈の削除 |
| 方向キー | nudge（1 px。 `Shift` 併用で 10 px） |
| `Ctrl+Z` / `Ctrl+Y` | 元に戻す・やり直す |

**編集中の書式**: 文字を選び、書式バーか `Ctrl+B` / `Ctrl+I` / `Ctrl+U`（取り消し線は `Alt+Shift+5`）で適用します。 書式バーは選択中だけ操作でき、選択範囲のすべてがその装飾のときだけボタンが押された状態になります。 編集を終えるには `Ctrl+Enter`・本文の外側をクリック・`Esc` を使います。

---

## 10. 他の UI 版との違い

| 項目 | 本 UI 版 |
| --- | --- |
| 本文の編集 | 入力欄を重ねず、表示中のレイアウトのままキャレットと選択範囲を描く（IME 対応） |
| 色の選択 | `ColorSelectorControl` を使う（代表色 40 色 + ColorPicker） |
| 斜体 | 行の高さとベースラインを変えない |

---

## 11. 含まれるもの

`EsUtil.Feature.Annotation.Core`（コアと提示層）、`EsUtil.Feature.Marquee.Core`（選択枠）、`EsUtil.Ui.AvaloniaUI.ColorSelector`（色の選択）、`EsUtil.Platform.Core`。

---

## 12. カスタマイズ

- **代表色の列数**: `AnnotationTextEditor.PaletteColumns`（既定 8 列。 40 色なら 8 列 × 5 行）。
- **書式バーの色の幅**: `AnnotationTextEditor.ColorWidth`。
- **色の表記**: `AnnotationTextEditor.TextFormat`（色名 + 不透明度 / カラーコード `#AARRGGBB`）。
- **任意の色を選ばせる**: `AnnotationPalette.ConfigureAll`（代表色 + 標準のタブ）。
- **新規作成時の既定値**: `AnnotationLayerControl.DraftDefaults`（枠線・吹き出し・本文）。 既存の注釈は各注釈が持つ外観・書式で描きます。
- **編集 UI の部品**: `AnnotationEditorControls`（見出し・項目名・色・数値・選択肢・装飾の切り替え）はフライアウトとホストの設定ペインが共有します。 `AnnotationFonts.All` は選べるフォントの一覧です。

---

## 13. ログ

`Log`（`ILogger`）と `LogPrefix` で出力先と接頭辞を設定します（既定の接頭辞は `[Annotation]`）。 記録する内容とレベルは頻度で決めます（規則は [`ログ規約.md`](../../../.github/ログ規約.md) §2・§6）。

| レベル | 記録する内容 |
| --- | --- |
| `Information` | 注釈ファイルの読み込み・保存、ファサードの生成（プロセスで 1 度） |
| `Debug` | 選択の変更、本文の編集の開始・終了、書式の適用、パネルの表示・終了、押下と解放、文書の作成、注釈の追加、提示層の生成 |
| `Trace` | ドラッグ中の文書の変更（マウス移動のたびに届くため既定では出力しない） |
| `Error` | 公開ファサードの操作で発生した例外（読み込み・保存の失敗） |

**障害の切り分け**: 既定（`Information`）では操作の記録だけが出ます。 症状の報告を受けたら **`Debug`** で「選択・編集・書式・パネル・押下が起きたか」、**`Trace`** で「ドラッグが文書へ反映されているか」を確認できます。 描画計画の解決（毎フレーム）と当たり判定は記録しません。

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
dotnet run --project EsUtil.Ui.AvaloniaUI/Annotation/sample/EsUtil.Ui.AvaloniaUI.Annotation.Sample.csproj
```

---

## 16. ドキュメント

- API リファレンス … [`docs/SPEC_API.md`](docs/SPEC_API.md)
- システム仕様書 … [`docs/SPEC_System.md`](docs/SPEC_System.md)
- サンプル … [`sample/README.md`](sample/README.md)
- コア（モデル・描画計画） … [`EsUtil.Feature.Annotation.Core`](../../EsUtil/Feature.Annotation.Core/README.md)

---

## 17. プロジェクト構成

| フォルダ | 内容 |
| --- | --- |
| `src/` | コントロール本体（`AnnotationLayerControl` / `AnnotationTextEditor` / 書式バー / 描画） |
| `tests/` | 単体テスト（コントロールの組立てと描画の写像） |
| `sample/` | 動作を確認するサンプル アプリ |
| `docs/` | API リファレンスとシステム仕様書 |

---

## 18. テスト

```powershell
dotnet test EsUtil.Ui.AvaloniaUI/Annotation/tests/EsUtil.Ui.AvaloniaUI.Annotation.Tests.csproj
```

判断（当たり判定・折り返し・JSON）のテストは [`EsUtil.Feature.Annotation.Core`](../../EsUtil/Feature.Annotation.Core/README.md) が持ちます。 手順と合否基準は [`.github/検証手順.md`](../../../.github/検証手順.md) が単一所有します。

---

## 19. ライセンス

[MIT License](../../../LICENSE)
