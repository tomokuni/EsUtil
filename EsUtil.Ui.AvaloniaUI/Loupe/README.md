# EsUtil.Ui.AvaloniaUI.Loupe

![NuGet](https://img.shields.io/nuget/v/EsUtil.Ui.AvaloniaUI.Loupe.svg)
![Release](https://github.com/tomokuni/EsUtil/actions/workflows/release.yml/badge.svg)
![Build](https://github.com/tomokuni/EsUtil/actions/workflows/build.yml/badge.svg?branch=main)
![Platform](https://img.shields.io/badge/Platform-Portable-6E6E6E)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![AOT](https://img.shields.io/badge/AOT-Ready-00695C)
![Avalonia](https://img.shields.io/badge/UI-Avalonia-8D46D6)
![License](https://img.shields.io/badge/License-MIT-3DA639)

> プロジェクト名: `EsUtil.Ui.AvaloniaUI.Loupe`（Avalonia UI 版）。 UI 非依存のロジックは `EsUtil.Feature.Loupe.Core`、MewUI 版は対になるコントロール（別プロジェクト）にあります。

## 目次 <!-- omit from toc -->

- [1. 概要](#1-概要)
- [2. 機能一覧](#2-機能一覧)
- [3. システム要件](#3-システム要件)
- [4. インストール](#4-インストール)
- [5. クイックスタート](#5-クイックスタート)
  - [5.1 AXAML](#51-axaml)
  - [5.2 ViewModel](#52-viewmodel)
  - [5.3 Code-behind](#53-code-behind)
- [6. 画像の受け渡し](#6-画像の受け渡し)
- [7. カスタマイズ](#7-カスタマイズ)
- [8. ログ](#8-ログ)
- [9. 未対応](#9-未対応)
- [10. サンプルアプリケーション](#10-サンプルアプリケーション)
- [11. ドキュメント](#11-ドキュメント)
- [12. プロジェクト構成](#12-プロジェクト構成)
- [13. テスト](#13-テスト)
- [14. ライセンス](#14-ライセンス)

---

## 1. 概要

Avalonia UI 用の拡大鏡コントロールです。 画像の任意のピクセル座標を中心に領域を拡大し、十字線・枠線付きで表示します（画像外は無地）。

画像端から外れた領域は透明として扱われるため、画像がルーペの視野から離れるほど、その部分だけ画像外の色（無地）で埋まります。

---

## 2. 機能一覧

- Avalonia Binding 対応の `LoupeControl`
- コアの画像（`EsReadOnlyBitmap`）を受け取る画像入力API（MewUI 版 / WinUI3 版 / WinForms 版と同じ契約）
- 省略可能な表示用の射影（`DisplaySource: IImage?`）。 渡すと切り出しの複製を作らずに描ける（寸法が一致するときだけ使う）
- 1〜64倍の拡大表示
- 画像範囲外は描かない（画像外の色が見える）
- 画像外（ペーストボード）の色（`Appearance`）設定
- 十字線の色設定（不透明度は色のアルファに統合）
- 中心座標ラベル表示（`(x, y)` 形式・表示/色/背景/フォントサイズ/角丸設定可能）
- 枠線の色（`Appearance`）・太さ（`Options`）設定
- **色と、ラベルのフォントサイズ・角丸は `Appearance` プロパティ（`LoupeAppearance`）、寸法（タイルサイズ・枠線の太さ）は `Options`（`LoupeOptions`）で設定**（コアは寸法・表示可否だけを持ち、描画にだけ必要な設定は描画層（`EsUtil.Feature.Loupe.Presenter`）が単一所有する）
- DIP と物理ピクセルの分離（`ClientToPixelScaling`）
- 窓の描画面への直接描画（フレームの描画フェーズで 1 回。 中間 Bitmap の生成も画素の複製もしない）
- 古い描画結果の自動破棄
- 非表示時は描画しない（解放する中間 Bitmap は無い）
- `ILogger` によるエラー通知
- 描画完了通知イベント（`Rendered`・`LoupeRenderedEventArgs` で描画先の `DrawingContext` と描画計画（切り出し左上・拡大率）を通知。 ホスト側の付加情報の描画用。 線分・選択矩形等の破線はホストが描画 API で描く）
- 中心座標ラベルはコントロールが **`Border` + `TextBlock` の子要素として配置**します（文言・表示可否はコア、実寸 → 配置矩形の写像は描画層（`EsUtil.Feature.Loupe.Presenter` の `LoupePresenter`（`LabelPlan` partial））、実寸測定は `FormattedText`、描画順はコアの `LoupeDrawLayer` が単一所有）
- **座標変換ポート（`SetCoordTransform(ICoordTransform?)` + `PositionOffset`）**: 対になるコントロールと同方式。 表示位置（`RenderTransform`）と表示サイズ（要求サイズ）をレイアウト時に自己計算（ウィンドウサイズ変更・ズーム・スクロールに追従）
- **`RefreshPosition()`**: パン・ズーム等のレイアウト非変更なビューポート変化時に、表示位置・サイズを再計算（座標変換ポートは呼び出し毎に最新の表示状態を評価）
- **見た目のカスタマイズ（2 つの入口）**: 色・太さ・表示可否は `Appearance`、矩形の並びで表せる装飾とラベルの文言・位置は `DrawPlanResolver`（`Func<LoupeDrawPlan, LoupeDrawPlan>`）で差し替えます。 矩形で表せない描画（円・斜線等）は `Appearance` の `Show*` を false にして `Rendered` のビットマップへ自前で描き、必要なら `DrawPlan` で計画どおりの描画を行います（派生は不要。 例はサンプルの `sample/Presentation/GridLoupePlan`）

---

## 3. システム要件

- .NET 10
- **対応 OS**: 指定なし（`net10.0`。 UI フレームワークの対応 OS に従う）
- Avalonia 12.1 以降
- C# 14
- **Native AOT**: 対応（`dotnet publish -p:PublishAot=true -r win-x64` で発行したサンプルが起動することを実測）

---

## 4. インストール

NuGet パッケージ（`EsUtil.Ui.AvaloniaUI.Loupe`）またはプロジェクト参照で利用します。

```xml
<ProjectReference Include="..\EsUtil.Ui.AvaloniaUI\Loupe\src\EsUtil.Ui.AvaloniaUI.Loupe.csproj" />
```

必要な名前空間:

```csharp
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EsUtil.Ui.AvaloniaUI.Loupe;
```

---

## 5. クイックスタート

### 5.1 AXAML

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:loupe="clr-namespace:EsUtil.Ui.AvaloniaUI.Loupe;assembly=EsUtil.Ui.AvaloniaUI.Loupe">
    <Canvas>
        <Image Source="{Binding DisplaySource}" />

        <loupe:LoupeControl
            IsVisible="{Binding IsLoupeVisible}"
            IsHitTestVisible="False"
            Source="{Binding SourceBitmap}"
            DisplaySource="{Binding DisplaySource}"
            CenterPoint="{Binding CenterPoint}"
            Options="{Binding Options}"
            ClientToPixelScaling="1.0" />
    </Canvas>
</Window>
```

### 5.2 ViewModel

```csharp
using Avalonia;
using Avalonia.Media.Imaging;
using EsUtil.Platform.Core.Imaging;            // EsReadOnlyBitmap（ルーペの拡大元）
using EsUtil.Feature.Loupe;                    // LoupeOptions（コア）と LoupeAppearance（描画にだけ必要な外観）

public sealed class MainViewModel
{
    // ルーペの拡大元はコアの画像（表示や保存に使う表示境界の画像は別に持つ）
    public EsReadOnlyBitmap SourceBitmap { get; } = LoadSource();

    // 表示用の射影（省略可）。 ホストが表示に使っている画像を渡すと切り出しの複製を作らない
    public Bitmap DisplaySource { get; } = LoadBitmap();

    public Point? CenterPoint { get; set; } = new(400, 300);

    public bool IsLoupeVisible { get; set; } = true;

    // 設定（基本サイズ・拡大率）はコアの値 1 つで公開する（外観は Appearance プロパティ）
    public LoupeOptions Options { get; set; } =
        LoupeOptions.Default with { RequestedSize = 240, ZoomLevel = 4 };

    private static EsReadOnlyBitmap LoadSource()
    {
        // アプリケーション側の画像読み込み処理を実装します（コアの画像として読み込む）。
        throw new NotImplementedException();
    }

    private static Bitmap LoadBitmap()
    {
        // アプリケーション側の表示用画像の読み込み処理を実装します。
        throw new NotImplementedException();
    }
}
```

`LoupeControl` は `Panel` なので、親の `Panel` や `Canvas` のVisual Treeへ配置してください。 表示位置・表示サイズは座標変換ポート方式（`SetCoordTransform` + `PositionOffset`）で自己計算します。

表示境界の画像しか読み込めない場合は、コアの画像へ変換してから渡します（`ToEsBitmap()` で複製し、`SetReadOnly()` の後に `EsReadOnlyBitmap.FromBitmap()` で読み取り専用の面へ渡します）。

### 5.3 Code-behind

```csharp
var loupe = new LoupeControl
{
    Source = sourceBitmap,          // 拡大元（コアの読み取り専用画像）
    DisplaySource = displayImage,   // 表示用の射影（省略可。 ホストが表示に使っている画像）
    CenterPoint = new Point(400, 300),
    Options = LoupeOptions.Default with { RequestedSize = 240, ZoomLevel = 4 },
    IsVisible = true,
    IsHitTestVisible = false,
    ZIndex = 100
};

canvas.Children.Add(loupe);
Canvas.SetLeft(loupe, 300);
Canvas.SetTop(loupe, 220);
```

---

## 6. 画像の受け渡し

拡大元（`Source`）の型は**コアの読み取り専用画像（`EsReadOnlyBitmap?`）**です（MewUI 版 / WinUI3 版 / WinForms 版と同じ契約）。 表示境界の画像（`Bitmap` / `WriteableBitmap` / `EsBitmapAvaloniaAdapter` 等）は**そのままでは設定できません**。 利用側がコアの画像へ変換してから設定します（表示境界の画像しか持たない場合の変換は「5.2 ViewModel」を参照）。

**表示用の射影（`DisplaySource: IImage?`）は省略できます**。 ホストが表示に使っている画像を渡すと、切り出しの複製も表示用の変換も作らずに描けます（0 コピー）。

- **拡大元と寸法が一致するときだけ使われます**（一致しない射影は無視され、拡大元の画素から描かれます。 描画 API は元矩形を画像の座標系で解釈するためです）。
- **破棄済みの射影も無視されます**（ホストが先に破棄した状態と競合しても、拡大元から描くため表示は失われません）。
- 描画の内容は射影の有無で変わりません。

---

## 7. カスタマイズ

差し替えの入口は 3 つです（**派生は不要**。 コントロールのプロパティへ設定します）。

| 差し替える対象 | 差し替え方 |
| --- | --- |
| 色・枠線の太さ・表示可否・ラベルの体裁 | `Appearance`（`LoupeAppearance`）を設定する |
| 矩形で並べられる装飾（十字線・枠線）とラベルの文言・位置 | `DrawPlanResolver`（`Func<LoupeDrawPlan, LoupeDrawPlan>`）を設定する（計画の値の置き換え） |
| 矩形で表せない描画（円・斜線等） | `Appearance` の `Show*` を false にして `Rendered` で受け取る描画面へ自前で描く（`DrawPlan` を持つ UI 版では計画どおりの描画も呼べる） |

描画計画（`LoupeDrawPlan`）と外観（`LoupeAppearance`）は `EsUtil.Feature.Loupe.Presenter` が単一所有し、4 つの UI 版で共通です。 UI が持つのは UI フレームワーク固有の描画 API の呼び出しだけです（仕様は [`docs/SPEC_API.md`](docs/SPEC_API.md)）。

---

## 8. ログ

`Log` へ `ILogger` を設定すると、設定の変更と、描画・操作の例外を出力します（既定は出力なし）。

| レベル | 出力例 |
| --- | --- |
| Debug | `[Loupe] Setting: ZoomLevel 4 → 8`（拡大元の画像・外観の変更も同じ書式） |
| Error | `[Loupe] Update exception` / `[Loupe] <操作> で例外が発生`（アプリは継続する） |

接頭辞は `LogPrefix` で設定し、提示層・コアへ伝搬します（既定 `[Loupe]`）。 ログ基盤（出力先・レベルの制御）はホストが所有します。

```csharp
loupe.Log = logger;
```

`LoupeControl` はログをファイルへ直接出力しません。 出力先（ファイル・コンソール・デバッグ出力）は利用側の `ILogger` 構成が決めます。

---

## 9. 未対応

本コントロールが対象外とする操作・機能は次のとおりです（制限の詳細は [`docs/SPEC_API.md`](docs/SPEC_API.md) を参照）。

- **拡大元の画像**: コアの画像（`EsReadOnlyBitmap`）だけを受け取る（UI フレームワークのビットマップ型は受け付けない）。
- **ポインター入力**: 拡大中心はホストが決める（入力の購読はホストが行う）。
- **印刷・画像の書き出し**: 持たない（`Rendered` で受け取る描画面へ描いた結果をホストが保存する）。

---

## 10. サンプルアプリケーション

動作確認用の独立サンプルアプリケーションを同梱しています。

```powershell
dotnet run --project sample/EsUtil.Ui.AvaloniaUI.Loupe.Sample.csproj
```

サンプルで確認できる内容:

- マウス追従による拡大表示
- `Source`（コアの画像）と `DisplaySource`（表示用の射影）のBinding
- 画像端から外れた部分の画像外の色（無地）
- ズーム・サイズ・DPIスケール変更
- 十字線・枠線の変更
- `IsVisible`、`Clear()`、`RequestRender()`
- `Log`（描画エラーの通知先 ILogger 設定例）

詳細は [sample/README.md](sample/README.md) を参照してください。

---

## 11. ドキュメント

- [外部機能仕様書](docs/SPEC_API.md)
- [システム仕様書](docs/SPEC_System.md)
- [サンプルアプリケーション](sample/README.md)

---

## 12. プロジェクト構成

本コントロールは、UI フレームワーク非依存のロジックと設定を `EsUtil.Feature.Loupe.Core` へ、描画計画と外観を `EsUtil.Feature.Loupe.Presenter` へ分離しています（マルチ UI 対応）。 層構成（Core / Presenter / UI）の全体像は [`EsUtil/README.md`](../../EsUtil/README.md) を参照してください。

```text
EsUtil.Ui.AvaloniaUI/Loupe/
├── src/                    # UI 層（Avalonia 固有の描画と配線）
│   ├── EsUtil.Ui.AvaloniaUI.Loupe.csproj
│   ├── LoupeControl.cs         # 公開 Panel（コードのみ。AXAML を持たない。公開API一覧・StyledProperty・公開プロパティ・公開メソッド・ラベル要素の配置・非公開実装）
│   ├── LoupeContextPainter.cs      # 描画計画を DrawingContext へ写す描画実装
│   ├── LoupeRenderedEventArgs.cs   # 描画完了の通知引数（UI 固有。中心座標ラベルはコントロールが表示する）
│   └── （ラベル要素（`Border` + `TextBlock`）の配置は本コントロールが行う）
├── sample/                 # 独立サンプルアプリ（sample/README.md 参照）
├── tests/                  # UI 依存の単体テスト（Avalonia ヘッドレス・直列）
└── docs/                   # SPEC_API.md（API リファレンス）/ SPEC_System.md（システム仕様書）

EsUtil/Feature.Loupe.Core/       # UI フレームワーク非依存の設定・計算・寸法・判断・ログ定義（設定の単一値 / 寸法 / 数値補正 / 表示位置・サイズ / 切り出し領域と可否 / ラベル配置 / 区画数の切り上げ / 描画完了時の表示情報）
EsUtil/Feature.Loupe.Presenter/     # 描画計画と外観の単一所有（LoupePresenter / LoupeAppearance / LoupeDrawPlan）
```

---

## 13. テスト

`EsUtil.Ui.AvaloniaUI/Loupe/tests` が本プロジェクトのテストを持ちます（xUnit v3）。 実行手順と合否基準は [検証手順.md](../../.github/検証手順.md) が定めます。

```powershell
dotnet test EsUtil.Ui.AvaloniaUI/Loupe/tests/EsUtil.Ui.AvaloniaUI.Loupe.Tests.csproj
```

---

## 14. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
