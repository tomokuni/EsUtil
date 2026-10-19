# EsUtil.Ui.WinUI3.Loupe

対になる他 UI 実装と同じ **UI 非依存コア（`EsUtil.Feature.Loupe.Core`）と提示層（`EsUtil.Feature.Loupe.Presenter`）** を使い、描画（`SKCanvas`）のみを **WinUI 3（Windows App SDK）** で実装したルーペ（拡大鏡）コントロールです。

## 1. 目的

ルーペのロジック（数値の安全域補正・切り出し領域の計算・表示レイアウト）は `EsUtil.Feature.Loupe.Core` が単一所有し、描画計画（層の順序・装飾の矩形・ラベルの文言と配置）と外観（色・枠線の太さ・表示可否）は UI 非依存の提示層（`EsUtil.Feature.Loupe.Presenter` の `LoupePresenter` / `LoupeAppearance`）が単一所有します。
本プロジェクトはその計画を **Skia のキャンバスへ描画させるだけ** の層です。
そのため他の UI 版と**同じ設定・同じ見た目**になり、同じ外観の設定を複数の UI で共用できます。 層構成（Core / Presenter / UI）の全体像は [`EsUtil/README.md`](../../EsUtil/README.md) を参照してください。

## 2. 特徴

| 項目 | 内容 |
| --- | --- |
| 基底クラス | `SkiaSharp.Views.Windows.SKXamlCanvas`（Skia の描画面を持つ WinUI 3 の要素） |
| 中間バッファ | **生成しない**。 画面のキャンバスへ直接描画する |
| 描画サイズ | `Options.RequestedSize` × `ClientToPixelScaling`（物理ピクセル）。 表示サイズは描画面の大きさから導くため常に一致する（領域に合わせた縮小はしない） |
| 拡大方式 | 最近傍（画素をそのまま拡大表示する） |
| 中心座標ラベル | **描画面へ描く**（テキスト要素を重ねない）。 実寸の測定と描画に同じ Skia の関数を使うため、背景と文字の大きさが常に一致する |
| DPI 追従 | `XamlRoot` の変更通知で表示倍率を取り込み、あわせて**描画のたびに描画面（物理ピクセル）と割り当て領域（DIP）の比へ合わせる**（ホストの設定は不要） |
| 連続再描画 | **無効**。 プロパティ変更時にのみ再描画する（静止画のため） |
| 位置決め | **本コントロールは位置を持たない**。 ホストが `ResolveLayout`（コアのファサード）の結果で配置する |
| プロパティ | **CLR プロパティ**（依存関係プロパティではない。 理由は [`docs/SPEC_System.md`](docs/SPEC_System.md) の設計判断を参照） |
| 見た目のカスタマイズ | 色・太さ・表示可否は `Appearance`、矩形の並びで表せる装飾とラベルの文言・位置は `DrawPlanResolver`、矩形で表せない描画は `Appearance` の `Show*` を false にして `Rendered` のキャンバスへ自前で描く（`DrawPlan` で計画どおりの描画もできる。 派生は不要） |

## 3. インストール（プロジェクト参照）

```xml
<ProjectReference Include="..\..\EsUtil.Ui.WinUI3.Loupe\src\WinUI3\EsUtil.Ui.WinUI3.Loupe.csproj" />
```

参照する側は **WinUI 3（Windows App SDK）** のプロジェクトにします（対象フレームワークは `net10.0-windows10.0.22621.0`、`UseWinUI` を有効にする）。
パッケージは `Microsoft.WindowsAppSDK` / `Microsoft.Windows.SDK.BuildTools` / `SkiaSharp.Views.WinUI` を参照します（バージョンの範囲は本体のプロジェクト ファイルが単一所有します）。

## 4. 使い方

```csharp
using EsUtil.Feature.Loupe;                    // LoupeOptions（コア）と LoupeAppearance（描画にだけ必要な外観）
using EsUtil.Ui.WinUI3.Loupe;
using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

// XAML で <loupe:LoupeControl x:Name="Loupe" /> を置き、コードビハインドから設定する
Loupe.Source = readOnlyImage;                  // 拡大元の 100% 画像（コアの EsReadOnlyBitmap）
Loupe.CenterPoint = new EsPoint(320, 240);     // 拡大中心（元画像ピクセル座標）
// 寸法（基本サイズ・拡大率）はコアの Options（with 式で部分変更する）
Loupe.Options = LoupeOptions.Default with
{
    RequestedSize = 160,                       // 一辺のサイズ（DIP）
    ZoomLevel = 4,                             // 拡大率
};
// 外観（色・枠線の太さ・表示可否）は提示層の Appearance で指定する
Loupe.Appearance = LoupeAppearance.Default with
{
    BorderThickness = 2,                       // 枠線の太さ（DIP）
    CrossColor = DrawingColor.FromArgb(102, 255, 0, 0),
    PasteboardColor = DrawingColor.FromArgb(255, 53, 53, 53),
    BorderColor = DrawingColor.FromArgb(255, 0, 0, 0),
};
```

`Source` と `CenterPoint` を更新すると自動的に再描画されます。 表示位置はホストが決めます。

```csharp
// 位置決めはコアのファサードが計算する（中心 + オフセット、親領域内へ補正）
var layout = Loupe.ResolveLayout(new EsSize(host.ActualWidth, host.ActualHeight));
if (layout.Position is { } position)
{
    Loupe.Width = layout.ClientSize;
    Loupe.Height = layout.ClientSize;
    Canvas.SetLeft(Loupe, position.X);
    Canvas.SetTop(Loupe, position.Y);
}
```

> **注意**: `ResolveLayout` はコアのファサード（`EsUtil.Feature.Loupe.Core` の `LoupeCore`、`public` 型）の API です（汎用コアとして公開されており、`InternalsVisibleTo` は不要です）。

## 5. プロパティ

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Source` | `EsReadOnlyBitmap?` | `null` | 拡大元の 100% 画像（借用であり破棄しない）。 null の場合は画像外の色のみ |
| `CenterPoint` | `EsPoint?` | `null` | 拡大中心（元画像ピクセル座標）。 null の場合は画像外の色のみ。 基底の `UIElement.CenterPoint`（描画変換の中心点）を隠す |
| `Options` | `LoupeOptions` | `LoupeOptions.Default` | 設定（基本サイズ・拡大率）。 `with` 式で部分変更する。 数値項目はコアの型が生成時に安全域へ補正する |
| `Appearance` | `LoupeAppearance` | `LoupeAppearance.Default` | 外観（枠線の太さ・表示可否（十字線・枠線・ラベル・ペーストボード））と描画に使う色（画像外・枠線・十字線・中心座標ラベルの 6 色）・ラベルの体裁。 `with` 式で部分変更する。 外観はコアの計算に入らない |
| `ClientToPixelScaling` | `double` | 1.0 | DIP → 物理ピクセルの係数。 0.1〜8.0 に補正（非有限は既定値）。 **描画のたびに描画面（物理ピクセル）と割り当て領域（DIP）の比へ合わせる**（DPI の変更通知が届かない状況でも拡大画像がルーペ領域と一致する） |
| `PositionOffset` | `double` | 0 | 拡大中心からの右下方向へのオフセット量（DIP）。 保持と位置の補正はコアが単一所有する |
| `DrawPlanResolver` | `Func<LoupeDrawPlan, LoupeDrawPlan>?` | `null` | 描画計画の差し替え点（装飾の矩形・ラベルの文言と配置）。 同じ入力では 1 フレーム 1 回だけ解決されるため、呼び出しの時点に依存しない純粋関数にすること |
| `Log` | `ILogger` | `NullLogger` | 設定変更ログと描画例外の出力先（null は `NullLogger`） |
| `LogPrefix` | `string?` | `[Loupe]` | ログの接頭辞（提示層・コアへも伝搬する） |

`Source` が受け取る画像型はコアの画像（`EsReadOnlyBitmap`）**だけ**です（フレームワークのビットマップ型や Skia の `SKBitmap` は受け付けません）。 コアの画像のピクセルを Skia の描画面へ直接描くためです（画像の変換は不要）。

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
| `ShowPasteboard` | `bool` | `true` | 画像外（ペーストボード）の表示可否（非表示でも描画面を透明で塗る） |
| `PasteboardColor` | `System.Drawing.Color` | 中性グレー（`#FF535353`） | 画像外（ペーストボード）の色 |
| `CrossColor` | `System.Drawing.Color` | 赤 40%（`#66FF0000`） | 十字線の色 |
| `BorderColor` | `System.Drawing.Color` | LightGray（`#FFD3D3D3`） | 枠線の色 |
| `CoordLabelColor` | `System.Drawing.Color` | 白 | 座標ラベルの文字色 |
| `CoordLabelBackgroundColor` | `System.Drawing.Color` | 黒 60%（`#99000000`） | 座標ラベルの背景色 |
| `CoordLabelFontSize` | `double` | 12.0 | 座標ラベルのフォントサイズ（DIP）。 6〜48 に補正 |
| `CoordLabelCornerRadius` | `int` | 2 | 座標ラベルの背景の角丸半径（DIP）。 0〜64 に補正（0 は角丸なし） |

数値の安全域への補正と変更ログは、設定（`Options`）と外観（`Appearance`）ではそれぞれの型が生成時に補正し、環境値（`ClientToPixelScaling`）では本コントロールの setter が補正します（補正の規則はコアの `LoupeCore.Limits` が単一所有します）。

## 6. イベント

| イベント | 引数 | 説明 |
| --- | --- | --- |
| `Rendered` | `LoupeRenderedEventArgs` | 描画完了の通知（描画先キャンバスと描画計画（切り出し左上・拡大率・一辺の物理ピクセル））。 ホストはハンドラ内でキャンバスへオーバーレイを追加描画できる |

オーバーレイの描画位置は `(対象座標 − e.Content.CaptureTopLeft) × e.Content.ZoomLevel` で求めます（`CaptureTopLeft` は可視域の左上（実効原点。 小数を含む）です）。

## 7. ログ

`Log` プロパティへ `ILogger` を設定すると、設定変更と描画の例外を出力します（既定は出力なし）。

| レベル | 出力例 |
| --- | --- |
| Debug | `[Loupe] Setting: Source 800x600 → null`（拡大元画像。 本プロジェクトのログ定義） / `[Loupe] Setting: ZoomLevel 4 → 8`（コアの設定値。 `RequestedSize` / `ClientToPixelScaling` も同じ書式） / `[Loupe] Setting: ShowCrosshair True → False`（外観。 変化した項目だけを出力する） |
| Error | `[Loupe] Update exception`（描画の例外。 アプリは継続する） / `[Loupe] SetCoordTransform で例外が発生`（公開 API の例外。 記録して再送出する） |

変化判定（同一値・同一インスタンスなら出力しない）は出力する主体のログ定義が単一所有するため、ホストは同値判定を持ちません。

## 8. 構成

```text
EsUtil.Ui.WinUI3/Loupe/
├── LICENSE                    # MIT（リポジトリ直下の LICENSE と同じ）
├── README.md                  # 本ファイル
├── docs/                      # SPEC_API.md（API リファレンス）/ SPEC_System.md（システム仕様書）
├── sample/                    # 独立サンプル アプリ（sample/README.md 参照）
├── src/WinUI3/
│   ├── EsUtil.Ui.WinUI3.Loupe.csproj  # net10.0-windows10.0.22621.0 / WinUI 3
│   ├── LoupeControl.cs             # SKXamlCanvas 派生のコントロール（描画は LoupeSkiaPainter へ委譲する）
│   ├── LoupeSkiaPainter.cs         # 描画計画（LoupeDrawPlan）を SKCanvas へ写す描画実装
│   ├── LoupeRenderedEventArgs.cs   # 描画完了の通知（キャンバスと描画計画）
│   ├── LoupeLogMessage.cs          # 画像の差し替え・描画例外のログ定義
│   └── Internal/                   # Skia の描画プリミティブ（テキスト・塗り・変換・スケーリング）
└── tests/WinUI3/
    ├── EsUtil.Ui.WinUI3.Loupe.Tests.csproj
    ├── TestHelper.cs                   # テスト共通（テスト画像の生成）
    ├── RecordingLogger.cs              # ログ収集（テスト用）
    ├── LogPrefixGroup.cs               # ログの接頭辞を設定するテストの直列化
    ├── LoupeSkiaPainter.Tests.cs       # 描画（画素の検証。 ラベルを含む）
    ├── LoupeLogMessage.Tests.cs        # ログの出力書式
    └── LoupeRenderedEventArgs.Tests.cs # 描画完了通知の引数
```

## 9. サンプルアプリケーション

動作確認用の独立サンプル アプリケーションを同梱しています（WinUI 3 のアンパッケージ アプリ）。

```powershell
dotnet run --project EsUtil.Ui.WinUI3/Loupe/sample/EsUtil.Ui.WinUI3.Loupe.Sample.csproj
```

サンプルで確認できる内容:

- マウス追従による拡大表示（`Source` / `CenterPoint`）
- 画像端から外れた部分の画像外の色（無地）
- 拡大率・サイズ・描画スケールの変更
- 十字線・枠線・座標ラベルの変更（「なし」で非表示）
- `Source` の解除（未設定時の下地）と再設定
- `Log`（設定変更ログの出力先 `ILogger` の設定例）
- 外観と描画計画の差し替え（見本。 `Appearance` と `DrawPlanResolver`）
- 選択矩形の破線オーバーレイ（`Rendered` へ追加描画する例）

詳細は [sample/README.md](sample/README.md) を参照してください。

## 10. ビルドとテスト

```powershell
dotnet build EsUtil.Ui.WinUI3/Loupe/src/EsUtil.Ui.WinUI3.Loupe.csproj
dotnet test EsUtil.Ui.WinUI3/Loupe/tests/EsUtil.Ui.WinUI3.Loupe.Tests.csproj
```

本プロジェクトは `GenerateDocumentationFile` を有効にし、**存在しないメンバーへの `<see cref>` をビルド警告（CS1574）で検出**します。

## 11. 関連ドキュメント

- [共有する UI 非依存ロジック](../../EsUtil/Feature.Loupe.Core/README.md)
- [共有する UI 非依存の描画層](../../EsUtil/Feature.Loupe.Presenter/README.md)
- [対になる MewUI 版ルーペ](../../EsUtil.Ui.MewUI/Loupe/README.md)
- [対になる Avalonia 版ルーペ](../../EsUtil.Ui.AvaloniaUI/Loupe/README.md)
- [API リファレンス](docs/SPEC_API.md)
- [システム仕様書](docs/SPEC_System.md)
- [サンプルアプリケーション](sample/README.md)

## 12. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
