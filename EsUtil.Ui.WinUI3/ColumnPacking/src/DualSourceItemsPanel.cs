using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EsUtil.Ui.WinUI3.ColumnPacking;

/// <summary><b>ItemsSource と直接子要素の両方を管理するパネルベースクラス</b></summary>
/// <remarks>
/// Panel から継承し、ItemsSource と XAML で直接追加された子要素の両方を同時に扱う機能を提供するベースクラスです。<br/>
/// ItemsControl の ItemsPanel として 100% 置き換え可能な互換性を提供します。<br/>
/// <br/>
/// 【特徴】<br/>
/// • DualSourceItemsManager を内包し、状態を一元管理<br/>
/// • ItemTemplate による要素生成をサポート<br/>
/// • ItemsSourceFirst プロパティで配置順序を制御<br/>
/// • DependencyProperty でバインディング対応<br/>
/// • パネル初期化時に自動的にレイアウト計算をトリガー<br/>
/// • デバッグ・モニタリング用の補助プロパティを提供<br/>
/// • 派生クラスは MeasureOverride/ArrangeOverride に専念可能<br/>
/// • 派生クラスが プロパティ変更イベントをオーバーライド可能<br/>
/// <br/>
/// 【設計原則】<br/>
/// • DualSourceItemsManager が唯一の真実の源（Single Source of Truth）<br/>
/// • DualSourceItemsPanel は UI プロパティのプロキシに徹する<br/>
/// • 同期ポイントは DependencyProperty コールバックのみ<br/>
/// • フォーカス管理と KeyboardNavigation は WinUI3 に委譲<br/>
/// <br/>
/// 【フォーカス管理】<br/>
/// • WinUI3 のネイティブなフォーカス管理に完全委譲<br/>
/// • 標準的な Panel と同等の動作<br/>
/// <br/>
/// 【拡張ポイント】<br/>
/// 派生クラスは以下の仮想メソッドをオーバーライドして、DependencyProperty 変更イベントを受け取ります：<br/>
/// • OnItemsSourceChangedInternal() - ItemsSource 変更時<br/>
/// • OnItemTemplateChangedInternal() - ItemTemplate 変更時<br/>
/// • OnItemsSourceFirstChangedInternal() - ItemsSourceFirst 変更時<br/>
/// • OnDependencyPropertyChangedInternal(DependencyPropertyChangedEventArgs) - 全プロパティ変更<br/>
/// </remarks>
public abstract class DualSourceItemsPanel : Panel
{
    /// <summary><b>ItemsSource と直接子要素の両方を管理するマネージャー</b></summary>
    /// <summary>ItemsSource と直接の子を管理する（派生クラスから使う）。</summary>
    protected DualSourceItemsManager ItemsManager { get; } = new();

    /// <summary><b>コンストラクター</b></summary>
    protected DualSourceItemsPanel()
    {
        // Loaded イベントにハンドラを登録し、パネルが UI ツリーに追加されたときに OnPanelLoaded メソッドを呼び出す
        Loaded += (s, e) => OnPanelLoaded();
    }


    /// <summary><b>ItemsManager が初期化済みかどうかを取得します</b></summary>
    /// <remarks>
    /// ItemsSource が最初に設定され、直接子要素が保存されたかどうかを判定します。<br/>
    /// true の場合、ItemsSource と直接子要素の管理が開始されています。<br/>
    /// </remarks>
    public bool IsManagerInitialized => ItemsManager.HasDirectChildItemsStored;

    /// <summary><b>ItemsSource と ItemTemplate が両方設定されているかどうかを取得します</b></summary>
    /// <remarks>
    /// 【用途】<br/>
    /// ItemsPanel として正しく機能するために必要な条件（ItemsSource と ItemTemplate が設定済み）を確認するために使用します。<br/>
    /// </remarks>
    public bool IsItemsSourceConnected => ItemsSource != null && ItemTemplate != null;

    /// <summary><b>直接保存された子要素の数を取得します（デバッグ用）</b></summary>
    /// <remarks>
    /// ItemsSource と直接子要素の分離管理状況を確認するために使用します。<br/>
    /// 0 より大きい値の場合、XAML で直接定義された子要素が存在します。<br/>
    /// </remarks>
    public int DirectChildItemsCount => ItemsManager.DirectChildItems.Count;


    /// <summary><b>ItemsSource 依存関係プロパティ</b></summary>
    /// <remarks>
    /// このプロパティにバインドされたコレクションからアイテムを生成します。
    /// </remarks>
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(object), typeof(DualSourceItemsPanel),
        new PropertyMetadata(null, OnItemsSourceChanged));

    /// <summary><b>ItemTemplate 依存関係プロパティ</b></summary>
    /// <remarks>
    /// 各アイテムの表示テンプレートを指定します。
    /// </remarks>
    public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(
        nameof(ItemTemplate), typeof(DataTemplate), typeof(DualSourceItemsPanel),
        new PropertyMetadata(null, OnItemTemplateChanged));

    /// <summary><b>ItemsSourceFirst 依存関係プロパティ</b></summary>
    /// <remarks>
    /// ItemsSource を先に配置するかどうかを指定します。
    /// </remarks>
    public static readonly DependencyProperty ItemsSourceFirstProperty = DependencyProperty.Register(
        nameof(ItemsSourceFirst), typeof(bool), typeof(DualSourceItemsPanel),
        new PropertyMetadata(true, OnItemsSourceFirstChanged));

    /// <summary><b>ItemsSource プロパティ</b></summary>
    /// <remarks>
    /// アイテムのソースとなるオブジェクトを取得または設定します。<br/>
    /// </remarks>
    public object? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary><b>ItemTemplate プロパティ</b></summary>
    /// <remarks>
    /// アイテムのデータテンプレートを取得または設定します。<br/>
    /// </remarks>
    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <summary><b>ItemsSourceFirst プロパティ</b></summary>
    /// <remarks>
    /// ItemsSource を先に配置するか、直接子要素を先に配置するかを取得または設定します。<br/>
    /// デフォルト: true (ItemsSource を先に配置)<br/>
    /// <br/>
    /// 【注意】<br/>
    /// このプロパティは DualSourceItemsManager にプロキシされます。<br/>
    /// </remarks>
    public bool ItemsSourceFirst
    {
        get => ItemsManager.ItemsSourceFirst;
        set => ItemsManager.ItemsSourceFirst = value;
    }


    /// <summary><b>ItemsSource が変更されたときに呼び出されます</b></summary>
    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var panel = (DualSourceItemsPanel)d;

        // 初めて ItemsSource が設定された場合（OldValue が null で NewValue が null でない場合）、現在の子要素コレクションを保存する
        // これにより、ItemsSource と直接子要素の両方を管理できるようになる
        if (e.OldValue == null && e.NewValue != null)
        {
            panel.ItemsManager.SaveDirectChildItems(panel.Children);
        }

        // 派生クラスがプロパティ変更を処理するための拡張ポイントを呼び出す
        panel.OnDependencyPropertyChangedInternal(e);
        // ItemsSource 変更専用の内部メソッドを呼び出し、アイテム再構築を行う
        panel.OnItemsSourceChangedInternal();
        // レイアウトを無効化して再計算をトリガーする
        panel.InvalidateMeasure();
    }

    /// <summary><b>ItemTemplate が変更されたときに呼び出されます</b></summary>
    private static void OnItemTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var panel = (DualSourceItemsPanel)d;

        // 派生クラスがプロパティ変更を処理するための拡張ポイントを呼び出す
        panel.OnDependencyPropertyChangedInternal(e);
        // ItemTemplate 変更専用の内部メソッドを呼び出し、アイテム再構築を行う
        panel.OnItemTemplateChangedInternal();
        // レイアウトを無効化して再計算をトリガーする
        panel.InvalidateMeasure();
    }

    /// <summary><b>ItemsSourceFirst が変更されたときに呼び出されます</b></summary>
    private static void OnItemsSourceFirstChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var panel = (DualSourceItemsPanel)d;

        // ItemsManager の ItemsSourceFirst プロパティを新しい値に更新する
        panel.ItemsManager.ItemsSourceFirst = (bool)e.NewValue;

        // 派生クラスがプロパティ変更を処理するための拡張ポイントを呼び出す
        panel.OnDependencyPropertyChangedInternal(e);
        // ItemsSourceFirst 変更専用の内部メソッドを呼び出し、アイテム再構築を行う
        panel.OnItemsSourceFirstChangedInternal();
        // レイアウトを無効化して再計算をトリガーする
        panel.InvalidateMeasure();
    }

    /// <summary><b>DependencyProperty が変更されたときに呼び出される内部メソッド</b></summary>
    protected virtual void OnDependencyPropertyChangedInternal(DependencyPropertyChangedEventArgs e)
    {
        // 派生クラスがオーバーライド可能
    }

    /// <summary><b>ItemsSource が変更されたときに呼び出される内部メソッド</b></summary>
    /// <remarks>
    /// 派生クラスでオーバーライド可能<br/>
    /// </remarks>
    protected virtual void OnItemsSourceChangedInternal()
    {
        // RebuildItems メソッドを呼び出して、ItemsSource と直接子要素を再構築する
        RebuildItems();
    }

    /// <summary><b>ItemTemplate が変更されたときに呼び出される内部メソッド</b></summary>
    /// <remarks>
    /// 派生クラスでオーバーライド可能<br/>
    /// </remarks>
    protected virtual void OnItemTemplateChangedInternal()
    {
        // RebuildItems メソッドを呼び出して、ItemsSource と直接子要素を再構築する
        RebuildItems();
    }

    /// <summary><b>ItemsSourceFirst が変更されたときに呼び出される内部メソッド</b></summary>
    /// <remarks>
    /// 派生クラスでオーバーライド可能<br/>
    /// </remarks>
    protected virtual void OnItemsSourceFirstChangedInternal()
    {
        // RebuildItems メソッドを呼び出して、ItemsSource と直接子要素を再構築する
        RebuildItems();
    }

    /// <summary><b>アイテムを再構築します</b></summary>
    protected void RebuildItems()
    {
        // ItemsSource から要素を生成し、直接子要素を結合して子要素を再構築
        ItemsManager.RebuildChildren(Children, ItemsSource, ItemTemplate);
    }

    /// <summary><b>パネルが UIツリーに追加されたときに呼び出されます</b></summary>
    private void OnPanelLoaded()
    {
        // 直接子要素が保存済みの場合、初回レイアウト計算をトリガー
        if (ItemsManager.HasDirectChildItemsStored)
        {
            RebuildItems();
        }
    }
}
