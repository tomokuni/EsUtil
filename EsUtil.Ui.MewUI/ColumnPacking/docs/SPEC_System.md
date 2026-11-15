# EsUtil.Ui.MewUI.ColumnPacking システム仕様書

MewUI のパネルとして、複数列の配置を要素へ反映する層の内部仕様です。

## 目次 <!-- omit from toc -->

- [1. システム概要](#1-システム概要)
- [2. 技術スタック](#2-技術スタック)
- [3. アーキテクチャ](#3-アーキテクチャ)
  - [3.1 依存方向](#31-依存方向)
  - [3.2 レイアウトの流れ](#32-レイアウトの流れ)
- [4. コンポーネント構成](#4-コンポーネント構成)
- [5. 依存関係](#5-依存関係)
- [6. 例外処理ポリシー](#6-例外処理ポリシー)
- [7. テスト方針](#7-テスト方針)
- [8. 設計判断](#8-設計判断)

## 1. システム概要

**配置の計算は UI 非依存の [`EsUtil.Feature.ColumnPacking.Core`](../../../EsUtil/Feature.ColumnPacking.Core/README.md) が単一所有**します。 本パネルは「幅の上限を渡す」「返ってきた矩形へ子を配置する」「結果をプロパティへ写す」だけを行います。

子は 2 種類あります。 `ItemsSource` + `ItemTemplate` から作る要素と、XAML（またはコード）に直接書いた要素です。 どちらを先に置くかは `ItemsSourceFirst` が決め、並べ直しは `DualSourceItemsManager` が担います。

## 2. 技術スタック

| 項目 | 内容 |
| --- | --- |
| 対象フレームワーク | `net10.0-windows10.0.17763.0` |
| UI フレームワーク | MewUI 0.22.1 |
| 配置の計算 | `EsUtil.Feature.ColumnPacking.Core`（プロジェクト参照） |
| テスト | xUnit v3（`tests/`）。 画面のランタイムを要する部分はサンプルで確認する |
| Native AOT | `IsAotCompatible=true`（解析警告 0 件をビルドで検査） |

## 3. アーキテクチャ

### 3.1 依存方向

```mermaid
flowchart TB
    PANEL["ColumnPackingPanel<br/>（Measure/Arrange と結果の公開）"]
    BASE["DualSourceItemsPanel<br/>（ItemsSource と直接の子）"]
    MGR["DualSourceItemsManager<br/>（子の並べ直し）"]
    ENGINE["EsUtil.Feature.ColumnPacking.Core<br/>（配置の計算）"]
    PANEL --> BASE
    BASE --> MGR
    PANEL --> ENGINE
```

UI 層は同一 UI の変換ブリッジと、層を持たない機能パッケージを参照できます（[`.github/依存関係.md`](../../../.github/依存関係.md) §3 の R5）。

### 3.2 レイアウトの流れ

```mermaid
sequenceDiagram
    participant X as MewUI のレイアウト
    participant P as ColumnPackingPanel
    participant E as ColumnPacker
    participant C as 子要素
    X->>P: MeasureContent(availableSize)
    P->>P: 子を計測し（幅, 高さ）を集める
    P->>E: new ColumnPacker(…)。Solve(幅の上限)
    E-->>P: 使用幅・最大列高さ・列の区切り
    P->>P: 結果を Layout* のプロパティへ写す
    P-->>X: 望ましい大きさ
    X->>P: ArrangeContent(bounds)
    P->>C: 計算した矩形へ配置する
```

- **幅が確定しないパス（無限大・0 以下）**では、直前に確定した幅で計算します（`ScrollViewer` が内容を 2 通りの幅で測る場合に、2 つのレイアウトが交互に現れて確定しないため。 実測 2026-11-01）。 幅がまだ分からない最初のパスでは 1 列として扱います。

## 4. コンポーネント構成

```text
EsUtil.Ui.MewUI/ColumnPacking/
├── docs/
├── sample/
├── src/
│   ├── ColumnPackingPanel.cs
│   ├── DualSourceItemsPanel.cs
│   └── DualSourceItemsManager.cs
└── tests/
```

| コンポーネント | 所在 | 内容 |
| --- | --- | --- |
| パネル本体 | `EsUtil.Ui.MewUI/ColumnPacking/src/ColumnPackingPanel.cs` | 配置の計算の呼び出し・結果の公開・子の配置 |
| ItemsSource の管理 | `EsUtil.Ui.MewUI/ColumnPacking/src/DualSourceItemsPanel.cs` | 依存関係プロパティと子の作り直し |
| 子の並べ直し | `EsUtil.Ui.MewUI/ColumnPacking/src/DualSourceItemsManager.cs` | 直接の子の保存と `ItemsSource` の要素の生成 |
| サンプル | `EsUtil.Ui.MewUI/ColumnPacking/sample/` | 列数・余白・項目数を変えて確認するアプリ（データ駆動の子は `SetItems` + `ItemTemplate` で渡す） |

## 5. 依存関係

- **`EsUtil.Feature.ColumnPacking.Core`**（配置の計算）。
- **`Aprillz.MewUI.Core`**（パネル・プロパティ システム・データ テンプレート）。
- `EsUtil.Platform.Core` などの他の EsUtil の層は参照しません。
- サンプルは `ProjectReference` で本体を参照します（サンプルは配布しないため）。

## 6. 例外処理ポリシー

- **例外を送出しません**（配置の計算の例外は、入力の検証をパネル側で行うことで防ぎます）。
- 幅が未確定（0 以下）の間は配置を計算しません（レイアウトの次のパスで計算します）。

## 7. テスト方針

- `DualSourceItemsManager` の状態（`ItemsSourceFirst`・保存の有無・`Reset`）と、`ItemsSource` からの要素生成（null の扱い）を単体テストで検証します。
- **パネル自身の配置は単体テストで検証しません**（画面のランタイムが必要なため）。 サンプル アプリで確認します。

## 8. 設計判断

- **配置の計算を UI 非依存の層へ置く**: 同じ計算を UI ごとに書き直すと、UI によって並びが変わります。 計算を 1 か所に置き、パネルは写すだけにしています。
- **結果を読み取り専用の依存関係プロパティで公開する**: バインディングで表示でき、派生クラスからも読めます（ログとサンプルの状態表示が使います）。
- **子の仮想化を行わない**: 配置の計算が「全件の高さ」を必要とするためです（仮想化するには高さの推定が要り、結果が変わります）。
- **名前空間とアセンブリ名を `EsUtil.Ui.MewUI.ColumnPacking` に揃える**: 型名（`ColumnPackingPanel`）をそのままパッケージ名にすると名前空間のセグメントと一致し、配下から型を参照できなくなる（`MA0049`。 `CS0118`）。 そのためパッケージ名は機能名、型名は実装の名前にしています。
- **コアのインスタンス（`Layout`）を保持する**: 子の計測結果と幅の上限が変わらない限り、同じ計算を繰り返さずに済みます（`ArrangeOverride` は結果を読むだけ）。
