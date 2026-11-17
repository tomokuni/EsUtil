# EsUtil.Ui.MewUI.ColorSelector

![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.MewUI.ColorSelector.svg)
![Release](https://github.com/tomokuni/EsUtil/actions/workflows/release.yml/badge.svg)
![Build](https://github.com/tomokuni/EsUtil/actions/workflows/build.yml/badge.svg?branch=main)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%20(1809%2C%2017763)%2B-0078D6)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![AOT](https://img.shields.io/badge/AOT-Ready-00695C)
![MewUI](https://img.shields.io/badge/UI-MewUI-00BFFF)
![License](https://img.shields.io/badge/License-MIT-3DA639)

色を選ぶ MewUI のコントロールです。 色名・16 進コードの入力、色の一覧からの選択、カラーピッカー（`ColorPicker`）を 1 つにまとめ、選択色の検証まで行います。

**色名の表と色文字列の解釈はコア（`EsUtil.Platform.Core` の `Palette`）が単一所有**します。 そのため Avalonia UI 版・WinUI 3 版と**同じ入力・同じ保存値・同じ並び順**になります（UI ごとに規則を持ちません）。

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

`ColorSelectorControl` は 1 行の複合コントロールです。 **標準のコンボボックス**が枠・入力欄・展開ボタンを担い、その左に**現在色の四角**を重ねて「■ + 色名」で表示します。 展開すると**タブ付きの一覧**（カスタム / テーマカラー / 色名 / ColorPicker）が出ます。

- **自前の枠・▼・… のボタン・区切りの線は作りません**（標準のコンボボックスをそのまま使います）。
- MewUI 0.22.1 の `ComboBox` は**編集できる入力欄を持たない**ため、枠も背景も持たないテキスト入力をコンボボックスの文字の位置へ重ね、文字の入力と表示を担わせます（AvaloniaUI 版の `IsEditable` の代替）。
- **入力欄は「■ + 色名」で表示**します（現在色の四角を入力欄の左へ重ね、文字はその右から始める。 文字は上下の中央にそろえる）。 無効な入力の間は四角を隠します。
- 展開時に**標準の一覧は出しません**。 展開ボタン（右端 30 DIP）の押下を自前で受けて標準の開閉を止め、タブ付きのポップアップを出します（押下の処理の後へ回して開く。 キーボードの Alt+↓ / F4 でも開けます）。
- **保存値**は `#AARRGGBB`（アルファが先頭。 例: `#80FF0000`）です（設定ファイルへそのまま保存できます）。
- **画面表示**は既定で `{色名または #RRGGBB}+{不透明度%}`（100% なら `+…` を省く。 例: `Red+50%`）です。 `TextFormat` で `#AARRGGBB` へ切り替えられます。
- **カスタム タブの色名を入力欄に出します**（色コードが一致したとき。 不透明度があるときは `+{n}%` を足す）。
- 入力は色名 / `#RRGGBB` / `#AARRGGBB` / それらに `+{0〜100}%` を付けた形を受け付けます。
- 入力欄のツールチップに「色名 + 不透明度」と「`#AARRGGBB`」を 2 行で表示します。

## 2. 機能一覧

- 色名・16 進コードのテキスト入力（入力のたびに解析して反映）
- **標準のコンボボックス**による枠・展開ボタン（自前の枠・▼・… を作らない）
- **入力欄は「■ + 色名」で表示**（現在色の四角を入力欄の左に重ね、文字はその右から始める。 文字は上下の中央にそろえる）
- 展開ボタン（右端）の押下でタブ付きの一覧を開く（標準の一覧を開かせない。 **開閉の往復がない**）。 キーボードの Alt+↓ / F4 でも開ける
- 色の一覧からの選択（色名順・色相順を切り替え可能）
- テーマの色の一覧（タブ「テーマカラー」）からの選択と、テーマの変更への追随（選んだテーマの色は明暗・アクセントの変更に追随）
- 一覧はタブで切り替える（**カスタム / テーマカラー / 色名 / ColorPicker**。 色名は色相順 / 色名順）
- **カスタム タブ**（`CustomTabs`。 題名と色名/色コードを動的に指定。 複数可。 **一覧**と**色の四角を 6 列に並べた格子**を選べる）
- **カスタム タブの色名を入力欄に表示**（色コードが一致したときに日本語の名前を出す。 不透明度があるときは `+{n}%`）
- タブの中身は**開いたときに遅延作成**（開いていないカラーピッカーのタブを生成しない）
- ネイティブのカラーピッカー（`ColorPicker`。 色相環とパレットの両方、アルファ編集あり）
- 入力欄の表記を切替可能（`TextFormat`。 色名 + 不透明度 / `#AARRGGBB`）
- 入力欄のツールチップ（色名 + 不透明度 / `#AARRGGBB` の 2 行）
- 入力の検証（無効な入力はエラー状態になり、フォーカスを外すと最後の有効値へ戻る）
- **色を確定したときの通知**（`ColorApplied`。 一覧・カラーピッカーで**同じ色を選び直したときも**発生する）
- `ILogger` による変更の記録（`Log` / `LogPrefix`）
- タブの表示切替（`ShowCustomTab` / `ShowThemeColorTab` / `ShowNamedColorTab` / `ShowPickerTab`）
- タブが 1 つだけのときの題名の表示切替（`ShowTabTitles`）
- 一覧を開いても**コントロールの幅は変わらない**（一覧だけが内容に合わせて広がる）

## 3. システム要件

| 項目 | 内容 |
| --- | --- |
| 対象フレームワーク | `net10.0-windows10.0.17763.0` |
| 対応 OS | Windows 専用（`net10.0-windows10.0.17763.0`）。 Windows 10 バージョン 1809（ビルド 17763）以降。 |
| UI フレームワーク | MewUI 0.22.1（`MewUIVersion` プロパティで固定） |
| 言語 | C# 14（.NET 10） |
| Native AOT | 対応（`IsAotCompatible=true`。 AOT の解析警告が 0 件であることをビルドで検査する） |

---

## 4. インストール

```xml
<ProjectReference Include="..\EsUtil.Ui.MewUI\ColorSelector\src\EsUtil.Ui.MewUI.ColorSelector.csproj" />
```

必要な名前空間:

```csharp
using EsUtil.Platform.Core.Palette;   // ColorSortMode（並び順）と EsColor（窓口）
using EsUtil.Ui.MewUI.ColorSelector;
```

---

## 5. クイックスタート

```csharp
var color = new ColorSelectorControl
{
    SelectedColor = "#80FF0000",              // 保存値（色名や "Red+50%" も設定できる）
    ColorSortMode = ColorSortMode.ByHue,      // 色名順にするなら ByName
    TextFormat = ColorTextFormat.HexCode,     // 入力欄を #AARRGGBB で表示する（既定は色名 + 不透明度）
};
color.SelectedColorChanged += (_, _) => Save(color.SelectedColor);

panel.Children(color);
```

- `SelectedColor` へ設定した値は**保存値の形へ正規化**されます（`Red` → `#FFFF0000`）。
- 不正な値を設定すると `HasValidationError` が `true` になり、`ValidationErrorMessage` に理由が入ります。
- 一覧・カラーピッカーで色を確定すると `ColorApplied` が発生します（**同じ色を選び直したときも発生**。 入力欄へ打ち込んだ場合は発生しません。 確定はフォーカスを外したときです）。

---

## 6. カスタマイズ

| 変える対象 | 入口 |
| --- | --- |
| 色の一覧の並び順 | `ColorSortMode`（`ByName` / `ByHue`） |
| 入力欄の表記 | `TextFormat`（`NameWithAlpha` / `HexCode`） |
| 一覧に出す色 | コアの `ColorNames`（Windows 標準の色名。 システム色は含まない）と、UI が提供するテーマの色（`ShowThemeColors` で一覧から外せる） |
| 一覧に出すタブ | `ShowCustomTab` / `ShowThemeColorTab` / `ShowNamedColorTab` / `ShowPickerTab`（タブを隠す）、`ShowTabTitles`（タブが 1 つだけのときに題名を省く）、`CustomTabs`（用途ごとの代表色を足す。 空のタブは表示しない。 並べ方は一覧 / 色の四角 6 列の格子） |
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

- **ComboBox の編集可**: MewUI 0.22.1 の `ComboBox` に編集できる入力欄はありません。 文字の編集は、コンボボックスの文字の位置へ重ねたテキスト入力が担います（AvaloniaUI 版と同じ操作感）。
- **カラーピッカーのインライン表示**: MewUI の `ColorPicker` はドロップダウンであり、一覧のポップアップの中へ埋め込めません。 「ColorPicker」タブを選ぶと一覧を閉じてからピッカーのドロップダウンを開きます（2 つのドロップダウンを重ねません）。
- **複数色の同時編集**: 持たない（1 つの色だけを扱う）。
- **色名の追加・変更**: 持たない（色名は Windows 標準。 追加したい場合はコアの `ColorNames` を拡張する）。

---

## 9. サンプルアプリケーション

`sample/` に動作確認用のサンプル アプリケーションがあります。 起動方法と確認項目は [`sample/README.md`](sample/README.md) を参照してください。

```powershell
dotnet run --project EsUtil.Ui.MewUI/ColorSelector/sample/EsUtil.Ui.MewUI.ColorSelector.Sample.csproj
```

---

## 10. ドキュメント

- [docs/SPEC_API.md](docs/SPEC_API.md) — 公開 API のリファレンス
- [docs/SPEC_System.md](docs/SPEC_System.md) — 構造と依存
- 色の規則（色名・受理する入力・保存値）… [`EsUtil.Platform.Core` の SPEC_API](../../EsUtil/Platform.Core/docs/SPEC_API.md)

---

## 11. プロジェクト構成

```text
EsUtil.Ui.MewUI/ColorSelector/
├── src/                                           # UI 層（MewUI の要素の組み立てと同期）
│   ├── EsUtil.Ui.MewUI.ColorSelector.csproj        # 独立プロジェクト（Platform.Core を参照）
│   ├── ColorSelectorControl.cs                     # 公開 API・入力と状態の同期・一覧とピッカー
│   ├── ColorSelectorCategory.cs                    # 一覧の項目の分類（internal）
│   ├── ColorSelectorItem.cs                        # 一覧の 1 件（表示名と適用値。 internal）
│   ├── ColorSelectorTabSpec.cs                     # 一覧の 1 タブ（題名・識別子・並べ方・項目。 internal）
│   ├── ColorSelectorTabLayout.cs                   # カスタム タブの並べ方（一覧 / 格子）
│   ├── ColorSelectorCustomTab.cs                   # 利用者が追加するタブ
│   ├── ColorSelectorCustomColor.cs                 # カスタム タブの 1 色
│   └── ColorSelectorLogMessage.cs                  # ログ定義（LoggerMessage ソース生成）
├── docs/                                          # SPEC_API.md / SPEC_System.md
├── sample/                                        # サンプルアプリ（sample/README.md 参照）
└── tests/                                         # 単体テスト（ウィンドウ不要の範囲）
```

---

## 12. テスト

```powershell
dotnet test EsUtil.Ui.MewUI/ColorSelector/tests/EsUtil.Ui.MewUI.ColorSelector.Tests.csproj
```

ウィンドウを開かずに検証できる範囲（既定値・正規化・検証・イベント・ログ）を対象にします。 入力（クリック・キー）は利用側アプリで確認します。

---

## 13. ライセンス

[MIT License](LICENSE)
