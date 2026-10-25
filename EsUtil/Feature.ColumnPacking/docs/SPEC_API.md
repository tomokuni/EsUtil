# EsUtil.Feature.ColumnPacking API リファレンス

`EsUtil.Feature.ColumnPacking` の公開 API です。

## 目次 <!-- omit from toc -->

- [1. 前提条件](#1-前提条件)
- [2. 公開 API](#2-公開-api)
  - [2.1 `ColumnPacker`](#21-columnpacker)
  - [2.2 `ColumnPacker.Method`](#22-columnpackermethod)
  - [2.3 `ColumnPacker.BinarySearchOptions`](#23-columnpackerbinarysearchoptions)
  - [2.4 `ColumnPacker.MetadataKey`](#24-columnpackermetadatakey)
  - [2.5 `ColumnPackerExtensions`](#25-columnpackerextensions)
- [3. 契約（利用者が守る・受け取る値）](#3-契約利用者が守る受け取る値)
- [4. 使用例](#4-使用例)
- [5. 制限事項](#5-制限事項)

## 1. 前提条件

- **単位**: 幅・高さ・余白はすべて同じ単位（DIP でもピクセルでもよい）で渡します。 本ライブラリは単位を解釈しません。
- **座標系**: 出力する矩形の原点は左上です（`X` は右方向、`Y` は下方向）。
- **アイテムの順序**: 渡した順に列へ詰めます（並べ替えません）。 並べ替えたい場合は呼び出し側で並べ替えてから渡します。
- **スレッド**: 1 つのインスタンスを同時に使えません（`CurrentMethod` を切り替えるため）。 並行して解く場合はインスタンスを分けます。

## 2. 公開 API

名前空間とアセンブリ名は `EsUtil.Feature.ColumnPacking` です。

### 2.1 `ColumnPacker`

アイテムの集合と余白を保持し、指定した幅の上限でレイアウトを解きます。

| メンバー | 内容 |
| --- | --- |
| `ColumnPacker(IReadOnlyList<(double Width, double Height)> items, (double Row, double Column) space, int columnLimit)` | アイテム・行間と列間の余白・列数の上限を渡して生成します。 |
| `(double UsedWidth, double MinHeight) Solve(double widthLimit)` | 幅の上限を渡して解きます。 `UsedWidth` は実際に使った幅、`MinHeight` は最小化された最大列高さです。 |
| `bool IsValidWidth(double widthLimit)` | 幅の上限が「最も広いアイテム + 余白」を下回るか（＝1 列でしか並べられないか）を返します。 |
| `(double UsedWidth, double MinHeight) SolveSingleColumnLayout()` | 1 列で並べたときの結果を返します。 |
| `void VerifyLayoutResult(double widthLimit)` | 直前の結果が幅の上限と単列配置の条件を満たすか検証します。 満たさない場合は例外を送出します。 |
| `(double UsedWidth, double MinHeight) GetLastResult()` | 直前の結果を返します。 |
| `(int StartIdx, int EndIdx)[] GetLastColumnSegments()` | 直前の列ごとのアイテム範囲（0 始まり・両端を含む）を返します。 |
| `IReadOnlyList<(double X, double Y, double Width, double Height)> GetLastItemLayouts()` | 直前のアイテムごとの矩形を返します。 |
| `void ClearCache()` | 列の計測結果のキャッシュを破棄します（アイテムを変えた場合に呼びます）。 |
| `Method CurrentMethod { get; set; }` | 使う算法を取得または設定します（既定は `BinarySearch`）。 |
| `BinarySearchOptions CurrentBinarySearchOptions { get; set; }` | 二分探索の調整値を取得または設定します（設定時に妥当性を検証します）。 |
| `string GetCurrentStrategyName()` | 現在の算法の名前（`Greedy` / `DynamicProgramming` / `BinarySearch`）を返します。 |
| `int? GetBinarySearchIterationCount()` | 直前の二分探索の反復回数を返します（二分探索を使っていない場合は null）。 |

### 2.2 `ColumnPacker.Method`

使う算法です。

| 値 | 内容 |
| --- | --- |
| `DynamicProgramming` | 動的計画法。 最適解（最大列高さの最小値）を保証します。 計算量は O(n² × m)。 |
| `Greedy` | 貪欲法。 高速（O(n × m)）ですが最適解を保証しません。 |
| `BinarySearch` | 二分探索。 貪欲法の結果を上限に、達成できる最大列高さを二分探索します（既定）。 |

### 2.3 `ColumnPacker.BinarySearchOptions`

二分探索の調整値です（`readonly record struct`）。

| メンバー | 内容 |
| --- | --- |
| `BinarySearchOptions(double Epsilon = 1e-3, int MaxIterations = 100, double LowerBoundRatio = 0.95)` | 収束の許容誤差・最大反復回数・下限の比率を指定します。 |
| `Default` | 既定値（`1e-3` / `100` / `0.95`）の共有インスタンスです。 |
| `Epsilon` / `MaxIterations` / `LowerBoundRatio` | 各値です。 |
| `void IsValid()` | 値が範囲内か検証します（範囲外は `ArgumentOutOfRangeException`）。 |

### 2.4 `ColumnPacker.MetadataKey`

算法が返す補足情報のキーです。

| 値 | 内容 |
| --- | --- |
| `StrategyName` | 使った算法の名前。 |
| `BinarySearchIterationCount` | 二分探索の反復回数。 |

### 2.5 `ColumnPackerExtensions`

`ColumnPacker` のインスタンスへ、C# 14 の拡張メンバーで追加される API です（`using EsUtil.Feature.ColumnPacking;` だけで使えます）。

| メンバー | 内容 |
| --- | --- |
| `Solve(double widthLimit, Method method)` | 呼び出しの間だけ算法を切り替えて解きます。 例外が発生しても元の算法へ戻します。 |
| `ItemCount` | 登録されているアイテムの数。 |
| `ColumnCount` | 直前の解で確定した列数。 |
| `IterationCount` | 直前の二分探索の反復回数（二分探索以外は 0）。 |

## 3. 契約（利用者が守る・受け取る値）

- **`widthLimit` は正の値**です（0 以下は `ArgumentException`）。
- **`MinHeight` は単列配置の高さ以下**です（`VerifyLayoutResult` が検証します）。
- **`UsedWidth` は `widthLimit` 以下**です。
- **列の範囲は 0 始まりで両端を含みます**。 アイテムが 0 件の場合は空の配列です。
- **`GetLastItemLayouts()` は直前の解の結果**です。 解く前に呼ぶと空の一覧を返します。

## 4. 使用例

```csharp
using EsUtil.Feature.ColumnPacking;

var items = new (double Width, double Height)[] { (120, 40), (120, 80), (120, 56) };
var engine = new ColumnPacker(items, (8.0, 8.0), columnLimit: 10);

var (usedWidth, minHeight) = engine.Solve(400.0);
foreach (var (start, end) in engine.GetLastColumnSegments())
{
    // 列ごとの処理（start から end までが 1 列に入る）
}
```

```csharp
using EsUtil.Feature.ColumnPacking;

// 最適解が必要なとき（アイテム数が少ない場合）
var optimal = engine.Solve(400.0, Method.DynamicProgramming);
```

## 5. 制限事項

- **`Solve` はインスタンスの状態を更新します**（直前の結果・キャッシュ）。 同じインスタンスを複数スレッドから同時に使わないでください。
- **アイテムを差し替える方法はありません**。 アイテムが変わった場合は新しいインスタンスを作ります（計測のキャッシュは `ClearCache` で破棄できます）。
- **列数は `columnLimit` を超えません**が、幅の上限が足りない場合は列数が減ります（1 列になることもあります）。
- **本ライブラリは描画を行いません**。 返した矩形を UI のパネルへ反映するのは利用側です（例: [`EsUtil.Ui.WinUI3.MultiColumn`](../../../EsUtil.Ui.WinUI3/MultiColumn/README.md)）。
