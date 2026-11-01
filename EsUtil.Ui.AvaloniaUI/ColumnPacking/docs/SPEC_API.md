# EsUtil.Ui.AvaloniaUI.ColumnPacking API リファレンス

`EsUtil.Ui.AvaloniaUI.ColumnPacking` の公開 API です。

## 目次 <!-- omit from toc -->

- [1. 前提条件](#1-前提条件)
- [2. 公開 API](#2-公開-api)
  - [2.1 `ColumnPackingPanel`](#21-columnpackingpanel)
  - [2.2 `DualSourceItemsPanel`（基底）](#22-dualsourceitemspanel基底)
  - [2.3 `DualSourceItemsManager`](#23-dualsourceitemsmanager)
- [3. 契約（利用者が守る・受け取る値）](#3-契約利用者が守る受け取る値)
- [4. 使用例](#4-使用例)
- [5. 制限事項](#5-制限事項)

## 1. 前提条件

- **単位**: すべて DIP です（Avalonia UI の座標系）。
- **名前空間**: `EsUtil.Ui.AvaloniaUI.ColumnPacking`（アセンブリ名も同じ）。 型 `ColumnPackingPanel` はパッケージ名と一致しません（名前空間のセグメントと型名を一致させない規則 `MA0049` のためです）。
- **ItemsSource**: `ItemsSource` を使う場合は `ItemTemplate` も設定してください（どちらかが null の間は要素を作りません）。
- **データだけを渡す場合は `SetItems` を使ってください**: `ItemsSource` は依存関係プロパティであり、値を書き換えるたびに子を作り直します。 `SetItems` は managed のリストをそのまま保持し、呼んだときにだけ作り直します。
- **初回の再構築に直接の子を含めてください**: 直接の子は最初の再構築時に保存されます（`ItemTemplate` を設定するとその時点で再構築されるため、`ItemTemplate` → 直接の子 → `SetItems` の順で設定します）。
- **レイアウトの実行**: 配置は Avalonia UI のレイアウト パス（`Measure` / `Arrange`）で行います。 パネルを `ScrollViewer` の中に置くと、幅の上限が確定してから計算されます。

## 2. 公開 API

### 2.1 `ColumnPackingPanel`

`DualSourceItemsPanel` を継承した、複数列の配置を行うパネルです。

| メンバー | 内容 |
| --- | --- |
| `double RowSpace { get; set; }` | 行間（DIP）。 既定は 8。 |
| `double ColumnSpace { get; set; }` | 列間（DIP）。 既定は 8。 |
| `int ColumnLimit { get; set; }` | 列数の上限。 既定は 10。 |
| `ColumnPacker.Method Method { get; set; }` | 配置の算法（`DynamicProgramming` / `Greedy` / `BinarySearch`）。 既定は `BinarySearch`。 |
| `double? LayoutUsedWidth { get; }` | 直前の配置で使った幅。 未計算の間は null。 |
| `double? LayoutMinHeight { get; }` | 直前の配置の最大列高さ（最小化された値）。 未計算の間は null。 |
| `int? LayoutColumnCount { get; }` | 直前の配置の列数。 未計算の間は null。 |
| `string? LayoutColumnSegments { get; }` | 直前の配置の列の区切り（`[3,6,9]` 形式）。 未計算の間は null。 |
| `ILogger Log { get; set; }` | ログの出力先（null は出力なし）。 |
| `string? LogPrefix { get; set; }` | ログの接頭辞（null または空白は既定値 `[ColumnPacking]` へ戻す）。 |
| `protected ColumnPacker? Layout { get; set; }` | 直前の配置に使った計算のインスタンス（派生クラス向け）。 |
| `protected override Size MeasureOverride(Size availableSize)` | 幅の上限から配置を計算します。 |
| `protected override Size ArrangeOverride(Size finalSize)` | 計算した矩形へ子を配置します。 |

### 2.2 `DualSourceItemsPanel`（基底）

`ItemsSource` から作る要素と、XAML（またはコード）に直接書いた子要素を同時に扱う抽象パネルです。

| メンバー | 内容 |
| --- | --- |
| `object? ItemsSource { get; set; }` | 並べるデータ。 変更すると子を作り直します。 |
| `IDataTemplate? ItemTemplate { get; set; }` | `ItemsSource` の各項目から要素を作るテンプレート。 |
| `void SetItems(IEnumerable<object?>? items)` | データの並びを渡して子を作り直す（**依存関係プロパティを介さない**）。 null は `ItemsSource` 経由へ戻す |
| `bool ItemsSourceFirst { get; set; }` | true なら `ItemsSource` の要素を先に置きます（既定は true）。 |
| `bool IsManagerInitialized { get; }` | 直接の子の保存が済んでいるか。 |
| `bool IsItemsSourceConnected { get; }` | `ItemsSource` と `ItemTemplate` の両方が設定されているか。 |
| `int DirectChildItemsCount { get; }` | 保存している直接の子の数。 |
| `protected DualSourceItemsManager ItemsManager { get; }` | 子の管理（派生クラス向け）。 |
| `protected void RebuildItems()` | 子を作り直します。 |
| `protected virtual void OnItemsSourceChangedInternal()` | `ItemsSource` の変更時に呼ばれます（派生クラスで上書きできます）。 |
| `protected virtual void OnItemTemplateChangedInternal()` | `ItemTemplate` の変更時に呼ばれます。 |
| `protected virtual void OnItemsSourceFirstChangedInternal()` | `ItemsSourceFirst` の変更時に呼ばれます。 |
| `protected virtual void OnDependencyPropertyChangedInternal(AvaloniaPropertyChangedEventArgs change)` | 依存関係プロパティの変更時に呼ばれます。 |

### 2.3 `DualSourceItemsManager`

`ItemsSource` の要素と直接の子を管理します（パネルの内部で使いますが、派生クラスからも使えます）。

| メンバー | 内容 |
| --- | --- |
| `bool ItemsSourceFirst { get; set; }` | `ItemsSource` を先に置くかどうか。 |
| `bool HasDirectChildItemsStored { get; }` | 直接の子を保存済みか。 |
| `IReadOnlyList<Control> DirectChildItems { get; }` | 保存している直接の子。 |
| `void SaveDirectChildItems(Controls currentChildren)` | 現在の子を「直接の子」として保存します。 |
| `static IReadOnlyList<Control> GenerateItemsSourceElements(object? itemsSource, IDataTemplate? itemTemplate)` | `ItemsSource` から要素を作ります（どちらかが null なら空）。 |
| `static IReadOnlyList<Control> GenerateElements(IEnumerable<object?>? items, IDataTemplate? itemTemplate)` | 項目の並びから要素を作ります（`SetItems` の経路。 依存関係プロパティを介さない）。 |
| `void RebuildChildren(Controls targetCollection, IReadOnlyList<Control> itemsSourceElements)` | 保存した直接の子と `ItemsSource` の要素を並べ直します。 |
| `void RebuildChildren(Controls targetCollection, object? itemsSource, IDataTemplate? itemTemplate)` | 上記の簡易形（要素をその場で作ります）。 |
| `void Reset()` | 保存した状態を破棄します。 |

## 3. 契約（利用者が守る・受け取る値）

- **`ItemsSource` を設定する前に置いた子は「直接の子」として保存**され、`ItemsSource` の要素と一緒に並びます。
- **`ItemsSource` と `ItemTemplate` の両方が必要**です（片方だけでは要素を作りません）。
- **`Layout*` のプロパティは読み取り専用**です（設定する入口がありません）。 値は配置のたびに更新されます。
- **レイアウト結果は配置の後**に確定します（最初の計測の直後は null のことがあります）。
- **幅が確定しないパス（無限大・0 以下）**では、直前に確定した幅で計算します（親が内容を 2 通りの幅で測る場合に、レイアウトが交互に変わらないようにするため）。

## 4. 使用例

```xml
<ScrollViewer HorizontalScrollBarVisibility="Disabled">
    <panel:ColumnPackingPanel ColumnLimit="3" RowSpace="8" ColumnSpace="8"
                            ItemsSource="{Binding Items}" ItemTemplate="{StaticResource ItemTemplate}">
        <Border Height="64"><TextBlock Text="固定の要素" Margin="8,4" /></Border>
    </panel:ColumnPackingPanel>
</ScrollViewer>
```

```xml
<!-- レイアウト結果を表示する -->
<TextBlock Text="{Binding #Panel.LayoutMinHeight}" />
```

## 5. 制限事項

- **子の仮想化は行いません**（`ItemsSource` の全件を要素として保持します）。 大量の項目には向きません。
- **列の高さは指定できません**（常に最大列高さを最小化します）。
- **列の幅は子の幅で決まります**（等幅にそろえる機能はありません）。
