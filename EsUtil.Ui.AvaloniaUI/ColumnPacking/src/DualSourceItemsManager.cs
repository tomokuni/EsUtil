using System.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace EsUtil.Ui.AvaloniaUI.ColumnPacking;

/// <summary><b>ItemsSource と直接子要素の両方を管理するクラス</b></summary>
/// <remarks>
/// ItemsControl パターンで、ItemsSource からの要素と XAML に直接書いた子要素を同時に扱うための管理ロジックをカプセル化します。<br/>
/// 唯一の真実の源（Single Source of Truth）として機能します。
/// <br/>
/// 【特徴】<br/>
/// • 状態を一元管理（ItemsSourceFirst, DirectChildItems）<br/>
/// • ItemsSource と直接子要素の分離管理<br/>
/// • 配置順序の制御（ItemsSourceFirst プロパティ）<br/>
/// • 再構築ロジックの一元化<br/>
/// </remarks>
public class DualSourceItemsManager
{
    /// <summary><b>直接追加された子要素を保存するリスト</b></summary>
    private List<Control> _directChildItems = [];

    /// <summary><b>ItemsSource を先に配置するかどうか</b></summary>
    /// <remarks>
    /// true: ItemsSource → 直接子要素<br/>
    /// false: 直接子要素 → ItemsSource
    /// </remarks>
    private bool _itemsSourceFirst = true;

    /// <summary><b>直接子要素が保存されたことを示すフラグ</b></summary>
    /// <remarks>
    /// 空の場合でも保存済みを区別するため
    /// </remarks>
    private bool _isDirectChildItemsSaved;

    /// <summary><b>直接子要素が保存されたかどうかを取得します</b></summary>
    /// <remarks>
    /// ItemsSource が最初に設定されるまで false を返します
    /// </remarks>
    public bool HasDirectChildItemsStored => _isDirectChildItemsSaved;

    /// <summary><b>保存された直接子要素を読み取り専用で取得します</b></summary>
    /// <value>保存した直接の子（並びは保存した時点のもの）。 未保存の間は空。</value>
    public IReadOnlyList<Control> DirectChildItems => _directChildItems.AsReadOnly();

    /// <summary><b>ItemsSource を先に配置するかどうかを取得または設定します</b></summary>
    /// <value>ItemsSource を先に置く場合は true。 既定は true。</value>
    /// <remarks>
    /// デフォルト: true (ItemsSource を先に配置)
    /// </remarks>
    public bool ItemsSourceFirst
    {
        get => _itemsSourceFirst;
        set => _itemsSourceFirst = value;
    }

    /// <summary><b>ItemsSource が初めて設定されたときの直接子要素を保存します</b></summary>
    /// <remarks>
    /// 【用途】<br/>
    /// DualSourceItemsPanel.OnItemsSourceChanged コールバックで呼び出し、その時点の子要素を保存します。<br/>
    /// 以降、同じパネルインスタンスでは呼び出されません（一度のみ）。<br/>
    /// </remarks>
    /// <param name="currentChildren">現在の子要素コレクション</param>
    public void SaveDirectChildItems(Controls currentChildren)
    {
        // まだ直接子要素が保存されていない場合のみ保存を実行する
        if (_isDirectChildItemsSaved)
        {
            return;
        }

        // 現在の子要素コレクションをそのまま複製する（要素の親は変えない）
        _directChildItems = [.. currentChildren];
        _isDirectChildItemsSaved = true;
    }

    /// <summary>項目の並びから要素を生成する（依存関係プロパティを介さない経路）。</summary>
    /// <param name="items">項目の並び。 null の場合は空。</param>
    /// <param name="itemTemplate">要素のテンプレート。 null の場合は空。</param>
    /// <returns>生成した要素。</returns>
    /// <remarks>
    /// <see cref="GenerateItemsSourceElements"/> と同じ規則（<c>DataContext</c> に項目を写す）を、<br/>
    /// コレクションをそのまま渡す経路へ適用する（<see cref="DualSourceItemsPanel.SetItems"/> から使う）。
    /// </remarks>
    public static IReadOnlyList<Control> GenerateElements(IEnumerable<object?>? items, IDataTemplate? itemTemplate)
        => items is null ? [] : GenerateItemsSourceElements(items, itemTemplate);

    /// <summary><b>ItemsSource から要素を生成します</b></summary>
    /// <remarks>
    /// 【処理】<br/>
    /// • ItemTemplate が null または ItemsSource が null の場合は空リストを返す<br/>
    /// • ItemsSource の各要素から DataTemplate.Build() で要素を生成<br/>
    /// • 生成された各要素に DataContext を設定<br/>
    /// </remarks>
    /// <param name="itemsSource">アイテムのソース</param>
    /// <param name="itemTemplate">アイテムテンプレート</param>
    /// <returns>
    /// 生成された <see cref="Control"/> のリスト。 <c>ItemTemplate</c> が null / <c>ItemsSource</c> が null /
    /// <c>ItemsSource</c> が <see cref="IEnumerable"/> でない場合は空のリスト。
    /// </returns>
    public static IReadOnlyList<Control> GenerateItemsSourceElements(object? itemsSource, IDataTemplate? itemTemplate)
    {
        var itemsSourceElements = new List<Control>();

        // ItemTemplate または ItemsSource が null の場合、空リストを返す
        if (itemTemplate is null || itemsSource is null)
        {
            return itemsSourceElements;
        }

        // ItemsSource が IEnumerable である場合、各要素を処理する
        if (itemsSource is IEnumerable enumerable)
        {
            // 各アイテムに対して DataTemplate から要素を生成し、DataContext を設定する
            foreach (var item in enumerable)
            {
                if (itemTemplate.Build(item) is { } element)
                {
                    element.DataContext = item;
                    itemsSourceElements.Add(element);
                }
            }
        }

        return itemsSourceElements;
    }

    /// <summary><b>ItemsSource と直接子要素を結合して、指定されたコレクションに追加します</b></summary>
    /// <remarks>
    /// 【処理フロー】<br/>
    /// 1. 対象コレクションをクリア<br/>
    /// 2. ItemsSourceFirst に基づいて配置順序を決定<br/>
    /// 3. ItemsSource 要素と直接子要素を順に追加<br/>
    /// </remarks>
    /// <param name="targetCollection">要素を追加するターゲットコレクション</param>
    /// <param name="itemsSourceElements">ItemsSource から生成された要素</param>
    public void RebuildChildren(Controls targetCollection, IReadOnlyList<Control> itemsSourceElements)
    {
        // ターゲットコレクションをクリアして再構築の準備をする
        targetCollection.Clear();

        // ItemsSourceFirst に従って、ItemsSource の要素と直接の子を順に追加する
        if (_itemsSourceFirst)
        {
            AddAll(targetCollection, itemsSourceElements);
            AddAll(targetCollection, _directChildItems);
            return;
        }

        AddAll(targetCollection, _directChildItems);
        AddAll(targetCollection, itemsSourceElements);
    }

    /// <summary><b>ItemsSource から要素を生成し、それを含めて子要素を再構築します</b></summary>
    /// <remarks>
    /// <see cref="GenerateItemsSourceElements"/> で <paramref name="itemsSource"/> の各要素を<br/>
    /// <paramref name="itemTemplate"/> から生成し、直接の子要素と結合して
    /// <paramref name="targetCollection"/> を作り直します（生成が空でも直接の子要素は残ります）。
    /// </remarks>
    /// <param name="targetCollection">要素を追加するターゲットコレクション</param>
    /// <param name="itemsSource">アイテムのソース（null 可）</param>
    /// <param name="itemTemplate">アイテム テンプレート（null 可）</param>
    public void RebuildChildren(Controls targetCollection, object? itemsSource, IDataTemplate? itemTemplate)
    {
        // ItemsSource から要素を生成する
        var itemsSourceElements = GenerateItemsSourceElements(itemsSource, itemTemplate);

        // ItemsSource と直接子要素を結合して子要素を再構築する
        RebuildChildren(targetCollection, itemsSourceElements);
    }

    /// <summary><b>マネージャーをリセットします</b></summary>
    /// <remarks>
    /// ItemsSource と直接子要素のキャッシュをクリアします。
    /// </remarks>
    public void Reset()
    {
        // 直接子要素のリストをクリアする
        _directChildItems.Clear();

        // 保存済みフラグを false にリセットする
        _isDirectChildItemsSaved = false;

        // ItemsSourceFirst をデフォルト値 true にリセットする
        _itemsSourceFirst = true;
    }

    /// <summary>要素をまとめて追加する。</summary>
    /// <param name="target">追加先のコレクション。</param>
    /// <param name="elements">追加する要素。</param>
    private static void AddAll(Controls target, IReadOnlyList<Control> elements)
    {
        for (var index = 0; index < elements.Count; index++)
        {
            target.Add(elements[index]);
        }
    }
}
