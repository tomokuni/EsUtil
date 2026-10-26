using System.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EsUtil.Ui.WinUI3.ColumnPacking;

/// <summary><b>ItemsSource と直接子要素の両方を管理するクラス</b></summary>
/// <remarks>
/// ItemsControl パターンで、ItemsSource からの要素と XAML で直接追加された子要素を同時に扱うための管理ロジックをカプセル化します。<br/>
/// 唯一の真実の源（Single Source of Truth）として機能します。<br/>
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
    private List<UIElement> _directChildItems = [];

    /// <summary><b>ItemsSource を先に配置するかどうか</b></summary>
    /// <remarks>
    /// true: ItemsSource → 直接子要素<br/>
    /// false: 直接子要素 → ItemsSource
    /// </remarks>
    private bool _itemsSourceFirst = true;

    /// <summary><b>直接子要素が保存されたかどうかを取得します</b></summary>
    /// <remarks>
    /// ItemsSource が最初に設定されるまで false を返します
    /// </remarks>
    public bool HasDirectChildItemsStored => _isDirectChildItemsSaved;

    /// <summary><b>保存された直接子要素を読み取り専用で取得します</b></summary>
    public IReadOnlyList<UIElement> DirectChildItems => _directChildItems.AsReadOnly();

    /// <summary><b>直接子要素が保存されたことを示すフラグ</b></summary>
    /// <remarks>
    /// 空の場合でも保存済みを区別するため
    /// </remarks>
    private bool _isDirectChildItemsSaved;

    /// <summary><b>ItemsSource を先に配置するかどうかを取得または設定します</b></summary>
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
    public void SaveDirectChildItems(UIElementCollection currentChildren)
    {
        // まだ直接子要素が保存されていない場合のみ保存を実行
        if (!_isDirectChildItemsSaved)
        {
            // 現在の子要素コレクションから UIElement のみを抽出し、リストにコピー
            _directChildItems = [.. currentChildren.OfType<UIElement>()];
            // 保存済みフラグを true に設定
            _isDirectChildItemsSaved = true;
        }
    }

    /// <summary><b>ItemsSource から要素を生成します</b></summary>
    /// <remarks>
    /// 【処理】<br/>
    /// • ItemTemplate が null または ItemsSource が null の場合は空リストを返す<br/>
    /// • ItemsSource の各要素から DataTemplate.LoadContent() で要素を生成<br/>
    /// • 生成された各要素に DataContext を設定<br/>
    /// </remarks>
    /// <param name="itemsSource">アイテムのソース</param>
    /// <param name="itemTemplate">アイテムテンプレート</param>
    /// <returns>生成された <see cref="FrameworkElement"/> のリスト。 次のいずれかに当たる場合は**空のリスト**（<c>ItemTemplate</c> が <see langword="null"/> / <c>ItemsSource</c> が <see langword="null"/> / <c>ItemsSource</c> が <see cref="IEnumerable"/> でない）。</returns>
    public static IReadOnlyList<FrameworkElement> GenerateItemsSourceElements(object? itemsSource, DataTemplate? itemTemplate)
    {
        var itemsSourceElements = new List<FrameworkElement>();

        // ItemTemplate または ItemsSource が null の場合、空リストを返す
        if (itemTemplate == null || itemsSource == null)
            return itemsSourceElements;

        // ItemsSource が IEnumerable である場合、各要素を処理
        if (itemsSource is IEnumerable enumerable)
        {
            // 各アイテムに対して DataTemplate から要素を生成し、DataContext を設定
            foreach (var item in enumerable)
            {
                var el = itemTemplate.LoadContent() as FrameworkElement;
                if (el is not null)
                {
                    el.DataContext = item;
                    itemsSourceElements.Add(el);
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
    public void RebuildChildren(UIElementCollection targetCollection, IReadOnlyList<FrameworkElement> itemsSourceElements)
    {
        // ターゲットコレクションをクリアして再構築の準備
        targetCollection.Clear();

        // ItemsSourceFirst が true の場合、ItemsSource 要素を先に追加
        if (_itemsSourceFirst)
        {
            // ItemsSource 要素を順に追加
            for (int i = 0; i < itemsSourceElements.Count; i++)
                targetCollection.Add(itemsSourceElements[i]);
            // 直接子要素を順に追加
            for (int i = 0; i < _directChildItems.Count; i++)
                targetCollection.Add(_directChildItems[i]);
        }
        else
        {
            // 直接子要素を先に追加
            for (int i = 0; i < _directChildItems.Count; i++)
                targetCollection.Add(_directChildItems[i]);
            // ItemsSource 要素を順に追加
            for (int i = 0; i < itemsSourceElements.Count; i++)
                targetCollection.Add(itemsSourceElements[i]);
        }
    }

    /// <summary><b>ItemsSource から要素を生成し、それを含めて子要素を再構築します</b></summary>
    /// <remarks>
    /// <see cref="GenerateItemsSourceElements"/> で <paramref name="itemsSource"/> の各要素を
    /// <see cref="DataTemplate"/> から生成し、直接の子要素と結合して
    /// <paramref name="targetCollection"/> を作り直します（生成が空でも直接の子要素は残ります）。
    /// </remarks>
    /// <param name="targetCollection">要素を追加するターゲットコレクション</param>
    /// <param name="itemsSource">アイテムのソース（null 可）</param>
    /// <param name="itemTemplate">アイテム テンプレート（null 可）</param>
    public void RebuildChildren(UIElementCollection targetCollection, object? itemsSource, DataTemplate? itemTemplate)
    {
        // ItemsSource から要素を生成
        var itemsSourceElements = GenerateItemsSourceElements(itemsSource, itemTemplate);

        // ItemsSource と直接子要素を結合して子要素を再構築
        RebuildChildren(targetCollection, itemsSourceElements);
    }

    /// <summary><b>マネージャーをリセットします</b></summary>
    /// <remarks>
    /// ItemsSource と直接子要素のキャッシュをクリア<br/>
    /// </remarks>
    public void Reset()
    {
        // 直接子要素のリストをクリア
        _directChildItems.Clear();
        // 保存済みフラグを false にリセット
        _isDirectChildItemsSaved = false;
        // ItemsSourceFirst をデフォルト値 true にリセット
        _itemsSourceFirst = true;
    }
}
