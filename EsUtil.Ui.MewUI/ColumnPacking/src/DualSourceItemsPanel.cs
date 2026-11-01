using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

namespace EsUtil.Ui.MewUI.ColumnPacking;

/// <summary><b>ItemsSource と直接子要素の両方を管理するパネルベースクラス</b></summary>
/// <remarks>
/// Panel から継承し、ItemsSource とコードで直接追加された子要素の両方を同時に扱う機能を提供するベースクラスです。<br/>
/// ItemsControl の ItemsPanel として置き換え可能な互換性を提供します。
/// <br/>
/// 【特徴】<br/>
/// • DualSourceItemsManager を内包し、状態を一元管理<br/>
/// • ItemTemplate による要素生成をサポート<br/>
/// • ItemsSourceFirst プロパティで配置順序を制御<br/>
/// • プロパティ システム（<see cref="MewProperty{T}"/>）でバインディング対応<br/>
/// • 派生クラスは MeasureContent/ArrangeContent に専念可能<br/>
/// • 派生クラスがプロパティ変更をオーバーライド可能<br/>
/// <br/>
/// 【設計原則】<br/>
/// • DualSourceItemsManager が唯一の真実の源（Single Source of Truth）<br/>
/// • DualSourceItemsPanel は UI プロパティのプロキシに徹する<br/>
/// • 同期ポイントはプロパティ変更の通知のみ<br/>
/// <br/>
/// 【拡張ポイント】<br/>
/// 派生クラスは以下の仮想メソッドをオーバーライドして、プロパティの変更を受け取ります:<br/>
/// • <see cref="OnItemsSourceChangedInternal"/> - ItemsSource 変更時<br/>
/// • <see cref="OnItemTemplateChangedInternal"/> - ItemTemplate 変更時<br/>
/// • <see cref="OnItemsSourceFirstChangedInternal"/> - ItemsSourceFirst 変更時<br/>
/// • <see cref="OnPropertyChangedInternal"/> - すべてのプロパティ変更<br/>
/// </remarks>
public abstract class DualSourceItemsPanel : Panel
{
    /// <summary><b>ItemsSource プロパティ</b></summary>
    /// <remarks>
    /// このプロパティへ設定したコレクションから要素を生成します（<see cref="ItemTemplateProperty"/> と両方が必要）。
    /// </remarks>
    public static readonly MewProperty<object?> ItemsSourceProperty =
        MewProperty<object?>.Register<DualSourceItemsPanel>(
            nameof(ItemsSource), null, MewPropertyOptions.AffectsLayout,
            changed: static (panel, oldValue, newValue) => panel.OnItemsSourcePropertyChanged(oldValue, newValue));

    /// <summary><b>ItemTemplate プロパティ</b></summary>
    /// <remarks>
    /// 各アイテムの表示テンプレートを指定します（<see cref="ItemsSourceProperty"/> と両方が必要）。
    /// </remarks>
    public static readonly MewProperty<IDataTemplate?> ItemTemplateProperty =
        MewProperty<IDataTemplate?>.Register<DualSourceItemsPanel>(
            nameof(ItemTemplate), null, MewPropertyOptions.AffectsLayout,
            changed: static (panel, oldValue, newValue) => panel.OnItemTemplatePropertyChanged(oldValue, newValue));

    /// <summary><b>ItemsSourceFirst プロパティ</b></summary>
    /// <remarks>
    /// ItemsSource から作った要素を先に配置するかどうかを指定します（既定は true）。
    /// </remarks>
    public static readonly MewProperty<bool> ItemsSourceFirstProperty =
        MewProperty<bool>.Register<DualSourceItemsPanel>(
            nameof(ItemsSourceFirst), true, MewPropertyOptions.AffectsLayout,
            changed: static (panel, oldValue, newValue) => panel.OnItemsSourceFirstPropertyChanged(oldValue, newValue));

    /// <summary><see cref="SetItems"/> で渡された項目（null は未設定）。</summary>
    /// <remarks>
    /// <b>プロパティを介さない経路</b>である: コレクションをそのまま保持するため、<br/>
    /// プロパティ システムの型変換（バインディング）を通さずにデータ駆動の子を並べられる。
    /// </remarks>
    private IReadOnlyList<object?>? _items;

    /// <summary>ItemsSource と直接の子を管理する（派生クラスから使う）。</summary>
    protected DualSourceItemsManager ItemsManager { get; } = new();

    /// <summary><b>ItemsManager が初期化済みかどうかを取得します</b></summary>
    /// <value>直接の子を保存済みの場合は true。</value>
    /// <remarks>
    /// ItemsSource が最初に設定され、直接子要素が保存されたかどうかを判定します。<br/>
    /// true の場合、ItemsSource と直接子要素の管理が開始されています。<br/>
    /// </remarks>
    public bool IsManagerInitialized => ItemsManager.HasDirectChildItemsStored;

    /// <summary><b>ItemsSource と ItemTemplate が両方設定されているかどうかを取得します</b></summary>
    /// <value>両方が設定されている場合は true。</value>
    /// <remarks>
    /// 【用途】<br/>
    /// ItemsPanel として正しく機能するために必要な条件（ItemsSource と ItemTemplate が設定済み）を確認するために使用します。<br/>
    /// </remarks>
    public bool IsItemsSourceConnected => ItemsSource is not null && ItemTemplate is not null;

    /// <summary><b>直接保存された子要素の数を取得します</b></summary>
    /// <value>保存した直接の子の件数。</value>
    /// <remarks>
    /// ItemsSource と直接子要素の分離管理状況を確認するために使用します。<br/>
    /// 0 より大きい値の場合、コードで直接定義された子要素が存在します。<br/>
    /// </remarks>
    public int DirectChildItemsCount => ItemsManager.DirectChildItems.Count;

    /// <summary><b>ItemsSource プロパティ</b></summary>
    /// <value>アイテムのソースとなるオブジェクト。 null の場合は要素を作らない。</value>
    /// <remarks>
    /// アイテムのソースとなるオブジェクトを取得または設定します。<br/>
    /// <see cref="ItemTemplate"/> と両方が設定されているときだけ要素を作ります。
    /// </remarks>
    public object? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary><b>ItemTemplate プロパティ</b></summary>
    /// <value>アイテムのデータ テンプレート。 null の場合は要素を作らない。</value>
    /// <remarks>
    /// アイテムのデータ テンプレートを取得または設定します。<br/>
    /// <see cref="ItemsSource"/> と両方が設定されているときだけ要素を作ります。
    /// </remarks>
    public IDataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <summary><b>ItemsSourceFirst プロパティ</b></summary>
    /// <value>ItemsSource の要素を先に置く場合は true。 既定は true。</value>
    /// <remarks>
    /// ItemsSource を先に配置するか、直接子要素を先に配置するかを取得または設定します。<br/>
    /// <br/>
    /// 【注意】<br/>
    /// このプロパティはプロパティ システムが値を保持し、<see cref="DualSourceItemsManager"/> へは変更通知で伝搬します。<br/>
    /// マネージャーを直接書き換えると並び順が再構築されないため、必ず本プロパティを経由してください。<br/>
    /// </remarks>
    public bool ItemsSourceFirst
    {
        get => GetValue(ItemsSourceFirstProperty);
        set => SetValue(ItemsSourceFirstProperty, value);
    }

    /// <summary>データから作る子の元になる項目を設定する（コレクションをそのまま渡す入口）。</summary>
    /// <param name="items">項目の並び。 null を指定すると <see cref="ItemsSource"/> 経由へ戻す。</param>
    /// <remarks>
    /// <b>バインディングが要らない場合はこちらを使う</b>: <see cref="ItemsSource"/> はプロパティ システムの値であり、<br/>
    /// 値を書き換えるたびに子を作り直します。 本メソッドは managed のリストをそのまま保持し、呼んだときにだけ作り直します。<br/>
    /// 要素の生成には <see cref="ItemTemplate"/> を使います（設定順序は問いません）。 呼ぶと子を再構築する<br/>
    /// （直接の子は初回に保存され、以後 <see cref="ItemsSourceFirst"/> の順序で並びます）。
    /// </remarks>
    public void SetItems(IEnumerable<object?>? items)
    {
        _items = items is null ? null : [.. items];
        RebuildItems();
        InvalidateMeasure();
    }

    /// <summary>項目を再構築します</summary>
    /// <remarks>
    /// <see cref="SetItems"/> で渡された項目がある場合はそちらを使います。<br/>
    /// 無い場合は <c>ItemsSource</c>（プロパティ）から作ります。
    /// </remarks>
    protected void RebuildItems()
    {
        if (_items is { } items)
        {
            // プロパティを経由せず、保持しているリストから要素を作る
            ItemsManager.SaveDirectChildItems(Children);
            ItemsManager.RebuildChildren(this, ItemsManager.GenerateElements(items, ItemTemplate));
            return;
        }

        // ItemsSource から要素を生成し、直接子要素を結合して子要素を再構築する
        ItemsManager.RebuildChildren(this, ItemsSource, ItemTemplate);
    }

    /// <summary><b>プロパティが変更されたときに呼び出されます</b></summary>
    /// <param name="changedProperty">変更されたプロパティ。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    /// <remarks>
    /// 派生クラスがオーバーライド可能です。<br/>
    /// MewUI のプロパティ変更通知は変更引数の型を持たないため、プロパティと前後の値で渡します。
    /// </remarks>
    protected virtual void OnPropertyChangedInternal(MewProperty changedProperty, object? oldValue, object? newValue)
    {
        // 派生クラスがオーバーライド可能
    }

    /// <summary><b>ItemsSource が変更されたときに呼び出されます</b></summary>
    /// <remarks>
    /// 派生クラスでオーバーライド可能です。
    /// </remarks>
    protected virtual void OnItemsSourceChangedInternal()
    {
        // ItemsSource と直接子要素を再構築する
        RebuildItems();
    }

    /// <summary><b>ItemTemplate が変更されたときに呼び出されます</b></summary>
    /// <remarks>
    /// 派生クラスでオーバーライド可能です。
    /// </remarks>
    protected virtual void OnItemTemplateChangedInternal()
    {
        // ItemsSource と直接子要素を再構築する
        RebuildItems();
    }

    /// <summary><b>ItemsSourceFirst が変更されたときに呼び出されます</b></summary>
    /// <remarks>
    /// 派生クラスでオーバーライド可能です。
    /// </remarks>
    protected virtual void OnItemsSourceFirstChangedInternal()
    {
        // 並び順を変えて子要素を再構築する
        RebuildItems();
    }

    /// <summary>ItemsSource の変更を処理する。</summary>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    private void OnItemsSourcePropertyChanged(object? oldValue, object? newValue)
    {
        // 初めて ItemsSource が設定されたとき、その時点の子を「直接の子」として保存する
        // （以降、ItemsSource から作る要素と直接の子を並べて管理する）
        if (oldValue is null && newValue is not null)
        {
            ItemsManager.SaveDirectChildItems(Children);
        }

        OnPropertyChangedInternal(ItemsSourceProperty, oldValue, newValue);
        OnItemsSourceChangedInternal();
        InvalidateMeasure();
    }

    /// <summary>ItemTemplate の変更を処理する。</summary>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    private void OnItemTemplatePropertyChanged(IDataTemplate? oldValue, IDataTemplate? newValue)
    {
        OnPropertyChangedInternal(ItemTemplateProperty, oldValue, newValue);
        OnItemTemplateChangedInternal();
        InvalidateMeasure();
    }

    /// <summary>ItemsSourceFirst の変更を処理する。</summary>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    private void OnItemsSourceFirstPropertyChanged(bool oldValue, bool newValue)
    {
        // マネージャーの並び順を新しい値へ更新してから作り直す
        ItemsManager.ItemsSourceFirst = newValue;
        OnPropertyChangedInternal(ItemsSourceFirstProperty, oldValue, newValue);
        OnItemsSourceFirstChangedInternal();
        InvalidateMeasure();
    }
}
