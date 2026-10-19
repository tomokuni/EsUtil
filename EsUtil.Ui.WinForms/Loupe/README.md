# EsUtil.Ui.WinForms.Loupe

他の UI 版と同一の **UI 非依存コア（`EsUtil.Feature.Loupe.Core`）と提示層（`EsUtil.Feature.Loupe.Presenter`）** を使い、描画を **System.Drawing（GDI+）** で行うルーペ（拡大鏡）コントロールです。

## 1. 目的

ルーペのロジック（数値の安全域補正・切り出し領域の計算・表示レイアウト）は `EsUtil.Feature.Loupe.Core` が、描画計画（層の順序・装飾の矩形・ラベルの文言と配置）と外観（色・枠線の太さ・表示可否）は提示層（`EsUtil.Feature.Loupe.Presenter` の `LoupePresenter` / `LoupeAppearance`）が所有します。
本プロジェクトはその計画を **GDI+ の描画面へ写すだけ** の層です。 そのため他の UI 版と**同じ設定・同じ見た目**になり、同じ外観の設定を各 UI で共用できます。 層構成（Core / Presenter / UI）の全体像は [`EsUtil/README.md`](../../EsUtil/README.md) を参照してください。

## 2. 動作環境

| 項目 | 内容 |
| --- | --- |
| 対象フレームワーク | `net10.0-windows`（`UseWindowsForms`。 Windows 専用） |
| 描画 API | GDI+（`System.Drawing`。 **SkiaSharp を使わない**） |
| 評価版の制限 | なし（パッケージは `EsUtil.Feature.Loupe.Presenter` / `EsUtil.Platform.Core` と `Microsoft.Extensions.Logging.Abstractions` のみを参照する） |

**SkiaSharp を使わない理由**: `SkiaSharp.Views.WindowsForms`（4.153.1）は `net10.0-windows10.0.19041` 向けの資産を持ち、`net10.0-windows`（= `net10.0-windows7.0`）では .NET Framework 向けの資産へフォールバックして復元に失敗します（実測: `NU1701`）。 対象フレームワークの方針（WinForms は Windows の版を含めない）と併せ、本プロジェクトは WinForms 自身の描画 API（GDI+）で描きます。

## 3. インストール（プロジェクト参照）

```xml
<ProjectReference Include="..\..\EsUtil.Ui.WinForms.Loupe\src\WinForms\EsUtil.Ui.WinForms.Loupe.csproj" />
```

本プロジェクトは `EsUtil.Platform.Core.Windows`（Windows 向けの画像変換）を参照しません（依存関係の規約により UI 層からは参照できない）。 必要な画像変換だけを `Internal` へ複製して所有します。

## 4. 使い方

```csharp
using EsUtil.Feature.Loupe;                    // LoupeOptions（コア）と LoupeAppearance（描画にだけ必要な外観）
using EsUtil.Ui.WinForms.Loupe;
using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

var loupe = new LoupeControl
{
    Source = readOnlyImage,                  // 拡大元の 100% 画像（コアの EsReadOnlyBitmap）
    CenterPoint = new EsPoint(320, 240),     // 拡大中心（元画像ピクセル座標）
    PositionOffset = 24,                     // 中心からの右下オフセット（DIP）
    // 寸法（表示サイズ・拡大率）はコアの Options（with 式で部分変更する）
    Options = LoupeOptions.Default with
    {
        RequestedSize = 240,                 // 一辺の表示サイズ（DIP）
        ZoomLevel = 4,                       // 拡大率
    },
    // 外観（色・枠線の太さ・表示可否）は提示層の Appearance で指定する
    Appearance = LoupeAppearance.Default with
    {
        BorderThickness = 2,
        CrossColor = DrawingColor.FromArgb(102, 255, 0, 0),
    },
};

parent.Controls.Add(loupe);
```

`Source` と `CenterPoint` を更新すると自動的に再描画されます。 **表示位置と表示サイズはホストが決めます**（WinForms のレイアウトは親が所有するため、コントロール自身は動きません）。

```csharp
// 位置決めはコアのファサードが計算する（中心 + オフセット、親領域内へ補正）
// 親領域のサイズは物理ピクセルであるため、ToDipSize で DIP へ換算して渡す
var layout = loupe.ResolveLayout(loupe.ToDipSize(parent.ClientSize));
if (layout.Position is { } position)
{
    var size = loupe.ToClientPixel(layout.ClientSize);
    loupe.Bounds = new Rectangle(
        loupe.ToClientPixel(position.X), loupe.ToClientPixel(position.Y), size, size);
}
else
{
    // 拡大中心が未設定、または親領域が表示サイズを収められない場合は配置しない（描画もされない）
    loupe.Bounds = Rectangle.Empty;
}
```

描画の直後に呼ばれる `Rendered` では、同じ描画面へオーバーレイ（選択矩形の破線等）を追加描画できます。

```csharp
loupe.Rendered += (_, e) =>
{
    var content = e.Content;                       // 実際に使われた描画計画
    var plan = content;                            // in 引数へ渡すためローカルへ写す
    MyOverlayPainter.Paint(e.Graphics, in plan);   // 描画面は物理ピクセル（1 ピクセル = ルーペ画像の 1 ピクセル）
};
```

## 5. プロパティ

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Source` | `EsReadOnlyBitmap?` | `null` | 拡大元の 100% 画像（借用であり破棄しない）。 null の場合は画像外の色のみ |
| `CenterPoint` | `EsPoint?` | `null` | 拡大中心（元画像ピクセル座標） |
| `Options` | `LoupeOptions` | `LoupeOptions.Default` | 設定（表示サイズ・拡大率）。 `with` 式で部分変更する。 数値項目はコアの型が生成時に安全域へ補正する |
| `Appearance` | `LoupeAppearance` | `LoupeAppearance.Default` | 外観（色・枠線の太さ・表示可否・ラベルの体裁）。 コアの計算には入らない |
| `ClientToPixelScaling` | `double` | 1.0 | DIP → 物理ピクセルの係数。 0.1〜8.0 に補正（非有限は既定値）。 ハンドルの生成時と DPI の変更時に実測値へ更新される |
| `PositionOffset` | `double` | 0 | ルーペ中心からの右下オフセット（DIP）。 位置の補正はコアが行う |
| `DrawPlanResolver` | `Func<LoupeDrawPlan, LoupeDrawPlan>?` | `null` | 描画計画の差し替え（矩形で表せる装飾とラベルの文言・位置）。 純粋関数にすること |
| `Log` | `ILogger` | `NullLogger` | 設定変更ログと描画例外の出力先（null は `NullLogger`） |
| `LogPrefix` | `string?` | `[Loupe]` | ログの接頭辞（提示層とコアへ伝搬する） |

メンバーの詳細は [`docs/SPEC_API.md`](docs/SPEC_API.md) を参照してください。

## 6. イベント

| イベント | 引数 | 説明 |
| --- | --- | --- |
| `Rendered` | `LoupeRenderedEventArgs` | 描画完了の通知（描画面と描画計画）。 ホストはハンドラー内で描画面へオーバーレイを追加描画できる |

オーバーレイの描画位置は `(対象座標 − e.Content.CaptureTopLeft) × e.Content.ZoomLevel` で求めます（`CaptureTopLeft` は可視域の左上（実効原点。 小数を含む）です）。

## 7. ログ

`Log` へ `ILogger` を設定すると、設定変更と描画の例外を出力します（既定は出力なし）。

| レベル | 出力例 |
| --- | --- |
| Debug | `[Loupe] Setting: Source 800x600 → null`（拡大元画像）/ `[Loupe] Setting: ZoomLevel 4 → 8`（設定・環境値・拡大中心はコアが出力する） |
| Error | `[Loupe] Update exception`（描画の例外。 アプリは継続する） |

## 8. 構成

| パス | 内容 |
| --- | --- |
| `EsUtil.Ui.WinForms/Loupe/README.md` | 本ファイル |
| `EsUtil.Ui.WinForms/Loupe/docs/SPEC_API.md` | API リファレンス |
| `EsUtil.Ui.WinForms/Loupe/docs/SPEC_System.md` | システム仕様書 |
| `EsUtil.Ui.WinForms/Loupe/src/EsUtil.Ui.WinForms.Loupe.csproj` | ライブラリ（`net10.0-windows` / `UseWindowsForms`） |
| `EsUtil.Ui.WinForms/Loupe/src/LoupeControl.cs` | コントロール（プロパティ・描画の入口・DPI 追従） |
| `EsUtil.Ui.WinForms/Loupe/src/LoupeGraphicsPainter.cs` | 描画計画を GDI+ へ写す描画実装 |
| `EsUtil.Ui.WinForms/Loupe/src/LoupeRenderedEventArgs.cs` | 描画完了の通知 |
| `EsUtil.Ui.WinForms/Loupe/src/LoupeLogMessage.cs` | ログ定義（本プロジェクトが所有する拡大元画像の変更・描画の例外） |
| `EsUtil.Ui.WinForms/Loupe/src/Internal/` | 複製した内部ヘルパー（DPI の換算・画像変換・幾何変換・ラベルの文字） |
| `EsUtil.Ui.WinForms/Loupe/tests/EsUtil.Ui.WinForms.Loupe.Tests.csproj` | 単体テスト |
| `EsUtil.Ui.WinForms/Loupe/sample/` | 独立サンプルアプリ（[`sample/README.md`](sample/README.md) を参照） |

## 9. サンプルアプリケーション

動作確認用の独立サンプルアプリケーションを同梱しています（UI は C# のコードで組み立てます）。

```powershell
dotnet run --project EsUtil.Ui.WinForms/Loupe/sample/EsUtil.Ui.WinForms.Loupe.Sample.csproj
```

サンプルで確認できる内容:

- マウス追従による拡大表示（`Source` / `CenterPoint`）
- 画像端から外れた部分の画像外の色（無地）
- 拡大率・表示サイズ・描画スケール（DIP → 物理ピクセル）の変更
- 十字線・画像外の色・枠線・座標ラベルの変更（「なし」で非表示）
- `Source` の解除（未設定時の下地）と再設定
- `Log`（設定変更ログの出力先 `ILogger` の設定例）
- 外観と描画計画の差し替え（見本。 `Appearance` と `DrawPlanResolver`）
- 描画完了通知（`Rendered`）へのオーバーレイ（選択矩形の破線）

詳細は [`sample/README.md`](sample/README.md) を参照してください。

## 10. ビルドとテスト

```powershell
dotnet build EsUtil.Ui.WinForms/Loupe/src/EsUtil.Ui.WinForms.Loupe.csproj
dotnet test EsUtil.Ui.WinForms/Loupe/tests/EsUtil.Ui.WinForms.Loupe.Tests.csproj
```

画素の検証（描画の正しさ）は `Bitmap` とその描画面で行うため、ウィンドウもメッセージ ループも不要です。 コントロールの検証は**ハンドルを生成しない範囲**（プロパティの補正・表示レイアウトの解決・描画計画の解決・ログ）に限ります。

## 11. 関連ドキュメント

- [共有する UI 非依存ロジック](../../EsUtil/Feature.Loupe.Core/README.md)
- [共有する UI 非依存の描画層](../../EsUtil/Feature.Loupe.Presenter/README.md)
- [対になる Avalonia UI 版ルーペ](../../EsUtil.Ui.AvaloniaUI/Loupe/README.md)
- [対になる MewUI 版ルーペ](../../EsUtil.Ui.MewUI/Loupe/README.md)
- [サンプルアプリケーション](sample/README.md)

## 12. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
