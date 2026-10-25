using Microsoft.UI.Xaml;
using System.Diagnostics;
using Windows.Foundation;

using EsUtil.Feature.ColumnPacking;
using static EsUtil.Feature.ColumnPacking.ColumnPacker;

namespace EsUtil.Ui.WinUI3.ColumnPacking;

/// <summary><b>垂直方向にアイテムを複数列配置し、高さを自動計算するカスタムパネル</b></summary>
/// <remarks>
/// Panel から継承し、ItemsSource と XAML で直接追加された子要素の両方を同時に扱う機能を提供するベースクラスです。<br/>
/// ItemsControl の ItemsPanel として 100% 置き換え可能な互換性を提供します。<br/>
/// <br/>
/// 【特徴】<br/>
/// • DualSourceItemsPanel から継承（ItemsControl の ItemsPanel として使用可能）<br/>
/// • ColumnPacker.Solve() の結果を直接反映<br/>
/// • 行間・列間スペースを制御可能な DependencyProperty<br/>
/// • 3 種類のアルゴリズム（DynamicProgramming/BinarySearch/Greedy）をサポート<br/>
/// • ベースクラスから継承したデバッグプロパティで実行状態をモニタリング可能<br/>
/// <br/>
/// 【ベースクラスのデバッグプロパティ】<br/>
/// • LastChildCount：最後のレイアウト計算時の子要素数<br/>
/// • IsManagerInitialized：ItemsManager が初期化済みかどうか<br/>
/// • IsItemsSourceConnected：ItemsSource と ItemTemplate が両方設定済みかどうか<br/>
/// • DirectChildItemsCount：直接保存された子要素の数<br/>
/// <br/>
/// 【計算フロー】<br/>
/// 1. MeasureOverride：各子要素を測定し、最適レイアウトを計算<br/>
/// 2. ArrangeOverride：計算結果に基づいて各子要素を配置<br/>
/// <br/>
/// 【最適化施策】<br/>
/// • Layout キャッシュで重複計算を削減し、ArrangeOverride での重複計算を回避<br/>
/// • ChildElements プロパティで LINQ Iterator なしの直接イテレーションを使用し、オブジェクト割り当てを削減<br/>
/// • FrameworkElement のみをフィルタリングして効率的な処理を実現<br/>
/// </remarks>
public partial class ColumnPackingPanel : DualSourceItemsPanel
{
    /// <summary><b>最後に計算したレイアウト結果</b></summary>
    /// <remarks>
    /// デバッグやモニタリング用に保持<br/>
    /// </remarks>
    protected ColumnPacker? Layout { get; set; }


    /// <summary><b>行間スペースを指定する DependencyProperty</b></summary>
    /// <remarks>
    /// デフォルト: 8.0<br/>
    /// </remarks>
    public static readonly DependencyProperty RowSpaceProperty = DependencyProperty.Register(
        nameof(RowSpace),
        typeof(double),
        typeof(ColumnPackingPanel),
        new PropertyMetadata(8.0, OnLayoutPropertyChanged));

    /// <summary><b>列間スペースを指定する DependencyProperty</b></summary>
    /// <remarks>
    /// デフォルト: 8.0<br/>
    /// </remarks>
    public static readonly DependencyProperty ColumnSpaceProperty = DependencyProperty.Register(
        nameof(ColumnSpace),
        typeof(double),
        typeof(ColumnPackingPanel),
        new PropertyMetadata(8.0, OnLayoutPropertyChanged));

    /// <summary><b>最大列数を指定する DependencyProperty</b></summary>
    /// <remarks>
    /// デフォルト: 10<br/>
    /// </remarks>
    public static readonly DependencyProperty ColumnLimitProperty = DependencyProperty.Register(
        nameof(ColumnLimit),
        typeof(int),
        typeof(ColumnPackingPanel),
        new PropertyMetadata(10, OnLayoutPropertyChanged));

    /// <summary><b>使用するアルゴリズムを指定する DependencyProperty</b></summary>
    /// <remarks>
    /// デフォルト: BinarySearch<br/>
    /// </remarks>
    public static readonly DependencyProperty MethodProperty = DependencyProperty.Register(
        nameof(Method),
        typeof(Method),
        typeof(ColumnPackingPanel),
        new PropertyMetadata(Method.BinarySearch, OnLayoutPropertyChanged));

    /// <summary><b>行間スペース（ピクセル）</b></summary>
    /// <remarks>
    /// デフォルト: 8.0<br/>
    /// </remarks>
    public double RowSpace
    {
        get => (double)GetValue(RowSpaceProperty);
        set => SetValue(RowSpaceProperty, value);
    }

    /// <summary><b>列間スペース（ピクセル）</b></summary>
    /// <remarks>
    /// デフォルト: 8.0<br/>
    /// </remarks>
    public double ColumnSpace
    {
        get => (double)GetValue(ColumnSpaceProperty);
        set => SetValue(ColumnSpaceProperty, value);
    }

    /// <summary><b>最大列数</b></summary>
    /// <remarks>
    /// デフォルト: 10<br/>
    /// </remarks>
    public int ColumnLimit
    {
        get => (int)GetValue(ColumnLimitProperty);
        set => SetValue(ColumnLimitProperty, value);
    }

    /// <summary><b>使用するアルゴリズム</b></summary>
    /// <remarks>
    /// デフォルト: BinarySearch<br/>
    /// </remarks>
    public Method Method
    {
        get => (Method)GetValue(MethodProperty);
        set => SetValue(MethodProperty, value);
    }

    /// <summary><b>DependencyProperty 変更時のコールバック</b></summary>
    /// <remarks>
    /// レイアウト再計算が必要な場合に InvalidateMeasure() を呼び出す<br/>
    /// </remarks>
    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var panel = (ColumnPackingPanel)d;
        // レイアウトキャッシュを無効化
        panel.Layout = null;
        // DependencyProperty も null にリセット
        panel.UpdateLayoutResultProperties();
        panel.InvalidateMeasure();
    }


    /// <summary><b>使用された幅を指定する DependencyProperty</b></summary>
    /// <remarks>
    /// Layout.Solve() の結果から自動更新されます。未計算時は null。<br/>
    /// </remarks>
    public static readonly DependencyProperty LayoutUsedWidthProperty = DependencyProperty.Register(
        nameof(LayoutUsedWidth),
        typeof(double?),
        typeof(ColumnPackingPanel),
        new PropertyMetadata(null));

    /// <summary><b>最小高さを指定する DependencyProperty</b></summary>
    /// <remarks>
    /// Layout.Solve() の結果から自動更新されます。未計算時は null。<br/>
    /// </remarks>
    public static readonly DependencyProperty LayoutMinHeightProperty = DependencyProperty.Register(
        nameof(LayoutMinHeight),
        typeof(double?),
        typeof(ColumnPackingPanel),
        new PropertyMetadata(null));

    /// <summary><b>計算されたレイアウトの列数を指定する DependencyProperty</b></summary>
    /// <remarks>
    /// Layout.GetLastColumnSegments() の長さから自動更新されます。未計算時は null。<br/>
    /// </remarks>
    public static readonly DependencyProperty LayoutColumnCountProperty = DependencyProperty.Register(
        nameof(LayoutColumnCount),
        typeof(int?),
        typeof(ColumnPackingPanel),
        new PropertyMetadata(null));

    /// <summary><b>計算されたレイアウトの列セグメント情報を指定する DependencyProperty</b></summary>
    /// <remarks>
    /// Layout.GetLastColumnSegments() の結果から自動更新されます。未計算時は null。<br/>
    /// </remarks>
    public static readonly DependencyProperty LayoutColumnSegmentsProperty = DependencyProperty.Register(
        nameof(LayoutColumnSegments),
        typeof(string),
        typeof(ColumnPackingPanel),
        new PropertyMetadata(null));

    /// <summary><b>使用された幅（ピクセル）</b></summary>
    /// <remarks>
    /// Layout.Solve() の結果から自動更新されます。未計算時は null。<br/>
    /// </remarks>
    public double? LayoutUsedWidth
    {
        get => (double?)GetValue(LayoutUsedWidthProperty);
        private set => SetValue(LayoutUsedWidthProperty, value);
    }

    /// <summary><b>最小高さ（ピクセル）</b></summary>
    /// <remarks>
    /// Layout.Solve() の結果から自動更新されます。未計算時は null。<br/>
    /// </remarks>
    public double? LayoutMinHeight
    {
        get => (double?)GetValue(LayoutMinHeightProperty);
        private set => SetValue(LayoutMinHeightProperty, value);
    }

    /// <summary><b>計算されたレイアウトの列数</b></summary>
    /// <remarks>
    /// Layout.GetLastColumnSegments() の長さから自動更新されます。未計算時は null。<br/>
    /// </remarks>
    public int? LayoutColumnCount
    {
        get => (int?)GetValue(LayoutColumnCountProperty);
        private set => SetValue(LayoutColumnCountProperty, value);
    }

    /// <summary><b>計算されたレイアウトの列セグメント情報</b></summary>
    /// <remarks>
    /// Layout.GetLastColumnSegments() の結果から自動更新されます。未計算時は null。<br/>
    /// </remarks>
    public string? LayoutColumnSegments
    {
        get => (string?)GetValue(LayoutColumnSegmentsProperty);
        private set => SetValue(LayoutColumnSegmentsProperty, value);
    }

    /// <summary><b>Layout の結果を DependencyProperty に反映します</b></summary>
    /// <remarks>
    /// MeasureOverride から呼び出されます。<br/>
    /// </remarks>
    private void UpdateLayoutResultProperties()
    {
        if (Layout is null)
        {
            LayoutUsedWidth = null;
            LayoutMinHeight = null;
            LayoutColumnCount = null;
            LayoutColumnSegments = null;
        }
        else
        {
            var result = Layout.GetLastResult();
            LayoutUsedWidth = result.UsedWidth;
            LayoutMinHeight = result.MinHeight;

            var segments = Layout.GetLastColumnSegments();
            LayoutColumnCount = segments.Length;
            LayoutColumnSegments = "[" + string.Join(",", segments.Select(seg => seg.EndIdx)) + "]";
        }
    }


    /// <summary><b>DependencyProperty が変更されたときに呼び出されます</b></summary>
    /// <remarks>
    /// ベースクラスのイベントを受け取り、カスタム処理を実行できます。<br/>
    /// </remarks>
    protected override void OnDependencyPropertyChangedInternal(DependencyPropertyChangedEventArgs e)
    {
        base.OnDependencyPropertyChangedInternal(e);

        // 派生クラスで全プロパティ変更を監視する場合
        // if (e.Property == ItemsSourceProperty) { ... }
        // if (e.Property == ItemTemplateProperty) { ... }
        // if (e.Property == ItemsSourceFirstProperty) { ... }
    }

    /// <summary><b>ItemsSource が変更されたときに呼び出されます</b></summary>
    /// <remarks>
    /// ベースクラスの RebuildItems() 後に実行されます。<br/>
    /// </remarks>
    protected override void OnItemsSourceChangedInternal()
    {
        base.OnItemsSourceChangedInternal();

        // Layout キャッシュを無効化
        Layout = null;
        // DependencyProperty もリセット
        UpdateLayoutResultProperties();
    }

    /// <summary><b>ItemTemplate が変更されたときに呼び出されます</b></summary>
    /// <remarks>
    /// ベースクラスの RebuildItems() 後に実行されます。<br/>
    /// </remarks>
    protected override void OnItemTemplateChangedInternal()
    {
        base.OnItemTemplateChangedInternal();

        // Layout キャッシュを無効化
        Layout = null;
        // DependencyProperty もリセット
        UpdateLayoutResultProperties();
    }

    /// <summary><b>ItemsSourceFirst が変更されたときに呼び出されます</b></summary>
    /// <remarks>
    /// ベースクラスの RebuildItems() 後に実行されます。<br/>
    /// </remarks>
    protected override void OnItemsSourceFirstChangedInternal()
    {
        base.OnItemsSourceFirstChangedInternal();

        // Layout キャッシュを無効化
        Layout = null;
        // DependencyProperty もリセット
        UpdateLayoutResultProperties();
    }


    /// <summary><b>測定をオーバーライドします</b></summary>
    /// <remarks>
    /// 【処理フロー】<br/>
    /// 1. 子要素がない場合：(0, 0) を返す<br/>
    /// 2. 各子要素を無限サイズで測定（自然サイズ取得）<br/>
    /// 3. ColumnPacking.Solve() で最適レイアウトを計算<br/>
    /// 4. 計算結果から必要なサイズ（MinHeight, UsedWidth）を返す<br/>
    /// <br/>
    /// 【統合キャッシング】<br/>
    /// Layout キャッシュで重複計算を削減<br/>
    /// ArrangeOverride での重複計算を回避<br/>
    /// </remarks>
    /// <param name="availableSize">利用可能なサイズ</param>
    /// <returns>パネルが必要とするサイズ</returns>
    protected override Size MeasureOverride(Size availableSize)
    {
        // 子要素がない場合
        if (Children.Count == 0)
            return new Size(0, 0);

        // 子要素を抽出
        var childElements = ChildElements;
        if (childElements.Count == 0)
            return new Size(0, 0);

        // アイテムサイズを構築
        var inf = new Size(double.PositiveInfinity, double.PositiveInfinity);
        foreach (var el in childElements) el.Measure(inf);
        var items = childElements.Select(e => (e.DesiredSize.Width, e.DesiredSize.Height)).ToArray();

        // ColumnPacking インスタンスを作成
        Layout ??= new ColumnPacker(items, (RowSpace, ColumnSpace), ColumnLimit);
        Layout.CurrentMethod = Method;

        // 利用可能な幅を決定する。
        double widthLimit = double.IsInfinity(availableSize.Width)
            ? double.PositiveInfinity
            : availableSize.Width;

        // ColumnPacking で最適レイアウトを計算
        var (UsedWidth, MinHeight) = Layout.Solve(widthLimit);
        Debug.WriteLine($"(UsedWidth, MinHeight): ({UsedWidth}, {MinHeight})");

        // Layout 計算結果を DependencyProperty に反映
        UpdateLayoutResultProperties();

        // 必要なサイズを返す
        return new Size(UsedWidth, MinHeight);
    }

    /// <summary><b>配置をオーバーライドします</b></summary>
    /// <remarks>
    /// 【処理フロー】<br/>
    /// 1. 子要素がない場合：終了<br/>
    /// 2. MeasureOverride で計算済みのキャッシュを活用<br/>
    /// 3. GetItemLayouts() で各子要素の座標を取得<br/>
    /// 4. Arrange() で各要素を配置<br/>
    /// </remarks>
    /// <param name="finalSize">最終的に割り当てられたサイズ</param>
    /// <returns>実際に使用したサイズ</returns>
    protected override Size ArrangeOverride(Size finalSize)
    {
        // 子要素がない場合またはレイアウト結果が無効な場合
        if (Children.Count == 0 || Layout is null)
            return finalSize;

        // 計算結果から各子要素の座標とサイズを取得
        var childElements = ChildElements;
        var itemLayouts = Layout.GetLastItemLayouts();
        Debug.WriteLine($"LayoutColumnSegments: {LayoutColumnSegments}");
        Debug.WriteLine($"Layout.Y: [{string.Join(",", itemLayouts.Select(s => s.Y))}]");

        // Arrange() で各要素を配置
        int layoutIndex = 0;
        foreach (var layout in itemLayouts)
        {
            if (layoutIndex >= childElements.Count)
                break;

            childElements[layoutIndex].Arrange(new Rect(layout.X, layout.Y, layout.Width, layout.Height));
            layoutIndex++;
        }

        return finalSize;
    }

    /// <summary><b>子要素を抽出します（LINQ Iterator なし）</b></summary>
    /// <returns>フィルタリングされた FrameworkElement のリスト</returns>
    private List<FrameworkElement> ChildElements => [.. Children.OfType<FrameworkElement>()];

}
