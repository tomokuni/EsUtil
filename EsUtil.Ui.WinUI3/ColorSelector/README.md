# EsUtil.Ui.WinUI3.ColorSelector

![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinUI3.ColorSelector.svg)
![Release](https://github.com/tomokuni/EsUtil/actions/workflows/release.yml/badge.svg)
![Build](https://github.com/tomokuni/EsUtil/actions/workflows/build.yml/badge.svg?branch=main)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%20(1809%2C%2017763)%2B-0078D6)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![AOT](https://img.shields.io/badge/AOT-Ready-00695C)
![WinUI3](https://img.shields.io/badge/UI-WinUI%203-0078D4)
![License](https://img.shields.io/badge/License-MIT-3DA639)

色を選ぶ WinUI 3 のコントロールです。 標準のコンボボックス（編集可）を入力欄に使い、**その左に現在色の四角を重ねて「■ + 色名」で表示**します。 色名・16 進コードの入力、タブ付きの一覧からの選択、カラーピッカー（`ColorPicker`）を 1 つにまとめ、選択色の検証まで行います。

**色名の表と色文字列の解釈はコア（`EsUtil.Platform.Core` の `Palette`）が単一所有**します。 そのため Avalonia UI 版・MewUI 版と**同じ入力・同じ保存値・同じ並び順**になります。

## 目次 <!-- omit from toc -->

- [1. 概要](#1-概要)
- [2. 機能一覧](#2-機能一覧)
- [3. システム要件](#3-システム要件)
- [4. インストール](#4-インストール)
- [5. クイックスタート](#5-クイックスタート)
- [6. カスタマイズ](#6-カスタマイズ)
- [7. ログ](#7-ログ)
- [8. 未対応](#8-未対応)
- [9. サンプルアプリケーション](#9-サンプルアプリケーション)
- [10. ドキュメント](#10-ドキュメント)
- [11. プロジェクト構成](#11-プロジェクト構成)
- [12. テスト](#12-テスト)
- [13. ライセンス](#13-ライセンス)

---

## 1. 概要

`ColorSelectorControl` は 1 行の複合コントロールです（基底は `Microsoft.UI.Xaml.Controls.UserControl`）。 中身は標準の **コンボボックス（編集可）** と、その左へ重ねた**現在色の四角**（「■ + 色名」）です。 自前の枠・▼・… は作りません。

- 展開ボタンを押すと、標準の一覧の代わりに**タブ付きの一覧**（`Flyout`）が出ます。 タブは **カスタム → テーマカラー → 色名 → ColorPicker** の順です。
- **入力欄の文字は四角の右から始まり、四角と上下の中央でそろいます**（四角は 12 DIP・左の余白 7 DIP、文字は左から 24 DIP）。
- **保存値**は `#AARRGGBB`（アルファが先頭）です。
- **画面表示**は既定で `{色名または #RRGGBB}+{不透明度%}`（100% なら `+…` を省く）です。 `TextFormat` で `#AARRGGBB` へ切り替えられます。 カスタム タブに登録した色名があるときは、その名前を優先します。
- 入力は色名 / `#RRGGBB` / `#AARRGGBB` / それらに `+{0〜100}%` を付けた形を受け付けます。
- 入力欄のツールチップ（`ToolTipService`）に「色名 + 不透明度」と「`#AARRGGBB`」を 2 行で表示します。

## 2. 機能一覧

- 色名・16 進コードのテキスト入力（入力のたびに解析して反映）
- **標準のコンボボックス**による入力欄と一覧の表示（自前の枠・▼・… を作らない）
- **入力欄は「■ + 色名」で表示**（現在色の四角を入力欄の左に重ね、文字はその右から始める。 文字は上下の中央にそろえる）
- 一覧はタブで切り替える（**カスタム / テーマカラー / 色名 / ColorPicker**。 色名は色相順 / 色名順）
- **カスタム タブ**（`CustomTabs`。 題名と色名/色コードを動的に指定。 複数可。 **一覧**と**■ だけを指定した列数で並べた格子**を選べる（列数 0 = 一覧 / 1 以上 = 格子））
- **カスタム タブの色名を入力欄に表示**（色コードが一致したときに日本語の名前を出す）
- ColorPicker タブでカラーピッカー（`ColorPicker`）を一覧の中へ直接表示
- テーマの色の選択と、テーマの変更への追随（選んだテーマの色は明暗・アクセントの変更に追随）
- **色を確定したときの通知**（`ColorApplied`。 一覧・カラーピッカーで**同じ色を選び直したときも**発生する）
- 入力の検証（無効な入力はエラー状態になり、フォーカスを外すと最後の有効値へ戻る。 無効な間は現在色の四角を隠す）
- バリデーション失敗時の枠線強調（`HighlightBorderOnError`。 既定は無効）
- 入力欄の表記を切替可能（`TextFormat`。 色名 + 不透明度 / `#AARRGGBB`）
- 入力欄のツールチップ（色名 + 不透明度 / `#AARRGGBB` の 2 行）
- タブの表示切替（`ShowCustomTab` / `ShowThemeColorTab` / `ShowNamedColorTab` / `ShowPickerTab`）
- タブが 1 つだけのときの題名の表示切替（`ShowTabTitles`）
- 一覧を開いても**コントロールの幅は変わらない**（一覧だけを、枠・タブの行・項目のうち最も広い幅へ広げる。 一覧はその幅いっぱいに置くため、スクロール バーは右端に来る）
- `ILogger` による変更の記録（`Log` / `LogPrefix`）

## 3. システム要件

| 項目 | 内容 |
| --- | --- |
| 対象フレームワーク | `net10.0-windows10.0.17763.0`（Windows App SDK 2.0.1 以降） |
| 対応 OS | Windows 10 バージョン 1809（ビルド 17763）以降 |
| 言語 | C# 14（.NET 10） |
| Native AOT | 対応（AOT で発行した配布物が起動することを実測）。 ただし AOT では managed コレクションを `ItemsSource` へ渡せない（`Items.Add` を使う） |

---

## 4. インストール

```xml
<ProjectReference Include="..\EsUtil.Ui.WinUI3\ColorSelector\src\EsUtil.Ui.WinUI3.ColorSelector.csproj" />
```

必要な名前空間:

```csharp
using EsUtil.Platform.Core.Palette;   // ColorSortMode（並び順）
using EsUtil.Ui.WinUI3.ColorSelector;
```

---

## 5. クイックスタート

```csharp
var color = new ColorSelectorControl
{
    SelectedColor = "#80FF0000",              // 保存値（色名や "Red+50%" も設定できる）
    ColorSortMode = ColorSortMode.ByHue,
    TextFormat = ColorTextFormat.HexCode,     // 入力欄を #AARRGGBB で表示する（既定は色名 + 不透明度）
};
color.SelectedColorChanged += (_, _) => Save(color.SelectedColor);
color.ColorApplied += (_, _) => ApplyToSelection(color.SelectedColor); // 一覧・ピッカーで確定するたび（同じ色でも）

Host.Children.Add(color);
```

- **XAML からは置けません**（コードで生成します。 WinUI 3 の `ColorPicker` を内包するため、コードビハインドでの組み立てを前提にしています）。
- `SelectedColor` へ設定した値は**保存値の形へ正規化**されます（`Red` → `#FFFF0000`）。
- 同じ色を選び直したときは `SelectedColorChanged` が出ません。 確定を伝える必要がある場合は `ColorApplied`（一覧・カラーピッカーで色を確定するたびに発生）を使ってください。

---

## 6. カスタマイズ

| 変える対象 | 入口 |
| --- | --- |
| 色の一覧の並び順 | `ColorSortMode`（`ByName` / `ByHue`） |
| 入力欄の表記 | `TextFormat`（`NameWithAlpha` / `HexCode`） |
| 一覧に出す色 | コアの `ColorNames`（Windows 標準の色名。 システム色は含まない）と、UI が提供するテーマの色（`ShowThemeColors` で一覧から外せる） |
| 一覧のタブ | `ShowCustomTab` / `ShowThemeColorTab` / `ShowNamedColorTab` / `ShowPickerTab` で取捨でき、`CustomTabs`（`ColorSelectorCustomTab` の並び）で用途ごとのタブ（例: 代表色）を足せる。 カスタム タブは `Columns` で色名付きの一覧（`0`）と ■ だけの格子（`1` 以上 = その列数）を選べる。 タブが 1 つだけなら `ShowTabTitles` で題名の行を省ける |
| ログの接頭辞 | `LogPrefix`（既定 `[ColorSelector]`） |

一覧と色文字列の規則はコアが単一所有するため、**本コントロールに派生や差し替え点はありません**（3 つの UI で同じ結果にするため）。

---

## 7. ログ

`Log` へ `ILogger` を設定すると、選択色の変更を記録します（既定は出力なし）。

| レベル | 出力例 |
| --- | --- |
| Information | `[ColorSelector] 設定: 選択色 #FFFFFFFF → #80FF0000` |

接頭辞は `LogPrefix` で設定します。 ログ基盤（出力先・レベルの制御）はホストが所有します。

---

## 8. 未対応

- **XAML からの配置**: 行わない（コードで生成する。 内包する `ColorPicker` の構成を保つため）。
- **複数色の同時編集**: 持たない（1 つの色だけを扱う）。
- **色名の追加・変更**: 持たない（色名は Windows 標準。 追加したい場合はコアの `ColorNames` を拡張する）。 よく使う色は `CustomTabs` でタブとして足せる。

---

## 9. サンプルアプリケーション

`sample/` に動作確認用のサンプル アプリケーションがあります。 起動方法と確認項目は [`sample/README.md`](sample/README.md) を参照してください。

```powershell
dotnet run --project EsUtil.Ui.WinUI3/ColorSelector/sample/EsUtil.Ui.WinUI3.ColorSelector.Sample.csproj
```

---

## 10. ドキュメント

- [docs/SPEC_API.md](docs/SPEC_API.md) — 公開 API のリファレンス
- [docs/SPEC_System.md](docs/SPEC_System.md) — 構造と依存
- 色の規則（色名・受理する入力・保存値）… [`EsUtil.Platform.Core` の SPEC_API](../../EsUtil/Platform.Core/docs/SPEC_API.md)

---

## 11. プロジェクト構成

```text
EsUtil.Ui.WinUI3/ColorSelector/
├── src/                                      # UI 層（WinUI の要素の組み立てと同期）
│   ├── EsUtil.Ui.WinUI3.ColorSelector.csproj # 独立プロジェクト（Platform.Core を参照）
│   ├── ColorSelectorControl.cs               # 公開 API・入力と状態の同期・タブ付き一覧とピッカー
│   ├── ColorApplyPlan.cs                     # 一覧・カラーピッカーで色を確定したときの適用の計画（内部）
│   ├── ColorSelectorItem.cs                  # 一覧の 1 件（表示名と適用値）
│   ├── ColorSelectorCategory.cs              # 一覧の項目の分類（テーマカラー / 色名 / カスタム）
│   ├── ColorSelectorTabSpec.cs               # 一覧の 1 タブ（題名・識別子・■ の列数・項目）
│   ├── ColorSelectorCustomTab.cs             # 利用者が追加するタブ（公開型）
│   ├── ColorSelectorCustomColor.cs           # カスタム タブの 1 色（公開型）
│   └── ColorSelectorLogMessage.cs            # ログ定義（LoggerMessage ソース生成）
├── docs/                                     # SPEC_API.md / SPEC_System.md
├── sample/                                   # サンプルアプリ（sample/README.md 参照）
└── tests/                                    # 単体テスト（XAML ランタイム不要の範囲）
```

---

## 12. テスト

```powershell
dotnet test EsUtil.Ui.WinUI3/ColorSelector/tests/EsUtil.Ui.WinUI3.ColorSelector.Tests.csproj
```

WinUI のコントロールは XAML ランタイム（`Application`）を必要とするため、テストは**色文字列の規則がコアと一致すること**、**一覧のタブの決め方**（`CreateTabs` / `ShouldShowTabStrip`）、**格子の行数と幅**（`CalculateSwatchRows` / `CalculateSwatchGridWidth`）、**カスタム タブの色名の表示**（`FormatDisplayText` / `ResolveCustomName`）、**入力欄の左の四角の表示**（`TryResolveCurrentColorSwatch` / `TextLeftIndent`）、**確定の通知の扱い**（`PlanItemSelection` / `PlanPickerSelection`）、**一覧の幅の決め方**（`ResolveFlyoutWidth` / `CalculateTabRowWidth` / `ResolveTabWidth`）、**ログ定義**を対象にします。 入力と表示はサンプル アプリと利用側アプリで確認します。

---

## 13. ライセンス

[MIT License](LICENSE)
