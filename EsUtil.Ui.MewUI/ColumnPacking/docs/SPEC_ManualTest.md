# EsUtil.Ui.MewUI.ColumnPacking 手動確認書

**自動テストで確認できない操作**（実描画・DPI・入力の感触・IME・ウィンドウの配置）の確認項目です。 操作 → 期待結果 → 自動化の可否 の形で書き、**経緯は書きません**（規則は [`.github/ドキュメント規約.md`](../../../.github/ドキュメント規約.md) §12）。

API は `ColumnPackingPanel` を操作します。 計算は `EsUtil.Feature.ColumnPacking.Core` が単一所有します。

## 目次 <!-- omit from toc -->

- [1. 配置の追随](#1-配置の追随)
- [2. 項目の供給](#2-項目の供給)

## 1. 配置の追随

| # | 操作（API / GUI） | 期待結果 | 自動化 |
| --- | --- | --- | --- |
| 1 | GUI: ウィンドウの幅を変える | 列数が変わり、`LayoutColumnCount` / `LayoutUsedWidth` / `LayoutMinHeight` が追随する | 一部可 |
| 2 | API: `RowSpace` / `ColumnSpace` を変える | 行間と列間が DIP で変わり、項目が重ならない | 一部可 |
| 3 | API: `ColumnLimit = 2` にして 10 件を並べる | 列数が 2 を超えない | 一部可 |
| 4 | API: `Method` を切り替える | 配置が変わり、`LayoutMinHeight` が実際の最大列高さと一致する | 一部可 |
| 5 | GUI: 表示倍率 150%・200% にする | 余白と項目の大きさが DIP で一定 | 不可（DPI は環境依存） |

## 2. 項目の供給

| # | 操作（API / GUI） | 期待結果 | 自動化 |
| --- | --- | --- | --- |
| 6 | GUI: 直接の子と `ItemsSource` を同時に置く | 両方が並び、`ItemsSourceFirst` の真偽で順序が入れ替わる | 一部可 |
| 7 | API: `ItemTemplate` を設定する | `ItemsSource` の各項目がテンプレートで作られる | 一部可 |
| 8 | API: `SetItems` を呼ぶ | 依存関係プロパティを介さずに子が作り直され、`ItemsSource` の値は変わらない | 一部可 |
| 9 | GUI: 高さが足りない状態にする | 項目が重ならず、列の高さが揃う（一覧はスクロールしない） | 不可 |
| 10 | GUI: 項目を 100 件以上並べる | 例外が出ず、スクロールなしで配置が完了する（仮想化はしない） | 不可 |
