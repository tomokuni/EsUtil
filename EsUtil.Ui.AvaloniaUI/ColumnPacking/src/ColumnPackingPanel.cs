using Avalonia;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using static EsUtil.Feature.ColumnPacking.ColumnPacker;

namespace EsUtil.Ui.AvaloniaUI.ColumnPacking;

/// <summary><b>垂直方向にアイテムを複数列配置し、高さを自動計算するカスタムパネル</b></summary>
/// <remarks>
/// Panel から継承し、ItemsSource と XAML で直接追加された子要素の両方を同時に扱う機能を提供するベースクラスです。<br/>
/// ItemsControl の ItemsPanel として置き換え可能な互換性を提供します。
/// <br/>
/// 【特徴】<br/>
/// • DualSourceItemsPanel から継承（ItemsControl の ItemsPanel として使用可能）<br/>
/// • ColumnPacker.Solve() の結果を直接反映<br/>
/// • 行間・列間スペースを制御可能な依存関係プロパティ<br/>
/// • 3 種類のアルゴリズム（DynamicProgramming/BinarySearch/Greedy）をサポート<br/>
/// • レイアウト結果を読み取り専用の依存関係プロパティで公開<br/>
/// <br/>
/// 【計算フロー】<br/>
/// 1. MeasureOverride：各子要素を測定し、最適レイアウトを計算<br/>
/// 2. ArrangeOverride：計算結果に基づいて各子要素を配置<br/>
/// <br/>
/// 【最適化施策】<br/>
/// • Layout キャッシュで重複計算を削減し、ArrangeOverride での重複計算を回避<br/>
/// • 子要素の一覧を 1 度だけ取り出して反復し、列挙の割り当てを削減
/// </remarks>
public class ColumnPackingPanel : DualSourceItemsPanel
{
    /// <summary><b>行間スペースを指定する依存関係プロパティ</b></summary>
    /// <remarks>
    /// デフォルト: 8.0 DIP<br/>
    /// </remarks>
    public static readonly StyledProperty<double> RowSpaceProperty =
        AvaloniaProperty.Register<ColumnPackingPanel, double>(nameof(RowSpace), 8.0);

    /// <summary><b>列間スペースを指定する依存関係プロパティ</b></summary>
    /// <remarks>
    /// デフォルト: 8.0 DIP<br/>
    /// </remarks>
    public static readonly StyledProperty<double> ColumnSpaceProperty =
        AvaloniaProperty.Register<ColumnPackingPanel, double>(nameof(ColumnSpace), 8.0);

    /// <summary><b>最大列数を指定する依存関係プロパティ</b></summary>
    /// <remarks>
    /// デフォルト: 10<br/>
    /// </remarks>
    public static readonly StyledProperty<int> ColumnLimitProperty =
        AvaloniaProperty.Register<ColumnPackingPanel, int>(nameof(ColumnLimit), 10);

    /// <summary><b>使用するアルゴリズムを指定する依存関係プロパティ</b></summary>
    /// <remarks>
    /// デフォルト: BinarySearch<br/>
    /// </remarks>
    public static readonly StyledProperty<Method> MethodProperty =
        AvaloniaProperty.Register<ColumnPackingPanel, Method>(nameof(Method), Method.BinarySearch);

    /// <summary><b>使用された幅を公開する読み取り専用プロパティ</b></summary>
    private static readonly DirectProperty<ColumnPackingPanel, double?> LayoutUsedWidthProperty =
        AvaloniaProperty.RegisterDirect<ColumnPackingPanel, double?>(nameof(LayoutUsedWidth), static panel => panel.LayoutUsedWidth);

    /// <summary><b>最小高さを公開する読み取り専用プロパティ</b></summary>
    private static readonly DirectProperty<ColumnPackingPanel, double?> LayoutMinHeightProperty =
        AvaloniaProperty.RegisterDirect<ColumnPackingPanel, double?>(nameof(LayoutMinHeight), static panel => panel.LayoutMinHeight);

    /// <summary><b>計算されたレイアウトの列数を公開する読み取り専用プロパティ</b></summary>
    private static readonly DirectProperty<ColumnPackingPanel, int?> LayoutColumnCountProperty =
        AvaloniaProperty.RegisterDirect<ColumnPackingPanel, int?>(nameof(LayoutColumnCount), static panel => panel.LayoutColumnCount);

    /// <summary><b>計算されたレイアウトの列セグメント情報を公開する読み取り専用プロパティ</b></summary>
    private static readonly DirectProperty<ColumnPackingPanel, string?> LayoutColumnSegmentsProperty =
        AvaloniaProperty.RegisterDirect<ColumnPackingPanel, string?>(nameof(LayoutColumnSegments), static panel => panel.LayoutColumnSegments);

    /// <summary><see cref="LayoutUsedWidth"/> の保持先。</summary>
    private double? _layoutUsedWidth;

    /// <summary><see cref="LayoutMinHeight"/> の保持先。</summary>
    private double? _layoutMinHeight;

    /// <summary><see cref="LayoutColumnCount"/> の保持先。</summary>
    private int? _layoutColumnCount;

    /// <summary><see cref="LayoutColumnSegments"/> の保持先。</summary>
    private string? _layoutColumnSegments;

    /// <summary>プロパティの変更を記録するロガー（未設定は <see cref="NullLogger"/>）。</summary>
    private ILogger _log = NullLogger.Instance;

    /// <summary>ログの接頭辞（未設定は null。 既定値は定義クラスが持つ）。</summary>
    private string? _logPrefix;

    /// <summary>生成の記録を出力済みかどうか（ロガーの初回設定で 1 度だけ出力する）。</summary>
    private bool _createdLogged;

    /// <summary>直近に記録したレイアウト結果の署名（同じ結果を繰り返し記録しないための比較キー）。</summary>
    private string? _lastLayoutSignature;

    /// <summary>直前に確定した幅の上限（DIP。 0 は未確定）。</summary>
    /// <remarks>
    /// <b>幅が確定しないパスがあるため保持する</b>: <c>ScrollViewer</c> は内容を「無限大の幅」と<br/>
    /// 「表示領域の幅」の両方で測ることがあり、無限大のパスで別の結果を返すと 2 つのレイアウトが交互に現れて確定しない。<br/>
    /// 無限大・0 以下のときは直前の幅を使う（どの UI でも同じ結果になるようにする）。
    /// </remarks>
    private double _lastWidthLimit;

    /// <summary><b>最後に計算したレイアウト結果</b></summary>
    /// <value>直近の計算のインスタンス。 未計算・無効化済みの間は null。</value>
    /// <remarks>
    /// 配置の計算は幅の上限で変わるため、子・幅・設定のいずれかが変わったら破棄する。<br/>
    /// 派生クラスがレイアウト結果を読むための入口である。
    /// </remarks>
    protected ColumnPacker? Layout { get; set; }

    /// <summary><b>行間スペース（DIP）</b></summary>
    /// <value>行の間隔。 既定は 8.0 DIP。</value>
    public double RowSpace
    {
        get => GetValue(RowSpaceProperty);
        set => SetValue(RowSpaceProperty, value);
    }

    /// <summary><b>列間スペース（DIP）</b></summary>
    /// <value>列の間隔。 既定は 8.0 DIP。</value>
    public double ColumnSpace
    {
        get => GetValue(ColumnSpaceProperty);
        set => SetValue(ColumnSpaceProperty, value);
    }

    /// <summary><b>最大列数</b></summary>
    /// <value>列数の上限。 既定は 10。</value>
    public int ColumnLimit
    {
        get => GetValue(ColumnLimitProperty);
        set => SetValue(ColumnLimitProperty, value);
    }

    /// <summary><b>使用するアルゴリズム</b></summary>
    /// <value>配置の算法。 既定は <see cref="Method.BinarySearch"/>。</value>
    public Method Method
    {
        get => GetValue(MethodProperty);
        set => SetValue(MethodProperty, value);
    }

    /// <summary><b>使用された幅（DIP）</b></summary>
    /// <value>直前の配置で使った幅。 未計算の間は null。</value>
    /// <remarks>
    /// 配置の計算（<c>ColumnPacker.Solve</c>）の結果から自動更新されます。 読み取り専用です。
    /// </remarks>
    public double? LayoutUsedWidth
    {
        get => _layoutUsedWidth;
        private set => SetAndRaise(LayoutUsedWidthProperty, ref _layoutUsedWidth, value);
    }

    /// <summary><b>最小高さ（DIP）</b></summary>
    /// <value>直前の配置の最大列高さ（最小化された値）。 未計算の間は null。</value>
    /// <remarks>
    /// 配置の計算（<c>ColumnPacker.Solve</c>）の結果から自動更新されます。 読み取り専用です。
    /// </remarks>
    public double? LayoutMinHeight
    {
        get => _layoutMinHeight;
        private set => SetAndRaise(LayoutMinHeightProperty, ref _layoutMinHeight, value);
    }

    /// <summary><b>計算されたレイアウトの列数</b></summary>
    /// <value>直前の配置の列数。 未計算の間は null。</value>
    /// <remarks>
    /// 配置の計算の列セグメントの長さから自動更新されます。 読み取り専用です。
    /// </remarks>
    public int? LayoutColumnCount
    {
        get => _layoutColumnCount;
        private set => SetAndRaise(LayoutColumnCountProperty, ref _layoutColumnCount, value);
    }

    /// <summary><b>計算されたレイアウトの列セグメント情報</b></summary>
    /// <value>直前の配置の列の区切り（<c>[3,6,9]</c> 形式）。 未計算の間は null。</value>
    /// <remarks>
    /// 配置の計算の列セグメントから自動更新されます。 読み取り専用です。
    /// </remarks>
    public string? LayoutColumnSegments
    {
        get => _layoutColumnSegments;
        private set => SetAndRaise(LayoutColumnSegmentsProperty, ref _layoutColumnSegments, value);
    }

    /// <summary>ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。 null を設定すると <see cref="NullLogger"/> になる（出力なし）。</value>
    /// <remarks>
    /// <b>頻度でレベルを分ける</b>: プロパティの変更は <c>Information</c>、レイアウト結果の変化は <c>Debug</c>、<br/>
    /// 項目ごとの座標は <c>Trace</c> で出力する（<c>.github/ログ規約.md</c> §2）。<br/>
    /// 初回の設定時に生成（初期設定の値）を 1 度だけ記録する。
    /// </remarks>
    public ILogger Log
    {
        get => _log;
        set
        {
            _log = value ?? NullLogger.Instance;
            LogFacadeCreatedOnce();
        }
    }

    /// <summary>ログの接頭辞を取得または設定する（ロガーと一緒に設定する）。</summary>
    /// <value>接頭辞（例: <c>[ColumnPacking]</c>）。 null または空白を設定すると既定値へ戻す。</value>
    /// <remarks>接頭辞は本層のログ定義（<see cref="ColumnPackingLogMessage"/>）が保持する（アセンブリで 1 つ）。</remarks>
    public string? LogPrefix
    {
        get => _logPrefix;
        set
        {
            _logPrefix = value;
            ColumnPackingLogMessage.Prefix = string.IsNullOrWhiteSpace(value) ? ColumnPackingLogMessage.DefaultPrefix : value;
        }
    }

    /// <summary><b>測定をオーバーライドします</b></summary>
    /// <remarks>
    /// 【処理フロー】<br/>
    /// 1. 子要素がない場合は (0, 0) を返す<br/>
    /// 2. 各子要素を無限サイズで測定（自然サイズ取得）<br/>
    /// 3. ColumnPacker.Solve() で最適レイアウトを計算<br/>
    /// 4. 計算結果から必要なサイズ（MinHeight, UsedWidth）を返す<br/>
    /// <br/>
    /// 【幅の扱い】<br/>
    /// 幅の上限が無限大の場合は列数の制約が無いため、1 列（<c>SolveSingleColumnLayout</c>）として扱う。<br/>
    /// 幅が 0 以下の間（レイアウトの最初のパス）は計算せず (0, 0) を返し、次のパスで計算する。
    /// </remarks>
    /// <param name="availableSize">利用可能なサイズ</param>
    /// <returns>パネルが必要とするサイズ</returns>
    protected override Size MeasureOverride(Size availableSize)
    {
        // 子要素がない場合は何も配置しない
        if (Children.Count == 0)
        {
            return default;
        }

        // 子要素を無限サイズで測定して自然サイズを取得する
        var childElements = Children;
        var infinity = new Size(double.PositiveInfinity, double.PositiveInfinity);
        for (var index = 0; index < childElements.Count; index++)
        {
            childElements[index].Measure(infinity);
        }

        // 測定結果からアイテムサイズを構築する
        var items = new (double Width, double Height)[childElements.Count];
        for (var index = 0; index < childElements.Count; index++)
        {
            var desiredSize = childElements[index].DesiredSize;
            items[index] = (desiredSize.Width, desiredSize.Height);
        }

        // ColumnPacking インスタンスを作成し、使用する算法を反映する
        Layout ??= new ColumnPacker(items, (RowSpace, ColumnSpace), ColumnLimit);
        Layout.CurrentMethod = Method;

        // 幅の上限を決める。 無限大・0 以下（幅が未確定のパス）は直前に確定した幅を使う
        var widthLimit = ResolveWidthLimit(availableSize.Width);

        // 幅がまだ分からない場合は列数の制約が無いため 1 列として扱う（幅の制約が無いホスト向け）
        if (!(widthLimit > 0))
        {
            var (singleUsedWidth, singleMinHeight) = Layout.SolveSingleColumnLayout();
            UpdateLayoutResultProperties();
            LogLayoutResultIfChanged([.. Layout.GetLastItemLayouts().Select(static layout => layout.Y)]);
            return new Size(singleUsedWidth, singleMinHeight);
        }

        _lastWidthLimit = widthLimit;

        // ColumnPacking で最適レイアウトを計算する
        var (usedWidth, minHeight) = Layout.Solve(widthLimit);

        // Layout 計算結果を依存関係プロパティへ反映する
        UpdateLayoutResultProperties();

        // レイアウト結果が変化したときだけ記録する（Debug。 項目ごとの座標は Trace）
        LogLayoutResultIfChanged([.. Layout.GetLastItemLayouts().Select(static layout => layout.Y)]);

        // 必要なサイズを返す
        return new Size(usedWidth, minHeight);
    }

    /// <summary><b>配置をオーバーライドします</b></summary>
    /// <remarks>
    /// 【処理フロー】<br/>
    /// 1. 子要素がない場合は終了<br/>
    /// 2. MeasureOverride で計算済みの結果をそのまま使う<br/>
    /// 3. GetLastItemLayouts() で各子要素の座標を取得し、Arrange() で配置する<br/>
    /// </remarks>
    /// <param name="finalSize">最終的に割り当てられたサイズ</param>
    /// <returns>実際に使用したサイズ</returns>
    protected override Size ArrangeOverride(Size finalSize)
    {
        // 子要素がない場合またはレイアウト結果が無効な場合は何も配置しない
        if (Children.Count == 0 || Layout is null)
        {
            return finalSize;
        }

        // 計算結果から各子要素の座標とサイズを取得して配置する
        var childElements = Children;
        var itemLayouts = Layout.GetLastItemLayouts();
        for (var index = 0; index < itemLayouts.Count && index < childElements.Count; index++)
        {
            var layout = itemLayouts[index];
            childElements[index].Arrange(new Rect(layout.X, layout.Y, layout.Width, layout.Height));
        }

        return finalSize;
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // 配置に効くプロパティが変わったら、計算結果を捨てて測り直す
        if (change.Property == RowSpaceProperty
            || change.Property == ColumnSpaceProperty
            || change.Property == ColumnLimitProperty
            || change.Property == MethodProperty)
        {
            LogLayoutSettingChanged(change);
            Layout = null;
            UpdateLayoutResultProperties();
            InvalidateMeasure();
        }
    }

    /// <summary><b>ItemsSource が変更されたときに呼び出されます</b></summary>
    /// <remarks>
    /// ベースクラスが子を作り直した後に実行されます。
    /// </remarks>
    protected override void OnItemsSourceChangedInternal()
    {
        base.OnItemsSourceChangedInternal();

        // Layout キャッシュを無効化し、公開している結果も戻す
        Layout = null;
        UpdateLayoutResultProperties();
    }

    /// <summary><b>ItemTemplate が変更されたときに呼び出されます</b></summary>
    /// <remarks>
    /// ベースクラスが子を作り直した後に実行されます。
    /// </remarks>
    protected override void OnItemTemplateChangedInternal()
    {
        base.OnItemTemplateChangedInternal();

        // Layout キャッシュを無効化し、公開している結果も戻す
        Layout = null;
        UpdateLayoutResultProperties();
    }

    /// <summary><b>ItemsSourceFirst が変更されたときに呼び出されます</b></summary>
    /// <remarks>
    /// ベースクラスが子を並べ直した後に実行されます。
    /// </remarks>
    protected override void OnItemsSourceFirstChangedInternal()
    {
        base.OnItemsSourceFirstChangedInternal();

        // 並びが変わると位置も変わるため、Layout キャッシュを無効化する
        Layout = null;
        UpdateLayoutResultProperties();
    }

    /// <summary>ロガーが設定された時点で生成を 1 度だけ記録する。</summary>
    private void LogFacadeCreatedOnce()
    {
        if (_createdLogged)
        {
            return;
        }

        _createdLogged = true;
        _log.LogFacadeCreated(ColumnLimit, RowSpace, ColumnSpace, Method.ToString());
    }

    /// <summary>プロパティの変更をログへ記録する。</summary>
    /// <param name="change">変更の内容。</param>
    /// <remarks>
    /// 数値は自然な小数表記（<c>0.#</c>）で整形する（同値の再設定では出力しない）。
    /// </remarks>
    private void LogLayoutSettingChanged(AvaloniaPropertyChangedEventArgs change)
    {
        var name = change.Property == ColumnLimitProperty ? nameof(ColumnLimit)
            : change.Property == RowSpaceProperty ? nameof(RowSpace)
            : change.Property == ColumnSpaceProperty ? nameof(ColumnSpace)
            : change.Property == MethodProperty ? nameof(Method)
            : "unknown";
        _log.LogSettingChanged(name, FormatSettingValue(change.OldValue), FormatSettingValue(change.NewValue));
    }

    /// <summary>プロパティの値をログ用の文字列へ整形する。</summary>
    /// <param name="value">整形する値。</param>
    /// <returns>不変カルチャの文字列。</returns>
    private static string FormatSettingValue(object? value) => value switch
    {
        null => "null",
        double number => ColumnPackingLogMessage.Format(number),
        IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "null",
    };

    /// <summary>幅の上限を決める（確定しないパスでは直前に確定した幅を使う）。</summary>
    /// <param name="availableWidth">親から提示された幅。</param>
    /// <returns>幅の上限。 まだ分からない場合は 0。</returns>
    private double ResolveWidthLimit(double availableWidth)
        => !double.IsInfinity(availableWidth) && availableWidth > 0 ? availableWidth : _lastWidthLimit;

    /// <summary><b>Layout の結果を依存関係プロパティへ反映します</b></summary>
    /// <remarks>
    /// 配置の計算の直後、および計算結果を破棄したときに呼び出します。
    /// </remarks>
    private void UpdateLayoutResultProperties()
    {
        if (Layout is null)
        {
            LayoutUsedWidth = null;
            LayoutMinHeight = null;
            LayoutColumnCount = null;
            LayoutColumnSegments = null;
            return;
        }

        var result = Layout.GetLastResult();
        LayoutUsedWidth = result.UsedWidth;
        LayoutMinHeight = result.MinHeight;

        var segments = Layout.GetLastColumnSegments();
        LayoutColumnCount = segments.Length;
        LayoutColumnSegments = "[" + string.Join(",", segments.Select(static segment => segment.EndIdx)) + "]";
    }

    /// <summary>レイアウト結果が変化していれば記録する（同じ結果の繰り返しは記録しない）。</summary>
    /// <param name="itemY">項目ごとの Y 座標（<c>Trace</c> で使う。 整形は定義側で行う）。</param>
    private void LogLayoutResultIfChanged(IReadOnlyList<double> itemY)
    {
        if (Layout is null)
        {
            return;
        }

        var result = Layout.GetLastResult();
        var segments = Layout.GetLastColumnSegments();
        var segmentsText = "[" + string.Join(",", segments.Select(static segment => segment.EndIdx)) + "]";

        // 丸めた値で比較する（レイアウトの再計算で同じ結果が繰り返し届くため、変化したときだけ記録する）
        var signature = string.Join(
            "/",
            ColumnPackingLogMessage.Format(result.UsedWidth),
            ColumnPackingLogMessage.Format(result.MinHeight),
            segmentsText);
        if (string.Equals(signature, _lastLayoutSignature, StringComparison.Ordinal))
        {
            return;
        }

        _lastLayoutSignature = signature;
        _log.LogLayoutChanged(result.UsedWidth, result.MinHeight, segments.Length, segmentsText);
        _log.LogLayoutItems(itemY);
    }
}
