# EsUtil.Feature.Loupe.ControlAvaloniaUI

> プロジェクト名: `EsUtil.Feature.Loupe.ControlAvaloniaUI`（Avalonia UI 版）。 UI 非依存のロジックは `EsUtil.Feature.Loupe.Core`、MewUI 版は対になるコントロール（別プロジェクト）にあります。

## 目次

- [1. 概要](#1-概要)
- [2. 機能一覧](#2-機能一覧)
- [3. システム要件](#3-システム要件)
- [4. インストール](#4-インストール)
- [5. クイックスタート](#5-クイックスタート)
  - [5.1 AXAML](#51-axaml)
  - [5.2 ViewModel](#52-viewmodel)
  - [5.3 Code-behind](#53-code-behind)
- [6. 描画動作](#6-描画動作)
  - [6.1 画像境界](#61-画像境界)
  - [6.2 非同期描画](#62-非同期描画)
- [7. サポート外の画像](#7-サポート外の画像)
- [8. 所有権とライフサイクル](#8-所有権とライフサイクル)
- [9. パフォーマンス](#9-パフォーマンス)
- [10. サンプルアプリケーション](#10-サンプルアプリケーション)
- [11. ドキュメント](#11-ドキュメント)
- [12. プロジェクト構成](#12-プロジェクト構成)
- [13. テスト](#13-テスト)
- [14. ライセンス](#14-ライセンス)

---

## 1. 概要

Avalonia UI 用の拡大鏡コントロールです。 画像の任意のピクセル座標を中心に領域を拡大し、十字線・枠線付きで表示します（画像外は無地）。

画像端から外れた領域は透明として扱われるため、画像がルーペの視野から離れるほど、その部分だけ画像外の色（無地）で埋まります。

## 2. 機能一覧

- Avalonia Binding 対応の `LoupeControl`
- `IImage` を受け取る画像入力API
- 現在対応する画像実装は `Bitmap` / `EsBitmapAvaloniaImage`（`EsBitmap` を内包する `IImage` アダプタ）
- 1〜64倍の拡大表示
- 画像範囲外の透明パディング
- 画像外（ペーストボード）の色（`Appearance`）設定
- 十字線の色設定（不透明度は色のアルファに統合）
- 中心座標ラベル表示（`(x, y)` 形式・表示/色/背景/フォントサイズ/角丸設定可能）
- 枠線の色（`Appearance`）・太さ（`Options`）設定
- **色と、ラベルのフォントサイズ・角丸は `Appearance` プロパティ（`LoupeAppearance`）、寸法（タイルサイズ・枠線の太さ）は `Options`（`LoupeOptions`）で設定**（コアは寸法・表示可否だけを持ち、描画にだけ必要な設定は描画層（`EsUtil.Feature.Loupe.Presenter`）が単一所有する）
- DIP と物理ピクセルの分離（`ClientToPixelScaling`）
- 非同期のルーペ画像生成（切り出し・合成は UI スレッドで実行）
- 古い描画結果の自動破棄
- 非表示時の生成Bitmap解放
- `ILogger` によるエラー通知
- 描画完了通知イベント（`Rendered`・`LoupeRenderedEventArgs` で描画済み Bitmap と描画計画（切り出し左上・拡大率）を通知。 ホスト側のオーバーレイ描画用。 線分・選択矩形等の破線はホストが描画済み Bitmap へ直接描く）
- 中心座標ラベルはコントロールが **`Border` + `TextBlock` の子要素として配置**します（文言・表示可否はコア、実寸 → 配置矩形の写像は描画層（`EsUtil.Feature.Loupe.Presenter` の `LoupePresenter`（`LabelPlan` partial））、実寸測定は `FormattedText`、描画順はコアの `LoupeDrawLayer` が単一所有）
- **座標変換ポート（`SetCoordTransform(ICoordTransform?)` + `PositionOffset`）**: MarqueeControl と同方式。 表示位置（`RenderTransform`）と表示サイズ（要求サイズ）をレイアウト時に自己計算（ウィンドウサイズ変更・ズーム・スクロールに追従）
- **`RefreshPosition()`**: パン・ズーム等のレイアウト非変更なビューポート変化時に、表示位置・サイズを再計算（座標変換ポートは呼び出し毎に最新の表示状態を評価）

## 3. システム要件

- .NET 10
- Avalonia 12.1 以降
- C# 14

## 4. インストール

現在はNuGetパッケージではなく、プロジェクト参照で利用します。

```xml
<ProjectReference Include="..\EsUtil.Feature.Loupe.ControlAvaloniaUI\src\EsUtil.Feature.Loupe.ControlAvaloniaUI.csproj" />
```

必要な名前空間:

```csharp
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EsUtil.Feature.Loupe.AvaloniaUI;
```

## 5. クイックスタート

### 5.1 AXAML

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:loupe="clr-namespace:EsUtil.Feature.Loupe.AvaloniaUI;assembly=EsUtil.Feature.Loupe.ControlAvaloniaUI">
    <Canvas>
        <Image Source="{Binding SourceImage}" />

        <loupe:LoupeControl
            IsVisible="{Binding IsLoupeVisible}"
            IsHitTestVisible="False"
            Source="{Binding SourceImage}"
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
using EsUtil.Feature.Loupe;                    // LoupeOptions（コア）と LoupeAppearance（描画にだけ必要な外観）

public sealed class MainViewModel
{
    public Bitmap SourceImage { get; } = LoadBitmap();

    public Point? CenterPoint { get; set; } = new(400, 300);

    public bool IsLoupeVisible { get; set; } = true;

    // 設定（基本サイズ・拡大率）はコアの値 1 つで公開する（外観は Appearance プロパティ）
    public LoupeOptions Options { get; set; } =
        LoupeOptions.Default with { RequestedSize = 240, ZoomLevel = 4 };

    private static Bitmap LoadBitmap()
    {
        // アプリケーション側の画像読み込み処理を実装します。
        throw new NotImplementedException();
    }
}
```

`LoupeControl` は `Panel` なので、親の `Panel` や `Canvas` のVisual Treeへ配置してください。 表示位置・表示サイズは座標変換ポート方式（`SetCoordTransform` + `PositionOffset`）で自己計算します。

### 5.3 Code-behind

```csharp
var loupe = new LoupeControl
{
    Source = sourceBitmap,
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

## 6. 描画動作

### 6.1 画像境界

`CenterPoint` は画像範囲外の座標も指定できます。

- 画像と重なる領域: 拡大画像を表示
- 画像範囲外の領域: 透明パディングと画像外の色（無地）を表示
- 視野全体が画像範囲外: 全面が画像外の色

### 6.2 非同期描画

ルーペ画像の生成は非同期で実行され、古い描画結果は自動的に破棄されます。 複数のプロパティが連続して変更された場合、再描画は 1 フレームに 1 回に結合されます。

## 7. サポート外の画像

公開プロパティの型はAvalonia標準の `IImage?` ですが、ピクセル抽出を必要とするため、対応する実体は `Bitmap` と `EsBitmapAvaloniaImage`（`EsBitmap` を内包する `IImage` アダプタ）です。

`Bitmap` / `EsBitmapAvaloniaImage` 以外の `IImage` を設定した場合は、`NotSupportedException` を発生させ、設定された `ILogger` へ記録します。

```csharp
loupe.Log = logger;
```

`LoupeControl` はログをファイルへ直接出力しません。 ファイル、コンソール、デバッグ出力などの出力先は利用側の `ILogger` 構成に依存します。

## 8. 所有権とライフサイクル

- `Source` に設定した元画像（`Bitmap` / `EsBitmapAvaloniaImage`）は利用側が所有します。 `LoupeControl` は元画像を破棄しません。
- ルーペ表示用に内部生成したBitmapは `LoupeControl` が所有します。
- 非表示化、`Clear()`、新しい描画結果の採用時に、古い生成Bitmapを破棄します。
- 非同期処理で古くなった生成結果も採用せず破棄します。

## 9. パフォーマンス

- `Source` や `CenterPoint` の高頻度更新は、利用側で更新間隔を制限することを推奨します。
- 大きな `Options.RequestedSize` や高い `ClientToPixelScaling` は生成Bitmapのサイズと処理負荷を増加させます。

## 10. サンプルアプリケーション

動作確認用の独立サンプルアプリケーションを同梱しています。

```powershell
dotnet run --project sample/EsUtil.Feature.Loupe.ControlAvaloniaUI.Sample.csproj
```

サンプルで確認できる内容:

- マウス追従による拡大表示
- 画像端から外れた部分の画像外の色（無地）
- ズーム・サイズ・DPIスケール変更
- 十字線・枠線の変更
- `IsVisible`、`Clear()`、`RequestRender()`
- `Log`（描画エラーの通知先 ILogger 設定例）

詳細は [sample/README.md](sample/README.md) を参照してください。

## 11. ドキュメント

- [外部機能仕様書](docs/SPEC_API.md)
- [システム仕様書](docs/SPEC_System.md)
- [サンプルアプリケーション](sample/README.md)

## 12. プロジェクト構成

本コントロールは、UI フレームワーク非依存のロジックと設定を `EsUtil.Feature.Loupe.Core` へ、描画計画と外観を `EsUtil.Feature.Loupe.Presenter` へ分離しています（マルチ UI 対応）。 層構成（Core / Presenter / UI）の全体像は [`src/README.md`](../README.md) を参照してください。

```text
EsUtil.Feature.Loupe.ControlAvaloniaUI/
├── src/AvaloniaUI/         # UI 層（Avalonia 固有の描画と配線）
│   ├── EsUtil.Feature.Loupe.ControlAvaloniaUI.csproj
│   ├── LoupeControl.cs         # 公開 Panel（コードのみ。AXAML を持たない。公開API一覧・StyledProperty・公開プロパティ・公開メソッド・ラベル要素の配置・非公開実装）
│   ├── LoupeRenderPipeline.cs      # ソース解決と描画の起動（IImage 型判定と Bitmap の複製は UI 固有。描画先は DrawingContext）
│   ├── LoupeSourceResolver.cs      # IImage の対応型判定・破棄済み検出
│   ├── LoupeRenderedEventArgs.cs   # 描画完了の通知引数（UI 固有。中心座標ラベルはコントロールが表示する）
│   └── （ラベル要素（`Border` + `TextBlock`）の配置は本コントロールが行う）
├── sample/                 # 独立サンプルアプリ（sample/README.md 参照）
├── tests/                  # UI 依存の単体テスト（Avalonia ヘッドレス・直列）
└── docs/                   # SPEC_API.md（API リファレンス）/ SPEC_System.md（システム仕様書）

src/EsUtil.Feature.Loupe.Core/       # UI フレームワーク非依存の設定・計算・寸法・判断・ログ定義（設定の単一値 / 寸法 / 数値補正 / 表示位置・サイズ / 切り出し領域と可否 / ラベル配置 / 区画数の切り上げ / 描画完了時の表示情報）
src/EsUtil.Feature.Loupe.Presenter/     # 描画計画と外観の単一所有（LoupePresenter / LoupeAppearance / LoupeDrawPlan）
```

## 13. テスト

`src/EsUtil.Feature.Loupe.ControlAvaloniaUI/tests` が本プロジェクトのテストを持ちます（xUnit v3）。 実行手順と合否基準は [検証手順.md](../../.github/検証手順.md) が定めます。

`powershell
dotnet test src/EsUtil.Feature.Loupe.ControlAvaloniaUI/tests/AvaloniaUI/EsUtil.Feature.Loupe.ControlAvaloniaUI.Tests.csproj
`

## 14. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
