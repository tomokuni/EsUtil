# EsUtil.Ui.AvaloniaUI.Thumbnail サンプルアプリケーション

## 目次 <!-- omit from toc -->

- [1. 概要](#1-概要)
- [2. 実行方法](#2-実行方法)
- [3. 操作方法](#3-操作方法)
- [4. 検証項目（動作確認チェックリスト）](#4-検証項目動作確認チェックリスト)
- [5. 構成](#5-構成)
- [6. 使い方（初めての方向け）](#6-使い方初めての方向け)
- [7. 拡張例（描画計画の差し替え）](#7-拡張例描画計画の差し替え)

---

## 1. 概要

`EsUtil.Ui.AvaloniaUI/Thumbnail/sample/` にある **ThumbnailControl 動作確認用の独立したサンプルアプリケーション**です。

- `ThumbnailControl`（`EsUtil.Ui.AvaloniaUI.Thumbnail`）を実際に配置して動作を確認できます。
- テスト画像を生成して一覧へ追加します（外部ファイルを必要としません）。
- 画像は**コアの型（`EsBitmap`）**で作り、コントロールが受け取る型へ変換して渡します。
- 折り返し配置・可視範囲・選択の強調（帯と太字）・表示テーマ（ライト / ダーク）・末尾へのスクロールを確認できます。

## 2. 実行方法

```powershell
dotnet run --project EsUtil.Ui.AvaloniaUI/Thumbnail/sample/EsUtil.Ui.AvaloniaUI.Thumbnail.Sample.csproj
```

リリース構成で実行する場合:

```powershell
dotnet run -c Release --project EsUtil.Ui.AvaloniaUI/Thumbnail/sample/EsUtil.Ui.AvaloniaUI.Thumbnail.Sample.csproj
```

## 3. 操作方法

| 操作 | 内容 |
| --- | --- |
| 起動直後 | 12 件の項目が並び、先頭が選択されます |
| 「追加」ボタン | 画像を 1 件生成して末尾へ追加（`Add`） |
| 「12 件に戻す」ボタン | 一覧を空にして 12 件を作り直す（`ClearItems` ＋ `Add`） |
| 「末尾へスクロール」ボタン | `ScrollToIndex(ItemCount - 1)` |
| 「表示テーマ」選択（共通の設定行） | `SampleTheme`（コントロールは明暗をフレームワークのテーマから自分で読むため、指定は不要） |
| 項目を 1 回押す | 選択（`SelectedIndex` と `SelectionChanged`） |
| 項目を 2 回押す | 確定（`ItemActivated`。 状態表示に添字を出す） |
| 項目へマウスを重ねる | ホバー帯の表示 |

## 4. 検証項目（動作確認チェックリスト）

- [ ] 12 件の項目が折り返して並ぶ（ウィンドウ幅を変えると列数が変わる）
- [ ] 画像の下に名前（`capture-0000.png` 形式）が表示される
- [ ] 先頭の項目が「現在」として強調される（名前が太字になる）
- [ ] 項目を 1 回押すと選択され、状態表示の添字が変わる
- [ ] 項目を 2 回押すと `ItemActivated` が発火し、状態表示が「確定: 添字」になる
- [ ] 項目へマウスを重ねるとホバー帯が付く（表示テーマをダークにすると色が変わる）
- [ ] 「表示テーマ」をダークにすると背景・ホバー・選択の色が暗色向けに変わる
- [ ] 「末尾へスクロール」で最後の項目が見える位置へ移動する
- [ ] 「追加」で項目が 1 件増え、状態表示の件数が増える
- [ ] ウィンドウを縮めても項目が切れず、スクロールで全件へ到達できる

## 5. 構成

```text
EsUtil.Ui.AvaloniaUI/Thumbnail/sample/
├── EsUtil.Ui.AvaloniaUI.Thumbnail.Sample.csproj   # サンプルアプリのプロジェクト（net10.0）
├── app.manifest                 # Windows DPI 設定（PerMonitorV2）
├── Program.cs                   # エントリポイント
├── App.axaml / App.axaml.cs     # アプリケーション定義（FluentTheme）
├── Imaging/
│   └── TestImageFactory.cs      # テスト画像生成（コアの EsBitmap を返す）
├── Views/
```

## 6. 使い方（初めての方向け）

1. **起動**: 12 件のサムネイルが折り返して並びます。
2. **画像の用意**: 画像は `EsUtil.Ui.AvaloniaUI/Thumbnail/sample/Imaging/TestImageFactory.cs` がコアの `EsBitmap` として生成します。 コントロールは Avalonia の画像型（`IImage`）を受け取るため、`EsUtil.Ui.AvaloniaUI.Interop.Imaging` の `EsBitmapAdapter.Adopt` で写しています。
3. **項目の追加**: `Add(画像, 名前, ツールチップ, tag)` で追加します。 生成に時間がかかる場合は、先に `null` で追加しておき、生成後に `SetImage(添字, 画像)` で差し替えます。
4. **見えている項目だけを作る**: `VisibleRange`（可視範囲）と `SlotBounds(添字)`（項目の矩形）を使うと、可視範囲の画像だけを遅延生成できます。
5. **外観**: `Options` / `Appearance` / `ImageWidth` は設定すると自動で反映されます（`IsDarkTheme` だけは `Refresh()` を呼びます）。

## 7. 拡張例（描画計画の差し替え）

`ThumbnailControl.DrawPlanResolver` に `Func<ThumbnailDrawPlan, ThumbnailDrawPlan>` を設定すると、1 件分の描画計画（矩形・層の順序・強調の種類・影・名前の体裁）を値の置き換えだけで変更できます（**派生は不要**）。

```csharp
list.DrawPlanResolver = plan => plan with
{
    Highlight = ThumbnailHighlightKind.Selected,
    Label = plan.Label with { FontSize = plan.Label.FontSize + 1, IsBold = true },
};
```

| 差し替える対象 | 差し替え方 |
| --- | --- |
| 強調の種類・影の有無・名前の体裁 | `DrawPlanResolver`（計画の値の置き換え） |
| 強調の帯の濃さ、名前の余白、影の算法 | `Appearance`（`ThumbnailAppearance`）を設定する |
| 配置（列数・余白・項目の寸法） | `Options`（`ThumbnailOptions`）を設定する |
| 矩形で表せない描画 | 計画のとおりに描いた後、UI 固有の描画 API で上塗りする（描画 API は UI が持つ） |

帯の色そのものは計画が持たず、UI が自分のテーマ（文字色・アクセント色）から解決します（`ThumbnailAppearance.ResolveHighlightArgb`）。 明暗で固定の 2 色を持たないため、利用者がテーマやアクセント色を変えても追随します。

差し替えは値の置き換えだけで完結するため、UI ごとの描画実装（Avalonia は `ThumbnailPanel` ほか。 いずれも内部）は変更不要です。
