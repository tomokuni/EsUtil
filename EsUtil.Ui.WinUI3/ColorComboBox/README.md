# EsUtil.Ui.WinUI3.ColorComboBox

![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.WinUI3.ColorComboBox.svg)
![Release](https://github.com/tomokuni/EsUtil/actions/workflows/release.yml/badge.svg)
![Build](https://github.com/tomokuni/EsUtil/actions/workflows/build.yml/badge.svg?branch=main)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%2B-0078D6)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![AOT](https://img.shields.io/badge/AOT-Ready-00695C)
![WinUI3](https://img.shields.io/badge/UI-WinUI%203-0078D4)
![License](https://img.shields.io/badge/License-MIT-3DA639)

色を選ぶ WinUI 3 のコントロールです。 色名・16 進コードの入力、色の一覧からの選択、カラーピッカー（`ColorPicker`）を 1 つにまとめ、選択色の検証まで行います。

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

`ColorComboBoxControl` は 1 行の複合コントロールです（基底は `Microsoft.UI.Xaml.Controls.Grid`）。 左から「現在色のスウォッチ」「テキスト入力」「一覧ボタン（▼）」「カラーピッカー ボタン（…）」を並べます。

- 4 つの区画は**隙間なく並べ、外周の 1 つの枠に収めます**（`Border` は 1 つだけ）。 区切りは枠より薄い 1 ピクセルの垂直線です。
- **保存値**は `#AARRGGBB`（アルファが先頭）です。
- **画面表示**は既定で `{色名または #RRGGBB}+{不透明度%}`（100% なら `+…` を省く）です。 `TextFormat` で `#AARRGGBB` へ切り替えられます。
- 入力は色名 / `#RRGGBB` / `#AARRGGBB` / それらに `+{0〜100}%` を付けた形を受け付けます。
- スウォッチと入力欄のツールチップ（`ToolTipService`）に「色名 + 不透明度」と「`#AARRGGBB`」を 2 行で表示します。

## 2. 機能一覧

- 色名・16 進コードのテキスト入力（入力のたびに解析して反映）
- 色の一覧からの選択（色名順・色相順を切り替え可能）
- WinUI のカラーピッカー（色相環またはパレット・アルファ編集あり）
- 現在色のスウォッチ表示
- 入力欄の表記を切替可能（`TextFormat`。 色名 + 不透明度 / `#AARRGGBB`）
- スウォッチと入力欄のツールチップ（色名 + 不透明度 / `#AARRGGBB` の 2 行）
- 入力の検証（無効な入力はエラー状態になり、フォーカスを外すと最後の有効値へ戻る）
- `ILogger` による変更の記録（`Log` / `LogPrefix`）

## 3. システム要件

| 項目 | 内容 |
| --- | --- |
| 対象フレームワーク | `net10.0-windows10.0.22621.0`（Windows App SDK 1.x） |
| 対応 OS | Windows 10 バージョン 2004（ビルド 19041）以降 |
| 言語 | C# 14（.NET 10） |
| Native AOT | 対応（AOT で発行した配布物が起動することを実測）。 ただし AOT では managed コレクションを `ItemsSource` へ渡せない（`Items.Add` を使う） |

---

## 4. インストール

```xml
<ProjectReference Include="..\EsUtil.Ui.WinUI3\ColorComboBox\src\EsUtil.Ui.WinUI3.ColorComboBox.csproj" />
```

必要な名前空間:

```csharp
using EsUtil.Platform.Core.Palette;   // ColorSortMode（並び順）
using EsUtil.Ui.WinUI3.ColorComboBox;
```

---

## 5. クイックスタート

```csharp
var color = new ColorComboBoxControl
{
    SelectedColor = "#80FF0000",              // 保存値（色名や "Red+50%" も設定できる）
    ColorSortMode = ColorSortMode.ByHue,
    TextFormat = ColorTextFormat.HexCode,     // 入力欄を #AARRGGBB で表示する（既定は色名 + 不透明度）
};
color.SelectedColorChanged += (_, _) => Save(color.SelectedColor);

Host.Children.Add(color);
```

- **XAML からは置けません**（コードで生成します。 WinUI 3 の `ColorPicker` を内包するため、コードビハインドでの組み立てを前提にしています）。
- `SelectedColor` へ設定した値は**保存値の形へ正規化**されます（`Red` → `#FFFF0000`）。

---

## 6. カスタマイズ

| 変える対象 | 入口 |
| --- | --- |
| 色の一覧の並び順 | `ColorSortMode`（`ByName` / `ByHue`） |
| 入力欄の表記 | `TextFormat`（`NameWithAlpha` / `HexCode`） |
| 一覧に出す色 | コアの `ColorNames`（Windows 標準の色名。 システム色は含まない） |
| ログの接頭辞 | `LogPrefix`（既定 `[ColorComboBox]`） |

一覧と色文字列の規則はコアが単一所有するため、**本コントロールに派生や差し替え点はありません**（4 つの UI で同じ結果にするため）。

---

## 7. ログ

`Log` へ `ILogger` を設定すると、選択色の変更を記録します（既定は出力なし）。

| レベル | 出力例 |
| --- | --- |
| Debug | `[ColorComboBox] Setting: SelectedColor #FFFFFFFF → #80FF0000` |

接頭辞は `LogPrefix` で設定します。 ログ基盤（出力先・レベルの制御）はホストが所有します。

---

## 8. 未対応

- **XAML からの配置**: 行わない（コードで生成する。 内包する `ColorPicker` の構成を保つため）。
- **複数色の同時編集**: 持たない（1 つの色だけを扱う）。
- **色名の追加・変更**: 持たない（色名は Windows 標準。 追加したい場合はコアの `ColorNames` を拡張する）。

---

## 9. サンプルアプリケーション

`sample/` に動作確認用のサンプル アプリケーションがあります。 起動方法と確認項目は [`sample/README.md`](sample/README.md) を参照してください。

```powershell
dotnet run --project EsUtil.Ui.WinUI3/ColorComboBox/sample/EsUtil.Ui.WinUI3.ColorComboBox.Sample.csproj
```

---

## 10. ドキュメント

- [docs/SPEC_API.md](docs/SPEC_API.md) — 公開 API のリファレンス
- [docs/SPEC_System.md](docs/SPEC_System.md) — 構造と依存
- 色の規則（色名・受理する入力・保存値）… [`EsUtil.Platform.Core` の SPEC_API](../../EsUtil/Platform.Core/docs/SPEC_API.md)

---

## 11. プロジェクト構成

```text
EsUtil.Ui.WinUI3/ColorComboBox/
├── src/                                            # UI 層（WinUI の要素の組み立てと同期）
│   ├── EsUtil.Ui.WinUI3.ColorComboBox.csproj        # 独立プロジェクト（Platform.Core を参照）
│   ├── ColorComboBoxControl.cs                      # 公開 API・入力と状態の同期・一覧とピッカー
│   └── LogMessage.cs                                # ログ定義（LoggerMessage ソース生成）
├── docs/                                           # SPEC_API.md / SPEC_System.md
├── sample/                                         # サンプルアプリ（sample/README.md 参照）
└── tests/                                          # 単体テスト（XAML ランタイム不要の範囲）
```

---

## 12. テスト

```powershell
dotnet test EsUtil.Ui.WinUI3/ColorComboBox/tests/EsUtil.Ui.WinUI3.ColorComboBox.Tests.csproj
```

WinUI のコントロールは XAML ランタイム（`Application`）を必要とするため、テストは**色文字列の規則がコアと一致すること**と**ログ定義**を対象にします。 入力と表示は利用側アプリで確認します。

---

## 13. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
