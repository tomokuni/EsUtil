# EsUtil.Ui.WinUI3.ColumnPacking サンプルアプリケーション

## 目次 <!-- omit from toc -->

- [1. 概要](#1-概要)
- [2. 実行方法](#2-実行方法)
- [3. 操作方法](#3-操作方法)
- [4. 検証項目（動作確認チェックリスト）](#4-検証項目動作確認チェックリスト)
- [5. 構成](#5-構成)
- [6. 使い方（初めての方向け）](#6-使い方初めての方向け)
- [7. 拡張例（派生クラス）](#7-拡張例派生クラス)
- [8. ドキュメント](#8-ドキュメント)

## 1. 概要

`EsUtil.Ui.WinUI3.ColumnPacking` の使い方を**動くアプリ**として確認するためのサンプルです（WinUI 3 / アンパッケージ）。

| 示すこと | 内容 |
| --- | --- |
| 2 種類の子 | データから作った子（`SetItems` + `ItemTemplate`。 Native AOT でも動く）と、固定の子を同時に並べる |
| 配置の制御 | 列の上限・行間・列間・項目数をスライダーで変える |
| 順序の制御 | 「直接の子を後ろに置く」（`ItemsSourceFirst`）で、データの子と固定の子の並び順が入れ替わる |
| 結果の公開 | レイアウト結果（使用幅・最大列高さ・列数・列の区切り）を公開プロパティとログ（Debug）で読む |

## 2. 実行方法

```powershell
dotnet run --project EsUtil.Ui.WinUI3/ColumnPacking/sample/EsUtil.Ui.WinUI3.ColumnPacking.Sample.csproj
```

**アンパッケージ**（MSIX を使わない）で動かします。 プロジェクトは **AnyCPU**（アーキテクチャを固定しない）で、通常のビルドはインストール済みの Windows App SDK ランタイムを使います。

Windows App SDK を同封する（自己完結）ときは `-r` を渡します（`-r` があるときだけ自己完結になります）。

```powershell
dotnet run --project EsUtil.Ui.WinUI3/ColumnPacking/sample/EsUtil.Ui.WinUI3.ColumnPacking.Sample.csproj -r win-x64
```

## 3. 操作方法

| 操作 | 結果 |
| --- | --- |
| 「列の上限」を動かす | 列数の上限が変わり、最大列高さが変わる |
| 「行間」「列間」を動かす | 子の間隔が変わる |
| 「アイテム数」を動かす | 項目の件数が変わり、子が作り直される |
| 「直接の子を後ろに置く」を切り替える | データの要素と固定の子の並び順が入れ替わる |
| 「ログ接頭辞」を変える | ライブラリのログの接頭辞が変わる（既定 `[ColumnPacking]`） |
| 「項目を再設定（SetItems）」（メソッド操作） | 現在の「アイテム数」で項目を作り直し、`SetItems` を呼び直す |

## 4. 検証項目（動作確認チェックリスト）

- [ ] 起動直後に、データの項目（青い枠）と直接の子（オレンジの枠）が同じパネルに並ぶ。
- [ ] 「列の上限」を 1 にすると 1 列になる。
- [ ] 「アイテム数」を増やすと列数が増え、最大列高さが小さくなる（幅の上限に収まる範囲で）。
- [ ] 「直接の子を後ろに置く」を外すと、オレンジの枠（直接の子）が先頭へ移る。
- [ ] ウィンドウを狭くすると列数が減り、1 列に近づく。

## 5. 構成

```text
sample/
├── App.xaml / App.xaml.cs                  アプリケーションの入口
├── MainWindow.cs                           サンプルの画面（設定パネル・パネル・ログ）
├── SampleItem.cs                           一覧に並べる項目（`[Bindable]`）
├── ItemTemplates.xaml                      データから作る子の DataTemplate
├── ItemTemplates.xaml.cs                   そのコードビハインド（`{Binding}` を使うため中身は初期化のみ）
└── app.manifest                            DPI 認識と対応 OS の宣言
```

## 6. 使い方（初めての方向け）

1. 上のスライダーで**列の上限**を 4 にします。
2. **アイテム数**を 30 程度に増やすと、列が増えて高さが揃います。
   レイアウト結果は、ログ・レベルを `Debug` 以上にすればログ領域に `Layout: UsedWidth=… MinHeight=…` として出力されます。
**AOT でも動くデータ駆動**: Native AOT では `ItemsSource`（依存関係プロパティ）に管理コレクションを渡せません（`E_INVALIDARG`。 [`.github/ビルド設定規約.md`](../../../.github/ビルド設定規約.md) §6.1）。 本サンプルは `SetItems`（managed のリストをそのまま渡す入口）でデータを渡し、要素は `ItemTemplate` から作ります。

**初期化の順序**: `ItemTemplate` → 直接の子の追加 → `SetItems`。 `ItemTemplate` を設定するとその時点で子が再構築されるため、直接の子はその後で足します。

## 7. 拡張例（派生クラス）

レイアウト結果は読み取り専用の依存関係プロパティ（`LayoutMinHeight` / `LayoutUsedWidth` / `LayoutColumnCount` / `LayoutColumnSegments`）として公開されているため、派生しなくても読めます。
派生する場合の拡張点は `MeasureOverride` / `ArrangeOverride` と、プロパティ変更のフック（`OnItemsSourceChangedInternal` など）です。

## 8. ドキュメント

- コントロールの使い方 … [`../README.md`](../README.md)
- API リファレンス … [`../docs/SPEC_API.md`](../docs/SPEC_API.md)
- システム仕様書 … [`../docs/SPEC_System.md`](../docs/SPEC_System.md)
