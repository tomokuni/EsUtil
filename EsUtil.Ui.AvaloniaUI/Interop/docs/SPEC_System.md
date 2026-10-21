# EsUtil.Ui.AvaloniaUI.Interop システム仕様書

Avalonia UI と EsUtil の型の境界を担うアセンブリの構造・依存・設計判断を扱います。 開発者向けの文書です。

## 目次 <!-- omit from toc -->

- [1. システム概要](#1-システム概要)
- [2. 技術スタック](#2-技術スタック)
- [3. アーキテクチャ](#3-アーキテクチャ)
- [4. コンポーネント構成](#4-コンポーネント構成)
- [5. 依存関係](#5-依存関係)
- [6. エラー処理ポリシー](#6-エラー処理ポリシー)
- [7. 実装上の注意](#7-実装上の注意)

## 1. システム概要

Avalonia UI の型と EsUtil の型の**境界**を担うアセンブリです。 UI フレームワークに依存する処理（型の変換と UI 固有の描画）だけを置き、それ以外は下の層へ委ねます。

## 2. 技術スタック

| 項目 | 内容 |
| --- | --- |
| 対象フレームワーク | `net10.0` |
| UI フレームワーク | `Avalonia` |
| 共通基盤 | `EsUtil.Platform.Core`（値型・画像・OS の抽象） |
| テスト | xUnit v3 |

## 3. アーキテクチャ

```mermaid
graph LR
  APP[アプリ（Avalonia UI）] --> BRIDGE[EsUtil.Ui.AvaloniaUI.Interop]
  BRIDGE --> CORE[EsUtil.Platform.Core]
  BRIDGE --> UI[`Avalonia`]
```

- **依存の向きは 1 方向です**（アプリ → 本アセンブリ → コアと UI フレームワーク）。 本アセンブリからアプリへは参照しません。
- **変換の規則を 1 か所に持ちます**: 同じ変換をアプリや各コントロールへ写すと、片方だけ直したときに挙動が食い違います。

## 4. コンポーネント構成

| 区分 | 型 |
| --- | --- |
| `Coord.ScreenConverter` | コントロール ローカルの DIP ↔ 画面物理ピクセル |
| `Imaging.EsBitmapAdapter` | コアの画像を `IImage` として描く（`Draw` で任意の `DrawingContext` へ描ける） |
| `Imaging.EsBitmapExtensions` | `ToAvaloniaBitmap` / `ToWriteableBitmap` / `ToEsBitmap` |
| `Imaging.SolidColorBrushCache` | `GetOrCreate`（色 → `SolidColorBrush`。 容量制限付き） |
| `Imaging.TextMeasure` | 文字列の実寸（Avalonia のテキスト計測） |

## 5. 依存関係

`EsUtil.Platform.Core` と `Avalonia` を参照します。 参照してよいプロジェクトの規則は [`.github/依存関係.md`](../../../.github/依存関係.md) §3 が定めます。

## 6. エラー処理ポリシー

- **未対応の値は既定値へ落とします**（キーの未定義値は `None`、対応の無い幾何は 0）。 例外にしません（UI の入力経路で例外を投げると操作が止まります）。
- **引数の null は `ArgumentNullException`** にします（呼び出し側の誤りであり、黙って既定値へ落とすと原因が分かりません）。
- **グラフィックス バックエンドが未登録の環境**（MewUI のテキスト計測など）では、UI フレームワークが投げる例外をそのまま伝えます（本アセンブリで握りつぶしません）。

## 7. 実装上の注意

- 描画エンジン（`Avalonia.Skia`）はアプリが選びます。 本アセンブリは `Avalonia` 本体のみを参照します。
- `EsBitmapAdapter` は `IImage` を実装するため、実描画にはアプリの描画エンジンが要ります。
