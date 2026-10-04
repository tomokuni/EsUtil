# EsUtil.Feature.Loupe.ControlMewUI

`EsUtil.Feature.Loupe.ControlAvaloniaUI`（Avalonia UI 実装）と同じ **UI 非依存コア（`EsUtil.Feature.Loupe.Core`）と提示層（`EsUtil.Feature.Loupe.Presenter`）** を使い、描画（`SKCanvas`）と入力の受付のみを **MewUI** で実装したルーペ（拡大鏡）コントロールです。

## 1. 目的

ルーペのロジック（数値の安全域補正・切り出し領域の計算・表示レイアウト）は `EsUtil.Feature.Loupe.Core` が単一所有し、描画計画（層の順序・装飾の矩形・ラベルの文言と配置）と外観（色・枠線の太さ・表示可否）は UI 非依存の提示層（`EsUtil.Feature.Loupe.Presenter` の `LoupePresenter` / `LoupeAppearance`）が単一所有します。
本プロジェクトはその計画を `SKCanvas` へ **Skia のキャンバスへ描画させるだけ** の層です。
そのため Avalonia 版と**同じ設定・同じ見た目**になり、設定ファイル（`%LOCALAPPDATA%\SaveEvidence\settings.json`）も両 UI で共用できます。 層構成（Core / Presenter / UI）の全体像は [`src/README.md`](../README.md) を参照してください。

## 2. 特徴

| 項目 | 内容 |
| --- | --- |
| 基底クラス | `Aprillz.MewUI.Controls.Panel`（子の `SkiaCanvasView` が Skia で直接描画する） |
| 中間バッファ | **生成しない**。 画面のキャンバスへ直接描画する（Avalonia 版は `RenderTargetBitmap` を経由する） |
| 描画サイズ | `Options.RequestedSize` × `ClientToPixelScaling`（物理ピクセル）。 表示サイズはキャンバスの大きさから導くため常に一致する（領域に合わせた縮小はしない） |
| 拡大方式 | 最近傍（画素をそのまま拡大表示する） |
| DPI 追従 | `OnDpiChanged` で表示スケーリング係数を DPI から換算した値へ自動更新する（換算規則は `DpiScaler.ToScaling` が単一所有。 ホストの設定は不要） |
| 連続再描画 | **無効**。 プロパティ変更時にのみ再描画する（静止画のため） |
| 位置決め | **本コントロールは位置を持たない**。 ホストが `ResolveLayout`（コアのファサード）の結果で配置する |

## 3. インストール（プロジェクト参照）

```xml
<ProjectReference Include="..\..\EsUtil.Feature.Loupe.ControlMewUI\src\EsUtil.Feature.Loupe.ControlMewUI.csproj" />
```

パッケージは `Aprillz.MewUI.Skia` を参照します（バージョンは `MewUIVersion` プロパティで 0.21.1 に固定）。

## 4. 使い方

```csharp
using EsUtil.Feature.Loupe;                    // LoupeOptions（コア）と LoupeAppearance（描画にだけ必要な外観）
using EsUtil.Feature.Loupe.MewUI;
using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

var loupe = new LoupeControl
{
    Source = esBitmap,                       // 拡大元の 100% 画像（コアの EsBitmap）
    CenterPoint = new EsPoint(320, 240),     // 拡大中心（元画像ピクセル座標）
    // 寸法（基本サイズ・拡大率）はコアの Options（with 式で部分変更する）
    Options = LoupeOptions.Default with
    {
        RequestedSize = 160,                 // 一辺のサイズ（DIP）
        ZoomLevel = 4,                       // 拡大率
    },
    // 外観（色・枠線の太さ・表示可否）は提示層の Appearance で指定する
    Appearance = LoupeAppearance.Default with
    {
        BorderThickness = 2,                 // 枠線の太さ（DIP）
        CrossColor = DrawingColor.FromArgb(102, 255, 0, 0),
        PasteboardColor = DrawingColor.FromArgb(255, 53, 53, 53),
        BorderColor = DrawingColor.FromArgb(255, 0, 0, 0),
    },
};
```

`Source` と `CenterPoint` を更新すると自動的に再描画されます（`InvalidateVisual`）。 表示位置はホストが決めます。

```csharp
// 位置決めはコアのファサードが計算する（中心 + オフセット、親領域内へ補正）
var layout = loupe.ResolveLayout(parentSize);
if (layout.Position is { } position)
{
    loupe.Arrange(new Rect(x + position.X, y + position.Y, layout.ClientSize, layout.ClientSize));
}
```

> **注意**: `ResolveLayout` はコアのファサード（`EsUtil.Feature.Loupe.Core` の `LoupeCore`、`public` 型）の API です（汎用コアとして公開されており、`InternalsVisibleTo` は不要です）。

## 5. プロパティ

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Source` | `EsBitmap?` | `null` | 拡大元の 100% 画像。 null の場合は画像外の色のみ |
| `CenterPoint` | `EsPoint?` | `null` | 拡大中心（元画像ピクセル座標）。 null の場合は画像外の色のみ |
| `Options` | `LoupeOptions` | `LoupeOptions.Default` | 設定（基本サイズ・拡大率）。 `with` 式で部分変更する。 数値項目はコアの型が生成時に安全域へ補正する |
| `Appearance` | `LoupeAppearance` | `LoupeAppearance.Default` | 外観（枠線の太さ・表示可否（十字線・枠線・ラベル））と描画に使う色（ペーストボード（画像外）・枠線・十字線・中心座標ラベルの 6 色）・ラベルの体裁。 `with` 式で部分変更する。 外観はコアの計算に入らない |
| `ClientToPixelScaling` | `double` | 1.0 | DIP → 物理ピクセルの係数。 0.1〜8.0 に補正（非有限は既定値）。 **描画のたびに描画面（物理ピクセル）と割り当て領域（DIP）の比へ合わせる**（DPI の変更通知は追加直後のコントロールへ届かないため、保持値だけに頼るとルーペ領域より小さい画像を描く） |
| `Log` | `ILogger` | `NullLogger` | 設定変更ログと描画例外の出力先（null は `NullLogger`。 Avalonia 版と同一の書式） |

`Options`（コアの `LoupeOptions`）の項目:

| 項目 | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `RequestedSize` | `int` | 160 | 一辺のサイズ（DIP）。 1〜4096 に補正 |
| `ZoomLevel` | `int` | 4 | 拡大率。 1〜64 に補正 |

`Appearance`（`LoupeAppearance`。 `EsUtil.Feature.Loupe`）の項目:

| 項目 | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `BorderThickness` | `int` | 2 | 枠線の太さ（DIP）。 1〜64 に補正 |
| `ShowCrosshair` / `ShowBorder` | `bool` | `true` | 十字線・枠線の表示可否（透明色を「描かない」の代用にしない） |
| `ShowCoordLabel` | `bool` | `true` | 左上の座標ラベルの表示可否（中心未設定時は描画しない） |
| `CrossColor` | `System.Drawing.Color` | 赤 40%（`#66FF0000`） | 十字線の色 |
| `BorderColor` | `System.Drawing.Color` | LightGray（`#FFD3D3D3`） | 枠線の色 |
| `CoordLabelColor` | `System.Drawing.Color` | 白 | 座標ラベルの文字色 |
| `CoordLabelBackgroundColor` | `System.Drawing.Color` | 黒 60%（`#99000000`） | 座標ラベルの背景色 |
| `CoordLabelFontSize` | `double` | 12.0 | 座標ラベルのフォントサイズ（DIP）。 6〜48 に補正 |
| `CoordLabelCornerRadius` | `int` | 2 | 座標ラベルの背景の角丸半径（DIP）。 0〜64 に補正（0 は角丸なし） |

`Options.RequestedSize` はレイアウトにも影響します（`MeasureContent` が一辺 `RequestedSize` の正方形を返します）。

数値の安全域への補正と変更ログはプロパティの登録（`MewProperty.Register` の `coerce` / `changed`）へ渡します。 したがってバインディングなど setter を経由しない設定でも安全域が保たれ、変更が記録されます（補正の規則はコアの `LoupeCore.Limits`、設定値の変更ログはコアの `LoupeLogMessage` が、外観の変更ログは提示層の `LoupePresenterLogMessage` が、拡大元画像の変更と描画の例外のログは**本プロジェクトの `LoupeUiLogMessage`** が単一所有します）。 設定（`Options`）と外観（`Appearance`）の数値項目はそれぞれの型が生成時に補正するため、本コントロールは補正を登録しません（環境値の `ClientToPixelScaling` のみ `coerce` を登録します）。

## 6. イベント

| イベント | 引数 | 説明 |
| --- | --- | --- |
| `Rendered` | `LoupeRenderedEventArgs` | 描画完了の通知（描画先キャンバス・一辺の物理ピクセル・切り出し領域の左上・スケール）。 ホストはハンドラ内でキャンバスへオーバーレイを追加描画できる |

オーバーレイの描画位置は `(対象座標 − CaptureTopLeft) × Scale` で求めます（破線の例は `Parts.CapturePreviewControl.MewUI`）。

## 7. ログ

`Log` プロパティへ `ILogger` を設定すると、設定変更と描画の例外を出力します（既定は出力なし）。 書式は Avalonia 版と同一です。

| レベル | 出力例 |
| --- | --- |
| Debug | `[Loupe] Setting: ZoomLevel 4 → 8`（コアの設定。 Source は `[Loupe] Setting: Source ...`、外観は `[Loupe] Setting: ...`）/ `CenterPoint` / `RequestedSize` / `ClientToPixelScaling` / `ZoomLevel`。 変化判定（同一値・同一インスタンスなら出力しない）は出力する主体のログ定義（Source は本プロジェクトの `LoupeUiLogMessage`、外観は提示層の `LoupePresenterLogMessage`、他はコアの `LoupeLogMessage`）が単一所有し、UI 層は同値判定を持たない。 設定の変更は変化した項目だけを出力する） |
| Error | `[Loupe] Update exception`（描画の例外。 アプリは継続する） |

Avalonia 版の Trace ログ（再描画パイプライン）は持ちません（MewUI 版は中間ビットマップを作らず同期描画するため）。

## 8. 構成

```text
src/EsUtil.Feature.Loupe.ControlMewUI/
├── README.md                # 本ファイル
├── docs/                    # SPEC_API.md（API リファレンス）/ SPEC_System.md（システム仕様書）`r`n├── src/
│   ├── EsUtil.Feature.Loupe.ControlMewUI.csproj   # net10.0 / MewUI 0.21.1 固定
│   ├── LoupeControl.cs             # Panel 派生のコントロール（描画は LoupeSkiaPainter へ委譲する）
│   ├── LoupeRenderedEventArgs.cs   # 描画完了の通知（キャンバスと表示情報）
│   ├── LoupeSkiaPainter.cs         # 描画計画（LoupeDrawPlan）を SKCanvas へ写す描画実装
└── tests/
    ├── EsUtil.Feature.Loupe.ControlMewUI.Tests.csproj
    ├── TestHelper.cs                   # テスト共通（描画先・画像生成）
    ├── RecordingLogger.cs              # ログ収集（テスト用）
    ├── LoupeControl.Tests.cs           # プロパティ補正・レイアウト
    └── LoupeRenderedEventArgs.Tests.cs # 描画完了通知の引数
```

## 9. ビルドとテスト

```powershell
dotnet build src/EsUtil.Feature.Loupe.ControlMewUI/src/MewUI/EsUtil.Feature.Loupe.ControlMewUI.csproj
dotnet test src/EsUtil.Feature.Loupe.ControlMewUI/tests/MewUI/EsUtil.Feature.Loupe.ControlMewUI.Tests.csproj
```

本プロジェクトは `GenerateDocumentationFile` を有効にし、**存在しないメンバーへの `<see cref>` をビルド警告（CS1574）で検出**します（XML は検証にのみ使い、配布物へは出しません）。

## 10. 関連ドキュメント

- `../EsUtil.Feature.Loupe.Core/README.md` — 共有する UI 非依存ロジック
- `../EsUtil.Feature.Loupe.Presenter/README.md` — 共有する UI 非依存の描画層
- `../EsUtil.Feature.Loupe.ControlAvaloniaUI/README.md` — Avalonia 版ルーペ
- `../App.MewUI/README.md` — 組み込み先のアプリ
