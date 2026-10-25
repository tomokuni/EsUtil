# EsUtil.Feature.MultiColumn

![NuGet](https://img.shields.io/nuget/v/EsUtil.Feature.MultiColumn.svg)
![Release](https://github.com/tomokuni/EsUtil/actions/workflows/release.yml/badge.svg)
![Build](https://github.com/tomokuni/EsUtil/actions/workflows/build.yml/badge.svg?branch=main)
![Platform](https://img.shields.io/badge/Platform-Portable-6E6E6E)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![AOT](https://img.shields.io/badge/AOT-Ready-00695C)
![License](https://img.shields.io/badge/License-MIT-3DA639)

**対応 OS**: 指定なし（`net10.0`。 OS 固有の API を使わない）。 ただし **Windows 以外での動作は未検証**。

複数のアイテムを複数の列へ並べ、**最大列高さを最小化**する UI 非依存のレイアウト計算ライブラリです。
名前空間とアセンブリ名は `EsUtil.Feature.MultiColumn` です（型名 `MultiColumnLayoutEngine` はパッケージ名と一致しません）。
アイテムの高さがばらつく一覧（カード・サムネイル・タグなど）を縦方向の列へ詰めるときに使います。

## 1. 概要

- **入力**: アイテムの（幅, 高さ）の一覧、行間・列間の余白、列数の上限、使用可能な幅の上限。
- **出力**: 実際に使った幅（`UsedWidth`）と、最小化された最大列高さ（`MinHeight`）。 加えて、列ごとの範囲と、アイテムごとの位置・大きさを取得できます。
- **算法は 3 つ**（`Method`）。 動的計画法（最適解を保証）・貪欲法（高速）・二分探索（バランス型）。 既定は二分探索です。

UI には依存しません（描画 API も描画の抽象も持ちません）。 位置と大きさは数値として返すため、どの UI のパネルからも同じ結果になります。

## 2. 使い方

```csharp
using EsUtil.Feature.MultiColumn;

// アイテムの（幅, 高さ）。 高さがばらついてよい
var items = new (double Width, double Height)[]
{
    (120, 40), (120, 80), (120, 56), (120, 96),
};

// 行間・列間の余白と、列数の上限（10 列まで）
var engine = new MultiColumnLayoutEngine(items, (RowSpace: 8.0, ColumnSpace: 8.0), columnLimit: 10);

// 使用できる幅の上限を渡す（この幅を超えないよう列数が決まる）
var (usedWidth, minHeight) = engine.Solve(widthLimit: 400.0);

// 列ごとの範囲（アイテムの添字の範囲）と、アイテムごとの矩形
var segments = engine.GetLastColumnSegments();
var layouts = engine.GetLastItemLayouts();
```

### 2.1 算法を指定して 1 回だけ解く

```csharp
using EsUtil.Feature.MultiColumn;

// 呼び出しの間だけ算法を切り替える（呼び出し後は元の算法へ戻る）
var (usedWidth, minHeight) = engine.Solve(widthLimit: 400.0, method: Method.Greedy);
```

### 2.2 二分探索の調整

```csharp
engine.CurrentBinarySearchOptions = new BinarySearchOptions(Epsilon: 1e-3, MaxIterations: 100, LowerBoundRatio: 0.95);
```

## 3. 含まれるもの

`MultiColumnLayoutEngine`（本体）と、その入れ子の型（`Method` / `BinarySearchOptions` / `MetadataKey`）、拡張メンバー（`MultiColumnLayoutEngineExtensions` の `Solve(widthLimit, method)` と状態参照プロパティ）。

## 4. 依存

なし（EsUtil のパッケージも NuGet パッケージも参照しません）。 規則は [`.github/依存関係.md`](../../.github/依存関係.md)。

## 5. 対応フレームワーク

| 項目 | 内容 |
| --- | --- |
| 対象フレームワーク | `net10.0` |
| 言語 | C# 14（.NET 10） |
| Native AOT | 対応（`IsAotCompatible=true`。 AOT の解析警告が 0 件であることをビルドで検査する） |

## 6. テスト

`tests/`（xUnit v3）。 算法ごとの最適性（動的計画法が最小の最大列高さを返すこと）・貪欲法と二分探索の品質・境界値（アイテム 0 件・1 件・幅の上限が最小・列数の上限 1）・不正な引数の例外・キャッシュの再計算を検証します。
手順は [`.github/検証手順.md`](../../.github/検証手順.md)。

## 7. ドキュメント

- 層の帰属 … [`../README.md`](../README.md)
- 依存の許可範囲 … [`.github/依存関係.md`](../../.github/依存関係.md)
- API リファレンス … [`docs/SPEC_API.md`](docs/SPEC_API.md)
- システム仕様書 … [`docs/SPEC_System.md`](docs/SPEC_System.md)

## 8. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
