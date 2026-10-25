# EsUtil.Ui.WinUI3.MultiColumn サンプルアプリケーション

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

`EsUtil.Ui.WinUI3.MultiColumn` の使い方を**動くアプリ**として確認するためのサンプルです（WinUI 3 / アンパッケージ）。

| 示すこと | 内容 |
| --- | --- |
| 2 種類の子 | `ItemsSource`（データ）と、XAML に直接書いた子を同時に並べる |
| 配置の制御 | 列の上限・行間・列間・項目数をスライダーで変える |
| 順序の制御 | 「ItemsSource を先に置く」で並び順が変わる |
| 結果の公開 | レイアウト結果（使用幅・最大列高さ・列数・列の区切り）をデバッグ表示で読む |
| 派生 | `MultiColumnPanelDebugText`（パネルを継承し、結果を画面へ重ねる） |

## 2. 実行方法

```powershell
dotnet run --project EsUtil.Ui.WinUI3/MultiColumn/sample/EsUtil.Ui.WinUI3.MultiColumn.Sample.csproj
```

**アンパッケージ + 自己完結**で動かすため、対象アーキテクチャを明示しています（既定は `win-x64`。 `-r win-arm64` 等で上書きできます）。

```powershell
dotnet run --project EsUtil.Ui.WinUI3/MultiColumn/sample/EsUtil.Ui.WinUI3.MultiColumn.Sample.csproj -r win-x64
```

## 3. 操作方法

| 操作 | 結果 |
| --- | --- |
| 「列の上限」を動かす | 列数の上限が変わり、最大列高さが変わる |
| 「行間」「列間」を動かす | 子の間隔が変わる |
| 「アイテム数」を動かす | `ItemsSource` の件数が変わり、子が作り直される |
| 「ItemsSource を先に置く」を切り替える | データの要素と直接の子の並び順が入れ替わる |
| 「デバッグ表示」を入れる | パネルの左上にレイアウト結果が重なって表示される |

## 4. 検証項目（動作確認チェックリスト）

- [ ] 起動直後に、データの項目（青い枠）と直接の子（オレンジの枠）が同じパネルに並ぶ。
- [ ] 「列の上限」を 1 にすると 1 列になる。
- [ ] 「アイテム数」を増やすと列数が増え、最大列高さが小さくなる（幅の上限に収まる範囲で）。
- [ ] 「ItemsSource を先に置く」を切ると、オレンジの枠が先に並ぶ。
- [ ] 「デバッグ表示」を入れると、`MinHeight` / `UsedWidth` / 列数 / 列の区切りが表示される。
- [ ] ウィンドウを狭くすると列数が減り、1 列に近づく。

## 5. 構成

```text
sample/
├── App.xaml / App.xaml.cs                  アプリケーションの入口
├── MainWindow.xaml / MainWindow.xaml.cs    サンプルの画面（スライダーとパネル）
├── SampleItem.cs                           一覧に並べる項目
├── Helper.cs                               デバッグ表示を祖先のパネルへ取り付ける補助
├── FrameworkElementExtensions.cs           内側の大きさ・余白の表示用の書式
├── MultiColumnPanelDebugText.cs            パネルを継承したデバッグ表示
└── app.manifest                            DPI 認識と対応 OS の宣言
```

## 6. 使い方（初めての方向け）

1. 上のスライダーで**列の上限**を 4 にします。
2. **アイテム数**を 30 程度に増やすと、列が増えて高さが揃います。
3. **デバッグ表示**を入れると、`MinHeight` が「最大列高さ」であることが読めます。
4. **ItemsSource を先に置く**を切ると、XAML に書いた 2 つの要素が先頭へ移ります。

## 7. 拡張例（派生クラス）

`MultiColumnPanelDebugText` は `MultiColumnPanel` を継承し、レイアウト結果（`LayoutMinHeight` など）を読んで表示するだけの例です。
表示の要素は**自分の子ではなく祖先のパネルへ追加**します（自分の子にすると配置の対象になり、結果が変わってしまいます）。

## 8. ドキュメント

- コントロールの使い方 … [`../README.md`](../README.md)
- API リファレンス … [`../docs/SPEC_API.md`](../docs/SPEC_API.md)
- システム仕様書 … [`../docs/SPEC_System.md`](../docs/SPEC_System.md)
