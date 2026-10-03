# EsUtil.Feature.Loupe.Presenter

ルーペ（拡大鍵）の見た目を定める **コントロール化を前提としない UI 非依存の層**です。 UI 実装が変わっても同じ処理を共有します。

## 1. 概要

本層は 2 つの関心事を持ちます。 どちらも「単体で使う `LoupeCore` だけでは決まらない」ため、コア（`EsUtil.Feature.Loupe.Core`）には置きません。

| 関心事 | 内容 | 代表型 |
| --- | --- | --- |
| **提示（Presenter）** | 描画の幾何（`LoupeGeometry`）と外観から**描画計画**（層の順序・装飾矩形・文言・ラベル配置）を解決する | `LoupePresenter` / `LoupeDrawPlan` / `LoupeDrawLayer` |
| **外観** | 色・フォント・角丸・枠線の太さ・ラベルのオフセット・表示可否（描画にだけ必要な設定の単一値） | `LoupeAppearance` |

**描画は行いません**。 UI フレームワークごとの描画モデルの差は描画 API の抽象では吸収しきれないため、本層は**描画計画というデータ**を返し、描画は各 UI の描画実装が自 UI の API で行います。

**本層はコントロール化を前提とした処理を持ちません**（拡大元画像・再描画のスケジューリング・組み込み先の概念は UI 層または各 UI が持ちます）。

## 2. 使い方

ルーペ（拡大鏡）を独自コントロールとして実装する場合、**状態の更新**と**描画**から作れます。

```csharp
// 1. 提示層（既定実装）を生成する。外観は LoupeAppearance（既定は LoupeAppearance.Default）、設定は LoupeOptions
// ロガーはコンストラクターでも後から（Log プロパティ）でも注入できる
private readonly LoupePresenter _presenter = new(logger);
private EsBitmap? _source;   // 拡大元の画像（本 UI が解決して渡す）

// 2. 状態の更新（マウス移動などで拡大中心を設定する）
public void SetCenter(EsPoint sourcePoint)
{
    _presenter.CenterPoint = sourcePoint;
    InvalidateVisual();
}

// 3. 描画（描画計画の解決は提示層、描画は自 UI の API）
protected override void Render(DrawingContext context)
{
    var plan = _presenter.ResolveDrawPlan(displaySize, MeasureText);  // 幾何・層・文言・配置を解決済み
    // 描画は自 UI の描画 API で行う（自 UI の型へ写して層の順に描く）
    PaintByOwnApi(context, _source, in plan);
    // ラベルを要素として置く場合は plan.Labels（配置指示）を要素へ写す
}
```

- 表示位置（左上）が必要なら `LoupePresenter.ResolveLayout(parentSize)` を使う（オフセット加算と領域内への位置の補正はコアが単一所有する）。
- **ロガーはコンストラクター（`new LoupePresenter(logger)`）と `Log` プロパティのどちらでも注入できます**。 ロガーが確定した時点（コンストラクター注入時、または `Log` の初回設定時）で**生成の記録を 1 度だけ**出力します。
- 拡大元画像は UI ごとに型が異なるため `LoupeDrawPlan` に含めません。 UI が `EsBitmap` を解決して描画へ渡します。
- 実寸測定（`MeasureText`）は描画 API 依存のため UI 層が与えます。

## 3. 含まれるもの

| グループ | 型 |
| --- | --- |
| 提示 | `LoupePresenter`（描画計画の解決。 `sealed` ではない） / `Drawing.LoupeDrawPlan` / `Drawing.LoupeDrawLayer` / `Drawing.LoupeTextPlacement` |
| 外観 | `Setting.LoupeAppearance`（色・フォント・角丸・枠線の太さ・ラベルのオフセット・表示可否） |
| ラベル | `Label.LoupeLabelPlacement`（配置指示） |
| ログ | `Logging.LoupePresenterLogMessage`（internal） |

## 4. 依存

- 依存は `EsUtil.Feature.Loupe.Core`（描画の幾何・寸法・切り出しの計算）と `EsUtil.Platform.Core`（`Es*` 値型）のみです。
- **UI フレームワークへ依存しません**（描画 API の抽象も持ちません）。
- 依存方向は UI 層 → 本層 → `EsUtil.Feature.Loupe.Core` → `EsUtil.Platform.Core` の一方向です（コアは本層を参照しません）。

層の全体像は [`src/README.md`](../README.md) を参照してください。

## 5. カスタマイズ

| 差し替えたいもの | 方法 |
| --- | --- |
| 見た目（色・フォント・角丸・枠線の太さ・表示可否） | `LoupeAppearance` を変える（UI の `Appearance` プロパティへ設定する） |
| 描画計画の構成（層・配置の独自化） | `LoupePresenter` を継承し `ResolveDrawPlan` を上書きする、または `LoupePresenterXxx` を作る |
| 描画そのもの | UI 層の描画実装を差し替える |
| 表示サイズ・拡大率 | `LoupeOptions`（`RequestedSize` / `ZoomLevel`）を変える（コアが単一所有する） |

## 6. ビルド

```powershell
dotnet build src/EsUtil.Feature.Loupe.Presenter/src/EsUtil.Feature.Loupe.Presenter.csproj
```

## 7. テスト

```powershell
dotnet test src/EsUtil.Feature.Loupe.Presenter/tests/EsUtil.Feature.Loupe.Presenter.Tests.csproj
```

`tests/` が単体テストを持ちます。 描画計画の値（矩形・層・配置）は UI 型を持たないため **UI 初期化なしで検証**でき、画素の検証は UI 側（ソフトウェア描画の UI）のテストが担います。

## 8. ドキュメント

- [`docs/SPEC_API.md`](docs/SPEC_API.md) — 公開 API のリファレンスと使用例
- [`docs/SPEC_System.md`](docs/SPEC_System.md) — アーキテクチャ・設計判断・ログ

## 9. ライセンス

MIT License（全文は [`LICENSE`](LICENSE) を参照）。
